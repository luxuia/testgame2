using System;
using System.Collections.Generic;
using Minecraft.Combat;

namespace Minecraft.GoalPlanning
{
    public interface IGoapActionLegalityGate
    {
        bool IsLegal(GoapActionType actionType, in GoapLegalityContext context, out string denyReason);
    }

    public sealed class CombatAuthorityLegalityGate : IGoapActionLegalityGate
    {
        public bool IsLegal(GoapActionType actionType, in GoapLegalityContext context, out string denyReason)
        {
            if (actionType == GoapActionType.BreakBlock
                || actionType == GoapActionType.PlaceBlock
                || actionType == GoapActionType.ConvertToFungus)
            {
                if (!context.CombatContext.CanEditTerrain())
                {
                    denyReason = "Terrain action denied by home/away authority gate.";
                    return false;
                }
            }

            denyReason = null;
            return true;
        }
    }

}
