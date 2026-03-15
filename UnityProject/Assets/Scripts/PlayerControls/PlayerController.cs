using Minecraft.Entities;
using Minecraft.PlayerControls;
using Minecraft.Rendering;
using UnityEngine;

namespace Minecraft.PlayerControls
{
    /// <summary>
    /// 玩家控制器，整合目标选择、寻路移动和攻击系统
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TargetSelector))]
    [RequireComponent(typeof(PathfindingMovementController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("攻击设置")]
        [Tooltip("攻击伤害")]
        public int AttackDamage = 5;

        [Tooltip("攻击冷却时间")]
        public float AttackCooldown = 0.5f;

        [Header("调试设置")]
        [Tooltip("是否显示调试信息")]
        public bool ShowDebugInfo = true;

        [System.NonSerialized] private IAABBEntity m_PlayerEntity;
        [System.NonSerialized] private TargetSelector m_TargetSelector;
        [System.NonSerialized] private PathfindingMovementController m_MovementController;
        [System.NonSerialized] private Camera m_Camera;

        [System.NonSerialized] private float m_LastAttackTime;
        [System.NonSerialized] private bool m_IsAttacking;
        [System.NonSerialized] private float m_DiggingDamage;
        [System.NonSerialized] private Vector3Int m_CurrentDiggingTarget;

        public bool IsMoving => m_MovementController != null && m_MovementController.IsMoving;
        public bool HasTarget => m_TargetSelector != null && m_TargetSelector.SelectedTarget.HasValue;
        public Vector3Int? TargetPosition => m_TargetSelector?.SelectedTarget;
        public bool HasDirection => m_MovementController != null && m_MovementController.HasDirection;
        public Vector3 TargetDirection => m_MovementController != null ? m_MovementController.TargetDirection : Vector3.zero;
        public bool HasVerticalMovement => m_MovementController != null && m_MovementController.HasVerticalMovement;

        public void CancelCurrentAction()
        {
            if (m_MovementController != null && m_MovementController.IsMoving)
            {
                m_MovementController.ClearTarget();
                
                if (ShowDebugInfo)
                {
                    Debug.Log($"[PlayerController] Pathfinding interrupted by player input");
                }
            }
        }

        public void Initialize(Camera camera, IAABBEntity playerEntity)
        {
            m_Camera = camera;
            m_PlayerEntity = playerEntity;

            m_TargetSelector = GetComponent<TargetSelector>();
            m_MovementController = GetComponent<PathfindingMovementController>();

            m_TargetSelector.Initialize(camera, playerEntity);
            m_MovementController.Initialize(playerEntity);

            m_TargetSelector.OnTargetSelectedEvent += OnTargetSelected;
            m_TargetSelector.OnTargetClearedEvent += OnTargetCleared;

            m_TargetSelector.enabled = true;
            m_MovementController.enabled = true;

            m_LastAttackTime = -999f;
            m_IsAttacking = false;
            m_DiggingDamage = 0;
            m_CurrentDiggingTarget = Vector3Int.down;

            Debug.Log($"[PlayerController] Initialized, TargetSelector: {(m_TargetSelector != null ? "valid" : "null")}, MovementController: {(m_MovementController != null ? "valid" : "null")}");
        }

        private void OnTargetSelected(Vector3Int target)
        {
            m_MovementController.SetTarget(target);
            
            if (ShowDebugInfo)
            {
                Debug.Log($"[PlayerController] Target selected: {target}, starting pathfinding");
            }
        }

        private void OnTargetCleared()
        {
            m_MovementController.ClearTarget();
            m_DiggingDamage = 0;
            m_CurrentDiggingTarget = Vector3Int.down;
            ShaderUtility.DigProgress = 0;
            ShaderUtility.TargetedBlockPosition = Vector3.down;

            if (ShowDebugInfo)
            {
                Debug.Log($"[PlayerController] Target cleared");
            }
        }

        private void Update()
        {
            if (m_PlayerEntity == null || m_TargetSelector == null || m_MovementController == null)
            {
                return;
            }

            UpdateAttack();
        }

        private void UpdateAttack()
        {
            if (!m_TargetSelector.SelectedTarget.HasValue)
            {
                ShaderUtility.DigProgress = 0;
                ShaderUtility.TargetedBlockPosition = Vector3.down;
                return;
            }

            Vector3Int targetWalkablePos = m_TargetSelector.SelectedTarget.Value;
            Vector3Int targetBlockPos = new Vector3Int(targetWalkablePos.x, targetWalkablePos.y - 1, targetWalkablePos.z);

            ShaderUtility.TargetedBlockPosition = targetBlockPos;

            if (m_MovementController.IsInAttackRange(targetWalkablePos))
            {
                if (!m_MovementController.IsMoving)
                {
                    if (targetWalkablePos == m_CurrentDiggingTarget)
                    {
                        m_DiggingDamage += Time.deltaTime * 5;
                        
                        IWorld world = m_PlayerEntity.World;
                        var block = world.RWAccessor.GetBlock(targetBlockPos.x, targetBlockPos.y, targetBlockPos.z);
                        
                        if (block != null && block.ID != 0)
                        {
                            float progress = m_DiggingDamage / block.Hardness;
                            ShaderUtility.DigProgress = (int)(progress * world.RenderingManager.DigProgressTextureCount) - 1;
                            
                            if (ShowDebugInfo)
                            {
                                Debug.Log($"[PlayerController] Digging block at {targetBlockPos}: {m_DiggingDamage}/{block.Hardness}, progress: {progress * 100}%");
                            }

                            if (m_DiggingDamage >= block.Hardness)
                            {
                                world.RWAccessor.SetBlock(targetBlockPos.x, targetBlockPos.y, targetBlockPos.z, world.BlockDataTable.GetBlock(0), Quaternion.identity, ModificationSource.PlayerAction);
                                
                                ShaderUtility.DigProgress = 0;
                                m_TargetSelector.ClearTarget();
                                m_DiggingDamage = 0;
                                m_CurrentDiggingTarget = Vector3Int.down;

                                if (ShowDebugInfo)
                                {
                                    Debug.Log($"[PlayerController] Destroyed block at {targetBlockPos}");
                                }
                            }
                        }
                    }
                    else
                    {
                        m_CurrentDiggingTarget = targetWalkablePos;
                        m_DiggingDamage = 0;
                    }
                }
            }
            else
            {
                m_DiggingDamage = 0;
                m_CurrentDiggingTarget = Vector3Int.down;
            }
        }

        private void OnDestroy()
        {
            if (m_TargetSelector != null)
            {
                m_TargetSelector.OnTargetSelectedEvent -= OnTargetSelected;
                m_TargetSelector.OnTargetClearedEvent -= OnTargetCleared;
            }
        }

        private void OnDrawGizmos()
        {
            if (!ShowDebugInfo || !m_TargetSelector.SelectedTarget.HasValue)
            {
                return;
            }

            Vector3Int target = m_TargetSelector.SelectedTarget.Value;

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(
                new Vector3(target.x + 0.5f, target.y + 0.5f, target.z + 0.5f),
                Vector3.one * 0.8f
            );

            if (m_PlayerEntity != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(
                    m_PlayerEntity.Position + Vector3.up,
                    new Vector3(target.x + 0.5f, target.y + 0.5f, target.z + 0.5f)
                );
            }
        }
    }
}
