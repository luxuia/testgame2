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

        [Header("调试设置")]
        [Tooltip("是否显示路径")]
        public bool ShowPath = true;

        [Tooltip("路径颜色")]
        public Color PathColor = Color.cyan;

        [Tooltip("目标标记颜色")]
        public Color TargetColor = Color.yellow;

        [System.NonSerialized] private IAABBEntity m_PlayerEntity;
        [System.NonSerialized] private List<Vector3Int> m_CurrentPath;
        [System.NonSerialized] private int m_CurrentPathIndex;
        [System.NonSerialized] private Vector3Int? m_TargetBlock;
        [System.NonSerialized] private bool m_IsMoving;
        [System.NonSerialized] private bool m_HasTarget;
        [System.NonSerialized] private LineRenderer m_PathLineRenderer;
        [System.NonSerialized] private GameObject m_TargetMarker;
        [System.NonSerialized] private Vector3 m_TargetDirection;
        [System.NonSerialized] private bool m_HasDirection;
        [System.NonSerialized] private bool m_HasVerticalMovement;

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
            m_IsMoving = false;
            m_HasTarget = false;
            m_HasDirection = false;
            m_TargetDirection = Vector3.zero;

            InitializeDebugVisuals();
            
            Debug.Log($"[PathfindingMovement] Initialized, player: {(m_PlayerEntity != null ? "valid" : "null")}");
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
        public void SetTarget(Vector3Int targetBlock)
        {
            m_TargetBlock = targetBlock;
            m_HasTarget = true;

            Vector3Int startPos = GetPlayerGridPosition();
            m_CurrentPath = AStarPathfinding.FindPath(startPos, targetBlock, m_PlayerEntity.World);

            if (m_CurrentPath != null && m_CurrentPath.Count > 0)
            {
                m_CurrentPath = AStarPathfinding.SimplifyPath(m_CurrentPath);
                m_CurrentPathIndex = 0;
                m_IsMoving = true;

                UpdatePathVisuals();
                UpdateTargetMarker();

                Debug.Log($"[PathfindingMovement] Path found with {m_CurrentPath.Count} waypoints");
            }
            else
            {
                Debug.Log($"[PathfindingMovement] No path found to {targetBlock}");
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
            m_HasTarget = false;
        }

        private void Update()
        {
            if (!m_IsMoving || m_CurrentPath == null || m_PlayerEntity == null)
            {
                m_HasDirection = false;
                return;
            }

            if (m_CurrentPathIndex >= m_CurrentPath.Count)
            {
                OnReachedTarget();
                return;
            }

            Vector3Int targetWaypoint = m_CurrentPath[m_CurrentPathIndex];
            Vector3 targetPos = new Vector3(targetWaypoint.x + 0.5f, m_PlayerEntity.Position.y, targetWaypoint.z + 0.5f);
            Vector3 currentPos = m_PlayerEntity.Position;

            Vector3 direction = (targetPos - currentPos);
            
            float horizontalDistance = new Vector2(direction.x, direction.z).magnitude;
            float verticalDistance = direction.y;

            bool needsJump = verticalDistance > 0.5f && horizontalDistance < 1.5f;

            if (horizontalDistance < ArrivalThreshold)
            {
                if (verticalDistance > 0.5f && !needsJump)
                {
                    m_CurrentPathIndex++;
                    UpdatePathVisuals();
                    m_HasDirection = false;
                    return;
                }

                if (Mathf.Abs(verticalDistance) < 0.5f || needsJump)
                {
                    m_CurrentPathIndex++;
                    UpdatePathVisuals();
                    m_HasDirection = false;
                }
            }
            else
            {
                direction.y = 0;
                direction.Normalize();
                m_TargetDirection = direction;
                m_HasVerticalMovement = needsJump;
                m_HasDirection = true;

                if (direction.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(direction);
                }

                if (true)
                {
                    Debug.Log($"[PathfindingMovement] Moving to waypoint {m_CurrentPathIndex}, direction: {direction}, needsJump: {needsJump}");
                }
            }

            if (m_TargetBlock.HasValue && IsInAttackRange(m_TargetBlock.Value))
            {
                OnReachedAttackRange();
            }
        }

        private void OnReachedTarget()
        {
            Debug.Log($"[PathfindingMovement] Reached target");
            StopMovement();
        }

        private void OnReachedAttackRange()
        {
            Debug.Log($"[PathfindingMovement] In attack range of {m_TargetBlock}");
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
            return new Vector3Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
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
