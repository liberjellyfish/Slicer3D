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
            #include "Assets/Shaders/Sdf/SdfSceneData.hlsl"

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
            float4 _SdfCutTileScreenSize;

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
            float _VolumeSampleJitterStrength;
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
            float _MaxDistance;
            float _HitEpsilon;
            float4 _ProxyBoundsMin;
            float4 _ProxyBoundsMax;
            float _DebugView;

            #include "Assets/Shaders/Sdf/SdfCommon.hlsl"
            #include "Assets/Shaders/Sdf/SdfVolumeMedium.hlsl"

            // Screen-space volume keeps blit composition here; scene sampling and integration live in includes.
            #include "Assets/Shaders/Sdf/SdfScreenSpaceVolumeTransforms.hlsl"
            #include "Assets/Shaders/Sdf/SdfScreenSpaceVolumeScene.hlsl"
            #include "Assets/Shaders/Sdf/SdfScreenSpaceVolumeLighting.hlsl"
            #include "Assets/Shaders/Sdf/SdfScreenSpaceVolumeDebug.hlsl"

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord.xy;
                float2 cutTileScreenSize = _SdfCutTileScreenSize.x > 1.0 && _SdfCutTileScreenSize.y > 1.0
                    ? _SdfCutTileScreenSize.xy
                    : _ScreenParams.xy;
                int sdfCutTileIndex = GetSdfCutTileIndex(uv * cutTileScreenSize);
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
                float volumeSampleJitter = SdfStableVolumeSampleJitter(uv * _ScreenParams.xy);
                VolumeTerms terms = EvaluateVolumeLighting(
                    rayOrigin,
                    rayDir,
                    rayDirWS,
                    segmentStart,
                    segmentEnd,
                    normalize(mainLight.direction),
                    mainLight.color,
                    sdfCutTileIndex,
                    volumeSampleJitter);

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
