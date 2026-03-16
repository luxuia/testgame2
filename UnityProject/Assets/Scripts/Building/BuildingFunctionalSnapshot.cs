using System;
using UnityEngine;

namespace Minecraft.Building
{
    [Serializable]
    public struct BuildingFunctionalSnapshot
    {
        public int ActiveBuildings;
        public int ActiveCoreCount;
        public BuildingFunctionFlags AvailableFunctions;

        public float MagicStorageBonus;
        public float HatcherySpeedBonus;
        public float RefinerySpeedBonus;
        public float DismantleYieldBonus;
        public float TurretPowerBonus;
        public float AdjutantEfficiencyBonus;

        public bool HasFunction(BuildingFunctionFlags functionFlags)
        {
            return (AvailableFunctions & functionFlags) == functionFlags;
        }

        public void Add(BuildingDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            ActiveBuildings++;
            AvailableFunctions |= definition.FunctionFlags;

            if (definition.TypeId == BuildingTypeId.FungalCore)
            {
                ActiveCoreCount++;
            }

            MagicStorageBonus += Mathf.Max(0f, definition.MagicStorageBonus);
            HatcherySpeedBonus += Mathf.Max(0f, definition.HatcherySpeedBonus);
            RefinerySpeedBonus += Mathf.Max(0f, definition.RefinerySpeedBonus);
            DismantleYieldBonus += Mathf.Max(0f, definition.DismantleYieldBonus);
            TurretPowerBonus += Mathf.Max(0f, definition.TurretPowerBonus);
            AdjutantEfficiencyBonus += Mathf.Max(0f, definition.AdjutantEfficiencyBonus);
        }
    }
}
