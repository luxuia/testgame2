// TerrainGenerator.cs
//
//  Author:
//        JasonXuDeveloper <jason@xgamedev.net>
//
//  Copyright (c) 2025 JEngine
//
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in
//  all copies or substantial portions of the Software.
//
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//  THE SOFTWARE.

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using JEngine.Core;
using UnityEngine;
using YooAsset;

namespace HotUpdate.Code.Terrain
{
    /// <summary>
    /// Minecraft 风格地形生成器，使用 Perlin 噪声生成高度图，通过 Prefab 加载渲染地块。
    /// </summary>
    public class TerrainGenerator
    {
        private const string BlockPrefabBase = "Assets/HotUpdate/Main/Common/Prefab/Blocks";
        private const int DirtLayerDepth = 4;

        private readonly string _packageName;
        private readonly Transform _terrainRoot;
        private readonly Dictionary<BlockType, AssetHandle> _prefabHandles = new();
        private readonly Dictionary<BlockType, Material> _fallbackMaterials = new();

        /// <summary>世界 X 方向尺寸（格数）。</summary>
        public int SizeX { get; set; } = 32;

        /// <summary>世界 Z 方向尺寸（格数）。</summary>
        public int SizeZ { get; set; } = 32;

        /// <summary>噪声缩放。</summary>
        public float NoiseScale { get; set; } = 0.05f;

        /// <summary>高度振幅。</summary>
        public float Amplitude { get; set; } = 8f;

        /// <summary>基准高度。</summary>
        public float BaseHeight { get; set; } = 4f;

        /// <summary>随机种子。</summary>
        public int Seed { get; set; } = 42;

        /// <summary>
        /// 创建地形生成器。
        /// </summary>
        /// <param name="packageName">YooAsset 包名，默认 main。</param>
        /// <param name="terrainRoot">地形根节点，可为 null。</param>
        public TerrainGenerator(string packageName = "main", Transform terrainRoot = null)
        {
            _packageName = packageName;
            _terrainRoot = terrainRoot != null ? terrainRoot : CreateTerrainRoot();
        }

        private static Transform CreateTerrainRoot()
        {
            var go = new GameObject("TerrainRoot");
            return go.transform;
        }

        /// <summary>
        /// 异步生成地形。
        /// </summary>
        public async UniTask GenerateAsync()
        {
            var package = Bootstrap.CreateOrGetPackage(_packageName);
            await PreloadPrefabsAsync(package);

            for (var x = 0; x < SizeX; x++)
            {
                for (var z = 0; z < SizeZ; z++)
                {
                    var height = PerlinNoiseHelper.SampleHeightInt(
                        x, z, NoiseScale, Amplitude, BaseHeight, Seed);

                    for (var y = 0; y <= height; y++)
                    {
                        var blockType = GetBlockTypeAt(y, height);
                        if (blockType == BlockType.Air)
                            continue;

                        SpawnBlock(blockType, x, y, z, package);
                    }
                }

                if (x % 8 == 0)
                    await UniTask.DelayFrame(1);
            }

            Debug.Log($"[TerrainGenerator] Generated terrain {SizeX}x{SizeZ}");
        }

        private static BlockType GetBlockTypeAt(int y, int surfaceHeight)
        {
            if (y == surfaceHeight)
                return BlockType.Grass;
            if (y > surfaceHeight - DirtLayerDepth)
                return BlockType.Dirt;
            return BlockType.Stone;
        }

        private async UniTask PreloadPrefabsAsync(ResourcePackage package)
        {
            var blockNames = new[] { "Block_Grass", "Block_Dirt", "Block_Stone" };

            foreach (var name in blockNames)
            {
                var locations = new[]
                {
                    $"{BlockPrefabBase}/{name}",
                    $"{BlockPrefabBase}/{name}.prefab",
                    name,
                };

                AssetHandle handle = null;
                foreach (var loc in locations)
                {
                    if (package.CheckLocationValid(loc))
                    {
                        handle = package.LoadAssetAsync<GameObject>(loc);
                        await handle.Task;
                        if (handle.Status == EOperationStatus.Succeed)
                            break;
                        handle.Release();
                        handle = null;
                    }
                }

                if (handle != null && handle.Status == EOperationStatus.Succeed)
                {
                    var blockType = GetBlockTypeFromPrefabName(name);
                    _prefabHandles[blockType] = handle;
                }
            }

            CreateFallbackMaterials();
        }

        private static BlockType GetBlockTypeFromPrefabName(string path)
        {
            if (path.Contains("Grass")) return BlockType.Grass;
            if (path.Contains("Dirt")) return BlockType.Dirt;
            if (path.Contains("Stone")) return BlockType.Stone;
            return BlockType.Stone;
        }

        private void CreateFallbackMaterials()
        {
            if (_fallbackMaterials.Count > 0) return;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;

            _fallbackMaterials[BlockType.Grass] = new Material(shader) { color = new Color(0.2f, 0.6f, 0.2f) };
            _fallbackMaterials[BlockType.Dirt] = new Material(shader) { color = new Color(0.45f, 0.3f, 0.2f) };
            _fallbackMaterials[BlockType.Stone] = new Material(shader) { color = new Color(0.5f, 0.5f, 0.55f) };
        }

        private void SpawnBlock(BlockType blockType, int x, int y, int z, ResourcePackage package)
        {
            var pos = new Vector3(x, y, z);

            if (_prefabHandles.TryGetValue(blockType, out var handle) && handle.IsValid)
            {
                var prefab = handle.GetAssetObject<GameObject>();
                if (prefab != null)
                {
                    var instance = Object.Instantiate(prefab, pos, Quaternion.identity, _terrainRoot);
                    instance.name = $"{blockType}_{x}_{y}_{z}";
                    return;
                }
            }

            SpawnFallbackBlock(blockType, pos);
        }

        private void SpawnFallbackBlock(BlockType blockType, Vector3 pos)
        {
            if (!_fallbackMaterials.TryGetValue(blockType, out var mat))
                return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(_terrainRoot);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = mat;

            go.name = $"{blockType}_fallback_{pos.x}_{pos.y}_{pos.z}";
        }

        /// <summary>
        /// 释放预加载的 Prefab 句柄。
        /// </summary>
        public void Release()
        {
            foreach (var handle in _prefabHandles.Values)
            {
                if (handle.IsValid)
                    handle.Release();
            }
            _prefabHandles.Clear();

            foreach (var mat in _fallbackMaterials.Values)
            {
                if (mat != null)
                    Object.Destroy(mat);
            }
            _fallbackMaterials.Clear();
        }
    }
}
