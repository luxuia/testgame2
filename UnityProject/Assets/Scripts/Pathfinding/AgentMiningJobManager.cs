using System.Collections.Generic;
using Minecraft.Configurations;
using Minecraft.PlayerControls;
using Minecraft.PhysicSystem;
using UnityEngine;
using UnityEngine.Profiling;

namespace Minecraft.Pathfinding
{
    [DisallowMultipleComponent]
    public class AgentMiningJobManager : MonoBehaviour
    {
        private static readonly Vector3Int[] s_SlotOffsets = new Vector3Int[]
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 0, -1),
        };

        [Header("References")]
        public CrowdFlowFieldCoordinator Coordinator;
        public TargetSelector SelectionSource;
        public BlockInteraction BuildBlockSource;

        [Header("Runtime")]
        [Tooltip("自动查找协调器")]
        public bool AutoFindCoordinator = true;

        [Tooltip("自动查找选区来源（TargetSelector）")]
        public bool AutoFindSelectionSource = true;

        [Tooltip("自动查找建造方块来源（BlockInteraction）")]
        public bool AutoFindBuildBlockSource = true;

        [Tooltip("有挖掘任务时关闭共享目标模式")]
        public bool DisableSharedTargetWhileMining = true;

        [Tooltip("有挖掘任务时临时缩小 NoPathfindRadius，避免agent提前停在槽位外")]
        public bool OverrideNoPathfindRadiusWhileMining = true;

        [Tooltip("挖掘期间使用的 NoPathfindRadius")]
        public float MiningNoPathfindRadius = 0.1f;

        [Tooltip("重建分配的最小间隔（秒）")]
        public float AssignmentRefreshInterval = 0.2f;

        [Tooltip("任务与槽位有效性检查间隔（秒）")]
        public float JobValidationInterval = 0.25f;

        [Header("Mining")]
        [Tooltip("单个agent每秒挖掘伤害")]
        public float AgentDigDamagePerSecond = 5f;

        [Tooltip("agent需要在该距离内才可对方块生效（米）")]
        public float AgentDigReachDistance = 1.6f;

        [Tooltip("agent距离分配槽位不超过该值才可开采（米）")]
        public float AgentSlotReachDistance = 0.45f;

        [Tooltip("槽位搜索半径（仅用于紧邻位不可用时的近邻修正）")]
        public int SlotSearchRadius = 1;

        [Header("Build")]
        [Tooltip("单个agent每秒建造进度")]
        public float AgentBuildProgressPerSecond = 1f;

        [Tooltip("单个建造任务所需总进度")]
        public float BuildWorkRequired = 1f;

        [Tooltip("建造方块内部名（为空时仅使用备用ID）")]
        public string BuildBlockInternalName = "dirt";

        [Tooltip("建造方块备用ID（当内部名无效时使用）")]
        public int BuildBlockFallbackId = 1;

        [Tooltip("优先使用玩家当前手持方块作为建造方块")]
        public bool PreferCurrentHandBlock = true;

        [Header("Debug")]
        public bool ShowDebugInfo = false;

        [System.NonSerialized] public int LastSelectedBlockCount;
        [System.NonSerialized] public int LastActiveJobCount;
        [System.NonSerialized] public int LastAssignedAgentCount;
        [System.NonSerialized] public float LastUpdateMs;

        private sealed class BlockJobState
        {
            public float RemainingWork;
        }

        private struct AgentAssignment
        {
            public Vector3Int Block;
            public Vector3Int Slot;

            public AgentAssignment(Vector3Int block, Vector3Int slot)
            {
                Block = block;
                Slot = slot;
            }
        }

        [System.NonSerialized] private readonly Dictionary<Vector3Int, BlockJobState> m_BlockJobs = new Dictionary<Vector3Int, BlockJobState>();
        [System.NonSerialized] private readonly Dictionary<Vector3Int, List<Vector3Int>> m_BlockSlots = new Dictionary<Vector3Int, List<Vector3Int>>();
        [System.NonSerialized] private readonly Dictionary<CrowdAgentController, AgentAssignment> m_Assignments = new Dictionary<CrowdAgentController, AgentAssignment>();
        [System.NonSerialized] private readonly Dictionary<Vector3Int, float> m_DamageBufferByBlock = new Dictionary<Vector3Int, float>();
        [System.NonSerialized] private readonly List<Vector3Int> m_BlockRemovalBuffer = new List<Vector3Int>(64);
        [System.NonSerialized] private readonly HashSet<Vector3Int> m_SelectedBlockSetBuffer = new HashSet<Vector3Int>();
        [System.NonSerialized] private readonly List<Vector3Int> m_BlockOrderBuffer = new List<Vector3Int>(64);
        [System.NonSerialized] private readonly List<CrowdAgentController> m_UnassignedAgentsBuffer = new List<CrowdAgentController>(128);
        [System.NonSerialized] private readonly List<CrowdAgentController> m_AssignmentCleanupAgentsBuffer = new List<CrowdAgentController>(64);
        [System.NonSerialized] private readonly HashSet<Vector3Int> m_OccupiedSlots = new HashSet<Vector3Int>();
        [System.NonSerialized] private readonly Dictionary<Vector3Int, int> m_AssignedCountByBlock = new Dictionary<Vector3Int, int>();
        [System.NonSerialized] private readonly HashSet<Vector3Int> m_ProcessingBlocksBuffer = new HashSet<Vector3Int>();

        [System.NonSerialized] private bool m_AssignmentsDirty = true;
        [System.NonSerialized] private TargetSelectionAction m_JobAction = TargetSelectionAction.None;
        [System.NonSerialized] private bool m_HasCachedSharedTargetState;
        [System.NonSerialized] private bool m_CachedForceSharedTarget;
        [System.NonSerialized] private bool m_HasCachedNoPathfindRadius;
        [System.NonSerialized] private float m_CachedNoPathfindRadius;
        [System.NonSerialized] private int m_LastSelectionVersion = -1;
        [System.NonSerialized] private float m_NextAssignmentTime;
        [System.NonSerialized] private float m_NextValidationTime;
        [System.NonSerialized] private string m_CachedBuildBlockInternalName;
        [System.NonSerialized] private int m_CachedBuildBlockFallbackId = int.MinValue;
        [System.NonSerialized] private BlockData m_CachedBuildBlockData;

        private void OnEnable()
        {
            m_LastSelectionVersion = -1;
            m_AssignmentsDirty = true;
            m_NextAssignmentTime = 0f;
            m_NextValidationTime = 0f;
            m_JobAction = TargetSelectionAction.None;
            m_CachedBuildBlockData = null;
            m_CachedBuildBlockInternalName = null;
            m_CachedBuildBlockFallbackId = int.MinValue;
        }

        private void Update()
        {
            float startTime = Time.realtimeSinceStartup;
            Profiler.BeginSample("AgentMiningJobManager.Update");
            try
            {
                if (!TryResolveReferences())
                {
                    ClearProcessingHighlights();
                    DeactivateMiningMode();
                    return;
                }

                IWorld world = World.Active;
                if (world == null)
                {
                    ClearProcessingHighlights();
                    DeactivateMiningMode();
                    return;
                }

                SyncJobsFromSelection(world);
                ValidateJobsPeriodically(world);

                if (m_BlockJobs.Count == 0)
                {
                    ClearAssignments(clearAgentTargets: false);
                    ClearProcessingHighlights();
                    DeactivateMiningMode();
                    return;
                }

                ActivateMiningMode();

                float now = Time.unscaledTime;
                if (m_AssignmentsDirty || now >= m_NextAssignmentTime)
                {
                    RebuildAssignments();
                    m_NextAssignmentTime = now + Mathf.Max(0.03f, AssignmentRefreshInterval);
                }

                ApplyAssignedTargets();
                UpdateProcessingHighlights();
                ProcessJobs(world, Time.deltaTime);
            }
            finally
            {
                LastSelectedBlockCount = SelectionSource != null ? SelectionSource.SelectionCount : 0;
                LastActiveJobCount = m_BlockJobs.Count;
                LastAssignedAgentCount = m_Assignments.Count;
                LastUpdateMs = (Time.realtimeSinceStartup - startTime) * 1000f;
                Profiler.EndSample();
            }
        }

        private bool TryResolveReferences()
        {
            if ((Coordinator == null || !Coordinator.isActiveAndEnabled) && AutoFindCoordinator)
            {
                Coordinator = GetComponent<CrowdFlowFieldCoordinator>();
                if (Coordinator == null)
                {
                    Coordinator = FindObjectOfType<CrowdFlowFieldCoordinator>();
                }
            }

            if ((SelectionSource == null || !SelectionSource.isActiveAndEnabled) && AutoFindSelectionSource)
            {
                SelectionSource = FindObjectOfType<TargetSelector>();
            }

            if ((BuildBlockSource == null || !BuildBlockSource.isActiveAndEnabled) && AutoFindBuildBlockSource)
            {
                BuildBlockSource = FindObjectOfType<BlockInteraction>();
            }

            return Coordinator != null && SelectionSource != null;
        }

        private void SyncJobsFromSelection(IWorld world)
        {
            TargetSelectionAction selectionAction = SelectionSource.SelectedAction;
            if (selectionAction != m_JobAction)
            {
                m_JobAction = selectionAction;
                m_LastSelectionVersion = -1;
                m_BlockJobs.Clear();
                m_BlockSlots.Clear();
                m_AssignmentsDirty = true;
            }

            int version = SelectionSource.SelectionVersion;
            if (version == m_LastSelectionVersion)
            {
                return;
            }

            m_LastSelectionVersion = version;
            m_SelectedBlockSetBuffer.Clear();

            IReadOnlyList<Vector3Int> selectedBlocks = SelectionSource.SelectedBlocks;
            for (int i = 0; i < selectedBlocks.Count; i++)
            {
                m_SelectedBlockSetBuffer.Add(selectedBlocks[i]);
            }

            m_BlockRemovalBuffer.Clear();
            foreach (KeyValuePair<Vector3Int, BlockJobState> entry in m_BlockJobs)
            {
                if (!m_SelectedBlockSetBuffer.Contains(entry.Key) || !TryGetValidBlockForAction(world, entry.Key, selectionAction, out _))
                {
                    m_BlockRemovalBuffer.Add(entry.Key);
                }
            }

            for (int i = 0; i < m_BlockRemovalBuffer.Count; i++)
            {
                Vector3Int block = m_BlockRemovalBuffer[i];
                m_BlockJobs.Remove(block);
                m_BlockSlots.Remove(block);
            }

            for (int i = 0; i < selectedBlocks.Count; i++)
            {
                Vector3Int block = selectedBlocks[i];
                if (!TryGetValidBlockForAction(world, block, selectionAction, out BlockData data))
                {
                    continue;
                }

                if (!m_BlockJobs.ContainsKey(block))
                {
                    m_BlockJobs[block] = new BlockJobState
                    {
                        RemainingWork = ResolveInitialWork(selectionAction, data)
                    };
                }
            }

            RebuildAllSlots(world);
            m_AssignmentsDirty = true;
        }

        private void ValidateJobsPeriodically(IWorld world)
        {
            float now = Time.unscaledTime;
            if (now < m_NextValidationTime)
            {
                return;
            }

            m_NextValidationTime = now + Mathf.Max(0.05f, JobValidationInterval);

            m_BlockRemovalBuffer.Clear();
            foreach (KeyValuePair<Vector3Int, BlockJobState> entry in m_BlockJobs)
            {
                if (!TryGetValidBlockForAction(world, entry.Key, m_JobAction, out _))
                {
                    m_BlockRemovalBuffer.Add(entry.Key);
                }
            }

            if (m_BlockRemovalBuffer.Count == 0)
            {
                RebuildAllSlots(world);
                return;
            }

            for (int i = 0; i < m_BlockRemovalBuffer.Count; i++)
            {
                Vector3Int block = m_BlockRemovalBuffer[i];
                m_BlockJobs.Remove(block);
                m_BlockSlots.Remove(block);
                SelectionSource.RemoveSelectedBlock(block);
            }

            RebuildAllSlots(world);
            m_AssignmentsDirty = true;
        }

        private void RebuildAllSlots(IWorld world)
        {
            m_BlockSlots.Clear();
            foreach (KeyValuePair<Vector3Int, BlockJobState> entry in m_BlockJobs)
            {
                Vector3Int block = entry.Key;
                List<Vector3Int> slots = BuildSlotsForBlock(block, world);
                m_BlockSlots[block] = slots;
            }
        }

        private List<Vector3Int> BuildSlotsForBlock(Vector3Int block, IWorld world)
        {
            var slots = new List<Vector3Int>(4);
            for (int i = 0; i < s_SlotOffsets.Length; i++)
            {
                // Walkable node y is "feet air cell", so start from side cell + up.
                Vector3Int candidate = block + s_SlotOffsets[i] + Vector3Int.up;
                if (AStarPathfinding.IsWalkableNode(candidate, world))
                {
                    slots.Add(candidate);
                    continue;
                }

                int radius = Mathf.Max(0, SlotSearchRadius);
                if (radius <= 0)
                {
                    continue;
                }

                Vector3Int? resolved = AStarPathfinding.FindNearestWalkableNode(candidate, world, radius);
                if (resolved.HasValue && IsNearBlock(resolved.Value, block) && !slots.Contains(resolved.Value))
                {
                    slots.Add(resolved.Value);
                }
            }

            return slots;
        }

        private static bool IsNearBlock(Vector3Int slot, Vector3Int block)
        {
            int dx = Mathf.Abs(slot.x - block.x);
            int dz = Mathf.Abs(slot.z - block.z);
            int manhattanXZ = dx + dz;
            int dy = Mathf.Abs(slot.y - block.y);
            return manhattanXZ <= 2 && dy <= 1;
        }

        private void RebuildAssignments()
        {
            CleanupInvalidAssignments();
            CollectUnassignedAgents();
            if (m_UnassignedAgentsBuffer.Count == 0)
            {
                m_AssignmentsDirty = false;
                return;
            }

            BuildBlockOrder();
            FirstPassAssignOneAgentPerBlock();
            SecondPassFillRemainingSlots();

            m_AssignmentsDirty = false;
        }

        private void CleanupInvalidAssignments()
        {
            m_AssignmentCleanupAgentsBuffer.Clear();
            m_OccupiedSlots.Clear();
            m_AssignedCountByBlock.Clear();

            foreach (KeyValuePair<CrowdAgentController, AgentAssignment> entry in m_Assignments)
            {
                CrowdAgentController agent = entry.Key;
                AgentAssignment assignment = entry.Value;

                if (!CanAgentReceiveAssignments(agent))
                {
                    m_AssignmentCleanupAgentsBuffer.Add(agent);
                    continue;
                }

                if (!m_BlockJobs.ContainsKey(assignment.Block))
                {
                    m_AssignmentCleanupAgentsBuffer.Add(agent);
                    continue;
                }

                if (!IsSlotStillValid(assignment.Block, assignment.Slot))
                {
                    m_AssignmentCleanupAgentsBuffer.Add(agent);
                    continue;
                }

                if (!m_OccupiedSlots.Add(assignment.Slot))
                {
                    m_AssignmentCleanupAgentsBuffer.Add(agent);
                    continue;
                }

                IncrementAssignedCount(assignment.Block);
            }

            for (int i = 0; i < m_AssignmentCleanupAgentsBuffer.Count; i++)
            {
                CrowdAgentController agent = m_AssignmentCleanupAgentsBuffer[i];
                if (agent != null)
                {
                    agent.ClearTarget();
                }

                m_Assignments.Remove(agent);
            }
        }

        private void CollectUnassignedAgents()
        {
            m_UnassignedAgentsBuffer.Clear();
            if (Coordinator == null || Coordinator.Agents == null)
            {
                return;
            }

            for (int i = 0; i < Coordinator.Agents.Count; i++)
            {
                CrowdAgentController agent = Coordinator.Agents[i];
                if (!CanAgentReceiveAssignments(agent))
                {
                    continue;
                }

                if (m_Assignments.ContainsKey(agent))
                {
                    continue;
                }

                m_UnassignedAgentsBuffer.Add(agent);
            }
        }

        private void BuildBlockOrder()
        {
            m_BlockOrderBuffer.Clear();
            foreach (KeyValuePair<Vector3Int, BlockJobState> entry in m_BlockJobs)
            {
                if (!m_BlockSlots.TryGetValue(entry.Key, out List<Vector3Int> slots) || slots.Count == 0)
                {
                    continue;
                }

                m_BlockOrderBuffer.Add(entry.Key);
            }

            m_BlockOrderBuffer.Sort(CompareBlocks);
        }

        private static int CompareBlocks(Vector3Int a, Vector3Int b)
        {
            int y = a.y.CompareTo(b.y);
            if (y != 0) return y;
            int x = a.x.CompareTo(b.x);
            if (x != 0) return x;
            return a.z.CompareTo(b.z);
        }

        private void FirstPassAssignOneAgentPerBlock()
        {
            for (int i = 0; i < m_BlockOrderBuffer.Count; i++)
            {
                if (m_UnassignedAgentsBuffer.Count == 0)
                {
                    return;
                }

                Vector3Int block = m_BlockOrderBuffer[i];
                if (GetAssignedCount(block) > 0)
                {
                    continue;
                }

                if (!TryGetFirstFreeSlot(block, out Vector3Int freeSlot))
                {
                    continue;
                }

                if (!TrySelectClosestAgentToSlot(freeSlot, out int agentIndex))
                {
                    continue;
                }

                AssignAgentToBlock(m_UnassignedAgentsBuffer[agentIndex], block, freeSlot, agentIndex);
            }
        }

        private void SecondPassFillRemainingSlots()
        {
            for (int i = m_UnassignedAgentsBuffer.Count - 1; i >= 0; i--)
            {
                CrowdAgentController agent = m_UnassignedAgentsBuffer[i];
                if (!CanAgentReceiveAssignments(agent))
                {
                    m_UnassignedAgentsBuffer.RemoveAt(i);
                    continue;
                }

                if (!TryFindClosestFreeSlotForAgent(agent, out Vector3Int block, out Vector3Int slot))
                {
                    continue;
                }

                AssignAgentToBlock(agent, block, slot, i);
            }
        }

        private bool TrySelectClosestAgentToSlot(Vector3Int slot, out int agentIndex)
        {
            float bestDistSq = float.MaxValue;
            int bestIndex = -1;
            Vector3 slotWorld = new Vector3(slot.x + 0.5f, slot.y + 0.5f, slot.z + 0.5f);

            for (int i = 0; i < m_UnassignedAgentsBuffer.Count; i++)
            {
                CrowdAgentController agent = m_UnassignedAgentsBuffer[i];
                if (!CanAgentReceiveAssignments(agent))
                {
                    continue;
                }

                Vector3 pos = agent.transform.position;
                float dx = slotWorld.x - pos.x;
                float dz = slotWorld.z - pos.z;
                float distSq = dx * dx + dz * dz;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestIndex = i;
                }
            }

            agentIndex = bestIndex;
            return bestIndex >= 0;
        }

        private bool TryFindClosestFreeSlotForAgent(CrowdAgentController agent, out Vector3Int block, out Vector3Int slot)
        {
            block = default;
            slot = default;

            Vector3 pos = agent.transform.position;
            float bestDistSq = float.MaxValue;
            bool found = false;

            for (int i = 0; i < m_BlockOrderBuffer.Count; i++)
            {
                Vector3Int candidateBlock = m_BlockOrderBuffer[i];
                if (!m_BlockSlots.TryGetValue(candidateBlock, out List<Vector3Int> slots) || slots.Count == 0)
                {
                    continue;
                }

                if (GetAssignedCount(candidateBlock) >= slots.Count)
                {
                    continue;
                }

                for (int s = 0; s < slots.Count; s++)
                {
                    Vector3Int candidateSlot = slots[s];
                    if (m_OccupiedSlots.Contains(candidateSlot))
                    {
                        continue;
                    }

                    float dx = (candidateSlot.x + 0.5f) - pos.x;
                    float dz = (candidateSlot.z + 0.5f) - pos.z;
                    float distSq = dx * dx + dz * dz;
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        block = candidateBlock;
                        slot = candidateSlot;
                        found = true;
                    }
                }
            }

            return found;
        }

        private void AssignAgentToBlock(CrowdAgentController agent, Vector3Int block, Vector3Int slot, int removeIndexFromUnassigned)
        {
            if (agent == null)
            {
                return;
            }

            m_Assignments[agent] = new AgentAssignment(block, slot);
            m_OccupiedSlots.Add(slot);
            IncrementAssignedCount(block);
            m_UnassignedAgentsBuffer.RemoveAt(removeIndexFromUnassigned);
        }

        private bool TryGetFirstFreeSlot(Vector3Int block, out Vector3Int slot)
        {
            slot = block;
            if (!m_BlockSlots.TryGetValue(block, out List<Vector3Int> slots))
            {
                return false;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (m_OccupiedSlots.Contains(slots[i]))
                {
                    continue;
                }

                slot = slots[i];
                return true;
            }

            return false;
        }

        private bool IsSlotStillValid(Vector3Int block, Vector3Int slot)
        {
            if (!m_BlockSlots.TryGetValue(block, out List<Vector3Int> slots))
            {
                return false;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == slot)
                {
                    return true;
                }
            }

            return false;
        }

        private int GetAssignedCount(Vector3Int block)
        {
            return m_AssignedCountByBlock.TryGetValue(block, out int count) ? count : 0;
        }

        private void IncrementAssignedCount(Vector3Int block)
        {
            if (m_AssignedCountByBlock.TryGetValue(block, out int count))
            {
                m_AssignedCountByBlock[block] = count + 1;
            }
            else
            {
                m_AssignedCountByBlock[block] = 1;
            }
        }

        private void ApplyAssignedTargets()
        {
            foreach (KeyValuePair<CrowdAgentController, AgentAssignment> entry in m_Assignments)
            {
                CrowdAgentController agent = entry.Key;
                if (!CanAgentReceiveAssignments(agent))
                {
                    continue;
                }

                Vector3Int slot = entry.Value.Slot;
                if (!agent.HasTarget || agent.TargetBlock != slot)
                {
                    agent.SetTarget(slot);
                }
            }
        }

        private void UpdateProcessingHighlights()
        {
            if (SelectionSource == null)
            {
                return;
            }

            m_ProcessingBlocksBuffer.Clear();
            foreach (KeyValuePair<CrowdAgentController, AgentAssignment> entry in m_Assignments)
            {
                CrowdAgentController agent = entry.Key;
                AgentAssignment assignment = entry.Value;
                if (!CanAgentReceiveAssignments(agent))
                {
                    continue;
                }

                if (!m_BlockJobs.ContainsKey(assignment.Block))
                {
                    continue;
                }

                m_ProcessingBlocksBuffer.Add(assignment.Block);
            }

            SelectionSource.SetProcessingBlocks(m_ProcessingBlocksBuffer);
        }

        private void ClearProcessingHighlights()
        {
            if (SelectionSource == null)
            {
                return;
            }

            m_ProcessingBlocksBuffer.Clear();
            SelectionSource.SetProcessingBlocks(m_ProcessingBlocksBuffer);
        }

        private void ProcessJobs(IWorld world, float deltaTime)
        {
            if (deltaTime <= 0f || m_Assignments.Count == 0 || m_BlockJobs.Count == 0)
            {
                return;
            }

            float perAgentWork = m_JobAction == TargetSelectionAction.Build
                ? Mathf.Max(0f, AgentBuildProgressPerSecond)
                : Mathf.Max(0f, AgentDigDamagePerSecond);
            if (perAgentWork <= 0f)
            {
                return;
            }

            BlockData buildBlock = null;
            if (m_JobAction == TargetSelectionAction.Build)
            {
                buildBlock = ResolveBuildBlock(world);
                if (buildBlock == null || buildBlock.ID == 0)
                {
                    return;
                }
            }

            m_DamageBufferByBlock.Clear();
            foreach (KeyValuePair<CrowdAgentController, AgentAssignment> entry in m_Assignments)
            {
                CrowdAgentController agent = entry.Key;
                AgentAssignment assignment = entry.Value;
                if (!CanAgentReceiveAssignments(agent) || !m_BlockJobs.ContainsKey(assignment.Block))
                {
                    continue;
                }

                if (!IsAgentReadyForJob(agent, assignment))
                {
                    continue;
                }

                if (m_DamageBufferByBlock.TryGetValue(assignment.Block, out float existing))
                {
                    m_DamageBufferByBlock[assignment.Block] = existing + perAgentWork;
                }
                else
                {
                    m_DamageBufferByBlock[assignment.Block] = perAgentWork;
                }
            }

            if (m_DamageBufferByBlock.Count == 0)
            {
                return;
            }

            m_BlockRemovalBuffer.Clear();
            foreach (KeyValuePair<Vector3Int, float> damage in m_DamageBufferByBlock)
            {
                Vector3Int block = damage.Key;
                if (!m_BlockJobs.TryGetValue(block, out BlockJobState state))
                {
                    continue;
                }

                if (!TryGetValidBlockForAction(world, block, m_JobAction, out _))
                {
                    m_BlockRemovalBuffer.Add(block);
                    continue;
                }

                state.RemainingWork -= damage.Value * deltaTime;
                if (state.RemainingWork <= 0f)
                {
                    if (m_JobAction == TargetSelectionAction.Build)
                    {
                        world.RWAccessor.SetBlock(block.x, block.y, block.z, buildBlock, Quaternion.identity, ModificationSource.InternalOrSystem);
                    }
                    else
                    {
                        world.RWAccessor.SetBlock(block.x, block.y, block.z, world.BlockDataTable.GetBlock(0), Quaternion.identity, ModificationSource.InternalOrSystem);
                    }

                    m_BlockRemovalBuffer.Add(block);
                }
            }

            if (m_BlockRemovalBuffer.Count == 0)
            {
                return;
            }

            for (int i = 0; i < m_BlockRemovalBuffer.Count; i++)
            {
                Vector3Int block = m_BlockRemovalBuffer[i];
                m_BlockJobs.Remove(block);
                m_BlockSlots.Remove(block);
                SelectionSource.RemoveSelectedBlock(block);
            }

            m_AssignmentsDirty = true;
            RebuildAllSlots(world);
        }

        private bool IsAgentReadyForJob(CrowdAgentController agent, AgentAssignment assignment)
        {
            float slotReach = Mathf.Max(0.05f, AgentSlotReachDistance);
            if (agent.GetPlanarDistanceToNode(assignment.Slot) > slotReach)
            {
                return false;
            }

            float reach = Mathf.Max(0.2f, AgentDigReachDistance);
            Vector3 pos = agent.transform.position;
            float dx = (assignment.Block.x + 0.5f) - pos.x;
            float dz = (assignment.Block.z + 0.5f) - pos.z;
            if ((dx * dx + dz * dz) > reach * reach)
            {
                return false;
            }

            float feetY = pos.y - agent.PivotHeightFromFeet;
            if (Mathf.Abs(feetY - assignment.Block.y) > 1.5f)
            {
                return false;
            }

            return true;
        }

        private float ResolveInitialWork(TargetSelectionAction action, BlockData block)
        {
            if (action == TargetSelectionAction.Build)
            {
                return Mathf.Max(0.01f, BuildWorkRequired);
            }

            return ResolveBlockHardness(block);
        }

        private static float ResolveBlockHardness(BlockData block)
        {
            if (block == null)
            {
                return 0.01f;
            }

            return Mathf.Max(0.01f, block.Hardness);
        }

        private static bool TryGetValidBlockForAction(IWorld world, Vector3Int blockPos, TargetSelectionAction action, out BlockData block)
        {
            switch (action)
            {
                case TargetSelectionAction.Mine:
                    return TryGetDiggableBlock(world, blockPos, out block);
                case TargetSelectionAction.Build:
                    return TryGetBuildableBlock(world, blockPos, out block);
                default:
                    block = null;
                    return false;
            }
        }

        private static bool TryGetDiggableBlock(IWorld world, Vector3Int blockPos, out BlockData block)
        {
            block = null;
            if (world == null)
            {
                return false;
            }

            block = world.RWAccessor.GetBlock(blockPos.x, blockPos.y, blockPos.z);
            return block != null && block.ID != 0 && block.PhysicState == PhysicState.Solid;
        }

        private static bool TryGetBuildableBlock(IWorld world, Vector3Int blockPos, out BlockData block)
        {
            block = null;
            if (world == null)
            {
                return false;
            }

            block = world.RWAccessor.GetBlock(blockPos.x, blockPos.y, blockPos.z);
            return block != null && block.ID == 0;
        }

        private BlockData ResolveBuildBlock(IWorld world)
        {
            if (world?.BlockDataTable == null)
            {
                return null;
            }

            if (PreferCurrentHandBlock && BuildBlockSource != null && BuildBlockSource.isActiveAndEnabled)
            {
                if (BuildBlockSource.TryGetCurrentHandBlockData(world, out BlockData handBlock) &&
                    handBlock != null &&
                    handBlock.ID != 0)
                {
                    return handBlock;
                }
            }

            string internalName = string.IsNullOrWhiteSpace(BuildBlockInternalName)
                ? string.Empty
                : BuildBlockInternalName.Trim();
            if (!string.IsNullOrEmpty(internalName))
            {
                if (m_CachedBuildBlockData == null || m_CachedBuildBlockInternalName != internalName)
                {
                    m_CachedBuildBlockInternalName = internalName;
                    m_CachedBuildBlockFallbackId = int.MinValue;
                    try
                    {
                        m_CachedBuildBlockData = world.BlockDataTable.GetBlock(internalName);
                    }
                    catch
                    {
                        m_CachedBuildBlockData = null;
                    }
                }

                if (m_CachedBuildBlockData != null && m_CachedBuildBlockData.ID != 0)
                {
                    return m_CachedBuildBlockData;
                }
            }

            int maxId = Mathf.Max(1, world.BlockDataTable.BlockCount - 1);
            int fallbackId = Mathf.Clamp(BuildBlockFallbackId, 1, maxId);
            if (m_CachedBuildBlockData == null || m_CachedBuildBlockFallbackId != fallbackId)
            {
                m_CachedBuildBlockFallbackId = fallbackId;
                m_CachedBuildBlockInternalName = string.Empty;
                m_CachedBuildBlockData = world.BlockDataTable.GetBlock(fallbackId);
            }

            return m_CachedBuildBlockData;
        }

        private void ClearAssignments(bool clearAgentTargets)
        {
            if (m_Assignments.Count == 0)
            {
                return;
            }

            if (clearAgentTargets)
            {
                foreach (KeyValuePair<CrowdAgentController, AgentAssignment> entry in m_Assignments)
                {
                    CrowdAgentController agent = entry.Key;
                    if (agent != null)
                    {
                        agent.ClearTarget();
                    }
                }
            }

            m_Assignments.Clear();
            m_OccupiedSlots.Clear();
            m_AssignedCountByBlock.Clear();
        }

        private void ActivateMiningMode()
        {
            if (Coordinator == null)
            {
                return;
            }

            if (DisableSharedTargetWhileMining && !m_HasCachedSharedTargetState)
            {
                m_CachedForceSharedTarget = Coordinator.ForceSharedTarget;
                m_HasCachedSharedTargetState = true;
                Coordinator.ForceSharedTarget = false;
            }

            if (OverrideNoPathfindRadiusWhileMining && !m_HasCachedNoPathfindRadius)
            {
                m_CachedNoPathfindRadius = Coordinator.NoPathfindRadius;
                m_HasCachedNoPathfindRadius = true;
                Coordinator.NoPathfindRadius = Mathf.Max(0f, MiningNoPathfindRadius);
            }
        }

        private void DeactivateMiningMode()
        {
            if (Coordinator == null)
            {
                return;
            }

            if (m_HasCachedSharedTargetState)
            {
                Coordinator.ForceSharedTarget = m_CachedForceSharedTarget;
                m_HasCachedSharedTargetState = false;
            }

            if (m_HasCachedNoPathfindRadius)
            {
                Coordinator.NoPathfindRadius = m_CachedNoPathfindRadius;
                m_HasCachedNoPathfindRadius = false;
            }
        }

        private static bool CanAgentReceiveAssignments(CrowdAgentController agent)
        {
            if (agent == null || !agent.isActiveAndEnabled)
            {
                return false;
            }

            return agent.CombatRuntime == null || agent.CombatRuntime.CanAct;
        }

        private void OnDisable()
        {
            ClearAssignments(clearAgentTargets: false);
            ClearProcessingHighlights();
            DeactivateMiningMode();
            m_BlockJobs.Clear();
            m_BlockSlots.Clear();
            m_JobAction = TargetSelectionAction.None;
            m_CachedBuildBlockData = null;
            m_CachedBuildBlockInternalName = null;
            m_CachedBuildBlockFallbackId = int.MinValue;
        }

        private void OnDrawGizmosSelected()
        {
            if (!ShowDebugInfo)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            foreach (KeyValuePair<Vector3Int, BlockJobState> entry in m_BlockJobs)
            {
                Vector3Int b = entry.Key;
                Gizmos.DrawWireCube(new Vector3(b.x + 0.5f, b.y + 0.5f, b.z + 0.5f), Vector3.one * 1.05f);
            }

            Gizmos.color = Color.green;
            foreach (KeyValuePair<CrowdAgentController, AgentAssignment> entry in m_Assignments)
            {
                CrowdAgentController agent = entry.Key;
                if (agent == null)
                {
                    continue;
                }

                Vector3Int slot = entry.Value.Slot;
                Vector3 slotPos = new Vector3(slot.x + 0.5f, slot.y + 0.5f, slot.z + 0.5f);
                Gizmos.DrawSphere(slotPos, 0.12f);
                Gizmos.DrawLine(agent.transform.position + Vector3.up * 0.25f, slotPos);
            }
        }
    }
}
