using System;
using System.Collections;
using System.Collections.Generic;
using Minecraft.Configurations;
using Minecraft.Entities;
using UnityEngine;
using static Minecraft.Rendering.LightingUtility;
using static Minecraft.WorldConsts;

namespace Minecraft
{
    internal class WorldSinglePlayer : World
    {
        private const int SpawnSearchRadius = 48;
        private const int SpawnHeadClearance = 2;
        private const float SpawnRetryInterval = 0.5f;
        private const int MaxSpawnSearchAttempts = 120;

        [NonSerialized] private Stack<Vector3Int> m_BlocksToLightQueue; // 这个也不用锁了，只会在主线程被使用
        [NonSerialized] private Stack<Vector3Int> m_ImportantBlocksToLightQueue;
        [NonSerialized] private Queue<Vector3Int> m_BlocksToTickQueue; // 这个不用锁，只会在主线程被使用

        protected override IEnumerator OnInitialize()
        {
            yield return null;

            m_BlocksToLightQueue = new Stack<Vector3Int>();
            m_ImportantBlocksToLightQueue = new Stack<Vector3Int>();
            m_BlocksToTickQueue = new Queue<Vector3Int>();
            StartCoroutine(EnablePlayer());
        }

        protected override void OnUpdate()
        {
            LightBlocks();
            TickBlocks();
        }

        private void LightBlocks()
        {
            // TODO: 优化

            int limit = MaxLightBlockCountPerFrame; // 防止卡死
            LightBlocks(m_ImportantBlocksToLightQueue, ref limit, ModificationSource.PlayerAction);
            LightBlocks(m_BlocksToLightQueue, ref limit, ModificationSource.InternalOrSystem);
        }

        private void LightBlocks(Stack<Vector3Int> queue, ref int limit, ModificationSource source)
        {
            while (limit-- > 0 && queue.Count > 0)
            {
                Vector3Int blockPos = queue.Pop();

                if (blockPos.y < 0 || blockPos.y >= ChunkHeight)
                {
                    continue;
                }

                if (!ChunkManager.GetChunk(ChunkPos.GetFromAny(blockPos.x, blockPos.z), false, out _))
                {
                    // 我不想管这个了，如果有人有好的算法请告诉我！
                    // m_BlocksToLightQueue.Push(blockPos);
                    // break;
                    continue;
                }

                int x = blockPos.x;
                int y = blockPos.y;
                int z = blockPos.z;

                BlockData block = RWAccessor.GetBlock(x, y, z);
                int opacity = Mathf.Max(block.LightOpacity, 1);
                int finalLight = 0;

                if (opacity < MaxLight || block.LightValue > 0) // 不然就是0
                {
                    int max = RWAccessor.GetAmbientLight(x + 1, y, z);
                    int temp;

                    if ((temp = RWAccessor.GetAmbientLight(x - 1, y, z)) > max)
                        max = temp;

                    if ((temp = RWAccessor.GetAmbientLight(x, y + 1, z)) > max)
                        max = temp;

                    if ((temp = RWAccessor.GetAmbientLight(x, y - 1, z)) > max)
                        max = temp;

                    if ((temp = RWAccessor.GetAmbientLight(x, y, z + 1)) > max)
                        max = temp;

                    if ((temp = RWAccessor.GetAmbientLight(x, y, z - 1)) > max)
                        max = temp;

                    finalLight = max - opacity;

                    if (block.LightValue > finalLight)
                    {
                        finalLight = block.LightValue; // 假设这个值一定是合法的（不过确实应该是合法的）
                    }
                    else if (finalLight < 0)
                    {
                        finalLight = 0;
                    }
                    //else if (finalLight > MaxLight)
                    //{
                    //    finalLight = MaxLight;
                    //}
                }

                if (RWAccessor.SetAmbientLightLevel(x, y, z, finalLight, source))
                {
                    queue.Push(new Vector3Int(x - 1, y, z));
                    queue.Push(new Vector3Int(x, y - 1, z));
                    queue.Push(new Vector3Int(x, y, z - 1));
                    queue.Push(new Vector3Int(x + 1, y, z));
                    queue.Push(new Vector3Int(x, y + 1, z));
                    queue.Push(new Vector3Int(x, y, z + 1));
                }
            }
        }

        private void TickBlocks()
        {
            int count = m_BlocksToTickQueue.Count;

            if (count > MaxTickBlockCountPerFrame)
            {
                count = MaxTickBlockCountPerFrame; // 防止卡死
            }

            while (count-- > 0)
            {
                Vector3Int blockPos = m_BlocksToTickQueue.Dequeue();
                int x = blockPos.x;
                int y = blockPos.y;
                int z = blockPos.z;
                RWAccessor.GetBlock(x, y, z)?.Tick(this, x, y, z);
            }
        }

        private IEnumerator EnablePlayer()
        {
            while (!Initialized || RWAccessor == null)
            {
                yield return null;
            }

            for (int attempt = 0; attempt < MaxSpawnSearchAttempts; attempt++)
            {
                if (TryGetPlayerTransform(out Transform playerTransform))
                {
                    if (TryFindSpawnPosition(playerTransform.position, out Vector3 spawnPosition))
                    {
                        playerTransform.position = spawnPosition;
                        EnablePlayerEntity(playerTransform);
                        yield break;
                    }
                }

                yield return new WaitForSeconds(SpawnRetryInterval);
            }

            // 兜底：即便没找到理想出生点，也不要卡住玩家控制。
            if (TryGetPlayerTransform(out Transform fallbackTransform))
            {
                EnablePlayerEntity(fallbackTransform);
            }
        }

        private bool TryGetPlayerTransform(out Transform playerTransform)
        {
            playerTransform = PlayerTransform;
            if (playerTransform != null)
            {
                return true;
            }

            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            playerTransform = playerObject != null ? playerObject.transform : null;
            return playerTransform != null;
        }

        private void EnablePlayerEntity(Transform playerTransform)
        {
            PlayerEntity entity = playerTransform.GetComponent<PlayerEntity>();
            if (entity != null)
            {
                entity.enabled = true;
            }
        }

        private bool TryFindSpawnPosition(Vector3 currentPosition, out Vector3 spawnPosition)
        {
            int originX = Mathf.FloorToInt(currentPosition.x);
            int originZ = Mathf.FloorToInt(currentPosition.z);

            bool found = false;
            Vector3 bestPosition = currentPosition;
            int bestScore = int.MaxValue;

            for (int dz = -SpawnSearchRadius; dz <= SpawnSearchRadius; dz++)
            {
                for (int dx = -SpawnSearchRadius; dx <= SpawnSearchRadius; dx++)
                {
                    int x = originX + dx;
                    int z = originZ + dz;

                    if (!TryEvaluateSpawnCandidate(x, z, originX, originZ, out Vector3 candidatePosition, out int score))
                    {
                        continue;
                    }

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestPosition = candidatePosition;
                        found = true;
                    }
                }
            }

            spawnPosition = bestPosition;
            return found;
        }

        private bool TryEvaluateSpawnCandidate(int x, int z, int originX, int originZ, out Vector3 candidatePosition, out int score)
        {
            candidatePosition = default;
            score = int.MaxValue;

            if (!ChunkManager.GetChunk(ChunkPos.GetFromAny(x, z), false, out _))
            {
                return false;
            }

            int topY = RWAccessor.GetTopVisibleBlockY(x, z, -1);
            if (topY < 1 || topY >= ChunkHeight - SpawnHeadClearance - 1)
            {
                return false;
            }

            BlockData centerBlock = RWAccessor.GetBlock(x, topY, z);
            if (!IsSpawnSurface(centerBlock))
            {
                return false;
            }

            if (!HasSpawnHeadroom(x, topY, z))
            {
                return false;
            }

            int maxHeightDelta = 0;
            int sumHeightDelta = 0;

            for (int nz = -1; nz <= 1; nz++)
            {
                for (int nx = -1; nx <= 1; nx++)
                {
                    int nearX = x + nx;
                    int nearZ = z + nz;
                    if (!ChunkManager.GetChunk(ChunkPos.GetFromAny(nearX, nearZ), false, out _))
                    {
                        return false;
                    }

                    int nearY = RWAccessor.GetTopVisibleBlockY(nearX, nearZ, -1);
                    if (nearY < 1)
                    {
                        return false;
                    }

                    BlockData nearTopBlock = RWAccessor.GetBlock(nearX, nearY, nearZ);
                    if (!IsSpawnSurface(nearTopBlock))
                    {
                        return false;
                    }

                    int delta = Mathf.Abs(nearY - topY);
                    if (delta > 1)
                    {
                        return false;
                    }

                    maxHeightDelta = Mathf.Max(maxHeightDelta, delta);
                    sumHeightDelta += delta;
                }
            }

            int distance = Mathf.Abs(x - originX) + Mathf.Abs(z - originZ);
            score = (maxHeightDelta * 1000) + (sumHeightDelta * 50) + distance;

            candidatePosition = new Vector3(x + 0.5f, topY + 1.05f, z + 0.5f);
            return true;
        }

        private bool HasSpawnHeadroom(int x, int topY, int z)
        {
            for (int y = 1; y <= SpawnHeadClearance; y++)
            {
                BlockData blockAbove = RWAccessor.GetBlock(x, topY + y, z);
                if (blockAbove == null)
                {
                    return false;
                }

                if (!IsAirLike(blockAbove))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsSpawnSurface(BlockData block)
        {
            if (block == null)
            {
                return false;
            }

            if (block.PhysicState != PhysicSystem.PhysicState.Solid)
            {
                return false;
            }

            if (block.HasFlag(BlockFlags.IgnoreCollisions) || block.HasFlag(BlockFlags.AlwaysInvisible))
            {
                return false;
            }

            string name = block.InternalName ?? string.Empty;
            if (name.Contains("water") || name.Contains("lava"))
            {
                return false;
            }

            return true;
        }

        private static bool IsAirLike(BlockData block)
        {
            if (block == null)
            {
                return false;
            }

            if (string.Equals(block.InternalName, "air", StringComparison.Ordinal))
            {
                return true;
            }

            if (block.HasFlag(BlockFlags.AlwaysInvisible))
            {
                return true;
            }

            return block.HasFlag(BlockFlags.IgnoreCollisions) && !ContainsLiquidKeyword(block.InternalName);
        }

        private static bool ContainsLiquidKeyword(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            return name.Contains("water") || name.Contains("lava");
        }

        public override void LightBlock(int x, int y, int z, ModificationSource source)
        {
            if (source == ModificationSource.InternalOrSystem)
            {
                m_BlocksToLightQueue.Push(new Vector3Int(x, y, z));
            }
            else
            {
                m_ImportantBlocksToLightQueue.Push(new Vector3Int(x, y, z));
            }
        }

        public override void TickBlock(int x, int y, int z)
        {
            m_BlocksToTickQueue.Enqueue(new Vector3Int(x, y, z));
            m_BlocksToTickQueue.Enqueue(new Vector3Int(x - 1, y, z));
            m_BlocksToTickQueue.Enqueue(new Vector3Int(x + 1, y, z));
            m_BlocksToTickQueue.Enqueue(new Vector3Int(x, y - 1, z));
            m_BlocksToTickQueue.Enqueue(new Vector3Int(x, y + 1, z));
            m_BlocksToTickQueue.Enqueue(new Vector3Int(x, y, z - 1));
            m_BlocksToTickQueue.Enqueue(new Vector3Int(x, y, z + 1));
        }
    }
}
