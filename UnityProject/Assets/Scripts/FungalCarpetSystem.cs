using System.Collections.Generic;
using Minecraft.Configurations;
using Minecraft.PhysicSystem;
using Minecraft.Rendering;
using UnityEngine;

namespace Minecraft
{
    [DisallowMultipleComponent]
    public class FungalCarpetSystem : MonoBehaviour
    {
        public enum FungalState : byte
        {
            None = 0,
            Infecting = 1,
            Damaged = 2,
            Completed = 3,
        }

        private struct CellState
        {
            public FungalState State;
            public float StateTime;
            public float LastTouchedTime;
        }

        private const byte EncodedNone = 0;
        private const byte EncodedInfecting = 85;
        private const byte EncodedDamaged = 170;
        private const byte EncodedCompleted = 255;

        private static FungalCarpetSystem s_Instance;

        [Header("State Timing")]
        [Min(0.05f)] public float InfectionToCompleteSeconds = 1.5f;
        [Min(0.05f)] public float DamagedToCompleteSeconds = 2.0f;

        [Header("State Map")]
        [Range(32, 512)] public int StateMapSize = 192;
        [Range(24, 256)] public int StateMapHalfRange = 96;
        [Min(0.02f)] public float StateMapUpdateInterval = 0.08f;

        [Header("Overlay")]
        [SerializeField] private Texture2D m_OverlayTexture;
        [Range(0f, 1f)] public float OverlayStrength = 0.72f;
        public Color InfectingColor = new Color(0.32f, 0.80f, 0.56f, 0.72f);
        public Color DamagedColor = new Color(0.78f, 0.27f, 0.22f, 0.76f);
        public Color CompletedColor = new Color(0.18f, 0.78f, 0.52f, 0.80f);

        [Header("Auto Infection")]
        public bool AutoInfectFromWorldPlayer = true;
        [Min(0.02f)] public float AutoInfectInterval = 0.08f;
        [Min(0f)] public float AutoInfectMinMoveDistance = 0.05f;

        [Header("Cleanup")]
        [Range(128, 2048)] public int MaxTrackedDistance = 768;
        [Min(1f)] public float CompletedCellLifetime = 120f;

        private readonly Dictionary<Vector3Int, CellState> m_CellStates = new Dictionary<Vector3Int, CellState>(2048);
        private readonly List<Vector3Int> m_RemoveBuffer = new List<Vector3Int>(512);
        private readonly List<Vector3Int> m_KeysBuffer = new List<Vector3Int>(2048);

        private Texture2D m_StateMapTexture;
        private Color32[] m_StatePixels;
        private Texture2D m_RuntimeFallbackOverlay;
        private Vector2Int m_MapOriginXZ;
        private float m_NextStateMapUpdateTime;
        private float m_NextAutoInfectTime;
        private Vector3 m_LastAutoInfectPosition;
        private bool m_HasAutoInfectPosition;

        public static void EnsureExists(Transform parent = null)
        {
            if (s_Instance != null)
            {
                return;
            }

            GameObject go = new GameObject("Fungal Carpet System");
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            s_Instance = go.AddComponent<FungalCarpetSystem>();
        }

        public static bool TryInfectAtWorldPosition(Vector3 worldPosition)
        {
            if (s_Instance == null)
            {
                EnsureExists();
            }

            return s_Instance != null && s_Instance.TryInfectAtWorldPositionInternal(worldPosition);
        }

        public static void NotifyBlockChanged(Vector3Int worldBlockPos)
        {
            if (s_Instance == null)
            {
                return;
            }

            if (!s_Instance.m_CellStates.TryGetValue(worldBlockPos, out CellState state))
            {
                return;
            }

            state.State = FungalState.Damaged;
            state.StateTime = 0f;
            state.LastTouchedTime = Time.time;
            s_Instance.m_CellStates[worldBlockPos] = state;
        }

        public static bool TryGetStateAtBlockPosition(Vector3Int worldBlockPos, out FungalState state)
        {
            if (s_Instance == null)
            {
                state = FungalState.None;
                return false;
            }

            return s_Instance.TryGetStateAtWorldBlockInternal(worldBlockPos, out state);
        }

        public static bool IsCompletedAtBlockPosition(Vector3Int worldBlockPos)
        {
            return TryGetStateAtBlockPosition(worldBlockPos, out FungalState state) && state == FungalState.Completed;
        }

        public static bool TryGetStateAtWorldPosition(Vector3 worldPosition, out FungalState state)
        {
            state = FungalState.None;

            IWorld world = World.Active;
            if (world == null || !world.Initialized)
            {
                return false;
            }

            if (!TryResolveGroundBlock(world, worldPosition, out Vector3Int groundPos))
            {
                return false;
            }

            return TryGetStateAtBlockPosition(groundPos, out state);
        }

        public static bool IsCompletedAtWorldPosition(Vector3 worldPosition)
        {
            return TryGetStateAtWorldPosition(worldPosition, out FungalState state) && state == FungalState.Completed;
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            InitializeTextures();
            PushShaderGlobals();
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }

            if (m_StateMapTexture != null)
            {
                Destroy(m_StateMapTexture);
                m_StateMapTexture = null;
            }

            if (m_RuntimeFallbackOverlay != null)
            {
                Destroy(m_RuntimeFallbackOverlay);
                m_RuntimeFallbackOverlay = null;
            }
        }

        private void Update()
        {
            TryAutoInfectFromWorldPlayer();
            UpdateStateTransitions(Time.deltaTime);

            if (Time.time < m_NextStateMapUpdateTime)
            {
                return;
            }

            m_NextStateMapUpdateTime = Time.time + Mathf.Max(0.02f, StateMapUpdateInterval);
            UpdateStateMapTexture();
            PushShaderGlobals();
        }

        private void InitializeTextures()
        {
            int mapSize = Mathf.Clamp(StateMapSize, 32, 512);
            // Keep this texture in linear space so encoded state values are not altered by sRGB conversion.
            m_StateMapTexture = new Texture2D(mapSize, mapSize, TextureFormat.RGBA32, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                name = "FungalStateMap_Runtime"
            };

            m_StatePixels = new Color32[mapSize * mapSize];
            ClearStateMapPixels();
            m_StateMapTexture.SetPixels32(m_StatePixels);
            m_StateMapTexture.Apply(false, false);

            if (m_OverlayTexture == null)
            {
                m_RuntimeFallbackOverlay = CreateFallbackOverlayTexture();
            }
        }

        private Texture2D CreateFallbackOverlayTexture()
        {
            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point,
                name = "FungalOverlayFallback_Runtime"
            };

            Color32[] pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                bool bright = ((i + (i / 4)) & 1) == 0;
                byte value = bright ? (byte)255 : (byte)175;
                pixels[i] = new Color32(value, value, value, 255);
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private void PushShaderGlobals()
        {
            ShaderUtility.FungalStateMap = m_StateMapTexture;
            ShaderUtility.FungalOverlayTexture = m_OverlayTexture != null ? m_OverlayTexture : m_RuntimeFallbackOverlay;
            ShaderUtility.FungalMapOriginXZ = new Vector2(m_MapOriginXZ.x, m_MapOriginXZ.y);
            ShaderUtility.FungalMapSize = m_StateMapTexture != null ? m_StateMapTexture.width : 0f;
            ShaderUtility.FungalOverlayStrength = OverlayStrength * 0.62f;
            ShaderUtility.FungalStateColorInfecting = ToneColor(WithMinAlpha(InfectingColor, 0.50f), 0.90f, 0.88f);
            ShaderUtility.FungalStateColorDamaged = ToneColor(WithMinAlpha(DamagedColor, 0.55f), 0.90f, 0.90f);
            ShaderUtility.FungalStateColorCompleted = ToneColor(WithMinAlpha(CompletedColor, 0.55f), 0.86f, 0.86f);
        }

        private static Color WithMinAlpha(Color color, float minAlpha)
        {
            color.a = Mathf.Max(minAlpha, color.a);
            return color;
        }

        private static Color ToneColor(Color color, float rgbScale, float alphaScale)
        {
            color.r *= rgbScale;
            color.g *= rgbScale;
            color.b *= rgbScale;
            color.a *= alphaScale;
            return color;
        }

        private void TryAutoInfectFromWorldPlayer()
        {
            if (!AutoInfectFromWorldPlayer)
            {
                return;
            }

            if (Time.time < m_NextAutoInfectTime)
            {
                return;
            }

            IWorld world = World.Active;
            if (world == null || !world.Initialized)
            {
                return;
            }

            Transform focusTransform = world.PlayerTransform != null
                ? world.PlayerTransform
                : world.MainCamera != null ? world.MainCamera.transform : null;
            if (focusTransform == null)
            {
                return;
            }

            Vector3 playerPosition = focusTransform.position;

            if (m_HasAutoInfectPosition)
            {
                Vector2 delta = new Vector2(
                    playerPosition.x - m_LastAutoInfectPosition.x,
                    playerPosition.z - m_LastAutoInfectPosition.z);
                float minMove = Mathf.Max(0f, AutoInfectMinMoveDistance);
                if (delta.sqrMagnitude < minMove * minMove)
                {
                    return;
                }
            }

            if (!TryInfectAtWorldPositionInternal(playerPosition))
            {
                return;
            }

            m_HasAutoInfectPosition = true;
            m_LastAutoInfectPosition = playerPosition;
            m_NextAutoInfectTime = Time.time + Mathf.Max(0.02f, AutoInfectInterval);
        }

        private bool TryInfectAtWorldPositionInternal(Vector3 worldPosition)
        {
            IWorld world = World.Active;
            if (world == null || !world.Initialized)
            {
                return false;
            }

            if (!TryResolveGroundBlock(world, worldPosition, out Vector3Int groundPos))
            {
                return false;
            }

            if (!m_CellStates.TryGetValue(groundPos, out CellState cell))
            {
                cell = new CellState
                {
                    State = FungalState.Infecting,
                    StateTime = 0f,
                    LastTouchedTime = Time.time,
                };
                m_CellStates.Add(groundPos, cell);
                return true;
            }

            cell.LastTouchedTime = Time.time;
            switch (cell.State)
            {
                case FungalState.Damaged:
                case FungalState.None:
                    cell.State = FungalState.Infecting;
                    cell.StateTime = 0f;
                    break;
                case FungalState.Infecting:
                    // Continuous stepping slightly accelerates infection completion.
                    cell.StateTime = Mathf.Min(cell.StateTime + 0.12f, InfectionToCompleteSeconds);
                    break;
                case FungalState.Completed:
                    break;
            }

            m_CellStates[groundPos] = cell;
            return true;
        }

        private bool TryGetStateAtWorldBlockInternal(Vector3Int worldBlockPos, out FungalState state)
        {
            if (m_CellStates.TryGetValue(worldBlockPos, out CellState cell))
            {
                state = cell.State;
                return true;
            }

            state = FungalState.None;
            return false;
        }

        private static bool TryResolveGroundBlock(IWorld world, Vector3 worldPosition, out Vector3Int groundPos)
        {
            int x = Mathf.FloorToInt(worldPosition.x);
            int z = Mathf.FloorToInt(worldPosition.z);
            int y = Mathf.FloorToInt(worldPosition.y - 0.1f);

            int topVisibleY = world.RWAccessor.GetTopVisibleBlockY(x, z, -1);
            if (topVisibleY >= 0)
            {
                BlockData top = world.RWAccessor.GetBlock(x, topVisibleY, z);
                if (top != null && top.PhysicState == PhysicState.Solid)
                {
                    groundPos = new Vector3Int(x, topVisibleY, z);
                    return true;
                }
            }

            int maxDepth = 24;
            for (int i = -2; i < maxDepth; i++)
            {
                int checkY = y - i;
                if (checkY < 0)
                {
                    continue;
                }

                BlockData block = world.RWAccessor.GetBlock(x, checkY, z);
                if (block != null && block.PhysicState == PhysicState.Solid)
                {
                    groundPos = new Vector3Int(x, checkY, z);
                    return true;
                }
            }

            groundPos = default;
            return false;
        }

        private void UpdateStateTransitions(float deltaTime)
        {
            if (m_CellStates.Count == 0)
            {
                return;
            }

            m_KeysBuffer.Clear();
            foreach (Vector3Int key in m_CellStates.Keys)
            {
                m_KeysBuffer.Add(key);
            }

            float now = Time.time;
            float completeTimeout = Mathf.Max(5f, CompletedCellLifetime);

            for (int i = 0; i < m_KeysBuffer.Count; i++)
            {
                Vector3Int key = m_KeysBuffer[i];
                CellState state = m_CellStates[key];
                state.StateTime += deltaTime;

                if (state.State == FungalState.Infecting && state.StateTime >= InfectionToCompleteSeconds)
                {
                    state.State = FungalState.Completed;
                    state.StateTime = 0f;
                    m_CellStates[key] = state;
                    continue;
                }

                if (state.State == FungalState.Damaged && state.StateTime >= DamagedToCompleteSeconds)
                {
                    state.State = FungalState.Completed;
                    state.StateTime = 0f;
                    m_CellStates[key] = state;
                    continue;
                }

                if (state.State == FungalState.Completed && now - state.LastTouchedTime > completeTimeout)
                {
                    m_RemoveBuffer.Add(key);
                    continue;
                }

                m_CellStates[key] = state;
            }

            if (m_RemoveBuffer.Count > 0)
            {
                for (int i = 0; i < m_RemoveBuffer.Count; i++)
                {
                    m_CellStates.Remove(m_RemoveBuffer[i]);
                }
                m_RemoveBuffer.Clear();
            }
        }

        private void UpdateStateMapTexture()
        {
            if (m_StateMapTexture == null || m_StatePixels == null)
            {
                return;
            }

            IWorld world = World.Active;
            if (world == null || !world.Initialized)
            {
                ClearStateMapPixels();
                m_StateMapTexture.SetPixels32(m_StatePixels);
                m_StateMapTexture.Apply(false, false);
                return;
            }

            Transform focusTransform = world.PlayerTransform != null
                ? world.PlayerTransform
                : world.MainCamera != null ? world.MainCamera.transform : null;
            if (focusTransform == null)
            {
                ClearStateMapPixels();
                m_StateMapTexture.SetPixels32(m_StatePixels);
                m_StateMapTexture.Apply(false, false);
                return;
            }

            Vector3 player = focusTransform.position;
            int mapSize = m_StateMapTexture.width;
            int halfRange = Mathf.Clamp(StateMapHalfRange, 8, mapSize / 2);
            m_MapOriginXZ = new Vector2Int(
                Mathf.FloorToInt(player.x) - halfRange,
                Mathf.FloorToInt(player.z) - halfRange);

            int maxDistanceSq = Mathf.Max(64, MaxTrackedDistance);
            maxDistanceSq *= maxDistanceSq;

            ClearStateMapPixels();
            m_KeysBuffer.Clear();
            foreach (Vector3Int key in m_CellStates.Keys)
            {
                m_KeysBuffer.Add(key);
            }

            for (int i = 0; i < m_KeysBuffer.Count; i++)
            {
                Vector3Int pos = m_KeysBuffer[i];

                int dxPlayer = pos.x - Mathf.FloorToInt(player.x);
                int dzPlayer = pos.z - Mathf.FloorToInt(player.z);
                if (dxPlayer * dxPlayer + dzPlayer * dzPlayer > maxDistanceSq)
                {
                    m_RemoveBuffer.Add(pos);
                    continue;
                }

                int tx = pos.x - m_MapOriginXZ.x;
                int tz = pos.z - m_MapOriginXZ.y;
                if (tx < 0 || tz < 0 || tx >= mapSize || tz >= mapSize)
                {
                    continue;
                }

                if (!m_CellStates.TryGetValue(pos, out CellState state))
                {
                    continue;
                }

                byte encoded = EncodeState(state.State);
                int index = tz * mapSize + tx;
                m_StatePixels[index] = new Color32(encoded, 0, 0, 255);
            }

            if (m_RemoveBuffer.Count > 0)
            {
                for (int i = 0; i < m_RemoveBuffer.Count; i++)
                {
                    m_CellStates.Remove(m_RemoveBuffer[i]);
                }
                m_RemoveBuffer.Clear();
            }

            m_StateMapTexture.SetPixels32(m_StatePixels);
            m_StateMapTexture.Apply(false, false);
        }

        private void ClearStateMapPixels()
        {
            for (int i = 0; i < m_StatePixels.Length; i++)
            {
                m_StatePixels[i] = new Color32(EncodedNone, 0, 0, 255);
            }
        }

        private static byte EncodeState(FungalState state)
        {
            return state switch
            {
                FungalState.Infecting => EncodedInfecting,
                FungalState.Damaged => EncodedDamaged,
                FungalState.Completed => EncodedCompleted,
                _ => EncodedNone,
            };
        }
    }
}
