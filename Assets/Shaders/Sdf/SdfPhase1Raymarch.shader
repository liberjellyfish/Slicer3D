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
        _DebugView ("Debug View", Range(0, 6)) = 0
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

            float Map(float3 p)
            {
                return ApplyCutPlanes(p, BaseShapeMap(p));
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

                float dominantHalfSpace = -1e20;
                float3 dominantNormalOS = float3(0.0, 1.0, 0.0);
                float dominantPlaneDistance = 1e20;

                [loop]
                for (int i = 0; i < _CutPlaneCount; i++)
                {
                    float planeSdf = dot(p, _CutPlanes[i].normal) + _CutPlanes[i].distance;
                    float halfSpaceSdf = -(planeSdf * _CutPlanes[i].sideSign);

                    if (halfSpaceSdf > dominantHalfSpace)
                    {
                        dominantHalfSpace = halfSpaceSdf;
                        dominantNormalOS = normalize(-_CutPlanes[i].normal * _CutPlanes[i].sideSign);
                        dominantPlaneDistance = planeSdf;
                    }
                }

                surfaceData.finalDistance = _CutPlaneCount > 0
                    ? max(surfaceData.baseDistance, dominantHalfSpace)
                    : surfaceData.baseDistance;
                surfaceData.dominantHalfSpace = dominantHalfSpace;

                if (_CutPlaneCount <= 0)
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

            float3 EstimateNormalOS(float3 p)
            {
                float e = max(_NormalEpsilon, 1e-4);
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

            float3 EvaluateDebugView(SdfSurfaceData surfaceData, float3 normalWS, SdfShadowTerms shadowTerms)
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

                return 0.0;
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

                if (!hit)
                {
                    clip(-1.0);
                }

                SdfSurfaceData surfaceData = EvaluateSurfaceData(hitPositionOS);
                float3 estimatedNormalOS = EstimateNormalOS(hitPositionOS);
                float3 normalOS = ResolveSurfaceNormalOS(estimatedNormalOS, surfaceData);
                float3 hitPositionWS = TransformObjectToWorld(hitPositionOS);
                float3 normalWS = normalize(TransformObjectToWorldNormal(normalOS));
                outDepth = ComputeNormalizedDeviceCoordinatesWithZ(hitPositionWS, GetWorldToHClipMatrix()).z;

                SdfShadowTerms shadowTerms;
                float3 finalColor = EvaluateLighting(hitPositionOS, hitPositionWS, normalOS, normalWS, surfaceData, shadowTerms);
                if (_DebugView > 0.5)
                {
                    finalColor = EvaluateDebugView(surfaceData, normalWS, shadowTerms);
                }

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}
