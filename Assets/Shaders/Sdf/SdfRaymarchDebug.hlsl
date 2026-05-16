#ifndef SDF_RAYMARCH_DEBUG_INCLUDED
#define SDF_RAYMARCH_DEBUG_INCLUDED

float3 EvaluateDebugView(SdfSurfaceData surfaceData, float3 normalWS, SdfShadowTerms shadowTerms, SdfVolumeTerms volumeTerms)
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

    if (debugView == 7)
    {
        return saturate(volumeTerms.densityDebug).xxx;
    }

    if (debugView == 8)
    {
        return saturate(volumeTerms.transmittance).xxx;
    }

    if (debugView == 9)
    {
        return saturate(volumeTerms.shadowDebug).xxx;
    }

    if (debugView == 10)
    {
        return saturate(volumeTerms.debugValue).xxx;
    }

    if (debugView == 11)
    {
        return saturate(volumeTerms.geometryShadowDebug).xxx;
    }

    if (debugView == 12)
    {
        return saturate(volumeTerms.mediaShadowDebug).xxx;
    }

    if (debugView == 13)
    {
        return saturate(volumeTerms.sigmaADebug).xxx;
    }

    if (debugView == 14)
    {
        return saturate(volumeTerms.sigmaSDebug).xxx;
    }

    if (debugView == 15)
    {
        return saturate(volumeTerms.sigmaTDebug).xxx;
    }

    if (debugView == 16)
    {
        float readableShadow = pow(saturate(1.0 - shadowTerms.sdfSoftShadow), 0.55) * 2.5;
        return saturate(readableShadow).xxx;
    }

    if (debugView == 17)
    {
        return saturate(volumeTerms.shapeMaskDebug).xxx;
    }

    if (debugView == 18)
    {
        return saturate(surfaceData.ambientOcclusion).xxx;
    }

    return 0.0;
}

#endif
