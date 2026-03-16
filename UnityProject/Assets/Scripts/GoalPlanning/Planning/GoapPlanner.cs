using System;
using System.Collections.Generic;
using Minecraft.Combat;

namespace Minecraft.GoalPlanning
{
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

}
