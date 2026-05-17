using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SdfSharedVolumeProxy))]
public sealed class SdfGlobalVolumeController : SdfVolumeControllerBase
{
    [Header("Global Target")]
    [SerializeField] private SdfSharedVolumeProxy sharedVolumeProxy;
    [SerializeField] private SdfRaymarchDriver volumeDriver;
    [SerializeField] private bool globalVolumeEnabled = true;
    [SerializeField] private bool useScreenSpaceVolume = true;
    [SerializeField] private SdfRaymarchDriver.VolumeContributionMode enabledContributionMode = SdfRaymarchDriver.VolumeContributionMode.VolumeOnly;
    [SerializeField] private SdfRaymarchDriver.VolumeContributionMode disabledContributionMode = SdfRaymarchDriver.VolumeContributionMode.Disabled;

    public SdfSharedVolumeProxy SharedVolumeProxy => sharedVolumeProxy;
    public bool GlobalVolumeEnabled => globalVolumeEnabled;
    public bool UseScreenSpaceVolume => useScreenSpaceVolume;
    public bool BlocksSharedProxyVolumeRendering => isActiveAndEnabled && !globalVolumeEnabled;

    protected override bool ControllerVolumeEnabled => globalVolumeEnabled;
    protected override SdfRaymarchDriver TargetDriver => volumeDriver;
    protected override SdfRaymarchDriver.VolumeContributionMode EnabledContributionMode => enabledContributionMode;
    protected override SdfRaymarchDriver.VolumeContributionMode DisabledContributionMode => disabledContributionMode;

    protected override void CacheReferences()
    {
        if (sharedVolumeProxy == null)
        {
            sharedVolumeProxy = GetComponent<SdfSharedVolumeProxy>();
        }

        if (volumeDriver == null && sharedVolumeProxy != null)
        {
            volumeDriver = sharedVolumeProxy.VolumeDriver;
        }

        if (volumeDriver == null)
        {
            volumeDriver = GetComponent<SdfRaymarchDriver>();
        }
    }

    protected override void ConfigureRoute()
    {
        if (sharedVolumeProxy == null)
        {
            return;
        }

        sharedVolumeProxy.SetScreenSpaceVolumeEnabled(globalVolumeEnabled && useScreenSpaceVolume);
    }

    public void SetGlobalVolumeEnabled(bool enabled)
    {
        if (globalVolumeEnabled == enabled)
        {
            return;
        }

        globalVolumeEnabled = enabled;
        ApplyToDriver();
    }

    public void SetUseScreenSpaceVolume(bool enabled)
    {
        if (useScreenSpaceVolume == enabled)
        {
            return;
        }

        useScreenSpaceVolume = enabled;
        ApplyToDriver();
    }
}
