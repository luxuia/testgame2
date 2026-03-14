// MenuItems.cs
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

using System.IO;
using JEngine.Core.Editor.CustomEditor;
using UnityEditor;
using UnityEngine;
using YooAsset;

namespace JEngine.Core.Editor
{
    public static class MenuItems
    {
        [MenuItem("JEngine/JEngine Panel #&J", priority = 1000)]
        private static void OpenWindow()
        {
            var window = EditorWindow.GetWindow<Panel>();
            window.titleContent = new GUIContent("JEngine Panel",
                EditorGUIUtility.IconContent("BuildSettings.Editor.Small").image);
            window.Show();
        }

        [MenuItem("JEngine/Open Editor Bundle Cache", priority = 3000)]
        private static void OpenDownloadPath()
        {
            var path = Path.Combine(new DirectoryInfo(Application.dataPath).Parent!.FullName,
                YooAssetSettingsData.GetDefaultYooFolderName());
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("JEngine: Download path is not set.");
                return;
            }

            if (!Directory.Exists(path))
            {
                Debug.LogWarning("JEngine: Download path does not exist.");
                return;
            }

            EditorUtility.RevealInFinder(path);
        }
    }
}