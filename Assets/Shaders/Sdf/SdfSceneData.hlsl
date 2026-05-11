#ifndef SDF_SCENE_DATA_INCLUDED
#define SDF_SCENE_DATA_INCLUDED

struct CutPlaneData
{
    float3 normal;
    float distance;
    float sideSign;
    float3 padding;
};

struct SdfSceneShapeData
{
    float4 worldToObjectRow0;
    float4 worldToObjectRow1;
    float4 worldToObjectRow2;
    float4 sphereCenterRadius;
    float4 boxExtentsShapeMode;
    float4 baseCutPlane;
    float4 cutRangeAndDistanceScale;
};

#endif
