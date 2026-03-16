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

}
