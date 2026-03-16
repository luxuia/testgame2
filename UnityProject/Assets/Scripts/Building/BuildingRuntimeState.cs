namespace Minecraft.Building
{
    [XLua.GCOptimize]
    [XLua.LuaCallCSharp]
    public enum BuildingRuntimeState : byte
    {
        Planned = 0,
        Active = 1,
        Suspended = 2,
        Destroyed = 3,
    }
}
