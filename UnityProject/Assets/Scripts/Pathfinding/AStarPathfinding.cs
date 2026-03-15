using System.Collections.Generic;
using Minecraft.Collections;
using Minecraft.PhysicSystem;
using UnityEngine;

namespace Minecraft.Pathfinding
{
    /// <summary>
    /// A*寻路算法实现，用于体素世界中的路径查找
    /// </summary>
    public static class AStarPathfinding
    {
        private static readonly Vector3Int[] s_NeighborOffsets = new Vector3Int[]
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 0, -1),
            new Vector3Int(1, 1, 0),
            new Vector3Int(-1, 1, 0),
            new Vector3Int(0, 1, 1),
            new Vector3Int(0, 1, -1),
            new Vector3Int(1, -1, 0),
            new Vector3Int(-1, -1, 0),
            new Vector3Int(0, -1, 1),
            new Vector3Int(0, -1, -1),
        };

        private static readonly Vector3Int[] s_CardinalOffsets = new Vector3Int[]
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 0, -1),
        };

        /// <summary>
        /// 查找从起点到终点的路径
        /// </summary>
        /// <param name="start">起点坐标</param>
        /// <param name="end">终点坐标</param>
        /// <param name="world">世界引用</param>
        /// <param name="maxIterations">最大迭代次数</param>
        /// <returns>路径点列表，如果找不到路径则返回null</returns>
        public static List<Vector3Int> FindPath(Vector3Int start, Vector3Int end, IWorld world, int maxIterations = 1000)
        {
            Debug.Log($"[AStar] FindPath from {start} to {end}");

            Vector3Int adjustedEnd = end;
            if (!IsWalkable(end, world))
            {
                Debug.Log($"[AStar] End {end} is not walkable, finding nearest walkable");
                adjustedEnd = FindNearestWalkable(end, world, 5);
                if (adjustedEnd == Vector3Int.down)
                {
                    Debug.Log($"[AStar] No walkable position found near target");
                    return null;
                }
                Debug.Log($"[AStar] Found nearest walkable: {adjustedEnd}");
            }

            Vector3Int adjustedStart = start;
            if (!IsWalkable(start, world))
            {
                Debug.Log($"[AStar] Start {start} is not walkable, finding nearest walkable");
                adjustedStart = FindNearestWalkable(start, world, 5);
                if (adjustedStart == Vector3Int.down)
                {
                    Debug.Log($"[AStar] No walkable position found near start");
                    return null;
                }
                Debug.Log($"[AStar] Found nearest walkable start: {adjustedStart}");
            }

            var openSet = new PriorityQueue<PathNode>();
            var closedSet = new HashSet<Vector3Int>();
            var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
            var gScore = new Dictionary<Vector3Int, float>();
            var fScore = new Dictionary<Vector3Int, float>();

            gScore[adjustedStart] = 0;
            fScore[adjustedStart] = Heuristic(adjustedStart, adjustedEnd);

            openSet.Enqueue(new PathNode(adjustedStart, fScore[adjustedStart]));

            int iterations = 0;
            while (openSet.Count > 0 && iterations < maxIterations)
            {
                iterations++;

                PathNode current = openSet.Dequeue();
                Vector3Int currentPos = current.Position;

                if (currentPos == adjustedEnd)
                {
                    var path = ReconstructPath(cameFrom, currentPos);
                    Debug.Log($"[AStar] Path found with {path.Count} nodes after {iterations} iterations");
                    for (int i = 0; i < path.Count; i++)
                    {
                        Debug.Log($"[AStar] Path node {i}: {path[i]}");
                    }
                    return path;
                }

                closedSet.Add(currentPos);

                foreach (Vector3Int neighbor in GetNeighbors(currentPos, world))
                {
                    if (closedSet.Contains(neighbor))
                    {
                        continue;
                    }

                    float tentativeGScore = gScore[currentPos] + GetDistance(currentPos, neighbor);

                    if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                    {
                        cameFrom[neighbor] = currentPos;
                        gScore[neighbor] = tentativeGScore;
                        fScore[neighbor] = tentativeGScore + Heuristic(neighbor, adjustedEnd);

                        openSet.Enqueue(new PathNode(neighbor, fScore[neighbor]));
                    }
                }
            }

            Debug.Log($"[AStar] No path found after {iterations} iterations, openSet count: {openSet.Count}");
            return null;
        }

        /// <summary>
        /// 获取相邻的可行走节点
        /// </summary>
        private static IEnumerable<Vector3Int> GetNeighbors(Vector3Int pos, IWorld world)
        {
            foreach (Vector3Int offset in s_CardinalOffsets)
            {
                Vector3Int neighbor = pos + offset;

                if (IsWalkable(neighbor, world))
                {
                    yield return neighbor;
                }
                else
                {
                    Vector3Int above = neighbor + Vector3Int.up;
                    if (IsWalkable(above, world) && IsWalkable(pos + Vector3Int.up, world))
                    {
                        yield return above;
                    }

                    Vector3Int below = neighbor + Vector3Int.down;
                    if (IsWalkable(below, world) && IsWalkable(pos, world))
                    {
                        yield return below;
                    }
                }
            }

            Vector3Int upPos = pos + Vector3Int.up;
            if (IsWalkable(upPos, world) && IsWalkable(pos, world))
            {
                yield return upPos;
            }

            Vector3Int downPos = pos + Vector3Int.down;
            if (IsWalkable(downPos, world) && IsWalkable(pos, world))
            {
                yield return downPos;
            }
        }

        /// <summary>
        /// 检查位置是否可行走
        /// </summary>
        private static bool IsWalkable(Vector3Int pos, IWorld world)
        {
            var block = world.RWAccessor.GetBlock(pos.x, pos.y, pos.z);
            if (block == null || block.PhysicState != PhysicState.Solid)
            {
                var groundBlock = world.RWAccessor.GetBlock(pos.x, pos.y - 1, pos.z);
                return groundBlock != null && groundBlock.PhysicState == PhysicState.Solid;
            }
            return false;
        }

        /// <summary>
        /// 查找最近的可行走位置
        /// </summary>
        private static Vector3Int FindNearestWalkable(Vector3Int pos, IWorld world, int searchRadius)
        {
            float bestDist = float.MaxValue;
            Vector3Int bestPos = Vector3Int.down;

            for (int x = -searchRadius; x <= searchRadius; x++)
            {
                for (int z = -searchRadius; z <= searchRadius; z++)
                {
                    for (int y = -searchRadius; y <= searchRadius; y++)
                    {
                        Vector3Int checkPos = pos + new Vector3Int(x, y, z);
                        if (IsWalkable(checkPos, world))
                        {
                            float dist = Vector3Int.Distance(pos, checkPos);
                            if (dist < bestDist)
                            {
                                bestDist = dist;
                                bestPos = checkPos;
                            }
                        }
                    }
                }
            }

            return bestPos;
        }

        /// <summary>
        /// 启发式函数（曼哈顿距离）
        /// </summary>
        private static float Heuristic(Vector3Int a, Vector3Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z);
        }

        /// <summary>
        /// 获取两点之间的移动成本
        /// </summary>
        private static float GetDistance(Vector3Int a, Vector3Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            int dz = Mathf.Abs(a.z - b.z);

            if (dy > 0)
            {
                return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
            }
            return dx + dz;
        }

        /// <summary>
        /// 重建路径
        /// </summary>
        private static List<Vector3Int> ReconstructPath(Dictionary<Vector3Int, Vector3Int> cameFrom, Vector3Int current)
        {
            var path = new List<Vector3Int> { current };

            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Insert(0, current);
            }

            return path;
        }

        /// <summary>
        /// 简化路径，移除多余的中间点
        /// </summary>
        public static List<Vector3Int> SimplifyPath(List<Vector3Int> path)
        {
            if (path == null || path.Count <= 2)
            {
                return path;
            }

            var simplified = new List<Vector3Int> { path[0] };
            Vector3Int lastDirection = Vector3Int.zero;

            for (int i = 1; i < path.Count; i++)
            {
                Vector3Int direction = path[i] - path[i - 1];
                if (direction != lastDirection)
                {
                    simplified.Add(path[i - 1]);
                    lastDirection = direction;
                }
            }

            simplified.Add(path[path.Count - 1]);
            return simplified;
        }
    }

    /// <summary>
    /// 路径节点
    /// </summary>
    public readonly struct PathNode : System.IComparable<PathNode>
    {
        public readonly Vector3Int Position;
        public readonly float FScore;

        public PathNode(Vector3Int position, float fScore)
        {
            Position = position;
            FScore = fScore;
        }

        public int CompareTo(PathNode other)
        {
            return FScore.CompareTo(other.FScore);
        }
    }
}
