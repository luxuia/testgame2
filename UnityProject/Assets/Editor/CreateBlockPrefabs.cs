// CreateBlockPrefabs.cs
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

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor utility to create Minecraft-style block prefabs for terrain generation.
/// </summary>
public static class CreateBlockPrefabs
{
    private const string BlocksPrefabPath = "Assets/HotUpdate/Main/Common/Prefab/Blocks";
    private const string BlocksMaterialPath = "Assets/HotUpdate/Main/Common/Material/Blocks";

    [MenuItem("JEngine/Create Block Prefabs")]
    public static void CreatePrefabs()
    {
        EnsureDirectories();

        var materials = CreateMaterials();
        CreateBlockPrefab("Block_Grass", materials.grass);
        CreateBlockPrefab("Block_Dirt", materials.dirt);
        CreateBlockPrefab("Block_Stone", materials.stone);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Block prefabs created at " + BlocksPrefabPath);
    }

    private static void EnsureDirectories()
    {
        if (!AssetDatabase.IsValidFolder("Assets/HotUpdate/Main/Common/Prefab/Blocks"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/HotUpdate/Main/Common/Prefab"))
            {
                AssetDatabase.CreateFolder("Assets/HotUpdate/Main/Common", "Prefab");
            }
            AssetDatabase.CreateFolder("Assets/HotUpdate/Main/Common/Prefab", "Blocks");
        }
        if (!AssetDatabase.IsValidFolder("Assets/HotUpdate/Main/Common/Material/Blocks"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/HotUpdate/Main/Common/Material"))
            {
                AssetDatabase.CreateFolder("Assets/HotUpdate/Main/Common", "Material");
            }
            AssetDatabase.CreateFolder("Assets/HotUpdate/Main/Common/Material", "Blocks");
        }
    }

    private static (Material grass, Material dirt, Material stone) CreateMaterials()
    {
        var grass = GetOrCreateMaterial("Block_Grass", new Color(0.2f, 0.6f, 0.2f));
        var dirt = GetOrCreateMaterial("Block_Dirt", new Color(0.45f, 0.3f, 0.2f));
        var stone = GetOrCreateMaterial("Block_Stone", new Color(0.5f, 0.5f, 0.55f));
        return (grass, dirt, stone);
    }

    private static Material GetOrCreateMaterial(string name, Color color)
    {
        var path = $"{BlocksMaterialPath}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null)
            return mat;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        mat = new Material(shader)
        {
            name = name,
            color = color
        };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void CreateBlockPrefab(string prefabName, Material material)
    {
        var path = $"{BlocksPrefabPath}/{prefabName}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            return;

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = prefabName;
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one;

        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }
}
#endif
