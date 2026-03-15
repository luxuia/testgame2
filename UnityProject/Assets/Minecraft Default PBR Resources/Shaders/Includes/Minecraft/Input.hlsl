#ifndef MINECRAFT_INPUT_INCLUDED
#define MINECRAFT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// 所有方块纹理（包括法线贴图、PBR 贴图）
TEXTURE2D_ARRAY(_BlockTextures); SAMPLER(sampler_BlockTextures);

// 方块挖掘进度贴图
TEXTURE2D_ARRAY(_DigProgressTextures); SAMPLER(sampler_DigProgressTextures);

// 方块的挖掘进度（贴图的索引）
int _DigProgress;

// 当前玩家准心瞄准的方块的世界坐标
float3 _TargetBlockPosition;

// 菌毯叠加贴图（独立于方块主贴图）
TEXTURE2D(_FungalOverlayTex); SAMPLER(sampler_FungalOverlayTex);

// 菌毯状态贴图（R 通道编码状态）
TEXTURE2D(_FungalStateMap); SAMPLER(sampler_FungalStateMap);

// 菌毯状态贴图左下角对应的世界格坐标（XZ）
float2 _FungalMapOriginXZ;

// 菌毯状态贴图的边长（正方形）
float _FungalMapSize;

// 菌毯叠加强度
float _FungalOverlayStrength;

// 菌毯状态色
half4 _FungalStateColorInfecting;
half4 _FungalStateColorDamaged;
half4 _FungalStateColorCompleted;

// 渲染距离，以方块为单位
int _RenderDistance;

// 视野距离，以方块为单位
int _ViewDistance;

// 光照限制
// x - 最小光照级别 [0, 0.5]
// y - 最大光照级别 [0.5, 1]
half2 _LightLimits;

// 白天世界环境的颜色，方块在 XOZ 平面上距离相机越远，颜色越接近该值
half4 _WorldAmbientColorDay;

// 夜晚世界环境的颜色，方块在 XOZ 平面上距离相机越远，颜色越接近该值
half4 _WorldAmbientColorNight;

#endif // MINECRAFT_INPUT_INCLUDED
