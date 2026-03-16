using System.Collections.Generic;
using UnityEngine;

namespace Minecraft.Building
{
    public static class BuildingDefaults
    {
        public static List<BuildingDefinition> CreateBaselineDefinitions()
        {
            return new List<BuildingDefinition>
            {
                new BuildingDefinition
                {
                    TypeId = BuildingTypeId.FungalCore,
                    InternalName = "building.fungal_core",
                    DisplayName = "Fungal Core",
                    Size = new Vector3Int(9, 7, 9),
                    FunctionFlags = BuildingFunctionFlags.CoreAuthority | BuildingFunctionFlags.BuildAreaUnlock,
                    RequiredSolidRatio = 0.22f,
                    RequireFungalFoundation = true,
                    MagicStorageBonus = 120f,
                },
                new BuildingDefinition
                {
                    TypeId = BuildingTypeId.HatcheryNest,
                    InternalName = "building.hatchery_nest",
                    DisplayName = "Hatchery Nest",
                    Size = new Vector3Int(7, 5, 7),
                    FunctionFlags = BuildingFunctionFlags.Hatchery,
                    RequiredSolidRatio = 0.18f,
                    RequireFungalFoundation = true,
                    HatcherySpeedBonus = 0.25f,
                },
                new BuildingDefinition
                {
                    TypeId = BuildingTypeId.CrystalRefinery,
                    InternalName = "building.crystal_refinery",
                    DisplayName = "Crystal Refinery",
                    Size = new Vector3Int(7, 5, 9),
                    FunctionFlags = BuildingFunctionFlags.Refinery,
                    RequiredSolidRatio = 0.20f,
                    RequireFungalFoundation = true,
                    RefinerySpeedBonus = 0.20f,
                },
                new BuildingDefinition
                {
                    TypeId = BuildingTypeId.SkillDismantler,
                    InternalName = "building.skill_dismantler",
                    DisplayName = "Skill Dismantler",
                    Size = new Vector3Int(5, 5, 5),
                    FunctionFlags = BuildingFunctionFlags.SkillDismantle,
                    RequiredSolidRatio = 0.18f,
                    RequireFungalFoundation = true,
                    DismantleYieldBonus = 0.15f,
                },
                new BuildingDefinition
                {
                    TypeId = BuildingTypeId.ResonanceTurret,
                    InternalName = "building.resonance_turret",
                    DisplayName = "Resonance Turret",
                    Size = new Vector3Int(5, 6, 5),
                    FunctionFlags = BuildingFunctionFlags.DefenseTurret,
                    RequiredSolidRatio = 0.25f,
                    RequireFungalFoundation = true,
                    TurretPowerBonus = 0.20f,
                },
                new BuildingDefinition
                {
                    TypeId = BuildingTypeId.AdjutantDormitory,
                    InternalName = "building.adjutant_dormitory",
                    DisplayName = "Adjutant Dormitory",
                    Size = new Vector3Int(9, 5, 7),
                    FunctionFlags = BuildingFunctionFlags.AdjutantAutomation,
                    RequiredSolidRatio = 0.16f,
                    RequireFungalFoundation = true,
                    AdjutantEfficiencyBonus = 0.20f,
                },
            };
        }
    }
}
