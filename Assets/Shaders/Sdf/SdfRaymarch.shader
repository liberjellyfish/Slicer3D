Shader "Custom/Sdf/Raymarch"
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
        _SdfSoftShadowSceneStrength ("SDF Soft Shadow Scene Strength", Range(0.0, 1.0)) = 0.55
        _SdfSoftShadowSelfIgnoreDistance ("SDF Soft Shadow Self Ignore Distance", Float) = 0.035
        _SdfAmbientOcclusionStrength ("SDF Ambient Occlusion Strength", Range(0.0, 1.0)) = 0.45
        _SdfAmbientOcclusionRadius ("SDF Ambient Occlusion Radius", Float) = 0.18
        _SdfAmbientOcclusionSteps ("SDF Ambient Occlusion Steps", Range(1, 8)) = 4
        _SdfAmbientOcclusionBias ("SDF Ambient Occlusion Bias", Float) = 0.004

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
        _VolumeSampleJitterStrength ("Volume Sample Jitter Strength", Range(0.0, 1.0)) = 0.85
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
        _VolumeDensityThreshold ("Volume Density Threshold", Range(0.0, 0.2)) = 0.012
        _VolumeAlphaClipThreshold ("Volume Alpha Clip Threshold", Range(0.0, 0.05)) = 0.006
        _VolumeEmissionIntensity ("Volume Emission Intensity", Range(0.0, 4.0)) = 0.0
        _VolumeEmissionColor ("Volume Emission Color", Color) = (0.0, 0.0, 0.0, 1.0)
        _VolumeAmbientMistEnabled ("Volume Ambient Mist Enabled", Range(0.0, 1.0)) = 0.0
        _VolumeAmbientMistDensity ("Volume Ambient Mist Density", Range(0.0, 0.04)) = 0.0
        _VolumeAmbientMistHeightFalloff ("Volume Ambient Mist Height Falloff", Range(0.0, 1.0)) = 0.35
        _VolumeMovingFogMaxDensity ("Volume Moving Fog Max Density", Range(0.02, 2.0)) = 0.45
        _VolumeMovingFogCompression ("Volume Moving Fog Compression", Range(0.0, 1.0)) = 0.0
        _VolumeFogShapeMode ("Volume Fog Shape Mode (0 Box / 1 Ellipsoid / 2 CapsuleY / 3 Cloud)", Range(0, 3)) = 3
        _VolumeFogShapeCenter ("Volume Fog Shape Center", Vector) = (0.0, 0.0, 0.0, 0.0)
        _VolumeFogShapeExtents ("Volume Fog Shape Extents", Vector) = (0.48, 0.42, 0.48, 0.0)
        _VolumeFogShapeRadius ("Volume Fog Shape Radius", Float) = 0.46
        _VolumeFogShapeHeight ("Volume Fog Shape Height", Float) = 0.92
        _VolumeFogShapeEdgeSoftness ("Volume Fog Shape Edge Softness", Float) = 0.12
        _VolumeFogShapeNoiseErosion ("Volume Fog Shape Noise Erosion", Range(0.0, 1.0)) = 0.22
        _VolumeFogShapeNoiseScale ("Volume Fog Shape Noise Scale", Float) = 2.2
        _VolumeCloudCoverage ("Volume Cloud Coverage", Range(0.0, 1.0)) = 0.42
        _VolumeCloudSoftness ("Volume Cloud Softness", Range(0.01, 1.0)) = 0.28
        _VolumeCloudDetailStrength ("Volume Cloud Detail Strength", Range(0.0, 1.0)) = 0.32
        _VolumeCloudDetailScale ("Volume Cloud Detail Scale", Float) = 7.0
        _VolumeCloudWarpStrength ("Volume Cloud Warp Strength", Range(0.0, 1.5)) = 0.35
        _VolumeCloudLobeCount ("Volume Cloud Lobe Count", Range(1, 24)) = 12
        _VolumeCloudLobeSpread ("Volume Cloud Lobe Spread", Vector) = (0.95, 0.62, 0.9, 0.0)
        _VolumeCloudLobeRadius ("Volume Cloud Lobe Radius", Range(0.05, 1.0)) = 0.34
        _VolumeCloudDensityBoost ("Volume Cloud Density Boost", Range(0.0, 4.0)) = 1.25
        _VolumeShadowSamples ("Volume Shadow Samples", Range(4, 64)) = 16
        _VolumeShadowMaxDistance ("Volume Shadow Max Distance", Float) = 2.0
        _VolumeGeometryShadowSharpness ("Volume Geometry Shadow Sharpness", Range(1.0, 64.0)) = 12.0
        _VolumeGeometryShadowMinStepScale ("Volume Geometry Shadow Min Step Scale", Range(0.25, 4.0)) = 1.0
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
        _DebugView ("Debug View", Range(0, 18)) = 0
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
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Shaders/Sdf/SdfSceneData.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            struct SdfSurfaceData
            {
                float baseDistance;
                float finalDistance;
                float cutMask;
                float cutDominanceMask;
                float edgeMask;
                float cutOcclusion;
                float ambientOcclusion;
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
                float shapeMaskDebug;
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
                float _SdfSoftShadowSceneStrength;
                float _SdfSoftShadowSelfIgnoreDistance;
                float _SdfAmbientOcclusionStrength;
                float _SdfAmbientOcclusionRadius;
                float _SdfAmbientOcclusionSteps;
                float _SdfAmbientOcclusionBias;
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
                float _VolumeSampleJitterStrength;
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
                float _VolumeDensityThreshold;
                float _VolumeAlphaClipThreshold;
                float _VolumeEmissionIntensity;
                float4 _VolumeEmissionColor;
                float _VolumeAmbientMistEnabled;
                float _VolumeAmbientMistDensity;
                float _VolumeAmbientMistHeightFalloff;
                float _VolumeMovingFogMaxDensity;
                float _VolumeMovingFogCompression;
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
                float _VolumeGeometryShadowSharpness;
                float _VolumeGeometryShadowMinStepScale;
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

            #include "Assets/Shaders/Sdf/SdfCommon.hlsl"
            #include "Assets/Shaders/Sdf/SdfVolumeMedium.hlsl"

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                return output;
            }

            // Raymarch shader keeps pass setup and final composition here; SDF responsibilities live in focused includes.
            #include "Assets/Shaders/Sdf/SdfRaymarchGeometry.hlsl"
            #include "Assets/Shaders/Sdf/SdfRaymarchSoftShadows.hlsl"
            #include "Assets/Shaders/Sdf/SdfRaymarchVolumeMediumSampling.hlsl"
            #include "Assets/Shaders/Sdf/SdfRaymarchSurfaceLighting.hlsl"
            #include "Assets/Shaders/Sdf/SdfRaymarchVolumeLighting.hlsl"
            #include "Assets/Shaders/Sdf/SdfRaymarchDebug.hlsl"

            half4 frag(Varyings input, out float outDepth : SV_Depth) : SV_Target
            {
                float3 rayOriginWS = GetCameraPositionWS();
                float3 rayDirWS = normalize(input.positionWS - rayOriginWS);

                float3 rayOriginOS = TransformWorldToObject(rayOriginWS);
                float3 rayDirOS = normalize(TransformWorldToObjectDir(rayDirWS));
                int sdfCutTileIndex = GetSdfCutTileIndex(input.positionCS.xy);

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
                    float distanceToSurface = Map(samplePointOS, sdfCutTileIndex);

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
                volumeTerms.shapeMaskDebug = 0.0;

                bool wantsSurfaceVolume = hit && _VolumeSurfaceContribution > 0.5;
                bool wantsBackgroundVolume = !hit && _VolumeBackgroundContribution > 0.5;
                if (wantsSurfaceVolume || wantsBackgroundVolume || _DebugView > 0.5)
                {
                    float volumeSampleJitter = SdfStableVolumeSampleJitter(input.positionCS.xy);
                    volumeTerms = EvaluateVolumeLighting(
                        rayOriginOS,
                        rayDirOS,
                        rayDirWS,
                        volumeStart,
                        volumeEnd,
                        normalize(volumeLight.direction),
                        volumeLight.color,
                        sdfCutTileIndex,
                        volumeSampleJitter);
                }

                SdfSurfaceData surfaceData;
                surfaceData.baseDistance = 0.0;
                surfaceData.finalDistance = 0.0;
                surfaceData.cutMask = 0.0;
                surfaceData.cutDominanceMask = 0.0;
                surfaceData.edgeMask = 0.0;
                surfaceData.cutOcclusion = 1.0;
                surfaceData.ambientOcclusion = 1.0;
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
                    t = RefineSurfaceHitT(rayOriginOS, rayDirOS, t, maxDistance, sdfCutTileIndex);
                    hitPositionOS = rayOriginOS + rayDirOS * t;
                }

                if (hit)
                {
                    if (_SdfSurfaceContribution <= 0.5)
                    {
                        float volumeAlpha = saturate(1.0 - volumeTerms.transmittance);
                        float scatteringLuminance = dot(volumeTerms.scattering, float3(0.2126, 0.7152, 0.0722));
                        volumeAlpha = saturate(max(volumeAlpha, scatteringLuminance * 2.0));

                        if (_DebugView <= 0.5 && volumeAlpha <= max(_VolumeAlphaClipThreshold, 0.0))
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
                        float3 estimatedNormalOS = EstimateNormalOS(hitPositionOS, sdfCutTileIndex);
                        float3 normalOS = ResolveSurfaceNormalOS(estimatedNormalOS, surfaceData);
                        surfaceData.ambientOcclusion = SampleSdfAmbientOcclusion(hitPositionOS, normalOS);
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

                    if (_DebugView <= 0.5 && volumeAlpha <= max(_VolumeAlphaClipThreshold, 0.0))
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
                    finalColor *= outputAlpha;
                }

                return half4(finalColor, outputAlpha);
            }
            ENDHLSL
        }
    }
}
