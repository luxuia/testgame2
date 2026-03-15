using System;
using UnityEngine;

namespace Minecraft.Combat
{
    public enum CombatBrainState : byte
    {
        Idle = 0,
        Move = 1,
        Attack = 2,
        TerrainOp = 3,
        Retreat = 4,
        Repair = 5,
    }

    public struct BattleDirectorSnapshot
    {
        public float CoreHealthNormalized;
        public bool IsBossEncounter;
        public bool IsUnderHeavyAttack;
    }

    public struct BattleDirectorDirective
    {
        public bool ForceDefensiveMode;
        public float OffenseWeight;
        public float DefenseWeight;
        public float RepairWeight;
    }

    public sealed class BattleDirector
    {
        public BattleDirectorDirective Evaluate(in BattleDirectorSnapshot snapshot, CombatRuntimeConfig config)
        {
            float offense = 1f;
            float defense = 1f;
            float repair = 1f;

            bool defensive = snapshot.CoreHealthNormalized <= config.AutoTakeoverCoreHpPct;
            if (defensive)
            {
                offense = 0.5f;
                defense = 2f;
                repair = 2.25f;
            }
            else if (snapshot.IsUnderHeavyAttack)
            {
                offense = 0.8f;
                defense = 1.5f;
                repair = 1.3f;
            }
            else if (snapshot.IsBossEncounter)
            {
                offense = 1.3f;
                defense = 1.1f;
                repair = 1f;
            }

            return new BattleDirectorDirective
            {
                ForceDefensiveMode = defensive,
                OffenseWeight = offense,
                DefenseWeight = defense,
                RepairWeight = repair,
            };
        }
    }

    public struct RoleBrainContext
    {
        public bool HasCombatTarget;
        public bool IsInAttackRange;
        public bool CanUseTerrainAction;
        public bool ShouldAdvance;

        public bool ShouldRetreat;
        public bool NeedsRepair;
        public bool HealthLow;
    }

    public struct RoleBrainDecision
    {
        public CombatBrainState State;
        public bool UsesSharedActionPipeline;
        public CombatActionKind PlannedAction;
    }

    public sealed class RoleBrain
    {
        public CombatBrainState CurrentState { get; private set; } = CombatBrainState.Idle;

        private readonly float m_MinStateStaySeconds;
        private readonly float m_SwitchCooldownSeconds;

        private float m_CurrentStateElapsed;
        private float m_NextSwitchAllowedTime;

        public RoleBrain(float minStateStaySeconds = 0.25f, float switchCooldownSeconds = 0.1f)
        {
            m_MinStateStaySeconds = Mathf.Max(0f, minStateStaySeconds);
            m_SwitchCooldownSeconds = Mathf.Max(0f, switchCooldownSeconds);
        }

        public RoleBrainDecision Tick(in RoleBrainContext context, in BattleDirectorDirective directive, float deltaTime, float now)
        {
            m_CurrentStateElapsed += Mathf.Max(0f, deltaTime);

            CombatBrainState desired = SelectHighestScoreState(in context, in directive);
            bool canSwitch = now >= m_NextSwitchAllowedTime && m_CurrentStateElapsed >= m_MinStateStaySeconds;
            if (desired != CurrentState && canSwitch)
            {
                CurrentState = desired;
                m_CurrentStateElapsed = 0f;
                m_NextSwitchAllowedTime = now + m_SwitchCooldownSeconds;
            }

            return BuildDecision(CurrentState, in context);
        }

        private static CombatBrainState SelectHighestScoreState(in RoleBrainContext context, in BattleDirectorDirective directive)
        {
            float bestScore = float.MinValue;
            CombatBrainState bestState = CombatBrainState.Idle;

            EvaluateStateScore(CombatBrainState.Repair, ScoreRepair(in context, in directive), ref bestState, ref bestScore);
            EvaluateStateScore(CombatBrainState.Retreat, ScoreRetreat(in context, in directive), ref bestState, ref bestScore);
            EvaluateStateScore(CombatBrainState.Attack, ScoreAttack(in context, in directive), ref bestState, ref bestScore);
            EvaluateStateScore(CombatBrainState.TerrainOp, ScoreTerrain(in context, in directive), ref bestState, ref bestScore);
            EvaluateStateScore(CombatBrainState.Move, ScoreMove(in context, in directive), ref bestState, ref bestScore);
            EvaluateStateScore(CombatBrainState.Idle, 0.1f, ref bestState, ref bestScore);

            return bestState;
        }

        private static void EvaluateStateScore(CombatBrainState state, float score, ref CombatBrainState bestState, ref float bestScore)
        {
            if (score > bestScore)
            {
                bestScore = score;
                bestState = state;
            }
        }

        private static float ScoreRepair(in RoleBrainContext context, in BattleDirectorDirective directive)
        {
            if (!context.NeedsRepair)
            {
                return -10f;
            }

            return 3f * directive.RepairWeight;
        }

        private static float ScoreRetreat(in RoleBrainContext context, in BattleDirectorDirective directive)
        {
            if (!context.ShouldRetreat && !context.HealthLow && !directive.ForceDefensiveMode)
            {
                return -5f;
            }

            return 2f * directive.DefenseWeight;
        }

        private static float ScoreAttack(in RoleBrainContext context, in BattleDirectorDirective directive)
        {
            if (!context.HasCombatTarget || !context.IsInAttackRange)
            {
                return -4f;
            }

            return 2.5f * directive.OffenseWeight;
        }

        private static float ScoreTerrain(in RoleBrainContext context, in BattleDirectorDirective directive)
        {
            if (!context.CanUseTerrainAction)
            {
                return -4f;
            }

            float baseScore = directive.ForceDefensiveMode ? 2f : 1f;
            return baseScore * Mathf.Max(0.5f, directive.DefenseWeight);
        }

        private static float ScoreMove(in RoleBrainContext context, in BattleDirectorDirective directive)
        {
            if (!context.ShouldAdvance || !context.HasCombatTarget)
            {
                return -3f;
            }

            return 1.5f * directive.OffenseWeight;
        }

        private static RoleBrainDecision BuildDecision(CombatBrainState state, in RoleBrainContext context)
        {
            switch (state)
            {
                case CombatBrainState.Attack:
                    return new RoleBrainDecision
                    {
                        State = state,
                        UsesSharedActionPipeline = true,
                        PlannedAction = CombatActionKind.Attack,
                    };
                case CombatBrainState.TerrainOp:
                    return new RoleBrainDecision
                    {
                        State = state,
                        UsesSharedActionPipeline = true,
                        PlannedAction = context.NeedsRepair ? CombatActionKind.PlaceBlock : CombatActionKind.BreakBlock,
                    };
                case CombatBrainState.Retreat:
                    return new RoleBrainDecision
                    {
                        State = state,
                        UsesSharedActionPipeline = true,
                        PlannedAction = CombatActionKind.Dodge,
                    };
                case CombatBrainState.Repair:
                    return new RoleBrainDecision
                    {
                        State = state,
                        UsesSharedActionPipeline = true,
                        PlannedAction = CombatActionKind.PlaceBlock,
                    };
                default:
                    return new RoleBrainDecision
                    {
                        State = state,
                        UsesSharedActionPipeline = false,
                        PlannedAction = CombatActionKind.Attack,
                    };
            }
        }
    }

    public sealed class ActionExecutor
    {
        private readonly CombatActionPipeline m_ActionPipeline;

        public ActionExecutor(CombatActionPipeline actionPipeline)
        {
            m_ActionPipeline = actionPipeline ?? throw new ArgumentNullException(nameof(actionPipeline));
        }

        public CombatActionResult ExecuteDecision(in RoleBrainDecision decision, in CombatActionRequest templateRequest)
        {
            if (!decision.UsesSharedActionPipeline)
            {
                return new CombatActionResult { Success = true };
            }

            CombatActionRequest actionRequest = templateRequest;
            actionRequest.ActionKind = decision.PlannedAction;
            actionRequest.TriggeredByAI = true;
            return m_ActionPipeline.Execute(in actionRequest);
        }
    }
}
