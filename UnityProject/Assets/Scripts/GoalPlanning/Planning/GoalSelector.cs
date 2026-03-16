using System;
using System.Collections.Generic;
using Minecraft.Combat;

namespace Minecraft.GoalPlanning
{
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

}
