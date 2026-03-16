using System;
using System.Collections.Generic;
using Minecraft.Configurations;
using Minecraft.PhysicSystem;
using UnityEngine;

namespace Minecraft.Building
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1200)]
    public sealed class BuildingRuntimeService : MonoBehaviour
    {
        private static BuildingRuntimeService s_Instance;

        [Header("Catalog")]
        [SerializeField] private bool m_UseBuiltInDefaultsWhenEmpty = true;
        [SerializeField] private List<BuildingDefinition> m_DefaultDefinitions = new List<BuildingDefinition>();

        [Header("Runtime Validation")]
        [Min(0.05f)] public float RevalidateInterval = 0.20f;
        [Range(1, 256)] public int MaxRevalidatePerTick = 24;

        private readonly Dictionary<BuildingTypeId, BuildingDefinition> m_DefinitionByType =
            new Dictionary<BuildingTypeId, BuildingDefinition>();
        private readonly Dictionary<int, BuildingInstanceRuntime> m_InstancesById =
            new Dictionary<int, BuildingInstanceRuntime>();
        private readonly Queue<int> m_DirtyQueue = new Queue<int>(64);
        private readonly HashSet<int> m_DirtySet = new HashSet<int>();

        private int m_NextRuntimeId = 1;
        private float m_NextRevalidateTime;
        private BuildingFunctionalSnapshot m_CurrentSnapshot;

        public static BuildingRuntimeService Instance => s_Instance;
        public static bool HasInstance => s_Instance != null;

        public BuildingFunctionalSnapshot CurrentSnapshot => m_CurrentSnapshot;

        public event Action<BuildingInstanceRuntime> OnBuildingStateChanged;
        public event Action<BuildingFunctionalSnapshot> OnSnapshotChanged;

        public static void EnsureExists(Transform parent = null)
        {
            if (s_Instance != null)
            {
                return;
            }

            GameObject go = new GameObject("Building Runtime Service");
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            s_Instance = go.AddComponent<BuildingRuntimeService>();
        }

        public static void NotifyBlockChanged(Vector3Int worldBlockPos)
        {
            if (s_Instance == null)
            {
                return;
            }

            s_Instance.HandleBlockChanged(worldBlockPos);
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            BootstrapDefinitionCatalog();
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }
        }

        private void Update()
        {
            if (Time.time < m_NextRevalidateTime)
            {
                return;
            }

            m_NextRevalidateTime = Time.time + Mathf.Max(0.05f, RevalidateInterval);
            RevalidateDirtyInstances(Mathf.Max(1, MaxRevalidatePerTick));
        }

        public ICollection<BuildingInstanceRuntime> GetAllInstances()
        {
            return m_InstancesById.Values;
        }

        public bool TryGetDefinition(BuildingTypeId typeId, out BuildingDefinition definition)
        {
            return m_DefinitionByType.TryGetValue(typeId, out definition);
        }

        public void RegisterDefinition(BuildingDefinition definition, bool overwrite = true)
        {
            if (definition == null)
            {
                return;
            }

            if (!definition.IsValid(out _))
            {
                return;
            }

            if (!overwrite && m_DefinitionByType.ContainsKey(definition.TypeId))
            {
                return;
            }

            m_DefinitionByType[definition.TypeId] = definition.Clone();
        }

        public bool TryPlaceBuilding(BuildingTypeId typeId, Vector3Int anchor, out BuildingInstanceRuntime runtime, out string error)
        {
            runtime = null;
            if (!TryGetDefinition(typeId, out BuildingDefinition definition))
            {
                error = $"Unknown building type: {typeId}";
                return false;
            }

            return TryPlaceBuilding(definition, anchor, out runtime, out error);
        }

        public bool TryPlaceBuilding(BuildingDefinition definition, Vector3Int anchor, out BuildingInstanceRuntime runtime, out string error)
        {
            runtime = null;

            if (definition == null)
            {
                error = "Building definition is null.";
                return false;
            }

            if (!definition.IsValid(out error))
            {
                return false;
            }

            Vector3Int groundedAnchor = ResolveGroundedAnchor(definition, anchor);
            if (!TryValidatePlacement(definition, groundedAnchor, out error))
            {
                return false;
            }

            runtime = new BuildingInstanceRuntime
            {
                RuntimeId = m_NextRuntimeId++,
                Definition = definition.Clone(),
                Anchor = groundedAnchor,
                State = BuildingRuntimeState.Planned,
                Dirty = true,
                LastFailureReason = "Pending validation",
                LastEvaluateTime = -1f,
            };

            m_InstancesById.Add(runtime.RuntimeId, runtime);
            MarkDirty(runtime.RuntimeId);
            RevalidateDirtyInstances(1);
            error = null;
            return true;
        }

        private static Vector3Int ResolveGroundedAnchor(BuildingDefinition definition, Vector3Int requestedAnchor)
        {
            IWorld world = World.Active;
            if (definition == null || world == null || !world.Initialized || world.RWAccessor == null)
            {
                return requestedAnchor;
            }

            Vector3Int size = definition.GetClampedSize();
            int highestTopY = int.MinValue;
            bool foundAny = false;

            for (int dx = 0; dx < size.x; dx++)
            {
                for (int dz = 0; dz < size.z; dz++)
                {
                    int worldX = requestedAnchor.x + dx;
                    int worldZ = requestedAnchor.z + dz;
                    int topY = world.RWAccessor.GetTopVisibleBlockY(worldX, worldZ, int.MinValue);
                    if (topY == int.MinValue)
                    {
                        continue;
                    }

                    if (!foundAny || topY > highestTopY)
                    {
                        highestTopY = topY;
                        foundAny = true;
                    }
                }
            }

            if (!foundAny)
            {
                return requestedAnchor;
            }

            int groundedY = highestTopY + 1;
            if (groundedY >= requestedAnchor.y)
            {
                return requestedAnchor;
            }

            requestedAnchor.y = groundedY;
            return requestedAnchor;
        }

        public bool RemoveBuilding(int runtimeId)
        {
            if (!m_InstancesById.TryGetValue(runtimeId, out BuildingInstanceRuntime runtime))
            {
                return false;
            }

            runtime.State = BuildingRuntimeState.Destroyed;
            m_InstancesById.Remove(runtimeId);
            m_DirtySet.Remove(runtimeId);
            RebuildSnapshot();
            OnBuildingStateChanged?.Invoke(runtime);
            return true;
        }

        public bool TryGetInstance(int runtimeId, out BuildingInstanceRuntime runtime)
        {
            return m_InstancesById.TryGetValue(runtimeId, out runtime);
        }

        public bool HasFunction(BuildingFunctionFlags functionFlags)
        {
            return m_CurrentSnapshot.HasFunction(functionFlags);
        }

        public void ForceRevalidateAll()
        {
            foreach (KeyValuePair<int, BuildingInstanceRuntime> pair in m_InstancesById)
            {
                MarkDirty(pair.Key);
            }

            RevalidateDirtyInstances(Mathf.Max(1, m_InstancesById.Count));
        }

        private void BootstrapDefinitionCatalog()
        {
            m_DefinitionByType.Clear();

            if (m_DefaultDefinitions == null)
            {
                m_DefaultDefinitions = new List<BuildingDefinition>();
            }

            if (m_UseBuiltInDefaultsWhenEmpty && m_DefaultDefinitions.Count == 0)
            {
                m_DefaultDefinitions.AddRange(BuildingDefaults.CreateBaselineDefinitions());
            }

            for (int i = 0; i < m_DefaultDefinitions.Count; i++)
            {
                RegisterDefinition(m_DefaultDefinitions[i], overwrite: true);
            }
        }

        private bool TryValidatePlacement(BuildingDefinition definition, Vector3Int anchor, out string error)
        {
            IWorld world = World.Active;
            if (world == null || !world.Initialized)
            {
                error = "World is not initialized.";
                return false;
            }

            Vector3Int size = definition.GetClampedSize();
            foreach (KeyValuePair<int, BuildingInstanceRuntime> pair in m_InstancesById)
            {
                BuildingInstanceRuntime existing = pair.Value;
                if (existing == null || existing.Definition == null || existing.State == BuildingRuntimeState.Destroyed)
                {
                    continue;
                }

                if (VolumesOverlap(anchor, size, existing.Anchor, existing.Definition.GetClampedSize()))
                {
                    error = $"Placement overlaps with building runtime #{existing.RuntimeId}.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool VolumesOverlap(Vector3Int aMin, Vector3Int aSize, Vector3Int bMin, Vector3Int bSize)
        {
            Vector3Int aMax = aMin + aSize;
            Vector3Int bMax = bMin + bSize;
            return aMin.x < bMax.x && aMax.x > bMin.x &&
                   aMin.y < bMax.y && aMax.y > bMin.y &&
                   aMin.z < bMax.z && aMax.z > bMin.z;
        }

        private void HandleBlockChanged(Vector3Int worldBlockPos)
        {
            foreach (KeyValuePair<int, BuildingInstanceRuntime> pair in m_InstancesById)
            {
                BuildingInstanceRuntime runtime = pair.Value;
                if (runtime == null || runtime.Definition == null || runtime.State == BuildingRuntimeState.Destroyed)
                {
                    continue;
                }

                if (runtime.ContainsBlock(worldBlockPos) || runtime.ContainsFoundationBlock(worldBlockPos))
                {
                    MarkDirty(runtime.RuntimeId);
                }
            }
        }

        private void MarkDirty(int runtimeId)
        {
            if (!m_InstancesById.TryGetValue(runtimeId, out BuildingInstanceRuntime runtime))
            {
                return;
            }

            if (!m_DirtySet.Add(runtimeId))
            {
                return;
            }

            runtime.Dirty = true;
            m_DirtyQueue.Enqueue(runtimeId);
        }

        private void RevalidateDirtyInstances(int budget)
        {
            int workBudget = Mathf.Max(1, budget);
            bool changed = false;

            while (workBudget-- > 0 && m_DirtyQueue.Count > 0)
            {
                int runtimeId = m_DirtyQueue.Dequeue();
                m_DirtySet.Remove(runtimeId);

                if (!m_InstancesById.TryGetValue(runtimeId, out BuildingInstanceRuntime runtime))
                {
                    continue;
                }

                runtime.Dirty = false;
                runtime.LastEvaluateTime = Time.time;

                bool operational = EvaluateOperational(runtime, out string reason);
                BuildingRuntimeState nextState = operational ? BuildingRuntimeState.Active : BuildingRuntimeState.Suspended;

                bool stateChanged = runtime.State != nextState;
                bool reasonChanged = !string.Equals(runtime.LastFailureReason, reason, StringComparison.Ordinal);

                runtime.State = nextState;
                runtime.LastFailureReason = reason;

                if (stateChanged || reasonChanged)
                {
                    changed = true;
                    OnBuildingStateChanged?.Invoke(runtime);
                }
            }

            if (changed)
            {
                RebuildSnapshot();
            }
        }

        private bool EvaluateOperational(BuildingInstanceRuntime runtime, out string failureReason)
        {
            IWorld world = World.Active;
            if (world == null || !world.Initialized)
            {
                failureReason = "World is not initialized.";
                return false;
            }

            BuildingDefinition definition = runtime.Definition;
            if (definition == null)
            {
                failureReason = "Missing building definition.";
                return false;
            }

            if (!definition.IsValid(out failureReason))
            {
                return false;
            }

            Vector3Int size = definition.GetClampedSize();
            int totalVolume = size.x * size.y * size.z;
            int solidCount = 0;

            HashSet<string> missingMarkers = BuildMissingMarkerSet(definition.RequiredMarkerBlocks);

            Vector3Int min = runtime.Anchor;
            Vector3Int max = runtime.MaxExclusive;
            for (int x = min.x; x < max.x; x++)
            {
                for (int y = min.y; y < max.y; y++)
                {
                    for (int z = min.z; z < max.z; z++)
                    {
                        BlockData block = world.RWAccessor.GetBlock(x, y, z);
                        if (block == null)
                        {
                            continue;
                        }

                        if (block.PhysicState == PhysicState.Solid && !block.HasFlag(BlockFlags.AlwaysInvisible))
                        {
                            solidCount++;
                        }

                        if (missingMarkers != null && !string.IsNullOrWhiteSpace(block.InternalName))
                        {
                            missingMarkers.Remove(block.InternalName.Trim());
                        }
                    }
                }
            }

            float minSolidRatio = Mathf.Clamp01(definition.RequiredSolidRatio);
            float currentSolidRatio = totalVolume > 0 ? (float)solidCount / totalVolume : 0f;
            if (currentSolidRatio + 0.0001f < minSolidRatio)
            {
                failureReason = $"Solid ratio too low ({currentSolidRatio:0.00} < {minSolidRatio:0.00}).";
                return false;
            }

            if (missingMarkers != null && missingMarkers.Count > 0)
            {
                failureReason = $"Missing marker blocks: {string.Join(", ", missingMarkers)}.";
                return false;
            }

            if (definition.RequireFungalFoundation)
            {
                if (!HasCompletedFungalFoundation(runtime))
                {
                    failureReason = "Fungal foundation is incomplete.";
                    return false;
                }
            }

            failureReason = null;
            return true;
        }

        private static HashSet<string> BuildMissingMarkerSet(string[] requiredMarkerBlocks)
        {
            if (requiredMarkerBlocks == null || requiredMarkerBlocks.Length == 0)
            {
                return null;
            }

            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < requiredMarkerBlocks.Length; i++)
            {
                string marker = requiredMarkerBlocks[i];
                if (string.IsNullOrWhiteSpace(marker))
                {
                    continue;
                }

                result.Add(marker.Trim());
            }

            return result.Count == 0 ? null : result;
        }

        private static bool HasCompletedFungalFoundation(BuildingInstanceRuntime runtime)
        {
            Vector3Int min = runtime.Anchor;
            Vector3Int max = runtime.MaxExclusive;
            int foundationY = runtime.FoundationY;

            for (int x = min.x; x < max.x; x++)
            {
                for (int z = min.z; z < max.z; z++)
                {
                    Vector3Int foundationPos = new Vector3Int(x, foundationY, z);
                    if (!FungalCarpetSystem.IsCompletedAtBlockPosition(foundationPos))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void RebuildSnapshot()
        {
            BuildingFunctionalSnapshot next = default;
            foreach (KeyValuePair<int, BuildingInstanceRuntime> pair in m_InstancesById)
            {
                BuildingInstanceRuntime runtime = pair.Value;
                if (runtime == null || runtime.State != BuildingRuntimeState.Active)
                {
                    continue;
                }

                next.Add(runtime.Definition);
            }

            m_CurrentSnapshot = next;
            OnSnapshotChanged?.Invoke(m_CurrentSnapshot);
        }
    }
}
