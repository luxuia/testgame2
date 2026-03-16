using System;
using System.Collections.Generic;
using Minecraft.Combat;

namespace Minecraft.GoalPlanning
{
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

        public GoapStepFailureOutcome MarkCurrentStepFailed()
        {
            return MarkCurrentStepFailed(null);
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

}
