using System.Collections.Generic;
using Minecraft.Entities;
using Minecraft.Pathfinding;
using Minecraft.Rendering;
using UnityEngine;

namespace Minecraft.PlayerControls
{
    /// <summary>
    /// 寻路移动控制器，处理自动寻路和移动
    /// </summary>
    public class PathfindingMovementController : MonoBehaviour
    {
        [Header("移动设置")]
        [Tooltip("移动速度")]
        public float MoveSpeed = 5f;

        [Tooltip("到达目标的距离阈值")]
        public float ArrivalThreshold = 1f;

        [Tooltip("攻击距离")]
        public float AttackDistance = 1.5f;

        [Tooltip("中间路径点到达阈值（越小越不容易切角撞墙）")]
        public float WaypointThreshold = 0.25f;

        [Tooltip("卡住检测间隔")]
        public float StuckCheckInterval = 0.35f;

        [Tooltip("卡住检测最小位移")]
        public float MinProgressDistance = 0.04f;

        [Tooltip("自动重寻路冷却时间")]
        public float RepathCooldown = 0.8f;

        [Header("Pathfinding Strategy")]
        [Tooltip("优先使用共享场流（适合多角色同目标，单角色建议关闭）")]
        public bool PreferFlowField = false;

        [Tooltip("场流节点预算（越大覆盖越广）")]
        public int FlowFieldMaxNodes = 12000;

        [Tooltip("场流缓存寿命（秒）")]
        public float FlowFieldCacheLifetime = 0.35f;

        [Tooltip("场流起终点可行走搜索半径")]
        public int FlowFieldSearchRadius = 12;

        [Tooltip("场流回溯出的路径最大长度")]
        public int FlowFieldMaxPathLength = 256;

        [Header("调试设置")]
        [Tooltip("是否显示路径")]
        public bool ShowPath = true;

        [Tooltip("是否打印每次寻路耗时和节点遍历量")]
        public bool LogPathfindingStats = true;

        [Tooltip("路径颜色")]
        public Color PathColor = Color.cyan;

        [Tooltip("目标标记颜色")]
        public Color TargetColor = Color.yellow;

        [System.NonSerialized] private IAABBEntity m_PlayerEntity;
        [System.NonSerialized] private List<Vector3Int> m_CurrentPath;
        [System.NonSerialized] private int m_CurrentPathIndex;
        [System.NonSerialized] private Vector3Int? m_TargetBlock;
        [System.NonSerialized] private Vector3Int? m_AttackCheckBlock;
        [System.NonSerialized] private bool m_IsMoving;
        [System.NonSerialized] private bool m_HasTarget;
        [System.NonSerialized] private LineRenderer m_PathLineRenderer;
        [System.NonSerialized] private GameObject m_TargetMarker;
        [System.NonSerialized] private Vector3 m_TargetDirection;
        [System.NonSerialized] private bool m_HasDirection;
        [System.NonSerialized] private bool m_HasVerticalMovement;
        [System.NonSerialized] private Vector3 m_LastStuckCheckPos;
        [System.NonSerialized] private float m_LastStuckCheckTime;
        [System.NonSerialized] private float m_LastRepathTime;

        public bool IsMoving => m_IsMoving;
        public bool HasTarget => m_HasTarget;
        public Vector3Int? TargetBlock => m_TargetBlock;
        public Vector3 TargetDirection => m_TargetDirection;
        public bool HasDirection => m_HasDirection;
        public bool HasVerticalMovement => m_HasVerticalMovement;

        public void Initialize(IAABBEntity playerEntity)
        {
            m_PlayerEntity = playerEntity;
            m_CurrentPath = null;
            m_CurrentPathIndex = 0;
            m_TargetBlock = null;
            m_AttackCheckBlock = null;
            m_IsMoving = false;
            m_HasTarget = false;
            m_HasDirection = false;
            m_TargetDirection = Vector3.zero;
            m_HasVerticalMovement = false;
            m_LastStuckCheckPos = transform.position;
            m_LastStuckCheckTime = Time.time;
            m_LastRepathTime = -999f;

            InitializeDebugVisuals();
        }

        private void InitializeDebugVisuals()
        {
            if (m_PathLineRenderer == null)
            {
                GameObject pathObj = new GameObject("PathLine");
                pathObj.transform.SetParent(transform);
                m_PathLineRenderer = pathObj.AddComponent<LineRenderer>();
                m_PathLineRenderer.startWidth = 0.05f;
                m_PathLineRenderer.endWidth = 0.05f;
                m_PathLineRenderer.useWorldSpace = true;
                m_PathLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                m_PathLineRenderer.startColor = PathColor;
                m_PathLineRenderer.endColor = PathColor;
                m_PathLineRenderer.enabled = false;
            }

            if (m_TargetMarker == null)
            {
                m_TargetMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                m_TargetMarker.name = "TargetMarker";
                m_TargetMarker.transform.SetParent(transform);
                m_TargetMarker.transform.localScale = Vector3.one * 0.3f;

                var col = m_TargetMarker.GetComponent<Collider>();
                if (col != null)
                {
                    Destroy(col);
                }

                var renderer = m_TargetMarker.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = new Material(Shader.Find("Sprites/Default"));
                    renderer.material.color = TargetColor;
                }

                m_TargetMarker.SetActive(false);
            }
        }

        /// <summary>
        /// 设置目标格子并开始寻路
        /// </summary>
        public void SetTarget(Vector3Int targetBlock, Vector3Int? attackCheckBlock = null)
        {
            if (m_HasTarget &&
                m_TargetBlock.HasValue &&
                m_TargetBlock.Value == targetBlock &&
                m_CurrentPath != null &&
                m_CurrentPathIndex < m_CurrentPath.Count)
            {
                m_AttackCheckBlock = attackCheckBlock ?? targetBlock;
                return;
            }

            m_TargetBlock = targetBlock;
            m_AttackCheckBlock = attackCheckBlock ?? targetBlock;
            m_HasTarget = true;

            Vector3Int startPos = GetPlayerGridPosition();

            m_CurrentPath = FindPathUsingConfiguredStrategy(startPos, targetBlock);

            if (m_CurrentPath != null && m_CurrentPath.Count > 0)
            {
                m_CurrentPath = AStarPathfinding.SimplifyPath(m_CurrentPath);
                m_CurrentPathIndex = 0;
                if (m_CurrentPath.Count > 1 && m_CurrentPath[0] == startPos)
                {
                    // Skip the current cell node to prevent "snap back to cell center" on each click.
                    m_CurrentPathIndex = 1;
                }
                m_IsMoving = true;
                m_LastStuckCheckPos = m_PlayerEntity.Position;
                m_LastStuckCheckTime = Time.time;

                UpdatePathVisuals();
                UpdateTargetMarker();
            }
            else
            {
                StopMovement();
            }
        }

        /// <summary>
        /// 停止移动
        /// </summary>
        public void StopMovement()
        {
            m_IsMoving = false;
            m_CurrentPath = null;
            m_CurrentPathIndex = 0;
            m_HasDirection = false;
            m_TargetDirection = Vector3.zero;
            m_HasVerticalMovement = false;
            m_LastStuckCheckPos = transform.position;
            m_LastStuckCheckTime = Time.time;

            if (m_PathLineRenderer != null)
            {
                m_PathLineRenderer.enabled = false;
            }

            if (m_TargetMarker != null)
            {
                m_TargetMarker.SetActive(false);
            }
        }

        /// <summary>
        /// 清除目标
        /// </summary>
        public void ClearTarget()
        {
            StopMovement();
            m_TargetBlock = null;
            m_AttackCheckBlock = null;
            m_HasTarget = false;
        }

        private void Update()
        {
            if (!m_IsMoving || m_CurrentPath == null || m_PlayerEntity == null)
            {
                m_HasDirection = false;
                m_HasVerticalMovement = false;
                return;
            }

            if (m_CurrentPathIndex >= m_CurrentPath.Count)
            {
                OnReachedTarget();
                return;
            }

            Vector3Int targetWaypoint = m_CurrentPath[m_CurrentPathIndex];
            Vector3 targetPos = new Vector3(targetWaypoint.x + 0.5f, targetWaypoint.y + 0.5f, targetWaypoint.z + 0.5f);
            Vector3 currentPos = m_PlayerEntity.Position;

            Vector3 direction = (targetPos - currentPos);
            
            float horizontalDistance = new Vector2(direction.x, direction.z).magnitude;
            int currentGridY = GetPlayerWalkableGridY(currentPos);
            int verticalGridDistance = targetWaypoint.y - currentGridY;
            bool isLastWaypoint = m_CurrentPathIndex >= m_CurrentPath.Count - 1;
            float waypointReachThreshold = isLastWaypoint ? ArrivalThreshold : Mathf.Min(ArrivalThreshold, WaypointThreshold);
            bool reachedWaypoint = horizontalDistance < waypointReachThreshold && verticalGridDistance == 0;

            bool needsJump = verticalGridDistance > 0 && horizontalDistance < Mathf.Max(ArrivalThreshold + 0.75f, 1.75f);

            if (reachedWaypoint)
            {
                m_CurrentPathIndex++;
                UpdatePathVisuals();
                m_HasDirection = false;
                m_HasVerticalMovement = false;
                return;
            }

            TryRecoverFromStuck(currentPos, horizontalDistance, needsJump);

            Vector3 horizontalDir = new Vector3(direction.x, 0, direction.z);
            if (horizontalDir.sqrMagnitude > 0.0001f)
            {
                horizontalDir.Normalize();
            }
            else
            {
                horizontalDir = transform.forward;
            }

            m_TargetDirection = horizontalDir;
            m_HasVerticalMovement = needsJump;
            m_HasDirection = true;

            if (horizontalDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(horizontalDir);
            }

            if (m_AttackCheckBlock.HasValue && IsInAttackRange(m_AttackCheckBlock.Value))
            {
                OnReachedAttackRange();
            }
        }

        private void OnReachedTarget()
        {
            StopMovement();
        }

        private void OnReachedAttackRange()
        {
            m_IsMoving = false;
        }

        /// <summary>
        /// 检查是否在攻击范围内
        /// </summary>
        public bool IsInAttackRange(Vector3Int targetBlock)
        {
            Vector3 playerPos = m_PlayerEntity.Position;
            Vector3 blockCenter = new Vector3(targetBlock.x + 0.5f, targetBlock.y + 0.5f, targetBlock.z + 0.5f);
            float distance = Vector3.Distance(new Vector3(playerPos.x, 0, playerPos.z), new Vector3(blockCenter.x, 0, blockCenter.z));
            return distance <= AttackDistance;
        }

        /// <summary>
        /// 获取玩家当前格子位置
        /// </summary>
        private Vector3Int GetPlayerGridPosition()
        {
            Vector3 pos = m_PlayerEntity.Position;
            return new Vector3Int(Mathf.FloorToInt(pos.x), GetPlayerWalkableGridY(pos), Mathf.FloorToInt(pos.z));
        }

        private int GetPlayerWalkableGridY(Vector3 worldPosition)
        {
            // Pathfinding y means the "feet block space" (air above solid ground), not entity center y.
            float feetY = worldPosition.y + m_PlayerEntity.BoundingBox.Min.y + 0.01f;
            return Mathf.FloorToInt(feetY);
        }

        private void TryRecoverFromStuck(Vector3 currentPos, float horizontalDistance, bool needsJump)
        {
            if (!m_IsMoving || m_CurrentPath == null || !m_TargetBlock.HasValue)
            {
                return;
            }

            if (needsJump)
            {
                // Let jump resolve first before considering this as a stuck scenario.
                m_LastStuckCheckPos = currentPos;
                m_LastStuckCheckTime = Time.time;
                return;
            }

            float now = Time.time;
            if (now - m_LastStuckCheckTime < StuckCheckInterval)
            {
                return;
            }

            float progress = Vector2.Distance(
                new Vector2(currentPos.x, currentPos.z),
                new Vector2(m_LastStuckCheckPos.x, m_LastStuckCheckPos.z));

            m_LastStuckCheckPos = currentPos;
            m_LastStuckCheckTime = now;

            if (progress >= MinProgressDistance || horizontalDistance < Mathf.Max(WaypointThreshold, 0.2f))
            {
                return;
            }

            if (now - m_LastRepathTime < RepathCooldown)
            {
                return;
            }

            m_LastRepathTime = now;

            TryRepath();
        }

        private bool TryRepath()
        {
            if (!m_TargetBlock.HasValue)
            {
                return false;
            }

            Vector3Int startPos = GetPlayerGridPosition();
            List<Vector3Int> newPath = FindPathUsingConfiguredStrategy(startPos, m_TargetBlock.Value);
            if (newPath == null || newPath.Count == 0)
            {
                return false;
            }

            m_CurrentPath = AStarPathfinding.SimplifyPath(newPath);
            m_CurrentPathIndex = 0;
            m_IsMoving = true;
            m_LastStuckCheckPos = m_PlayerEntity.Position;
            m_LastStuckCheckTime = Time.time;
            UpdatePathVisuals();
            return true;
        }

        private List<Vector3Int> FindPathUsingConfiguredStrategy(Vector3Int startPos, Vector3Int targetBlock)
        {
            List<Vector3Int> path = null;
            IWorld world = m_PlayerEntity.World;

            if (PreferFlowField)
            {
                float flowStartTime = Time.realtimeSinceStartup;
                path = FlowFieldPathfinding.FindPath(
                    startPos,
                    targetBlock,
                    world,
                    Mathf.Max(1000, FlowFieldMaxNodes),
                    Mathf.Max(0.05f, FlowFieldCacheLifetime),
                    Mathf.Max(2, FlowFieldSearchRadius),
                    Mathf.Max(16, FlowFieldMaxPathLength),
                    out int flowBuildExpandedNodes,
                    out int flowIntegrationNodeCount,
                    out int flowTraceSteps,
                    out bool flowCacheHit);
                float flowMs = (Time.realtimeSinceStartup - flowStartTime) * 1000f;

                if (LogPathfindingStats)
                {
                    Debug.Log(
                        $"[Pathfinding][FlowField] success={path != null && path.Count > 0} " +
                        $"timeMs={flowMs:F3} cacheHit={flowCacheHit} " +
                        $"buildExpanded={flowBuildExpandedNodes} integrationNodes={flowIntegrationNodeCount} traceSteps={flowTraceSteps} " +
                        $"start={startPos} end={targetBlock}");
                }
            }

            if (path == null || path.Count == 0)
            {
                float aStarStartTime = Time.realtimeSinceStartup;
                path = AStarPathfinding.FindPath(startPos, targetBlock, world, 5000, out int aStarExpandedNodes);
                float aStarMs = (Time.realtimeSinceStartup - aStarStartTime) * 1000f;

                if (LogPathfindingStats)
                {
                    Debug.Log(
                        $"[Pathfinding][AStar] success={path != null && path.Count > 0} " +
                        $"timeMs={aStarMs:F3} expandedNodes={aStarExpandedNodes} start={startPos} end={targetBlock}");
                }
            }

            return path;
        }

        private void UpdatePathVisuals()
        {
            if (!ShowPath || m_PathLineRenderer == null || m_CurrentPath == null)
            {
                if (m_PathLineRenderer != null)
                {
                    m_PathLineRenderer.enabled = false;
                }
                return;
            }

            m_PathLineRenderer.enabled = true;
            m_PathLineRenderer.positionCount = m_CurrentPath.Count - m_CurrentPathIndex;

            for (int i = m_CurrentPathIndex; i < m_CurrentPath.Count; i++)
            {
                Vector3Int pos = m_CurrentPath[i];
                m_PathLineRenderer.SetPosition(i - m_CurrentPathIndex, new Vector3(pos.x + 0.5f, pos.y + 0.1f, pos.z + 0.5f));
            }
        }

        private void UpdateTargetMarker()
        {
            if (!ShowPath || m_TargetMarker == null || !m_TargetBlock.HasValue)
            {
                if (m_TargetMarker != null)
                {
                    m_TargetMarker.SetActive(false);
                }
                return;
            }

            m_TargetMarker.SetActive(true);
            m_TargetMarker.transform.position = new Vector3(
                m_TargetBlock.Value.x + 0.5f,
                m_TargetBlock.Value.y + 0.5f,
                m_TargetBlock.Value.z + 0.5f
            );
        }

        private void OnDrawGizmos()
        {
            if (!ShowPath || m_CurrentPath == null)
            {
                return;
            }

            Gizmos.color = PathColor;

            for (int i = m_CurrentPathIndex; i < m_CurrentPath.Count - 1; i++)
            {
                Vector3 from = new Vector3(m_CurrentPath[i].x + 0.5f, m_CurrentPath[i].y + 0.1f, m_CurrentPath[i].z + 0.5f);
                Vector3 to = new Vector3(m_CurrentPath[i + 1].x + 0.5f, m_CurrentPath[i + 1].y + 0.1f, m_CurrentPath[i + 1].z + 0.5f);
                Gizmos.DrawLine(from, to);
            }

            if (m_TargetBlock.HasValue)
            {
                Gizmos.color = TargetColor;
                Gizmos.DrawWireCube(
                    new Vector3(m_TargetBlock.Value.x + 0.5f, m_TargetBlock.Value.y + 0.5f, m_TargetBlock.Value.z + 0.5f),
                    Vector3.one * 0.8f
                );
            }
        }
    }
}
