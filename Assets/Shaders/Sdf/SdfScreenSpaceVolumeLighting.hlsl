#ifndef SDF_SCREEN_SPACE_VOLUME_LIGHTING_INCLUDED
#define SDF_SCREEN_SPACE_VOLUME_LIGHTING_INCLUDED

float ObjectRayUnitToWorldLength(float3 rayOrigin, float3 rayDir)
{
    return max(length(VolumeToWorld(rayOrigin + rayDir) - VolumeToWorld(rayOrigin)), 1e-4);
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

    float noiseMask = SdfEvaluateVolumeNoiseMask(samplePosition);

    float cloudCore = smoothstep(0.16, 0.82, volumeShapeMask);
    cloudCore *= cloudCore;
    float cutCore = smoothstep(0.05, 0.42, cutBand);
    float baseFog = _VolumeBaseFogDensity * cloudCore;
    float supportedHeightFog = heightFog * cloudCore;
    float supportedShapeBand = shapeBand * cloudCore;
    float cloudBodyDensity = cloudCore * max(_VolumeCloudDensityBoost, 0.0);
    float cutDensity = cutCore * _VolumeCutFogBoost;
    float movingDensity = (baseFog + supportedHeightFog * localVolumeMask + supportedShapeBand * 0.06 + cloudBodyDensity + cutDensity) * noiseMask * boundaryFade;
    movingDensity = movingDensity > max(_VolumeDensityThreshold, 0.0) ? movingDensity : 0.0;

    float density = SdfApplyAmbientMistAndMovingFogCompression(movingDensity, height01, boundaryFade);
    densityDebug = saturate(density);

    SdfDensityToParticipatingMedia(density, sigmaA, sigmaS, sigmaT, emissionRadiance);
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

#endif
