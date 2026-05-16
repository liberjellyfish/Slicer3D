#ifndef SDF_RAYMARCH_SURFACE_LIGHTING_INCLUDED
#define SDF_RAYMARCH_SURFACE_LIGHTING_INCLUDED

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
    float ambientOcclusion = saturate(surfaceData.ambientOcclusion);
    float directOcclusion = lerp(1.0, ambientOcclusion, 0.32);
    float indirectOcclusion = ambientOcclusion;

    float3 surfaceColor = ComposeSurfaceColor(surfaceData);

    float3 ambient = surfaceColor * _AmbientStrength * cutOcclusion * indirectOcclusion;
    float3 diffuse = surfaceColor * mainLight.color * (ndotl * _DiffuseStrength * shadowTerms.totalShadow) * cutOcclusion * directOcclusion;
    float3 edgeAccentColor = lerp(_CutFaceColor.rgb, mainLight.color, 0.5);
    float edgeLight = lerp(0.35, 1.0, shadowTerms.totalShadow);
    float3 edgeAccent = surfaceData.edgeMask * _CutFaceEdgeBoost * edgeAccentColor * edgeLight * directOcclusion;

    return ambient + diffuse + edgeAccent;
}

#endif
