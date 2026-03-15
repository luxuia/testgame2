using System;
using Minecraft.Entities;
using Minecraft.PhysicSystem;
using Minecraft.Rendering;
using Minecraft.UI;
using UnityEngine;
using UnityEngine.Events;
using Physics = Minecraft.PhysicSystem.Physics;

namespace Minecraft.PlayerControls
{
    /// <summary>
    /// 目标选择器，处理鼠标点击选中目标格子
    /// </summary>
    [DisallowMultipleComponent]
    public class TargetSelector : MonoBehaviour
    {
        [Header("选择设置")]
        [Tooltip("选择距离")]
        public float SelectionDistance = 50f;

        [Tooltip("目标标记颜色")]
        public Color TargetMarkerColor = Color.yellow;

        [Header("事件")]
        [SerializeField] private UnityEvent<Vector3Int> m_OnTargetSelected;
        [SerializeField] private UnityEvent m_OnTargetCleared;

        [NonSerialized] private Camera m_Camera;
        [NonSerialized] private IAABBEntity m_PlayerEntity;
        [NonSerialized] private Vector3Int? m_SelectedTarget;
        [NonSerialized] private GameObject m_TargetMarker;
        [NonSerialized] private LineRenderer m_SelectionLine;

        /// <summary>
        /// 目标选中事件
        /// </summary>
        public event UnityAction<Vector3Int> OnTargetSelectedEvent;

        /// <summary>
        /// 目标清除事件
        /// </summary>
        public event UnityAction OnTargetClearedEvent;

        /// <summary>
        /// 当前选中的目标
        /// </summary>
        public Vector3Int? SelectedTarget => m_SelectedTarget;

        /// <summary>
        /// 初始化目标选择器
        /// </summary>
        public void Initialize(Camera camera, IAABBEntity playerEntity)
        {
            m_Camera = camera;
            m_PlayerEntity = playerEntity;
            
            InitializeVisuals();
            
            Debug.Log($"[TargetSelector] Initialized, camera: {(m_Camera != null ? "valid" : "null")}, player: {(m_PlayerEntity != null ? "valid" : "null")}");
        }

        private void InitializeVisuals()
        {
            if (m_TargetMarker == null)
            {
                m_TargetMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                m_TargetMarker.name = "TargetMarker";
                m_TargetMarker.transform.SetParent(transform);
                m_TargetMarker.transform.localScale = Vector3.one * 1.05f;

                var col = m_TargetMarker.GetComponent<Collider>();
                if (col != null)
                {
                    Destroy(col);
                }

                var renderer = m_TargetMarker.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = new Material(Shader.Find("Sprites/Default"));
                    renderer.material.color = TargetMarkerColor;
                    renderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    renderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    renderer.material.SetInt("_ZWrite", 0);
                    renderer.material.DisableKeyword("_ALPHATEST_ON");
                    renderer.material.EnableKeyword("_ALPHABLEND_ON");
                    renderer.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    renderer.material.renderQueue = 3000;
                    var color = TargetMarkerColor;
                    color.a = 0.5f;
                    renderer.material.color = color;
                }

                m_TargetMarker.SetActive(false);
            }

            if (m_SelectionLine == null)
            {
                var lineObj = new GameObject("SelectionLine");
                lineObj.transform.SetParent(transform);
                m_SelectionLine = lineObj.AddComponent<LineRenderer>();
                m_SelectionLine.startWidth = 0.05f;
                m_SelectionLine.endWidth = 0.05f;
                m_SelectionLine.useWorldSpace = true;
                m_SelectionLine.material = new Material(Shader.Find("Sprites/Default"));
                m_SelectionLine.startColor = TargetMarkerColor;
                m_SelectionLine.endColor = TargetMarkerColor;
                m_SelectionLine.enabled = false;
            }
        }

        private void Update()
        {
            if (m_Camera == null || m_PlayerEntity == null)
            {
                return;
            }

            HandleInput();
            UpdateVisuals();
        }

        private void HandleInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 cursorPosition = GetCursorPosition();
                Ray ray = m_Camera.ScreenPointToRay(cursorPosition);
                IWorld world = m_PlayerEntity.World;

                Debug.Log($"[TargetSelector] Mouse clicked at cursor: {cursorPosition}, ray origin: {ray.origin}, direction: {ray.direction}");
                Debug.Log($"[TargetSelector] World: {(world != null ? "valid" : "null")}");

                if (Physics.RaycastBlock(ray, SelectionDistance, world, SelectRaycastFilter, out BlockRaycastHit hit))
                {
                    Debug.Log($"[TargetSelector] Hit block at: {hit.Position}");
                    
                    Vector3Int targetBlockPos = hit.Position;
                    Vector3Int standPos = FindStandPosition(targetBlockPos, world);
                    
                    Debug.Log($"[TargetSelector] Target block: {targetBlockPos}, stand position: {standPos}");
                    SelectTarget(targetBlockPos, standPos);
                }
                else
                {
                    Debug.Log($"[TargetSelector] No block hit");
                }
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ClearTarget();
            }
        }

        private Vector3Int FindStandPosition(Vector3Int blockPos, IWorld world)
        {
            Vector3Int[] offsets = new Vector3Int[]
            {
                new Vector3Int(1, 0, 0),
                new Vector3Int(-1, 0, 0),
                new Vector3Int(0, 0, 1),
                new Vector3Int(0, 0, -1),
            };

            foreach (var offset in offsets)
            {
                Vector3Int checkPos = blockPos + offset;
                var block = world.RWAccessor.GetBlock(checkPos.x, checkPos.y, checkPos.z);
                
                if (block == null || block.PhysicState != PhysicState.Solid)
                {
                    var groundBlock = world.RWAccessor.GetBlock(checkPos.x, checkPos.y - 1, checkPos.z);
                    if (groundBlock != null && groundBlock.PhysicState == PhysicState.Solid)
                    {
                        return checkPos;
                    }
                }
            }

            return blockPos;
        }

        private Vector2 GetCursorPosition()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                return CursorReticle.MousePos + new Vector2(Screen.width / 2f, Screen.height/2);
            }
            return Input.mousePosition;
        }

        private bool SelectRaycastFilter(Configurations.BlockData block)
        {
            return block.PhysicState == PhysicState.Solid;
        }

        /// <summary>
        /// 选中目标
        /// </summary>
        public void SelectTarget(Vector3Int target)
        {
            m_SelectedTarget = target;
            m_OnTargetSelected?.Invoke(target);
            OnTargetSelectedEvent?.Invoke(target);
            ShaderUtility.TargetedBlockPosition = target;


            Debug.Log($"[TargetSelector] Selected target: {target}");
        }

        /// <summary>
        /// 清除目标
        /// </summary>
        public void ClearTarget()
        {
            if (m_SelectedTarget.HasValue)
            {
                m_SelectedTarget = null;
                m_OnTargetCleared?.Invoke();
                OnTargetClearedEvent?.Invoke();
                ShaderUtility.TargetedBlockPosition = Vector3.down;

                Debug.Log($"[TargetSelector] Target cleared");
            }
        }

        private void UpdateVisuals()
        {
            if (m_SelectedTarget.HasValue)
            {
                if (m_TargetMarker != null)
                {
                    m_TargetMarker.SetActive(true);
                    m_TargetMarker.transform.position = new Vector3(
                        m_SelectedTarget.Value.x + 0.5f,
                        m_SelectedTarget.Value.y + 0.5f,
                        m_SelectedTarget.Value.z + 0.5f
                    );
                }

                if (m_SelectionLine != null && m_PlayerEntity != null)
                {
                    m_SelectionLine.enabled = true;
                    m_SelectionLine.SetPosition(0, m_PlayerEntity.Position + Vector3.up);
                    m_SelectionLine.SetPosition(1, new Vector3(
                        m_SelectedTarget.Value.x + 0.5f,
                        m_SelectedTarget.Value.y + 0.5f,
                        m_SelectedTarget.Value.z + 0.5f
                    ));
                }
            }
            else
            {
                if (m_TargetMarker != null)
                {
                    m_TargetMarker.SetActive(false);
                }

                if (m_SelectionLine != null)
                {
                    m_SelectionLine.enabled = false;
                }
            }
        }

        private void OnDisable()
        {
            ClearTarget();
        }

        private void OnDrawGizmos()
        {
            if (m_SelectedTarget.HasValue)
            {
                Gizmos.color = TargetMarkerColor;
                Gizmos.DrawWireCube(
                    new Vector3(
                        m_SelectedTarget.Value.x + 0.5f,
                        m_SelectedTarget.Value.y + 0.5f,
                        m_SelectedTarget.Value.z + 0.5f
                    ),
                    Vector3.one * 1.1f
                );
            }
        }
    }
}
