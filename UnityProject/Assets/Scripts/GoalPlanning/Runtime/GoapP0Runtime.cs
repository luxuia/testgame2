using System;
using System.Collections.Generic;
using Minecraft.Combat;

namespace Minecraft.GoalPlanning
{
    internal sealed class GoapP0Runtime
    {
        private const float EmergencyThreatLevelThreshold = 1f;

        private readonly GoalSelector m_GoalSelector;
        private readonly GoapPlanner m_Planner;
        private readonly PlanExecutor m_Executor;
        private readonly IGoapReservationStore m_ReservationStore;
        private readonly string m_AgentId;
        private readonly GoapRolePreset m_RolePreset;
        private readonly GoapTelemetryBuffer m_Telemetry;

        private GoapGoalDefinition m_CurrentGoal;
        private string m_LastDeniedReason;
        private bool m_ForceImmediateReplan;
        private bool m_HasCurrentReservation;
        private GoapReservationKey m_CurrentReservation;

        public PlanExecutor Executor => m_Executor;
        public GoapTelemetryBuffer Telemetry => m_Telemetry;

        public GoapP0Runtime(
            string agentId = "agent.default",
            IGoapReservationStore reservationStore = null,
            GoapRolePreset rolePreset = GoapRolePreset.PlayerClone,
            GoapTelemetryBuffer telemetry = null)
        {
            m_GoalSelector = new GoalSelector();
            m_Planner = new GoapPlanner();
            m_Executor = new PlanExecutor();
            m_ReservationStore = reservationStore ?? new InMemoryGoapReservationStore();
            m_AgentId = string.IsNullOrWhiteSpace(agentId) ? "agent.default" : agentId;
            m_RolePreset = rolePreset;
            m_Telemetry = telemetry ?? new GoapTelemetryBuffer();
        }

        public GoapTickResult Tick(
            IReadOnlyDictionary<FactKey, float> state,
            IReadOnlyList<GoapGoalDefinition> goals,
            IReadOnlyList<GoapActionDefinition> actions,
            GoapPlannerConfig config,
            float nowSec,
            Func<GoapGoalDefinition, IReadOnlyDictionary<FactKey, float>, float> dynamicGoalScorer = null,
            Func<GoapActionDefinition, bool> actionFilter = null)
        {
            if (state == null || goals == null || actions == null || config == null)
            {
                return new GoapTickResult(null, float.MinValue, false, true, false, false, false, false, default, 0, "Invalid GOAP tick input.");
            }

            m_ReservationStore?.SweepExpired(nowSec);

            if (dynamicGoalScorer == null)
            {
                GoapEnvironmentWeights roleWeights = GoapRolePresets.GetEnvironmentWeights(m_RolePreset);
                dynamicGoalScorer = (goal, facts) => GoapGoalScoreTuning.EvaluateDynamicScore(goal, facts, roleWeights);
            }

            GoalSelectionResult selection = m_GoalSelector.SelectWithHysteresis(
                goals,
                state,
                m_CurrentGoal,
                config.GoalSwitchHysteresis,
                dynamicGoalScorer);

            // RimWorld-like priority preemption: emergency goals override normal scorer ordering.
            bool emergency = IsEmergency(state, config);
            if (emergency && TryResolveEmergencyGoal(goals, out GoapGoalDefinition emergencyGoal))
            {
                float emergencyScore = m_GoalSelector.EvaluateGoalScore(emergencyGoal, state, dynamicGoalScorer);
                if (m_CurrentGoal != emergencyGoal)
                {
                    m_Telemetry.Record(new GoapTelemetryEvent(
                        GoapTelemetryEventType.EmergencyInterrupt,
                        nowSec,
                        "Emergency preemption promoted defense goal.",
                        emergencyGoal.Type,
                        GoapActionType.Idle,
                        null));
                }

                selection = new GoalSelectionResult(emergencyGoal, emergencyScore, selection.Score, keptCurrentByHysteresis: false);
            }

            bool goalChanged = selection.Goal != m_CurrentGoal;
            if (goalChanged && selection.Goal != null)
            {
                m_Telemetry.Record(new GoapTelemetryEvent(
                    GoapTelemetryEventType.GoalSwitch,
                    nowSec,
                    "Goal switched by score selection/hysteresis.",
                    selection.Goal.Type,
                    GoapActionType.Idle,
                    null));
            }

            m_CurrentGoal = selection.Goal;

            bool isPlanStale = m_Executor.ActivePlan != null
                               && nowSec - m_Executor.ActivePlan.CreatedAtSec >= config.PlanStaleTimeoutSec;

            float scoreDelta = selection.BestAlternativeScore > float.MinValue
                ? selection.BestAlternativeScore - selection.Score
                : 0f;
            if (scoreDelta < 0f)
            {
                scoreDelta = 0f;
            }

            bool shouldReplan = goalChanged
                                || m_ForceImmediateReplan
                                || m_Executor.ActivePlan == null
                                || m_Executor.IsPlanFinished()
                                || m_Executor.ShouldReplan(
                                    nowSec,
                                    config,
                                    emergency,
                                    m_Executor.CurrentStepInterrupted,
                                    isPlanStale,
                                    scoreDelta);

            bool replanned = false;
            bool usedFallback = false;
            bool stepInterrupted = false;
            bool reservationConflict = false;

            if (shouldReplan)
            {
                m_LastDeniedReason = null;
                ReleaseCurrentReservation();

                Func<GoapActionDefinition, bool> composedFilter = ComposeFilter(
                    actionFilter,
                    nowSec,
                    denyReason => m_LastDeniedReason = denyReason);

                GoapPlan plan = m_Planner.BuildPlan(state, m_CurrentGoal, actions, config, nowSec, composedFilter);
                replanned = true;
                m_ForceImmediateReplan = false;

                if (plan == null || plan.Steps.Count == 0)
                {
                    plan = GoapFallbackFactory.CreateFallbackPlan(emergency, nowSec);
                    usedFallback = true;
                }

                m_Executor.SetPlan(plan, nowSec);
            }

            bool hasStep = m_Executor.TryGetCurrentStep(out GoapPlanStep step);
            if (hasStep && !m_Executor.CurrentStepInProgress)
            {
                // Toil-like small-step execution: each tick executes one interruptible step.
                m_Executor.BeginCurrentStep();
            }

            if (emergency
                && m_Executor.CurrentStepInProgress
                && !IsEmergencyCompatibleStep(step.Action))
            {
                m_Executor.MarkCurrentStepInterrupted();
                m_ForceImmediateReplan = true;
                stepInterrupted = true;
                hasStep = false;
                step = default;
            }

            if (hasStep
                && !TryReserveCurrentStep(step, nowSec, config, out string reservationReason))
            {
                string reservationKey = step.Action != null && step.Action.TryGetReservationKey(out GoapReservationKey deniedKey)
                    ? deniedKey.ToString()
                    : string.Empty;
                // Reservation borrowing: prevent multi-agent conflicts on the same actionable target.
                m_Telemetry.Record(new GoapTelemetryEvent(
                    GoapTelemetryEventType.ReservationConflict,
                    nowSec,
                    reservationReason,
                    m_CurrentGoal != null ? m_CurrentGoal.Type : GoapGoalType.Recover,
                    step.Action != null ? step.Action.Type : GoapActionType.Idle,
                    reservationKey));
                m_LastDeniedReason = reservationReason;
                reservationConflict = true;
                m_ForceImmediateReplan = true;
                hasStep = false;
                step = default;
            }

            return new GoapTickResult(
                m_CurrentGoal,
                selection.Score,
                replanned,
                usedFallback,
                emergency,
                stepInterrupted,
                reservationConflict,
                hasStep,
                step,
                m_Planner.LastExpandedNodes,
                m_LastDeniedReason);
        }

        public void NotifyCurrentStepSucceeded()
        {
            ReleaseCurrentReservation();
            m_Executor.MarkCurrentStepSucceeded();
        }

        public void NotifyCurrentStepFailed(GoapPlannerConfig config)
        {
            ReleaseCurrentReservation();
            GoapStepFailureOutcome outcome = m_Executor.MarkCurrentStepFailed(config);
            if (outcome == GoapStepFailureOutcome.ReplanAndReassign)
            {
                m_ForceImmediateReplan = true;
            }
        }

        public void ReleaseAllReservations()
        {
            ReleaseCurrentReservation();
            m_ReservationStore?.ReleaseAllForOwner(m_AgentId);
        }

        private Func<GoapActionDefinition, bool> ComposeFilter(
            Func<GoapActionDefinition, bool> externalFilter,
            float nowSec,
            Action<string> onDenied)
        {
            return action =>
            {
                if (action == null)
                {
                    onDenied?.Invoke("Action is null.");
                    return false;
                }

                if (action.TryGetReservationKey(out GoapReservationKey key)
                    && m_ReservationStore != null
                    && m_ReservationStore.IsReservedByOther(in key, m_AgentId, nowSec))
                {
                    onDenied?.Invoke($"Reservation denied: {key}");
                    return false;
                }

                if (externalFilter != null && !externalFilter(action))
                {
                    onDenied?.Invoke("Action filtered by legality/authority gate.");
                    return false;
                }

                return true;
            };
        }

        private static bool IsEmergency(IReadOnlyDictionary<FactKey, float> state, GoapPlannerConfig config)
        {
            if (state == null || config == null)
            {
                return false;
            }

            bool coreEmergency = state.TryGetValue(FactKey.CoreHpPct, out float coreHpPct)
                                 && coreHpPct <= config.EmergencyOverrideCoreHpPct;
            bool threatEmergency = state.TryGetValue(FactKey.ThreatLevel, out float threatLevel)
                                   && threatLevel >= EmergencyThreatLevelThreshold;
            return coreEmergency || threatEmergency;
        }

        private static bool TryResolveEmergencyGoal(IReadOnlyList<GoapGoalDefinition> goals, out GoapGoalDefinition emergencyGoal)
        {
            emergencyGoal = null;
            if (goals == null)
            {
                return false;
            }

            float bestScore = float.MinValue;
            for (int i = 0; i < goals.Count; i++)
            {
                GoapGoalDefinition goal = goals[i];
                if (goal == null)
                {
                    continue;
                }

                bool isEmergencyGoal = goal.Type == GoapGoalType.DefendCore || goal.Type == GoapGoalType.Recover;
                if (!isEmergencyGoal)
                {
                    continue;
                }

                if (goal.BasePriority <= bestScore)
                {
                    continue;
                }

                bestScore = goal.BasePriority;
                emergencyGoal = goal;
            }

            return emergencyGoal != null;
        }

        private bool TryReserveCurrentStep(in GoapPlanStep step, float nowSec, GoapPlannerConfig config, out string denyReason)
        {
            denyReason = null;

            if (m_ReservationStore == null || step.Action == null || !step.Action.TryGetReservationKey(out GoapReservationKey key))
            {
                return true;
            }

            if (m_HasCurrentReservation && m_CurrentReservation.Equals(key))
            {
                return true;
            }

            if (m_ReservationStore.IsReservedByOther(in key, m_AgentId, nowSec))
            {
                denyReason = $"Reservation conflict: {key}";
                return false;
            }

            float timeoutSec = Math.Max(0.25f, config.PlanStaleTimeoutSec);
            if (!m_ReservationStore.TryClaim(in key, m_AgentId, nowSec, timeoutSec, out _))
            {
                denyReason = $"Reservation claim failed: {key}";
                return false;
            }

            ReleaseCurrentReservation();
            m_HasCurrentReservation = true;
            m_CurrentReservation = key;
            return true;
        }

        private void ReleaseCurrentReservation()
        {
            if (!m_HasCurrentReservation || m_ReservationStore == null)
            {
                return;
            }

            m_ReservationStore.Release(in m_CurrentReservation, m_AgentId);
            m_HasCurrentReservation = false;
            m_CurrentReservation = default;
        }

        private static bool IsEmergencyCompatibleStep(GoapActionDefinition action)
        {
            if (action == null)
            {
                return false;
            }

            return action.Type == GoapActionType.Retreat
                   || action.Type == GoapActionType.RepairCore
                   || action.Type == GoapActionType.HoldPosition
                   || action.Type == GoapActionType.AttackTarget;
        }
    }
}

