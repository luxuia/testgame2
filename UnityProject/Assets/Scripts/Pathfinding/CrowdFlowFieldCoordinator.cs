using System.Collections.Generic;
using UnityEngine;

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

        [Header("Runtime")]
        [Tooltip("协调器Tick间隔（秒），越大越省CPU")]
        public float TickInterval = 0.05f;

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
        [System.NonSerialized] private readonly Stack<List<CrowdAgentController>> m_GroupListPool = new Stack<List<CrowdAgentController>>();
        [System.NonSerialized] private readonly List<Vector3Int> m_SingleTargetBuffer = new List<Vector3Int>(1);
        [System.NonSerialized] private bool m_AgentsDirty = true;
        [System.NonSerialized] private float m_NextAgentRefreshTime;
        [System.NonSerialized] private float m_NextTickTime;
        [System.NonSerialized] private float m_AccumulatedDeltaTime;

        private void Start()
        {
            TryAcquireWorld();
            TrySpawnAgents();
            RefreshAgentsIfNeeded();
        }

        private void OnEnable()
        {
            m_AgentsDirty = true;
        }

        private void OnTransformChildrenChanged()
        {
            if (AgentSearchRoot == null || AgentSearchRoot == transform)
            {
                m_AgentsDirty = true;
            }
        }

        private void Update()
        {
            TryAcquireWorld();
            if (m_World == null)
            {
                return;
            }

            TrySpawnAgents();
            RefreshAgentsIfNeeded();

            m_AccumulatedDeltaTime += Time.deltaTime;
            float now = Time.unscaledTime;
            if (now < m_NextTickTime)
            {
                return;
            }

            m_NextTickTime = now + Mathf.Max(0.01f, TickInterval);
            float tickDelta = Mathf.Max(0.0001f, m_AccumulatedDeltaTime);
            m_AccumulatedDeltaTime = 0f;
            TickAgents(tickDelta);
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
        }

        private void TickAgents(float deltaTime)
        {
            if (Agents == null || Agents.Count == 0)
            {
                return;
            }

            Vector3Int sharedTarget = TargetTransform != null
                ? new Vector3Int(Mathf.FloorToInt(TargetTransform.position.x), Mathf.FloorToInt(TargetTransform.position.y), Mathf.FloorToInt(TargetTransform.position.z))
                : StaticTarget;

            int maxNodesCap = Mathf.Max(1000, FlowFieldMaxNodes);
            float cacheLifetime = Mathf.Max(0.05f, FlowFieldCacheLifetime);
            int searchRadius = Mathf.Max(2, FlowFieldSearchRadius);

            if (ForceSharedTarget)
            {
                int activeCount = 0;
                for (int i = 0; i < Agents.Count; i++)
                {
                    CrowdAgentController agent = Agents[i];
                    if (agent == null || !agent.isActiveAndEnabled)
                    {
                        continue;
                    }

                    activeCount++;
                    if (!agent.HasTarget || agent.TargetBlock != sharedTarget)
                    {
                        agent.SetTarget(sharedTarget);
                    }
                }

                if (activeCount == 0)
                {
                    return;
                }

                int maxNodes = ResolveNodeBudget(activeCount, maxNodesCap);
                m_SingleTargetBuffer.Clear();
                m_SingleTargetBuffer.Add(sharedTarget);

                FlowFieldPathfinding.PrepareFields(
                    m_World,
                    m_SingleTargetBuffer,
                    maxNodes,
                    cacheLifetime,
                    searchRadius);

                int movedShared = 0;
                int failedShared = 0;
                for (int i = 0; i < Agents.Count; i++)
                {
                    CrowdAgentController agent = Agents[i];
                    if (agent == null || !agent.isActiveAndEnabled)
                    {
                        continue;
                    }

                    Vector3Int current = agent.GetCurrentGridPosition();
                    if (FlowFieldPathfinding.TryGetNextNode(
                        current,
                        sharedTarget,
                        m_World,
                        maxNodes,
                        cacheLifetime,
                        searchRadius,
                        out Vector3Int next))
                    {
                        agent.MoveTowardsNode(next, deltaTime);
                        movedShared++;
                    }
                    else
                    {
                        failedShared++;
                    }
                }

                m_LastMovedCount = movedShared;
                m_LastFailedCount = failedShared;
                return;
            }

            ReleaseTargetGroupsToPool();

            for (int i = 0; i < Agents.Count; i++)
            {
                CrowdAgentController agent = Agents[i];
                if (agent == null || !agent.isActiveAndEnabled)
                {
                    continue;
                }

                Vector3Int target = agent.HasTarget ? agent.TargetBlock : sharedTarget;
                if (!agent.HasTarget)
                {
                    agent.SetTarget(target);
                }

                if (!m_TargetGroups.TryGetValue(target, out List<CrowdAgentController> list))
                {
                    list = m_GroupListPool.Count > 0 ? m_GroupListPool.Pop() : new List<CrowdAgentController>(64);
                    m_TargetGroups.Add(target, list);
                    m_GroupTargets.Add(target);
                }

                list.Add(agent);
            }

            if (m_GroupTargets.Count == 0)
            {
                return;
            }

            int groupedMaxNodes = ResolveNodeBudget(Agents.Count, maxNodesCap);

            FlowFieldPathfinding.PrepareFields(
                m_World,
                m_GroupTargets,
                groupedMaxNodes,
                cacheLifetime,
                searchRadius);

            int moved = 0;
            int failed = 0;

            foreach (KeyValuePair<Vector3Int, List<CrowdAgentController>> group in m_TargetGroups)
            {
                Vector3Int target = group.Key;
                List<CrowdAgentController> list = group.Value;

                for (int i = 0; i < list.Count; i++)
                {
                    CrowdAgentController agent = list[i];
                    Vector3Int current = agent.GetCurrentGridPosition();

                    if (FlowFieldPathfinding.TryGetNextNode(
                        current,
                        target,
                        m_World,
                        groupedMaxNodes,
                        cacheLifetime,
                        searchRadius,
                        out Vector3Int next))
                    {
                        agent.MoveTowardsNode(next, deltaTime);
                        moved++;
                    }
                    else
                    {
                        failed++;
                    }
                }
            }

            m_LastMovedCount = moved;
            m_LastFailedCount = failed;
            ReleaseTargetGroupsToPool();
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
