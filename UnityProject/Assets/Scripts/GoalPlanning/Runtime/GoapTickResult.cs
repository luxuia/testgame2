using System;
using System.Collections.Generic;
using Minecraft.Combat;

namespace Minecraft.GoalPlanning
{
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

}
