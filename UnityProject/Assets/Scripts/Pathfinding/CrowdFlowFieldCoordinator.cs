using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace Minecraft.Pathfinding
{
    [DisallowMultipleComponent]
    public class CrowdFlowFieldCoordinator : MonoBehaviour
    {
        [Header("Agents")]
        [Tooltip("自动收集子节点上的 CrowdAgentController")]
        public bool AutoFindAgents = true;

        [Tooltip("自动收集时是否包含未激活对象")]
        public bool IncludeInactiveAgents = false;

        [Tooltip("自动收集轮询间隔（秒）")]
        public float AutoRefreshInterval = 0.5f;

        [Tooltip("自动收集范围根节点（为空时使用当前对象）")]
        public Transform AgentSearchRoot;

        [Tooltip("强制所有代理共享同一目标（用于压测）")]
        public bool ForceSharedTarget = true;

        [Tooltip("参与更新的代理")]
        public List<CrowdAgentController> Agents = new List<CrowdAgentController>();

        [Header("Spawn (Benchmark)")]
        [Tooltip("启动时自动生成代理")]
        public bool SpawnOnStart = false;

        [Tooltip("代理预制体（可选）")]
        public GameObject AgentPrefab;

        [Tooltip("自动生成代理数量")]
        public int SpawnCount = 300;

        [Tooltip("每帧最多生成多少代理（分帧生成，避免卡顿）")]
        public int SpawnPerFrame = 12;

        [Tooltip("生成半径")]
        public float SpawnRadius = 24f;

        [Tooltip("生成时寻找可行走点半径")]
        public int SpawnSearchRadius = 16;

        [Header("Target")]
        [Tooltip("动态目标（优先）")]
        public Transform TargetTransform;

        [Tooltip("没有动态目标时使用")]
        public Vector3Int StaticTarget = new Vector3Int(0, 64, 0);

        [Header("Flow Field")]
        [Tooltip("场流节点预算（越大覆盖越广）")]
        public int FlowFieldMaxNodes = 25000;

        [Tooltip("场流缓存寿命（秒）")]
        public float FlowFieldCacheLifetime = 0.25f;

        [Tooltip("场流起终点可行走搜索半径")]
        public int FlowFieldSearchRadius = 12;

        [Tooltip("重复校验/重建场流的最小间隔（秒）")]
        public float FlowFieldPrepareInterval = 1.2f;

        [Header("Runtime")]
        [Tooltip("协调器Tick间隔（秒），越大越省CPU")]
        public float TickInterval = 0.05f;

        [Tooltip("随机寻路采样最小间隔（秒）")]
        public float RepathMinInterval = 1f;

        [Tooltip("随机寻路采样最大间隔（秒）")]
        public float RepathMaxInterval = 2f;

        [Tooltip("目标在该半径内时不再采样寻路（米）")]
        public float NoPathfindRadius = 2f;

        [Header("Formation (Shared Target)")]
        [Tooltip("共享目标时，是否为每个代理分配不同终点（目标周围阵型）")]
        public bool EnableTargetFormation = true;

        [Tooltip("阵型间距（格）")]
        public float FormationSpacing = 1.5f;

        [Tooltip("阵型点修正到可行走点的搜索半径")]
        public int FormationSearchRadius = 4;

        [Tooltip("阵型重建最小间隔（秒）")]
        public float FormationRebuildInterval = 0.35f;

        [Tooltip("阵型最大扩展环数（防止无界搜索）")]
        public int FormationMaxRings = 24;

        [Tooltip("进入该半径后，从共享路径切换为直接靠拢阵型点（米）")]
        public float FormationHandoverRadius = 5f;

        [Tooltip("编队接管退出缓冲（米），避免边界来回抖动")]
        public float FormationHandoverExitBuffer = 1.5f;

        [Header("Flow Target Smoothing")]
        [Tooltip("共享目标移动时，是否对FlowField目标做低频平滑更新")]
        public bool EnableSharedTargetSmoothing = true;

        [Tooltip("FlowField共享目标最小更新时间隔（秒）")]
        public float SharedTargetUpdateInterval = 0.45f;

        [Tooltip("共享目标偏移超过该距离（格）时才触发更新")]
        public float SharedTargetRebuildDistance = 3f;

        [Tooltip("共享目标最大允许滞后距离（格），超过则立即更新")]
        public float SharedTargetMaxLagDistance = 10f;

        [Tooltip("共享Flow目标在XZ平面的量化步长（格）")]
        public int SharedTargetGridStep = 2;

        [Tooltip("共享Flow目标是否锁定到上一帧Y（减少垂直抖动重建）")]
        public bool SharedTargetLockY = true;

        [Tooltip("移动共享目标时限制节点预算峰值（减少重建尖峰）")]
        public bool LimitMovingSharedTargetNodes = true;

        [Tooltip("移动共享目标时的节点预算上限")]
        public int MovingSharedTargetMaxNodes = 8000;

        [Header("Local Avoidance (RVO-like)")]
        [Tooltip("是否启用局部避障（多人跟随建议开启）")]
        public bool EnableLocalAvoidance = true;

        [Tooltip("单个代理半径（米）")]
        public float AgentRadius = 0.35f;

        [Tooltip("邻居检测半径（米）")]
        public float NeighborDistance = 2f;

        [Tooltip("避障时间视野（秒）")]
        public float AvoidanceTimeHorizon = 0.6f;

        [Tooltip("避障强度系数")]
        public float AvoidanceStrength = 1.25f;

        [Tooltip("局部避障允许的最大邻居数")]
        public int MaxAvoidanceNeighbors = 8;

        [Tooltip("避障空间哈希格尺寸（米）")]
        public float AvoidanceCellSize = 1.5f;

        [Tooltip("允许超过基础速度的最大倍率（用于绕行）")]
        public float AvoidanceMaxSpeedMultiplier = 1.15f;

        [Tooltip("靠近目标节点时避障衰减距离（米）")]
        public float AvoidanceFadeNearNodeDistance = 1.2f;

        [Tooltip("速度平滑最大加速度（米/秒^2）")]
        public float MaxPlanarAcceleration = 24f;

        [Tooltip("微速度死区（米/秒），小于该值直接置0以消除抖动")]
        public float VelocityDeadzone = 0.08f;

        [Tooltip("启用按活跃代理数缩放的节点预算")]
        public bool UseAdaptiveNodeBudget = true;

        [Tooltip("每个活跃代理分配的场流节点预算（自适应时使用）")]
        public int NodesPerActiveAgent = 600;

        [System.NonSerialized] private IWorld m_World;
        [System.NonSerialized] private bool m_HasSpawned;
        [System.NonSerialized] private int m_SpawnedCount;
        [System.NonSerialized] private int m_LastMovedCount;
        [System.NonSerialized] private int m_LastFailedCount;
        [System.NonSerialized] private readonly Dictionary<Vector3Int, List<CrowdAgentController>> m_TargetGroups = new Dictionary<Vector3Int, List<CrowdAgentController>>();
        [System.NonSerialized] private readonly List<Vector3Int> m_GroupTargets = new List<Vector3Int>();
        [System.NonSerialized] private readonly Dictionary<Vector3Int, Vector3Int> m_PreparedTargetByRequestedTarget = new Dictionary<Vector3Int, Vector3Int>();
        [System.NonSerialized] private readonly Dictionary<Vector3Int, float> m_NextPrepareCheckTimeByRequestedTarget = new Dictionary<Vector3Int, float>();
        [System.NonSerialized] private readonly Dictionary<Vector3Int, float> m_LastRequestedTargetUseTime = new Dictionary<Vector3Int, float>();
        [System.NonSerialized] private readonly List<Vector3Int> m_PrepareTargetCleanupBuffer = new List<Vector3Int>(64);
        [System.NonSerialized] private readonly Stack<List<CrowdAgentController>> m_GroupListPool = new Stack<List<CrowdAgentController>>();
        [System.NonSerialized] private bool m_AgentsDirty = true;
        [System.NonSerialized] private float m_NextAgentRefreshTime;
        [System.NonSerialized] private float m_NextTickTime;
        [System.NonSerialized] private readonly Dictionary<CrowdAgentController, AgentRuntimeState> m_AgentRuntimeStates = new Dictionary<CrowdAgentController, AgentRuntimeState>();
        [System.NonSerialized] private readonly List<CrowdAgentController> m_RuntimeStateCleanupBuffer = new List<CrowdAgentController>(64);
        [System.NonSerialized] private readonly List<CrowdAgentController> m_MoveAgentsBuffer = new List<CrowdAgentController>(256);
        [System.NonSerialized] private readonly Dictionary<CrowdAgentController, Vector3Int> m_FormationTargets = new Dictionary<CrowdAgentController, Vector3Int>();
        [System.NonSerialized] private readonly List<CrowdAgentController> m_FormationAgentsBuffer = new List<CrowdAgentController>(256);
        [System.NonSerialized] private readonly List<Vector3Int> m_FormationCandidatesBuffer = new List<Vector3Int>(512);
        [System.NonSerialized] private readonly HashSet<Vector3Int> m_FormationUsedTargets = new HashSet<Vector3Int>();
        [System.NonSerialized] private readonly Dictionary<Vector3Int, Vector3Int?> m_FormationWalkableCache = new Dictionary<Vector3Int, Vector3Int?>();
        [System.NonSerialized] private bool m_FormationDirty = true;
        [System.NonSerialized] private bool m_HasFormationCenter;
        [System.NonSerialized] private Vector3Int m_LastFormationCenter;
        [System.NonSerialized] private float m_NextFormationRebuildTime;
        [System.NonSerialized] private bool m_HasSharedFlowTarget;
        [System.NonSerialized] private Vector3Int m_SharedFlowTarget;
        [System.NonSerialized] private float m_NextSharedTargetUpdateTime;
        [System.NonSerialized] private int m_SharedFlowTargetVersion;
        [System.NonSerialized] private int m_LastPreparedFlowTargetVersion = -1;
        [System.NonSerialized] private readonly Dictionary<Vector2Int, List<CrowdAgentController>> m_AvoidanceGrid = new Dictionary<Vector2Int, List<CrowdAgentController>>();
        [System.NonSerialized] private readonly List<Vector2Int> m_AvoidanceGridKeys = new List<Vector2Int>(128);
        [System.NonSerialized] private readonly Stack<List<CrowdAgentController>> m_AvoidanceGridListPool = new Stack<List<CrowdAgentController>>();
        [System.NonSerialized] private readonly List<CrowdAgentController> m_NeighborBuffer = new List<CrowdAgentController>(32);

        private struct AgentRuntimeState
        {
            public float NextPathSampleTime;
            public Vector3Int NextNode;
            public bool HasNextNode;
            public int LastSeenFrame;
            public Vector2 CurrentPlanarVelocity;
            public Vector2 DesiredPlanarVelocity;
            public int FlowTargetVersion;
            public bool InFormationHandover;
        }

        private void Start()
        {
            TryAcquireWorld();
            TrySpawnAgents();
            RefreshAgentsIfNeeded();
        }

        private void OnEnable()
        {
            m_AgentsDirty = true;
            m_FormationDirty = true;
            m_HasSharedFlowTarget = false;
            m_NextSharedTargetUpdateTime = 0f;
            m_LastPreparedFlowTargetVersion = -1;
            m_PreparedTargetByRequestedTarget.Clear();
            m_NextPrepareCheckTimeByRequestedTarget.Clear();
            m_LastRequestedTargetUseTime.Clear();
        }

        private void OnTransformChildrenChanged()
        {
            if (AgentSearchRoot == null || AgentSearchRoot == transform)
            {
                m_AgentsDirty = true;
                m_FormationDirty = true;
            }
        }

        private void Update()
        {
            Profiler.BeginSample("CrowdFlowFieldCoordinator.Update");
            try
            {
                TryAcquireWorld();
                if (m_World == null)
                {
                    return;
                }

                Profiler.BeginSample("CrowdFlowFieldCoordinator.TrySpawnAgents");
                TrySpawnAgents();
                Profiler.EndSample();

                Profiler.BeginSample("CrowdFlowFieldCoordinator.RefreshAgents");
                RefreshAgentsIfNeeded();
                Profiler.EndSample();

                Profiler.BeginSample("CrowdFlowFieldCoordinator.MoveCachedAgents");
                MoveAgentsWithCachedNextNode(Time.deltaTime);
                Profiler.EndSample();

                float now = Time.unscaledTime;
                if (now < m_NextTickTime)
                {
                    return;
                }

                m_NextTickTime = now + Mathf.Max(0.01f, TickInterval);

                Profiler.BeginSample("CrowdFlowFieldCoordinator.TickAgents");
                TickAgents();
                Profiler.EndSample();
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        private void TryAcquireWorld()
        {
            m_World ??= World.Active;
        }

        private void RefreshAgentsIfNeeded()
        {
            if (!AutoFindAgents)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (!m_AgentsDirty && now < m_NextAgentRefreshTime)
            {
                return;
            }

            Transform searchRoot = AgentSearchRoot != null ? AgentSearchRoot : transform;
            Agents.Clear();
            searchRoot.GetComponentsInChildren(IncludeInactiveAgents, Agents);
            m_AgentsDirty = false;
            m_FormationDirty = true;
            m_NextAgentRefreshTime = now + Mathf.Max(0.05f, AutoRefreshInterval);
        }

        private void TrySpawnAgents()
        {
            if (!SpawnOnStart || m_HasSpawned)
            {
                return;
            }

            if (SpawnCount <= 0)
            {
                m_HasSpawned = true;
                return;
            }

            Transform spawnParent = GetOrCreateSpawnRoot();
            int spawnPerFrame = Mathf.Max(1, SpawnPerFrame);
            int spawnedThisFrame = 0;
            Vector3 spawnCenter = TargetTransform != null
                ? TargetTransform.position
                : new Vector3(StaticTarget.x + 0.5f, StaticTarget.y + 1f, StaticTarget.z + 0.5f);

            while (m_SpawnedCount < SpawnCount && spawnedThisFrame < spawnPerFrame)
            {
                Vector2 offset = Random.insideUnitCircle * SpawnRadius;
                Vector3 worldPos = spawnCenter + new Vector3(offset.x, 0f, offset.y);

                if (m_World != null && SpawnSearchRadius > 0)
                {
                    Vector3Int probe = new Vector3Int(
                        Mathf.FloorToInt(worldPos.x),
                        Mathf.FloorToInt(worldPos.y),
                        Mathf.FloorToInt(worldPos.z));
                    Vector3Int? walkable = AStarPathfinding.FindNearestWalkableNode(probe, m_World, SpawnSearchRadius);
                    if (walkable.HasValue)
                    {
                        // Keep target+offset xz, only correct vertical placement to avoid underground spawn.
                        worldPos.y = walkable.Value.y + 1f;
                    }
                }

                GameObject go;
                if (AgentPrefab != null)
                {
                    go = Instantiate(AgentPrefab, worldPos, Quaternion.identity, spawnParent);
                }
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    go.name = $"CrowdAgent_{m_SpawnedCount:D3}";
                    go.transform.SetParent(spawnParent);
                    go.transform.position = worldPos;
                }

                CrowdAgentController agent = go.GetComponent<CrowdAgentController>();
                if (agent == null)
                {
                    agent = go.AddComponent<CrowdAgentController>();
                }

                Agents.Add(agent);
                m_SpawnedCount++;
                spawnedThisFrame++;
            }

            if (m_SpawnedCount >= SpawnCount)
            {
                m_HasSpawned = true;
            }

            m_AgentsDirty = true;
            m_FormationDirty = true;
        }

        private void MoveAgentsWithCachedNextNode(float deltaTime)
        {
            if (deltaTime <= 0f || Agents == null || Agents.Count == 0)
            {
                m_LastMovedCount = 0;
                return;
            }

            m_MoveAgentsBuffer.Clear();
            for (int i = 0; i < Agents.Count; i++)
            {
                CrowdAgentController agent = Agents[i];
                if (agent == null || !agent.isActiveAndEnabled)
                {
                    continue;
                }

                if (!m_AgentRuntimeStates.TryGetValue(agent, out AgentRuntimeState runtime) || !runtime.HasNextNode)
                {
                    continue;
                }

                if (agent.IsNearNode(runtime.NextNode))
                {
                    runtime.CurrentPlanarVelocity = Vector2.zero;
                    runtime.DesiredPlanarVelocity = Vector2.zero;
                    m_AgentRuntimeStates[agent] = runtime;
                    continue;
                }

                runtime.DesiredPlanarVelocity = agent.GetDesiredPlanarVelocity(runtime.NextNode);
                if (runtime.DesiredPlanarVelocity.sqrMagnitude <= 0.0001f)
                {
                    runtime.CurrentPlanarVelocity = Vector2.zero;
                    runtime.DesiredPlanarVelocity = Vector2.zero;
                    m_AgentRuntimeStates[agent] = runtime;
                    continue;
                }

                m_AgentRuntimeStates[agent] = runtime;
                m_MoveAgentsBuffer.Add(agent);
            }

            if (m_MoveAgentsBuffer.Count == 0)
            {
                m_LastMovedCount = 0;
                return;
            }

            if (EnableLocalAvoidance)
            {
                BuildAvoidanceGrid();
            }

            int moved = 0;
            for (int i = 0; i < m_MoveAgentsBuffer.Count; i++)
            {
                CrowdAgentController agent = m_MoveAgentsBuffer[i];
                if (!m_AgentRuntimeStates.TryGetValue(agent, out AgentRuntimeState runtime))
                {
                    continue;
                }

                Vector2 targetPlanarVelocity = runtime.DesiredPlanarVelocity;
                if (EnableLocalAvoidance)
                {
                    Vector2 avoidance = ComputeReciprocalAvoidance(agent, runtime);
                    float fadeDistance = Mathf.Max(agent.NodeReachDistance * 2f, AvoidanceFadeNearNodeDistance);
                    float distanceToNode = agent.GetPlanarDistanceToNode(runtime.NextNode);
                    float fadeDenominator = Mathf.Max(0.001f, fadeDistance - agent.NodeReachDistance);
                    float avoidanceFade = Mathf.Clamp01((distanceToNode - agent.NodeReachDistance) / fadeDenominator);
                    targetPlanarVelocity += avoidance * Mathf.Max(0f, AvoidanceStrength) * avoidanceFade;
                }

                float maxSpeed = Mathf.Max(0.1f, agent.MoveSpeed * Mathf.Max(0.5f, AvoidanceMaxSpeedMultiplier));
                float speedSq = targetPlanarVelocity.sqrMagnitude;
                if (speedSq > maxSpeed * maxSpeed)
                {
                    targetPlanarVelocity = targetPlanarVelocity / Mathf.Sqrt(speedSq) * maxSpeed;
                }

                float maxAccel = Mathf.Max(0.1f, MaxPlanarAcceleration);
                Vector2 finalPlanarVelocity = Vector2.MoveTowards(
                    runtime.CurrentPlanarVelocity,
                    targetPlanarVelocity,
                    maxAccel * deltaTime);

                float deadzone = Mathf.Max(0f, VelocityDeadzone);
                if (finalPlanarVelocity.sqrMagnitude < deadzone * deadzone &&
                    targetPlanarVelocity.sqrMagnitude < deadzone * deadzone)
                {
                    finalPlanarVelocity = Vector2.zero;
                }

                agent.MoveWithPlanarVelocity(runtime.NextNode, new Vector3(finalPlanarVelocity.x, 0f, finalPlanarVelocity.y), deltaTime);
                runtime.CurrentPlanarVelocity = finalPlanarVelocity;
                m_AgentRuntimeStates[agent] = runtime;
                moved++;
            }

            if (EnableLocalAvoidance)
            {
                ReleaseAvoidanceGridToPool();
            }

            m_LastMovedCount = moved;
        }

        private void BuildAvoidanceGrid()
        {
            ReleaseAvoidanceGridToPool();

            float cellSize = Mathf.Max(0.5f, AvoidanceCellSize);
            for (int i = 0; i < m_MoveAgentsBuffer.Count; i++)
            {
                CrowdAgentController agent = m_MoveAgentsBuffer[i];
                Vector2Int cell = GetAvoidanceCell(agent.transform.position, cellSize);
                if (!m_AvoidanceGrid.TryGetValue(cell, out List<CrowdAgentController> list))
                {
                    list = m_AvoidanceGridListPool.Count > 0 ? m_AvoidanceGridListPool.Pop() : new List<CrowdAgentController>(16);
                    m_AvoidanceGrid[cell] = list;
                    m_AvoidanceGridKeys.Add(cell);
                }

                list.Add(agent);
            }
        }

        private void ReleaseAvoidanceGridToPool()
        {
            for (int i = 0; i < m_AvoidanceGridKeys.Count; i++)
            {
                Vector2Int cell = m_AvoidanceGridKeys[i];
                if (!m_AvoidanceGrid.TryGetValue(cell, out List<CrowdAgentController> list))
                {
                    continue;
                }

                list.Clear();
                m_AvoidanceGridListPool.Push(list);
            }

            m_AvoidanceGrid.Clear();
            m_AvoidanceGridKeys.Clear();
            m_NeighborBuffer.Clear();
        }

        private Vector2 ComputeReciprocalAvoidance(CrowdAgentController agent, AgentRuntimeState runtime)
        {
            m_NeighborBuffer.Clear();

            float neighborDistance = Mathf.Max(AgentRadius * 2.5f, NeighborDistance);
            float cellSize = Mathf.Max(0.5f, AvoidanceCellSize);
            int searchRange = Mathf.Max(1, Mathf.CeilToInt(neighborDistance / cellSize));
            int maxNeighbors = Mathf.Max(1, MaxAvoidanceNeighbors);
            float neighborDistanceSq = neighborDistance * neighborDistance;

            Vector3 pos3 = agent.transform.position;
            Vector2 selfPos = new Vector2(pos3.x, pos3.z);
            Vector2Int centerCell = GetAvoidanceCell(pos3, cellSize);

            for (int x = -searchRange; x <= searchRange; x++)
            {
                for (int z = -searchRange; z <= searchRange; z++)
                {
                    Vector2Int cell = new Vector2Int(centerCell.x + x, centerCell.y + z);
                    if (!m_AvoidanceGrid.TryGetValue(cell, out List<CrowdAgentController> list))
                    {
                        continue;
                    }

                    for (int i = 0; i < list.Count; i++)
                    {
                        CrowdAgentController neighbor = list[i];
                        if (neighbor == null || neighbor == agent)
                        {
                            continue;
                        }

                        Vector3 nPos3 = neighbor.transform.position;
                        float dx = nPos3.x - pos3.x;
                        float dz = nPos3.z - pos3.z;
                        if (dx * dx + dz * dz > neighborDistanceSq)
                        {
                            continue;
                        }

                        m_NeighborBuffer.Add(neighbor);
                        if (m_NeighborBuffer.Count >= maxNeighbors)
                        {
                            break;
                        }
                    }

                    if (m_NeighborBuffer.Count >= maxNeighbors)
                    {
                        break;
                    }
                }

                if (m_NeighborBuffer.Count >= maxNeighbors)
                {
                    break;
                }
            }

            if (m_NeighborBuffer.Count == 0)
            {
                return Vector2.zero;
            }

            float safeRadius = Mathf.Max(0.1f, AgentRadius) * 2f;
            float horizon = Mathf.Max(0.05f, AvoidanceTimeHorizon);
            Vector2 avoidance = Vector2.zero;
            int avoidCount = 0;
            Vector2 selfVelocity = runtime.CurrentPlanarVelocity.sqrMagnitude > 0.0001f
                ? runtime.CurrentPlanarVelocity
                : runtime.DesiredPlanarVelocity;

            for (int i = 0; i < m_NeighborBuffer.Count; i++)
            {
                CrowdAgentController neighbor = m_NeighborBuffer[i];
                Vector3 neighborPos3 = neighbor.transform.position;
                Vector2 toNeighbor = new Vector2(neighborPos3.x - pos3.x, neighborPos3.z - pos3.z);
                float distSq = toNeighbor.sqrMagnitude;
                if (distSq <= 0.000001f)
                {
                    continue;
                }

                float dist = Mathf.Sqrt(distSq);
                Vector2 normal = toNeighbor / dist;
                if (dist < safeRadius)
                {
                    float penetration = (safeRadius - dist) / safeRadius;
                    avoidance -= normal * (penetration + 0.5f);
                    avoidCount++;
                    continue;
                }

                Vector2 neighborVelocity = Vector2.zero;
                if (m_AgentRuntimeStates.TryGetValue(neighbor, out AgentRuntimeState neighborRuntime))
                {
                    neighborVelocity = neighborRuntime.CurrentPlanarVelocity.sqrMagnitude > 0.0001f
                        ? neighborRuntime.CurrentPlanarVelocity
                        : neighborRuntime.DesiredPlanarVelocity;
                }

                Vector2 relativeVelocity = selfVelocity - neighborVelocity;
                float relVelSq = relativeVelocity.sqrMagnitude;
                if (relVelSq <= 0.0001f)
                {
                    continue;
                }

                float timeToClosest = -Vector2.Dot(toNeighbor, relativeVelocity) / relVelSq;
                if (timeToClosest <= 0f || timeToClosest > horizon)
                {
                    continue;
                }

                Vector2 closestOffset = toNeighbor + relativeVelocity * timeToClosest;
                float closestDist = closestOffset.magnitude;
                if (closestDist >= safeRadius)
                {
                    continue;
                }

                Vector2 avoidNormal;
                if (closestDist > 0.0001f)
                {
                    avoidNormal = closestOffset / closestDist;
                }
                else
                {
                    avoidNormal = new Vector2(-normal.y, normal.x);
                }

                float urgency = 1f - (timeToClosest / horizon);
                float pressure = (safeRadius - closestDist) / safeRadius;
                avoidance -= avoidNormal * (urgency * pressure);
                avoidCount++;
            }

            if (avoidCount > 0)
            {
                avoidance /= avoidCount;
            }

            return avoidance;
        }

        private static Vector2Int GetAvoidanceCell(Vector3 position, float cellSize)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / cellSize),
                Mathf.FloorToInt(position.z / cellSize));
        }

        private Vector3Int ResolveAgentTarget(CrowdAgentController agent, Vector3Int sharedTarget)
        {
            if (!ForceSharedTarget)
            {
                return agent.HasTarget ? agent.TargetBlock : sharedTarget;
            }

            if (!EnableTargetFormation)
            {
                return sharedTarget;
            }

            return m_FormationTargets.TryGetValue(agent, out Vector3Int assigned) ? assigned : sharedTarget;
        }

        private void EnsureFormationTargets(Vector3Int sharedTarget, float now)
        {
            if (!EnableTargetFormation)
            {
                return;
            }

            bool centerChanged = !m_HasFormationCenter || m_LastFormationCenter != sharedTarget;
            bool intervalElapsed = now >= m_NextFormationRebuildTime;
            if (!m_FormationDirty && !centerChanged && !intervalElapsed)
            {
                return;
            }

            RebuildFormationTargets(sharedTarget);
            m_HasFormationCenter = true;
            m_LastFormationCenter = sharedTarget;
            m_NextFormationRebuildTime = now + Mathf.Max(0.05f, FormationRebuildInterval);
            m_FormationDirty = false;
        }

        private void RebuildFormationTargets(Vector3Int sharedTarget)
        {
            m_FormationTargets.Clear();
            m_FormationAgentsBuffer.Clear();
            m_FormationUsedTargets.Clear();
            m_FormationWalkableCache.Clear();

            if (Agents == null || Agents.Count == 0)
            {
                return;
            }

            for (int i = 0; i < Agents.Count; i++)
            {
                CrowdAgentController agent = Agents[i];
                if (agent == null || !agent.isActiveAndEnabled)
                {
                    continue;
                }

                m_FormationAgentsBuffer.Add(agent);
            }

            if (m_FormationAgentsBuffer.Count == 0)
            {
                return;
            }

            m_FormationAgentsBuffer.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
            int spacingInCells = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1f, FormationSpacing)));
            BuildFormationCandidates(sharedTarget, spacingInCells, m_FormationAgentsBuffer.Count);

            for (int i = 0; i < m_FormationAgentsBuffer.Count; i++)
            {
                CrowdAgentController agent = m_FormationAgentsBuffer[i];
                Vector3Int assigned = sharedTarget;
                bool hasAssignment = false;

                for (int c = 0; c < m_FormationCandidatesBuffer.Count; c++)
                {
                    Vector3Int candidate = m_FormationCandidatesBuffer[c];
                    if (!TryResolveFormationPoint(candidate, out Vector3Int resolved))
                    {
                        continue;
                    }

                    if (!m_FormationUsedTargets.Add(resolved))
                    {
                        continue;
                    }

                    assigned = resolved;
                    hasAssignment = true;
                    break;
                }

                if (!hasAssignment && TryResolveFormationPoint(sharedTarget, out Vector3Int centerResolved) && m_FormationUsedTargets.Add(centerResolved))
                {
                    assigned = centerResolved;
                }

                m_FormationTargets[agent] = assigned;
            }
        }

        private void BuildFormationCandidates(Vector3Int center, int spacingInCells, int agentCount)
        {
            m_FormationCandidatesBuffer.Clear();
            m_FormationCandidatesBuffer.Add(center);

            int requiredCandidates = Mathf.Max(agentCount * 3, agentCount + 8);
            int maxRings = Mathf.Max(1, FormationMaxRings);

            for (int ring = 1; ring <= maxRings && m_FormationCandidatesBuffer.Count < requiredCandidates; ring++)
            {
                int r = ring * spacingInCells;

                for (int x = -r; x <= r; x += spacingInCells)
                {
                    m_FormationCandidatesBuffer.Add(new Vector3Int(center.x + x, center.y, center.z - r));
                    m_FormationCandidatesBuffer.Add(new Vector3Int(center.x + x, center.y, center.z + r));
                }

                for (int z = -r + spacingInCells; z <= r - spacingInCells; z += spacingInCells)
                {
                    m_FormationCandidatesBuffer.Add(new Vector3Int(center.x - r, center.y, center.z + z));
                    m_FormationCandidatesBuffer.Add(new Vector3Int(center.x + r, center.y, center.z + z));
                }
            }
        }

        private bool TryResolveFormationPoint(Vector3Int candidate, out Vector3Int resolved)
        {
            if (m_FormationWalkableCache.TryGetValue(candidate, out Vector3Int? cached))
            {
                if (cached.HasValue)
                {
                    resolved = cached.Value;
                    return true;
                }

                resolved = candidate;
                return false;
            }

            Vector3Int? walkable = null;
            if (m_World != null)
            {
                int radius = Mathf.Max(0, FormationSearchRadius);
                if (radius > 0)
                {
                    walkable = AStarPathfinding.FindNearestWalkableNode(candidate, m_World, radius);
                }
                else if (AStarPathfinding.IsWalkableNode(candidate, m_World))
                {
                    walkable = candidate;
                }
            }

            m_FormationWalkableCache[candidate] = walkable;
            if (walkable.HasValue)
            {
                resolved = walkable.Value;
                return true;
            }

            resolved = candidate;
            return false;
        }

        private Vector3Int ResolveSharedFlowTarget(Vector3Int liveTarget, float now)
        {
            Vector3Int candidateTarget = QuantizeSharedTarget(liveTarget);

            if (!ForceSharedTarget || !EnableSharedTargetSmoothing)
            {
                SetSharedFlowTarget(candidateTarget, now, false);
                return candidateTarget;
            }

            if (!m_HasSharedFlowTarget)
            {
                SetSharedFlowTarget(candidateTarget, now, true);
                return m_SharedFlowTarget;
            }

            float lagDistanceSq = GetGridDistanceSqXZ(m_SharedFlowTarget, candidateTarget);
            float maxLag = Mathf.Max(0.5f, SharedTargetMaxLagDistance);
            float rebuildDistance = Mathf.Max(0.5f, SharedTargetRebuildDistance);
            bool exceededLag = lagDistanceSq >= maxLag * maxLag;
            bool exceededRebuildDistance = lagDistanceSq >= rebuildDistance * rebuildDistance;
            bool intervalElapsed = now >= m_NextSharedTargetUpdateTime;

            if (exceededLag || (intervalElapsed && exceededRebuildDistance))
            {
                SetSharedFlowTarget(candidateTarget, now, true);
            }

            return m_SharedFlowTarget;
        }

        private Vector3Int QuantizeSharedTarget(Vector3Int liveTarget)
        {
            int step = Mathf.Max(1, SharedTargetGridStep);
            int x = step > 1 ? Mathf.RoundToInt((float)liveTarget.x / step) * step : liveTarget.x;
            int z = step > 1 ? Mathf.RoundToInt((float)liveTarget.z / step) * step : liveTarget.z;
            int y = liveTarget.y;

            if (SharedTargetLockY && m_HasSharedFlowTarget)
            {
                y = m_SharedFlowTarget.y;
            }

            return new Vector3Int(x, y, z);
        }

        private void SetSharedFlowTarget(Vector3Int target, float now, bool scheduleNextUpdate)
        {
            if (!m_HasSharedFlowTarget || m_SharedFlowTarget != target)
            {
                m_SharedFlowTarget = target;
                m_HasSharedFlowTarget = true;
                m_SharedFlowTargetVersion++;
            }
            else
            {
                m_HasSharedFlowTarget = true;
            }

            if (scheduleNextUpdate)
            {
                m_NextSharedTargetUpdateTime = now + Mathf.Max(0.02f, SharedTargetUpdateInterval);
            }
        }

        private static float GetGridDistanceSqXZ(Vector3Int a, Vector3Int b)
        {
            int dx = a.x - b.x;
            int dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private void TickAgents()
        {
            if (Agents == null || Agents.Count == 0)
            {
                return;
            }

            Vector3Int liveSharedTarget = TargetTransform != null
                ? new Vector3Int(Mathf.FloorToInt(TargetTransform.position.x), Mathf.FloorToInt(TargetTransform.position.y), Mathf.FloorToInt(TargetTransform.position.z))
                : StaticTarget;

            float now = Time.unscaledTime;
            Vector3Int flowSharedTarget = ResolveSharedFlowTarget(liveSharedTarget, now);
            int flowTargetVersion = m_SharedFlowTargetVersion;

            int maxNodesCap = Mathf.Max(1000, FlowFieldMaxNodes);
            if (ForceSharedTarget &&
                TargetTransform != null &&
                EnableSharedTargetSmoothing &&
                LimitMovingSharedTargetNodes)
            {
                maxNodesCap = Mathf.Min(maxNodesCap, Mathf.Max(1000, MovingSharedTargetMaxNodes));
            }

            float cacheLifetime = Mathf.Max(0.05f, FlowFieldCacheLifetime);
            int searchRadius = Mathf.Max(2, FlowFieldSearchRadius);
            ReleaseTargetGroupsToPool();

            int currentFrame = Time.frameCount;
            int activeCount = 0;
            int failed = 0;
            int sampledAgentCount = 0;

            if (ForceSharedTarget && EnableTargetFormation)
            {
                EnsureFormationTargets(liveSharedTarget, now);
            }

            Profiler.BeginSample("CrowdFlowFieldCoordinator.CollectAndMoveCached");
            for (int i = 0; i < Agents.Count; i++)
            {
                CrowdAgentController agent = Agents[i];
                if (agent == null || !agent.isActiveAndEnabled)
                {
                    continue;
                }

                activeCount++;

                Vector3Int target = ResolveAgentTarget(agent, liveSharedTarget);
                Vector3Int pathTarget = ForceSharedTarget ? flowSharedTarget : target;
                if (ForceSharedTarget || !agent.HasTarget || agent.TargetBlock != target)
                {
                    agent.SetTarget(target);
                }

                if (!m_AgentRuntimeStates.TryGetValue(agent, out AgentRuntimeState runtime))
                {
                    runtime = new AgentRuntimeState
                    {
                        NextPathSampleTime = now,
                        HasNextNode = false,
                        FlowTargetVersion = flowTargetVersion
                    };
                }

                runtime.LastSeenFrame = currentFrame;
                if (ForceSharedTarget && runtime.FlowTargetVersion != flowTargetVersion)
                {
                    runtime.FlowTargetVersion = flowTargetVersion;
                    runtime.NextPathSampleTime = now;
                }

                if (IsWithinNoPathfindRadius(agent, target))
                {
                    runtime.HasNextNode = false;
                    runtime.NextPathSampleTime = now + GetRandomRepathInterval();
                    runtime.CurrentPlanarVelocity = Vector2.zero;
                    runtime.DesiredPlanarVelocity = Vector2.zero;
                    runtime.InFormationHandover = false;
                    m_AgentRuntimeStates[agent] = runtime;
                    continue;
                }

                bool canUseFormationHandover = ForceSharedTarget && EnableTargetFormation && target != liveSharedTarget;
                if (!canUseFormationHandover)
                {
                    runtime.InFormationHandover = false;
                }
                else
                {
                    float enterRadius = Mathf.Max(1f, FormationHandoverRadius);
                    float exitRadius = enterRadius + Mathf.Max(0f, FormationHandoverExitBuffer);
                    float distSqToLiveTarget = GetDistanceSqToTarget(agent.transform.position, liveSharedTarget);

                    if (runtime.InFormationHandover)
                    {
                        if (distSqToLiveTarget > exitRadius * exitRadius)
                        {
                            runtime.InFormationHandover = false;
                        }
                    }
                    else if (distSqToLiveTarget <= enterRadius * enterRadius)
                    {
                        runtime.InFormationHandover = true;
                    }
                }

                if (runtime.InFormationHandover)
                {
                    runtime.HasNextNode = true;
                    runtime.NextNode = target;
                    runtime.NextPathSampleTime = now + GetRandomRepathInterval();
                    m_AgentRuntimeStates[agent] = runtime;
                    continue;
                }

                bool shouldSamplePath = now >= runtime.NextPathSampleTime;
                if (runtime.HasNextNode && agent.IsNearNode(runtime.NextNode))
                {
                    shouldSamplePath = true;
                }

                if (shouldSamplePath)
                {
                    if (!m_TargetGroups.TryGetValue(pathTarget, out List<CrowdAgentController> list))
                    {
                        list = m_GroupListPool.Count > 0 ? m_GroupListPool.Pop() : new List<CrowdAgentController>(64);
                        m_TargetGroups.Add(pathTarget, list);
                        m_GroupTargets.Add(pathTarget);
                    }

                    list.Add(agent);
                    sampledAgentCount++;
                }

                m_AgentRuntimeStates[agent] = runtime;
            }
            Profiler.EndSample();

            if (activeCount == 0)
            {
                m_LastMovedCount = 0;
                m_LastFailedCount = 0;
                CleanupRuntimeStates(currentFrame);
                return;
            }

            if (m_GroupTargets.Count > 0)
            {
                int groupedMaxNodes = ResolveNodeBudget(Mathf.Max(1, sampledAgentCount), maxNodesCap);
                float prepareInterval = Mathf.Max(0.05f, FlowFieldPrepareInterval);
                bool forcePrepareForSharedVersion = ForceSharedTarget && m_LastPreparedFlowTargetVersion != flowTargetVersion;

                Profiler.BeginSample("CrowdFlowFieldCoordinator.PrepareFields");
                for (int i = 0; i < m_GroupTargets.Count; i++)
                {
                    Vector3Int requestedTarget = m_GroupTargets[i];
                    m_LastRequestedTargetUseTime[requestedTarget] = now;

                    bool shouldPrepare = forcePrepareForSharedVersion;
                    if (!m_PreparedTargetByRequestedTarget.TryGetValue(requestedTarget, out Vector3Int preparedTarget))
                    {
                        shouldPrepare = true;
                    }
                    else if (!FlowFieldPathfinding.HasPreparedField(preparedTarget, m_World))
                    {
                        shouldPrepare = true;
                    }
                    else if (!m_NextPrepareCheckTimeByRequestedTarget.TryGetValue(requestedTarget, out float nextCheckTime) || now >= nextCheckTime)
                    {
                        shouldPrepare = true;
                    }

                    if (!shouldPrepare)
                    {
                        continue;
                    }

                    if (FlowFieldPathfinding.TryPrepareField(
                            requestedTarget,
                            m_World,
                            groupedMaxNodes,
                            cacheLifetime,
                            searchRadius,
                            out Vector3Int resolvedPreparedTarget))
                    {
                        m_PreparedTargetByRequestedTarget[requestedTarget] = resolvedPreparedTarget;
                        m_NextPrepareCheckTimeByRequestedTarget[requestedTarget] = now + prepareInterval;
                    }
                    else
                    {
                        m_PreparedTargetByRequestedTarget.Remove(requestedTarget);
                        m_NextPrepareCheckTimeByRequestedTarget[requestedTarget] = now + Mathf.Min(0.25f, prepareInterval);
                    }
                }
                Profiler.EndSample();

                if (forcePrepareForSharedVersion)
                {
                    m_LastPreparedFlowTargetVersion = flowTargetVersion;
                }

                CleanupPreparedTargetCaches(now);

                Profiler.BeginSample("CrowdFlowFieldCoordinator.ResolveSampledAgents");
                foreach (KeyValuePair<Vector3Int, List<CrowdAgentController>> group in m_TargetGroups)
                {
                    Vector3Int requestedTarget = group.Key;
                    List<CrowdAgentController> list = group.Value;
                    bool hasPreparedTarget = m_PreparedTargetByRequestedTarget.TryGetValue(requestedTarget, out Vector3Int preparedTarget);

                    for (int i = 0; i < list.Count; i++)
                    {
                        CrowdAgentController agent = list[i];
                        if (agent == null || !agent.isActiveAndEnabled)
                        {
                            continue;
                        }

                        if (!m_AgentRuntimeStates.TryGetValue(agent, out AgentRuntimeState runtime))
                        {
                            runtime = new AgentRuntimeState();
                        }

                        Vector3Int current = agent.GetCurrentGridPosition();
                        runtime.LastSeenFrame = currentFrame;
                        runtime.NextPathSampleTime = now + GetRandomRepathInterval();

                        if (hasPreparedTarget && FlowFieldPathfinding.TryGetNextNodeFromPreparedTarget(
                            current,
                            preparedTarget,
                            m_World,
                            searchRadius,
                            out Vector3Int next))
                        {
                            runtime.HasNextNode = true;
                            runtime.NextNode = next;
                            m_AgentRuntimeStates[agent] = runtime;
                        }
                        else
                        {
                            runtime.HasNextNode = false;
                            runtime.CurrentPlanarVelocity = Vector2.zero;
                            runtime.DesiredPlanarVelocity = Vector2.zero;
                            m_AgentRuntimeStates[agent] = runtime;
                            failed++;
                        }
                    }
                }
                Profiler.EndSample();
            }

            m_LastFailedCount = failed;

            Profiler.BeginSample("CrowdFlowFieldCoordinator.CleanupRuntimeState");
            CleanupRuntimeStates(currentFrame);
            Profiler.EndSample();

            ReleaseTargetGroupsToPool();
        }

        private float GetRandomRepathInterval()
        {
            float min = Mathf.Max(0.1f, RepathMinInterval);
            float max = Mathf.Max(min, RepathMaxInterval);
            return Random.Range(min, max);
        }

        private bool IsWithinNoPathfindRadius(CrowdAgentController agent, Vector3Int target)
        {
            float radius = Mathf.Max(0f, NoPathfindRadius);
            if (radius <= 0f)
            {
                return false;
            }

            return IsWithinTargetRadius(agent.transform.position, target, radius);
        }

        private static bool IsWithinTargetRadius(Vector3 worldPos, Vector3Int target, float radius)
        {
            float r = Mathf.Max(0f, radius);
            return GetDistanceSqToTarget(worldPos, target) <= r * r;
        }

        private static float GetDistanceSqToTarget(Vector3 worldPos, Vector3Int target)
        {
            float dx = (target.x + 0.5f) - worldPos.x;
            float dz = (target.z + 0.5f) - worldPos.z;
            return dx * dx + dz * dz;
        }

        private void CleanupPreparedTargetCaches(float now)
        {
            if (m_LastRequestedTargetUseTime.Count <= 128)
            {
                return;
            }

            m_PrepareTargetCleanupBuffer.Clear();
            foreach (KeyValuePair<Vector3Int, float> entry in m_LastRequestedTargetUseTime)
            {
                if (now - entry.Value > 6f)
                {
                    m_PrepareTargetCleanupBuffer.Add(entry.Key);
                }
            }

            for (int i = 0; i < m_PrepareTargetCleanupBuffer.Count; i++)
            {
                Vector3Int key = m_PrepareTargetCleanupBuffer[i];
                m_LastRequestedTargetUseTime.Remove(key);
                m_PreparedTargetByRequestedTarget.Remove(key);
                m_NextPrepareCheckTimeByRequestedTarget.Remove(key);
            }
        }

        private void CleanupRuntimeStates(int currentFrame)
        {
            if (m_AgentRuntimeStates.Count == 0)
            {
                return;
            }

            m_RuntimeStateCleanupBuffer.Clear();
            foreach (KeyValuePair<CrowdAgentController, AgentRuntimeState> entry in m_AgentRuntimeStates)
            {
                if (entry.Key == null || entry.Value.LastSeenFrame != currentFrame)
                {
                    m_RuntimeStateCleanupBuffer.Add(entry.Key);
                }
            }

            for (int i = 0; i < m_RuntimeStateCleanupBuffer.Count; i++)
            {
                m_AgentRuntimeStates.Remove(m_RuntimeStateCleanupBuffer[i]);
            }
        }

        private int ResolveNodeBudget(int activeAgents, int maxNodesCap)
        {
            if (!UseAdaptiveNodeBudget)
            {
                return maxNodesCap;
            }

            int nodesPerAgent = Mathf.Max(64, NodesPerActiveAgent);
            int adaptive = Mathf.Max(1000, activeAgents * nodesPerAgent);
            return Mathf.Clamp(adaptive, 1000, maxNodesCap);
        }

        private Transform GetOrCreateSpawnRoot()
        {
            if (AgentSearchRoot != null)
            {
                return AgentSearchRoot;
            }

            GameObject root = new GameObject("CrowdAgents");
            root.transform.SetParent(transform, false);
            AgentSearchRoot = root.transform;
            m_AgentsDirty = true;
            return AgentSearchRoot;
        }

        private void ReleaseTargetGroupsToPool()
        {
            if (m_TargetGroups.Count == 0)
            {
                m_GroupTargets.Clear();
                return;
            }

            foreach (List<CrowdAgentController> list in m_TargetGroups.Values)
            {
                list.Clear();
                m_GroupListPool.Push(list);
            }

            m_TargetGroups.Clear();
            m_GroupTargets.Clear();
        }
    }
}
