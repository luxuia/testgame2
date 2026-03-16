using System;
using System.Collections.Generic;
using Minecraft.Combat;

namespace Minecraft.GoalPlanning
{
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

}
