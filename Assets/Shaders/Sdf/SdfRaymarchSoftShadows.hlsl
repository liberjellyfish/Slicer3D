#ifndef SDF_RAYMARCH_SOFT_SHADOWS_INCLUDED
#define SDF_RAYMARCH_SOFT_SHADOWS_INCLUDED

float GetSoftShadowMinStep()
{
    return max(_HitEpsilon * max(_SdfSoftShadowMinStepScale, 0.25), 0.0005);
}

float SampleSdfSoftShadowMap(float3 rayOriginOS, float3 rayDirOS, float maxDistance, float useShadowScene, float selfIgnoreDistance)
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

        float h = useShadowScene > 0.5
            ? ShadowSceneMap(rayOriginOS + rayDirOS * t)
            : Map(rayOriginOS + rayDirOS * t, -1);
        bool ignoreSelfContact = t < selfIgnoreDistance;
        h = ignoreSelfContact ? max(h, minStep) : h;
        if (h < _HitEpsilon && !ignoreSelfContact)
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

float SampleSdfSoftShadow(float3 rayOriginOS, float3 rayDirOS, float maxDistance)
{
    float localShadow = SampleSdfSoftShadowMap(rayOriginOS, rayDirOS, maxDistance, 0.0, 0.0);

    if (_SdfShadowSceneShapeCount <= 0 || _SdfSoftShadowSceneStrength <= 0.0)
    {
        return localShadow;
    }

    float sceneShadow = SampleSdfSoftShadowMap(
        rayOriginOS,
        rayDirOS,
        maxDistance,
        1.0,
        max(_SdfSoftShadowSelfIgnoreDistance, _SdfSoftShadowStart));
    return saturate(localShadow * lerp(1.0, sceneShadow, saturate(_SdfSoftShadowSceneStrength)));
}

float SampleMainLightShadow(float3 positionWS)
{
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light lightAtPoint = GetMainLight(shadowCoord);
    return lerp(1.0, lightAtPoint.shadowAttenuation, saturate(_ReceiveMainLightShadowStrength));
}

#endif
