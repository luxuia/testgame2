using System;
using System.Collections.Generic;
using Minecraft.Combat;

namespace Minecraft.GoalPlanning
{
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

}
