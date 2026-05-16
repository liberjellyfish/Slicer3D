#ifndef SDF_COMMON_INCLUDED
#define SDF_COMMON_INCLUDED

#ifndef SDF_CUT_TILE_SIZE
#define SDF_CUT_TILE_SIZE 16
#endif

#ifndef SDF_CUT_TILE_OVERFLOW_BIT
#define SDF_CUT_TILE_OVERFLOW_BIT 0x80000000u
#endif

float SdSphere(float3 p, float3 center, float radius)
{
    return length(p - center) - radius;
}

float SdBox(float3 p, float3 extents)
{
    float3 q = abs(p) - extents;
    return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
}

float SdPlane(float3 p, float3 normal, float offset)
{
    return dot(p, normalize(normal)) + offset;
}

float SdClippedBoxForData(float3 p, float3 extents, float3 planeNormal, float planeOffset)
{
    return max(SdBox(p, extents), SdPlane(p, planeNormal, planeOffset));
}

float BaseShapeMapForData(float3 p, float3 sphereCenter, float sphereRadius, float3 boxExtents, float4 baseCutPlane, float shapeMode)
{
    float sphereDistance = SdSphere(p, sphereCenter, sphereRadius);
    float clippedBoxDistance = SdClippedBoxForData(p, boxExtents, baseCutPlane.xyz, baseCutPlane.w);
    int mode = (int)round(shapeMode);

    if (mode == 0)
    {
        return sphereDistance;
    }

    if (mode == 1)
    {
        return clippedBoxDistance;
    }

    return min(sphereDistance, clippedBoxDistance);
}

int GetSdfCutTileIndex(float2 pixelPosition)
{
    if (_SdfCutTileEnabled <= 0 || _SdfCutTileGridWidth <= 0 || _SdfCutTileGridHeight <= 0)
    {
        return -1;
    }

    int2 tileCoord = int2(floor(pixelPosition / SDF_CUT_TILE_SIZE));
    if (tileCoord.x < 0 || tileCoord.y < 0 || tileCoord.x >= _SdfCutTileGridWidth || tileCoord.y >= _SdfCutTileGridHeight)
    {
        return -1;
    }

    return tileCoord.y * _SdfCutTileGridWidth + tileCoord.x;
}

bool TryGetSdfCutTileRange(int tileIndex, out uint offset, out uint count)
{
    offset = 0u;
    count = 0u;
    if (tileIndex < 0 || _SdfCutTileEnabled <= 0)
    {
        return false;
    }

    uint2 range = _SdfCutTileRanges[tileIndex];
    offset = range.x;
    count = range.y & 0x7fffffffu;
    return (range.y & SDF_CUT_TILE_OVERFLOW_BIT) == 0u;
}

bool IntersectProxyBounds(float3 rayOrigin, float3 rayDir, out float tEnter, out float tExit)
{
    float3 invDir = rcp(rayDir);
    float3 t0 = (_ProxyBoundsMin.xyz - rayOrigin) * invDir;
    float3 t1 = (_ProxyBoundsMax.xyz - rayOrigin) * invDir;
    float3 tMin = min(t0, t1);
    float3 tMax = max(t0, t1);
    tEnter = max(max(tMin.x, tMin.y), tMin.z);
    tExit = min(min(tMax.x, tMax.y), tMax.z);
    return tExit >= max(tEnter, 0.0);
}

float Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float ValueNoise3D(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);

    float n000 = Hash13(i + float3(0.0, 0.0, 0.0));
    float n100 = Hash13(i + float3(1.0, 0.0, 0.0));
    float n010 = Hash13(i + float3(0.0, 1.0, 0.0));
    float n110 = Hash13(i + float3(1.0, 1.0, 0.0));
    float n001 = Hash13(i + float3(0.0, 0.0, 1.0));
    float n101 = Hash13(i + float3(1.0, 0.0, 1.0));
    float n011 = Hash13(i + float3(0.0, 1.0, 1.0));
    float n111 = Hash13(i + float3(1.0, 1.0, 1.0));

    float nx00 = lerp(n000, n100, u.x);
    float nx10 = lerp(n010, n110, u.x);
    float nx01 = lerp(n001, n101, u.x);
    float nx11 = lerp(n011, n111, u.x);
    float nxy0 = lerp(nx00, nx10, u.y);
    float nxy1 = lerp(nx01, nx11, u.y);
    return lerp(nxy0, nxy1, u.z);
}

float Fbm3D(float3 p)
{
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;
    value += ValueNoise3D(p * frequency) * amplitude;
    frequency *= 2.03;
    amplitude *= 0.5;
    value += ValueNoise3D(p * frequency + 17.13) * amplitude;
    frequency *= 2.11;
    amplitude *= 0.5;
    value += ValueNoise3D(p * frequency + 41.71) * amplitude;
    frequency *= 2.07;
    amplitude *= 0.5;
    value += ValueNoise3D(p * frequency + 83.19) * amplitude;
    return saturate(value / 0.9375);
}

float SdfLuminance(float3 color)
{
    return dot(color, float3(0.2126, 0.7152, 0.0722));
}

float SdfHash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float SdfStableVolumeSampleJitter(float2 pixelPosition)
{
    float noise = lerp(0.1, 0.9, SdfHash12(floor(pixelPosition)));
    return lerp(0.5, noise, saturate(_VolumeSampleJitterStrength));
}

float EvaluateScatteringPhase(float3 lightDirWS, float3 viewDirWS)
{
    float g = clamp(_VolumeLightAnisotropy, -0.8, 0.8);
    float g2 = g * g;
    float cosTheta = clamp(dot(-lightDirWS, viewDirWS), -1.0, 1.0);
    float denom = pow(max(1.0 + g2 - 2.0 * g * cosTheta, 1e-3), 1.5);
    return (1.0 - g2) / (12.56637061 * denom);
}

float EvaluateEmptyVolumeStepScale(float sigmaT, float densityDebug, float shapeMaskDebug)
{
    float mediumPresence = max(saturate(densityDebug * 8.0), saturate(shapeMaskDebug));
    float emptyScale = lerp(2.0, 1.0, smoothstep(0.02, 0.28, mediumPresence));
    return sigmaT <= 1e-5 ? emptyScale : 1.0;
}

bool HasVolumeSampleContribution(float sigmaT, float3 emissionRadiance)
{
    return sigmaT > 1e-5 || SdfLuminance(emissionRadiance) > 1e-5;
}

float3 ApplyVolumeDisplayMapping(float3 color)
{
    color = max(color, 0.0);
    color *= max(_VolumeExposure, 0.0);
    return color / (1.0 + color);
}

#endif
