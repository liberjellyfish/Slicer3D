Shader "Custom/Sdf/Phase1Raymarch"
{
    Properties
    {
        [Header(Surface)]
        _BaseColor ("Base Color", Color) = (1.0, 0.82, 0.68, 1.0)
        _AmbientStrength ("Ambient Strength", Range(0.0, 1.0)) = 0.15
        _DiffuseStrength ("Diffuse Strength", Range(0.0, 2.0)) = 1.0

        [Header(Shadowing)]
        _ReceiveMainLightShadowStrength ("Receive Main Light Shadow Strength", Range(0.0, 1.0)) = 1.0
        _SdfSoftShadowStrength ("SDF Soft Shadow Strength", Range(0.0, 1.0)) = 0.85
        _SdfSoftShadowSharpness ("SDF Soft Shadow Sharpness", Range(1.0, 64.0)) = 16.0
        _SdfSoftShadowSteps ("SDF Soft Shadow Steps", Range(4, 64)) = 24
        _SdfSoftShadowStart ("SDF Soft Shadow Start", Float) = 0.01
        _SdfSoftShadowDistance ("SDF Soft Shadow Distance", Float) = 1.5
        _SdfSoftShadowNormalBias ("SDF Soft Shadow Normal Bias", Float) = 0.005
        _SdfSoftShadowMinStepScale ("SDF Soft Shadow Min Step Scale", Range(0.25, 4.0)) = 1.0
        _SdfSoftShadowMaxStepFraction ("SDF Soft Shadow Max Step Fraction", Range(0.02, 0.5)) = 0.2
        _SdfSoftShadowDistanceFadeStart ("SDF Soft Shadow Distance Fade Start", Range(0.0, 1.0)) = 0.7

        [Header(Cut Surface)]
        _CutFaceColor ("Cut Face Color", Color) = (0.97, 0.43, 0.31, 1.0)
        _CutFaceBlend ("Cut Face Blend", Range(0.0, 1.0)) = 0.85
        _CutFaceDominanceSoftness ("Cut Face Dominance Softness", Float) = 0.01
        _CutFaceOcclusionStrength ("Cut Face Occlusion Strength", Range(0.0, 1.0)) = 0.45
        _CutFaceOcclusionDistance ("Cut Face Occlusion Distance", Float) = 0.2
        _CutFaceBandSoftness ("Cut Face Band Softness", Float) = 0.01
        _CutFaceEdgeWidth ("Cut Face Edge Width", Float) = 0.04
        _CutFaceEdgeBoost ("Cut Face Edge Boost", Range(0.0, 2.0)) = 0.35
        _CutFaceFreshnessBoost ("Cut Face Freshness Boost", Range(0.0, 2.0)) = 0.3

        [Header(Render Mode)]
        _SdfSurfaceContribution ("SDF Surface Contribution", Range(0.0, 1.0)) = 1.0
        _UseSceneSdf ("Use Scene SDF", Range(0.0, 1.0)) = 0.0

        [Header(Volume Lighting)]
        _VolumeLightEnabled ("Volume Light Enabled", Range(0.0, 1.0)) = 0.0
        _VolumeSurfaceContribution ("Volume Surface Contribution", Range(0.0, 1.0)) = 1.0
        _VolumeBackgroundContribution ("Volume Background Contribution", Range(0.0, 1.0)) = 1.0
        _VolumeLightIntensity ("Volume Light Intensity", Range(0.0, 8.0)) = 3.0
        _VolumeLightDensity ("Volume Light Density", Range(0.0, 8.0)) = 1.4
        _VolumeLightAnisotropy ("Volume Light Anisotropy", Range(-0.8, 0.8)) = 0.15
        _VolumeLightSamples ("Volume Light Samples", Range(4, 32)) = 20
        _VolumeLightMaxDistance ("Volume Light Max Distance", Float) = 1.2
        _VolumeLightMaxStepLength ("Volume Light Max Step Length", Float) = 0.06
        _VolumeLightShadowStrength ("Volume Light Shadow Strength", Range(0.0, 1.0)) = 0.75
        _VolumeLightShadowBias ("Volume Light Shadow Bias", Float) = 0.01
        _VolumeLightSurfaceFadeDistance ("Volume Light Surface Fade Distance", Float) = 0.22
        _VolumeLightPlaneBand ("Volume Light Plane Band", Float) = 0.16
        _VolumeLightRemovedDepth ("Volume Light Removed Depth", Float) = 0.28
        _VolumeLightShapeDepth ("Volume Light Shape Depth", Float) = 0.24
        _VolumeLightNoiseScale ("Volume Light Noise Scale", Float) = 4.0
        _VolumeLightNoiseStrength ("Volume Light Noise Strength", Range(0.0, 1.0)) = 0.18
        _VolumeLightNoiseDrift ("Volume Light Noise Drift", Float) = 0.2
        _VolumeBaseFogDensity ("Volume Base Fog Density", Range(0.0, 0.08)) = 0.002
        _VolumeHeightFogStrength ("Volume Height Fog Strength", Range(0.0, 0.5)) = 0.08
        _VolumeCutFogBoost ("Volume Cut Fog Boost", Range(0.0, 4.0)) = 1.4
        _VolumeNoiseContrast ("Volume Noise Contrast", Range(0.25, 4.0)) = 1.25
        _VolumeAbsorptionDensity ("Volume Absorption Density", Range(0.0, 8.0)) = 0.18
        _VolumeEmissionIntensity ("Volume Emission Intensity", Range(0.0, 4.0)) = 0.0
        _VolumeEmissionColor ("Volume Emission Color", Color) = (0.0, 0.0, 0.0, 1.0)
        _VolumeShadowSamples ("Volume Shadow Samples", Range(4, 64)) = 16
        _VolumeShadowMaxDistance ("Volume Shadow Max Distance", Float) = 2.0
        _VolumePointLightEnabled ("Volume Point Light Enabled", Range(0.0, 1.0)) = 0.0
        _VolumePointLightPositionWS ("Volume Point Light Position WS", Vector) = (1.4, 1.6, -2.2, 1.0)
        _VolumePointLightColor ("Volume Point Light Color", Color) = (1.0, 0.76, 0.48, 1.0)
        _VolumePointLightIntensity ("Volume Point Light Intensity", Range(0.0, 64.0)) = 18.0
        _VolumePointLightRange ("Volume Point Light Range", Float) = 6.0
        _VolumeExposure ("Volume Exposure", Range(0.1, 4.0)) = 1.15
        _VolumeColorTint ("Volume Color Tint", Color) = (1.0, 0.82, 0.62, 1.0)

        [Header(Ray Marching)]
        _MaxSteps ("Max Steps", Range(8, 256)) = 96
        _MaxDistance ("Max Distance", Float) = 8.0
        _HitEpsilon ("Hit Epsilon", Float) = 0.001
        _NormalEpsilon ("Normal Epsilon", Float) = 0.002

        [Header(Shape)]
        _ShapeMode ("Shape Mode (0 Sphere / 1 Clipped Box / 2 Union)", Range(0, 2)) = 2
        _SphereCenter ("Sphere Center (Object Space)", Vector) = (0.0, 0.0, 0.0, 0.0)
        _SphereRadius ("Sphere Radius", Float) = 0.33
        _BoxExtents ("Box Extents", Vector) = (0.28, 0.28, 0.28, 0.0)
        _CutPlaneNormal ("Cut Plane Normal (Object Space)", Vector) = (0.8, 0.2, 0.0, 0.0)
        _CutPlaneOffset ("Cut Plane Offset", Float) = 0.0

        [HideInInspector] _CutPlaneCount ("Cut Plane Count", Int) = 0
        [HideInInspector] _ProxyBoundsMin ("Proxy Bounds Min", Vector) = (-0.5, -0.5, -0.5, 0.0)
        [HideInInspector] _ProxyBoundsMax ("Proxy Bounds Max", Vector) = (0.5, 0.5, 0.5, 0.0)

        [Header(Debug)]
        _DebugView ("Debug View", Range(0, 15)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "SdfForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            struct CutPlaneData
            {
                float3 normal;
                float distance;
                float sideSign;
                float3 padding;
            };

            struct SdfSurfaceData
            {
                float baseDistance;
                float finalDistance;
                float cutMask;
                float cutDominanceMask;
                float edgeMask;
                float cutOcclusion;
                float cutInteriorDepth;
                float3 cutNormalOS;
                float dominantPlaneDistance;
                float dominantHalfSpace;
            };

            struct SdfShadowTerms
            {
                float mainLightShadow;
                float sdfSoftShadow;
                float totalShadow;
            };

            struct SdfVolumeTerms
            {
                float3 scattering;
                float transmittance;
                float debugValue;
                float densityDebug;
                float shadowDebug;
                float geometryShadowDebug;
                float mediaShadowDebug;
                float sigmaADebug;
                float sigmaSDebug;
                float sigmaTDebug;
            };

            struct SdfVolumeShadowSample
            {
                float geometryVisibility;
                float mediaTransmittance;
                float combinedVisibility;
            };

            struct SdfVolumeMediumBands
            {
                float finalDistance;
                float cutBand;
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

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _AmbientStrength;
                float _DiffuseStrength;
                float _ReceiveMainLightShadowStrength;
                float _SdfSoftShadowStrength;
                float _SdfSoftShadowSharpness;
                float _SdfSoftShadowSteps;
                float _SdfSoftShadowStart;
                float _SdfSoftShadowDistance;
                float _SdfSoftShadowNormalBias;
                float _SdfSoftShadowMinStepScale;
                float _SdfSoftShadowMaxStepFraction;
                float _SdfSoftShadowDistanceFadeStart;
                float4 _CutFaceColor;
                float _CutFaceBlend;
                float _CutFaceDominanceSoftness;
                float _CutFaceOcclusionStrength;
                float _CutFaceOcclusionDistance;
                float _CutFaceBandSoftness;
                float _CutFaceEdgeWidth;
                float _CutFaceEdgeBoost;
                float _CutFaceFreshnessBoost;
                float _SdfSurfaceContribution;
                float _UseSceneSdf;
                float _VolumeLightEnabled;
                float _VolumeSurfaceContribution;
                float _VolumeBackgroundContribution;
                float _VolumeLightIntensity;
                float _VolumeLightDensity;
                float _VolumeLightAnisotropy;
                float _VolumeLightSamples;
                float _VolumeLightMaxDistance;
                float _VolumeLightMaxStepLength;
                float _VolumeLightShadowStrength;
                float _VolumeLightShadowBias;
                float _VolumeLightSurfaceFadeDistance;
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
                float _VolumeEmissionIntensity;
                float4 _VolumeEmissionColor;
                float _VolumeShadowSamples;
                float _VolumeShadowMaxDistance;
                float _VolumePointLightEnabled;
                float4 _VolumePointLightPositionWS;
                float4 _VolumePointLightColor;
                float _VolumePointLightIntensity;
                float _VolumePointLightRange;
                float _VolumeExposure;
                float4 _VolumeColorTint;
                float _MaxSteps;
                float _MaxDistance;
                float _HitEpsilon;
                float _NormalEpsilon;
                float _ShapeMode;
                float4 _SphereCenter;
                float _SphereRadius;
                float4 _BoxExtents;
                float4 _CutPlaneNormal;
                float _CutPlaneOffset;
                int _CutPlaneCount;
                float4 _ProxyBoundsMin;
                float4 _ProxyBoundsMax;
                float _DebugView;
            CBUFFER_END

            StructuredBuffer<CutPlaneData> _CutPlanes;
            StructuredBuffer<SdfSceneShapeData> _SdfSceneShapes;
            StructuredBuffer<CutPlaneData> _SdfSceneCutPlanes;
            int _SdfSceneShapeCount;

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                return output;
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
                float3 n = normalize(normal);
                return dot(p, n) + offset;
            }

            float SdClippedBox(float3 p)
            {
                float boxDistance = SdBox(p, _BoxExtents.xyz);
                float planeDistance = SdPlane(p, _CutPlaneNormal.xyz, _CutPlaneOffset);
                return max(boxDistance, planeDistance);
            }

            float BaseShapeMap(float3 p)
            {
                float sphereDistance = SdSphere(p, _SphereCenter.xyz, _SphereRadius);
                float clippedBoxDistance = SdClippedBox(p);
                int shapeMode = (int)round(_ShapeMode);

                if (shapeMode == 0)
                {
                    return sphereDistance;
                }

                if (shapeMode == 1)
                {
                    return clippedBoxDistance;
                }

                return min(sphereDistance, clippedBoxDistance);
            }

            float ApplyCutPlanes(float3 p, float baseDistance)
            {
                float d = baseDistance;

                [loop]
                for (int i = 0; i < _CutPlaneCount; i++)
                {
                    float planeSdf = dot(p, _CutPlanes[i].normal) + _CutPlanes[i].distance;
                    float halfSpaceSdf = -(planeSdf * _CutPlanes[i].sideSign);
                    d = max(d, halfSpaceSdf);
                }

                return d;
            }

            float SdClippedBoxForData(float3 p, float3 extents, float3 planeNormal, float planeOffset)
            {
                float boxDistance = SdBox(p, extents);
                float planeDistance = SdPlane(p, planeNormal, planeOffset);
                return max(boxDistance, planeDistance);
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

            float ApplySceneCutPlanes(float3 p, float baseDistance, int cutStart, int cutCount)
            {
                float d = baseDistance;

                [loop]
                for (int i = 0; i < cutCount; i++)
                {
                    CutPlaneData cutPlane = _SdfSceneCutPlanes[cutStart + i];
                    float planeSdf = dot(p, cutPlane.normal) + cutPlane.distance;
                    float halfSpaceSdf = -(planeSdf * cutPlane.sideSign);
                    d = max(d, halfSpaceSdf);
                }

                return d;
            }

            float EvaluateSceneCutBandForShape(float3 p, float baseDistance, int cutStart, int cutCount, float distanceScale)
            {
                if (cutCount <= 0)
                {
                    return 0.0;
                }

                float dominantHalfSpace = -1e20;
                float dominantPlaneDistance = 1e20;

                [loop]
                for (int i = 0; i < cutCount; i++)
                {
                    CutPlaneData cutPlane = _SdfSceneCutPlanes[cutStart + i];
                    float planeSdf = dot(p, cutPlane.normal) + cutPlane.distance;
                    float halfSpaceSdf = -(planeSdf * cutPlane.sideSign);

                    if (halfSpaceSdf > dominantHalfSpace)
                    {
                        dominantHalfSpace = halfSpaceSdf;
                        dominantPlaneDistance = planeSdf;
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

            float SceneMap(float3 p)
            {
                if (_SdfSceneShapeCount <= 0)
                {
                    return BaseShapeMap(p);
                }

                float3 pWS = TransformObjectToWorld(p);
                float d = 1e20;

                [loop]
                for (int i = 0; i < _SdfSceneShapeCount; i++)
                {
                    SdfSceneShapeData shape = _SdfSceneShapes[i];
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
                    shapeDistance = ApplySceneCutPlanes(pShapeOS, shapeDistance, cutStart, cutCount) * distanceScale;
                    d = min(d, shapeDistance);
                }

                return d;
            }

            float Map(float3 p)
            {
                if (_UseSceneSdf > 0.5)
                {
                    return SceneMap(p);
                }

                return ApplyCutPlanes(p, BaseShapeMap(p));
            }

            bool EvaluateDominantCutPlane(float3 p, out float dominantHalfSpace, out float dominantPlaneDistance, out float3 dominantNormalOS)
            {
                dominantHalfSpace = -1e20;
                dominantPlaneDistance = 1e20;
                dominantNormalOS = float3(0.0, 1.0, 0.0);

                if (_CutPlaneCount <= 0)
                {
                    return false;
                }

                [loop]
                for (int i = 0; i < _CutPlaneCount; i++)
                {
                    float planeSdf = dot(p, _CutPlanes[i].normal) + _CutPlanes[i].distance;
                    float halfSpaceSdf = -(planeSdf * _CutPlanes[i].sideSign);

                    if (halfSpaceSdf > dominantHalfSpace)
                    {
                        dominantHalfSpace = halfSpaceSdf;
                        dominantPlaneDistance = planeSdf;
                        dominantNormalOS = normalize(-_CutPlanes[i].normal * _CutPlanes[i].sideSign);
                    }
                }

                return true;
            }

            SdfSurfaceData EvaluateSurfaceData(float3 p)
            {
                SdfSurfaceData surfaceData;
                surfaceData.baseDistance = BaseShapeMap(p);
                surfaceData.finalDistance = surfaceData.baseDistance;
                surfaceData.cutMask = 0.0;
                surfaceData.cutDominanceMask = 0.0;
                surfaceData.edgeMask = 0.0;
                surfaceData.cutOcclusion = 1.0;
                surfaceData.cutInteriorDepth = 0.0;
                surfaceData.cutNormalOS = float3(0.0, 1.0, 0.0);
                surfaceData.dominantPlaneDistance = 1e20;
                surfaceData.dominantHalfSpace = -1e20;

                float dominantHalfSpace;
                float3 dominantNormalOS;
                float dominantPlaneDistance;
                bool hasCutPlane = EvaluateDominantCutPlane(p, dominantHalfSpace, dominantPlaneDistance, dominantNormalOS);

                surfaceData.finalDistance = hasCutPlane
                    ? max(surfaceData.baseDistance, dominantHalfSpace)
                    : surfaceData.baseDistance;
                surfaceData.dominantHalfSpace = dominantHalfSpace;

                if (!hasCutPlane)
                {
                    return surfaceData;
                }

                float bandSoftness = max(_CutFaceBandSoftness, _HitEpsilon);
                float planeProximityMask = 1.0 - smoothstep(0.0, bandSoftness, abs(dominantPlaneDistance));
                float interiorMask = saturate((-surfaceData.baseDistance) / bandSoftness);
                float dominanceSoftness = max(_CutFaceDominanceSoftness, _HitEpsilon);
                float dominanceDelta = dominantHalfSpace - surfaceData.baseDistance;
                float cutDominanceMask = smoothstep(0.0, dominanceSoftness, dominanceDelta);
                surfaceData.cutDominanceMask = cutDominanceMask;
                surfaceData.cutMask = planeProximityMask * interiorMask * cutDominanceMask;
                surfaceData.cutNormalOS = dominantNormalOS;
                surfaceData.dominantPlaneDistance = dominantPlaneDistance;

                float edgeWidth = max(_CutFaceEdgeWidth, _HitEpsilon);
                float shellEdgeMask = 1.0 - smoothstep(0.0, edgeWidth, abs(surfaceData.baseDistance));
                surfaceData.edgeMask = shellEdgeMask * surfaceData.cutMask;

                float aoDistance = max(_CutFaceOcclusionDistance, edgeWidth);
                float interiorDepth = saturate((-surfaceData.baseDistance) / aoDistance);
                surfaceData.cutInteriorDepth = interiorDepth;
                float occlusionMask = saturate(0.3 + interiorDepth * 0.7);
                surfaceData.cutOcclusion = 1.0 - surfaceData.cutMask * occlusionMask * _CutFaceOcclusionStrength;

                return surfaceData;
            }

            float GetNormalSampleEpsilon()
            {
                float3 proxySize = max(_ProxyBoundsMax.xyz - _ProxyBoundsMin.xyz, float3(1e-4, 1e-4, 1e-4));
                float minProxyExtent = max(min(proxySize.x, min(proxySize.y, proxySize.z)) * 0.5, 1e-4);
                float lowerBound = max(_HitEpsilon * 0.5, minProxyExtent * 1e-4);
                float upperBound = max(lowerBound, minProxyExtent * 0.01);
                float requested = max(_NormalEpsilon * 0.25, 1e-5);
                return clamp(requested, lowerBound, upperBound);
            }

            float3 EstimateNormalOS(float3 p)
            {
                float e = GetNormalSampleEpsilon();
                float3 x = float3(e, 0.0, 0.0);
                float3 y = float3(0.0, e, 0.0);
                float3 z = float3(0.0, 0.0, e);

                float3 gradient = float3(
                    Map(p + x) - Map(p - x),
                    Map(p + y) - Map(p - y),
                    Map(p + z) - Map(p - z)
                );

                return normalize(gradient);
            }

            bool IntersectProxyBounds(float3 rayOriginOS, float3 rayDirOS, out float tEnter, out float tExit)
            {
                float3 invDir = 1.0 / rayDirOS;

                float3 t0 = (_ProxyBoundsMin.xyz - rayOriginOS) * invDir;
                float3 t1 = (_ProxyBoundsMax.xyz - rayOriginOS) * invDir;

                float3 tMin = min(t0, t1);
                float3 tMax = max(t0, t1);

                tEnter = max(max(tMin.x, tMin.y), tMin.z);
                tExit = min(min(tMax.x, tMax.y), tMax.z);

                return tExit >= max(tEnter, 0.0);
            }

            float ObjectRayUnitToWorldLength(float3 rayOriginOS, float3 rayDirOS)
            {
                float3 p0WS = TransformObjectToWorld(rayOriginOS);
                float3 p1WS = TransformObjectToWorld(rayOriginOS + rayDirOS);
                return max(length(p1WS - p0WS), 1e-4);
            }

            float RefineSurfaceHitT(float3 rayOriginOS, float3 rayDirOS, float coarseT, float maxDistance)
            {
                float refinedT = coarseT;
                float refineEpsilon = max(_HitEpsilon * 0.125, 1e-5);

                // Continue from the coarse sphere-trace hit with a tighter epsilon
                // so lighting does not quantize into visible contour bands.
                [unroll]
                for (int refineStep = 0; refineStep < 6; refineStep++)
                {
                    float3 p = rayOriginOS + rayDirOS * refinedT;
                    float h = Map(p);
                    if (h <= refineEpsilon)
                    {
                        break;
                    }

                    refinedT += min(h, _HitEpsilon);
                    if (refinedT >= maxDistance)
                    {
                        return maxDistance;
                    }
                }

                return refinedT;
            }

            float GetSoftShadowMinStep()
            {
                return max(_HitEpsilon * max(_SdfSoftShadowMinStepScale, 0.25), 0.0005);
            }

            float SampleSdfSoftShadow(float3 rayOriginOS, float3 rayDirOS, float maxDistance)
            {
                if (_SdfSoftShadowStrength <= 0.0 || _SdfSoftShadowSteps < 1.0 || maxDistance <= 0.0)
                {
                    return 1.0;
                }

                float minStep = GetSoftShadowMinStep();
                float maxStep = max(minStep, maxDistance * saturate(_SdfSoftShadowMaxStepFraction));
                float softness = max(_SdfSoftShadowSharpness, 1.0);
                float shadow = 1.0;
                float previousH = 1e20;
                float t = max(_SdfSoftShadowStart, minStep);

                [loop]
                for (int stepIndex = 0; stepIndex < (int)_SdfSoftShadowSteps; stepIndex++)
                {
                    if (t >= maxDistance)
                    {
                        break;
                    }

                    float h = Map(rayOriginOS + rayDirOS * t);
                    if (h < _HitEpsilon)
                    {
                        return 0.0;
                    }

                    float y = h * h / max(2.0 * previousH, 1e-4);
                    float d = sqrt(max(h * h - y * y, 0.0));
                    shadow = min(shadow, softness * d / max(t - y, 1e-4));

                    previousH = h;
                    float adaptiveStep = clamp(max(h, y), minStep, maxStep);
                    t += adaptiveStep;
                }

                float fadeStart = saturate(_SdfSoftShadowDistanceFadeStart);
                if (fadeStart < 1.0)
                {
                    float travel01 = saturate(t / max(maxDistance, 1e-4));
                    float fadeToLight = smoothstep(fadeStart, 1.0, travel01);
                    shadow = lerp(shadow, 1.0, fadeToLight);
                }

                return saturate(shadow);
            }

            float SampleMainLightShadow(float3 positionWS)
            {
                float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                Light lightAtPoint = GetMainLight(shadowCoord);
                return lerp(1.0, lightAtPoint.shadowAttenuation, saturate(_ReceiveMainLightShadowStrength));
            }

            float EvaluateScatteringPhase(float3 lightDirWS, float3 viewDirWS)
            {
                float g = clamp(_VolumeLightAnisotropy, -0.8, 0.8);
                float g2 = g * g;
                float cosTheta = clamp(dot(-lightDirWS, viewDirWS), -1.0, 1.0);
                float denom = pow(max(1.0 + g2 - 2.0 * g * cosTheta, 1e-3), 1.5);
                return (1.0 - g2) / (12.56637061 * denom);
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

            float EvaluateProxyBoundaryFade(float3 p)
            {
                float3 distanceToMin = p - _ProxyBoundsMin.xyz;
                float3 distanceToMax = _ProxyBoundsMax.xyz - p;
                float minDistance = min(min(distanceToMin.x, distanceToMin.y), distanceToMin.z);
                float maxDistance = min(min(distanceToMax.x, distanceToMax.y), distanceToMax.z);
                float boundaryDistance = min(minDistance, maxDistance);
                return smoothstep(0.0, max(_VolumeLightSurfaceFadeDistance, _HitEpsilon), boundaryDistance);
            }

            SdfVolumeMediumBands EvaluateVolumeMediumBands(float3 samplePositionOS)
            {
                SdfVolumeMediumBands bands;
                bands.finalDistance = 1e20;
                bands.cutBand = 0.0;

                if (_UseSceneSdf > 0.5 && _SdfSceneShapeCount > 0)
                {
                    float3 samplePositionWS = TransformObjectToWorld(samplePositionOS);

                    [loop]
                    for (int i = 0; i < _SdfSceneShapeCount; i++)
                    {
                        SdfSceneShapeData shape = _SdfSceneShapes[i];
                        float4 pWS4 = float4(samplePositionWS, 1.0);
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
                        float finalDistance = ApplySceneCutPlanes(pShapeOS, baseDistance, cutStart, cutCount) * distanceScale;
                        bands.finalDistance = min(bands.finalDistance, finalDistance);
                        bands.cutBand = max(bands.cutBand, EvaluateSceneCutBandForShape(pShapeOS, baseDistance, cutStart, cutCount, distanceScale));
                    }

                    return bands;
                }

                bands.finalDistance = ApplyCutPlanes(samplePositionOS, BaseShapeMap(samplePositionOS));
                float dominantHalfSpace;
                float dominantPlaneDistance;
                float3 dominantNormalOS;
                if (EvaluateDominantCutPlane(samplePositionOS, dominantHalfSpace, dominantPlaneDistance, dominantNormalOS))
                {
                    float baseDistance = BaseShapeMap(samplePositionOS);
                    float planeBand = 1.0 - smoothstep(0.0, max(_VolumeLightPlaneBand, _HitEpsilon), abs(dominantPlaneDistance));
                    float originalInterior = saturate((-baseDistance) / max(_VolumeLightRemovedDepth, _HitEpsilon));
                    float removedSide = smoothstep(0.0, max(_VolumeLightRemovedDepth, _HitEpsilon), dominantHalfSpace);
                    bands.cutBand = planeBand * originalInterior * lerp(0.65, 1.0, removedSide);
                }

                return bands;
            }

            void GetParticipatingMedia(
                float3 samplePositionOS,
                out float sigmaA,
                out float sigmaS,
                out float sigmaT,
                out float3 emissionRadiance,
                out float densityDebug)
            {
                SdfVolumeMediumBands mediumBands = EvaluateVolumeMediumBands(samplePositionOS);
                float finalDistance = mediumBands.finalDistance;
                float boundaryFade = EvaluateProxyBoundaryFade(samplePositionOS);
                float shapeBand = exp(-abs(finalDistance) / max(_VolumeLightShapeDepth, _HitEpsilon));
                float cutBand = mediumBands.cutBand;

                float heightSpan = max(_ProxyBoundsMax.y - _ProxyBoundsMin.y, _HitEpsilon);
                float height01 = saturate((samplePositionOS.y - _ProxyBoundsMin.y) / heightSpan);
                float heightFog = pow(1.0 - height01, 1.6) * _VolumeHeightFogStrength;
                float localVolumeMask = saturate(max(shapeBand * 1.15, cutBand));

                float3 noisePosition = samplePositionOS * _VolumeLightNoiseScale;
                noisePosition += float3(0.37, 0.19, 0.53) * (_Time.y * _VolumeLightNoiseDrift);
                float noiseValue = ValueNoise3D(noisePosition);
                float contrastPivot = saturate((noiseValue - 0.5) * _VolumeNoiseContrast + 0.5);
                float contrastNoise = smoothstep(0.2, 0.95, contrastPivot);
                float noiseMask = lerp(1.0, 0.45 + contrastNoise * 0.95, saturate(_VolumeLightNoiseStrength));

                float baseFog = _VolumeBaseFogDensity;
                float density = (baseFog + heightFog * localVolumeMask + shapeBand * 0.2 + cutBand * _VolumeCutFogBoost) * noiseMask * boundaryFade;
                densityDebug = saturate(density);

                sigmaA = max(0.0, density * _VolumeAbsorptionDensity);
                sigmaS = max(0.0, density * _VolumeLightDensity);
                sigmaT = max(0.0, sigmaA + sigmaS);
                emissionRadiance = density * max(_VolumeEmissionIntensity, 0.0) * _VolumeEmissionColor.rgb;
            }

            float TraceVolumeGeometryVisibility(float3 shadowOriginOS, float3 lightDirOS, float maxShadowDistance)
            {
                if (maxShadowDistance <= _HitEpsilon)
                {
                    return 1.0;
                }

                float minStep = max(_HitEpsilon * 2.0, 0.001);
                float maxStep = max(minStep, _VolumeLightMaxStepLength * 1.5);
                int stepCount = min(max((int)_VolumeShadowSamples, 4), 64);
                float t = minStep;

                [loop]
                for (int stepIndex = 0; stepIndex < stepCount; stepIndex++)
                {
                    if (t >= maxShadowDistance)
                    {
                        break;
                    }

                    float geometryDistance = Map(shadowOriginOS + lightDirOS * t);
                    if (geometryDistance < _HitEpsilon)
                    {
                        return 0.0;
                    }

                    t += clamp(geometryDistance, minStep, maxStep);
                }

                return 1.0;
            }

            float TraceVolumeMediaTransmittance(float3 shadowOriginOS, float3 lightDirOS, float maxShadowDistance)
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
                float worldUnitsPerObjectUnit = ObjectRayUnitToWorldLength(shadowOriginOS, lightDirOS);
                float stepLengthWS = stepLength * worldUnitsPerObjectUnit;
                float transmittance = 1.0;

                [loop]
                for (int stepIndex = 0; stepIndex < shadowStepCount; stepIndex++)
                {
                    float sampleT = (stepIndex + 0.5) * stepLength;
                    float3 p = shadowOriginOS + lightDirOS * sampleT;

                    float sigmaA;
                    float sigmaS;
                    float sigmaT;
                    float3 emissionRadiance;
                    float densityDebug;
                    GetParticipatingMedia(p, sigmaA, sigmaS, sigmaT, emissionRadiance, densityDebug);
                    transmittance *= exp(-sigmaT * stepLengthWS);

                    if (transmittance < 0.01)
                    {
                        break;
                    }
                }

                return saturate(transmittance);
            }

            SdfVolumeShadowSample EvaluateVolumeLightShadow(float3 samplePositionOS, float3 lightDirOS, float requestedMaxDistance)
            {
                SdfVolumeShadowSample shadowSample;
                shadowSample.geometryVisibility = 1.0;
                shadowSample.mediaTransmittance = 1.0;
                shadowSample.combinedVisibility = 1.0;

                if (_VolumeLightShadowStrength <= 0.0)
                {
                    return shadowSample;
                }

                float shadowBias = max(_VolumeLightShadowBias, _HitEpsilon);
                float3 shadowOriginOS = samplePositionOS + lightDirOS * shadowBias;

                float lightTEnter;
                float lightTExit;
                if (!IntersectProxyBounds(shadowOriginOS, lightDirOS, lightTEnter, lightTExit))
                {
                    return shadowSample;
                }

                float configuredDistance = max(_VolumeShadowMaxDistance, _HitEpsilon);
                float maxShadowDistance = min(min(requestedMaxDistance, configuredDistance), max(lightTExit, 0.0));
                if (maxShadowDistance <= _HitEpsilon)
                {
                    return shadowSample;
                }

                shadowSample.geometryVisibility = TraceVolumeGeometryVisibility(shadowOriginOS, lightDirOS, maxShadowDistance);
                shadowSample.mediaTransmittance = TraceVolumeMediaTransmittance(shadowOriginOS, lightDirOS, maxShadowDistance);
                shadowSample.combinedVisibility = shadowSample.geometryVisibility * shadowSample.mediaTransmittance;
                shadowSample.combinedVisibility = lerp(1.0, saturate(shadowSample.combinedVisibility), saturate(_VolumeLightShadowStrength));
                return shadowSample;
            }

            SdfShadowTerms EvaluateShadowTerms(
                float3 hitPositionOS,
                float3 normalOS,
                float3 lightDirWS,
                float mainLightShadow)
            {
                SdfShadowTerms shadowTerms;
                shadowTerms.mainLightShadow = mainLightShadow;

                float shadowDistance = max(_SdfSoftShadowDistance, 0.0);
                float3 lightDirOS = normalize(TransformWorldToObjectDir(lightDirWS));
                float bias = max(_SdfSoftShadowNormalBias, _HitEpsilon);
                float sdfSoftShadow = SampleSdfSoftShadow(
                    hitPositionOS + normalOS * bias,
                    lightDirOS,
                    shadowDistance);

                shadowTerms.sdfSoftShadow = lerp(1.0, sdfSoftShadow, saturate(_SdfSoftShadowStrength));
                shadowTerms.totalShadow = saturate(shadowTerms.mainLightShadow * shadowTerms.sdfSoftShadow);
                return shadowTerms;
            }

            float3 ComposeSurfaceColor(SdfSurfaceData surfaceData)
            {
                float3 baseColor = _BaseColor.rgb;
                float3 cutColor = lerp(baseColor, _CutFaceColor.rgb, saturate(_CutFaceBlend));
                float freshnessBoost = 1.0 + surfaceData.cutInteriorDepth * _CutFaceFreshnessBoost;
                float3 freshCutColor = saturate(cutColor * freshnessBoost);
                float edgeBlend = saturate(surfaceData.edgeMask * 0.75);
                freshCutColor = lerp(freshCutColor, min(freshCutColor + 0.15, 1.0), edgeBlend);
                return lerp(baseColor, freshCutColor, saturate(surfaceData.cutMask));
            }

            float3 ResolveSurfaceNormalOS(float3 estimatedNormalOS, SdfSurfaceData surfaceData)
            {
                float cutNormalBlend = saturate(max(surfaceData.cutMask, surfaceData.cutDominanceMask * 0.85));
                return normalize(lerp(estimatedNormalOS, surfaceData.cutNormalOS, cutNormalBlend));
            }

            float3 EvaluateLighting(
                float3 hitPositionOS,
                float3 hitPositionWS,
                float3 normalOS,
                float3 normalWS,
                SdfSurfaceData surfaceData,
                out SdfShadowTerms shadowTerms)
            {
                float4 shadowCoord = TransformWorldToShadowCoord(hitPositionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float3 lightDirWS = normalize(mainLight.direction);
                float ndotl = saturate(dot(normalWS, lightDirWS));

                float mainLightShadow = lerp(1.0, mainLight.shadowAttenuation, saturate(_ReceiveMainLightShadowStrength));
                shadowTerms = EvaluateShadowTerms(hitPositionOS, normalOS, lightDirWS, mainLightShadow);
                float cutMask = saturate(surfaceData.cutMask);
                float cutOcclusion = lerp(1.0, surfaceData.cutOcclusion, cutMask);

                float3 surfaceColor = ComposeSurfaceColor(surfaceData);

                float3 ambient = surfaceColor * _AmbientStrength * cutOcclusion;
                float3 diffuse = surfaceColor * mainLight.color * (ndotl * _DiffuseStrength * shadowTerms.totalShadow) * cutOcclusion;
                float3 edgeAccentColor = lerp(_CutFaceColor.rgb, mainLight.color, 0.5);
                float edgeLight = lerp(0.35, 1.0, shadowTerms.totalShadow);
                float3 edgeAccent = surfaceData.edgeMask * _CutFaceEdgeBoost * edgeAccentColor * edgeLight;

                return ambient + diffuse + edgeAccent;
            }

            SdfVolumeTerms EvaluateVolumeLighting(
                float3 rayOriginOS,
                float3 rayDirOS,
                float3 rayDirWS,
                float segmentStart,
                float segmentEnd,
                float3 mainLightDirWS,
                float3 mainLightColor)
            {
                SdfVolumeTerms volumeTerms;
                volumeTerms.scattering = 0.0;
                volumeTerms.transmittance = 1.0;
                volumeTerms.debugValue = 0.0;
                volumeTerms.densityDebug = 0.0;
                volumeTerms.shadowDebug = 1.0;
                volumeTerms.geometryShadowDebug = 1.0;
                volumeTerms.mediaShadowDebug = 1.0;
                volumeTerms.sigmaADebug = 0.0;
                volumeTerms.sigmaSDebug = 0.0;
                volumeTerms.sigmaTDebug = 0.0;

                if (_VolumeLightEnabled <= 0.0 || _VolumeLightSamples < 1.0)
                {
                    return volumeTerms;
                }

                segmentStart = max(segmentStart, 0.0);
                segmentEnd = max(segmentEnd, segmentStart);
                float segmentLength = segmentEnd - segmentStart;
                if (segmentLength <= _HitEpsilon)
                {
                    return volumeTerms;
                }

                float maxStepLength = max(_VolumeLightMaxStepLength, _HitEpsilon);
                int requestedSampleCount = max((int)_VolumeLightSamples, 1);
                int distanceSampleCount = max((int)ceil(segmentLength / maxStepLength), 1);
                int sampleCount = min(max(requestedSampleCount, distanceSampleCount), 96);
                float stepLength = segmentLength / sampleCount;
                float worldUnitsPerObjectUnit = ObjectRayUnitToWorldLength(rayOriginOS, rayDirOS);
                float stepLengthWS = stepLength * worldUnitsPerObjectUnit;
                float3 viewDirWS = normalize(-rayDirWS);
                float mainLightPhase = EvaluateScatteringPhase(mainLightDirWS, viewDirWS);
                float transmittance = 1.0;
                float3 scatteredLight = float3(0.0, 0.0, 0.0);
                float densityPeak = 0.0;
                float sigmaAPeak = 0.0;
                float sigmaSPeak = 0.0;
                float sigmaTPeak = 0.0;
                float shadowAccumulation = 0.0;
                float geometryShadowAccumulation = 0.0;
                float mediaShadowAccumulation = 0.0;
                float contributingSamples = 0.0;
                float3 mainLightDirOS = normalize(TransformWorldToObjectDir(mainLightDirWS));
                float3 pointLightPositionOS = TransformWorldToObject(_VolumePointLightPositionWS.xyz);

                [loop]
                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    float sampleT = segmentStart + (sampleIndex + 0.5) * stepLength;
                    float3 samplePositionOS = rayOriginOS + rayDirOS * sampleT;
                    float sigmaA;
                    float sigmaS;
                    float sigmaT;
                    float3 emissionRadiance;
                    float densityDebug;
                    GetParticipatingMedia(samplePositionOS, sigmaA, sigmaS, sigmaT, emissionRadiance, densityDebug);

                    if (sigmaT <= 1e-5 && dot(emissionRadiance, float3(0.2126, 0.7152, 0.0722)) <= 1e-5)
                    {
                        continue;
                    }

                    float3 samplePositionWS = TransformObjectToWorld(samplePositionOS);
                    float3 sourceRadiance = float3(0.0, 0.0, 0.0);
                    float weightedShadow = 0.0;
                    float weightedGeometryShadow = 0.0;
                    float weightedMediaShadow = 0.0;
                    float sourceWeight = 0.0;

                    SdfVolumeShadowSample mainShadow = EvaluateVolumeLightShadow(samplePositionOS, mainLightDirOS, _VolumeShadowMaxDistance);
                    float mainWeight = max(dot(mainLightColor, float3(0.2126, 0.7152, 0.0722)), 1e-4);
                    sourceRadiance += mainLightColor * mainShadow.combinedVisibility * mainLightPhase;
                    weightedShadow += mainShadow.combinedVisibility * mainWeight;
                    weightedGeometryShadow += mainShadow.geometryVisibility * mainWeight;
                    weightedMediaShadow += mainShadow.mediaTransmittance * mainWeight;
                    sourceWeight += mainWeight;

                    if (_VolumePointLightEnabled > 0.5 && _VolumePointLightIntensity > 0.0 && _VolumePointLightRange > _HitEpsilon)
                    {
                        float3 toPointLightWS = _VolumePointLightPositionWS.xyz - samplePositionWS;
                        float pointDistanceSq = max(dot(toPointLightWS, toPointLightWS), 1e-4);
                        float pointDistance = sqrt(pointDistanceSq);
                        float pointRangeAttenuation = saturate(1.0 - pointDistance / max(_VolumePointLightRange, _HitEpsilon));

                        if (pointRangeAttenuation > 0.0)
                        {
                            float3 pointLightDirWS = toPointLightWS / pointDistance;
                            float3 pointLightDirOS = normalize(TransformWorldToObjectDir(pointLightDirWS));
                            float pointDistanceOS = length(pointLightPositionOS - samplePositionOS);
                            float pointPhase = EvaluateScatteringPhase(pointLightDirWS, viewDirWS);
                            float attenuation = pointRangeAttenuation * pointRangeAttenuation / pointDistanceSq;
                            float3 pointRadiance = _VolumePointLightColor.rgb * (_VolumePointLightIntensity * attenuation);
                            SdfVolumeShadowSample pointShadow = EvaluateVolumeLightShadow(samplePositionOS, pointLightDirOS, pointDistanceOS);
                            float pointWeight = max(dot(pointRadiance, float3(0.2126, 0.7152, 0.0722)), 1e-4);

                            sourceRadiance += pointRadiance * pointShadow.combinedVisibility * pointPhase;
                            weightedShadow += pointShadow.combinedVisibility * pointWeight;
                            weightedGeometryShadow += pointShadow.geometryVisibility * pointWeight;
                            weightedMediaShadow += pointShadow.mediaTransmittance * pointWeight;
                            sourceWeight += pointWeight;
                        }
                    }

                    float segmentTransmittance = exp(-sigmaT * stepLengthWS);
                    float3 sourceTerm = sourceRadiance * max(_VolumeLightIntensity, 0.0) * sigmaS + emissionRadiance;
                    float sourceIntegral = sigmaT > 1e-5
                        ? (1.0 - segmentTransmittance) / sigmaT
                        : stepLengthWS;
                    float3 integratedScatter = sourceTerm * sourceIntegral;

                    scatteredLight += transmittance * integratedScatter;
                    transmittance *= segmentTransmittance;
                    densityPeak = max(densityPeak, densityDebug);
                    sigmaAPeak = max(sigmaAPeak, sigmaA);
                    sigmaSPeak = max(sigmaSPeak, sigmaS);
                    sigmaTPeak = max(sigmaTPeak, sigmaT);
                    float inverseSourceWeight = 1.0 / max(sourceWeight, 1e-4);
                    shadowAccumulation += weightedShadow * inverseSourceWeight;
                    geometryShadowAccumulation += weightedGeometryShadow * inverseSourceWeight;
                    mediaShadowAccumulation += weightedMediaShadow * inverseSourceWeight;
                    contributingSamples += 1.0;

                    if (transmittance < 0.01)
                    {
                        break;
                    }
                }

                volumeTerms.scattering = scatteredLight * _VolumeColorTint.rgb;
                volumeTerms.transmittance = saturate(transmittance);
                volumeTerms.densityDebug = densityPeak;
                volumeTerms.sigmaADebug = saturate(sigmaAPeak);
                volumeTerms.sigmaSDebug = saturate(sigmaSPeak);
                volumeTerms.sigmaTDebug = saturate(sigmaTPeak);
                volumeTerms.shadowDebug = contributingSamples > 0.0 ? saturate(shadowAccumulation / contributingSamples) : 1.0;
                volumeTerms.geometryShadowDebug = contributingSamples > 0.0 ? saturate(geometryShadowAccumulation / contributingSamples) : 1.0;
                volumeTerms.mediaShadowDebug = contributingSamples > 0.0 ? saturate(mediaShadowAccumulation / contributingSamples) : 1.0;
                volumeTerms.debugValue = saturate(densityPeak * 0.45 + (1.0 - volumeTerms.transmittance) * 0.3 + (1.0 - volumeTerms.shadowDebug) * 0.15 + sigmaTPeak * 0.1);
                return volumeTerms;
            }

            float3 EvaluateDebugView(SdfSurfaceData surfaceData, float3 normalWS, SdfShadowTerms shadowTerms, SdfVolumeTerms volumeTerms)
            {
                int debugView = (int)round(_DebugView);

                if (debugView == 1)
                {
                    return normalWS * 0.5 + 0.5;
                }

                if (debugView == 2)
                {
                    return saturate(surfaceData.cutMask).xxx;
                }

                if (debugView == 3)
                {
                    return saturate(surfaceData.cutDominanceMask).xxx;
                }

                if (debugView == 4)
                {
                    return saturate(shadowTerms.mainLightShadow).xxx;
                }

                if (debugView == 5)
                {
                    return saturate(shadowTerms.sdfSoftShadow).xxx;
                }

                if (debugView == 6)
                {
                    return saturate(shadowTerms.totalShadow).xxx;
                }

                if (debugView == 7)
                {
                    return saturate(volumeTerms.densityDebug).xxx;
                }

                if (debugView == 8)
                {
                    return saturate(volumeTerms.transmittance).xxx;
                }

                if (debugView == 9)
                {
                    return saturate(volumeTerms.shadowDebug).xxx;
                }

                if (debugView == 10)
                {
                    return saturate(volumeTerms.debugValue).xxx;
                }

                if (debugView == 11)
                {
                    return saturate(volumeTerms.geometryShadowDebug).xxx;
                }

                if (debugView == 12)
                {
                    return saturate(volumeTerms.mediaShadowDebug).xxx;
                }

                if (debugView == 13)
                {
                    return saturate(volumeTerms.sigmaADebug).xxx;
                }

                if (debugView == 14)
                {
                    return saturate(volumeTerms.sigmaSDebug).xxx;
                }

                if (debugView == 15)
                {
                    return saturate(volumeTerms.sigmaTDebug).xxx;
                }

                return 0.0;
            }

            float3 ApplyVolumeDisplayMapping(float3 color)
            {
                color = max(color, 0.0);
                color *= max(_VolumeExposure, 0.0);
                return color / (1.0 + color);
            }

            half4 frag(Varyings input, out float outDepth : SV_Depth) : SV_Target
            {
                float3 rayOriginWS = GetCameraPositionWS();
                float3 rayDirWS = normalize(input.positionWS - rayOriginWS);

                float3 rayOriginOS = TransformWorldToObject(rayOriginWS);
                float3 rayDirOS = normalize(TransformWorldToObjectDir(rayDirWS));

                float tEnter;
                float tExit;
                if (!IntersectProxyBounds(rayOriginOS, rayDirOS, tEnter, tExit))
                {
                    clip(-1.0);
                }

                float t = max(tEnter, 0.0);
                float maxDistance = min(_MaxDistance, tExit);
                float volumeStart = max(tEnter, 0.0);

                bool hit = false;
                float3 hitPositionOS = 0.0;

                [loop]
                for (int stepIndex = 0; stepIndex < (int)_MaxSteps; stepIndex++)
                {
                    if (t > maxDistance)
                    {
                        break;
                    }

                    float3 samplePointOS = rayOriginOS + rayDirOS * t;
                    float distanceToSurface = Map(samplePointOS);

                    if (distanceToSurface < _HitEpsilon)
                    {
                        hit = true;
                        hitPositionOS = samplePointOS;
                        break;
                    }

                    t += max(distanceToSurface, _HitEpsilon * 0.5);
                }

                float hitDistance = hit ? t : maxDistance;
                float volumeEnd = hit ? hitDistance : maxDistance;
                float3 volumeDepthPositionOS = rayOriginOS + rayDirOS * volumeEnd;
                float3 volumeDepthPositionWS = TransformObjectToWorld(volumeDepthPositionOS);
                float3 volumeReferencePositionOS = rayOriginOS + rayDirOS * max(volumeStart, 0.0);
                float3 volumeReferencePositionWS = TransformObjectToWorld(volumeReferencePositionOS);

                Light volumeLight = GetMainLight(TransformWorldToShadowCoord(volumeReferencePositionWS));
                SdfVolumeTerms volumeTerms;
                volumeTerms.scattering = 0.0;
                volumeTerms.transmittance = 1.0;
                volumeTerms.debugValue = 0.0;
                volumeTerms.densityDebug = 0.0;
                volumeTerms.shadowDebug = 1.0;
                volumeTerms.geometryShadowDebug = 1.0;
                volumeTerms.mediaShadowDebug = 1.0;
                volumeTerms.sigmaADebug = 0.0;
                volumeTerms.sigmaSDebug = 0.0;
                volumeTerms.sigmaTDebug = 0.0;

                bool wantsSurfaceVolume = hit && _VolumeSurfaceContribution > 0.5;
                bool wantsBackgroundVolume = !hit && _VolumeBackgroundContribution > 0.5;
                if (wantsSurfaceVolume || wantsBackgroundVolume || _DebugView > 0.5)
                {
                    volumeTerms = EvaluateVolumeLighting(
                        rayOriginOS,
                        rayDirOS,
                        rayDirWS,
                        volumeStart,
                        volumeEnd,
                        normalize(volumeLight.direction),
                        volumeLight.color);
                }

                SdfSurfaceData surfaceData;
                surfaceData.baseDistance = 0.0;
                surfaceData.finalDistance = 0.0;
                surfaceData.cutMask = 0.0;
                surfaceData.cutDominanceMask = 0.0;
                surfaceData.edgeMask = 0.0;
                surfaceData.cutOcclusion = 1.0;
                surfaceData.cutInteriorDepth = 0.0;
                surfaceData.cutNormalOS = float3(0.0, 1.0, 0.0);
                surfaceData.dominantPlaneDistance = 1e20;
                surfaceData.dominantHalfSpace = -1e20;

                SdfShadowTerms shadowTerms;
                shadowTerms.mainLightShadow = 1.0;
                shadowTerms.sdfSoftShadow = 1.0;
                shadowTerms.totalShadow = 1.0;
                float3 normalWS = float3(0.0, 1.0, 0.0);
                float3 finalColor = float3(0.0, 0.0, 0.0);
                float outputAlpha = 1.0;

                if (hit)
                {
                    t = RefineSurfaceHitT(rayOriginOS, rayDirOS, t, maxDistance);
                    hitPositionOS = rayOriginOS + rayDirOS * t;
                }

                if (hit)
                {
                    if (_SdfSurfaceContribution <= 0.5)
                    {
                        float volumeAlpha = saturate(1.0 - volumeTerms.transmittance);
                        float scatteringLuminance = dot(volumeTerms.scattering, float3(0.2126, 0.7152, 0.0722));
                        volumeAlpha = saturate(max(volumeAlpha, scatteringLuminance * 2.0));

                        if (_DebugView <= 0.5 && volumeAlpha <= 0.003)
                        {
                            clip(-1.0);
                        }

                        outDepth = ComputeNormalizedDeviceCoordinatesWithZ(volumeDepthPositionWS, GetWorldToHClipMatrix()).z;
                        finalColor = volumeAlpha > 1e-4 ? volumeTerms.scattering / volumeAlpha : float3(0.0, 0.0, 0.0);
                        outputAlpha = volumeAlpha;
                    }
                    else
                    {
                        surfaceData = EvaluateSurfaceData(hitPositionOS);
                        float3 estimatedNormalOS = EstimateNormalOS(hitPositionOS);
                        float3 normalOS = ResolveSurfaceNormalOS(estimatedNormalOS, surfaceData);
                        float3 hitPositionWS = TransformObjectToWorld(hitPositionOS);
                        normalWS = normalize(TransformObjectToWorldNormal(normalOS));
                        outDepth = ComputeNormalizedDeviceCoordinatesWithZ(hitPositionWS, GetWorldToHClipMatrix()).z;

                        finalColor = EvaluateLighting(hitPositionOS, hitPositionWS, normalOS, normalWS, surfaceData, shadowTerms);
                        finalColor = finalColor * volumeTerms.transmittance + volumeTerms.scattering;
                    }
                }
                else
                {
                    if (_VolumeBackgroundContribution <= 0.5)
                    {
                        clip(-1.0);
                    }

                    float volumeAlpha = saturate(1.0 - volumeTerms.transmittance);
                    float scatteringLuminance = dot(volumeTerms.scattering, float3(0.2126, 0.7152, 0.0722));
                    volumeAlpha = saturate(max(volumeAlpha, scatteringLuminance * 2.0));

                    if (_DebugView <= 0.5 && volumeAlpha <= 0.003)
                    {
                        clip(-1.0);
                    }

                    outDepth = ComputeNormalizedDeviceCoordinatesWithZ(volumeDepthPositionWS, GetWorldToHClipMatrix()).z;
                    finalColor = volumeAlpha > 1e-4 ? volumeTerms.scattering / volumeAlpha : float3(0.0, 0.0, 0.0);
                    outputAlpha = volumeAlpha;
                }

                if (_DebugView > 0.5)
                {
                    finalColor = EvaluateDebugView(surfaceData, normalWS, shadowTerms, volumeTerms);
                    outputAlpha = 1.0;
                }
                else
                {
                    finalColor = ApplyVolumeDisplayMapping(finalColor);
                }

                return half4(finalColor, outputAlpha);
            }
            ENDHLSL
        }
    }
}
