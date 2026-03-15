using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Minecraft.Collections;
using Minecraft.PhysicSystem;
using UnityEngine;

namespace Minecraft.Pathfinding
{
    public static class AStarPathfinding
    {
        private static readonly Vector3Int[] s_CardinalOffsets = new Vector3Int[]
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 0, -1),
        };
        private static readonly Vector3Int[] s_DiagonalOffsets = new Vector3Int[]
        {
            new Vector3Int(1, 0, 1),
            new Vector3Int(1, 0, -1),
            new Vector3Int(-1, 0, 1),
            new Vector3Int(-1, 0, -1),
        };

        public static List<Vector3Int> FindPath(Vector3Int start, Vector3Int end, IWorld world, int maxIterations = 1000)
        {
            return FindPath(start, end, world, maxIterations, out _);
        }

        public static List<Vector3Int> FindPath(Vector3Int start, Vector3Int end, IWorld world, int maxIterations, out int expandedNodes)
        {
            expandedNodes = 0;

            Vector3Int? adjustedEnd = end;
            if (!IsWalkable(end, world))
            {
                adjustedEnd = FindNearestWalkableNode(end, world, 10);
                if (!adjustedEnd.HasValue)
                {
                    return null;
                }
            }

            Vector3Int? adjustedStart = start;
            if (!IsWalkable(start, world))
            {
                adjustedStart = FindNearestWalkableNode(start, world, 10);
                if (!adjustedStart.HasValue)
                {
                    return null;
                }
            }

            var openSet = new List<Vector3Int>();
            var openSetF = new Dictionary<Vector3Int, float>();
            var closedSet = new HashSet<Vector3Int>();
            var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
            var gScore = new Dictionary<Vector3Int, float>();
            var fScore = new Dictionary<Vector3Int, float>();

            float startF = Heuristic(adjustedStart.Value, adjustedEnd.Value);
            gScore[adjustedStart.Value] = 0;
            fScore[adjustedStart.Value] = startF;
            openSet.Add(adjustedStart.Value);
            openSetF[adjustedStart.Value] = startF;

            int iterations = 0;
            while (openSet.Count > 0 && iterations < maxIterations)
            {
                iterations++;
                expandedNodes = iterations;

                float lowestF = float.MaxValue;
                float lowestH = float.MaxValue;
                int lowestIndex = 0;
                for (int i = 0; i < openSet.Count; i++)
                {
                    Vector3Int pos = openSet[i];
                    float f = openSetF[pos];
                    float h = Heuristic(pos, adjustedEnd.Value);
                    
                    if (f < lowestF || (f == lowestF && h < lowestH))
                    {
                        lowestF = f;
                        lowestH = h;
                        lowestIndex = i;
                    }
                }

                Vector3Int currentPos = openSet[lowestIndex];
                openSet.RemoveAt(lowestIndex);
                openSetF.Remove(currentPos);

                if (currentPos == adjustedEnd)
                {
                    return ReconstructPath(cameFrom, currentPos);
                }

                closedSet.Add(currentPos);

                foreach (Vector3Int neighbor in EnumerateNeighbors(currentPos, world, true))
                {
                    if (closedSet.Contains(neighbor))
                    {
                        continue;
                    }

                    float tentativeGScore = gScore[currentPos] + GetTravelCost(currentPos, neighbor);
                    float newF = tentativeGScore + Heuristic(neighbor, adjustedEnd.Value);

                    if (!fScore.ContainsKey(neighbor) || newF < fScore[neighbor])
                    {
                        if (openSet.Contains(neighbor))
                        {
                            openSet.Remove(neighbor);
                        }

                        cameFrom[neighbor] = currentPos;
                        gScore[neighbor] = tentativeGScore;
                        fScore[neighbor] = newF;
                        openSet.Add(neighbor);
                        openSetF[neighbor] = newF;
                    }
                }
            }

            return null;
        }

        public static IEnumerable<Vector3Int> EnumerateNeighbors(Vector3Int pos, IWorld world, bool includeDiagonal)
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
                    if (IsWalkable(above, world) && CanMoveUp(pos, world))
                    {
                        yield return above;
                    }

                    Vector3Int below = neighbor + Vector3Int.down;
                    if (IsWalkable(below, world))
                    {
                        yield return below;
                    }
                }
            }

            if (!includeDiagonal)
            {
                yield break;
            }

            foreach (Vector3Int offset in s_DiagonalOffsets)
            {
                Vector3Int diagonal = pos + offset;
                if (!IsWalkable(diagonal, world))
                {
                    continue;
                }

                // Prevent corner cutting by requiring both orthogonal side cells to be walkable.
                Vector3Int sideA = pos + new Vector3Int(offset.x, 0, 0);
                Vector3Int sideB = pos + new Vector3Int(0, 0, offset.z);
                if (!IsWalkable(sideA, world) || !IsWalkable(sideB, world))
                {
                    continue;
                }

                yield return diagonal;
            }
        }

        private static bool CanMoveUp(Vector3Int pos, IWorld world)
        {
            var blockAbove = world.RWAccessor.GetBlock(pos.x, pos.y + 1, pos.z);
            return blockAbove == null || blockAbove.PhysicState != PhysicState.Solid;
        }

        public static bool IsWalkableNode(Vector3Int pos, IWorld world) => IsWalkable(pos, world);

        private static bool IsWalkable(Vector3Int pos, IWorld world)
        {
            if (pos.y < 0)
                return false;
                
            var block = world.RWAccessor.GetBlock(pos.x, pos.y, pos.z);
            bool hasBlock = block != null && block.PhysicState == PhysicState.Solid;
            
            if (hasBlock)
                return false;
                
            var groundBlock = world.RWAccessor.GetBlock(pos.x, pos.y - 1, pos.z);
            bool hasGround = groundBlock != null && groundBlock.PhysicState == PhysicState.Solid;
            
            return hasGround;
        }

        public static Vector3Int? FindNearestWalkableNode(Vector3Int pos, IWorld world, int searchRadius)
        {
            float bestDist = float.MaxValue;
            Vector3Int? bestPos = null;

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

        private static float Heuristic(Vector3Int a, Vector3Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z);
        }

        public static float GetTravelCost(Vector3Int a, Vector3Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            int dz = Mathf.Abs(a.z - b.z);

            if (dx == 1 && dz == 1 && dy == 0)
            {
                return 1.4142135f;
            }

            if (dy > 0)
            {
                return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
            }
            return dx + dz;
        }

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

        public static List<Vector3Int> SimplifyPath(List<Vector3Int> path)
        {
            if (path == null || path.Count <= 2)
            {
                return path;
            }

            var simplified = new List<Vector3Int> { path[0] };
            Vector3Int lastDirection = path[1] - path[0];

            for (int i = 1; i < path.Count - 1; i++)
            {
                Vector3Int nextDirection = path[i + 1] - path[i];
                bool hasHeightChange = path[i].y != path[i - 1].y || path[i + 1].y != path[i].y;

                // Preserve every waypoint around vertical movement to avoid losing jump/fall key points.
                if (hasHeightChange || nextDirection != lastDirection)
                {
                    if (simplified[simplified.Count - 1] != path[i])
                    {
                        simplified.Add(path[i]);
                    }
                }

                lastDirection = nextDirection;
            }

            Vector3Int lastPoint = path[path.Count - 1];
            if (simplified[simplified.Count - 1] != lastPoint)
            {
                simplified.Add(lastPoint);
            }

            return simplified;
        }
    }

    public static class FlowFieldPathfinding
    {
        private readonly struct CacheKey : IEquatable<CacheKey>
        {
            public readonly int WorldHash;
            public readonly Vector3Int Target;

            public CacheKey(int worldHash, Vector3Int target)
            {
                WorldHash = worldHash;
                Target = target;
            }

            public bool Equals(CacheKey other) => WorldHash == other.WorldHash && Target == other.Target;
            public override bool Equals(object obj) => obj is CacheKey other && Equals(other);
            public override int GetHashCode() => (WorldHash * 397) ^ Target.GetHashCode();
        }

        private readonly struct FrontierNode : IComparable<FrontierNode>
        {
            public readonly Vector3Int Position;
            public readonly float Cost;

            public FrontierNode(Vector3Int position, float cost)
            {
                Position = position;
                Cost = cost;
            }

            public int CompareTo(FrontierNode other) => Cost.CompareTo(other.Cost);
        }

        private sealed class FlowFieldData
        {
            public Vector3Int Target;
            public readonly Dictionary<Vector3Int, float> IntegrationCost;
            public float BuiltAtTime;
            public int ExpandedNodes;
            public int IntegrationNodeCount;

            public FlowFieldData(Dictionary<Vector3Int, float> integrationCost)
            {
                IntegrationCost = integrationCost;
            }

            public void Update(Vector3Int target, float builtAtTime, int expandedNodes)
            {
                Target = target;
                BuiltAtTime = builtAtTime;
                ExpandedNodes = expandedNodes;
                IntegrationNodeCount = IntegrationCost != null ? IntegrationCost.Count : 0;
            }
        }

        private static readonly Dictionary<CacheKey, FlowFieldData> s_Cache = new Dictionary<CacheKey, FlowFieldData>();
        private static readonly List<CacheKey> s_RemoveBuffer = new List<CacheKey>();
        private static readonly Dictionary<CacheKey, int> s_BuiltFrameByKey = new Dictionary<CacheKey, int>();
        private static readonly HashSet<Vector3Int> s_UniqueTargetsBuffer = new HashSet<Vector3Int>();
        private static readonly Dictionary<CacheKey, float> s_FailedUntilByRequestedTarget = new Dictionary<CacheKey, float>();
        private static readonly Stack<PriorityQueue<FrontierNode>> s_FrontierPool = new Stack<PriorityQueue<FrontierNode>>(4);

        public static List<Vector3Int> FindPath(
            Vector3Int start,
            Vector3Int end,
            IWorld world,
            int maxNodes,
            float cacheLifetime,
            int searchRadius,
            int maxPathLength)
        {
            return FindPath(
                start,
                end,
                world,
                maxNodes,
                cacheLifetime,
                searchRadius,
                maxPathLength,
                out _,
                out _,
                out _,
                out _);
        }

        public static List<Vector3Int> FindPath(
            Vector3Int start,
            Vector3Int end,
            IWorld world,
            int maxNodes,
            float cacheLifetime,
            int searchRadius,
            int maxPathLength,
            out int buildExpandedNodes,
            out int integrationNodeCount,
            out int traceSteps,
            out bool cacheHit)
        {
            buildExpandedNodes = 0;
            integrationNodeCount = 0;
            traceSteps = 0;
            cacheHit = false;

            if (!TryGetOrBuildField(end, world, maxNodes, cacheLifetime, searchRadius, out FlowFieldData field, out cacheHit))
            {
                return null;
            }

            buildExpandedNodes = field.ExpandedNodes;
            integrationNodeCount = field.IntegrationNodeCount;
            return TracePath(start, field, world, searchRadius, maxPathLength, out traceSteps);
        }

        public static void PrepareFields(
            IWorld world,
            IEnumerable<Vector3Int> targets,
            int maxNodes,
            float cacheLifetime,
            int searchRadius)
        {
            if (world == null || targets == null)
            {
                return;
            }

            s_UniqueTargetsBuffer.Clear();

            foreach (Vector3Int target in targets)
            {
                if (!s_UniqueTargetsBuffer.Add(target))
                {
                    continue;
                }

                TryGetOrBuildField(target, world, maxNodes, cacheLifetime, searchRadius, out _, out _);
            }
        }

        public static bool TryPrepareField(
            Vector3Int end,
            IWorld world,
            int maxNodes,
            float cacheLifetime,
            int searchRadius,
            out Vector3Int preparedTarget)
        {
            preparedTarget = end;

            if (!TryGetOrBuildField(end, world, maxNodes, cacheLifetime, searchRadius, out FlowFieldData field, out _))
            {
                return false;
            }

            preparedTarget = field.Target;
            return true;
        }

        public static bool TryGetNextNode(
            Vector3Int current,
            Vector3Int end,
            IWorld world,
            int maxNodes,
            float cacheLifetime,
            int searchRadius,
            out Vector3Int nextNode)
        {
            nextNode = current;

            if (!TryGetOrBuildField(end, world, maxNodes, cacheLifetime, searchRadius, out FlowFieldData field, out _))
            {
                return false;
            }

            return TryResolveNextNode(current, field, world, searchRadius, out nextNode);
        }

        public static bool TryGetNextNodeFromPreparedTarget(
            Vector3Int current,
            Vector3Int preparedTarget,
            IWorld world,
            int searchRadius,
            out Vector3Int nextNode)
        {
            nextNode = current;
            if (world == null)
            {
                return false;
            }

            CacheKey key = new CacheKey(RuntimeHelpers.GetHashCode(world), preparedTarget);
            if (!s_Cache.TryGetValue(key, out FlowFieldData field) || field == null)
            {
                return false;
            }

            return TryResolveNextNode(current, field, world, searchRadius, out nextNode);
        }

        public static bool HasPreparedField(Vector3Int preparedTarget, IWorld world)
        {
            if (world == null)
            {
                return false;
            }

            CacheKey key = new CacheKey(RuntimeHelpers.GetHashCode(world), preparedTarget);
            return s_Cache.ContainsKey(key);
        }

        private static bool TryResolveNextNode(
            Vector3Int current,
            FlowFieldData field,
            IWorld world,
            int searchRadius,
            out Vector3Int nextNode)
        {
            nextNode = current;
            if (field == null)
            {
                return false;
            }

            Vector3Int? adjustedCurrent = AStarPathfinding.IsWalkableNode(current, world)
                ? current
                : AStarPathfinding.FindNearestWalkableNode(current, world, searchRadius);
            if (!adjustedCurrent.HasValue)
            {
                return false;
            }

            if (!field.IntegrationCost.TryGetValue(adjustedCurrent.Value, out float currentCost))
            {
                return false;
            }

            if (adjustedCurrent.Value == field.Target)
            {
                nextNode = field.Target;
                return true;
            }

            Vector3Int bestNeighbor = adjustedCurrent.Value;
            float bestCost = currentCost;

            foreach (Vector3Int neighbor in AStarPathfinding.EnumerateNeighbors(adjustedCurrent.Value, world, true))
            {
                if (!field.IntegrationCost.TryGetValue(neighbor, out float neighborCost))
                {
                    continue;
                }

                if (neighborCost < bestCost - 0.0001f)
                {
                    bestCost = neighborCost;
                    bestNeighbor = neighbor;
                }
            }

            if (bestNeighbor == adjustedCurrent.Value)
            {
                return false;
            }

            nextNode = bestNeighbor;
            return true;
        }

        public static void InvalidateWorld(IWorld world)
        {
            if (world == null)
            {
                return;
            }

            int worldHash = RuntimeHelpers.GetHashCode(world);
            s_RemoveBuffer.Clear();

            foreach (KeyValuePair<CacheKey, FlowFieldData> entry in s_Cache)
            {
                if (entry.Key.WorldHash == worldHash)
                {
                    s_RemoveBuffer.Add(entry.Key);
                }
            }

            for (int i = 0; i < s_RemoveBuffer.Count; i++)
            {
                CacheKey key = s_RemoveBuffer[i];
                s_Cache.Remove(key);
                s_BuiltFrameByKey.Remove(key);
                s_FailedUntilByRequestedTarget.Remove(key);
            }
        }

        private static bool TryGetOrBuildField(
            Vector3Int end,
            IWorld world,
            int maxNodes,
            float cacheLifetime,
            int searchRadius,
            out FlowFieldData field,
            out bool cacheHit)
        {
            field = null;
            cacheHit = false;

            if (world == null)
            {
                return false;
            }

            float now = Time.time;
            int worldHash = RuntimeHelpers.GetHashCode(world);
            CacheKey requestedKey = new CacheKey(worldHash, end);

            if (s_FailedUntilByRequestedTarget.TryGetValue(requestedKey, out float retryAfter) && now < retryAfter)
            {
                return false;
            }

            Vector3Int? adjustedEnd = AStarPathfinding.IsWalkableNode(end, world)
                ? end
                : AStarPathfinding.FindNearestWalkableNode(end, world, searchRadius);
            if (!adjustedEnd.HasValue)
            {
                s_FailedUntilByRequestedTarget[requestedKey] = now + Mathf.Max(cacheLifetime, 0.1f);
                return false;
            }

            CacheKey key = new CacheKey(worldHash, adjustedEnd.Value);
            s_FailedUntilByRequestedTarget.Remove(requestedKey);

            bool validCache = s_Cache.TryGetValue(key, out field) && now - field.BuiltAtTime <= cacheLifetime;
            if (validCache)
            {
                cacheHit = true;
                return true;
            }

            int frame = Time.frameCount;
            if (s_BuiltFrameByKey.TryGetValue(key, out int builtFrame) && builtFrame == frame && field != null)
            {
                cacheHit = true;
                return true;
            }

            field = BuildFlowField(adjustedEnd.Value, world, maxNodes, now, field);
            if (field == null)
            {
                s_FailedUntilByRequestedTarget[requestedKey] = now + Mathf.Max(cacheLifetime, 0.1f);
                return false;
            }

            s_Cache[key] = field;
            s_BuiltFrameByKey[key] = frame;
            CleanupExpired(now, Mathf.Max(cacheLifetime, 0.1f) * 3f);
            return true;
        }

        private static FlowFieldData BuildFlowField(
            Vector3Int target,
            IWorld world,
            int maxNodes,
            float now,
            FlowFieldData reuseField)
        {
            if (!AStarPathfinding.IsWalkableNode(target, world))
            {
                return null;
            }

            Dictionary<Vector3Int, float> costs;
            if (reuseField != null && reuseField.IntegrationCost != null)
            {
                costs = reuseField.IntegrationCost;
                costs.Clear();
            }
            else
            {
                costs = new Dictionary<Vector3Int, float>(Mathf.Max(256, maxNodes));
            }

            PriorityQueue<FrontierNode> frontier = AcquireFrontier(Mathf.Max(64, maxNodes / 4));

            costs[target] = 0f;
            frontier.Enqueue(new FrontierNode(target, 0f));

            int expanded = 0;
            while (frontier.Count > 0 && expanded < maxNodes)
            {
                FrontierNode current = frontier.Dequeue();
                if (!costs.TryGetValue(current.Position, out float currentBest) || current.Cost > currentBest + 0.0001f)
                {
                    continue;
                }

                expanded++;

                foreach (Vector3Int neighbor in AStarPathfinding.EnumerateNeighbors(current.Position, world, true))
                {
                    float nextCost = current.Cost + AStarPathfinding.GetTravelCost(current.Position, neighbor);
                    if (costs.TryGetValue(neighbor, out float oldCost) && nextCost >= oldCost)
                    {
                        continue;
                    }

                    costs[neighbor] = nextCost;
                    frontier.Enqueue(new FrontierNode(neighbor, nextCost));
                }
            }

            ReleaseFrontier(frontier);

            if (costs.Count == 0)
            {
                return null;
            }

            FlowFieldData data = reuseField ?? new FlowFieldData(costs);
            data.Update(target, now, expanded);
            return data;
        }

        private static PriorityQueue<FrontierNode> AcquireFrontier(int capacityHint)
        {
            if (s_FrontierPool.Count > 0)
            {
                return s_FrontierPool.Pop();
            }

            return new PriorityQueue<FrontierNode>(Mathf.Max(64, capacityHint));
        }

        private static void ReleaseFrontier(PriorityQueue<FrontierNode> frontier)
        {
            if (frontier == null)
            {
                return;
            }

            frontier.Clear();
            if (s_FrontierPool.Count < 4)
            {
                s_FrontierPool.Push(frontier);
            }
        }

        private static List<Vector3Int> TracePath(Vector3Int start, FlowFieldData field, IWorld world, int searchRadius, int maxPathLength, out int traceSteps)
        {
            traceSteps = 0;
            Vector3Int? adjustedStart = AStarPathfinding.IsWalkableNode(start, world)
                ? start
                : AStarPathfinding.FindNearestWalkableNode(start, world, searchRadius);
            if (!adjustedStart.HasValue)
            {
                return null;
            }

            if (!field.IntegrationCost.ContainsKey(adjustedStart.Value))
            {
                return null;
            }

            var path = new List<Vector3Int>(32) { adjustedStart.Value };
            Vector3Int current = adjustedStart.Value;

            int steps = 0;
            while (current != field.Target && steps < maxPathLength)
            {
                steps++;
                traceSteps = steps;
                if (!field.IntegrationCost.TryGetValue(current, out float currentCost))
                {
                    return null;
                }

                Vector3Int bestNeighbor = current;
                float bestCost = currentCost;

                foreach (Vector3Int neighbor in AStarPathfinding.EnumerateNeighbors(current, world, true))
                {
                    if (!field.IntegrationCost.TryGetValue(neighbor, out float neighborCost))
                    {
                        continue;
                    }

                    if (neighborCost < bestCost - 0.0001f)
                    {
                        bestCost = neighborCost;
                        bestNeighbor = neighbor;
                    }
                }

                if (bestNeighbor == current)
                {
                    return null;
                }

                path.Add(bestNeighbor);
                current = bestNeighbor;
            }

            return current == field.Target ? path : null;
        }

        private static void CleanupExpired(float now, float expireAfterSeconds)
        {
            s_RemoveBuffer.Clear();

            foreach (KeyValuePair<CacheKey, FlowFieldData> entry in s_Cache)
            {
                if (now - entry.Value.BuiltAtTime > expireAfterSeconds)
                {
                    s_RemoveBuffer.Add(entry.Key);
                }
            }

            for (int i = 0; i < s_RemoveBuffer.Count; i++)
            {
                CacheKey key = s_RemoveBuffer[i];
                s_Cache.Remove(key);
                s_BuiltFrameByKey.Remove(key);
            }
        }
    }
}
