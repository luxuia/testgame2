using System;
using Minecraft.Configurations;
using Minecraft.Jobs;
using Minecraft.PhysicSystem;
using UnityEngine;
using UnityEngine.UI;
using static Minecraft.WorldConsts;
using Physics = Minecraft.PhysicSystem.Physics;

namespace Minecraft.BuildMode
{
    [DisallowMultipleComponent]
    public class BuildModeController : MonoBehaviour
    {
        [SerializeField] private float m_RaycastMaxDistance = 64f;
        [SerializeField] private Text m_BuildModeHintText;

        [NonSerialized] private Camera m_Camera;
        [NonSerialized] private IWorld m_World;

        public BuildMode CurrentMode { get; private set; } = BuildMode.Deconstruct;
        public string ConstructBlockType { get; private set; } = "stone";
        public bool BuildModeActive { get; private set; }

        public void Initialize(Camera camera, IWorld world)
        {
            m_Camera = camera;
            m_World = world;
        }

        public void SetMode(BuildMode mode, string blockType = null)
        {
            CurrentMode = mode;
            if (!string.IsNullOrEmpty(blockType))
            {
                ConstructBlockType = blockType;
            }
            BuildModeActive = true;
            UpdateHintText();
        }

        public void ClearBuildMode()
        {
            BuildModeActive = false;
            UpdateHintText();
        }

        private void UpdateHintText()
        {
            if (m_BuildModeHintText == null) return;
            m_BuildModeHintText.gameObject.SetActive(BuildModeActive);
            string modeName = CurrentMode switch { BuildMode.Construct => "建造", BuildMode.Deconstruct => "分解", BuildMode.Mine => "采集", _ => CurrentMode.ToString() };
            m_BuildModeHintText.text = BuildModeActive ? $"建造模式 - {modeName} (B 退出)" : "";
        }

        /// <summary>
        /// Raycast from screen position to get block position. Returns false if no hit.
        /// </summary>
        public bool GetBlockAtScreenPosition(Vector2 screenPos, out Vector3Int blockPos)
        {
            blockPos = Vector3Int.zero;
            if (m_Camera == null || m_World == null) return false;

            Ray ray = m_Camera.ScreenPointToRay(screenPos);
            if (Physics.RaycastBlock(ray, m_RaycastMaxDistance, m_World, SelectForRaycast, out BlockRaycastHit hit))
            {
                blockPos = hit.Position;
                return true;
            }
            return false;
        }

        private static bool SelectForRaycast(BlockData block)
        {
            return block != null && block.PhysicState == PhysicState.Solid && !block.HasFlag(BlockFlags.IgnoreDestroyBlockRaycast);
        }

        /// <summary>
        /// Called when drag finishes. Processes all blocks in the selection.
        /// </summary>
        public void HandleDragFinished(DragParams dragParams)
        {
            if (m_World == null || !m_World.RWAccessor.Accessible) return;

            int count = 0;
            for (int x = dragParams.MinX; x <= dragParams.MaxX; x++)
            {
                for (int y = dragParams.MinY; y <= dragParams.MaxY; y++)
                {
                    for (int z = dragParams.MinZ; z <= dragParams.MaxZ; z++)
                    {
                        if (y < 0 || y >= ChunkHeight) continue;

                        DoBuildAt(x, y, z);
                        count++;
                    }
                }
            }
        }

        private void DoBuildAt(int x, int y, int z)
        {
            BlockData currentBlock = m_World.RWAccessor.GetBlock(x, y, z);
            BlockData airBlock = m_World.BlockDataTable.GetBlock(0);

            if (CurrentMode == BuildMode.Construct)
            {
                BlockData toPlace = m_World.BlockDataTable.GetBlock(ConstructBlockType);
                if (toPlace == null) return;

                if (currentBlock == null || currentBlock.ID == 0 || currentBlock == airBlock)
                {
                    JobManager.Instance.Enqueue(new BlockJob
                    {
                        Position = new Vector3Int(x, y, z),
                        JobType = BlockJobType.Build,
                        BlockType = ConstructBlockType
                    });
                }
            }
            else if (CurrentMode == BuildMode.Deconstruct || CurrentMode == BuildMode.Mine)
            {
                if (currentBlock != null && currentBlock.ID != 0 && currentBlock != airBlock &&
                    !currentBlock.HasFlag(BlockFlags.IgnoreDestroyBlockRaycast) &&
                    currentBlock.PhysicState == PhysicState.Solid)
                {
                    JobManager.Instance.Enqueue(new BlockJob
                    {
                        Position = new Vector3Int(x, y, z),
                        JobType = BlockJobType.Deconstruct,
                        BlockType = currentBlock.InternalName
                    });
                }
            }
        }
    }
}
