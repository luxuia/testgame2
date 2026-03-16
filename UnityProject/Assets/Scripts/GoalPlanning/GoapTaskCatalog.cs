using System;
using System.Collections.Generic;

namespace Minecraft.GoalPlanning
{
    public static class GoapTaskCatalog
    {
        public const string ReservationTokenSelectedBlock = "{selected_block}";
        public const string ReservationTokenSelectedEntity = "{selected_entity}";
        public const string ReservationTokenCore = "{core_point}";
        public const string ReservationTokenAgentSlot = "{agent_slot}";

        public static void BuildForRole(
            GoapRolePreset role,
            IList<GoapGoalDefinition> goals,
            IList<GoapActionDefinition> actions)
        {
            if (goals == null || actions == null)
            {
                return;
            }

            AddGoalsForRole(role, goals);
            AddCommonActions(actions);
            AddRoleSpecificActions(role, actions);
        }

        private static void AddGoalsForRole(GoapRolePreset role, IList<GoapGoalDefinition> goals)
        {
            switch (role)
            {
                case GoapRolePreset.Deputy:
                    AddGoal(goals, GoapGoalType.DefendCore, 1.6f,
                        new GoapCondition(FactKey.CoreHpPct, CompareOp.GreaterOrEqual, 0.55f),
                        new GoapCondition(FactKey.ThreatLevel, CompareOp.LessOrEqual, 0.25f));
                    AddGoal(goals, GoapGoalType.BuildDefense, 1.35f,
                        new GoapCondition(FactKey.BuildNeed, CompareOp.LessOrEqual, 0.2f));
                    AddGoal(goals, GoapGoalType.MineResource, 1.05f,
                        new GoapCondition(FactKey.ResourceNeed, CompareOp.LessOrEqual, 0.2f));
                    AddGoal(goals, GoapGoalType.Recover, 1.2f,
                        new GoapCondition(FactKey.AgentHpPct, CompareOp.GreaterOrEqual, 0.65f));
                    AddGoal(goals, GoapGoalType.AttackThreat, 0.95f,
                        new GoapCondition(FactKey.ThreatLevel, CompareOp.LessOrEqual, 0.1f));
                    break;
                case GoapRolePreset.Puji:
                    AddGoal(goals, GoapGoalType.DefendCore, 1.5f,
                        new GoapCondition(FactKey.CoreHpPct, CompareOp.GreaterOrEqual, 0.5f),
                        new GoapCondition(FactKey.ThreatLevel, CompareOp.LessOrEqual, 0.2f));
                    AddGoal(goals, GoapGoalType.BuildDefense, 1.25f,
                        new GoapCondition(FactKey.BuildNeed, CompareOp.LessOrEqual, 0.3f));
                    AddGoal(goals, GoapGoalType.MineResource, 1.2f,
                        new GoapCondition(FactKey.ResourceNeed, CompareOp.LessOrEqual, 0.25f));
                    AddGoal(goals, GoapGoalType.AttackThreat, 1f,
                        new GoapCondition(FactKey.ThreatLevel, CompareOp.LessOrEqual, 0.15f));
                    AddGoal(goals, GoapGoalType.Recover, 1.05f,
                        new GoapCondition(FactKey.AgentHpPct, CompareOp.GreaterOrEqual, 0.6f));
                    break;
                default:
                    AddGoal(goals, GoapGoalType.DefendCore, 1.45f,
                        new GoapCondition(FactKey.CoreHpPct, CompareOp.GreaterOrEqual, 0.55f),
                        new GoapCondition(FactKey.ThreatLevel, CompareOp.LessOrEqual, 0.2f));
                    AddGoal(goals, GoapGoalType.AttackThreat, 1.15f,
                        new GoapCondition(FactKey.ThreatLevel, CompareOp.LessOrEqual, 0.1f));
                    AddGoal(goals, GoapGoalType.ExpandFungus, 1.05f,
                        new GoapCondition(FactKey.IsInHomeTerritory, CompareOp.GreaterOrEqual, 1f));
                    AddGoal(goals, GoapGoalType.BuildDefense, 1f,
                        new GoapCondition(FactKey.BuildNeed, CompareOp.LessOrEqual, 0.2f));
                    AddGoal(goals, GoapGoalType.Recover, 1.1f,
                        new GoapCondition(FactKey.AgentHpPct, CompareOp.GreaterOrEqual, 0.65f));
                    break;
            }
        }

        private static void AddCommonActions(IList<GoapActionDefinition> actions)
        {
            actions.Add(CreateAction(
                "task.move.to-target",
                GoapActionType.MoveTo,
                0.9f,
                GoapReservationKind.None,
                null,
                null,
                Effect(
                    new GoapEffect(FactKey.TargetReachable, EffectOp.Set, 1f))));

            actions.Add(CreateAction(
                "task.defend.attack-target",
                GoapActionType.AttackTarget,
                1.15f,
                GoapReservationKind.Slot,
                ReservationTokenSelectedEntity,
                Condition(
                    new GoapCondition(FactKey.TargetReachable, CompareOp.GreaterOrEqual, 1f),
                    new GoapCondition(FactKey.ThreatLevel, CompareOp.Greater, 0.1f)),
                Effect(
                    new GoapEffect(FactKey.ThreatLevel, EffectOp.Add, -0.45f))));

            actions.Add(CreateAction(
                "task.build.place-block",
                GoapActionType.PlaceBlock,
                1.2f,
                GoapReservationKind.Block,
                ReservationTokenSelectedBlock,
                Condition(
                    new GoapCondition(FactKey.IsInHomeTerritory, CompareOp.GreaterOrEqual, 1f),
                    new GoapCondition(FactKey.BuildNeed, CompareOp.Greater, 0.2f)),
                Effect(
                    new GoapEffect(FactKey.BuildNeed, EffectOp.Add, -0.35f))));

            actions.Add(CreateAction(
                "task.terrain.break-block",
                GoapActionType.BreakBlock,
                1.15f,
                GoapReservationKind.Block,
                ReservationTokenSelectedBlock,
                Condition(
                    new GoapCondition(FactKey.IsInHomeTerritory, CompareOp.GreaterOrEqual, 1f),
                    new GoapCondition(FactKey.ThreatLevel, CompareOp.Greater, 0.1f)),
                Effect(
                    new GoapEffect(FactKey.ThreatLevel, EffectOp.Add, -0.2f))));

            actions.Add(CreateAction(
                "task.recover.repair-core",
                GoapActionType.RepairCore,
                0.95f,
                GoapReservationKind.RepairPoint,
                ReservationTokenCore,
                Condition(
                    new GoapCondition(FactKey.IsInHomeTerritory, CompareOp.GreaterOrEqual, 1f),
                    new GoapCondition(FactKey.CoreHpPct, CompareOp.Less, 0.9f)),
                Effect(
                    new GoapEffect(FactKey.CoreHpPct, EffectOp.Add, 0.25f))));

            actions.Add(CreateAction(
                "task.recover.retreat",
                GoapActionType.Retreat,
                0.25f,
                GoapReservationKind.None,
                null,
                Condition(new GoapCondition(FactKey.AgentHpPct, CompareOp.Less, 0.4f)),
                Effect(
                    new GoapEffect(FactKey.AgentHpPct, EffectOp.Add, 0.2f),
                    new GoapEffect(FactKey.TargetReachable, EffectOp.Set, 1f))));

            actions.Add(CreateAction(
                "task.hold-position",
                GoapActionType.HoldPosition,
                0.5f,
                GoapReservationKind.Slot,
                ReservationTokenAgentSlot,
                null,
                null));

            actions.Add(CreateAction(
                "task.idle",
                GoapActionType.Idle,
                0.7f,
                GoapReservationKind.None,
                null,
                null,
                null));
        }

        private static void AddRoleSpecificActions(GoapRolePreset role, IList<GoapActionDefinition> actions)
        {
            if (role == GoapRolePreset.PlayerClone)
            {
                actions.Add(CreateAction(
                    "task.expand.convert-fungus",
                    GoapActionType.ConvertToFungus,
                    1.35f,
                    GoapReservationKind.Block,
                    ReservationTokenSelectedBlock,
                    Condition(
                        new GoapCondition(FactKey.HasPortableFungusCharge, CompareOp.Greater, 0f),
                        new GoapCondition(FactKey.TargetReachable, CompareOp.GreaterOrEqual, 1f)),
                    Effect(
                        new GoapEffect(FactKey.IsInHomeTerritory, EffectOp.Set, 1f),
                        new GoapEffect(FactKey.HasPortableFungusCharge, EffectOp.Add, -1f),
                        new GoapEffect(FactKey.BuildNeed, EffectOp.Add, -0.2f))));
            }

            actions.Add(CreateAction(
                "task.build.functional-block",
                GoapActionType.BuildFunctionalBlock,
                1.3f,
                GoapReservationKind.Slot,
                ReservationTokenSelectedBlock,
                Condition(
                    new GoapCondition(FactKey.IsInHomeTerritory, CompareOp.GreaterOrEqual, 1f),
                    new GoapCondition(FactKey.BuildNeed, CompareOp.Greater, 0.35f)),
                Effect(
                    new GoapEffect(FactKey.BuildNeed, EffectOp.Set, 0.1f))));

            actions.Add(CreateAction(
                "task.mine.gather-resource",
                GoapActionType.GatherResource,
                1.1f,
                GoapReservationKind.Block,
                ReservationTokenSelectedBlock,
                Condition(
                    new GoapCondition(FactKey.ResourceNeed, CompareOp.Greater, 0.2f),
                    new GoapCondition(FactKey.TargetReachable, CompareOp.GreaterOrEqual, 1f)),
                Effect(
                    new GoapEffect(FactKey.ResourceNeed, EffectOp.Add, -0.4f))));
        }

        private static void AddGoal(
            IList<GoapGoalDefinition> goals,
            GoapGoalType type,
            float basePriority,
            params GoapCondition[] desired)
        {
            goals.Add(new GoapGoalDefinition
            {
                Type = type,
                BasePriority = basePriority,
                DesiredState = Condition(desired),
            });
        }

        private static GoapActionDefinition CreateAction(
            string id,
            GoapActionType type,
            float baseCost,
            GoapReservationKind reservationKind,
            string reservationTargetId,
            List<GoapCondition> preconditions,
            List<GoapEffect> effects)
        {
            return new GoapActionDefinition
            {
                Id = id,
                Type = type,
                BaseCost = baseCost,
                ReservationKind = reservationKind,
                ReservationTargetId = reservationTargetId,
                Preconditions = preconditions ?? new List<GoapCondition>(),
                Effects = effects ?? new List<GoapEffect>(),
            };
        }

        private static List<GoapCondition> Condition(params GoapCondition[] conditions)
        {
            if (conditions == null || conditions.Length == 0)
            {
                return new List<GoapCondition>();
            }

            return new List<GoapCondition>(conditions);
        }

        private static List<GoapEffect> Effect(params GoapEffect[] effects)
        {
            if (effects == null || effects.Length == 0)
            {
                return new List<GoapEffect>();
            }

            return new List<GoapEffect>(effects);
        }
    }
}
