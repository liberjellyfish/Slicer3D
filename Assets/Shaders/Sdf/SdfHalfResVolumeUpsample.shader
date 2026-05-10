Shader "Hidden/Sdf/HalfResVolumeUpsample"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "SdfHalfResVolumeUpsample"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_SdfHalfResVolumeTexture);

            float4 _SdfHalfResVolumeTexelSize;
            float _SdfHalfResVolumeBilateralEnabled;
            float _SdfHalfResVolumeDepthSensitivity;
            float _SdfHalfResVolumeDeltaClamp;

            bool HasSceneDepth(float rawDepth)
            {
                #if UNITY_REVERSED_Z
                    return rawDepth > 0.0001;
                #else
                    return rawDepth < 0.9999;
                #endif
            }

            float4 SampleSourceColor(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, _BlitMipLevel);
            }

            float4 SampleHalfComposite(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_SdfHalfResVolumeTexture, sampler_LinearClamp, uv, 0.0);
            }

            float3 SampleVolumeDelta(float2 uv)
            {
                float3 halfComposite = SampleHalfComposite(uv).rgb;
                float3 sourceColor = SampleSourceColor(uv).rgb;
                float3 delta = halfComposite - sourceColor;
                float clampLimit = max(_SdfHalfResVolumeDeltaClamp, 0.0);
                return clampLimit > 0.0 ? clamp(delta, -float3(clampLimit, clampLimit, clampLimit), float3(clampLimit, clampLimit, clampLimit)) : delta;
            }

            float DepthWeight(float centerRawDepth, float sampleRawDepth, float centerEyeDepth)
            {
                if (_SdfHalfResVolumeBilateralEnabled <= 0.5)
                {
                    return 1.0;
                }

                bool centerHasDepth = HasSceneDepth(centerRawDepth);
                bool sampleHasDepth = HasSceneDepth(sampleRawDepth);
                if (centerHasDepth != sampleHasDepth)
                {
                    return 0.04;
                }

                if (!centerHasDepth)
                {
                    return 1.0;
                }

                float sampleEyeDepth = LinearEyeDepth(sampleRawDepth, _ZBufferParams);
                float depthDelta = abs(sampleEyeDepth - centerEyeDepth);
                float depthScale = max(_SdfHalfResVolumeDepthSensitivity * max(centerEyeDepth, 1.0), 1e-4);
                return exp2(-depthDelta / depthScale);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord.xy;
                float4 sourceColor = SampleSourceColor(uv);

                float centerRawDepth = SampleSceneDepth(uv);
                float centerEyeDepth = HasSceneDepth(centerRawDepth)
                    ? LinearEyeDepth(centerRawDepth, _ZBufferParams)
                    : 1.0;

                float2 halfSize = max(_SdfHalfResVolumeTexelSize.zw, float2(1.0, 1.0));
                float2 halfTexel = _SdfHalfResVolumeTexelSize.xy;
                float2 halfPixel = uv * halfSize - 0.5;
                float2 basePixel = floor(halfPixel);
                float2 pixelFraction = saturate(halfPixel - basePixel);

                float3 deltaSum = 0.0;
                float weightSum = 0.0;

                [unroll]
                for (int y = 0; y < 2; y++)
                {
                    [unroll]
                    for (int x = 0; x < 2; x++)
                    {
                        float2 samplePixel = clamp(basePixel + float2(x, y), float2(0.0, 0.0), halfSize - float2(1.0, 1.0));
                        float2 sampleUv = (samplePixel + 0.5) * halfTexel;
                        float2 bilinearPair = float2(x == 0 ? 1.0 - pixelFraction.x : pixelFraction.x,
                                                      y == 0 ? 1.0 - pixelFraction.y : pixelFraction.y);
                        float weight = max(bilinearPair.x * bilinearPair.y, 1e-4);
                        float sampleRawDepth = SampleSceneDepth(sampleUv);
                        weight *= DepthWeight(centerRawDepth, sampleRawDepth, centerEyeDepth);

                        deltaSum += SampleVolumeDelta(sampleUv) * weight;
                        weightSum += weight;
                    }
                }

                float3 volumeDelta = weightSum > 1e-5 ? deltaSum / weightSum : SampleVolumeDelta(uv);
                return float4(sourceColor.rgb + volumeDelta, sourceColor.a);
            }
            ENDHLSL
        }
    }
}
