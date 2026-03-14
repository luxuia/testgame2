using UnityEngine;

namespace Minecraft.BuildMode
{
    /// <summary>
    /// 3D AABB selection parameters from drag (box select).
    /// </summary>
    public readonly struct DragParams
    {
        public int MinX { get; }
        public int MinY { get; }
        public int MinZ { get; }
        public int MaxX { get; }
        public int MaxY { get; }
        public int MaxZ { get; }

        public int BlockCount => (MaxX - MinX + 1) * (MaxY - MinY + 1) * (MaxZ - MinZ + 1);

        public DragParams(Vector3Int start, Vector3Int end)
        {
            MinX = Mathf.Min(start.x, end.x);
            MinY = Mathf.Min(start.y, end.y);
            MinZ = Mathf.Min(start.z, end.z);
            MaxX = Mathf.Max(start.x, end.x);
            MaxY = Mathf.Max(start.y, end.y);
            MaxZ = Mathf.Max(start.z, end.z);
        }

        public DragParams(Vector3Int single)
        {
            MinX = MaxX = single.x;
            MinY = MaxY = single.y;
            MinZ = MaxZ = single.z;
        }

        public bool Contains(int x, int y, int z)
        {
            return x >= MinX && x <= MaxX && y >= MinY && y <= MaxY && z >= MinZ && z <= MaxZ;
        }
    }
}
