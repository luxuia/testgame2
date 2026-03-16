using System;
using System.Collections.Generic;
using Minecraft.Combat;

namespace Minecraft.GoalPlanning
{
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

}
