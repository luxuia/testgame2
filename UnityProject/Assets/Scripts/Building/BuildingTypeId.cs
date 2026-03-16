namespace Minecraft.Building
{
    [XLua.GCOptimize]
    [XLua.LuaCallCSharp]
    public enum BuildingTypeId : byte
    {
        Unknown = 0,
        FungalCore = 1,
        HatcheryNest = 2,
        CrystalRefinery = 3,
        SkillDismantler = 4,
        ResonanceTurret = 5,
        AdjutantDormitory = 6,
    }
}
