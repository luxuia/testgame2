using System;
using System.Collections.Generic;
using Minecraft.Combat;

namespace Minecraft.GoalPlanning
{
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

}
