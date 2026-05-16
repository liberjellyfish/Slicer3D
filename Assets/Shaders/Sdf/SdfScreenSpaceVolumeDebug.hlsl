#ifndef SDF_SCREEN_SPACE_VOLUME_DEBUG_INCLUDED
#define SDF_SCREEN_SPACE_VOLUME_DEBUG_INCLUDED

float3 EvaluateDebugView(VolumeTerms terms)
{
    int debugView = (int)round(_DebugView);
    if (debugView == 7) return saturate(terms.densityDebug).xxx;
    if (debugView == 8) return saturate(terms.transmittance).xxx;
    if (debugView == 9) return saturate(terms.shadowDebug).xxx;
    if (debugView == 10) return saturate(terms.densityDebug * 0.45 + (1.0 - terms.transmittance) * 0.3 + (1.0 - terms.shadowDebug) * 0.15 + terms.sigmaTDebug * 0.1).xxx;
    if (debugView == 11) return saturate(terms.geometryShadowDebug).xxx;
    if (debugView == 12) return saturate(terms.mediaShadowDebug).xxx;
    if (debugView == 13) return saturate(terms.sigmaADebug).xxx;
    if (debugView == 14) return saturate(terms.sigmaSDebug).xxx;
    if (debugView == 15) return saturate(terms.sigmaTDebug).xxx;
    if (debugView == 17) return saturate(terms.shapeMaskDebug).xxx;
    return float3(-1.0, -1.0, -1.0);
}

#endif
