using System;
using System.Collections.Generic;
using Minecraft.Combat;
using Minecraft.Entities;
using Minecraft.Pathfinding;
using UnityEngine;

namespace Minecraft.Faction
{
    [DisallowMultipleComponent]
    public sealed class FactionScenarioDirector : MonoBehaviour
    {
        [Header("V1 Guard")]
        [SerializeField] private bool m_EnforceSingleHostileProfileV1 = true;
        [SerializeField] private string m_ForcedV1FactionId;

        [Header("Scene Targets")]
        [SerializeField] private Transform m_CoreTarget;
        [SerializeField] private Transform m_RegroupPoint;

        [Header("Faction Data")]
        [SerializeField] private FactionObjectivePreset m_DefaultObjectivePreset;
        [SerializeField] private FactionGoapPlannerPreset m_DefaultPlannerPreset = new FactionGoapPlannerPreset();
        [SerializeField] private List<FactionProfileDefinition> m_FactionProfiles = new List<FactionProfileDefinition>();
        [SerializeField] private List<FactionSpawnAnchor> m_SpawnAnchors = new List<FactionSpawnAnchor>();

        [Header("Spawning")]
        [SerializeField] [Min(0f)] private float m_InitialWaveDelaySec = 1f;
        [SerializeField] [Min(0.1f)] private float m_FallbackWaveIntervalSec = 10f;

        [Header("Crowd Integration")]
        [SerializeField] private bool m_AttachSpawnedAgentsToCrowdRoot = true;
        [SerializeField] private Transform m_AgentRuntimeParent;
        [SerializeField] [Range(0, 6)] private int m_CombatChunkPreloadRadius = 2;
        [SerializeField] [Min(0.1f)] private float m_CombatChunkPreloadIntervalSec = 0.5f;

        [Header("Idle Training Dummy")]
        [SerializeField] private bool m_EnableIdleTrainingDummy = true;
        [SerializeField] [Min(1f)] private float m_IdleTrainingSpawnRadius = 3.5f;
        [SerializeField] [Min(1f)] private float m_IdleTrainingMaxHealth = 1200f;

        [Header("Debug")]
        [SerializeField] private bool m_EnableRuntimeLog;

        private readonly List<FactionAgentBrainBridge> m_ActiveAgents = new List<FactionAgentBrainBridge>(64);

        private FactionProfileDefinition m_ActiveProfile;
        private FactionAssaultStage m_CurrentStage = FactionAssaultStage.Assemble;
        private float m_StageEnteredAt;

        private int m_CurrentWaveIndex;
        private int m_WaveRemainingSpawn;
        private int m_WaveSpawnedCount;
        private float m_NextWaveStartTime;
        private float m_NextSpawnTickTime;
        private int m_SpawnAnchorCursor;
        private Transform m_IdleTrainingDummy;
        private CrowdFlowFieldCoordinator m_CrowdCoordinator;
        private float m_NextChunkPreloadTime;

        private void Start()
        {
            ResolveActiveProfile();
            if (m_ActiveProfile != null)
            {
                EnsureIdleTrainingDummy();
            }

            m_StageEnteredAt = Time.time;
            m_NextWaveStartTime = Time.time + Mathf.Max(0f, m_InitialWaveDelaySec);
        }

        private void Update()
        {
            CleanupDeadAgents();
            if (m_ActiveProfile == null)
            {
                return;
            }

            PreloadCombatChunks();
            EnsureIdleTrainingDummy();
            UpdateStage();
            TrySpawnWave();
            BroadcastDirective();
        }

        private void ResolveActiveProfile()
        {
            m_ActiveProfile = null;

            for (int i = 0; i < m_FactionProfiles.Count; i++)
            {
                FactionProfileDefinition candidate = m_FactionProfiles[i];
                if (candidate == null || !candidate.Enabled)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(m_ForcedV1FactionId)
                    && !string.Equals(candidate.FactionId, m_ForcedV1FactionId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (m_EnforceSingleHostileProfileV1)
                {
                    if (candidate.IsPrimaryV1)
                    {
                        m_ActiveProfile = candidate;
                        break;
                    }

                    if (m_ActiveProfile == null)
                    {
                        m_ActiveProfile = candidate;
                    }
                }
                else if (m_ActiveProfile == null)
                {
                    m_ActiveProfile = candidate;
                }
            }

            if (m_EnableRuntimeLog)
            {
                Debug.Log(m_ActiveProfile == null
                    ? "[FactionScenarioDirector] No active faction profile resolved."
                    : $"[FactionScenarioDirector] Active profile: {m_ActiveProfile.FactionId}");
            }
        }

        private void UpdateStage()
        {
            FactionObjectivePreset preset = ResolveObjectivePreset();
            float minStay = preset != null ? Mathf.Max(0.1f, preset.StageMinStaySeconds) : 1f;
            if (Time.time - m_StageEnteredAt < minStay)
            {
                return;
            }

            bool shouldRegroup = ShouldRegroup(preset);
            int aliveCount = m_ActiveAgents.Count;
            switch (m_CurrentStage)
            {
                case FactionAssaultStage.Assemble:
                    if (aliveCount > 0)
                    {
                        EnterStage(FactionAssaultStage.Advance);
                    }
                    break;

                case FactionAssaultStage.Advance:
                    if (shouldRegroup)
                    {
                        EnterStage(FactionAssaultStage.Regroup);
                    }
                    else if (AnyAgentNearAssaultTarget(preset))
                    {
                        EnterStage(FactionAssaultStage.Attack);
                    }
                    break;

                case FactionAssaultStage.Attack:
                    if (shouldRegroup)
                    {
                        EnterStage(FactionAssaultStage.Regroup);
                    }
                    break;

                case FactionAssaultStage.Regroup:
                    if (!shouldRegroup && aliveCount > 0)
                    {
                        EnterStage(FactionAssaultStage.Advance);
                    }
                    break;
            }
        }

        private bool ShouldRegroup(FactionObjectivePreset preset)
        {
            if (m_ActiveAgents.Count == 0)
            {
                return false;
            }

            float healthThreshold = preset != null ? preset.RegroupHealthThreshold : 0.3f;
            float casualtyThreshold = preset != null ? preset.CasualtyRegroupThreshold : 0.4f;
            int failureThreshold = preset != null ? Mathf.Max(1, preset.RegroupFailureThreshold) : 3;

            float avgHealth = 0f;
            int maxFailures = 0;
            for (int i = 0; i < m_ActiveAgents.Count; i++)
            {
                FactionAgentBrainBridge agent = m_ActiveAgents[i];
                if (agent == null || !agent.IsAlive)
                {
                    continue;
                }

                CrowdAgentController crowd = agent.GetComponent<CrowdAgentController>();
                if (crowd != null && crowd.CombatRuntime != null)
                {
                    avgHealth += crowd.CombatRuntime.CurrentHealth / Mathf.Max(1f, crowd.CombatRuntime.MaxHealth);
                }

                maxFailures = Mathf.Max(maxFailures, agent.ConsecutiveActionFailures);
            }

            avgHealth /= Mathf.Max(1, m_ActiveAgents.Count);
            float casualtyRate = m_WaveSpawnedCount <= 0
                ? 0f
                : Mathf.Clamp01((m_WaveSpawnedCount - m_ActiveAgents.Count) / (float)m_WaveSpawnedCount);

            return avgHealth < healthThreshold
                   || casualtyRate >= casualtyThreshold
                   || maxFailures >= failureThreshold;
        }

        private bool AnyAgentNearAssaultTarget(FactionObjectivePreset preset)
        {
            Transform playerTarget = ResolvePlayerTarget();
            Transform coreTarget = ResolveCoreTarget();
            Transform target = playerTarget != null ? playerTarget : coreTarget;
            if (target == null)
            {
                return false;
            }

            float attackRange = preset != null ? Mathf.Max(0.5f, preset.AttackRange) : 1.8f;
            float sqr = attackRange * attackRange * 1.6f;
            Vector3 pos = target.position;
            for (int i = 0; i < m_ActiveAgents.Count; i++)
            {
                FactionAgentBrainBridge agent = m_ActiveAgents[i];
                if (agent == null || !agent.IsAlive)
                {
                    continue;
                }

                if (Vector3.SqrMagnitude(agent.transform.position - pos) <= sqr)
                {
                    return true;
                }
            }

            return false;
        }

        private void EnterStage(FactionAssaultStage next)
        {
            if (m_CurrentStage == next)
            {
                return;
            }

            m_CurrentStage = next;
            m_StageEnteredAt = Time.time;
            if (m_EnableRuntimeLog)
            {
                Debug.Log($"[FactionScenarioDirector] Stage -> {next}");
            }
        }

        private void TrySpawnWave()
        {
            if (!IsSpawnEnvironmentReady())
            {
                m_NextWaveStartTime = Time.time + 0.25f;
                return;
            }

            if (Time.time < m_NextWaveStartTime)
            {
                return;
            }

            if (m_WaveRemainingSpawn <= 0)
            {
                BeginWaveIfPossible();
            }

            if (m_WaveRemainingSpawn <= 0 || Time.time < m_NextSpawnTickTime)
            {
                return;
            }

            FactionWaveDefinition wave = ResolveCurrentWave();
            int spawnPerTick = wave != null ? Mathf.Max(1, wave.SpawnPerTick) : 1;
            int aliveCap = Mathf.Max(1, m_ActiveProfile.AliveCap);

            int spawnedThisTick = 0;
            while (spawnedThisTick < spawnPerTick && m_WaveRemainingSpawn > 0 && m_ActiveAgents.Count < aliveCap)
            {
                if (!TrySpawnOneAgent())
                {
                    break;
                }

                m_WaveRemainingSpawn--;
                m_WaveSpawnedCount++;
                spawnedThisTick++;
            }

            float interval = wave != null ? Mathf.Max(0.1f, wave.SpawnIntervalSec) : m_FallbackWaveIntervalSec;
            m_NextSpawnTickTime = Time.time + Mathf.Min(0.25f, interval * 0.1f);

            if (m_WaveRemainingSpawn <= 0)
            {
                m_CurrentWaveIndex++;
                m_NextWaveStartTime = Time.time + interval;
            }
        }

        private void BeginWaveIfPossible()
        {
            FactionWaveDefinition wave = ResolveCurrentWave();
            if (wave == null)
            {
                return;
            }

            m_WaveRemainingSpawn = Mathf.Max(0, wave.SpawnBudget);
            m_WaveSpawnedCount = 0;
            m_NextSpawnTickTime = Time.time;
        }

        private bool TrySpawnOneAgent()
        {
            if (!TryResolveNextAnchor(m_ActiveProfile.FactionId, out FactionSpawnAnchor anchor) || anchor.AnchorTransform == null)
            {
                return false;
            }

            Vector3 spawnPos = ResolveSpawnPosition(anchor.AnchorTransform.position);
            Transform runtimeParent = ResolveAgentRuntimeParent();

            GameObject go;
            if (m_ActiveProfile.AgentPrefab != null)
            {
                go = Instantiate(m_ActiveProfile.AgentPrefab, spawnPos, anchor.AnchorTransform.rotation, runtimeParent);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.transform.SetPositionAndRotation(spawnPos, anchor.AnchorTransform.rotation);
                go.transform.SetParent(runtimeParent);
            }

            go.name = $"{m_ActiveProfile.FactionId}_Agent_{m_CurrentWaveIndex:D2}_{m_WaveSpawnedCount:D3}";

            CrowdAgentController agent = go.GetComponent<CrowdAgentController>();
            if (agent == null)
            {
                agent = go.AddComponent<CrowdAgentController>();
            }
            agent.CombatRole = m_ActiveProfile.CombatRole;

            FactionAgentBrainBridge bridge = go.GetComponent<FactionAgentBrainBridge>();
            if (bridge == null)
            {
                bridge = go.AddComponent<FactionAgentBrainBridge>();
            }

            bridge.Initialize(m_ActiveProfile.FactionId, ResolveObjectivePreset(), m_DefaultPlannerPreset);
            m_ActiveAgents.Add(bridge);
            bridge.SetDirective(BuildDirective());
            return true;
        }

        private Transform ResolveAgentRuntimeParent()
        {
            if (m_AgentRuntimeParent != null)
            {
                return m_AgentRuntimeParent;
            }

            if (!m_AttachSpawnedAgentsToCrowdRoot)
            {
                return transform;
            }

            if (m_CrowdCoordinator == null)
            {
                m_CrowdCoordinator = FindObjectOfType<CrowdFlowFieldCoordinator>();
            }

            if (m_CrowdCoordinator == null)
            {
                return transform;
            }

            return m_CrowdCoordinator.AgentSearchRoot != null
                ? m_CrowdCoordinator.AgentSearchRoot
                : m_CrowdCoordinator.transform;
        }

        private Vector3 ResolveSpawnPosition(Vector3 anchorPosition)
        {
            IWorld world = World.Active;
            if (world == null || !world.Initialized || world.RWAccessor == null)
            {
                return new Vector3(
                    anchorPosition.x,
                    ResolveFallbackSpawnY(anchorPosition.y),
                    anchorPosition.z);
            }

            int x = Mathf.FloorToInt(anchorPosition.x);
            int z = Mathf.FloorToInt(anchorPosition.z);
            int topY = world.RWAccessor.GetTopVisibleBlockY(x, z, int.MinValue);
            if (topY != int.MinValue)
            {
                return new Vector3(anchorPosition.x, topY + 1.05f, anchorPosition.z);
            }

            // Request anchor chunk loading so CrowdAgentController ground-snap can resolve on subsequent frames.
            if (world.ChunkManager != null)
            {
                world.ChunkManager.GetChunk(ChunkPos.GetFromAny(x, z), true, out _);
            }

            return new Vector3(
                anchorPosition.x,
                ResolveFallbackSpawnY(anchorPosition.y),
                anchorPosition.z);
        }

        private bool IsSpawnEnvironmentReady()
        {
            IWorld world = World.Active;
            return world != null && world.Initialized && world.RWAccessor != null;
        }

        private void PreloadCombatChunks()
        {
            if (Time.time < m_NextChunkPreloadTime)
            {
                return;
            }

            m_NextChunkPreloadTime = Time.time + Mathf.Max(0.1f, m_CombatChunkPreloadIntervalSec);

            IWorld world = World.Active;
            if (world == null || !world.Initialized || world.ChunkManager == null)
            {
                return;
            }

            int radius = Mathf.Max(0, m_CombatChunkPreloadRadius);
            PreloadChunksAround(world, ResolvePlayerTarget(), radius);
            PreloadChunksAround(world, m_CoreTarget, radius);
            PreloadChunksAround(world, m_RegroupPoint, radius);

            if (m_SpawnAnchors == null)
            {
                return;
            }

            for (int i = 0; i < m_SpawnAnchors.Count; i++)
            {
                FactionSpawnAnchor anchor = m_SpawnAnchors[i];
                if (anchor == null || !anchor.Enabled || anchor.AnchorTransform == null)
                {
                    continue;
                }

                PreloadChunksAround(world, anchor.AnchorTransform, radius);
            }
        }

        private static void PreloadChunksAround(IWorld world, Transform focus, int radius)
        {
            if (world == null || world.ChunkManager == null || focus == null)
            {
                return;
            }

            int x = Mathf.FloorToInt(focus.position.x);
            int z = Mathf.FloorToInt(focus.position.z);
            ChunkPos center = ChunkPos.GetFromAny(x, z);
            for (int ox = -radius; ox <= radius; ox++)
            {
                for (int oz = -radius; oz <= radius; oz++)
                {
                    world.ChunkManager.GetChunk(center.AddOffset(ox, oz), true, out _);
                }
            }
        }

        private float ResolveFallbackSpawnY(float defaultY)
        {
            Transform playerTarget = ResolvePlayerTarget();
            if (playerTarget != null)
            {
                return playerTarget.position.y;
            }

            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                return taggedPlayer.transform.position.y;
            }

            if (m_CoreTarget != null)
            {
                return m_CoreTarget.position.y;
            }

            return defaultY;
        }

        private bool TryResolveNextAnchor(string factionId, out FactionSpawnAnchor result)
        {
            result = null;
            if (m_SpawnAnchors == null || m_SpawnAnchors.Count == 0)
            {
                return false;
            }

            int count = m_SpawnAnchors.Count;
            for (int i = 0; i < count; i++)
            {
                int index = (m_SpawnAnchorCursor + i) % count;
                FactionSpawnAnchor anchor = m_SpawnAnchors[index];
                if (anchor == null || !anchor.Enabled || anchor.AnchorTransform == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(anchor.FactionId)
                    && !string.Equals(anchor.FactionId, factionId, StringComparison.Ordinal))
                {
                    continue;
                }

                m_SpawnAnchorCursor = (index + 1) % count;
                result = anchor;
                return true;
            }

            return false;
        }

        private void BroadcastDirective()
        {
            FactionRuntimeDirective directive = BuildDirective();
            for (int i = 0; i < m_ActiveAgents.Count; i++)
            {
                FactionAgentBrainBridge agent = m_ActiveAgents[i];
                if (agent == null)
                {
                    continue;
                }

                agent.SetDirective(directive);
            }
        }

        private FactionRuntimeDirective BuildDirective()
        {
            Transform regroup = ResolveRegroupPoint();
            Vector3 homeCenter = regroup != null
                ? regroup.position
                : (m_CoreTarget != null ? m_CoreTarget.position : transform.position);
            FactionObjectivePreset preset = ResolveObjectivePreset();

            return new FactionRuntimeDirective
            {
                FactionId = m_ActiveProfile != null ? m_ActiveProfile.FactionId : "hostile-assault-v1",
                Stage = m_CurrentStage,
                PlayerTarget = ResolvePlayerTarget(),
                CoreTarget = ResolveCoreTarget(),
                RegroupPoint = regroup,
                IdleTrainingTarget = m_IdleTrainingDummy,
                CoreHealthNormalized = ResolveCoreHealthNormalized(),
                AllowAwayTerrainEdit = preset != null && preset.AllowAwayTerrainEdit,
                HomeCenter = homeCenter,
                HomeRadius = preset != null ? preset.HomeTerritoryRadius : 24f,
                ObjectivePreset = preset,
            };
        }

        private void EnsureIdleTrainingDummy()
        {
            if (!m_EnableIdleTrainingDummy)
            {
                if (m_IdleTrainingDummy != null)
                {
                    Destroy(m_IdleTrainingDummy.gameObject);
                    m_IdleTrainingDummy = null;
                }

                return;
            }

            if (m_IdleTrainingDummy != null && IsTrainingDummyAlive(m_IdleTrainingDummy))
            {
                return;
            }

            if (m_IdleTrainingDummy != null)
            {
                Destroy(m_IdleTrainingDummy.gameObject);
                m_IdleTrainingDummy = null;
            }

            Vector3 spawnPos = ResolveIdleTrainingSpawnPosition();
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"{ResolveActiveFactionId()}_IdleTrainingDummy";
            go.transform.SetParent(transform);
            go.transform.position = spawnPos;
            go.transform.rotation = Quaternion.identity;

            CrowdAgentController controller = go.GetComponent<CrowdAgentController>();
            if (controller == null)
            {
                controller = go.AddComponent<CrowdAgentController>();
            }

            controller.MoveSpeed = 0f;
            controller.VerticalMoveSpeed = 0f;
            controller.RotationSpeed = 0f;
            controller.CombatRole = CombatActorRole.Enemy;
            controller.CombatMaxHealth = Mathf.Max(1f, m_IdleTrainingMaxHealth);
            controller.CombatInitialHealth = controller.CombatMaxHealth;
            controller.CombatAttackPower = 0f;
            controller.CombatDefense = 0f;
            controller.DestroyOnDespawn = false;

            if (controller.CombatRuntime != null)
            {
                controller.CombatRuntime.Role = controller.CombatRole;
                controller.CombatRuntime.DisplayName = go.name;
                controller.CombatRuntime.MaxHealth = controller.CombatMaxHealth;
                controller.CombatRuntime.CurrentHealth = controller.CombatMaxHealth;
                controller.CombatRuntime.AttackPower = controller.CombatAttackPower;
                controller.CombatRuntime.Defense = controller.CombatDefense;
                controller.CombatRuntime.Position = go.transform.position;
                controller.CombatRuntime.LifecycleState = CombatLifecycleState.Alive;
            }

            m_IdleTrainingDummy = go.transform;
        }

        private bool IsTrainingDummyAlive(Transform dummy)
        {
            if (dummy == null || !dummy.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (!dummy.TryGetComponent(out CrowdAgentController controller))
            {
                return false;
            }

            return controller.CombatRuntime != null && controller.CombatRuntime.IsAlive;
        }

        private Vector3 ResolveIdleTrainingSpawnPosition()
        {
            Vector3 origin = ResolveIdleTrainingOrigin();
            float radius = Mathf.Max(1f, m_IdleTrainingSpawnRadius);
            Vector2 offset2D = UnityEngine.Random.insideUnitCircle;
            if (offset2D.sqrMagnitude <= 0.0001f)
            {
                offset2D = Vector2.right;
            }

            offset2D = offset2D.normalized * radius;
            return origin + new Vector3(offset2D.x, 0f, offset2D.y);
        }

        private Vector3 ResolveIdleTrainingOrigin()
        {
            Transform anchor = ResolvePrimaryAnchorTransform();
            if (anchor != null)
            {
                return anchor.position;
            }

            Transform regroup = ResolveRegroupPoint();
            if (regroup != null)
            {
                return regroup.position;
            }

            if (m_CoreTarget != null)
            {
                return m_CoreTarget.position;
            }

            return transform.position;
        }

        private Transform ResolvePrimaryAnchorTransform()
        {
            if (m_SpawnAnchors == null || m_SpawnAnchors.Count == 0)
            {
                return null;
            }

            string factionId = ResolveActiveFactionId();
            for (int i = 0; i < m_SpawnAnchors.Count; i++)
            {
                FactionSpawnAnchor anchor = m_SpawnAnchors[i];
                if (anchor == null || !anchor.Enabled || anchor.AnchorTransform == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(anchor.FactionId)
                    && !string.Equals(anchor.FactionId, factionId, StringComparison.Ordinal))
                {
                    continue;
                }

                return anchor.AnchorTransform;
            }

            return null;
        }

        private string ResolveActiveFactionId()
        {
            return m_ActiveProfile != null && !string.IsNullOrWhiteSpace(m_ActiveProfile.FactionId)
                ? m_ActiveProfile.FactionId
                : "hostile-assault-v1";
        }

        private void CleanupDeadAgents()
        {
            for (int i = m_ActiveAgents.Count - 1; i >= 0; i--)
            {
                FactionAgentBrainBridge agent = m_ActiveAgents[i];
                if (agent == null || !agent.IsAlive)
                {
                    m_ActiveAgents.RemoveAt(i);
                }
            }
        }

        private Transform ResolvePlayerTarget()
        {
            IWorld world = World.Active;
            return world != null ? world.PlayerTransform : null;
        }

        private Transform ResolveCoreTarget()
        {
            return m_CoreTarget;
        }

        private Transform ResolveRegroupPoint()
        {
            if (m_RegroupPoint != null)
            {
                return m_RegroupPoint;
            }

            return m_CoreTarget;
        }

        private float ResolveCoreHealthNormalized()
        {
            if (m_CoreTarget == null)
            {
                return 1f;
            }

            if (m_CoreTarget.TryGetComponent(out PlayerEntity player) && player.CombatRuntime != null)
            {
                return Mathf.Clamp01(player.CombatRuntime.CurrentHealth / Mathf.Max(1f, player.CombatRuntime.MaxHealth));
            }

            if (m_CoreTarget.TryGetComponent(out CrowdAgentController agent) && agent.CombatRuntime != null)
            {
                return Mathf.Clamp01(agent.CombatRuntime.CurrentHealth / Mathf.Max(1f, agent.CombatRuntime.MaxHealth));
            }

            return 1f;
        }

        private FactionObjectivePreset ResolveObjectivePreset()
        {
            return m_DefaultObjectivePreset;
        }

        private FactionWaveDefinition ResolveCurrentWave()
        {
            if (m_ActiveProfile == null || m_ActiveProfile.Waves == null || m_ActiveProfile.Waves.Count == 0)
            {
                return null;
            }

            int idx = Mathf.Abs(m_CurrentWaveIndex) % m_ActiveProfile.Waves.Count;
            return m_ActiveProfile.Waves[idx];
        }
    }
}
