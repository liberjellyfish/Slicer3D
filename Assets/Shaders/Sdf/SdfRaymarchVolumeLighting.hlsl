#ifndef SDF_RAYMARCH_VOLUME_LIGHTING_INCLUDED
#define SDF_RAYMARCH_VOLUME_LIGHTING_INCLUDED

SdfVolumeTerms EvaluateVolumeLighting(
    float3 rayOriginOS,
    float3 rayDirOS,
    float3 rayDirWS,
    float segmentStart,
    float segmentEnd,
    float3 mainLightDirWS,
    float3 mainLightColor,
    int tileIndex)
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
    volumeTerms.shapeMaskDebug = 0.0;

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
    float3 viewDirWS = normalize(-rayDirWS);
    float mainLightPhase = EvaluateScatteringPhase(mainLightDirWS, viewDirWS);
    float transmittance = 1.0;
    float3 scatteredLight = float3(0.0, 0.0, 0.0);
    float densityPeak = 0.0;
    float sigmaAPeak = 0.0;
    float sigmaSPeak = 0.0;
    float sigmaTPeak = 0.0;
    float shapeMaskPeak = 0.0;
    float shadowAccumulation = 0.0;
    float geometryShadowAccumulation = 0.0;
    float mediaShadowAccumulation = 0.0;
    float contributingSamples = 0.0;
    float3 mainLightDirOS = normalize(TransformWorldToObjectDir(mainLightDirWS));
    float3 pointLightPositionOS = TransformWorldToObject(_VolumePointLightPositionWS.xyz);
    float mainLightLuminance = SdfLuminance(mainLightColor);
    float sampleT = segmentStart;

    [loop]
    for (int sampleIndex = 0; sampleIndex < 96; sampleIndex++)
    {
        if (sampleIndex >= sampleCount || sampleT >= segmentEnd)
        {
            break;
        }

        float currentStepLength = min(stepLength, segmentEnd - sampleT);
        float3 samplePositionOS = rayOriginOS + rayDirOS * (sampleT + currentStepLength * 0.5);
        float sigmaA;
        float sigmaS;
        float sigmaT;
        float3 emissionRadiance;
        float shapeMaskDebug;
        float densityDebug;
        GetParticipatingMedia(samplePositionOS, sigmaA, sigmaS, sigmaT, emissionRadiance, shapeMaskDebug, densityDebug, tileIndex);

        if (!HasVolumeSampleContribution(sigmaT, emissionRadiance))
        {
            sampleT += min(currentStepLength * EvaluateEmptyVolumeStepScale(sigmaT, densityDebug, shapeMaskDebug), segmentEnd - sampleT);
            continue;
        }

        float3 samplePositionWS = TransformObjectToWorld(samplePositionOS);
        float3 sourceRadiance = float3(0.0, 0.0, 0.0);
        float weightedShadow = 0.0;
        float weightedGeometryShadow = 0.0;
        float weightedMediaShadow = 0.0;
        float sourceWeight = 0.0;

        if (mainLightLuminance * sigmaS * max(_VolumeLightIntensity, 0.0) > 1e-4)
        {
            SdfVolumeShadowSample mainShadow = EvaluateVolumeLightShadow(samplePositionOS, mainLightDirOS, _VolumeShadowMaxDistance);
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
                float3 pointLightDirOS = normalize(TransformWorldToObjectDir(pointLightDirWS));
                float pointDistanceOS = length(pointLightPositionOS - samplePositionOS);
                float pointPhase = EvaluateScatteringPhase(pointLightDirWS, viewDirWS);
                float attenuation = pointRangeAttenuation * pointRangeAttenuation / pointDistanceSq;
                float3 pointRadiance = _VolumePointLightColor.rgb * (_VolumePointLightIntensity * attenuation);
                float pointLuminance = SdfLuminance(pointRadiance);

                if (pointLuminance * sigmaS * max(_VolumeLightIntensity, 0.0) > 1e-4)
                {
                    SdfVolumeShadowSample pointShadow = EvaluateVolumeLightShadow(samplePositionOS, pointLightDirOS, pointDistanceOS);
                    float pointWeight = max(pointLuminance, 1e-4);

                    sourceRadiance += pointRadiance * pointShadow.combinedVisibility * pointPhase;
                    weightedShadow += pointShadow.combinedVisibility * pointWeight;
                    weightedGeometryShadow += pointShadow.geometryVisibility * pointWeight;
                    weightedMediaShadow += pointShadow.mediaTransmittance * pointWeight;
                    sourceWeight += pointWeight;
                }
            }
        }

        float currentStepLengthWS = currentStepLength * worldUnitsPerObjectUnit;
        float segmentTransmittance = exp(-sigmaT * currentStepLengthWS);
        float3 sourceTerm = sourceRadiance * max(_VolumeLightIntensity, 0.0) * sigmaS + emissionRadiance;
        float sourceIntegral = sigmaT > 1e-5
            ? (1.0 - segmentTransmittance) / sigmaT
            : currentStepLengthWS;
        float3 integratedScatter = sourceTerm * sourceIntegral;

        scatteredLight += transmittance * integratedScatter;
        transmittance *= segmentTransmittance;
        densityPeak = max(densityPeak, densityDebug);
        sigmaAPeak = max(sigmaAPeak, sigmaA);
        sigmaSPeak = max(sigmaSPeak, sigmaS);
        sigmaTPeak = max(sigmaTPeak, sigmaT);
        shapeMaskPeak = max(shapeMaskPeak, shapeMaskDebug);
        if (sourceWeight > 0.0)
        {
            float inverseSourceWeight = 1.0 / max(sourceWeight, 1e-4);
            shadowAccumulation += weightedShadow * inverseSourceWeight;
            geometryShadowAccumulation += weightedGeometryShadow * inverseSourceWeight;
            mediaShadowAccumulation += weightedMediaShadow * inverseSourceWeight;
            contributingSamples += 1.0;
        }

        sampleT += currentStepLength;

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
    volumeTerms.shapeMaskDebug = saturate(shapeMaskPeak);
    volumeTerms.shadowDebug = contributingSamples > 0.0 ? saturate(shadowAccumulation / contributingSamples) : 1.0;
    volumeTerms.geometryShadowDebug = contributingSamples > 0.0 ? saturate(geometryShadowAccumulation / contributingSamples) : 1.0;
    volumeTerms.mediaShadowDebug = contributingSamples > 0.0 ? saturate(mediaShadowAccumulation / contributingSamples) : 1.0;
    volumeTerms.debugValue = saturate(densityPeak * 0.45 + (1.0 - volumeTerms.transmittance) * 0.3 + (1.0 - volumeTerms.shadowDebug) * 0.15 + sigmaTPeak * 0.1);
    return volumeTerms;
}

#endif
