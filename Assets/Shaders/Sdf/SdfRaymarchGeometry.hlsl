#ifndef SDF_RAYMARCH_GEOMETRY_INCLUDED
#define SDF_RAYMARCH_GEOMETRY_INCLUDED

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

            CutPlaneData cutPlane = _SdfSceneCutPlanes[cutIndex];
            float planeSdf = dot(p, cutPlane.normal) + cutPlane.distance;
            float halfSpaceSdf = -(planeSdf * cutPlane.sideSign);
            d = max(d, halfSpaceSdf);
        }

        return d;
    }

    [loop]
    for (int i = 0; i < cutCount; i++)
    {
        CutPlaneData cutPlane = _SdfSceneCutPlanes[cutStart + i];
        float planeSdf = dot(p, cutPlane.normal) + cutPlane.distance;
        float halfSpaceSdf = -(planeSdf * cutPlane.sideSign);
        d = max(d, halfSpaceSdf);
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

            CutPlaneData cutPlane = _SdfSceneCutPlanes[cutIndex];
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
            CutPlaneData cutPlane = _SdfSceneCutPlanes[cutStart + i];
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

float SceneMap(float3 p, int tileIndex)
{
    if (_SdfSceneShapeCount <= 0)
    {
        return BaseShapeMap(p);
    }

    float3 pWS = TransformObjectToWorld(p);
    float d = 1e20;

    [loop]
    for (int i = 0; i < _SdfSceneShapeCount; i++)
    {
        SdfSceneShapeData shape = _SdfSceneShapes[i];
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

float ApplyShadowSceneCutPlanes(float3 p, float baseDistance, int cutStart, int cutCount)
{
    float d = baseDistance;

    [loop]
    for (int i = 0; i < cutCount; i++)
    {
        CutPlaneData cutPlane = _SdfShadowSceneCutPlanes[cutStart + i];
        float planeSdf = dot(p, cutPlane.normal) + cutPlane.distance;
        float halfSpaceSdf = -(planeSdf * cutPlane.sideSign);
        d = max(d, halfSpaceSdf);
    }

    return d;
}

float ShadowSceneMap(float3 p)
{
    if (_SdfShadowSceneShapeCount <= 0)
    {
        return ApplyCutPlanes(p, BaseShapeMap(p));
    }

    float3 pWS = TransformObjectToWorld(p);
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
        shapeDistance = ApplyShadowSceneCutPlanes(pShapeOS, shapeDistance, cutStart, cutCount) * distanceScale;
        d = min(d, shapeDistance);
    }

    return d;
}

float Map(float3 p, int tileIndex)
{
    if (_UseSceneSdf > 0.5)
    {
        return SceneMap(p, tileIndex);
    }

    return ApplyCutPlanes(p, BaseShapeMap(p));
}

bool EvaluateDominantCutPlane(float3 p, out float dominantHalfSpace, out float dominantPlaneDistance, out float3 dominantNormalOS)
{
    dominantHalfSpace = -1e20;
    dominantPlaneDistance = 1e20;
    dominantNormalOS = float3(0.0, 1.0, 0.0);

    if (_CutPlaneCount <= 0)
    {
        return false;
    }

    [loop]
    for (int i = 0; i < _CutPlaneCount; i++)
    {
        float planeSdf = dot(p, _CutPlanes[i].normal) + _CutPlanes[i].distance;
        float halfSpaceSdf = -(planeSdf * _CutPlanes[i].sideSign);

        if (halfSpaceSdf > dominantHalfSpace)
        {
            dominantHalfSpace = halfSpaceSdf;
            dominantPlaneDistance = planeSdf;
            dominantNormalOS = normalize(-_CutPlanes[i].normal * _CutPlanes[i].sideSign);
        }
    }

    return true;
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
    surfaceData.ambientOcclusion = 1.0;
    surfaceData.cutInteriorDepth = 0.0;
    surfaceData.cutNormalOS = float3(0.0, 1.0, 0.0);
    surfaceData.dominantPlaneDistance = 1e20;
    surfaceData.dominantHalfSpace = -1e20;

    float dominantHalfSpace;
    float3 dominantNormalOS;
    float dominantPlaneDistance;
    bool hasCutPlane = EvaluateDominantCutPlane(p, dominantHalfSpace, dominantPlaneDistance, dominantNormalOS);

    surfaceData.finalDistance = hasCutPlane
        ? max(surfaceData.baseDistance, dominantHalfSpace)
        : surfaceData.baseDistance;
    surfaceData.dominantHalfSpace = dominantHalfSpace;

    if (!hasCutPlane)
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

float GetNormalSampleEpsilon()
{
    float3 proxySize = max(_ProxyBoundsMax.xyz - _ProxyBoundsMin.xyz, float3(1e-4, 1e-4, 1e-4));
    float minProxyExtent = max(min(proxySize.x, min(proxySize.y, proxySize.z)) * 0.5, 1e-4);
    float lowerBound = max(_HitEpsilon * 0.5, minProxyExtent * 1e-4);
    float upperBound = max(lowerBound, minProxyExtent * 0.01);
    float requested = max(_NormalEpsilon * 0.25, 1e-5);
    return clamp(requested, lowerBound, upperBound);
}

float3 EstimateNormalOS(float3 p, int tileIndex)
{
    float e = GetNormalSampleEpsilon();
    float3 x = float3(e, 0.0, 0.0);
    float3 y = float3(0.0, e, 0.0);
    float3 z = float3(0.0, 0.0, e);

    float3 gradient = float3(
        Map(p + x, tileIndex) - Map(p - x, tileIndex),
        Map(p + y, tileIndex) - Map(p - y, tileIndex),
        Map(p + z, tileIndex) - Map(p - z, tileIndex)
    );

    return normalize(gradient);
}

float SampleSdfAmbientOcclusion(float3 p, float3 normalOS)
{
    float strength = saturate(_SdfAmbientOcclusionStrength);
    if (strength <= 0.0)
    {
        return 1.0;
    }

    int stepCount = min(max((int)round(_SdfAmbientOcclusionSteps), 1), 8);
    float radius = max(_SdfAmbientOcclusionRadius, _HitEpsilon * 2.0);
    float bias = max(_SdfAmbientOcclusionBias, _HitEpsilon);
    float occlusion = 0.0;
    float weightSum = 0.0;

    [loop]
    for (int i = 0; i < 8; i++)
    {
        if (i >= stepCount)
        {
            break;
        }

        float sample01 = (i + 1.0) / stepCount;
        float sampleDistance = bias + radius * sample01 * sample01;
        float3 samplePosition = p + normalOS * sampleDistance;
        float expectedDistance = sampleDistance - bias;
        float sampledDistance = _SdfShadowSceneShapeCount > 0
            ? ShadowSceneMap(samplePosition)
            : Map(samplePosition, -1);
        float sampleOcclusion = saturate((expectedDistance - sampledDistance) / max(radius, 1e-4));
        float weight = 1.0 - sample01 * 0.65;
        occlusion += sampleOcclusion * weight;
        weightSum += weight;
    }

    occlusion = weightSum > 0.0 ? occlusion / weightSum : 0.0;
    return saturate(1.0 - occlusion * strength);
}

float ObjectRayUnitToWorldLength(float3 rayOriginOS, float3 rayDirOS)
{
    float3 p0WS = TransformObjectToWorld(rayOriginOS);
    float3 p1WS = TransformObjectToWorld(rayOriginOS + rayDirOS);
    return max(length(p1WS - p0WS), 1e-4);
}

float RefineSurfaceHitT(float3 rayOriginOS, float3 rayDirOS, float coarseT, float maxDistance, int tileIndex)
{
    float refinedT = coarseT;
    float refineEpsilon = max(_HitEpsilon * 0.125, 1e-5);

    // Continue from the coarse sphere-trace hit with a tighter epsilon
    // so lighting does not quantize into visible contour bands.
    [unroll]
    for (int refineStep = 0; refineStep < 6; refineStep++)
    {
        float3 p = rayOriginOS + rayDirOS * refinedT;
        float h = Map(p, tileIndex);
        if (h <= refineEpsilon)
        {
            break;
        }

        refinedT += min(h, _HitEpsilon);
        if (refinedT >= maxDistance)
        {
            return maxDistance;
        }
    }

    return refinedT;
}

#endif
