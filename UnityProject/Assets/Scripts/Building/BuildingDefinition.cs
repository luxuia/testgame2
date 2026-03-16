using System;
using UnityEngine;

namespace Minecraft.Building
{
    [Serializable]
    public sealed class BuildingDefinition
    {
        public BuildingTypeId TypeId = BuildingTypeId.Unknown;
        public string InternalName = "building.unknown";
        public string DisplayName = "Unknown Building";

        [Tooltip("Building footprint size in blocks (x, y, z).")]
        public Vector3Int Size = new Vector3Int(7, 5, 7);

        [Tooltip("Require completed fungal foundation under the footprint.")]
        public bool RequireFungalFoundation = true;

        [Range(0f, 1f)]
        [Tooltip("Minimum solid block ratio required inside building volume.")]
        public float RequiredSolidRatio = 0.15f;

        [Tooltip("Each listed block internal name must appear at least once in volume.")]
        public string[] RequiredMarkerBlocks = Array.Empty<string>();

        [Header("Provided Functions")]
        public BuildingFunctionFlags FunctionFlags = BuildingFunctionFlags.None;

        [Header("Functional Bonuses")]
        [Min(0f)] public float MagicStorageBonus;
        [Min(0f)] public float HatcherySpeedBonus;
        [Min(0f)] public float RefinerySpeedBonus;
        [Min(0f)] public float DismantleYieldBonus;
        [Min(0f)] public float TurretPowerBonus;
        [Min(0f)] public float AdjutantEfficiencyBonus;

        public Vector3Int GetClampedSize()
        {
            return new Vector3Int(
                Mathf.Max(1, Size.x),
                Mathf.Max(1, Size.y),
                Mathf.Max(1, Size.z));
        }

        public bool IsValid(out string error)
        {
            if (TypeId == BuildingTypeId.Unknown)
            {
                error = "Building type cannot be Unknown.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(InternalName))
            {
                error = "Building internal name is required.";
                return false;
            }

            Vector3Int clamped = GetClampedSize();
            if (clamped.x <= 0 || clamped.y <= 0 || clamped.z <= 0)
            {
                error = "Building size must be positive.";
                return false;
            }

            if (RequiredSolidRatio < 0f || RequiredSolidRatio > 1f)
            {
                error = "RequiredSolidRatio must be in [0,1].";
                return false;
            }

            error = null;
            return true;
        }

        public BuildingDefinition Clone()
        {
            BuildingDefinition copy = (BuildingDefinition)MemberwiseClone();
            copy.RequiredMarkerBlocks = RequiredMarkerBlocks == null
                ? Array.Empty<string>()
                : (string[])RequiredMarkerBlocks.Clone();
            return copy;
        }
    }
}
