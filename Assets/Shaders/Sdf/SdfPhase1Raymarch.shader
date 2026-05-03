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

        [Header(Cut Surface)]
        _CutFaceColor ("Cut Face Color", Color) = (0.97, 0.43, 0.31, 1.0)
        _CutFaceBlend ("Cut Face Blend", Range(0.0, 1.0)) = 0.85
        _CutFaceOcclusionStrength ("Cut Face Occlusion Strength", Range(0.0, 1.0)) = 0.45
        _CutFaceOcclusionDistance ("Cut Face Occlusion Distance", Float) = 0.2
        _CutFaceBandSoftness ("Cut Face Band Softness", Float) = 0.01
        _CutFaceEdgeWidth ("Cut Face Edge Width", Float) = 0.04
        _CutFaceEdgeBoost ("Cut Face Edge Boost", Range(0.0, 2.0)) = 0.35

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
                float edgeMask;
                float cutOcclusion;
                float3 cutNormalOS;
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
                float4 _CutFaceColor;
                float _CutFaceBlend;
                float _CutFaceOcclusionStrength;
                float _CutFaceOcclusionDistance;
                float _CutFaceBandSoftness;
                float _CutFaceEdgeWidth;
                float _CutFaceEdgeBoost;
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
                surfaceData.edgeMask = 0.0;
                surfaceData.cutOcclusion = 1.0;
                surfaceData.cutNormalOS = float3(0.0, 1.0, 0.0);

                float dominantHalfSpace = -1e20;
                float3 dominantNormalOS = float3(0.0, 1.0, 0.0);

                [loop]
                for (int i = 0; i < _CutPlaneCount; i++)
                {
                    float planeSdf = dot(p, _CutPlanes[i].normal) + _CutPlanes[i].distance;
                    float halfSpaceSdf = -(planeSdf * _CutPlanes[i].sideSign);

                    if (halfSpaceSdf > dominantHalfSpace)
                    {
                        dominantHalfSpace = halfSpaceSdf;
                        dominantNormalOS = normalize(-_CutPlanes[i].normal * _CutPlanes[i].sideSign);
                    }
                }

                surfaceData.finalDistance = _CutPlaneCount > 0
                    ? max(surfaceData.baseDistance, dominantHalfSpace)
                    : surfaceData.baseDistance;

                if (_CutPlaneCount <= 0)
                {
                    return surfaceData;
                }

                float bandSoftness = max(_CutFaceBandSoftness, _HitEpsilon);
                float cutDominance = dominantHalfSpace - surfaceData.baseDistance;
                surfaceData.cutMask = smoothstep(-bandSoftness, bandSoftness, cutDominance);
                surfaceData.cutNormalOS = dominantNormalOS;

                float edgeWidth = max(_CutFaceEdgeWidth, _HitEpsilon);
                surfaceData.edgeMask = 1.0 - smoothstep(0.0, edgeWidth, abs(surfaceData.baseDistance));

                float aoDistance = max(_CutFaceOcclusionDistance, edgeWidth);
                float interiorDepth = saturate((-surfaceData.baseDistance) / aoDistance);
                surfaceData.cutOcclusion = 1.0 - interiorDepth * _CutFaceOcclusionStrength;

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

            float SampleSdfSoftShadow(float3 rayOriginOS, float3 rayDirOS, float maxDistance)
            {
                if (_SdfSoftShadowStrength <= 0.0 || _SdfSoftShadowSteps < 1.0 || maxDistance <= 0.0)
                {
                    return 1.0;
                }

                float minStep = max(_HitEpsilon * 0.5, 0.001);
                float maxStep = max(minStep, maxDistance * 0.25);
                float softness = max(_SdfSoftShadowSharpness, 1.0);
                float shadow = 1.0;
                float t = max(_SdfSoftShadowStart, _HitEpsilon * 2.0);

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

                    shadow = min(shadow, softness * h / max(t, 1e-4));
                    t += clamp(h, minStep, maxStep);
                }

                return saturate(shadow);
            }

            float3 EvaluateLighting(
                float3 hitPositionOS,
                float3 hitPositionWS,
                float3 normalOS,
                float3 normalWS,
                SdfSurfaceData surfaceData)
            {
                float4 shadowCoord = TransformWorldToShadowCoord(hitPositionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float3 lightDirWS = normalize(mainLight.direction);
                float3 lightDirOS = normalize(TransformWorldToObjectDir(lightDirWS));
                float ndotl = saturate(dot(normalWS, lightDirWS));

                float mainLightShadow = lerp(1.0, mainLight.shadowAttenuation, saturate(_ReceiveMainLightShadowStrength));

                float shadowDistance = max(_SdfSoftShadowDistance, 0.0);
                float sdfSoftShadow = SampleSdfSoftShadow(
                    hitPositionOS + normalOS * _SdfSoftShadowNormalBias,
                    lightDirOS,
                    shadowDistance);
                sdfSoftShadow = lerp(1.0, sdfSoftShadow, saturate(_SdfSoftShadowStrength));

                float totalShadow = saturate(mainLightShadow * sdfSoftShadow);
                float cutMask = saturate(surfaceData.cutMask);
                float cutOcclusion = lerp(1.0, surfaceData.cutOcclusion, cutMask);

                float3 baseColor = _BaseColor.rgb;
                float3 cutColor = lerp(baseColor, _CutFaceColor.rgb, saturate(_CutFaceBlend));
                float3 surfaceColor = lerp(baseColor, cutColor, cutMask);

                float3 ambient = surfaceColor * _AmbientStrength * cutOcclusion;
                float3 diffuse = surfaceColor * mainLight.color * (ndotl * _DiffuseStrength * totalShadow) * cutOcclusion;
                float3 edgeAccent = cutMask * surfaceData.edgeMask * _CutFaceEdgeBoost * mainLight.color * totalShadow;

                return ambient + diffuse + edgeAccent;
            }

            half4 frag(Varyings input) : SV_Target
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
                float3 normalOS = normalize(lerp(estimatedNormalOS, surfaceData.cutNormalOS, surfaceData.cutMask));
                float3 hitPositionWS = TransformObjectToWorld(hitPositionOS);
                float3 normalWS = normalize(TransformObjectToWorldNormal(normalOS));

                float3 finalColor = EvaluateLighting(hitPositionOS, hitPositionWS, normalOS, normalWS, surfaceData);
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}
