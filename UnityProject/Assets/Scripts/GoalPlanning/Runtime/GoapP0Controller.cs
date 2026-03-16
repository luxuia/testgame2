using System;
using System.Collections.Generic;

namespace Minecraft.GoalPlanning
{
    public sealed class GoapP0Controller
    {
        private readonly GoapP0Runtime m_Runtime;

        public PlanExecutor Executor => m_Runtime.Executor;
        public GoapTelemetryBuffer Telemetry => m_Runtime.Telemetry;

        public GoapP0Controller(
            string agentId = "agent.default",
            IGoapReservationStore reservationStore = null,
            GoapRolePreset rolePreset = GoapRolePreset.PlayerClone,
            GoapTelemetryBuffer telemetry = null)
        {
            m_Runtime = new GoapP0Runtime(agentId, reservationStore, rolePreset, telemetry);
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
            return m_Runtime.Tick(state, goals, actions, config, nowSec, dynamicGoalScorer, actionFilter);
        }

        public void NotifyCurrentStepSucceeded()
        {
            m_Runtime.NotifyCurrentStepSucceeded();
        }

        public void NotifyCurrentStepFailed(GoapPlannerConfig config)
        {
            m_Runtime.NotifyCurrentStepFailed(config);
        }

        public void ReleaseAllReservations()
        {
            m_Runtime.ReleaseAllReservations();
        }
    }
}
