#ifndef SDF_VOLUME_MEDIUM_INCLUDED
#define SDF_VOLUME_MEDIUM_INCLUDED

float EvaluateCloudLobeMask(float3 localP, float3 extents)
{
    float3 normalizedP = localP / max(extents, float3(1e-4, 1e-4, 1e-4));
    float3 spread = max(_VolumeCloudLobeSpread.xyz, float3(0.0, 0.0, 0.0));
    float baseRadius = max(_VolumeCloudLobeRadius, 0.05);
    int lobeCount = min(max((int)round(_VolumeCloudLobeCount), 1), 24);
    float lobeMask = 0.0;

    [loop]
    for (int lobeIndex = 0; lobeIndex < 24; lobeIndex++)
    {
        if (lobeIndex >= lobeCount)
        {
            break;
        }

        float index = (float)lobeIndex;
        float3 seed = float3(index * 19.17 + 3.11, index * 41.73 + 7.61, index * 73.31 + 13.97);
        float3 center = float3(
            ValueNoise3D(seed + 11.3),
            ValueNoise3D(seed + 29.7),
            ValueNoise3D(seed + 53.1)) * 2.0 - 1.0;
        center *= spread;
        center.y *= 0.75;

        float radiusJitter = lerp(0.68, 1.28, ValueNoise3D(seed + 91.4));
        float radius = baseRadius * radiusJitter;
        float3 lobeDelta = (normalizedP - center) / max(radius, 1e-4);
        float lobe = 1.0 - smoothstep(0.55, 1.18, length(lobeDelta));
        lobeMask = max(lobeMask, lobe);
    }

    return saturate(lobeMask);
}

float EvaluateProxyBoundaryFade(float3 p)
{
    float3 distanceToMin = p - _ProxyBoundsMin.xyz;
    float3 distanceToMax = _ProxyBoundsMax.xyz - p;
    float minDistance = min(min(distanceToMin.x, distanceToMin.y), distanceToMin.z);
    float maxDistance = min(min(distanceToMax.x, distanceToMax.y), distanceToMax.z);
    float boundaryDistance = min(minDistance, maxDistance);
    return smoothstep(0.0, max(_VolumeLightSurfaceFadeDistance, _HitEpsilon), boundaryDistance);
}

float SdVolumeEllipsoid(float3 p, float3 extents)
{
    float3 safeExtents = max(extents, float3(1e-4, 1e-4, 1e-4));
    float3 q = p / safeExtents;
    float minExtent = min(safeExtents.x, min(safeExtents.y, safeExtents.z));
    return (length(q) - 1.0) * minExtent;
}

float SdVolumeCapsuleY(float3 p, float radius, float height)
{
    float safeRadius = max(radius, 1e-4);
    float halfSegment = max(height * 0.5 - safeRadius, 0.0);
    float3 q = p;
    q.y -= clamp(q.y, -halfSegment, halfSegment);
    return length(q) - safeRadius;
}

float EvaluateVolumeFogShapeMask(float3 p)
{
    int mode = (int)round(_VolumeFogShapeMode);
    if (mode <= 0)
    {
        return 1.0;
    }

    float3 localP = p - _VolumeFogShapeCenter.xyz;
    float3 extents = max(_VolumeFogShapeExtents.xyz, float3(0.01, 0.01, 0.01));
    float signedDistance = mode == 2
        ? SdVolumeCapsuleY(localP, _VolumeFogShapeRadius, _VolumeFogShapeHeight)
        : SdVolumeEllipsoid(localP, extents);

    if (mode == 3)
    {
        float edgeSoftness = max(_VolumeFogShapeEdgeSoftness, _HitEpsilon);
        float baseShapeMask = 1.0 - smoothstep(0.0, edgeSoftness, signedDistance);
        float lobeMask = EvaluateCloudLobeMask(localP, extents);
        float height01 = saturate(localP.y / max(extents.y, 1e-4) * 0.5 + 0.5);
        float heightFade = smoothstep(0.02, 0.18, height01) * (1.0 - smoothstep(0.82, 0.98, height01));
        float3 drift = float3(0.29, 0.47, 0.61) * (_Time.y * _VolumeLightNoiseDrift);
        float baseScale = max(_VolumeFogShapeNoiseScale, 0.1);
        float maxExtent = max(extents.x, max(extents.y, extents.z));
        float3 warpSeed = localP * baseScale + drift;
        float3 warp = float3(
            ValueNoise3D(warpSeed + 13.1),
            ValueNoise3D(warpSeed + 37.7),
            ValueNoise3D(warpSeed + 71.3)) * 2.0 - 1.0;
        float3 cloudP = (localP + warp * maxExtent * 0.2 * saturate(_VolumeCloudWarpStrength / 1.5)) * baseScale + drift;
        float lowFrequency = Fbm3D(cloudP);
        float detailScale = max(_VolumeCloudDetailScale, 0.1);
        float detail = Fbm3D(localP * detailScale + drift * 1.7);
        float coverageThreshold = lerp(0.78, 0.28, saturate(_VolumeCloudCoverage));
        float cloudSoftness = max(_VolumeCloudSoftness, 0.01);
        float cloudMask = smoothstep(coverageThreshold - cloudSoftness, coverageThreshold + cloudSoftness, lowFrequency);
        float detailErosion = smoothstep(0.42, 0.92, detail) * saturate(_VolumeCloudDetailStrength);
        float edgeWeight = 1.0 - smoothstep(-edgeSoftness * 2.5, -edgeSoftness * 0.25, signedDistance);
        cloudMask *= 1.0 - detailErosion * lerp(0.35, 0.85, edgeWeight);
        float erosion = saturate(_VolumeFogShapeNoiseErosion);
        float cloudInfluence = saturate(0.45 + erosion * 0.75);
        float lobeBody = saturate(max(lobeMask, baseShapeMask * 0.25));
        float carvedMask = lerp(baseShapeMask, lobeBody * cloudMask, cloudInfluence);
        return saturate(carvedMask * heightFade);
    }

    float erosion = saturate(_VolumeFogShapeNoiseErosion);
    if (erosion > 0.0)
    {
        float3 noiseP = localP * max(_VolumeFogShapeNoiseScale, 0.1);
        noiseP += float3(0.29, 0.47, 0.61) * (_Time.y * _VolumeLightNoiseDrift);
        float noiseValue = ValueNoise3D(noiseP) * 2.0 - 1.0;
        signedDistance += noiseValue * max(_VolumeFogShapeEdgeSoftness, _HitEpsilon) * erosion * 1.75;
    }

    return 1.0 - smoothstep(0.0, max(_VolumeFogShapeEdgeSoftness, _HitEpsilon), signedDistance);
}

float SdfEvaluateVolumeNoiseMask(float3 samplePosition)
{
    float3 noisePosition = samplePosition * _VolumeLightNoiseScale;
    noisePosition += float3(0.37, 0.19, 0.53) * (_Time.y * _VolumeLightNoiseDrift);
    float noiseValue = ValueNoise3D(noisePosition);
    float contrastPivot = saturate((noiseValue - 0.5) * _VolumeNoiseContrast + 0.5);
    float contrastNoise = smoothstep(0.2, 0.95, contrastPivot);
    return lerp(1.0, 0.45 + contrastNoise * 0.95, saturate(_VolumeLightNoiseStrength));
}

float SdfApplyAmbientMistAndMovingFogCompression(float movingDensity, float height01, float boundaryFade)
{
    if (_VolumeAmbientMistEnabled <= 0.5)
    {
        return movingDensity;
    }

    float maxMovingDensity = max(_VolumeMovingFogMaxDensity, 1e-4);
    float compressedMovingDensity = maxMovingDensity * (1.0 - exp(-movingDensity / maxMovingDensity));
    movingDensity = lerp(movingDensity, compressedMovingDensity, saturate(_VolumeMovingFogCompression));

    float heightFalloff = lerp(1.0, pow(1.0 - height01, 1.25), saturate(_VolumeAmbientMistHeightFalloff));
    float ambientMist = max(_VolumeAmbientMistDensity, 0.0) * heightFalloff * boundaryFade;
    return ambientMist + movingDensity;
}

void SdfDensityToParticipatingMedia(
    float density,
    out float sigmaA,
    out float sigmaS,
    out float sigmaT,
    out float3 emissionRadiance)
{
    sigmaA = max(0.0, density * _VolumeAbsorptionDensity);
    sigmaS = max(0.0, density * _VolumeLightDensity);
    sigmaT = max(0.0, sigmaA + sigmaS);
    emissionRadiance = density * max(_VolumeEmissionIntensity, 0.0) * _VolumeEmissionColor.rgb;
}

#endif
