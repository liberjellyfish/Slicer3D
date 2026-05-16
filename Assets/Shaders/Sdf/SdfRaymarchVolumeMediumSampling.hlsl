#ifndef SDF_RAYMARCH_VOLUME_MEDIUM_SAMPLING_INCLUDED
#define SDF_RAYMARCH_VOLUME_MEDIUM_SAMPLING_INCLUDED

SdfVolumeMediumBands EvaluateVolumeMediumBands(float3 samplePositionOS, int tileIndex)
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
            float finalDistance = ApplySceneCutPlanes(pShapeOS, baseDistance, cutStart, cutCount, tileIndex) * distanceScale;
            bands.finalDistance = min(bands.finalDistance, finalDistance);
            bands.cutBand = max(bands.cutBand, EvaluateSceneCutBandForShape(pShapeOS, baseDistance, cutStart, cutCount, distanceScale, tileIndex));
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
    out float shapeMaskDebug,
    out float densityDebug,
    int tileIndex)
{
    SdfVolumeMediumBands mediumBands = EvaluateVolumeMediumBands(samplePositionOS, tileIndex);
    float finalDistance = mediumBands.finalDistance;
    float boundaryFade = EvaluateProxyBoundaryFade(samplePositionOS);
    float volumeShapeMask = EvaluateVolumeFogShapeMask(samplePositionOS);
    shapeMaskDebug = saturate(volumeShapeMask);
    float shapeBand = exp(-abs(finalDistance) / max(_VolumeLightShapeDepth, _HitEpsilon));
    float cutBand = mediumBands.cutBand;

    float heightSpan = max(_ProxyBoundsMax.y - _ProxyBoundsMin.y, _HitEpsilon);
    float height01 = saturate((samplePositionOS.y - _ProxyBoundsMin.y) / heightSpan);
    float heightFog = pow(1.0 - height01, 1.6) * _VolumeHeightFogStrength;
    float localVolumeMask = saturate(max(shapeBand * 1.15, cutBand));

    float noiseMask = SdfEvaluateVolumeNoiseMask(samplePositionOS);

    float mediumSupport = saturate(max(volumeShapeMask, cutBand));
    float baseFog = _VolumeBaseFogDensity * mediumSupport;
    float supportedHeightFog = heightFog * mediumSupport;
    float supportedShapeBand = shapeBand * volumeShapeMask;
    float cloudBodyDensity = volumeShapeMask * max(_VolumeCloudDensityBoost, 0.0);
    float movingDensity = (baseFog + supportedHeightFog * localVolumeMask + supportedShapeBand * 0.16 + cloudBodyDensity + cutBand * _VolumeCutFogBoost) * noiseMask * boundaryFade;
    movingDensity = movingDensity > max(_VolumeDensityThreshold, 0.0) ? movingDensity : 0.0;

    float density = SdfApplyAmbientMistAndMovingFogCompression(movingDensity, height01, boundaryFade);
    densityDebug = saturate(density);

    SdfDensityToParticipatingMedia(density, sigmaA, sigmaS, sigmaT, emissionRadiance);
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

        float geometryDistance = Map(shadowOriginOS + lightDirOS * t, -1);
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
    float transmittance = 1.0;
    float t = 0.0;

    [loop]
    for (int stepIndex = 0; stepIndex < 64; stepIndex++)
    {
        if (stepIndex >= shadowStepCount || t >= maxShadowDistance)
        {
            break;
        }

        float currentStepLength = min(stepLength, maxShadowDistance - t);
        float3 p = shadowOriginOS + lightDirOS * (t + currentStepLength * 0.5);

        float sigmaA;
        float sigmaS;
        float sigmaT;
        float3 emissionRadiance;
        float shapeMaskDebug;
        float densityDebug;
        GetParticipatingMedia(p, sigmaA, sigmaS, sigmaT, emissionRadiance, shapeMaskDebug, densityDebug, -1);

        if (!HasVolumeSampleContribution(sigmaT, emissionRadiance))
        {
            t += min(currentStepLength * EvaluateEmptyVolumeStepScale(sigmaT, densityDebug, shapeMaskDebug), maxShadowDistance - t);
            continue;
        }

        transmittance *= exp(-sigmaT * currentStepLength * worldUnitsPerObjectUnit);
        t += currentStepLength;

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
    if (shadowSample.geometryVisibility <= 0.0 && (int)round(_DebugView) != 12)
    {
        shadowSample.combinedVisibility = lerp(1.0, 0.0, saturate(_VolumeLightShadowStrength));
        return shadowSample;
    }

    shadowSample.mediaTransmittance = TraceVolumeMediaTransmittance(shadowOriginOS, lightDirOS, maxShadowDistance);
    shadowSample.combinedVisibility = shadowSample.geometryVisibility * shadowSample.mediaTransmittance;
    shadowSample.combinedVisibility = lerp(1.0, saturate(shadowSample.combinedVisibility), saturate(_VolumeLightShadowStrength));
    return shadowSample;
}

#endif
