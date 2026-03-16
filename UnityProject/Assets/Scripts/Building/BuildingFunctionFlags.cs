using System;

namespace Minecraft.Building
{
    [Flags]
    [XLua.GCOptimize]
    [XLua.LuaCallCSharp]
    public enum BuildingFunctionFlags
    {
        None = 0,
        CoreAuthority = 1 << 0,
        BuildAreaUnlock = 1 << 1,
        Hatchery = 1 << 2,
        Refinery = 1 << 3,
        SkillDismantle = 1 << 4,
        DefenseTurret = 1 << 5,
        AdjutantAutomation = 1 << 6,
    }
}
