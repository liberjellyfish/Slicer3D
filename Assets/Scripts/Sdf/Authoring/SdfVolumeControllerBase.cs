using UnityEngine;

public abstract class SdfVolumeControllerBase : MonoBehaviour
{
    public enum VolumeProfile
    {
        SoftCloud = 0,
        ExplosionCloud = 1,
        CutDustCloud = 2,
        PerformanceCloud = 3
    }

    [Header("Apply")]
    [SerializeField] protected VolumeProfile profile = VolumeProfile.ExplosionCloud;
    [SerializeField] protected bool applyOnEnable = true;
    [SerializeField] protected bool applyOnValidate = true;
    [SerializeField] protected bool applyAutomaticallyInPlayMode = false;
    [SerializeField] protected bool refreshProfileDefaultsOnValidate = false;
    [SerializeField] protected bool animateWarmCoreLightInPlayMode = false;

    [Header("Driver Preset")]
    [SerializeField] protected bool applyDriverPresetBeforeSettings = true;
    [SerializeField] protected SdfRaymarchDriver.VolumePreset driverPreset = SdfRaymarchDriver.VolumePreset.CinematicWarm;

    [Header("Cloud Shape")]
    [SerializeField] protected Vector3 shapeExtents = new Vector3(0.78f, 0.62f, 0.72f);
    [SerializeField] [Min(0.001f)] protected float edgeSoftness = 0.22f;
    [SerializeField] [Range(0.0f, 1.0f)] protected float noiseErosion = 0.72f;
    [SerializeField] [Min(0.1f)] protected float noiseScale = 1.75f;
    [SerializeField] [Range(0.0f, 1.0f)] protected float coverage = 0.48f;
    [SerializeField] [Range(0.01f, 1.0f)] protected float softness = 0.2f;
    [SerializeField] [Range(0.0f, 1.0f)] protected float detailStrength = 0.64f;
    [SerializeField] [Min(0.1f)] protected float detailScale = 9.0f;
    [SerializeField] [Range(0.0f, 1.5f)] protected float warpStrength = 0.62f;
    [SerializeField] [Range(1, 24)] protected int lobeCount = 16;
    [SerializeField] protected Vector3 lobeSpread = new Vector3(1.05f, 0.58f, 0.95f);
    [SerializeField] [Range(0.05f, 1.0f)] protected float lobeRadius = 0.42f;
    [SerializeField] [Range(0.0f, 4.0f)] protected float densityBoost = 0.95f;

    [Header("Cloud Medium")]
    [SerializeField] [Range(0.0f, 8.0f)] protected float lightIntensity = 2.8f;
    [SerializeField] [Range(0.0f, 8.0f)] protected float lightDensity = 0.82f;
    [SerializeField] [Range(0.0f, 0.08f)] protected float baseFogDensity = 0.0f;
    [SerializeField] [Range(0.0f, 0.5f)] protected float heightFogStrength = 0.008f;
    [SerializeField] [Range(0.0f, 4.0f)] protected float cutFogBoost = 0.95f;
    [SerializeField] [Range(0.0f, 8.0f)] protected float absorptionDensity = 0.18f;
    [SerializeField] [Range(0.0f, 0.2f)] protected float densityThreshold = 0.045f;
    [SerializeField] [Range(0.0f, 0.05f)] protected float alphaClipThreshold = 0.012f;
    [SerializeField] [Range(0.1f, 4.0f)] protected float exposure = 1.2f;
    [SerializeField] protected Color colorTint = new Color(1.0f, 0.78f, 0.56f, 1.0f);
    [SerializeField] [Range(0.0f, 4.0f)] protected float emissionIntensity = 0.0f;
    [SerializeField] protected Color emissionColor = new Color(1.0f, 0.62f, 0.22f, 1.0f);

    [Header("Warm Core Light")]
    [SerializeField] protected bool enableWarmCoreLight = true;
    [SerializeField] protected Vector3 warmCoreLocalPosition = new Vector3(-0.15f, 0.18f, -0.35f);
    [SerializeField] protected Color warmCoreColor = new Color(1.0f, 0.68f, 0.28f, 1.0f);
    [SerializeField] [Range(0.0f, 64.0f)] protected float warmCoreIntensity = 18.0f;
    [SerializeField] [Min(0.05f)] protected float warmCoreRange = 4.8f;
    [SerializeField] [Min(0.0f)] protected float warmCoreOrbitRadius = 0.18f;
    [SerializeField] protected float warmCoreOrbitSpeed = 18.0f;

    protected virtual bool ControllerVolumeEnabled => true;
    protected abstract SdfRaymarchDriver TargetDriver { get; }
    protected abstract SdfRaymarchDriver.VolumeContributionMode EnabledContributionMode { get; }
    protected virtual SdfRaymarchDriver.VolumeContributionMode DisabledContributionMode => SdfRaymarchDriver.VolumeContributionMode.Disabled;

    protected virtual void OnEnable()
    {
        CacheReferences();
        ConfigureRoute();
        if (applyOnEnable && ShouldApplyAutomatically())
        {
            ApplyToDriver();
        }
    }

    protected virtual void OnValidate()
    {
        ClampSettings();
        CacheReferences();
        ConfigureRoute();
        if (applyOnValidate && isActiveAndEnabled && ShouldApplyAutomatically())
        {
            if (refreshProfileDefaultsOnValidate)
            {
                ApplyProfileDefaults();
            }

            ApplyToDriver();
        }
    }

    protected virtual void OnDisable()
    {
    }

    protected virtual void LateUpdate()
    {
        if (!Application.isPlaying || !animateWarmCoreLightInPlayMode || !ControllerVolumeEnabled)
        {
            return;
        }

        ApplyWarmCoreLight(TargetDriver);
    }

    [ContextMenu("Apply Volume To Driver")]
    public void ApplyToDriver()
    {
        CacheReferences();
        ConfigureRoute();
        SdfRaymarchDriver driver = TargetDriver;
        if (driver == null)
        {
            return;
        }

        if (!ControllerVolumeEnabled)
        {
            driver.SetVolumeContributionMode(DisabledContributionMode);
            return;
        }

        ApplyVolumeSettingsToDriver(driver, EnabledContributionMode);
    }

    [ContextMenu("Apply Profile Defaults")]
    public void ApplyProfileDefaults()
    {
        switch (profile)
        {
            case VolumeProfile.PerformanceCloud:
                ApplyPerformanceProfile();
                break;
            case VolumeProfile.CutDustCloud:
                ApplyCutDustProfile();
                break;
            case VolumeProfile.SoftCloud:
                ApplySoftProfile();
                break;
            default:
                ApplyExplosionProfile();
                break;
        }

        ClampSettings();
    }

    protected abstract void CacheReferences();

    protected virtual void ConfigureRoute()
    {
    }

    protected bool ShouldApplyAutomatically()
    {
        return !Application.isPlaying || applyAutomaticallyInPlayMode;
    }

    protected void ApplyVolumeSettingsToDriver(
        SdfRaymarchDriver driver,
        SdfRaymarchDriver.VolumeContributionMode contributionMode)
    {
        if (driver == null)
        {
            return;
        }

        if (applyDriverPresetBeforeSettings)
        {
            driver.ApplyVolumePreset(driverPreset);
        }

        driver.SetVolumeContributionMode(contributionMode);
        driver.SetCloudShapeSettings(
            SdfRaymarchDriver.VolumeFogShapeMode.NoiseErodedEllipsoid,
            shapeExtents,
            edgeSoftness,
            noiseErosion,
            noiseScale,
            coverage,
            softness,
            detailStrength,
            detailScale,
            warpStrength,
            lobeCount,
            lobeSpread,
            lobeRadius,
            densityBoost);
        driver.SetVolumeMediumSettings(
            lightIntensity,
            lightDensity,
            baseFogDensity,
            heightFogStrength,
            cutFogBoost,
            absorptionDensity,
            exposure,
            colorTint);
        driver.SetVolumeEmissionSettings(emissionIntensity, emissionColor);
        driver.SetVolumeVisibilitySettings(densityThreshold, alphaClipThreshold);
        ApplyWarmCoreLight(driver);
    }

    protected void ApplyWarmCoreLight(SdfRaymarchDriver driver)
    {
        if (driver == null)
        {
            return;
        }

        Vector3 localPosition = warmCoreLocalPosition;
        if (Application.isPlaying && animateWarmCoreLightInPlayMode && warmCoreOrbitRadius > 0.0f)
        {
            float angle = Time.time * warmCoreOrbitSpeed * Mathf.Deg2Rad;
            localPosition += new Vector3(Mathf.Cos(angle), 0.25f * Mathf.Sin(angle * 0.7f), Mathf.Sin(angle)) * warmCoreOrbitRadius;
        }

        driver.SetVolumePointLight(
            enableWarmCoreLight,
            transform.TransformPoint(localPosition),
            warmCoreColor,
            warmCoreIntensity,
            warmCoreRange);
    }

    protected void ClampSettings()
    {
        shapeExtents = new Vector3(
            Mathf.Max(shapeExtents.x, 0.01f),
            Mathf.Max(shapeExtents.y, 0.01f),
            Mathf.Max(shapeExtents.z, 0.01f));
        lobeSpread = new Vector3(
            Mathf.Max(lobeSpread.x, 0.0f),
            Mathf.Max(lobeSpread.y, 0.0f),
            Mathf.Max(lobeSpread.z, 0.0f));
        warmCoreRange = Mathf.Max(0.05f, warmCoreRange);
        warmCoreOrbitRadius = Mathf.Max(0.0f, warmCoreOrbitRadius);
    }

    private void ApplySoftProfile()
    {
        driverPreset = SdfRaymarchDriver.VolumePreset.CinematicWarm;
        shapeExtents = new Vector3(0.82f, 0.58f, 0.78f);
        edgeSoftness = 0.28f;
        noiseErosion = 0.5f;
        noiseScale = 1.45f;
        coverage = 0.5f;
        softness = 0.32f;
        detailStrength = 0.38f;
        detailScale = 6.5f;
        warpStrength = 0.32f;
        lobeCount = 10;
        lobeSpread = new Vector3(0.95f, 0.5f, 0.9f);
        lobeRadius = 0.46f;
        densityBoost = 0.7f;
        lightIntensity = 2.0f;
        lightDensity = 0.55f;
        baseFogDensity = 0.0f;
        heightFogStrength = 0.006f;
        cutFogBoost = 0.8f;
        absorptionDensity = 0.14f;
        densityThreshold = 0.04f;
        alphaClipThreshold = 0.012f;
        exposure = 1.05f;
        colorTint = new Color(1.0f, 0.84f, 0.68f, 1.0f);
        emissionIntensity = 0.0f;
        emissionColor = new Color(1.0f, 0.72f, 0.42f, 1.0f);
        warmCoreIntensity = 10.0f;
        warmCoreRange = 4.6f;
    }

    private void ApplyExplosionProfile()
    {
        driverPreset = SdfRaymarchDriver.VolumePreset.CinematicWarm;
        shapeExtents = new Vector3(0.78f, 0.62f, 0.72f);
        edgeSoftness = 0.22f;
        noiseErosion = 0.72f;
        noiseScale = 1.75f;
        coverage = 0.48f;
        softness = 0.2f;
        detailStrength = 0.64f;
        detailScale = 9.0f;
        warpStrength = 0.62f;
        lobeCount = 16;
        lobeSpread = new Vector3(1.05f, 0.58f, 0.95f);
        lobeRadius = 0.42f;
        densityBoost = 0.95f;
        lightIntensity = 2.8f;
        lightDensity = 0.82f;
        baseFogDensity = 0.0f;
        heightFogStrength = 0.008f;
        cutFogBoost = 0.95f;
        absorptionDensity = 0.18f;
        densityThreshold = 0.045f;
        alphaClipThreshold = 0.012f;
        exposure = 1.2f;
        colorTint = new Color(1.0f, 0.78f, 0.56f, 1.0f);
        emissionIntensity = 0.0f;
        emissionColor = new Color(1.0f, 0.62f, 0.22f, 1.0f);
        warmCoreIntensity = 18.0f;
        warmCoreRange = 4.8f;
    }

    private void ApplyCutDustProfile()
    {
        driverPreset = SdfRaymarchDriver.VolumePreset.CinematicWarm;
        shapeExtents = new Vector3(0.72f, 0.42f, 0.72f);
        edgeSoftness = 0.18f;
        noiseErosion = 0.64f;
        noiseScale = 2.2f;
        coverage = 0.52f;
        softness = 0.18f;
        detailStrength = 0.58f;
        detailScale = 10.0f;
        warpStrength = 0.48f;
        lobeCount = 12;
        lobeSpread = new Vector3(1.0f, 0.32f, 0.9f);
        lobeRadius = 0.34f;
        densityBoost = 0.9f;
        lightIntensity = 2.4f;
        lightDensity = 0.78f;
        baseFogDensity = 0.0f;
        heightFogStrength = 0.006f;
        cutFogBoost = 1.35f;
        absorptionDensity = 0.18f;
        densityThreshold = 0.04f;
        alphaClipThreshold = 0.012f;
        exposure = 1.12f;
        colorTint = new Color(1.0f, 0.78f, 0.62f, 1.0f);
        emissionIntensity = 0.0f;
        emissionColor = new Color(1.0f, 0.58f, 0.22f, 1.0f);
        warmCoreIntensity = 12.0f;
        warmCoreRange = 4.6f;
    }

    private void ApplyPerformanceProfile()
    {
        driverPreset = SdfRaymarchDriver.VolumePreset.Performance;
        shapeExtents = new Vector3(0.74f, 0.52f, 0.7f);
        edgeSoftness = 0.24f;
        noiseErosion = 0.45f;
        noiseScale = 1.4f;
        coverage = 0.46f;
        softness = 0.34f;
        detailStrength = 0.16f;
        detailScale = 5.0f;
        warpStrength = 0.2f;
        lobeCount = 8;
        lobeSpread = new Vector3(0.9f, 0.46f, 0.85f);
        lobeRadius = 0.44f;
        densityBoost = 0.55f;
        lightIntensity = 1.8f;
        lightDensity = 0.48f;
        baseFogDensity = 0.0f;
        heightFogStrength = 0.004f;
        cutFogBoost = 0.65f;
        absorptionDensity = 0.1f;
        densityThreshold = 0.05f;
        alphaClipThreshold = 0.014f;
        exposure = 1.0f;
        colorTint = new Color(1.0f, 0.84f, 0.68f, 1.0f);
        emissionIntensity = 0.0f;
        emissionColor = Color.black;
        warmCoreIntensity = 8.0f;
        warmCoreRange = 4.2f;
    }
}
