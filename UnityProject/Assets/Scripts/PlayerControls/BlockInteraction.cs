using System;
using Minecraft.Configurations;
using Minecraft.Entities;
using Minecraft.Lua;
using Minecraft.PhysicSystem;
using Minecraft.Rendering;
using UnityEngine;
using UnityEngine.UI;
using Physics = Minecraft.PhysicSystem.Physics;

namespace Minecraft.PlayerControls
{
    [DisallowMultipleComponent]
    public class BlockInteraction : MonoBehaviour, ILuaCallCSharp
    {
        [Range(3, 12)] public float RaycastMaxDistance = 2;
        [Min(0.1f)] public float MaxClickSpacing = 0.4f;

        [Header("调试设置")]
        [Tooltip("是否显示调试射线")]
        public bool ShowDebugRay = true;
        [Tooltip("射线颜色（未命中）")]
        public Color RayColor = Color.green;
        [Tooltip("射线颜色（命中）")]
        public Color HitColor = Color.red;
        [Tooltip("射线宽度")]
        public float RayWidth = 0.02f;

        [SerializeField] private Text m_CurrentHandBlockText;
        [SerializeField] private InputField m_HandBlockInput;
        [SerializeField] private MonoBehaviour[] m_DisableWhenEditHandBlock;

        [NonSerialized] private Camera m_Camera;
        [NonSerialized] private IAABBEntity m_PlayerEntity;
        [NonSerialized] private Func<BlockData, bool> m_DestroyRaycastSelector;
        [NonSerialized] private Func<BlockData, bool> m_PlaceRaycastSelector;

        [NonSerialized] private bool m_IsDigging;
        [NonSerialized] private float m_DiggingDamage;
        [NonSerialized] private Vector3Int m_FirstDigPos;
        [NonSerialized] private Vector3Int m_ClickedPos;
        [NonSerialized] private float m_ClickTime;

        [NonSerialized] private GameObject m_HandBlockInputGO;

        [NonSerialized] private Vector3 m_DebugRayOrigin;
        [NonSerialized] private Vector3 m_DebugRayDirection;
        [NonSerialized] private Vector3? m_DebugHitPoint;
        [NonSerialized] private bool m_DebugHasHit;
        [NonSerialized] private LineRenderer m_RayLineRenderer;
        [NonSerialized] private GameObject m_HitMarkerObject;

        public string CurrentHandBlockInternalName
        {
            get
            {
                if (m_CurrentHandBlockText == null || string.IsNullOrWhiteSpace(m_CurrentHandBlockText.text))
                {
                    return string.Empty;
                }

                return m_CurrentHandBlockText.text.Trim();
            }
        }

        public void Initialize(Camera camera, IAABBEntity playerEntity)
        {
            m_Camera = camera;
            m_PlayerEntity = playerEntity;
            m_DestroyRaycastSelector = DestroyRaycastSelect;
            m_PlaceRaycastSelector = PlaceRaycastSelect;
            
            InitializeDebugVisuals();
        }

        private void InitializeDebugVisuals()
        {
            if (m_RayLineRenderer == null)
            {
                GameObject rayObj = new GameObject("DebugRay");
                rayObj.transform.SetParent(transform);
                m_RayLineRenderer = rayObj.AddComponent<LineRenderer>();
                m_RayLineRenderer.startWidth = RayWidth;
                m_RayLineRenderer.endWidth = RayWidth;
                m_RayLineRenderer.positionCount = 2;
                m_RayLineRenderer.useWorldSpace = true;
                m_RayLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                m_RayLineRenderer.enabled = ShowDebugRay;
            }

            if (m_HitMarkerObject == null)
            {
                m_HitMarkerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                m_HitMarkerObject.name = "DebugHitMarker";
                m_HitMarkerObject.transform.SetParent(transform);
                m_HitMarkerObject.transform.localScale = Vector3.one * 0.1f;
                
                Collider col = m_HitMarkerObject.GetComponent<Collider>();
                if (col != null)
                {
                    Destroy(col);
                }
                
                Renderer renderer = m_HitMarkerObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = new Material(Shader.Find("Sprites/Default"));
                    renderer.material.color = HitColor;
                }
                
                m_HitMarkerObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            m_IsDigging = false;
            m_DiggingDamage = 0;
            m_FirstDigPos = Vector3Int.down;
            m_ClickedPos = Vector3Int.down;
            m_ClickTime = 0;
            SetDigProgress(0);

            m_HandBlockInputGO = m_HandBlockInput.gameObject;
            
            m_DebugHitPoint = null;
            m_DebugHasHit = false;

            if (m_RayLineRenderer != null)
            {
                m_RayLineRenderer.enabled = ShowDebugRay;
            }
        }

        private void OnDisable()
        {
            SetDigProgress(0);
            ShaderUtility.TargetedBlockPosition = Vector3.down;
            
            if (m_RayLineRenderer != null)
            {
                m_RayLineRenderer.enabled = false;
            }
            
            if (m_HitMarkerObject != null)
            {
                m_HitMarkerObject.SetActive(false);
            }
        }

        private void Update()
        {
            return;

            if (ChangeHandBlock())
            {
                return;
            }

            if (!m_Camera)
            {
                return;
            }

            Ray ray = GetRay();
            IWorld world = m_PlayerEntity.World;

            if (ShowDebugRay)
            {
                UpdateDebugRayData(ray, world);
                UpdateDebugVisuals();
            }

            DigBlock(ray, world);
            PlaceBlock(ray, world);
        }

        private void UpdateDebugRayData(Ray ray, IWorld world)
        {
            m_DebugRayOrigin = ray.origin;
            m_DebugRayDirection = ray.direction;

            if (Physics.RaycastBlock(ray, RaycastMaxDistance, world, m_DestroyRaycastSelector, out BlockRaycastHit hit))
            {
                m_DebugHitPoint = new Vector3(hit.Position.x + 0.5f, hit.Position.y + 0.5f, hit.Position.z + 0.5f);
                m_DebugHasHit = true;
            }
            else
            {
                m_DebugHitPoint = null;
                m_DebugHasHit = false;
            }
        }

        private void UpdateDebugVisuals()
        {
            if (m_RayLineRenderer == null)
            {
                InitializeDebugVisuals();
            }

            if (m_DebugHasHit && m_DebugHitPoint.HasValue)
            {
                m_RayLineRenderer.enabled = true;
                m_RayLineRenderer.startColor = HitColor;
                m_RayLineRenderer.endColor = HitColor;
                m_RayLineRenderer.SetPosition(0, m_DebugRayOrigin);
                m_RayLineRenderer.SetPosition(1, m_DebugHitPoint.Value);

                if (m_HitMarkerObject != null)
                {
                    m_HitMarkerObject.SetActive(true);
                    m_HitMarkerObject.transform.position = m_DebugHitPoint.Value;
                }
            }
            else
            {
                m_RayLineRenderer.enabled = true;
                m_RayLineRenderer.startColor = RayColor;
                m_RayLineRenderer.endColor = RayColor;
                m_RayLineRenderer.SetPosition(0, m_DebugRayOrigin);
                m_RayLineRenderer.SetPosition(1, m_DebugRayOrigin + m_DebugRayDirection * RaycastMaxDistance);

                if (m_HitMarkerObject != null)
                {
                    m_HitMarkerObject.SetActive(false);
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (!ShowDebugRay || !enabled)
            {
                return;
            }

            if (m_DebugHasHit && m_DebugHitPoint.HasValue)
            {
                Gizmos.color = HitColor;
                Gizmos.DrawRay(m_DebugRayOrigin, m_DebugRayDirection * Vector3.Distance(m_DebugRayOrigin, m_DebugHitPoint.Value));

                Vector3 hitPoint = m_DebugHitPoint.Value;
                float size = 0.1f;
                
                Gizmos.DrawWireCube(hitPoint, Vector3.one * size);
                
                Gizmos.DrawLine(hitPoint - Vector3.up * 0.3f, hitPoint + Vector3.up * 0.3f);
                Gizmos.DrawLine(hitPoint - Vector3.right * 0.3f, hitPoint + Vector3.right * 0.3f);
                Gizmos.DrawLine(hitPoint - Vector3.forward * 0.3f, hitPoint + Vector3.forward * 0.3f);
            }
            else
            {
                Gizmos.color = RayColor;
                Gizmos.DrawRay(m_DebugRayOrigin, m_DebugRayDirection * RaycastMaxDistance);
            }
        }

        private bool ChangeHandBlock()
        {
            if (!Input.GetKeyDown(KeyCode.Return))
            {
                return m_HandBlockInputGO.activeInHierarchy;
            }

            if (m_HandBlockInputGO.activeInHierarchy)
            {
                m_CurrentHandBlockText.text = m_HandBlockInput.text;
                m_HandBlockInput.DeactivateInputField();
                m_HandBlockInputGO.SetActive(false);

                for (int i = 0; i < m_DisableWhenEditHandBlock.Length; i++)
                {
                    m_DisableWhenEditHandBlock[i].enabled = true;
                }

                return false;
            }
            else
            {
                for (int i = 0; i < m_DisableWhenEditHandBlock.Length; i++)
                {
                    m_DisableWhenEditHandBlock[i].enabled = false;
                }

                m_HandBlockInputGO.SetActive(true);
                m_HandBlockInput.text = string.Empty;
                m_HandBlockInput.ActivateInputField();

                return true;
            }
        }

        private void DigBlock(Ray ray, IWorld world)
        {
            if (Physics.RaycastBlock(ray, RaycastMaxDistance, world, m_DestroyRaycastSelector, out BlockRaycastHit hit))
            {
                ShaderUtility.TargetedBlockPosition = hit.Position;
            }
            else
            {
                ShaderUtility.TargetedBlockPosition = Vector3.down;
            }
        }

        private void PlaceBlock(Ray ray, IWorld world)
        {
            if (Input.GetMouseButtonDown(1))
            {
                BlockData block = world.BlockDataTable.GetBlock(m_CurrentHandBlockText.text);

                if (Physics.RaycastBlock(ray, RaycastMaxDistance, world, m_PlaceRaycastSelector, out BlockRaycastHit hit))
                {
                    Vector3Int pos = (hit.Position + hit.Normal).FloorToInt();
                    AABB playerBB = m_PlayerEntity.BoundingBox + m_PlayerEntity.Position;
                    AABB blockBB = hit.Block.GetBoundingBox(pos, world, false).Value;

                    if (!playerBB.Intersects(blockBB))
                    {
                        Quaternion rotation = Quaternion.identity;

                        if ((block.RotationAxes & BlockRotationAxes.AroundXOrZAxis) == BlockRotationAxes.AroundXOrZAxis)
                        {
                            rotation *= Quaternion.FromToRotation(Vector3.up, hit.Normal);
                        }

                        if ((block.RotationAxes & BlockRotationAxes.AroundYAxis) == BlockRotationAxes.AroundYAxis)
                        {
                            Vector3 forward = m_PlayerEntity.Forward;
                            forward = Mathf.Abs(forward.x) > Mathf.Abs(forward.z) ? new Vector3(forward.x, 0, 0) : new Vector3(0, 0, forward.z);
                            rotation *= Quaternion.LookRotation(-forward.normalized, Vector3.up);
                        }

                        world.RWAccessor.SetBlock(pos.x, pos.y, pos.z, block, rotation, ModificationSource.PlayerAction);
                    }
                }
            }
        }

        private bool DestroyRaycastSelect(BlockData block)
        {
            return !block.HasFlag(BlockFlags.IgnoreDestroyBlockRaycast) && block.PhysicState == PhysicState.Solid;
        }

        private bool PlaceRaycastSelect(BlockData block)
        {
            return !block.HasFlag(BlockFlags.IgnorePlaceBlockRaycast) && block.PhysicState == PhysicState.Solid;
        }

        public bool TryGetCurrentHandBlockData(IWorld world, out BlockData block)
        {
            block = null;
            if (world?.BlockDataTable == null)
            {
                return false;
            }

            string internalName = CurrentHandBlockInternalName;
            if (string.IsNullOrEmpty(internalName))
            {
                return false;
            }

            try
            {
                block = world.BlockDataTable.GetBlock(internalName);
            }
            catch
            {
                block = null;
                return false;
            }

            return block != null && block.ID != 0;
        }

        private Ray GetRay()
        {
            Vector3 origin = m_PlayerEntity.Position - Vector3.up/2;
            Vector3 direction = m_PlayerEntity.Forward;
            return new Ray(origin, direction);
        }

        private void SetDigProgress(float progress)
        {
            ShaderUtility.DigProgress = (int)(progress * m_PlayerEntity.World.RenderingManager.DigProgressTextureCount) - 1;
        }
    }
}
