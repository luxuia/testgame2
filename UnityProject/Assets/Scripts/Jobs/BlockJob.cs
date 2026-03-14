using UnityEngine;

namespace Minecraft.Jobs
{
    public enum BlockJobType
    {
        Build,
        Deconstruct
    }

    public class BlockJob
    {
        public Vector3Int Position { get; set; }
        public BlockJobType JobType { get; set; }
        public string BlockType { get; set; }
        public float WorkTimeRequired { get; set; } = 1f;
        public float WorkTimeDone { get; set; }
        public bool IsBeingWorked { get; set; }
        public bool IsCompleted { get; set; }

        public bool IsAdjacentTo(Vector3 pos)
        {
            Vector3 p = new Vector3(Position.x + 0.5f, Position.y + 0.5f, Position.z + 0.5f);
            float dist = Vector3.Distance(pos, p);
            return dist <= 1.5f;
        }
    }
}
