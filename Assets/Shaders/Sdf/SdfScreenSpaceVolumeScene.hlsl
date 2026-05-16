#ifndef SDF_SCREEN_SPACE_VOLUME_SCENE_INCLUDED
#define SDF_SCREEN_SPACE_VOLUME_SCENE_INCLUDED

float ApplySceneCutPlanes(float3 p, float baseDistance, int cutStart, int cutCount, int tileIndex)
{
    float d = baseDistance;
    uint tileOffset;
    uint tileCount;
    if (TryGetSdfCutTileRange(tileIndex, tileOffset, tileCount))
    {
        int cutEnd = cutStart + cutCount;
        [loop]
        for (uint tileCutIndex = 0u; tileCutIndex < tileCount; tileCutIndex++)
        {
            int cutIndex = _SdfCutTileIndices[tileOffset + tileCutIndex];
            if (cutIndex < cutStart || cutIndex >= cutEnd)
            {
                continue;
            }

            CutPlaneData cutPlane = _SdfShadowSceneCutPlanes[cutIndex];
            float planeSdf = dot(p, cutPlane.normal) + cutPlane.distance;
            d = max(d, -(planeSdf * cutPlane.sideSign));
        }

        return d;
    }

    [loop]
    for (int i = 0; i < cutCount; i++)
    {
        CutPlaneData cutPlane = _SdfShadowSceneCutPlanes[cutStart + i];
        float planeSdf = dot(p, cutPlane.normal) + cutPlane.distance;
        d = max(d, -(planeSdf * cutPlane.sideSign));
    }
    return d;
}

float EvaluateSceneCutBandForShape(float3 p, float baseDistance, int cutStart, int cutCount, float distanceScale, int tileIndex)
{
    if (cutCount <= 0)
    {
        return 0.0;
    }

    float dominantHalfSpace = -1e20;
    float dominantPlaneDistance = 1e20;
    uint tileOffset;
    uint tileCount;
    if (TryGetSdfCutTileRange(tileIndex, tileOffset, tileCount))
    {
        int cutEnd = cutStart + cutCount;
        [loop]
        for (uint tileCutIndex = 0u; tileCutIndex < tileCount; tileCutIndex++)
        {
            int cutIndex = _SdfCutTileIndices[tileOffset + tileCutIndex];
            if (cutIndex < cutStart || cutIndex >= cutEnd)
            {
                continue;
            }

            CutPlaneData cutPlane = _SdfShadowSceneCutPlanes[cutIndex];
            float planeSdf = dot(p, cutPlane.normal) + cutPlane.distance;
            float halfSpaceSdf = -(planeSdf * cutPlane.sideSign);
            if (halfSpaceSdf > dominantHalfSpace)
            {
                dominantHalfSpace = halfSpaceSdf;
                dominantPlaneDistance = planeSdf;
            }
        }

        if (dominantHalfSpace <= -1e19)
        {
            return 0.0;
        }
    }
    else
    {
        [loop]
        for (int i = 0; i < cutCount; i++)
        {
            CutPlaneData cutPlane = _SdfShadowSceneCutPlanes[cutStart + i];
            float planeSdf = dot(p, cutPlane.normal) + cutPlane.distance;
            float halfSpaceSdf = -(planeSdf * cutPlane.sideSign);
            if (halfSpaceSdf > dominantHalfSpace)
            {
                dominantHalfSpace = halfSpaceSdf;
                dominantPlaneDistance = planeSdf;
            }
        }
    }

    float scaledBaseDistance = baseDistance * distanceScale;
    float scaledPlaneDistance = dominantPlaneDistance * distanceScale;
    float scaledHalfSpace = dominantHalfSpace * distanceScale;
    float planeBand = 1.0 - smoothstep(0.0, max(_VolumeLightPlaneBand, _HitEpsilon), abs(scaledPlaneDistance));
    float originalInterior = saturate((-scaledBaseDistance) / max(_VolumeLightRemovedDepth, _HitEpsilon));
    float removedSide = smoothstep(0.0, max(_VolumeLightRemovedDepth, _HitEpsilon), scaledHalfSpace);
    return planeBand * originalInterior * lerp(0.65, 1.0, removedSide);
}

float ShadowSceneMap(float3 volumeP, int tileIndex)
{
    if (_SdfShadowSceneShapeCount <= 0)
    {
        return 1e20;
    }

    float3 pWS = VolumeToWorld(volumeP);
    float d = 1e20;
    [loop]
    for (int i = 0; i < _SdfShadowSceneShapeCount; i++)
    {
        SdfSceneShapeData shape = _SdfShadowSceneShapes[i];
        float4 pWS4 = float4(pWS, 1.0);
        float3 pShapeOS = float3(
            dot(shape.worldToObjectRow0, pWS4),
            dot(shape.worldToObjectRow1, pWS4),
            dot(shape.worldToObjectRow2, pWS4));
        float shapeDistance = BaseShapeMapForData(
            pShapeOS,
            shape.sphereCenterRadius.xyz,
            shape.sphereCenterRadius.w,
            shape.boxExtentsShapeMode.xyz,
            shape.baseCutPlane,
            shape.boxExtentsShapeMode.w);
        int cutStart = (int)round(shape.cutRangeAndDistanceScale.x);
        int cutCount = (int)round(shape.cutRangeAndDistanceScale.y);
        float distanceScale = max(shape.cutRangeAndDistanceScale.z, 1e-4);
        shapeDistance = ApplySceneCutPlanes(pShapeOS, shapeDistance, cutStart, cutCount, tileIndex) * distanceScale;
        d = min(d, shapeDistance);
    }
    return d;
}

VolumeBands EvaluateVolumeMediumBands(float3 volumeP, int tileIndex)
{
    VolumeBands bands;
    bands.finalDistance = 1e20;
    bands.cutBand = 0.0;

    if (_SdfShadowSceneShapeCount <= 0)
    {
        return bands;
    }

    float3 pWS = VolumeToWorld(volumeP);
    [loop]
    for (int i = 0; i < _SdfShadowSceneShapeCount; i++)
    {
        SdfSceneShapeData shape = _SdfShadowSceneShapes[i];
        float4 pWS4 = float4(pWS, 1.0);
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

#endif
