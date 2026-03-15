using System;
using System.Collections.Generic;
using Minecraft.Combat;

namespace Minecraft.GoalPlanning
{
    public enum FactKey
    {
        CoreHpPct = 0,
        ThreatLevel = 1,
        ResourceNeed = 2,
        BuildNeed = 3,
        IsInHomeTerritory = 4,
        HasPortableFungusCharge = 5,
        AgentHpPct = 6,
        TargetReachable = 7
    }

    public enum CompareOp
    {
        Less = 0,
        LessOrEqual = 1,
        Equal = 2,
        GreaterOrEqual = 3,
        Greater = 4
    }

    public enum EffectOp
    {
        Set = 0,
        Add = 1,
        Multiply = 2
    }

    public enum GoapGoalType
    {
        DefendCore = 0,
        ExpandFungus = 1,
        BuildDefense = 2,
        MineResource = 3,
        AttackThreat = 4,
        Recover = 5
    }

    public enum GoapActionType
    {
        MoveTo = 0,
        BreakBlock = 1,
        PlaceBlock = 2,
        ConvertToFungus = 3,
        BuildFunctionalBlock = 4,
        RepairCore = 5,
        AttackTarget = 6,
        Retreat = 7,
        GatherResource = 8,
        SummonUnit = 9,
        HoldPosition = 10,
        Idle = 11
    }

    public enum GoapReservationKind
    {
        None = 0,
        Block = 1,
        Slot = 2,
        RepairPoint = 3
    }

    public enum GoapRolePreset
    {
        PlayerClone = 0,
        Deputy = 1,
        Puji = 2
    }

    public readonly struct GoapCondition
    {
        public readonly FactKey Key;
        public readonly CompareOp Op;
        public readonly float Value;

        public GoapCondition(FactKey key, CompareOp op, float value)
        {
            Key = key;
            Op = op;
            Value = value;
        }

        public bool Evaluate(IReadOnlyDictionary<FactKey, float> state)
        {
            if (state == null || !state.TryGetValue(Key, out float current))
            {
                current = 0f;
            }

            switch (Op)
            {
                case CompareOp.Less:
                    return current < Value;
                case CompareOp.LessOrEqual:
                    return current <= Value;
                case CompareOp.Equal:
                    return Math.Abs(current - Value) <= 0.0001f;
                case CompareOp.GreaterOrEqual:
                    return current >= Value;
                case CompareOp.Greater:
                    return current > Value;
                default:
                    return false;
            }
        }
    }

    public readonly struct GoapEffect
    {
        public readonly FactKey Key;
        public readonly EffectOp Op;
        public readonly float Value;

        public GoapEffect(FactKey key, EffectOp op, float value)
        {
            Key = key;
            Op = op;
            Value = value;
        }

        public void Apply(IDictionary<FactKey, float> state)
        {
            if (state == null)
            {
                return;
            }

            if (!state.TryGetValue(Key, out float current))
            {
                current = 0f;
            }

            switch (Op)
            {
                case EffectOp.Set:
                    state[Key] = Value;
                    break;
                case EffectOp.Add:
                    state[Key] = current + Value;
                    break;
                case EffectOp.Multiply:
                    state[Key] = current * Value;
                    break;
            }
        }
    }

    public sealed class GoapActionDefinition
    {
        public string Id;
        public GoapActionType Type;
        public float BaseCost = 1f;
        public GoapReservationKind ReservationKind = GoapReservationKind.None;
        public string ReservationTargetId;
        public string CooldownTag;
        public List<string> InterruptTags = new List<string>();
        public List<GoapCondition> Preconditions = new List<GoapCondition>();
        public List<GoapEffect> Effects = new List<GoapEffect>();

        public bool IsApplicable(IReadOnlyDictionary<FactKey, float> state)
        {
            for (int i = 0; i < Preconditions.Count; i++)
            {
                if (!Preconditions[i].Evaluate(state))
                {
                    return false;
                }
            }

            return true;
        }

        public bool IsTerrainAction()
        {
            return Type == GoapActionType.BreakBlock
                   || Type == GoapActionType.PlaceBlock
                   || Type == GoapActionType.ConvertToFungus;
        }

        public bool TryGetReservationKey(out GoapReservationKey key)
        {
            if (ReservationKind == GoapReservationKind.None || string.IsNullOrWhiteSpace(ReservationTargetId))
            {
                key = default;
                return false;
            }

            key = new GoapReservationKey(ReservationKind, ReservationTargetId);
            return true;
        }
    }

    public sealed class GoapGoalDefinition
    {
        public GoapGoalType Type;
        public float BasePriority = 1f;
        public List<GoapCondition> DesiredState = new List<GoapCondition>();

        public bool IsSatisfied(IReadOnlyDictionary<FactKey, float> state)
        {
            for (int i = 0; i < DesiredState.Count; i++)
            {
                if (!DesiredState[i].Evaluate(state))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public readonly struct GoapPlanStep
    {
        public readonly GoapActionDefinition Action;
        public readonly float Cost;

        public GoapPlanStep(GoapActionDefinition action, float cost)
        {
            Action = action;
            Cost = cost;
        }
    }

    public sealed class GoapPlan
    {
        public readonly List<GoapPlanStep> Steps;
        public readonly float TotalCost;
        public readonly float CreatedAtSec;

        public GoapPlan(List<GoapPlanStep> steps, float totalCost, float createdAtSec)
        {
            Steps = steps ?? new List<GoapPlanStep>();
            TotalCost = totalCost;
            CreatedAtSec = createdAtSec;
        }
    }

    public sealed class GoapPlannerConfig
    {
        public int MaxPlanDepth = 3;
        public float ReplanCooldownSec = 0.25f;
        public float GoalSwitchHysteresis = 1f;
        public int PlannerBudgetPerTick = 64;
        public float EmergencyOverrideCoreHpPct = 0.2f;
        public int ActionFailureRetryLimit = 2;
        public float PlanStaleTimeoutSec = 1.5f;
        public float HomeBiasWeight = 1f;
        public float AwaySafetyWeight = 1f;
    }

    public static class GoapConfigContract
    {
        public static readonly HashSet<string> ApprovedTopLevelKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "maxPlanDepth",
            "replanCooldownSec",
            "goalSwitchHysteresis",
            "plannerBudgetPerTick",
            "emergencyOverrideCoreHpPct",
            "actionFailureRetryLimit",
            "planStaleTimeoutSec",
            "homeBiasWeight",
            "awaySafetyWeight"
        };

        public static bool ValidateTopLevelKeys(IEnumerable<string> keys, out string error)
        {
            error = null;

            if (keys == null)
            {
                error = "GOAP config keys are null.";
                return false;
            }

            HashSet<string> set = new HashSet<string>(keys, StringComparer.Ordinal);

            foreach (string approved in ApprovedTopLevelKeys)
            {
                if (!set.Contains(approved))
                {
                    error = $"Missing required GOAP config key: {approved}";
                    return false;
                }
            }

            foreach (string key in set)
            {
                if (!ApprovedTopLevelKeys.Contains(key))
                {
                    error = $"Out-of-scope GOAP config key: {key}";
                    return false;
                }
            }

            return true;
        }
    }

    public readonly struct GoalSelectionResult
    {
        public readonly GoapGoalDefinition Goal;
        public readonly float Score;
        public readonly float BestAlternativeScore;
        public readonly bool KeptCurrentByHysteresis;

        public GoalSelectionResult(GoapGoalDefinition goal, float score, float bestAlternativeScore, bool keptCurrentByHysteresis = false)
        {
            Goal = goal;
            Score = score;
            BestAlternativeScore = bestAlternativeScore;
            KeptCurrentByHysteresis = keptCurrentByHysteresis;
        }
    }

    public readonly struct GoapLegalityContext
    {
        public readonly CombatContext CombatContext;

        public GoapLegalityContext(CombatContext combatContext)
        {
            CombatContext = combatContext;
        }
    }

    public interface IGoapActionLegalityGate
    {
        bool IsLegal(GoapActionType actionType, in GoapLegalityContext context, out string denyReason);
    }

    public sealed class CombatAuthorityLegalityGate : IGoapActionLegalityGate
    {
        public bool IsLegal(GoapActionType actionType, in GoapLegalityContext context, out string denyReason)
        {
            if (actionType == GoapActionType.BreakBlock
                || actionType == GoapActionType.PlaceBlock
                || actionType == GoapActionType.ConvertToFungus)
            {
                if (!context.CombatContext.CanEditTerrain())
                {
                    denyReason = "Terrain action denied by home/away authority gate.";
                    return false;
                }
            }

            denyReason = null;
            return true;
        }
    }

    public static class GoapBlackboardBuilder
    {
        public static Dictionary<FactKey, float> Build(
            in BattleDirectorSnapshot snapshot,
            in CombatContext combatContext,
            float resourceNeed,
            float buildNeed,
            float agentHpPct,
            bool targetReachable,
            bool hasPortableFungusCharge)
        {
            Dictionary<FactKey, float> state = new Dictionary<FactKey, float>(8)
            {
                [FactKey.CoreHpPct] = snapshot.CoreHealthNormalized,
                [FactKey.ThreatLevel] = snapshot.IsUnderHeavyAttack ? 1f : 0f,
                [FactKey.ResourceNeed] = resourceNeed,
                [FactKey.BuildNeed] = buildNeed,
                [FactKey.IsInHomeTerritory] = combatContext.TerritoryKind == CombatTerritoryKind.Home ? 1f : 0f,
                [FactKey.HasPortableFungusCharge] = hasPortableFungusCharge ? 1f : 0f,
                [FactKey.AgentHpPct] = agentHpPct,
                [FactKey.TargetReachable] = targetReachable ? 1f : 0f,
            };
            return state;
        }
    }

    public static class GoapActionFilters
    {
        public static Func<GoapActionDefinition, bool> CreateAuthorityFilter(
            IGoapActionLegalityGate gate,
            GoapLegalityContext context,
            Action<string> onDenied = null)
        {
            if (gate == null)
            {
                return _ => true;
            }

            GoapLegalityContext capturedContext = context;

            return action =>
            {
                if (action == null)
                {
                    return false;
                }

                if (gate.IsLegal(action.Type, in capturedContext, out string denyReason))
                {
                    return true;
                }

                onDenied?.Invoke(denyReason);
                return false;
            };
        }
    }

    public readonly struct GoapReservationKey : IEquatable<GoapReservationKey>
    {
        public readonly GoapReservationKind Kind;
        public readonly string TargetId;

        public GoapReservationKey(GoapReservationKind kind, string targetId)
        {
            Kind = kind;
            TargetId = targetId ?? string.Empty;
        }

        public bool Equals(GoapReservationKey other)
        {
            return Kind == other.Kind && string.Equals(TargetId, other.TargetId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is GoapReservationKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Kind * 397) ^ StringComparer.Ordinal.GetHashCode(TargetId);
            }
        }

        public override string ToString()
        {
            return $"{Kind}:{TargetId}";
        }
    }

    public readonly struct GoapReservationClaim
    {
        public readonly string OwnerId;
        public readonly GoapReservationKey Key;
        public readonly float ClaimedAtSec;
        public readonly float ExpiresAtSec;

        public GoapReservationClaim(string ownerId, in GoapReservationKey key, float claimedAtSec, float expiresAtSec)
        {
            OwnerId = ownerId;
            Key = key;
            ClaimedAtSec = claimedAtSec;
            ExpiresAtSec = expiresAtSec;
        }

        public bool IsExpired(float nowSec)
        {
            return nowSec >= ExpiresAtSec;
        }
    }

    public interface IGoapReservationStore
    {
        bool TryClaim(in GoapReservationKey key, string ownerId, float nowSec, float timeoutSec, out GoapReservationClaim claim);
        void Release(in GoapReservationKey key, string ownerId);
        void ReleaseAllForOwner(string ownerId);
        bool IsReservedByOther(in GoapReservationKey key, string ownerId, float nowSec);
        int SweepExpired(float nowSec);
    }

    public sealed class InMemoryGoapReservationStore : IGoapReservationStore
    {
        private readonly Dictionary<GoapReservationKey, GoapReservationClaim> m_Claims
            = new Dictionary<GoapReservationKey, GoapReservationClaim>();

        public bool TryClaim(in GoapReservationKey key, string ownerId, float nowSec, float timeoutSec, out GoapReservationClaim claim)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                claim = default;
                return false;
            }

            SweepExpired(nowSec);

            if (m_Claims.TryGetValue(key, out GoapReservationClaim current))
            {
                if (!string.Equals(current.OwnerId, ownerId, StringComparison.Ordinal))
                {
                    claim = current;
                    return false;
                }
            }

            float expireAt = nowSec + Math.Max(0.05f, timeoutSec);
            claim = new GoapReservationClaim(ownerId, key, nowSec, expireAt);
            m_Claims[key] = claim;
            return true;
        }

        public void Release(in GoapReservationKey key, string ownerId)
        {
            if (!m_Claims.TryGetValue(key, out GoapReservationClaim current))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(ownerId) || string.Equals(current.OwnerId, ownerId, StringComparison.Ordinal))
            {
                m_Claims.Remove(key);
            }
        }

        public void ReleaseAllForOwner(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return;
            }

            List<GoapReservationKey> toRemove = null;
            foreach (KeyValuePair<GoapReservationKey, GoapReservationClaim> pair in m_Claims)
            {
                if (!string.Equals(pair.Value.OwnerId, ownerId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (toRemove == null)
                {
                    toRemove = new List<GoapReservationKey>();
                }

                toRemove.Add(pair.Key);
            }

            if (toRemove == null)
            {
                return;
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                m_Claims.Remove(toRemove[i]);
            }
        }

        public bool IsReservedByOther(in GoapReservationKey key, string ownerId, float nowSec)
        {
            if (!m_Claims.TryGetValue(key, out GoapReservationClaim claim))
            {
                return false;
            }

            if (claim.IsExpired(nowSec))
            {
                m_Claims.Remove(key);
                return false;
            }

            return !string.Equals(claim.OwnerId, ownerId, StringComparison.Ordinal);
        }

        public int SweepExpired(float nowSec)
        {
            List<GoapReservationKey> expired = null;
            foreach (KeyValuePair<GoapReservationKey, GoapReservationClaim> pair in m_Claims)
            {
                if (!pair.Value.IsExpired(nowSec))
                {
                    continue;
                }

                if (expired == null)
                {
                    expired = new List<GoapReservationKey>();
                }

                expired.Add(pair.Key);
            }

            if (expired == null)
            {
                return 0;
            }

            for (int i = 0; i < expired.Count; i++)
            {
                m_Claims.Remove(expired[i]);
            }

            return expired.Count;
        }
    }

    public enum GoapTelemetryEventType
    {
        GoalSwitch = 0,
        EmergencyInterrupt = 1,
        ReservationConflict = 2
    }

    public readonly struct GoapTelemetryEvent
    {
        public readonly GoapTelemetryEventType Type;
        public readonly float TimeSec;
        public readonly string Message;
        public readonly GoapGoalType GoalType;
        public readonly GoapActionType ActionType;
        public readonly string ReservationKey;

        public GoapTelemetryEvent(
            GoapTelemetryEventType type,
            float timeSec,
            string message,
            GoapGoalType goalType,
            GoapActionType actionType,
            string reservationKey)
        {
            Type = type;
            TimeSec = timeSec;
            Message = message;
            GoalType = goalType;
            ActionType = actionType;
            ReservationKey = reservationKey;
        }
    }

    public sealed class GoapTelemetryBuffer
    {
        private readonly Queue<GoapTelemetryEvent> m_Buffer;
        private readonly int m_Capacity;

        public GoapTelemetryBuffer(int capacity = 64)
        {
            m_Capacity = Math.Max(8, capacity);
            m_Buffer = new Queue<GoapTelemetryEvent>(m_Capacity);
        }

        public void Record(in GoapTelemetryEvent evt)
        {
            if (m_Buffer.Count >= m_Capacity)
            {
                m_Buffer.Dequeue();
            }

            m_Buffer.Enqueue(evt);
        }

        public GoapTelemetryEvent[] Snapshot()
        {
            return m_Buffer.ToArray();
        }

        public void Clear()
        {
            m_Buffer.Clear();
        }
    }

    public sealed class GoapTelemetryPanel
    {
        private readonly GoapTelemetryBuffer m_Source;

        public GoapTelemetryPanel(GoapTelemetryBuffer source)
        {
            m_Source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public string[] BuildRows(int maxRows = 12)
        {
            GoapTelemetryEvent[] snapshot = m_Source.Snapshot();
            if (snapshot.Length == 0)
            {
                return Array.Empty<string>();
            }

            int count = Math.Max(1, maxRows);
            int start = Math.Max(0, snapshot.Length - count);
            List<string> rows = new List<string>(snapshot.Length - start);
            for (int i = start; i < snapshot.Length; i++)
            {
                GoapTelemetryEvent evt = snapshot[i];
                rows.Add(
                    $"{evt.TimeSec:0.00}s | {evt.Type} | goal={evt.GoalType} action={evt.ActionType} | {evt.Message}");
            }

            return rows.ToArray();
        }
    }

    public readonly struct GoapEnvironmentWeights
    {
        public readonly float ThreatPressure;
        public readonly float ResourcePressure;
        public readonly float BuildPressure;

        public GoapEnvironmentWeights(float threatPressure, float resourcePressure, float buildPressure)
        {
            ThreatPressure = threatPressure;
            ResourcePressure = resourcePressure;
            BuildPressure = buildPressure;
        }
    }

    public static class GoapRolePresets
    {
        public static GoapPlannerConfig CreatePlannerConfig(GoapRolePreset role)
        {
            GoapPlannerConfig config = new GoapPlannerConfig();
            switch (role)
            {
                case GoapRolePreset.Deputy:
                    config.HomeBiasWeight = 1.2f;
                    config.ReplanCooldownSec = 0.2f;
                    config.GoalSwitchHysteresis = 0.85f;
                    break;
                case GoapRolePreset.Puji:
                    config.HomeBiasWeight = 0.8f;
                    config.ReplanCooldownSec = 0.35f;
                    config.GoalSwitchHysteresis = 1.15f;
                    break;
                default:
                    config.HomeBiasWeight = 1f;
                    config.ReplanCooldownSec = 0.25f;
                    config.GoalSwitchHysteresis = 1f;
                    break;
            }

            return config;
        }

        public static GoapEnvironmentWeights GetEnvironmentWeights(GoapRolePreset role)
        {
            switch (role)
            {
                case GoapRolePreset.Deputy:
                    return new GoapEnvironmentWeights(1.1f, 1f, 1.15f);
                case GoapRolePreset.Puji:
                    return new GoapEnvironmentWeights(0.9f, 1.2f, 0.9f);
                default:
                    return new GoapEnvironmentWeights(1f, 1f, 1f);
            }
        }
    }

    public static class GoapGoalScoreTuning
    {
        public static float EvaluateDynamicScore(
            GoapGoalDefinition goal,
            IReadOnlyDictionary<FactKey, float> state,
            GoapEnvironmentWeights weights)
        {
            if (goal == null || state == null)
            {
                return 0f;
            }

            float threat = GetFact(state, FactKey.ThreatLevel);
            float resourceNeed = GetFact(state, FactKey.ResourceNeed);
            float buildNeed = GetFact(state, FactKey.BuildNeed);
            float coreHp = GetFact(state, FactKey.CoreHpPct);

            switch (goal.Type)
            {
                case GoapGoalType.DefendCore:
                    return (1f - coreHp) * 4f + threat * 3f * weights.ThreatPressure;
                case GoapGoalType.BuildDefense:
                    return buildNeed * 2f * weights.BuildPressure + threat * weights.ThreatPressure;
                case GoapGoalType.MineResource:
                    return resourceNeed * 2f * weights.ResourcePressure;
                case GoapGoalType.AttackThreat:
                    return threat * 2f * weights.ThreatPressure;
                case GoapGoalType.Recover:
                    return (1f - GetFact(state, FactKey.AgentHpPct)) * 2f;
                default:
                    return 0f;
            }
        }

        private static float GetFact(IReadOnlyDictionary<FactKey, float> state, FactKey key)
        {
            return state.TryGetValue(key, out float value) ? value : 0f;
        }
    }

    public sealed class GoalSelector
    {
        public float EvaluateGoalScore(
            GoapGoalDefinition goal,
            IReadOnlyDictionary<FactKey, float> state,
            Func<GoapGoalDefinition, IReadOnlyDictionary<FactKey, float>, float> dynamicScorer = null)
        {
            if (goal == null)
            {
                return float.MinValue;
            }

            float score = goal.BasePriority;
            if (dynamicScorer != null)
            {
                score += dynamicScorer(goal, state);
            }

            return score;
        }

        public GoalSelectionResult SelectBestGoal(
            IReadOnlyList<GoapGoalDefinition> goals,
            IReadOnlyDictionary<FactKey, float> state,
            Func<GoapGoalDefinition, IReadOnlyDictionary<FactKey, float>, float> dynamicScorer = null)
        {
            GoapGoalDefinition best = null;
            float bestScore = float.MinValue;

            if (goals == null)
            {
                return new GoalSelectionResult(null, bestScore, bestScore);
            }

            for (int i = 0; i < goals.Count; i++)
            {
                GoapGoalDefinition goal = goals[i];
                if (goal == null)
                {
                    continue;
                }

                float score = EvaluateGoalScore(goal, state, dynamicScorer);
                if (score > bestScore)
                {
                    best = goal;
                    bestScore = score;
                }
            }

            return new GoalSelectionResult(best, bestScore, bestScore);
        }

        public GoalSelectionResult SelectWithHysteresis(
            IReadOnlyList<GoapGoalDefinition> goals,
            IReadOnlyDictionary<FactKey, float> state,
            GoapGoalDefinition currentGoal,
            float hysteresis,
            Func<GoapGoalDefinition, IReadOnlyDictionary<FactKey, float>, float> dynamicScorer = null)
        {
            GoalSelectionResult best = SelectBestGoal(goals, state, dynamicScorer);
            if (best.Goal == null || currentGoal == null)
            {
                return best;
            }

            float currentScore = EvaluateGoalScore(currentGoal, state, dynamicScorer);
            if (best.Score <= currentScore + Math.Max(0f, hysteresis))
            {
                return new GoalSelectionResult(currentGoal, currentScore, best.Score, keptCurrentByHysteresis: true);
            }

            return best;
        }
    }

    public sealed class GoapPlanner
    {
        private GoapPlan m_BestPlan;
        private float m_BestCost;
        private int m_ExpandedNodes;
        private int m_Budget;
        private int m_MaxDepth;
        private float m_NowSec;
        private Func<GoapActionDefinition, bool> m_ActionFilter;

        public int LastExpandedNodes => m_ExpandedNodes;

        public GoapPlan BuildPlan(
            IReadOnlyDictionary<FactKey, float> initialState,
            GoapGoalDefinition goal,
            IReadOnlyList<GoapActionDefinition> actions,
            GoapPlannerConfig config,
            float nowSec,
            Func<GoapActionDefinition, bool> actionFilter = null)
        {
            if (initialState == null || goal == null || actions == null || config == null)
            {
                return null;
            }

            if (goal.IsSatisfied(initialState))
            {
                return new GoapPlan(new List<GoapPlanStep>(0), 0f, nowSec);
            }

            m_BestPlan = null;
            m_BestCost = float.MaxValue;
            m_ExpandedNodes = 0;
            m_Budget = Math.Max(1, config.PlannerBudgetPerTick);
            m_MaxDepth = ResolveSearchDepth(config);
            m_NowSec = nowSec;
            m_ActionFilter = actionFilter;

            List<GoapPlanStep> steps = new List<GoapPlanStep>(m_MaxDepth);
            Dictionary<FactKey, float> mutableState = CloneState(initialState);
            Search(mutableState, goal, actions, steps, 0f, 0);
            return m_BestPlan;
        }

        private void Search(
            Dictionary<FactKey, float> state,
            GoapGoalDefinition goal,
            IReadOnlyList<GoapActionDefinition> actions,
            List<GoapPlanStep> steps,
            float totalCost,
            int depth)
        {
            if (goal.IsSatisfied(state))
            {
                if (totalCost < m_BestCost)
                {
                    m_BestCost = totalCost;
                    m_BestPlan = new GoapPlan(new List<GoapPlanStep>(steps), totalCost, m_NowSec);
                }
                return;
            }

            if (depth >= m_MaxDepth || m_ExpandedNodes >= m_Budget || totalCost >= m_BestCost)
            {
                return;
            }

            for (int i = 0; i < actions.Count; i++)
            {
                GoapActionDefinition action = actions[i];
                if (action == null || !action.IsApplicable(state))
                {
                    continue;
                }

                if (m_ActionFilter != null && !m_ActionFilter(action))
                {
                    continue;
                }

                m_ExpandedNodes++;
                if (m_ExpandedNodes > m_Budget)
                {
                    return;
                }

                Dictionary<FactKey, float> next = CloneState(state);
                ApplyEffects(next, action.Effects);

                float nextCost = totalCost + Math.Max(0f, action.BaseCost);
                steps.Add(new GoapPlanStep(action, action.BaseCost));
                Search(next, goal, actions, steps, nextCost, depth + 1);
                steps.RemoveAt(steps.Count - 1);
            }
        }

        private static void ApplyEffects(Dictionary<FactKey, float> state, IReadOnlyList<GoapEffect> effects)
        {
            if (effects == null)
            {
                return;
            }

            for (int i = 0; i < effects.Count; i++)
            {
                effects[i].Apply(state);
            }
        }

        private static Dictionary<FactKey, float> CloneState(IReadOnlyDictionary<FactKey, float> state)
        {
            Dictionary<FactKey, float> copy = new Dictionary<FactKey, float>();
            foreach (KeyValuePair<FactKey, float> kv in state)
            {
                copy[kv.Key] = kv.Value;
            }

            return copy;
        }

        private static int ResolveSearchDepth(GoapPlannerConfig config)
        {
            int baseDepth = Math.Max(1, config.MaxPlanDepth);

            // Keep long-chain optimization strictly budget-gated to stay within MVP cost envelope.
            if (config.PlannerBudgetPerTick < 128)
            {
                return baseDepth;
            }

            return Math.Min(baseDepth + 2, 8);
        }
    }

    public enum GoapStepFailureOutcome
    {
        RetryCurrentStep = 0,
        ReplanAndReassign = 1
    }

    public sealed class PlanExecutor
    {
        private GoapPlan m_ActivePlan;
        private int m_CurrentIndex;
        private float m_LastReplanAtSec;
        private int m_ConsecutiveFailures;
        private bool m_CurrentStepInProgress;
        private bool m_CurrentStepInterrupted;

        public GoapPlan ActivePlan => m_ActivePlan;
        public int CurrentIndex => m_CurrentIndex;
        public float LastReplanAtSec => m_LastReplanAtSec;
        public int ConsecutiveFailures => m_ConsecutiveFailures;
        public bool CurrentStepInProgress => m_CurrentStepInProgress;
        public bool CurrentStepInterrupted => m_CurrentStepInterrupted;

        public void SetPlan(GoapPlan plan, float nowSec)
        {
            m_ActivePlan = plan;
            m_CurrentIndex = 0;
            m_LastReplanAtSec = nowSec;
            m_ConsecutiveFailures = 0;
            m_CurrentStepInProgress = false;
            m_CurrentStepInterrupted = false;
        }

        public void ClearPlan()
        {
            m_ActivePlan = null;
            m_CurrentIndex = 0;
            m_CurrentStepInProgress = false;
            m_CurrentStepInterrupted = false;
        }

        public bool TryGetCurrentStep(out GoapPlanStep step)
        {
            if (m_ActivePlan == null || m_CurrentIndex < 0 || m_CurrentIndex >= m_ActivePlan.Steps.Count)
            {
                step = default;
                return false;
            }

            step = m_ActivePlan.Steps[m_CurrentIndex];
            return true;
        }

        public bool BeginCurrentStep()
        {
            if (!TryGetCurrentStep(out _))
            {
                return false;
            }

            m_CurrentStepInProgress = true;
            m_CurrentStepInterrupted = false;
            return true;
        }

        public void MarkCurrentStepInterrupted()
        {
            m_CurrentStepInProgress = false;
            m_CurrentStepInterrupted = true;
        }

        public void MarkCurrentStepSucceeded()
        {
            if (m_ActivePlan == null)
            {
                return;
            }

            m_CurrentIndex++;
            m_ConsecutiveFailures = 0;
            m_CurrentStepInProgress = false;
            m_CurrentStepInterrupted = false;
        }

        public GoapStepFailureOutcome MarkCurrentStepFailed(GoapPlannerConfig config)
        {
            m_ConsecutiveFailures++;
            m_CurrentStepInProgress = false;
            m_CurrentStepInterrupted = false;

            if (config != null && m_ConsecutiveFailures > config.ActionFailureRetryLimit)
            {
                return GoapStepFailureOutcome.ReplanAndReassign;
            }

            return GoapStepFailureOutcome.RetryCurrentStep;
        }

        public bool IsPlanFinished()
        {
            return m_ActivePlan == null || m_CurrentIndex >= m_ActivePlan.Steps.Count;
        }

        public bool ShouldReplan(
            float nowSec,
            GoapPlannerConfig config,
            bool emergency,
            bool hasInterrupt,
            bool isPlanStale,
            float scoreDeltaToBestCandidate)
        {
            if (config == null)
            {
                return true;
            }

            if (emergency || hasInterrupt || isPlanStale || m_ConsecutiveFailures > config.ActionFailureRetryLimit)
            {
                return true;
            }

            if (nowSec - m_LastReplanAtSec < config.ReplanCooldownSec)
            {
                return false;
            }

            return scoreDeltaToBestCandidate > config.GoalSwitchHysteresis;
        }
    }

    public static class GoapFallbackFactory
    {
        private static readonly GoapActionDefinition s_EmergencyRetreatAction = new GoapActionDefinition
        {
            Id = "fallback.retreat",
            Type = GoapActionType.Retreat,
            BaseCost = 0f
        };

        private static readonly GoapActionDefinition s_IdleAction = new GoapActionDefinition
        {
            Id = "fallback.idle",
            Type = GoapActionType.Idle,
            BaseCost = 0f
        };

        public static GoapPlan CreateFallbackPlan(bool emergency, float nowSec)
        {
            GoapActionDefinition action = emergency ? s_EmergencyRetreatAction : s_IdleAction;
            List<GoapPlanStep> steps = new List<GoapPlanStep>(1)
            {
                new GoapPlanStep(action, 0f)
            };
            return new GoapPlan(steps, 0f, nowSec);
        }
    }

    public readonly struct GoapTickResult
    {
        public readonly GoapGoalDefinition SelectedGoal;
        public readonly float SelectedGoalScore;
        public readonly bool Replanned;
        public readonly bool UsedFallbackPlan;
        public readonly bool EmergencyOverrideTriggered;
        public readonly bool StepInterrupted;
        public readonly bool ReservationConflict;
        public readonly bool HasStep;
        public readonly GoapPlanStep CurrentStep;
        public readonly int ExpandedNodes;
        public readonly string LastDeniedReason;

        public GoapTickResult(
            GoapGoalDefinition selectedGoal,
            float selectedGoalScore,
            bool replanned,
            bool usedFallbackPlan,
            bool emergencyOverrideTriggered,
            bool stepInterrupted,
            bool reservationConflict,
            bool hasStep,
            in GoapPlanStep currentStep,
            int expandedNodes,
            string lastDeniedReason)
        {
            SelectedGoal = selectedGoal;
            SelectedGoalScore = selectedGoalScore;
            Replanned = replanned;
            UsedFallbackPlan = usedFallbackPlan;
            EmergencyOverrideTriggered = emergencyOverrideTriggered;
            StepInterrupted = stepInterrupted;
            ReservationConflict = reservationConflict;
            HasStep = hasStep;
            CurrentStep = currentStep;
            ExpandedNodes = expandedNodes;
            LastDeniedReason = lastDeniedReason;
        }
    }

    public sealed class GoapP0Controller
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

        public GoapP0Controller(
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
