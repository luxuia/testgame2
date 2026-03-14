using System;
using Minecraft.BuildMode;
using Minecraft.Configurations;
using Minecraft.PhysicSystem;
using UnityEngine;
using Physics = Minecraft.PhysicSystem.Physics;

namespace Minecraft.PlayerControls
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BuildModeController))]
    public class BuildModeInput : MonoBehaviour
    {
        [SerializeField] private float m_RaycastMaxDistance = 64f;

        [NonSerialized] private Camera m_Camera;
        [NonSerialized] private BuildModeController m_BuildModeController;
        [NonSerialized] private IWorld m_World;

        [NonSerialized] private bool m_IsDragging;
        [NonSerialized] private Vector3Int m_DragStartPos;
        [NonSerialized] private Vector2 m_DragStartScreen;

        public void Initialize(Camera camera, IWorld world)
        {
            m_Camera = camera;
            m_World = world;
            m_BuildModeController = GetComponent<BuildModeController>();
            m_BuildModeController.Initialize(camera, world);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.B))
            {
                if (m_BuildModeController.BuildModeActive)
                    m_BuildModeController.ClearBuildMode();
                else
                    m_BuildModeController.SetMode(Minecraft.BuildMode.BuildMode.Deconstruct);
            }
            if (Input.GetKeyDown(KeyCode.G))
            {
                m_BuildModeController.SetMode(Minecraft.BuildMode.BuildMode.Construct, "stone");
            }

            if (!m_BuildModeController.BuildModeActive || m_Camera == null || m_World == null) return;

            if (Input.GetMouseButtonDown(0))
            {
                if (GetBlockAtScreenPosition(Input.mousePosition, out Vector3Int pos))
                {
                    m_IsDragging = true;
                    m_DragStartPos = pos;
                    m_DragStartScreen = Input.mousePosition;
                }
            }

            if (Input.GetMouseButtonUp(0) && m_IsDragging)
            {
                m_IsDragging = false;

                if (GetBlockAtScreenPosition(Input.mousePosition, out Vector3Int endPos))
                {
                    var dragParams = new DragParams(m_DragStartPos, endPos);
                    m_BuildModeController.HandleDragFinished(dragParams);
                }
                else
                {
                    m_BuildModeController.HandleDragFinished(new DragParams(m_DragStartPos));
                }
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                m_BuildModeController.ClearBuildMode();
            }
        }

        private bool GetBlockAtScreenPosition(Vector2 screenPos, out Vector3Int blockPos)
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
    }
}
