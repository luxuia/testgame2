using System;
using System.Collections.Generic;
using Minecraft.Configurations;
using Minecraft.Entities;
using Minecraft.Lua;
using Minecraft.PhysicSystem;
using Minecraft.Rendering;
using UnityEngine;
using UnityEngine.Serialization;
using static Minecraft.WorldConsts;

namespace Minecraft.PlayerControls
{
    public class FluidInteractor : MonoBehaviour, ILuaCallCSharp
    {
        [Serializable]
        private class FluidInfo
        {
            public string BlockName;
            public float VelocityMultiplier;
            public int ViewDistance;

            [ColorUsage(true, true)]
            [FormerlySerializedAs("AmbientColor")]
            public Color AmbientColorDay;

            [ColorUsage(true, true)]
            public Color AmbientColorNight;
        }

        [SerializeField] private FluidInfo[] m_Fluids;

        private Dictionary<string, FluidInfo> m_FluidMap;
        private string m_BlockAtHead = null;
        private string m_BlockAtBody = null;

        private void Awake()
        {
            EnsureFluidMapInitialized();
        }

        private void Start()
        {
            EnsureFluidMapInitialized();
        }

        public void UpdateState(IAABBEntity entity, Transform camera, out float velocityMultiplier)
        {
            velocityMultiplier = 1f;
            if (entity?.World == null || entity.World.RWAccessor == null)
            {
                return;
            }

            EnsureFluidMapInitialized();
            if (camera != null)
            {
                CheckHead(entity, camera);
            }

            velocityMultiplier = CheckBody(entity);
        }

        private void CheckHead(IAABBEntity entity, Transform camera)
        {
            if (camera == null || entity?.World == null || entity.World.RWAccessor == null)
            {
                return;
            }

            Vector3 pos = camera.position;
            int y = Mathf.FloorToInt(pos.y);

            if (y < 0 || y >= ChunkHeight)
            {
                return;
            }

            int x = Mathf.FloorToInt(pos.x);
            int z = Mathf.FloorToInt(pos.z);
            BlockData block = entity.World.RWAccessor.GetBlock(x, y, z);
            string currentBlock = block != null ? block.InternalName : null;

            if (currentBlock != m_BlockAtHead && !string.IsNullOrEmpty(currentBlock) && m_FluidMap.TryGetValue(currentBlock, out FluidInfo info))
            {
                m_BlockAtHead = currentBlock;
                ShaderUtility.ViewDistance = info.ViewDistance;
                ShaderUtility.WorldAmbientColorDay = info.AmbientColorDay;
                ShaderUtility.WorldAmbientColorNight = info.AmbientColorNight;
            }
            else if (currentBlock != m_BlockAtHead)
            {
                m_BlockAtHead = currentBlock;
            }
        }

        private float CheckBody(IAABBEntity entity)
        {
            if (entity?.World == null || entity.World.RWAccessor == null)
            {
                return 1f;
            }

            AABB aabb = entity.BoundingBox + entity.Position;
            Vector3Int center = aabb.Center.FloorToInt();
            int minY = Mathf.FloorToInt(aabb.Min.y);
            int maxY = Mathf.FloorToInt(aabb.Max.y);

            BlockData blockData = null;
            int index = int.MaxValue;

            for (int y = minY; y < maxY; y++)
            {
                BlockData block = entity.World.RWAccessor.GetBlock(center.x, y, center.z);
                if (block == null || string.IsNullOrEmpty(block.InternalName) || m_Fluids == null)
                {
                    continue;
                }

                // 根据 m_Fluids 数组元素的顺序来确定方块
                for (int i = 0; i < m_Fluids.Length; i++)
                {
                    if (m_Fluids[i] == null || string.IsNullOrEmpty(m_Fluids[i].BlockName))
                    {
                        continue;
                    }

                    // 越靠前，优先级越高
                    if (i >= index)
                    {
                        break;
                    }

                    if (block.InternalName == m_Fluids[i].BlockName)
                    {
                        blockData = block;
                        index = i;
                        break;
                    }
                }
            }

            if (blockData != null && m_BlockAtBody != blockData.InternalName)
            {
                m_BlockAtBody = blockData.InternalName;
                return m_Fluids[index].VelocityMultiplier;
            }

            return !string.IsNullOrEmpty(m_BlockAtBody) && m_FluidMap.TryGetValue(m_BlockAtBody, out FluidInfo info)
                ? info.VelocityMultiplier
                : 1; // default is 1
        }

        private void EnsureFluidMapInitialized()
        {
            if (m_FluidMap != null)
            {
                return;
            }

            m_FluidMap = new Dictionary<string, FluidInfo>(StringComparer.Ordinal);
            if (m_Fluids == null)
            {
                return;
            }

            for (int i = 0; i < m_Fluids.Length; i++)
            {
                FluidInfo fluid = m_Fluids[i];
                if (fluid == null || string.IsNullOrEmpty(fluid.BlockName))
                {
                    continue;
                }

                // Last definition wins to tolerate duplicate block entries in inspector.
                m_FluidMap[fluid.BlockName] = fluid;
            }
        }
    }
}
