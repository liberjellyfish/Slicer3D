#ifndef SDF_SCREEN_SPACE_VOLUME_TRANSFORMS_INCLUDED
#define SDF_SCREEN_SPACE_VOLUME_TRANSFORMS_INCLUDED

float3 VolumeToWorld(float3 p)
{
    return mul(_SdfVolumeLocalToWorld, float4(p, 1.0)).xyz;
}

float3 WorldToVolume(float3 p)
{
    return mul(_SdfVolumeWorldToLocal, float4(p, 1.0)).xyz;
}

float3 WorldDirToVolume(float3 d)
{
    return mul((float3x3)_SdfVolumeWorldToLocal, d);
}

float3 VolumeDirToWorld(float3 d)
{
    return mul((float3x3)_SdfVolumeLocalToWorld, d);
}

#endif
