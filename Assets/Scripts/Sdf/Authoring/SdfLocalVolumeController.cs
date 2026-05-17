using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SdfRaymarchDriver))]
public sealed class SdfLocalVolumeController : SdfVolumeControllerBase
{
    [Header("Local Target")]
    [SerializeField] private SdfRaymarchDriver targetDriver;
    [SerializeField] private bool localVolumeEnabled = false;
    [SerializeField] private bool preserveLocalVolumeModeFromSharedProxy = true;
    [SerializeField] private SdfRaymarchDriver.VolumeContributionMode enabledContributionMode = SdfRaymarchDriver.VolumeContributionMode.Full;
    [SerializeField] private SdfRaymarchDriver.VolumeContributionMode disabledContributionMode = SdfRaymarchDriver.VolumeContributionMode.SurfaceOnly;

    [Header("Runtime Stats")]
    [SerializeField] private int sharedOwnershipVersion = 1;

    private bool lastLocalVolumeEnabled;
    private bool lastPreserveLocalVolumeMode;
    private SdfRaymarchDriver.VolumeContributionMode lastEnabledContributionMode;
    private SdfRaymarchDriver.VolumeContributionMode lastDisabledContributionMode;

    public bool LocalVolumeEnabled => localVolumeEnabled;
    public bool PreserveLocalVolumeModeFromSharedProxy => preserveLocalVolumeModeFromSharedProxy;
    public bool BlocksSharedSurfaceModeOverride => isActiveAndEnabled && localVolumeEnabled && preserveLocalVolumeModeFromSharedProxy;
    public int SharedOwnershipVersion => sharedOwnershipVersion;

    protected override bool ControllerVolumeEnabled => localVolumeEnabled;
    protected override SdfRaymarchDriver TargetDriver => targetDriver;
    protected override SdfRaymarchDriver.VolumeContributionMode EnabledContributionMode => enabledContributionMode;
    protected override SdfRaymarchDriver.VolumeContributionMode DisabledContributionMode => disabledContributionMode;

    protected override void OnEnable()
    {
        CacheReferences();
        RefreshSharedOwnershipVersion(true);
        base.OnEnable();
    }

    protected override void OnDisable()
    {
        if (targetDriver != null)
        {
            targetDriver.SetVolumeContributionMode(disabledContributionMode);
        }

        RefreshSharedOwnershipVersion(true);
    }

    protected override void OnValidate()
    {
        CacheReferences();
        RefreshSharedOwnershipVersion(false);
        base.OnValidate();
    }

    protected override void CacheReferences()
    {
        if (targetDriver == null)
        {
            targetDriver = GetComponent<SdfRaymarchDriver>();
        }
    }

    public void SetLocalVolumeEnabled(bool enabled)
    {
        if (localVolumeEnabled == enabled)
        {
            return;
        }

        localVolumeEnabled = enabled;
        RefreshSharedOwnershipVersion(true);
        ApplyToDriver();
    }

    public static bool ShouldPreserveLocalVolumeMode(SdfRaymarchDriver driver)
    {
        if (driver == null)
        {
            return false;
        }

        SdfLocalVolumeController localController = driver.GetComponent<SdfLocalVolumeController>();
        return localController != null && localController.BlocksSharedSurfaceModeOverride;
    }

    public static int GetSharedOwnershipHash(SdfRaymarchDriver driver)
    {
        if (driver == null)
        {
            return 0;
        }

        SdfLocalVolumeController localController = driver.GetComponent<SdfLocalVolumeController>();
        return localController != null ? localController.SharedOwnershipVersion : 0;
    }

    private void RefreshSharedOwnershipVersion(bool force)
    {
        bool changed = force
            || lastLocalVolumeEnabled != localVolumeEnabled
            || lastPreserveLocalVolumeMode != preserveLocalVolumeModeFromSharedProxy
            || lastEnabledContributionMode != enabledContributionMode
            || lastDisabledContributionMode != disabledContributionMode;

        if (!changed)
        {
            return;
        }

        lastLocalVolumeEnabled = localVolumeEnabled;
        lastPreserveLocalVolumeMode = preserveLocalVolumeModeFromSharedProxy;
        lastEnabledContributionMode = enabledContributionMode;
        lastDisabledContributionMode = disabledContributionMode;
        unchecked
        {
            sharedOwnershipVersion++;
            if (sharedOwnershipVersion <= 0)
            {
                sharedOwnershipVersion = 1;
            }
        }
    }
}
