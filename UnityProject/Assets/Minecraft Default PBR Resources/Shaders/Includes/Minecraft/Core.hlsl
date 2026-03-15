#ifndef MINECRAFT_CORE_INCLUDED
#define MINECRAFT_CORE_INCLUDED

#include "Includes/Minecraft/Input.hlsl"

#define SAMPLE_BLOCK_TEXTURE(uv, index) SAMPLE_TEXTURE2D_ARRAY(_BlockTextures, sampler_BlockTextures, uv, index)
#define SAMPLE_BLOCK_ALBEDO(uv, indices) SAMPLE_BLOCK_TEXTURE(uv, indices.x)
#define SAMPLE_BLOCK_NORMAL(uv, indices) SAMPLE_BLOCK_TEXTURE(uv, indices.y)
#define SAMPLE_BLOCK_MER(uv, indices) SAMPLE_BLOCK_TEXTURE(uv, indices.z)

inline half4 EaseIn(half4 a, half4 b, float w)
{
    return a + (b - a) * w * w * w; // 先慢后快
}

inline void HighlightBlock(float3 blockPos, float2 uv, half4 highlightColor, inout half4 color)
{
    float3 delta = blockPos - _TargetBlockPosition;
    float dist = delta.x * delta.x + delta.y * delta.y + delta.z * delta.z;

    UNITY_BRANCH
    if (dist <= 0.01)
    {
        color += highlightColor;

        UNITY_BRANCH
        if (_DigProgress > -1)
        {
            half4 tex = SAMPLE_TEXTURE2D_ARRAY(_DigProgressTextures, sampler_DigProgressTextures, uv, _DigProgress);
            color.rgb *= tex.rgb;
            color.a = saturate(color.a + tex.a);
        }
    }
}

inline half4 GetFungalStateColor(float encodedState)
{
    if (encodedState < 0.2)
    {
        return half4(0, 0, 0, 0);
    }

    if (encodedState < 0.5)
    {
        return _FungalStateColorInfecting;
    }

    if (encodedState < 0.85)
    {
        return _FungalStateColorDamaged;
    }

    return _FungalStateColorCompleted;
}

inline half GetFungalStateStrength(half encodedState)
{
    if (encodedState < 0.2h)
    {
        return 0.0h;
    }

    if (encodedState < 0.5h)
    {
        return 0.45h;
    }

    if (encodedState < 0.85h)
    {
        return 0.75h;
    }

    return 1.0h;
}

inline void ApplyFungalOverlay(float3 blockPos, float3 normalWS, float2 uv, inout half4 color)
{
    if (_FungalMapSize < 1 || _FungalOverlayStrength <= 0.001)
    {
        return;
    }

    float2 gridPos = floor(blockPos.xz) - _FungalMapOriginXZ;
    if (gridPos.x < 0 || gridPos.y < 0 || gridPos.x >= _FungalMapSize || gridPos.y >= _FungalMapSize)
    {
        return;
    }

    float2 stateUv = (gridPos + 0.5) / _FungalMapSize;
    half encodedState = SAMPLE_TEXTURE2D(_FungalStateMap, sampler_FungalStateMap, stateUv).r;
    half4 stateColor = GetFungalStateColor(encodedState);
    if (stateColor.a <= 0.001)
    {
        return;
    }

    half4 overlayTex = SAMPLE_TEXTURE2D(_FungalOverlayTex, sampler_FungalOverlayTex, uv);
    half overlayPattern = dot(overlayTex.rgb, half3(0.299h, 0.587h, 0.114h));
    half overlayMask = max(overlayTex.a, overlayPattern);
    half mask = saturate(overlayMask * stateColor.a * _FungalOverlayStrength);
    if (mask <= 0.001)
    {
        return;
    }

    half3 fungalResult = color.rgb;

    float spatialSeed = dot(floor(blockPos.xz), float2(0.73, 1.11));
    half pulse = 0.5h + 0.5h * sin(_Time.y * 6.0 + spatialSeed * 6.0 + overlayPattern * 4.0);

    float2 crackUv = uv * 18.0 + blockPos.xz * 2.7;
    half crackWave = abs(sin(crackUv.x * 3.1 + crackUv.y * 2.3));
    half crackNoise = frac(sin(dot(crackUv, float2(12.9898, 78.233))) * 43758.5453);
    half crackMask = saturate(smoothstep(0.72, 0.95, crackWave) * (0.45h + crackNoise * 0.55h));

    if (encodedState < 0.5h) // Infecting
    {
        half infectMask = mask * (0.45h + 0.55h * pulse);
        half3 infectColor = lerp(stateColor.rgb * 0.35h, stateColor.rgb * 1.15h, pulse);
        fungalResult = lerp(fungalResult, infectColor, infectMask);
        fungalResult += stateColor.rgb * (0.06h * pulse * infectMask);
    }
    else if (encodedState < 0.85h) // Damaged
    {
        half damageMask = mask * 0.9h;
        half3 damagedBase = lerp(fungalResult, stateColor.rgb * 0.42h, damageMask);
        half crackDarkness = saturate(crackMask * damageMask * 1.1h);
        fungalResult = lerp(damagedBase, damagedBase * 0.3h, crackDarkness);
    }
    else // Completed
    {
        half completeMask = mask * 0.9h;
        half3 completedBase = lerp(fungalResult, stateColor.rgb, completeMask);
        half3 completedGlow = stateColor.rgb * (0.14h + overlayPattern * 0.12h) * completeMask;
        fungalResult = completedBase + completedGlow;
    }

    // Edge crawling animation: detect fungal front by comparing with neighboring state cells.
    half stateStrength = GetFungalStateStrength(encodedState);
    if (stateStrength > 0.001h)
    {
        float texel = 1.0 / _FungalMapSize;
        float2 offsetX = float2(texel, 0.0);
        float2 offsetY = float2(0.0, texel);

        half strengthLeft = GetFungalStateStrength(SAMPLE_TEXTURE2D(_FungalStateMap, sampler_FungalStateMap, saturate(stateUv - offsetX)).r);
        half strengthRight = GetFungalStateStrength(SAMPLE_TEXTURE2D(_FungalStateMap, sampler_FungalStateMap, saturate(stateUv + offsetX)).r);
        half strengthDown = GetFungalStateStrength(SAMPLE_TEXTURE2D(_FungalStateMap, sampler_FungalStateMap, saturate(stateUv - offsetY)).r);
        half strengthUp = GetFungalStateStrength(SAMPLE_TEXTURE2D(_FungalStateMap, sampler_FungalStateMap, saturate(stateUv + offsetY)).r);

        half edgeDelta = max(max(stateStrength - strengthLeft, stateStrength - strengthRight), max(stateStrength - strengthDown, stateStrength - strengthUp));
        half edgeFactor = saturate(edgeDelta * 2.4h);

        half crawlPhase = _Time.y * 7.4h + dot(blockPos.xz, half2(1.7h, -1.3h)) * 1.3h + overlayPattern * 10.0h;
        half crawlBand = smoothstep(0.35h, 1.0h, 0.5h + 0.5h * sin(crawlPhase));
        half edgeMask = edgeFactor * mask * (0.35h + 0.65h * crawlBand);

        half3 edgeTint = lerp(stateColor.rgb * 0.75h, stateColor.rgb * 1.55h, crawlBand);
        fungalResult = lerp(fungalResult, edgeTint, edgeMask);
        fungalResult += edgeTint * (0.08h * edgeMask);
    }

    color.rgb = fungalResult;
}

struct BlockAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    int3 texIndices : TEXCOORD1; // x: albedo, y: normal, z: mer（对应纹理在纹理数组中的索引）
    float3 lights : TEXCOORD2; // x: emission, y: sky_light, z: block_light（均为 [0, 1] 的数字）
    float3 blockPositionWS : TEXCOORD3;
};

#endif // MINECRAFT_CORE_INCLUDED
