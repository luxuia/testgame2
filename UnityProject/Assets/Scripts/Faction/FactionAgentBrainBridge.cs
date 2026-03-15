using System;
using System.Collections.Generic;
using Minecraft.Combat;
using Minecraft.Entities;
using Minecraft.GoalPlanning;
using Minecraft.Pathfinding;
using UnityEngine;

namespace Minecraft.Faction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CrowdAgentController))]
    public sealed class FactionAgentBrainBridge : MonoBehaviour
    {
        private enum StepExecutionStatus : byte
        {
            Running = 0,
            Succeeded = 1,
            Failed = 2,
        }

        private readonly List<GoapGoalDefinition> m_Goals = new List<GoapGoalDefinition>(2);
        private readonly List<GoapActionDefinition> m_Actions = new List<GoapActionDefinition>(8);

        private CrowdAgentController m_Agent;
        private GoapP0Controller m_GoapController;
        private CombatRuntimeConfig m_CombatConfig;
        private CombatActionPipeline m_CombatPipeline;
        private CombatAuthorityLegalityGate m_AuthorityGate;

        private FactionGoapPlannerPreset m_PlannerPreset;
        private GoapPlannerConfig m_PlannerConfig;
        private FactionObjectivePreset m_ObjectivePreset;
        private FactionRuntimeDirective m_Directive;

        private float m_NextBrainTickTime;
        private float m_NextTargetProbeTime;

        private Transform m_SelectedTarget;
        private bool m_SelectedPlayerTarget;
        private bool m_SelectedTargetReachable;
        private string m_LastFailureReason;

        public string FactionId { get; private set; }
        public int ConsecutiveActionFailures { get; private set; }
        public string LastFailureReason => m_LastFailureReason;
        public bool IsAlive => m_Agent != null && m_Agent.CombatRuntime != null && m_Agent.CombatRuntime.IsAlive;

        private void Awake()
        {
            m_Agent = GetComponent<CrowdAgentController>();
            m_GoapController = new GoapP0Controller();
            m_CombatConfig = new CombatRuntimeConfig();
            m_CombatPipeline = new CombatActionPipeline(m_CombatConfig);
            m_AuthorityGate = new CombatAuthorityLegalityGate();
        }

        public void Initialize(string factionId, FactionObjectivePreset objectivePreset, FactionGoapPlannerPreset plannerPreset)
        {
            FactionId = string.IsNullOrWhiteSpace(factionId) ? "hostile-assault-v1" : factionId;
            m_ObjectivePreset = objectivePreset;
            m_PlannerPreset = plannerPreset ?? new FactionGoapPlannerPreset();
            m_PlannerConfig = m_PlannerPreset.ToConfig();
            BuildGoapDefinitions();
            m_NextBrainTickTime = 0f;
            m_NextTargetProbeTime = 0f;
            ConsecutiveActionFailures = 0;
            m_LastFailureReason = null;
        }

        public void SetDirective(in FactionRuntimeDirective directive)
        {
            m_Directive = directive;
            if (m_ObjectivePreset == null && directive.ObjectivePreset != null)
            {
                m_ObjectivePreset = directive.ObjectivePreset;
            }
        }

        private void Update()
        {
            if (m_Agent == null || m_Agent.CombatRuntime == null || !m_Agent.CombatRuntime.IsAlive)
            {
                return;
            }

            float tickInterval = m_ObjectivePreset != null
                ? Mathf.Max(0.05f, m_ObjectivePreset.BrainTickIntervalSec)
                : 0.2f;
            if (Time.time < m_NextBrainTickTime)
            {
                return;
            }

            m_NextBrainTickTime = Time.time + tickInterval;
            TickBrain();
        }

        private void TickBrain()
        {
            RefreshSelectedTarget();

            CombatContext combatContext = BuildCombatContext();
            BattleDirectorSnapshot snapshot = new BattleDirectorSnapshot
            {
                CoreHealthNormalized = Mathf.Clamp01(m_Directive.CoreHealthNormalized),
                IsBossEncounter = false,
                IsUnderHeavyAttack = m_Directive.Stage == FactionAssaultStage.Attack,
            };

            float hpPct = Mathf.Clamp01(m_Agent.CombatRuntime.CurrentHealth / Mathf.Max(1f, m_Agent.CombatRuntime.MaxHealth));
            Dictionary<FactKey, float> state = GoapBlackboardBuilder.Build(
                snapshot,
                combatContext,
                resourceNeed: 0.35f,
                buildNeed: m_Directive.Stage == FactionAssaultStage.Regroup ? 0.8f : 0.15f,
                agentHpPct: hpPct,
                targetReachable: m_SelectedTargetReachable,
                hasPortableFungusCharge: false);

            state[FactKey.ThreatLevel] = HasThreatTarget() ? 1f : 0f;

            GoapLegalityContext legalityContext = new GoapLegalityContext(combatContext);
            Func<GoapActionDefinition, bool> filter = GoapActionFilters.CreateAuthorityFilter(
                m_AuthorityGate,
                legalityContext,
                deny => m_LastFailureReason = deny);

            GoapTickResult tick = m_GoapController.Tick(
                state,
                m_Goals,
                m_Actions,
                m_PlannerConfig,
                Time.time,
                DynamicGoalScorer,
                filter);

            if (!tick.HasStep)
            {
                RunDeterministicFallback();
                return;
            }

            StepExecutionStatus status = ExecuteStep(tick.CurrentStep.Action?.Type ?? GoapActionType.Idle, combatContext);
            switch (status)
            {
                case StepExecutionStatus.Succeeded:
                    m_GoapController.Executor.MarkCurrentStepSucceeded();
                    ConsecutiveActionFailures = 0;
                    m_LastFailureReason = null;
                    break;
                case StepExecutionStatus.Failed:
                    m_GoapController.Executor.MarkCurrentStepFailed();
                    ConsecutiveActionFailures++;
                    break;
            }

            if (tick.UsedFallbackPlan)
            {
                RunDeterministicFallback();
            }
        }

        private float DynamicGoalScorer(GoapGoalDefinition goal, IReadOnlyDictionary<FactKey, float> state)
        {
            if (goal == null)
            {
                return 0f;
            }

            float hpPct = state != null && state.TryGetValue(FactKey.AgentHpPct, out float hp) ? hp : 1f;
            float threat = state != null && state.TryGetValue(FactKey.ThreatLevel, out float t) ? t : 0f;
            float score = 0f;

            switch (goal.Type)
            {
                case GoapGoalType.AttackThreat:
                    score += threat * 2f;
                    if (m_Directive.Stage == FactionAssaultStage.Attack)
                    {
                        score += 1.75f;
                    }
                    else if (m_Directive.Stage == FactionAssaultStage.Advance)
                    {
                        score += 0.95f;
                    }

                    if (m_SelectedPlayerTarget)
                    {
                        score += m_ObjectivePreset != null ? m_ObjectivePreset.PlayerTargetPriority : 1f;
                    }
                    else
                    {
                        score += m_ObjectivePreset != null ? m_ObjectivePreset.CoreTargetPriority : 0.6f;
                    }
                    break;

                case GoapGoalType.Recover:
                    if (m_Directive.Stage == FactionAssaultStage.Regroup)
                    {
                        score += 2.5f;
                    }

                    if (m_ObjectivePreset != null)
                    {
                        if (hpPct < m_ObjectivePreset.RegroupHealthThreshold)
                        {
                            score += 3f;
                        }

                        if (ConsecutiveActionFailures >= m_ObjectivePreset.RegroupFailureThreshold)
                        {
                            score += 2f;
                        }
                    }
                    else if (hpPct < 0.35f)
                    {
                        score += 2f;
                    }
                    break;
            }

            return score;
        }

        private StepExecutionStatus ExecuteStep(GoapActionType actionType, in CombatContext context)
        {
            switch (actionType)
            {
                case GoapActionType.MoveTo:
                    return ExecuteMoveTo();
                case GoapActionType.AttackTarget:
                    return ExecuteAttack(context);
                case GoapActionType.BreakBlock:
                    return ExecuteTerrain(context, CombatActionKind.BreakBlock);
                case GoapActionType.PlaceBlock:
                    return ExecuteTerrain(context, CombatActionKind.PlaceBlock);
                case GoapActionType.Retreat:
                    return ExecuteRetreat();
                case GoapActionType.HoldPosition:
                case GoapActionType.Idle:
                    m_Agent.ClearTarget();
                    return StepExecutionStatus.Succeeded;
                default:
                    return StepExecutionStatus.Succeeded;
            }
        }

        private StepExecutionStatus ExecuteMoveTo()
        {
            if (m_SelectedTarget == null)
            {
                m_LastFailureReason = "MoveTo has no selected target.";
                return StepExecutionStatus.Failed;
            }

            Vector3Int target = ToGrid(m_SelectedTarget.position);
            m_Agent.SetTarget(target);
            if (IsWithinAttackRange(m_SelectedTarget.position))
            {
                return StepExecutionStatus.Succeeded;
            }

            return StepExecutionStatus.Running;
        }

        private StepExecutionStatus ExecuteRetreat()
        {
            Transform regroup = ResolveRegroupPoint();
            if (regroup == null)
            {
                m_LastFailureReason = "Retreat has no regroup point.";
                return StepExecutionStatus.Failed;
            }

            m_Agent.SetTarget(ToGrid(regroup.position));
            if (Vector3.SqrMagnitude(regroup.position - transform.position) <= 1.25f * 1.25f)
            {
                return StepExecutionStatus.Succeeded;
            }

            return StepExecutionStatus.Running;
        }

        private StepExecutionStatus ExecuteAttack(in CombatContext context)
        {
            if (m_SelectedTarget == null)
            {
                m_LastFailureReason = "Attack has no selected target.";
                return StepExecutionStatus.Failed;
            }

            if (!IsWithinAttackRange(m_SelectedTarget.position))
            {
                m_Agent.SetTarget(ToGrid(m_SelectedTarget.position));
                return StepExecutionStatus.Running;
            }

            CombatActionRequest request = new CombatActionRequest
            {
                ActionKind = CombatActionKind.Attack,
                Context = context,
                Actor = m_Agent.CombatRuntime,
                TargetEntity = ResolveCombatTargetRuntime(),
                TargetBlock = ToGrid(m_SelectedTarget.position),
                Skill = null,
                PreferredAnimationActionName = "Punch",
                TriggeredByAI = true,
            };

            CombatActionResult result = m_CombatPipeline.Execute(in request);
            if (!result.Success)
            {
                m_LastFailureReason = result.FailureReason;
                return StepExecutionStatus.Failed;
            }

            return StepExecutionStatus.Succeeded;
        }

        private StepExecutionStatus ExecuteTerrain(in CombatContext context, CombatActionKind actionKind)
        {
            Vector3Int target = m_SelectedTarget != null
                ? ToGrid(m_SelectedTarget.position)
                : m_Agent.GetCurrentGridPosition() + Vector3Int.RoundToInt(transform.forward);

            CombatActionRequest request = new CombatActionRequest
            {
                ActionKind = actionKind,
                Context = context,
                Actor = m_Agent.CombatRuntime,
                TargetEntity = null,
                TargetBlock = target,
                Skill = null,
                PreferredAnimationActionName = actionKind == CombatActionKind.PlaceBlock ? "SpecialAttack1" : "Punch",
                TriggeredByAI = true,
            };

            CombatActionResult result = m_CombatPipeline.Execute(in request);
            if (!result.Success)
            {
                m_LastFailureReason = result.FailureReason;
                return StepExecutionStatus.Failed;
            }

            return StepExecutionStatus.Succeeded;
        }

        private CombatEntityRuntime ResolveCombatTargetRuntime()
        {
            if (m_SelectedTarget == null)
            {
                return null;
            }

            if (m_SelectedTarget.TryGetComponent(out PlayerEntity playerEntity))
            {
                return playerEntity.CombatRuntime;
            }

            if (m_SelectedTarget.TryGetComponent(out CrowdAgentController crowdAgent))
            {
                return crowdAgent.CombatRuntime;
            }

            return null;
        }

        private void RunDeterministicFallback()
        {
            if (m_Directive.Stage == FactionAssaultStage.Regroup)
            {
                ExecuteRetreat();
                return;
            }

            if (m_SelectedTarget != null)
            {
                m_Agent.SetTarget(ToGrid(m_SelectedTarget.position));
                return;
            }

            m_Agent.ClearTarget();
        }

        private void RefreshSelectedTarget()
        {
            if (Time.time < m_NextTargetProbeTime)
            {
                return;
            }

            float interval = m_ObjectivePreset != null
                ? Mathf.Max(0.1f, m_ObjectivePreset.RepathProbeIntervalSec)
                : 0.6f;
            m_NextTargetProbeTime = Time.time + interval;

            if (m_Directive.Stage == FactionAssaultStage.Regroup)
            {
                m_SelectedTarget = ResolveRegroupPoint();
                m_SelectedPlayerTarget = false;
                m_SelectedTargetReachable = m_SelectedTarget != null;
                return;
            }

            Transform player = ResolvePlayerTarget();
            Transform core = ResolveCoreTarget();

            bool playerReachable = IsTargetReachable(player);
            if (player != null && playerReachable)
            {
                m_SelectedTarget = player;
                m_SelectedPlayerTarget = true;
                m_SelectedTargetReachable = true;
                return;
            }

            bool coreReachable = IsTargetReachable(core);
            if (core != null && coreReachable)
            {
                m_SelectedTarget = core;
                m_SelectedPlayerTarget = false;
                m_SelectedTargetReachable = true;
                return;
            }

            m_SelectedTarget = player != null ? player : core;
            m_SelectedPlayerTarget = player != null;
            m_SelectedTargetReachable = m_SelectedTarget != null;
        }

        private bool IsTargetReachable(Transform target)
        {
            if (target == null)
            {
                return false;
            }

            IWorld world = World.Active;
            if (world == null || !world.Initialized)
            {
                return true;
            }

            Vector3Int current = m_Agent.GetCurrentGridPosition();
            Vector3Int desired = ToGrid(target.position);
            int searchRadius = m_ObjectivePreset != null ? Mathf.Max(1, m_ObjectivePreset.ReachabilitySearchRadius) : 6;
            Vector3Int? start = AStarPathfinding.FindNearestWalkableNode(current, world, searchRadius);
            Vector3Int? end = AStarPathfinding.FindNearestWalkableNode(desired, world, searchRadius);
            if (!start.HasValue || !end.HasValue)
            {
                return false;
            }

            if (start.Value == end.Value)
            {
                return true;
            }

            int iterations = m_ObjectivePreset != null ? Mathf.Max(32, m_ObjectivePreset.ReachabilityIterations) : 180;
            List<Vector3Int> path = AStarPathfinding.FindPath(start.Value, end.Value, world, iterations);
            return path != null && path.Count > 0;
        }

        private CombatContext BuildCombatContext()
        {
            Vector3 center = m_Directive.HomeCenter;
            float radius = Mathf.Max(1f, m_Directive.HomeRadius);
            bool inHome = Vector3.SqrMagnitude(transform.position - center) <= radius * radius;

            return new CombatContext
            {
                TerritoryKind = inHome ? CombatTerritoryKind.Home : CombatTerritoryKind.Away,
                HasTemporaryAwayEditAuthority = m_Directive.AllowAwayTerrainEdit,
                DirectorForceTerrainAuthority = false,
            };
        }

        private bool HasThreatTarget()
        {
            CombatEntityRuntime target = ResolveCombatTargetRuntime();
            if (target != null)
            {
                return target.IsAlive;
            }

            return m_SelectedTarget != null;
        }

        private bool IsWithinAttackRange(Vector3 targetPosition)
        {
            float range = m_ObjectivePreset != null ? Mathf.Max(0.5f, m_ObjectivePreset.AttackRange) : 1.8f;
            return Vector3.SqrMagnitude(targetPosition - transform.position) <= range * range;
        }

        private Transform ResolvePlayerTarget()
        {
            if (m_Directive.PlayerTarget != null)
            {
                return m_Directive.PlayerTarget;
            }

            IWorld world = World.Active;
            return world != null ? world.PlayerTransform : null;
        }

        private Transform ResolveCoreTarget()
        {
            return m_Directive.CoreTarget;
        }

        private Transform ResolveRegroupPoint()
        {
            if (m_Directive.RegroupPoint != null)
            {
                return m_Directive.RegroupPoint;
            }

            return m_Directive.CoreTarget;
        }

        private void BuildGoapDefinitions()
        {
            m_Goals.Clear();
            m_Actions.Clear();

            GoapGoalDefinition attackGoal = new GoapGoalDefinition
            {
                Type = GoapGoalType.AttackThreat,
                BasePriority = 1f,
                DesiredState = new List<GoapCondition>
                {
                    new GoapCondition(FactKey.ThreatLevel, CompareOp.LessOrEqual, 0.1f),
                },
            };

            GoapGoalDefinition recoverGoal = new GoapGoalDefinition
            {
                Type = GoapGoalType.Recover,
                BasePriority = 0.9f,
                DesiredState = new List<GoapCondition>
                {
                    new GoapCondition(FactKey.AgentHpPct, CompareOp.GreaterOrEqual, 0.65f),
                },
            };

            m_Goals.Add(attackGoal);
            m_Goals.Add(recoverGoal);

            m_Actions.Add(new GoapActionDefinition
            {
                Id = "assault.move",
                Type = GoapActionType.MoveTo,
                BaseCost = 1f,
                Preconditions = new List<GoapCondition>
                {
                    new GoapCondition(FactKey.ThreatLevel, CompareOp.Greater, 0.1f),
                },
                Effects = new List<GoapEffect>
                {
                    new GoapEffect(FactKey.TargetReachable, EffectOp.Set, 1f),
                },
            });

            m_Actions.Add(new GoapActionDefinition
            {
                Id = "assault.attack",
                Type = GoapActionType.AttackTarget,
                BaseCost = 1.2f,
                Preconditions = new List<GoapCondition>
                {
                    new GoapCondition(FactKey.TargetReachable, CompareOp.GreaterOrEqual, 1f),
                    new GoapCondition(FactKey.ThreatLevel, CompareOp.Greater, 0.1f),
                },
                Effects = new List<GoapEffect>
                {
                    new GoapEffect(FactKey.ThreatLevel, EffectOp.Set, 0f),
                },
            });

            m_Actions.Add(new GoapActionDefinition
            {
                Id = "assault.break-block",
                Type = GoapActionType.BreakBlock,
                BaseCost = 1.3f,
                Preconditions = new List<GoapCondition>
                {
                    new GoapCondition(FactKey.IsInHomeTerritory, CompareOp.GreaterOrEqual, 1f),
                    new GoapCondition(FactKey.ThreatLevel, CompareOp.Greater, 0.1f),
                },
                Effects = new List<GoapEffect>
                {
                    new GoapEffect(FactKey.ThreatLevel, EffectOp.Add, -0.2f),
                },
            });

            m_Actions.Add(new GoapActionDefinition
            {
                Id = "assault.place-block",
                Type = GoapActionType.PlaceBlock,
                BaseCost = 1.4f,
                Preconditions = new List<GoapCondition>
                {
                    new GoapCondition(FactKey.IsInHomeTerritory, CompareOp.GreaterOrEqual, 1f),
                    new GoapCondition(FactKey.AgentHpPct, CompareOp.Less, 0.45f),
                },
                Effects = new List<GoapEffect>
                {
                    new GoapEffect(FactKey.AgentHpPct, EffectOp.Add, 0.05f),
                },
            });

            m_Actions.Add(new GoapActionDefinition
            {
                Id = "assault.retreat",
                Type = GoapActionType.Retreat,
                BaseCost = 0.2f,
                Preconditions = new List<GoapCondition>
                {
                    new GoapCondition(FactKey.AgentHpPct, CompareOp.Less, 0.35f),
                },
                Effects = new List<GoapEffect>
                {
                    new GoapEffect(FactKey.AgentHpPct, EffectOp.Add, 0.2f),
                    new GoapEffect(FactKey.TargetReachable, EffectOp.Set, 1f),
                },
            });

            m_Actions.Add(new GoapActionDefinition
            {
                Id = "assault.hold",
                Type = GoapActionType.HoldPosition,
                BaseCost = 0.5f,
                Preconditions = new List<GoapCondition>(),
                Effects = new List<GoapEffect>(),
            });

            m_Actions.Add(new GoapActionDefinition
            {
                Id = "assault.idle",
                Type = GoapActionType.Idle,
                BaseCost = 0.6f,
                Preconditions = new List<GoapCondition>(),
                Effects = new List<GoapEffect>(),
            });
        }

        private static Vector3Int ToGrid(Vector3 worldPos)
        {
            return new Vector3Int(
                Mathf.FloorToInt(worldPos.x),
                Mathf.FloorToInt(worldPos.y),
                Mathf.FloorToInt(worldPos.z));
        }
    }
}
