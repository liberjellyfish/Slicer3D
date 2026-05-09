Shader "Hidden/Sdf/ScreenSpaceVolume"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "SdfScreenSpaceVolume"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            struct CutPlaneData
            {
                float3 normal;
                float distance;
                float sideSign;
                float3 padding;
            };

            struct SdfSceneShapeData
            {
                float4 worldToObjectRow0;
                float4 worldToObjectRow1;
                float4 worldToObjectRow2;
                float4 sphereCenterRadius;
                float4 boxExtentsShapeMode;
                float4 baseCutPlane;
                float4 cutRangeAndDistanceScale;
            };

            struct VolumeBands
            {
                float finalDistance;
                float cutBand;
            };

            struct VolumeShadowSample
            {
                float geometryVisibility;
                float mediaTransmittance;
                float combinedVisibility;
            };

            struct VolumeTerms
            {
                float3 scattering;
                float transmittance;
                float densityDebug;
                float shadowDebug;
                float geometryShadowDebug;
                float mediaShadowDebug;
                float sigmaADebug;
                float sigmaSDebug;
                float sigmaTDebug;
                float shapeMaskDebug;
                float scatteringVisibility;
            };

            StructuredBuffer<SdfSceneShapeData> _SdfShadowSceneShapes;
            StructuredBuffer<CutPlaneData> _SdfShadowSceneCutPlanes;
            int _SdfShadowSceneShapeCount;
            StructuredBuffer<uint2> _SdfCutTileRanges;
            StructuredBuffer<int> _SdfCutTileIndices;
            int _SdfCutTileEnabled;
            int _SdfCutTileGridWidth;
            int _SdfCutTileGridHeight;
            int _SdfCutTileMaxIndicesPerTile;

            #define SDF_CUT_TILE_SIZE 16
            #define SDF_CUT_TILE_OVERFLOW_BIT 0x80000000u

            float _SdfScreenSpaceVolumeEnabled;
            float _SdfVolumeVisibilityMode;
            float4x4 _SdfVolumeWorldToLocal;
            float4x4 _SdfVolumeLocalToWorld;

            float _VolumeLightEnabled;
            float _VolumeLightIntensity;
            float _VolumeLightDensity;
            float _VolumeLightAnisotropy;
            float _VolumeLightSamples;
            float _VolumeLightMaxStepLength;
            float _VolumeLightShadowStrength;
            float _VolumeLightShadowBias;
            float _VolumeLightSurfaceFadeDistance;
            float _VolumeSurfaceOcclusionStrength;
            float _VolumeSurfaceOcclusionRadius;
            float _VolumeLightPlaneBand;
            float _VolumeLightRemovedDepth;
            float _VolumeLightShapeDepth;
            float _VolumeLightNoiseScale;
            float _VolumeLightNoiseStrength;
            float _VolumeLightNoiseDrift;
            float _VolumeBaseFogDensity;
            float _VolumeHeightFogStrength;
            float _VolumeCutFogBoost;
            float _VolumeNoiseContrast;
            float _VolumeAbsorptionDensity;
            float _VolumeDensityThreshold;
            float _VolumeAlphaClipThreshold;
            float _VolumeEmissionIntensity;
            float4 _VolumeEmissionColor;
            float _VolumeFogShapeMode;
            float4 _VolumeFogShapeCenter;
            float4 _VolumeFogShapeExtents;
            float _VolumeFogShapeRadius;
            float _VolumeFogShapeHeight;
            float _VolumeFogShapeEdgeSoftness;
            float _VolumeFogShapeNoiseErosion;
            float _VolumeFogShapeNoiseScale;
            float _VolumeCloudCoverage;
            float _VolumeCloudSoftness;
            float _VolumeCloudDetailStrength;
            float _VolumeCloudDetailScale;
            float _VolumeCloudWarpStrength;
            float _VolumeCloudLobeCount;
            float4 _VolumeCloudLobeSpread;
            float _VolumeCloudLobeRadius;
            float _VolumeCloudDensityBoost;
            float _VolumeShadowSamples;
            float _VolumeShadowMaxDistance;
            float _VolumePointLightEnabled;
            float4 _VolumePointLightPositionWS;
            float4 _VolumePointLightColor;
            float _VolumePointLightIntensity;
            float _VolumePointLightRange;
            float _VolumeExposure;
            float4 _VolumeColorTint;
            float _MaxDistance;
            float _HitEpsilon;
            float4 _ProxyBoundsMin;
            float4 _ProxyBoundsMax;
            float _DebugView;

            float3 VolumeToWorld(float3 p)
            {
                return mul(_SdfVolumeLocalToWorld, float4(p, 1.0)).xyz;
            }

            float3 WorldToVolume(float3 p)
            {
                return mul(_SdfVolumeWorldToLocal, float4(p, 1.0)).xyz;
            }

            float3 WorldDirToVolume(float3 d)
            {
                return mul((float3x3)_SdfVolumeWorldToLocal, d);
            }

            float3 VolumeDirToWorld(float3 d)
            {
                return mul((float3x3)_SdfVolumeLocalToWorld, d);
            }

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
                if (mode == 0) return sphereDistance;
                if (mode == 1) return clippedBoxDistance;
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

            float ApplySceneCutPlanes(float3 p, float baseDistance, int cutStart, int cutCount, int tileIndex)
            {
                float d = baseDistance;
                uint tileOffset;
                uint tileCount;
                if (TryGetSdfCutTileRange(tileIndex, tileOffset, tileCount))
                {
                    int cutEnd = cutStart + cutCount;
                    [loop]
                    for (uint tileCutIndex = 0u; tileCutIndex < tileCount; tileCutIndex++)
                    {
                        int cutIndex = _SdfCutTileIndices[tileOffset + tileCutIndex];
                        if (cutIndex < cutStart || cutIndex >= cutEnd)
                        {
                            continue;
                        }

                        CutPlaneData cutPlane = _SdfShadowSceneCutPlanes[cutIndex];
                        float planeSdf = dot(p, cutPlane.normal) + cutPlane.distance;
                        d = max(d, -(planeSdf * cutPlane.sideSign));
                    }

                    return d;
                }

                [loop]
                for (int i = 0; i < cutCount; i++)
                {
                    CutPlaneData cutPlane = _SdfShadowSceneCutPlanes[cutStart + i];
                    float planeSdf = dot(p, cutPlane.normal) + cutPlane.distance;
                    d = max(d, -(planeSdf * cutPlane.sideSign));
                }
                return d;
            }

            float EvaluateSceneCutBandForShape(float3 p, float baseDistance, int cutStart, int cutCount, float distanceScale, int tileIndex)
            {
                if (cutCount <= 0)
                {
                    return 0.0;
                }

                float dominantHalfSpace = -1e20;
                float dominantPlaneDistance = 1e20;
                uint tileOffset;
                uint tileCount;
                if (TryGetSdfCutTileRange(tileIndex, tileOffset, tileCount))
                {
                    int cutEnd = cutStart + cutCount;
                    [loop]
                    for (uint tileCutIndex = 0u; tileCutIndex < tileCount; tileCutIndex++)
                    {
                        int cutIndex = _SdfCutTileIndices[tileOffset + tileCutIndex];
                        if (cutIndex < cutStart || cutIndex >= cutEnd)
                        {
                            continue;
                        }

                        CutPlaneData cutPlane = _SdfShadowSceneCutPlanes[cutIndex];
                        float planeSdf = dot(p, cutPlane.normal) + cutPlane.distance;
                        float halfSpaceSdf = -(planeSdf * cutPlane.sideSign);
                        if (halfSpaceSdf > dominantHalfSpace)
                        {
                            dominantHalfSpace = halfSpaceSdf;
                            dominantPlaneDistance = planeSdf;
                        }
                    }

                    if (dominantHalfSpace <= -1e19)
                    {
                        return 0.0;
                    }
                }
                else
                {
                    [loop]
                    for (int i = 0; i < cutCount; i++)
                    {
                        CutPlaneData cutPlane = _SdfShadowSceneCutPlanes[cutStart + i];
                        float planeSdf = dot(p, cutPlane.normal) + cutPlane.distance;
                        float halfSpaceSdf = -(planeSdf * cutPlane.sideSign);
                        if (halfSpaceSdf > dominantHalfSpace)
                        {
                            dominantHalfSpace = halfSpaceSdf;
                            dominantPlaneDistance = planeSdf;
                        }
                    }
                }

                float scaledBaseDistance = baseDistance * distanceScale;
                float scaledPlaneDistance = dominantPlaneDistance * distanceScale;
                float scaledHalfSpace = dominantHalfSpace * distanceScale;
                float planeBand = 1.0 - smoothstep(0.0, max(_VolumeLightPlaneBand, _HitEpsilon), abs(scaledPlaneDistance));
                float originalInterior = saturate((-scaledBaseDistance) / max(_VolumeLightRemovedDepth, _HitEpsilon));
                float removedSide = smoothstep(0.0, max(_VolumeLightRemovedDepth, _HitEpsilon), scaledHalfSpace);
                return planeBand * originalInterior * lerp(0.65, 1.0, removedSide);
            }

            float ShadowSceneMap(float3 volumeP, int tileIndex)
            {
                if (_SdfShadowSceneShapeCount <= 0)
                {
                    return 1e20;
                }

                float3 pWS = VolumeToWorld(volumeP);
                float d = 1e20;
                [loop]
                for (int i = 0; i < _SdfShadowSceneShapeCount; i++)
                {
                    SdfSceneShapeData shape = _SdfShadowSceneShapes[i];
                    float4 pWS4 = float4(pWS, 1.0);
                    float3 pShapeOS = float3(
                        dot(shape.worldToObjectRow0, pWS4),
                        dot(shape.worldToObjectRow1, pWS4),
                        dot(shape.worldToObjectRow2, pWS4));
                    float shapeDistance = BaseShapeMapForData(
                        pShapeOS,
                        shape.sphereCenterRadius.xyz,
                        shape.sphereCenterRadius.w,
                        shape.boxExtentsShapeMode.xyz,
                        shape.baseCutPlane,
                        shape.boxExtentsShapeMode.w);
                    int cutStart = (int)round(shape.cutRangeAndDistanceScale.x);
                    int cutCount = (int)round(shape.cutRangeAndDistanceScale.y);
                    float distanceScale = max(shape.cutRangeAndDistanceScale.z, 1e-4);
                    shapeDistance = ApplySceneCutPlanes(pShapeOS, shapeDistance, cutStart, cutCount, tileIndex) * distanceScale;
                    d = min(d, shapeDistance);
                }
                return d;
            }

            VolumeBands EvaluateVolumeMediumBands(float3 volumeP, int tileIndex)
            {
                VolumeBands bands;
                bands.finalDistance = 1e20;
                bands.cutBand = 0.0;

                if (_SdfShadowSceneShapeCount <= 0)
                {
                    return bands;
                }

                float3 pWS = VolumeToWorld(volumeP);
                [loop]
                for (int i = 0; i < _SdfShadowSceneShapeCount; i++)
                {
                    SdfSceneShapeData shape = _SdfShadowSceneShapes[i];
                    float4 pWS4 = float4(pWS, 1.0);
                    float3 pShapeOS = float3(
                        dot(shape.worldToObjectRow0, pWS4),
                        dot(shape.worldToObjectRow1, pWS4),
                        dot(shape.worldToObjectRow2, pWS4));
                    float baseDistance = BaseShapeMapForData(
                        pShapeOS,
                        shape.sphereCenterRadius.xyz,
                        shape.sphereCenterRadius.w,
                        shape.boxExtentsShapeMode.xyz,
                        shape.baseCutPlane,
                        shape.boxExtentsShapeMode.w);
                    int cutStart = (int)round(shape.cutRangeAndDistanceScale.x);
                    int cutCount = (int)round(shape.cutRangeAndDistanceScale.y);
                    float distanceScale = max(shape.cutRangeAndDistanceScale.z, 1e-4);
                    float finalDistance = ApplySceneCutPlanes(pShapeOS, baseDistance, cutStart, cutCount, tileIndex) * distanceScale;
                    bands.finalDistance = min(bands.finalDistance, finalDistance);
                    bands.cutBand = max(bands.cutBand, EvaluateSceneCutBandForShape(pShapeOS, baseDistance, cutStart, cutCount, distanceScale, tileIndex));
                }

                return bands;
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

            float ObjectRayUnitToWorldLength(float3 rayOrigin, float3 rayDir)
            {
                return max(length(VolumeToWorld(rayOrigin + rayDir) - VolumeToWorld(rayOrigin)), 1e-4);
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
                float n000 = Hash13(i);
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
                return lerp(lerp(nx00, nx10, u.y), lerp(nx01, nx11, u.y), u.z);
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

            float EvaluateCloudLobeMask(float3 localP, float3 extents)
            {
                float3 normalizedP = localP / max(extents, float3(1e-4, 1e-4, 1e-4));
                float3 spread = max(_VolumeCloudLobeSpread.xyz, float3(0.0, 0.0, 0.0));
                float baseRadius = max(_VolumeCloudLobeRadius, 0.05);
                int lobeCount = min(max((int)round(_VolumeCloudLobeCount), 1), 24);
                float lobeMask = 0.0;

                [loop]
                for (int lobeIndex = 0; lobeIndex < 24; lobeIndex++)
                {
                    if (lobeIndex >= lobeCount) break;
                    float index = (float)lobeIndex;
                    float3 seed = float3(index * 19.17 + 3.11, index * 41.73 + 7.61, index * 73.31 + 13.97);
                    float3 center = float3(ValueNoise3D(seed + 11.3), ValueNoise3D(seed + 29.7), ValueNoise3D(seed + 53.1)) * 2.0 - 1.0;
                    center *= spread;
                    center.y *= 0.75;
                    float radius = baseRadius * lerp(0.68, 1.28, ValueNoise3D(seed + 91.4));
                    float3 lobeDelta = (normalizedP - center) / max(radius, 1e-4);
                    lobeMask = max(lobeMask, 1.0 - smoothstep(0.55, 1.18, length(lobeDelta)));
                }

                return saturate(lobeMask);
            }

            float EvaluateProxyBoundaryFade(float3 p)
            {
                float3 distanceToMin = p - _ProxyBoundsMin.xyz;
                float3 distanceToMax = _ProxyBoundsMax.xyz - p;
                float minDistance = min(min(distanceToMin.x, distanceToMin.y), distanceToMin.z);
                float maxDistance = min(min(distanceToMax.x, distanceToMax.y), distanceToMax.z);
                float boundaryDistance = min(minDistance, maxDistance);
                return smoothstep(0.0, max(_VolumeLightSurfaceFadeDistance, _HitEpsilon), boundaryDistance);
            }

            float SdVolumeEllipsoid(float3 p, float3 extents)
            {
                float3 safeExtents = max(extents, float3(1e-4, 1e-4, 1e-4));
                float3 q = p / safeExtents;
                float minExtent = min(safeExtents.x, min(safeExtents.y, safeExtents.z));
                return (length(q) - 1.0) * minExtent;
            }

            float SdVolumeCapsuleY(float3 p, float radius, float height)
            {
                float safeRadius = max(radius, 1e-4);
                float halfSegment = max(height * 0.5 - safeRadius, 0.0);
                p.y -= clamp(p.y, -halfSegment, halfSegment);
                return length(p) - safeRadius;
            }

            float EvaluateVolumeFogShapeMask(float3 p)
            {
                int mode = (int)round(_VolumeFogShapeMode);
                if (mode <= 0)
                {
                    return 1.0;
                }

                float3 localP = p - _VolumeFogShapeCenter.xyz;
                float3 extents = max(_VolumeFogShapeExtents.xyz, float3(0.01, 0.01, 0.01));
                float signedDistance = mode == 2
                    ? SdVolumeCapsuleY(localP, _VolumeFogShapeRadius, _VolumeFogShapeHeight)
                    : SdVolumeEllipsoid(localP, extents);

                if (mode == 3)
                {
                    float edgeSoftness = max(_VolumeFogShapeEdgeSoftness, _HitEpsilon);
                    float baseShapeMask = 1.0 - smoothstep(0.0, edgeSoftness, signedDistance);
                    float lobeMask = EvaluateCloudLobeMask(localP, extents);
                    float height01 = saturate(localP.y / max(extents.y, 1e-4) * 0.5 + 0.5);
                    float heightFade = smoothstep(0.02, 0.18, height01) * (1.0 - smoothstep(0.82, 0.98, height01));
                    float3 drift = float3(0.29, 0.47, 0.61) * (_Time.y * _VolumeLightNoiseDrift);
                    float baseScale = max(_VolumeFogShapeNoiseScale, 0.1);
                    float maxExtent = max(extents.x, max(extents.y, extents.z));
                    float3 warpSeed = localP * baseScale + drift;
                    float3 warp = float3(ValueNoise3D(warpSeed + 13.1), ValueNoise3D(warpSeed + 37.7), ValueNoise3D(warpSeed + 71.3)) * 2.0 - 1.0;
                    float3 cloudP = (localP + warp * maxExtent * 0.2 * saturate(_VolumeCloudWarpStrength / 1.5)) * baseScale + drift;
                    float lowFrequency = Fbm3D(cloudP);
                    float detail = Fbm3D(localP * max(_VolumeCloudDetailScale, 0.1) + drift * 1.7);
                    float coverageThreshold = lerp(0.78, 0.28, saturate(_VolumeCloudCoverage));
                    float cloudMask = smoothstep(coverageThreshold - max(_VolumeCloudSoftness, 0.01), coverageThreshold + max(_VolumeCloudSoftness, 0.01), lowFrequency);
                    float detailErosion = smoothstep(0.42, 0.92, detail) * saturate(_VolumeCloudDetailStrength);
                    float edgeWeight = 1.0 - smoothstep(-edgeSoftness * 2.5, -edgeSoftness * 0.25, signedDistance);
                    cloudMask *= 1.0 - detailErosion * lerp(0.35, 0.85, edgeWeight);
                    float cloudInfluence = saturate(0.45 + saturate(_VolumeFogShapeNoiseErosion) * 0.75);
                    float lobeBody = saturate(max(lobeMask, baseShapeMask * 0.25));
                    return saturate(lerp(baseShapeMask, lobeBody * cloudMask, cloudInfluence) * heightFade);
                }

                float erosion = saturate(_VolumeFogShapeNoiseErosion);
                if (erosion > 0.0)
                {
                    float3 noiseP = localP * max(_VolumeFogShapeNoiseScale, 0.1);
                    noiseP += float3(0.29, 0.47, 0.61) * (_Time.y * _VolumeLightNoiseDrift);
                    signedDistance += (ValueNoise3D(noiseP) * 2.0 - 1.0) * max(_VolumeFogShapeEdgeSoftness, _HitEpsilon) * erosion * 1.75;
                }

                return 1.0 - smoothstep(0.0, max(_VolumeFogShapeEdgeSoftness, _HitEpsilon), signedDistance);
            }

            void GetParticipatingMedia(float3 samplePosition, out float sigmaA, out float sigmaS, out float sigmaT, out float3 emissionRadiance, out float shapeMaskDebug, out float densityDebug, int tileIndex)
            {
                VolumeBands mediumBands = EvaluateVolumeMediumBands(samplePosition, tileIndex);
                float boundaryFade = EvaluateProxyBoundaryFade(samplePosition);
                float volumeShapeMask = EvaluateVolumeFogShapeMask(samplePosition);
                shapeMaskDebug = saturate(volumeShapeMask);
                float shapeBand = exp(-abs(mediumBands.finalDistance) / max(_VolumeLightShapeDepth, _HitEpsilon));
                float cutBand = mediumBands.cutBand;

                float heightSpan = max(_ProxyBoundsMax.y - _ProxyBoundsMin.y, _HitEpsilon);
                float height01 = saturate((samplePosition.y - _ProxyBoundsMin.y) / heightSpan);
                float heightFog = pow(1.0 - height01, 1.6) * _VolumeHeightFogStrength;
                float localVolumeMask = saturate(max(shapeBand * 1.15, cutBand));

                float3 noisePosition = samplePosition * _VolumeLightNoiseScale + float3(0.37, 0.19, 0.53) * (_Time.y * _VolumeLightNoiseDrift);
                float contrastPivot = saturate((ValueNoise3D(noisePosition) - 0.5) * _VolumeNoiseContrast + 0.5);
                float contrastNoise = smoothstep(0.2, 0.95, contrastPivot);
                float noiseMask = lerp(1.0, 0.45 + contrastNoise * 0.95, saturate(_VolumeLightNoiseStrength));

                float cloudCore = smoothstep(0.16, 0.82, volumeShapeMask);
                cloudCore *= cloudCore;
                float cutCore = smoothstep(0.05, 0.42, cutBand);
                float baseFog = _VolumeBaseFogDensity * cloudCore;
                float supportedHeightFog = heightFog * cloudCore;
                float supportedShapeBand = shapeBand * cloudCore;
                float cloudBodyDensity = cloudCore * max(_VolumeCloudDensityBoost, 0.0);
                float cutDensity = cutCore * _VolumeCutFogBoost;
                float density = (baseFog + supportedHeightFog * localVolumeMask + supportedShapeBand * 0.06 + cloudBodyDensity + cutDensity) * noiseMask * boundaryFade;
                density = density > max(_VolumeDensityThreshold, 0.0) ? density : 0.0;
                densityDebug = saturate(density);

                sigmaA = max(0.0, density * _VolumeAbsorptionDensity);
                sigmaS = max(0.0, density * _VolumeLightDensity);
                sigmaT = max(0.0, sigmaA + sigmaS);
                emissionRadiance = density * max(_VolumeEmissionIntensity, 0.0) * _VolumeEmissionColor.rgb;
            }

            float SdfLuminance(float3 color)
            {
                return dot(color, float3(0.2126, 0.7152, 0.0722));
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

            float EvaluateScatteringPhase(float3 lightDirWS, float3 viewDirWS)
            {
                float g = clamp(_VolumeLightAnisotropy, -0.8, 0.8);
                float g2 = g * g;
                float cosTheta = clamp(dot(-lightDirWS, viewDirWS), -1.0, 1.0);
                float denom = pow(max(1.0 + g2 - 2.0 * g * cosTheta, 1e-3), 1.5);
                return (1.0 - g2) / (12.56637061 * denom);
            }

            float TraceVolumeGeometryVisibility(float3 shadowOrigin, float3 lightDir, float maxShadowDistance)
            {
                if (maxShadowDistance <= _HitEpsilon || _SdfShadowSceneShapeCount <= 0)
                {
                    return 1.0;
                }

                float minStep = max(_HitEpsilon * 2.0, 0.001);
                float maxStep = max(minStep, _VolumeLightMaxStepLength * 1.5);
                float t = minStep;
                [loop]
                for (int stepIndex = 0; stepIndex < 64; stepIndex++)
                {
                    if (stepIndex >= (int)max(_VolumeShadowSamples, 4.0) || t >= maxShadowDistance) break;
                    float h = ShadowSceneMap(shadowOrigin + lightDir * t, -1);
                    if (h < _HitEpsilon) return 0.0;
                    t += clamp(h, minStep, maxStep);
                }
                return 1.0;
            }

            float EvaluateSceneSurfaceOcclusion(float3 samplePosition)
            {
                if (_SdfShadowSceneShapeCount <= 0 || _VolumeSurfaceOcclusionStrength <= 0.0)
                {
                    return 1.0;
                }

                float radius = max(_VolumeSurfaceOcclusionRadius, _HitEpsilon);
                float surfaceDistance = max(ShadowSceneMap(samplePosition, -1), 0.0);
                float proximity = 1.0 - smoothstep(0.0, radius, surfaceDistance);
                return saturate(1.0 - proximity * saturate(_VolumeSurfaceOcclusionStrength));
            }

            float TraceVolumeMediaTransmittance(float3 shadowOrigin, float3 lightDir, float maxShadowDistance)
            {
                if (maxShadowDistance <= _HitEpsilon)
                {
                    return 1.0;
                }

                float shadowMaxStepLength = max(_VolumeLightMaxStepLength * 1.25, _HitEpsilon);
                int requestedShadowStepCount = max((int)_VolumeShadowSamples, 4);
                int distanceShadowStepCount = max((int)ceil(maxShadowDistance / shadowMaxStepLength), 1);
                int shadowStepCount = min(max(requestedShadowStepCount, distanceShadowStepCount), 64);
                float stepLength = maxShadowDistance / shadowStepCount;
                float worldUnitsPerVolumeUnit = ObjectRayUnitToWorldLength(shadowOrigin, lightDir);
                float transmittance = 1.0;
                float t = 0.0;

                [loop]
                for (int stepIndex = 0; stepIndex < 64; stepIndex++)
                {
                    if (stepIndex >= shadowStepCount || t >= maxShadowDistance) break;
                    float currentStepLength = min(stepLength, maxShadowDistance - t);
                    float3 p = shadowOrigin + lightDir * (t + currentStepLength * 0.5);
                    float sigmaA, sigmaS, sigmaT, shapeMaskDebug, densityDebug;
                    float3 emissionRadiance;
                    GetParticipatingMedia(p, sigmaA, sigmaS, sigmaT, emissionRadiance, shapeMaskDebug, densityDebug, -1);

                    if (!HasVolumeSampleContribution(sigmaT, emissionRadiance))
                    {
                        t += min(currentStepLength * EvaluateEmptyVolumeStepScale(sigmaT, densityDebug, shapeMaskDebug), maxShadowDistance - t);
                        continue;
                    }

                    transmittance *= exp(-sigmaT * currentStepLength * worldUnitsPerVolumeUnit);
                    t += currentStepLength;
                    if (transmittance < 0.01) break;
                }

                return saturate(transmittance);
            }

            VolumeShadowSample EvaluateVolumeLightShadow(float3 samplePosition, float3 lightDir, float requestedMaxDistance)
            {
                VolumeShadowSample shadowSample;
                shadowSample.geometryVisibility = 1.0;
                shadowSample.mediaTransmittance = 1.0;
                shadowSample.combinedVisibility = 1.0;

                if (_VolumeLightShadowStrength <= 0.0)
                {
                    return shadowSample;
                }

                float3 shadowOrigin = samplePosition + lightDir * max(_VolumeLightShadowBias, _HitEpsilon);
                float tEnter, tExit;
                if (!IntersectProxyBounds(shadowOrigin, lightDir, tEnter, tExit))
                {
                    return shadowSample;
                }

                float maxShadowDistance = min(min(requestedMaxDistance, max(_VolumeShadowMaxDistance, _HitEpsilon)), max(tExit, 0.0));
                shadowSample.geometryVisibility = TraceVolumeGeometryVisibility(shadowOrigin, lightDir, maxShadowDistance);
                if (shadowSample.geometryVisibility <= 0.0 && (int)round(_DebugView) != 12)
                {
                    shadowSample.combinedVisibility = lerp(1.0, 0.0, saturate(_VolumeLightShadowStrength));
                    return shadowSample;
                }

                shadowSample.mediaTransmittance = TraceVolumeMediaTransmittance(shadowOrigin, lightDir, maxShadowDistance);
                shadowSample.combinedVisibility = lerp(1.0, saturate(shadowSample.geometryVisibility * shadowSample.mediaTransmittance), saturate(_VolumeLightShadowStrength));
                return shadowSample;
            }

            float TraceSceneSurfaceEnd(float3 rayOrigin, float3 rayDir, float segmentStart, float segmentEnd, int tileIndex)
            {
                if (_SdfShadowSceneShapeCount <= 0)
                {
                    return segmentEnd;
                }

                float t = max(segmentStart, 0.0);
                [loop]
                for (int stepIndex = 0; stepIndex < 96; stepIndex++)
                {
                    if (t >= segmentEnd)
                    {
                        break;
                    }

                    float h = ShadowSceneMap(rayOrigin + rayDir * t, tileIndex);
                    if (h < _HitEpsilon)
                    {
                        return t;
                    }

                    t += max(h, _HitEpsilon * 0.5);
                }

                return segmentEnd;
            }

            VolumeTerms EvaluateVolumeLighting(float3 rayOrigin, float3 rayDir, float3 rayDirWS, float segmentStart, float segmentEnd, float3 mainLightDirWS, float3 mainLightColor, int tileIndex)
            {
                VolumeTerms terms;
                terms.scattering = 0.0;
                terms.transmittance = 1.0;
                terms.densityDebug = 0.0;
                terms.shadowDebug = 1.0;
                terms.geometryShadowDebug = 1.0;
                terms.mediaShadowDebug = 1.0;
                terms.sigmaADebug = 0.0;
                terms.sigmaSDebug = 0.0;
                terms.sigmaTDebug = 0.0;
                terms.shapeMaskDebug = 0.0;
                terms.scatteringVisibility = 0.0;

                if (_VolumeLightEnabled <= 0.0 || _VolumeLightSamples < 1.0)
                {
                    return terms;
                }

                float segmentLength = max(segmentEnd - max(segmentStart, 0.0), 0.0);
                if (segmentLength <= _HitEpsilon)
                {
                    return terms;
                }

                int requestedSampleCount = max((int)_VolumeLightSamples, 1);
                int distanceSampleCount = max((int)ceil(segmentLength / max(_VolumeLightMaxStepLength, _HitEpsilon)), 1);
                int sampleCount = min(max(requestedSampleCount, distanceSampleCount), 96);
                float stepLength = segmentLength / sampleCount;
                float worldUnitsPerVolumeUnit = ObjectRayUnitToWorldLength(rayOrigin, rayDir);
                float3 viewDirWS = normalize(-rayDirWS);
                float mainLightPhase = EvaluateScatteringPhase(mainLightDirWS, viewDirWS);
                float3 mainLightDir = normalize(WorldDirToVolume(mainLightDirWS));
                float3 pointLightPosition = WorldToVolume(_VolumePointLightPositionWS.xyz);
                float mainLightLuminance = SdfLuminance(mainLightColor);
                float transmittance = 1.0;
                float shadowAccumulation = 0.0;
                float geometryShadowAccumulation = 0.0;
                float mediaShadowAccumulation = 0.0;
                float contributingSamples = 0.0;
                float scatteringVisibilityAccumulation = 0.0;
                float sampleT = segmentStart;

                [loop]
                for (int sampleIndex = 0; sampleIndex < 96; sampleIndex++)
                {
                    if (sampleIndex >= sampleCount || sampleT >= segmentEnd) break;
                    float currentStepLength = min(stepLength, segmentEnd - sampleT);
                    float3 samplePosition = rayOrigin + rayDir * (sampleT + currentStepLength * 0.5);
                    float sigmaA, sigmaS, sigmaT, shapeMaskDebug, densityDebug;
                    float3 emissionRadiance;
                    GetParticipatingMedia(samplePosition, sigmaA, sigmaS, sigmaT, emissionRadiance, shapeMaskDebug, densityDebug, tileIndex);
                    if (!HasVolumeSampleContribution(sigmaT, emissionRadiance))
                    {
                        sampleT += min(currentStepLength * EvaluateEmptyVolumeStepScale(sigmaT, densityDebug, shapeMaskDebug), segmentEnd - sampleT);
                        continue;
                    }

                    float3 samplePositionWS = VolumeToWorld(samplePosition);
                    float surfaceOcclusion = EvaluateSceneSurfaceOcclusion(samplePosition);
                    float3 sourceRadiance = 0.0;
                    float weightedShadow = 0.0;
                    float weightedGeometryShadow = 0.0;
                    float weightedMediaShadow = 0.0;
                    float sourceWeight = 0.0;

                    if (mainLightLuminance * sigmaS * max(_VolumeLightIntensity, 0.0) > 1e-4)
                    {
                        VolumeShadowSample mainShadow = EvaluateVolumeLightShadow(samplePosition, mainLightDir, _VolumeShadowMaxDistance);
                        float mainWeight = max(mainLightLuminance, 1e-4);
                        sourceRadiance += mainLightColor * mainShadow.combinedVisibility * mainLightPhase;
                        weightedShadow += mainShadow.combinedVisibility * mainWeight;
                        weightedGeometryShadow += mainShadow.geometryVisibility * mainWeight;
                        weightedMediaShadow += mainShadow.mediaTransmittance * mainWeight;
                        sourceWeight += mainWeight;
                    }

                    if (_VolumePointLightEnabled > 0.5 && _VolumePointLightIntensity > 0.0 && _VolumePointLightRange > _HitEpsilon)
                    {
                        float3 toPointLightWS = _VolumePointLightPositionWS.xyz - samplePositionWS;
                        float pointDistanceSq = max(dot(toPointLightWS, toPointLightWS), 1e-4);
                        float pointDistance = sqrt(pointDistanceSq);
                        float pointRangeAttenuation = saturate(1.0 - pointDistance / max(_VolumePointLightRange, _HitEpsilon));
                        if (pointRangeAttenuation > 0.0)
                        {
                            float3 pointLightDirWS = toPointLightWS / pointDistance;
                            float3 pointLightDir = normalize(WorldDirToVolume(pointLightDirWS));
                            float pointPhase = EvaluateScatteringPhase(pointLightDirWS, viewDirWS);
                            float attenuation = pointRangeAttenuation * pointRangeAttenuation / pointDistanceSq;
                            float3 pointRadiance = _VolumePointLightColor.rgb * (_VolumePointLightIntensity * attenuation);
                            float pointLuminance = SdfLuminance(pointRadiance);
                            if (pointLuminance * sigmaS * max(_VolumeLightIntensity, 0.0) > 1e-4)
                            {
                                VolumeShadowSample pointShadow = EvaluateVolumeLightShadow(samplePosition, pointLightDir, length(pointLightPosition - samplePosition));
                                float pointWeight = max(pointLuminance, 1e-4);
                                sourceRadiance += pointRadiance * pointShadow.combinedVisibility * pointPhase;
                                weightedShadow += pointShadow.combinedVisibility * pointWeight;
                                weightedGeometryShadow += pointShadow.geometryVisibility * pointWeight;
                                weightedMediaShadow += pointShadow.mediaTransmittance * pointWeight;
                                sourceWeight += pointWeight;
                            }
                        }
                    }

                    float currentStepLengthWS = currentStepLength * worldUnitsPerVolumeUnit;
                    float segmentTransmittance = exp(-sigmaT * currentStepLengthWS);
                    float3 sourceTerm = (sourceRadiance * surfaceOcclusion) * max(_VolumeLightIntensity, 0.0) * sigmaS + emissionRadiance * surfaceOcclusion;
                    float sourceIntegral = sigmaT > 1e-5 ? (1.0 - segmentTransmittance) / sigmaT : currentStepLengthWS;
                    terms.scattering += transmittance * sourceTerm * sourceIntegral;
                    scatteringVisibilityAccumulation += transmittance * SdfLuminance(sourceTerm) * currentStepLengthWS;
                    transmittance *= segmentTransmittance;

                    terms.densityDebug = max(terms.densityDebug, densityDebug);
                    terms.sigmaADebug = max(terms.sigmaADebug, sigmaA);
                    terms.sigmaSDebug = max(terms.sigmaSDebug, sigmaS);
                    terms.sigmaTDebug = max(terms.sigmaTDebug, sigmaT);
                    terms.shapeMaskDebug = max(terms.shapeMaskDebug, shapeMaskDebug);
                    if (sourceWeight > 0.0)
                    {
                        float inverseSourceWeight = rcp(max(sourceWeight, 1e-4));
                        shadowAccumulation += weightedShadow * inverseSourceWeight;
                        geometryShadowAccumulation += weightedGeometryShadow * inverseSourceWeight;
                        mediaShadowAccumulation += weightedMediaShadow * inverseSourceWeight;
                        contributingSamples += 1.0;
                    }

                    sampleT += currentStepLength;
                    if (transmittance < 0.01) break;
                }

                terms.scattering *= _VolumeColorTint.rgb;
                terms.transmittance = saturate(transmittance);
                terms.sigmaADebug = saturate(terms.sigmaADebug);
                terms.sigmaSDebug = saturate(terms.sigmaSDebug);
                terms.sigmaTDebug = saturate(terms.sigmaTDebug);
                terms.shapeMaskDebug = saturate(terms.shapeMaskDebug);
                terms.shadowDebug = contributingSamples > 0.0 ? saturate(shadowAccumulation / contributingSamples) : 1.0;
                terms.geometryShadowDebug = contributingSamples > 0.0 ? saturate(geometryShadowAccumulation / contributingSamples) : 1.0;
                terms.mediaShadowDebug = contributingSamples > 0.0 ? saturate(mediaShadowAccumulation / contributingSamples) : 1.0;
                terms.scatteringVisibility = saturate(scatteringVisibilityAccumulation * max(_VolumeExposure, 0.0));
                return terms;
            }

            float3 ApplyVolumeDisplayMapping(float3 color)
            {
                color = max(color, 0.0) * max(_VolumeExposure, 0.0);
                return color / (1.0 + color);
            }

            float3 EvaluateDebugView(VolumeTerms terms)
            {
                int debugView = (int)round(_DebugView);
                if (debugView == 7) return saturate(terms.densityDebug).xxx;
                if (debugView == 8) return saturate(terms.transmittance).xxx;
                if (debugView == 9) return saturate(terms.shadowDebug).xxx;
                if (debugView == 10) return saturate(terms.densityDebug * 0.45 + (1.0 - terms.transmittance) * 0.3 + (1.0 - terms.shadowDebug) * 0.15 + terms.sigmaTDebug * 0.1).xxx;
                if (debugView == 11) return saturate(terms.geometryShadowDebug).xxx;
                if (debugView == 12) return saturate(terms.mediaShadowDebug).xxx;
                if (debugView == 13) return saturate(terms.sigmaADebug).xxx;
                if (debugView == 14) return saturate(terms.sigmaSDebug).xxx;
                if (debugView == 15) return saturate(terms.sigmaTDebug).xxx;
                if (debugView == 17) return saturate(terms.shapeMaskDebug).xxx;
                return float3(-1.0, -1.0, -1.0);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord.xy;
                int sdfCutTileIndex = GetSdfCutTileIndex(uv * _ScreenParams.xy);
                float4 sourceColor = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, _BlitMipLevel);
                bool wantsDebug = _DebugView > 0.5;

                if (_SdfScreenSpaceVolumeEnabled <= 0.5)
                {
                    return sourceColor;
                }

                float rawDepth = SampleSceneDepth(uv);
                float3 depthWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 cameraWS = GetCameraPositionWS();
                float3 rayDirWS = normalize(depthWS - cameraWS);

                #if UNITY_REVERSED_Z
                bool hasSceneDepth = rawDepth > 0.0001;
                #else
                bool hasSceneDepth = rawDepth < 0.9999;
                #endif

                float3 rayOrigin = WorldToVolume(cameraWS);
                float3 rayDir = normalize(WorldDirToVolume(rayDirWS));
                float tEnter, tExit;
                if (!IntersectProxyBounds(rayOrigin, rayDir, tEnter, tExit))
                {
                    return wantsDebug ? float4(0.0, 0.0, 0.0, sourceColor.a) : sourceColor;
                }

                float segmentStart = max(tEnter, 0.0);
                float segmentEnd = min(tExit, _MaxDistance);
                if (hasSceneDepth)
                {
                    float3 depthVolume = WorldToVolume(depthWS);
                    float depthT = dot(depthVolume - rayOrigin, rayDir);
                    segmentEnd = min(segmentEnd, depthT);
                }

                if (segmentEnd <= segmentStart + _HitEpsilon)
                {
                    return wantsDebug ? float4(0.0, 0.0, 0.0, sourceColor.a) : sourceColor;
                }

                segmentEnd = TraceSceneSurfaceEnd(rayOrigin, rayDir, segmentStart, segmentEnd, sdfCutTileIndex);
                if (segmentEnd <= segmentStart + _HitEpsilon)
                {
                    return wantsDebug ? float4(0.0, 0.0, 0.0, sourceColor.a) : sourceColor;
                }

                float3 volumeReferenceWS = VolumeToWorld(rayOrigin + rayDir * segmentStart);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(volumeReferenceWS));
                VolumeTerms terms = EvaluateVolumeLighting(
                    rayOrigin,
                    rayDir,
                    rayDirWS,
                    segmentStart,
                    segmentEnd,
                    normalize(mainLight.direction),
                    mainLight.color,
                    sdfCutTileIndex);

                float3 debugColor = EvaluateDebugView(terms);
                if (debugColor.x >= 0.0)
                {
                    return float4(debugColor, sourceColor.a);
                }

                float3 mappedScattering = ApplyVolumeDisplayMapping(terms.scattering);
                float scatteringLuminance = dot(mappedScattering, float3(0.2126, 0.7152, 0.0722));
                float litVisibility = smoothstep(
                    max(_VolumeAlphaClipThreshold, 0.0),
                    max(_VolumeAlphaClipThreshold + 0.12, 0.13),
                    max(scatteringLuminance, terms.scatteringVisibility * 0.35));
                float extinctionAlpha = saturate(1.0 - terms.transmittance);
                float volumeAlpha = _SdfVolumeVisibilityMode > 0.5
                    ? litVisibility
                    : saturate(max(extinctionAlpha, litVisibility));
                if (volumeAlpha <= max(_VolumeAlphaClipThreshold, 0.0))
                {
                    return sourceColor;
                }

                float extinctionWeight = _SdfVolumeVisibilityMode > 0.5 ? volumeAlpha * 0.35 : 1.0;
                float transmittance = lerp(1.0, terms.transmittance, extinctionWeight);
                float3 volumeColor = mappedScattering * volumeAlpha;
                return float4(sourceColor.rgb * transmittance + volumeColor, sourceColor.a);
            }
            ENDHLSL
        }
    }
}
