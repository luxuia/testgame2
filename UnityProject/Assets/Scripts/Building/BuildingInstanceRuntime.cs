using System;
using UnityEngine;

namespace Minecraft.Building
{
    [Serializable]
    public sealed class BuildingInstanceRuntime
    {
        public int RuntimeId;
        public BuildingDefinition Definition;
        public Vector3Int Anchor;
        public BuildingRuntimeState State = BuildingRuntimeState.Planned;
        public bool Dirty = true;
        public float LastEvaluateTime = -1f;
        public string LastFailureReason = "Not evaluated";

        public Vector3Int MaxExclusive
        {
            get
            {
                Vector3Int size = Definition != null ? Definition.GetClampedSize() : Vector3Int.one;
                return Anchor + size;
            }
        }

        public int FoundationY => Anchor.y > 0 ? Anchor.y - 1 : Anchor.y;

        public bool ContainsBlock(Vector3Int worldBlockPos)
        {
            Vector3Int max = MaxExclusive;
            return worldBlockPos.x >= Anchor.x && worldBlockPos.x < max.x &&
                   worldBlockPos.y >= Anchor.y && worldBlockPos.y < max.y &&
                   worldBlockPos.z >= Anchor.z && worldBlockPos.z < max.z;
        }

        public bool ContainsFoundationBlock(Vector3Int worldBlockPos)
        {
            Vector3Int max = MaxExclusive;
            return worldBlockPos.y == FoundationY &&
                   worldBlockPos.x >= Anchor.x && worldBlockPos.x < max.x &&
                   worldBlockPos.z >= Anchor.z && worldBlockPos.z < max.z;
        }
    }
}
