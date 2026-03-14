// PerlinNoiseHelper.cs
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

using UnityEngine;

namespace HotUpdate.Code.Terrain
{
    /// <summary>
    /// Perlin 噪声工具，用于程序化地形高度采样。
    /// </summary>
    public static class PerlinNoiseHelper
    {
        /// <summary>
        /// 采样高度值。
        /// </summary>
        /// <param name="x">世界 X 坐标。</param>
        /// <param name="z">世界 Z 坐标。</param>
        /// <param name="scale">噪声缩放，控制起伏尺度。</param>
        /// <param name="amplitude">高度振幅。</param>
        /// <param name="baseHeight">基准高度。</param>
        /// <param name="seed">随机种子，用于偏移采样。</param>
        /// <returns>高度值（浮点），可转为整数用于体素网格。</returns>
        public static float SampleHeight(
            float x,
            float z,
            float scale = 0.05f,
            float amplitude = 8f,
            float baseHeight = 4f,
            int seed = 0)
        {
            var sampleX = x * scale + seed;
            var sampleZ = z * scale + seed;
            var noise = Mathf.PerlinNoise(sampleX, sampleZ);
            return noise * amplitude + baseHeight;
        }

        /// <summary>
        /// 采样高度值（整数，用于体素网格）。
        /// </summary>
        public static int SampleHeightInt(
            float x,
            float z,
            float scale = 0.05f,
            float amplitude = 8f,
            float baseHeight = 4f,
            int seed = 0)
        {
            var h = SampleHeight(x, z, scale, amplitude, baseHeight, seed);
            return Mathf.Max(0, Mathf.RoundToInt(h));
        }

        /// <summary>
        /// 分形噪声采样（多 octave 叠加，增强细节）。
        /// </summary>
        /// <param name="octaves">叠加层数。</param>
        /// <param name="persistence">每层振幅衰减系数。</param>
        /// <param name="lacunarity">每层频率倍增系数。</param>
        public static float SampleHeightFractal(
            float x,
            float z,
            float scale = 0.05f,
            float amplitude = 8f,
            float baseHeight = 4f,
            int seed = 0,
            int octaves = 3,
            float persistence = 0.5f,
            float lacunarity = 2f)
        {
            var total = 0f;
            var freq = 1f;
            var amp = 1f;
            var maxValue = 0f;

            for (var i = 0; i < octaves; i++)
            {
                var sampleX = (x * scale + seed) * freq + i * 100f;
                var sampleZ = (z * scale + seed) * freq + i * 100f;
                total += Mathf.PerlinNoise(sampleX, sampleZ) * amp;
                maxValue += amp;
                amp *= persistence;
                freq *= lacunarity;
            }

            var noise = total / maxValue;
            return noise * amplitude + baseHeight;
        }

        /// <summary>
        /// 分形噪声采样（整数）。
        /// </summary>
        public static int SampleHeightFractalInt(
            float x,
            float z,
            float scale = 0.05f,
            float amplitude = 8f,
            float baseHeight = 4f,
            int seed = 0,
            int octaves = 3,
            float persistence = 0.5f,
            float lacunarity = 2f)
        {
            var h = SampleHeightFractal(x, z, scale, amplitude, baseHeight, seed, octaves, persistence, lacunarity);
            return Mathf.Max(0, Mathf.RoundToInt(h));
        }
    }
}
