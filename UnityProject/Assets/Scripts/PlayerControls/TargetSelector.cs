using System;
using System.Collections.Generic;
using Minecraft.Entities;
using Minecraft.PhysicSystem;
using Minecraft.Rendering;
using UnityEngine;
using UnityEngine.Events;
using Physics = Minecraft.PhysicSystem.Physics;

namespace Minecraft.PlayerControls
{
    public enum TargetSelectionAction
    {
        None = 0,
        Mine = 1,
        Build = 2,
    }

    /// <summary>
    /// 目标选择器，处理鼠标点击选中目标格子
    /// </summary>
    [DisallowMultipleComponent]
    public class TargetSelector : MonoBehaviour
    {
        private static readonly Vector3Int[] s_StandSearchOffsets = new Vector3Int[]
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 0, -1),
        };

        [Header("选择设置")]
        [Tooltip("选择距离")]
        public float SelectionDistance = 50f;

        [Tooltip("目标标记颜色")]
        public Color TargetMarkerColor = Color.yellow;

        [Tooltip("拖拽判定阈值（像素）")]
        public float DragThresholdPixels = 8f;

        [Tooltip("单次框选的最大方块数量（超出将截断）")]
        public int MaxSelectedBlocks = 256;

        [Tooltip("是否显示多选标记")]
        public bool ShowSelectionMarkers = true;

        [Tooltip("多选标记颜色")]
        public Color SelectionMarkerColor = new Color(1f, 1f, 0f, 0.35f);

        [Tooltip("agent正在处理中的方块颜色")]
        public Color ProcessingSelectionMarkerColor = new Color(1f, 0.45f, 0.1f, 0.45f);

        [Tooltip("多选标记缩放")]
        public float SelectionMarkerScale = 1.03f;

        [Header("事件")]
        [SerializeField] private UnityEvent<Vector3Int> m_OnTargetSelected;
        [SerializeField] private UnityEvent m_OnTargetCleared;

        [NonSerialized] private Camera m_Camera;
        [NonSerialized] private IAABBEntity m_PlayerEntity;
        [NonSerialized] private Vector3Int? m_SelectedTargetBlock;
        [NonSerialized] private Vector3Int? m_StandPosition;
        [NonSerialized] private TargetSelectionAction m_SelectedAction;
        [NonSerialized] private GameObject m_TargetMarker;
        [NonSerialized] private LineRenderer m_SelectionLine;
        [NonSerialized] private bool m_WasCursorLocked;
        [NonSerialized] private Func<Configurations.BlockData, bool> m_SurfaceRaycastFilter;
        [NonSerialized] private Transform m_RuntimeMarkerRoot;
        [NonSerialized] private readonly List<Vector3Int> m_SelectedBlocks = new List<Vector3Int>(64);
        [NonSerialized] private readonly HashSet<Vector3Int> m_SelectedBlockSet = new HashSet<Vector3Int>();
        [NonSerialized] private readonly List<GameObject> m_SelectionMarkers = new List<GameObject>(64);
        [NonSerialized] private readonly Stack<GameObject> m_SelectionMarkerPool = new Stack<GameObject>(64);
        [NonSerialized] private bool m_IsPointerSelecting;
        [NonSerialized] private int m_PointerMouseButton = -1;
        [NonSerialized] private TargetSelectionAction m_PointerAction;
        [NonSerialized] private Vector2 m_PointerStartCursor;
        [NonSerialized] private bool m_IsDragging;
        [NonSerialized] private readonly List<Vector3Int> m_StrokeBlocks = new List<Vector3Int>(64);
        [NonSerialized] private readonly HashSet<Vector3Int> m_StrokeBlockSet = new HashSet<Vector3Int>();
        [NonSerialized] private readonly List<Vector3Int> m_PreviewBlocks = new List<Vector3Int>(64);
        [NonSerialized] private readonly HashSet<Vector3Int> m_PreviewBlockSet = new HashSet<Vector3Int>();
        [NonSerialized] private Vector3Int? m_PreviewTargetBlock;
        [NonSerialized] private Vector3Int? m_PreviewStandPosition;
        [NonSerialized] private bool m_SelectionVisualDirty;
        [NonSerialized] private int m_SelectionVersion;
        [NonSerialized] private readonly HashSet<Vector3Int> m_ProcessingBlocks = new HashSet<Vector3Int>();

        public Vector3Int? SelectedTargetBlock => m_SelectedTargetBlock;
        public Vector3Int? StandPosition => m_StandPosition;
        public TargetSelectionAction SelectedAction => m_SelectedAction;
        public bool IsMineSelection => m_SelectedAction == TargetSelectionAction.Mine;
        public bool IsBuildSelection => m_SelectedAction == TargetSelectionAction.Build;
        public IReadOnlyList<Vector3Int> SelectedBlocks => m_SelectedBlocks;
        public int SelectionCount => m_SelectedBlocks.Count;
        public bool IsMultiSelection => m_SelectedBlocks.Count > 1;
        public int SelectionVersion => m_SelectionVersion;

        public event UnityAction<Vector3Int> OnTargetSelectedEvent;
        public event UnityAction OnTargetClearedEvent;
        public event UnityAction OnSelectionChangedEvent;

        public void Initialize(Camera camera, IAABBEntity playerEntity)
        {
            m_Camera = camera;
            m_PlayerEntity = playerEntity;
            m_SurfaceRaycastFilter ??= SelectSurfaceRaycastFilter;
            
            InitializeVisuals();
        }

        private void InitializeVisuals()
        {
            EnsureRuntimeMarkerRoot();

            if (m_TargetMarker == null)
            {
                m_TargetMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                m_TargetMarker.name = "TargetMarker";
                m_TargetMarker.transform.localScale = Vector3.one * 1.05f;
                m_TargetMarker.transform.SetParent(m_RuntimeMarkerRoot, false);

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

        private void OnEnable()
        {
            m_SelectedTargetBlock = null;
            m_StandPosition = null;
            m_SelectedAction = TargetSelectionAction.None;
            m_SelectedBlocks.Clear();
            m_SelectedBlockSet.Clear();
            m_IsPointerSelecting = false;
            m_PointerMouseButton = -1;
            m_PointerAction = TargetSelectionAction.None;
            m_IsDragging = false;
            m_PreviewTargetBlock = null;
            m_PreviewStandPosition = null;
            m_PreviewBlocks.Clear();
            m_PreviewBlockSet.Clear();
            m_SelectionVisualDirty = true;
            m_WasCursorLocked = Cursor.lockState == CursorLockMode.Locked;
            
            if (m_TargetMarker != null)
            {
                m_TargetMarker.SetActive(false);
            }
            
            if (m_SelectionLine != null)
            {
                m_SelectionLine.enabled = false;
            }

            HideAllSelectionMarkers();
        }

        private void Update()
        {
            if (m_Camera == null || m_PlayerEntity == null)
            {
                return;
            }

            bool isCursorLocked = Cursor.lockState == CursorLockMode.Locked;
            if (m_WasCursorLocked && !isCursorLocked)
            {
                m_WasCursorLocked = isCursorLocked;
                return;
            }
            m_WasCursorLocked = isCursorLocked;

            HandleInput();
            UpdateVisuals();
        }

        private void HandleInput()
        {
            if (!m_IsPointerSelecting)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    TryBeginPointerSelection(0, TargetSelectionAction.Mine);
                }
                else if (Input.GetMouseButtonDown(1))
                {
                    TryBeginPointerSelection(1, TargetSelectionAction.Build);
                }
            }

            if (m_IsPointerSelecting && m_PointerMouseButton >= 0)
            {
                if (Input.GetMouseButton(m_PointerMouseButton))
                {
                    Vector2 cursorPosition = GetCursorPosition();
                    float threshold = Mathf.Max(0f, DragThresholdPixels);
                    if (!m_IsDragging && (cursorPosition - m_PointerStartCursor).sqrMagnitude > threshold * threshold)
                    {
                        m_IsDragging = true;
                    }

                    TryAppendPointerSample();
                }

                if (Input.GetMouseButtonUp(m_PointerMouseButton))
                {
                    ResolvePointerSelectionRelease();
                    CancelPointerSelection();
                }
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ClearTarget();
            }
        }

        private void TryBeginPointerSelection(int mouseButton, TargetSelectionAction action)
        {
            if (m_Camera == null || m_PlayerEntity == null)
            {
                return;
            }

            if (!TryGetPointerSelectionBlock(action, out Vector3Int block))
            {
                return;
            }

            m_IsPointerSelecting = true;
            m_PointerMouseButton = mouseButton;
            m_PointerAction = action;
            m_PointerStartCursor = GetCursorPosition();
            m_IsDragging = false;
            m_StrokeBlocks.Clear();
            m_StrokeBlockSet.Clear();
            if (AppendStrokeBlock(block))
            {
                RebuildPreviewSelection(m_PlayerEntity.World, action);
            }
        }

        private void TryAppendPointerSample()
        {
            if (!m_IsPointerSelecting || m_PointerAction == TargetSelectionAction.None)
            {
                return;
            }

            if (TryGetPointerSelectionBlock(m_PointerAction, out Vector3Int block))
            {
                if (AppendStrokeBlock(block))
                {
                    RebuildPreviewSelection(m_PlayerEntity.World, m_PointerAction);
                }
            }
        }

        private void ResolvePointerSelectionRelease()
        {
            if (m_Camera == null || m_PlayerEntity == null)
            {
                return;
            }

            if (m_StrokeBlocks.Count == 0)
            {
                return;
            }

            IWorld world = m_PlayerEntity.World;
            if (world == null)
            {
                return;
            }

            ApplyStrokeSelection(world, m_PointerAction, m_StrokeBlocks, !m_IsDragging);
        }

        private void ApplyStrokeSelection(IWorld world, TargetSelectionAction action, IList<Vector3Int> blocks, bool invokeSingleSelectEvent)
        {
            m_SelectedBlocks.Clear();
            m_SelectedBlockSet.Clear();

            int maxCount = Mathf.Max(1, MaxSelectedBlocks);
            for (int i = 0; i < blocks.Count && m_SelectedBlocks.Count < maxCount; i++)
            {
                Vector3Int block = blocks[i];
                if (!IsSelectableBlockForAction(world, action, block))
                {
                    continue;
                }

                if (!m_SelectedBlockSet.Add(block))
                {
                    continue;
                }

                m_SelectedBlocks.Add(block);
            }

            if (m_SelectedBlocks.Count == 0)
            {
                ClearTarget();
                return;
            }

            Vector3Int focusBlock = m_SelectedBlocks[0];
            Vector3Int standPos = FindNearbyStandPosition(focusBlock, world);
            bool sameSelection =
                m_SelectedTargetBlock.HasValue &&
                m_StandPosition.HasValue &&
                m_SelectedTargetBlock.Value == focusBlock &&
                m_StandPosition.Value == standPos &&
                m_SelectedAction == action &&
                m_SelectedBlocks.Count == 1;

            m_SelectedAction = action;
            m_SelectedTargetBlock = focusBlock;
            m_StandPosition = standPos;
            ShaderUtility.TargetedBlockPosition = focusBlock;
            MarkSelectionChanged();

            if (!sameSelection && invokeSingleSelectEvent && action == TargetSelectionAction.Mine)
            {
                m_OnTargetSelected?.Invoke(standPos);
                OnTargetSelectedEvent?.Invoke(standPos);
            }
        }

        private void CancelPointerSelection()
        {
            m_IsPointerSelecting = false;
            m_PointerMouseButton = -1;
            m_PointerAction = TargetSelectionAction.None;
            m_IsDragging = false;
            m_StrokeBlocks.Clear();
            m_StrokeBlockSet.Clear();
            ClearPreviewSelection();
        }

        private bool AppendStrokeBlock(Vector3Int block)
        {
            if (!m_StrokeBlockSet.Add(block))
            {
                return false;
            }

            if (m_StrokeBlocks.Count >= Mathf.Max(1, MaxSelectedBlocks))
            {
                return false;
            }

            m_StrokeBlocks.Add(block);
            return true;
        }

        private bool TryGetPointerSelectionBlock(TargetSelectionAction action, out Vector3Int block)
        {
            block = default;
            if (m_Camera == null || m_PlayerEntity == null)
            {
                return false;
            }

            IWorld world = m_PlayerEntity.World;
            if (world == null)
            {
                return false;
            }

            Ray ray = m_Camera.ScreenPointToRay(GetCursorPosition());
            if (!Physics.RaycastBlock(ray, SelectionDistance, world, m_SurfaceRaycastFilter, out BlockRaycastHit hit))
            {
                return false;
            }

            if (action == TargetSelectionAction.Mine)
            {
                block = hit.Position;
                return IsSelectableBlockForAction(world, action, block);
            }

            if (action == TargetSelectionAction.Build)
            {
                Vector3Int buildPos = (hit.Position + hit.Normal).FloorToInt();
                if (!IsSelectableBlockForAction(world, action, buildPos))
                {
                    return false;
                }

                block = buildPos;
                return true;
            }

            return false;
        }

        private void RebuildPreviewSelection(IWorld world, TargetSelectionAction action)
        {
            m_PreviewBlocks.Clear();
            m_PreviewBlockSet.Clear();
            m_PreviewTargetBlock = null;
            m_PreviewStandPosition = null;

            if (world == null)
            {
                m_SelectionVisualDirty = true;
                return;
            }

            int maxCount = Mathf.Max(1, MaxSelectedBlocks);
            for (int i = 0; i < m_StrokeBlocks.Count && m_PreviewBlocks.Count < maxCount; i++)
            {
                Vector3Int block = m_StrokeBlocks[i];
                if (!IsSelectableBlockForAction(world, action, block))
                {
                    continue;
                }

                if (!m_PreviewBlockSet.Add(block))
                {
                    continue;
                }

                m_PreviewBlocks.Add(block);
            }

            if (m_PreviewBlocks.Count > 0)
            {
                m_PreviewTargetBlock = m_PreviewBlocks[0];
                m_PreviewStandPosition = FindNearbyStandPosition(m_PreviewTargetBlock.Value, world);
            }

            m_SelectionVisualDirty = true;
        }

        private void ClearPreviewSelection()
        {
            m_PreviewTargetBlock = null;
            m_PreviewStandPosition = null;
            m_PreviewBlocks.Clear();
            m_PreviewBlockSet.Clear();
            m_SelectionVisualDirty = true;
        }

        private static bool IsSelectableBlockForAction(IWorld world, TargetSelectionAction action, Vector3Int block)
        {
            if (world == null)
            {
                return false;
            }

            var blockData = world.RWAccessor.GetBlock(block.x, block.y, block.z);
            if (blockData == null)
            {
                return false;
            }

            switch (action)
            {
                case TargetSelectionAction.Mine:
                    return blockData.ID != 0 && blockData.PhysicState == PhysicState.Solid;
                case TargetSelectionAction.Build:
                    return blockData.ID == 0;
                default:
                    return false;
            }
        }

        public bool RemoveSelectedBlock(Vector3Int block)
        {
            if (!m_SelectedBlockSet.Remove(block))
            {
                return false;
            }

            for (int i = 0; i < m_SelectedBlocks.Count; i++)
            {
                if (m_SelectedBlocks[i] == block)
                {
                    m_SelectedBlocks.RemoveAt(i);
                    break;
                }
            }

            if (m_SelectedBlocks.Count == 0)
            {
                ClearTarget();
                return true;
            }

            Vector3Int nextFocus = m_SelectedBlocks[0];
            m_SelectedTargetBlock = nextFocus;
            m_StandPosition = FindNearbyStandPosition(nextFocus, m_PlayerEntity.World);
            ShaderUtility.TargetedBlockPosition = nextFocus;
            MarkSelectionChanged();
            return true;
        }

        public void SetProcessingBlocks(ICollection<Vector3Int> processingBlocks)
        {
            bool changed;

            if (processingBlocks == null || processingBlocks.Count == 0)
            {
                changed = m_ProcessingBlocks.Count > 0;
                if (changed)
                {
                    m_ProcessingBlocks.Clear();
                }
            }
            else
            {
                changed = m_ProcessingBlocks.Count != processingBlocks.Count;
                if (!changed)
                {
                    foreach (Vector3Int block in m_ProcessingBlocks)
                    {
                        if (!processingBlocks.Contains(block))
                        {
                            changed = true;
                            break;
                        }
                    }
                }

                if (changed)
                {
                    m_ProcessingBlocks.Clear();
                    foreach (Vector3Int block in processingBlocks)
                    {
                        m_ProcessingBlocks.Add(block);
                    }
                }
            }

            if (changed)
            {
                m_SelectionVisualDirty = true;
            }
        }

        private Vector3Int FindNearbyStandPosition(Vector3Int blockPos, IWorld world)
        {
            for (int i = 0; i < s_StandSearchOffsets.Length; i++)
            {
                Vector3Int offset = s_StandSearchOffsets[i];
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
                return Minecraft.UI.CursorReticle.MousePos + new Vector2(Screen.width / 2f, Screen.height/2);
            }
            return Input.mousePosition;
        }

        private bool SelectSurfaceRaycastFilter(Configurations.BlockData block)
        {
            return block.PhysicState == PhysicState.Solid;
        }

        public void ClearTarget()
        {
            bool hadSelection = m_SelectedBlocks.Count > 0 || m_SelectedTargetBlock.HasValue;
            bool hadPointerSelection = m_IsPointerSelecting;
            if (!hadSelection && !hadPointerSelection)
            {
                return;
            }

            m_SelectedBlocks.Clear();
            m_SelectedBlockSet.Clear();
            m_SelectedAction = TargetSelectionAction.None;
            m_SelectedTargetBlock = null;
            m_StandPosition = null;
            m_ProcessingBlocks.Clear();
            CancelPointerSelection();
            HideAllSelectionMarkers();
            m_SelectionVisualDirty = true;
            if (hadSelection)
            {
                m_OnTargetCleared?.Invoke();
                OnTargetClearedEvent?.Invoke();
            }
            ShaderUtility.TargetedBlockPosition = Vector3.down;
            MarkSelectionChanged();
        }

        private void UpdateVisuals()
        {
            RefreshSelectionMarkers();

            bool hasPreviewFocus = m_IsPointerSelecting && m_PreviewTargetBlock.HasValue && m_PreviewStandPosition.HasValue;
            Vector3Int? visualTarget = hasPreviewFocus ? m_PreviewTargetBlock : m_SelectedTargetBlock;
            Vector3Int? visualStand = hasPreviewFocus ? m_PreviewStandPosition : m_StandPosition;

            if (visualTarget.HasValue && visualStand.HasValue)
            {
                if (m_TargetMarker != null)
                {
                    m_TargetMarker.SetActive(true);
                    m_TargetMarker.transform.position = new Vector3(
                        visualTarget.Value.x + 0.5f,
                        visualTarget.Value.y + 0.5f,
                        visualTarget.Value.z + 0.5f
                    );
                    UpdateTargetMarkerColor(visualTarget.Value, hasPreviewFocus);
                }

                if (m_SelectionLine != null && m_PlayerEntity != null)
                {
                    m_SelectionLine.enabled = true;
                    m_SelectionLine.SetPosition(0, m_PlayerEntity.Position + Vector3.up);
                    m_SelectionLine.SetPosition(1, new Vector3(
                        visualStand.Value.x + 0.5f,
                        visualStand.Value.y + 0.5f,
                        visualStand.Value.z + 0.5f
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

        private void MarkSelectionChanged()
        {
            m_SelectionVersion++;
            m_SelectionVisualDirty = true;
            OnSelectionChangedEvent?.Invoke();
        }

        private void RefreshSelectionMarkers()
        {
            if (!m_SelectionVisualDirty)
            {
                return;
            }

            m_SelectionVisualDirty = false;
            HideAllSelectionMarkers();

            bool usePreview = m_IsPointerSelecting && m_PreviewBlocks.Count > 0;
            IList<Vector3Int> visualBlocks = usePreview ? m_PreviewBlocks : m_SelectedBlocks;
            if (!ShowSelectionMarkers || visualBlocks.Count <= 1)
            {
                return;
            }

            for (int i = 0; i < visualBlocks.Count; i++)
            {
                Vector3Int block = visualBlocks[i];
                GameObject marker = AcquireSelectionMarker();
                marker.transform.position = new Vector3(block.x + 0.5f, block.y + 0.5f, block.z + 0.5f);
                marker.transform.localScale = Vector3.one * Mathf.Max(1.001f, SelectionMarkerScale);
                Color markerColor = !usePreview && m_ProcessingBlocks.Contains(block)
                    ? ProcessingSelectionMarkerColor
                    : SelectionMarkerColor;
                SetSelectionMarkerColor(marker, markerColor);
                marker.SetActive(true);
            }
        }

        private void UpdateTargetMarkerColor(Vector3Int targetBlock, bool isPreview)
        {
            if (m_TargetMarker == null)
            {
                return;
            }

            Renderer renderer = m_TargetMarker.GetComponent<Renderer>();
            if (renderer == null || renderer.material == null)
            {
                return;
            }

            Color targetColor = !isPreview && m_ProcessingBlocks.Contains(targetBlock)
                ? ProcessingSelectionMarkerColor
                : TargetMarkerColor;
            targetColor.a = 0.5f;
            renderer.material.color = targetColor;
        }

        private static void SetSelectionMarkerColor(GameObject marker, Color color)
        {
            if (marker == null)
            {
                return;
            }

            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer == null || renderer.material == null)
            {
                return;
            }

            renderer.material.color = color;
        }

        private GameObject AcquireSelectionMarker()
        {
            GameObject marker;
            if (m_SelectionMarkerPool.Count > 0)
            {
                marker = m_SelectionMarkerPool.Pop();
            }
            else
            {
                marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = "SelectionMarker";

                Collider col = marker.GetComponent<Collider>();
                if (col != null)
                {
                    Destroy(col);
                }

                Renderer renderer = marker.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = new Material(Shader.Find("Sprites/Default"));
                    renderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    renderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    renderer.material.SetInt("_ZWrite", 0);
                    renderer.material.DisableKeyword("_ALPHATEST_ON");
                    renderer.material.EnableKeyword("_ALPHABLEND_ON");
                    renderer.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    renderer.material.renderQueue = 3000;
                    renderer.material.color = SelectionMarkerColor;
                }

                marker.SetActive(false);
            }

            // Keep markers in world space so they do not follow the player transform.
            marker.transform.SetParent(null, true);
            if (m_RuntimeMarkerRoot == null)
            {
                EnsureRuntimeMarkerRoot();
            }
            marker.transform.SetParent(m_RuntimeMarkerRoot, true);
            m_SelectionMarkers.Add(marker);
            return marker;
        }

        private void HideAllSelectionMarkers()
        {
            for (int i = 0; i < m_SelectionMarkers.Count; i++)
            {
                GameObject marker = m_SelectionMarkers[i];
                if (marker == null)
                {
                    continue;
                }

                marker.SetActive(false);
                m_SelectionMarkerPool.Push(marker);
            }

            m_SelectionMarkers.Clear();
        }

        private void OnDisable()
        {
            ClearTarget();
        }

        private void OnDestroy()
        {
            m_SelectionMarkers.Clear();
            m_SelectionMarkerPool.Clear();

            if (m_SelectionLine != null)
            {
                Destroy(m_SelectionLine.gameObject);
                m_SelectionLine = null;
            }

            if (m_RuntimeMarkerRoot != null)
            {
                Destroy(m_RuntimeMarkerRoot.gameObject);
                m_RuntimeMarkerRoot = null;
                m_TargetMarker = null;
            }
        }

        private void EnsureRuntimeMarkerRoot()
        {
            if (m_RuntimeMarkerRoot != null)
            {
                return;
            }

            string rootName = $"SelectionMarkers_{GetInstanceID()}";
            var rootObj = new GameObject(rootName);
            Transform parent = ResolveRuntimeMarkerParent();
            if (parent != null)
            {
                rootObj.transform.SetParent(parent, false);
            }

            rootObj.transform.localPosition = Vector3.zero;
            rootObj.transform.localRotation = Quaternion.identity;
            rootObj.transform.localScale = Vector3.one;
            m_RuntimeMarkerRoot = rootObj.transform;
        }

        private static Transform ResolveRuntimeMarkerParent()
        {
            if (World.Active is World world && world != null)
            {
                return world.transform;
            }

            return null;
        }

        private void OnDrawGizmos()
        {
            if (m_SelectedTargetBlock.HasValue)
            {
                Gizmos.color = TargetMarkerColor;
                Gizmos.DrawWireCube(
                    new Vector3(
                        m_SelectedTargetBlock.Value.x + 0.5f,
                        m_SelectedTargetBlock.Value.y + 0.5f,
                        m_SelectedTargetBlock.Value.z + 0.5f
                    ),
                    Vector3.one * 1.1f
                );
            }
        }
    }
}
