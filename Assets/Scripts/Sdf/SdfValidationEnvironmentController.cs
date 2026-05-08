using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
public class SdfValidationEnvironmentController : MonoBehaviour
{
    public enum ValidationMode
    {
        Normal = 0,
        SoftShadow = 1,
        CutSurface = 2,
        Volume = 3,
        Surface = 4,
        VolumeDensity = 5,
        VolumeTransmittance = 6,
        VolumeShadow = 7,
        VolumeComposite = 8,
        FinalLighting = 9,
        VolumeGeometryShadow = 10,
        VolumeMediaTransmittance = 11,
        VolumeSigmaA = 12,
        VolumeSigmaS = 13,
        VolumeSigmaT = 14,
        VolumeShapeMask = 15
    }

    [Header("References")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private SdfLightingValidationRig lightingValidationRig;
    [SerializeField] private Renderer backdropRenderer;
    [SerializeField] private GameObject dustVisualizationRoot;
    [SerializeField] private SdfPhase1Driver[] sdfDrivers = System.Array.Empty<SdfPhase1Driver>();
    [SerializeField] private SdfSharedVolumeProxy sharedVolumeProxy;
    [SerializeField] private Camera[] validationCameras = System.Array.Empty<Camera>();

    [Header("Mode")]
    [SerializeField] private ValidationMode validationMode = ValidationMode.Normal;
    [SerializeField] private bool applyOnEnable = true;
    [SerializeField] private bool applyOnValidate = true;

    [Header("Lighting")]
    [SerializeField] private float normalAmbientIntensity = 1.0f;
    [SerializeField] private float validationAmbientIntensity = 0.35f;
    [SerializeField] private bool useDarkValidationSky = true;
    [SerializeField] private Color validationAmbientColor = new Color(0.035f, 0.033f, 0.03f, 1.0f);
    [SerializeField] private Color validationCameraBackgroundColor = new Color(0.045f, 0.043f, 0.04f, 1.0f);
    [SerializeField] private float normalLightIntensity = 1.0f;
    [SerializeField] private float validationLightIntensity = 1.45f;
    [SerializeField] private Color normalLightColor = new Color(1.0f, 0.95686275f, 0.8392157f, 1.0f);
    [SerializeField] private Color validationLightColor = new Color(0.97f, 0.97f, 1.0f, 1.0f);
    [SerializeField] private bool useValidationCookie = true;
    [SerializeField] [Range(0.1f, 1.0f)] private float cookieEdgeSoftness = 0.72f;
    [SerializeField] [Range(0.0f, 1.0f)] private float cookieCenterBias = 0.35f;
    [SerializeField] [Min(16)] private int cookieResolution = 128;

    [Header("Backdrop")]
    [SerializeField] private bool showBackdropInValidationModes = false;
    [SerializeField] private Color normalBackdropColor = new Color(0.28f, 0.29f, 0.32f, 1.0f);
    [SerializeField] private Color validationBackdropColor = new Color(0.12f, 0.14f, 0.18f, 1.0f);

    [Header("Rig")]
    [SerializeField] private bool animateLightInValidationModes = true;
    [SerializeField] private bool pulseLightIntensityInVolumeMode = false;

    [Header("Volume Preset")]
    [SerializeField] private bool applyVolumePresetInValidationModes = true;
    [SerializeField] private SdfPhase1Driver.VolumePreset volumePreset = SdfPhase1Driver.VolumePreset.CinematicWarm;

    [Header("Volume Ownership")]
    [SerializeField] private bool enforceSingleVolumeBackground = true;
    [SerializeField] private SdfPhase1Driver.VolumeContributionMode nonOwnerVolumeContributionMode = SdfPhase1Driver.VolumeContributionMode.SurfaceOnly;

    [Header("Virtual Point Light")]
    [SerializeField] private bool enableVirtualPointLight = true;
    [SerializeField] private Transform virtualPointLightAnchor;
    [SerializeField] private bool animateVirtualPointLight = true;
    [SerializeField] private Vector3 virtualPointLightCenter = new Vector3(0.0f, 0.4f, -0.25f);
    [SerializeField] [Min(0.05f)] private float virtualPointLightOrbitRadius = 2.35f;
    [SerializeField] private float virtualPointLightHeight = 1.35f;
    [SerializeField] private float virtualPointLightYawSpeed = 24.0f;
    [SerializeField] private Color virtualPointLightColor = new Color(1.0f, 0.76f, 0.48f, 1.0f);
    [SerializeField] [Min(0.0f)] private float virtualPointLightIntensity = 18.0f;
    [SerializeField] [Min(0.05f)] private float virtualPointLightRange = 6.0f;

    [Header("Runtime Debug")]
    [SerializeField] private bool enableRuntimeDebugHotkeys = true;
    [SerializeField] private KeyCode lightingDebugKey = KeyCode.F1;
    [SerializeField] private KeyCode volumeDensityDebugKey = KeyCode.F2;
    [SerializeField] private KeyCode volumeTransmittanceDebugKey = KeyCode.F3;
    [SerializeField] private KeyCode volumeShadowDebugKey = KeyCode.F4;
    [SerializeField] private KeyCode volumeCompositeDebugKey = KeyCode.F5;
    [SerializeField] private KeyCode volumeGeometryShadowDebugKey = KeyCode.F6;
    [SerializeField] private KeyCode volumeMediaShadowDebugKey = KeyCode.F7;

    private Texture2D generatedCookie;
    private Mesh generatedBackdropMesh;
    private LightShadows cachedShadowMode;
    private float cachedAmbientIntensity = -1.0f;
    private AmbientMode cachedAmbientMode;
    private Color cachedAmbientLight;
    private Material cachedSkyboxMaterial;
    private Material generatedBackdropMaterial;
    private CameraClearFlags[] cachedCameraClearFlags = System.Array.Empty<CameraClearFlags>();
    private Color[] cachedCameraBackgroundColors = System.Array.Empty<Color>();
    private bool hasCachedCameraState;

    private void OnEnable()
    {
        AutoResolveReferences(false);
        CacheDefaults();
        if (Application.isPlaying && applyOnEnable)
        {
            ApplyCurrentMode();
        }
    }

    private void OnValidate()
    {
        cookieResolution = Mathf.Max(16, cookieResolution);
        virtualPointLightOrbitRadius = Mathf.Max(0.05f, virtualPointLightOrbitRadius);
        virtualPointLightRange = Mathf.Max(0.05f, virtualPointLightRange);
        AutoResolveReferences(false);
        if (Application.isPlaying && applyOnValidate)
        {
            ApplyCurrentMode();
        }
    }

    private void Update()
    {
        if (Application.isPlaying && IsVolumeValidationMode(validationMode))
        {
            UpdateVirtualPointLight();
        }

        if (Application.isPlaying && enforceSingleVolumeBackground)
        {
            ApplyVolumeBackgroundOwnership();
        }

        if (!Application.isPlaying || !enableRuntimeDebugHotkeys)
        {
            return;
        }

        HandleRuntimeDebugHotkeys();
    }

    [ContextMenu("Apply Current Mode")]
    public void ApplyCurrentMode()
    {
        AutoResolveReferences(true);
        CacheDefaults();

        bool validationActive = IsValidationModeActive(validationMode);
        bool volumeValidationActive = IsVolumeValidationMode(validationMode);
        ApplyLighting(validationActive);
        ApplyBackdrop(validationActive);
        ApplyDust(volumeValidationActive);
        ApplyRig(validationActive);
        ApplyVolumePresetIfNeeded(volumeValidationActive);
        ApplyVolumeBackgroundOwnership();
        if (volumeValidationActive)
        {
            UpdateVirtualPointLight();
        }
        ApplyDriverDebug(validationMode);
    }

    [ContextMenu("Capture Current As Normal Lighting")]
    public void CaptureCurrentAsNormalLighting()
    {
        if (directionalLight != null)
        {
            normalLightIntensity = directionalLight.intensity;
            normalLightColor = directionalLight.color;
            cachedShadowMode = directionalLight.shadows;
        }

        normalAmbientIntensity = RenderSettings.ambientIntensity;
        if (backdropRenderer != null && backdropRenderer.sharedMaterial != null)
        {
            normalBackdropColor = backdropRenderer.sharedMaterial.color;
        }
    }

    [ContextMenu("Apply Volume Preset To Drivers")]
    public void ApplyVolumePresetToDrivers()
    {
        AutoResolveReferences(true);
        ApplyVolumePresetIfNeeded(true);
        ApplyVolumeBackgroundOwnership();
        UpdateVirtualPointLight();
    }

    [ContextMenu("Auto Resolve References")]
    public void AutoResolveReferences()
    {
        AutoResolveReferences(false);
    }

    private void AutoResolveReferences(bool allowSceneMutation)
    {
        if (directionalLight == null)
        {
            directionalLight = RenderSettings.sun;
        }

        if (directionalLight == null)
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                {
                    directionalLight = lights[i];
                    break;
                }
            }
        }

        if (lightingValidationRig == null && directionalLight != null)
        {
            lightingValidationRig = directionalLight.GetComponent<SdfLightingValidationRig>();
        }

        if (allowSceneMutation && backdropRenderer == null)
        {
            EnsureBackdropExists();
        }

        RefreshDrivers();
        ResolveSharedVolumeProxy(allowSceneMutation);
        RefreshDrivers();
        SyncSharedVolumeProxySources();
        RefreshDustRoot();
        RefreshValidationCameras();
    }

    private void CacheDefaults()
    {
        if (cachedAmbientIntensity < 0.0f)
        {
            cachedAmbientIntensity = RenderSettings.ambientIntensity;
            cachedAmbientMode = RenderSettings.ambientMode;
            cachedAmbientLight = RenderSettings.ambientLight;
            cachedSkyboxMaterial = RenderSettings.skybox;
        }

        if (directionalLight != null)
        {
            cachedShadowMode = directionalLight.shadows;
        }

        CacheCameraDefaults();
    }

    private static bool IsValidationModeActive(ValidationMode mode)
    {
        return mode != ValidationMode.Normal;
    }

    private static bool IsVolumeValidationMode(ValidationMode mode)
    {
        return mode == ValidationMode.Volume
            || mode == ValidationMode.VolumeDensity
            || mode == ValidationMode.VolumeTransmittance
            || mode == ValidationMode.VolumeShadow
            || mode == ValidationMode.VolumeComposite
            || mode == ValidationMode.FinalLighting
            || mode == ValidationMode.VolumeGeometryShadow
            || mode == ValidationMode.VolumeMediaTransmittance
            || mode == ValidationMode.VolumeSigmaA
            || mode == ValidationMode.VolumeSigmaS
            || mode == ValidationMode.VolumeSigmaT
            || mode == ValidationMode.VolumeShapeMask;
    }

    private void CacheCameraDefaults()
    {
        if (hasCachedCameraState || validationCameras == null)
        {
            return;
        }

        cachedCameraClearFlags = new CameraClearFlags[validationCameras.Length];
        cachedCameraBackgroundColors = new Color[validationCameras.Length];
        for (int i = 0; i < validationCameras.Length; i++)
        {
            if (validationCameras[i] == null)
            {
                continue;
            }

            cachedCameraClearFlags[i] = validationCameras[i].clearFlags;
            cachedCameraBackgroundColors[i] = validationCameras[i].backgroundColor;
        }

        hasCachedCameraState = true;
    }

    private void ApplyCameraBackground(bool validationActive)
    {
        if (validationCameras == null || validationCameras.Length <= 0)
        {
            return;
        }

        for (int i = 0; i < validationCameras.Length; i++)
        {
            Camera targetCamera = validationCameras[i];
            if (targetCamera == null)
            {
                continue;
            }

            if (validationActive)
            {
                targetCamera.clearFlags = CameraClearFlags.SolidColor;
                targetCamera.backgroundColor = validationCameraBackgroundColor;
                continue;
            }

            if (hasCachedCameraState && i < cachedCameraClearFlags.Length && i < cachedCameraBackgroundColors.Length)
            {
                targetCamera.clearFlags = cachedCameraClearFlags[i];
                targetCamera.backgroundColor = cachedCameraBackgroundColors[i];
            }
        }
    }

    private void ApplyLighting(bool validationActive)
    {
        RenderSettings.ambientIntensity = validationActive ? validationAmbientIntensity : normalAmbientIntensity;
        if (validationActive && useDarkValidationSky)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = validationAmbientColor;
            ApplyCameraBackground(validationActive);
        }
        else if (!validationActive)
        {
            RenderSettings.skybox = cachedSkyboxMaterial;
            RenderSettings.ambientMode = cachedAmbientMode;
            RenderSettings.ambientLight = cachedAmbientLight;
            ApplyCameraBackground(validationActive);
        }

        if (directionalLight == null)
        {
            return;
        }

        directionalLight.intensity = validationActive ? validationLightIntensity : normalLightIntensity;
        directionalLight.color = validationActive ? validationLightColor : normalLightColor;
        directionalLight.shadows = validationActive ? LightShadows.Soft : cachedShadowMode;

        if (!useValidationCookie)
        {
            if (!validationActive)
            {
                directionalLight.cookie = null;
            }

            return;
        }

        directionalLight.cookie = validationActive ? GetOrCreateCookie() : null;
    }

    private void ApplyBackdrop(bool validationActive)
    {
        if (backdropRenderer == null)
        {
            return;
        }

        if (backdropRenderer == null)
        {
            return;
        }

        bool shouldShow = validationActive && showBackdropInValidationModes;
        backdropRenderer.enabled = shouldShow;
        if (!shouldShow || backdropRenderer.sharedMaterial == null)
        {
            return;
        }

        backdropRenderer.sharedMaterial.color = validationActive ? validationBackdropColor : normalBackdropColor;
    }

    private void ApplyDust(bool showDust)
    {
        if (dustVisualizationRoot != null)
        {
            dustVisualizationRoot.SetActive(showDust);
        }
    }

    private void ApplyRig(bool validationActive)
    {
        if (lightingValidationRig == null)
        {
            return;
        }

        lightingValidationRig.AnimateInPlayMode = validationActive && animateLightInValidationModes;
        lightingValidationRig.ConfigureSweep(
            enableYawRotation: validationActive,
            enablePitchOscillation: validationActive,
            enableIntensityPulse: IsVolumeValidationMode(validationMode) && pulseLightIntensityInVolumeMode);
    }

    private void ApplyVolumePresetIfNeeded(bool volumeValidationActive)
    {
        if (!applyVolumePresetInValidationModes || !volumeValidationActive)
        {
            return;
        }

        RefreshDrivers();
        if (sharedVolumeProxy != null)
        {
            sharedVolumeProxy.ApplyVolumePreset(volumePreset);
            ApplySharedVolumeCloudController();
            ApplySurfaceOnlyToDrivers();
            return;
        }

        if (sdfDrivers == null)
        {
            return;
        }

        for (int i = 0; i < sdfDrivers.Length; i++)
        {
            if (sdfDrivers[i] != null)
            {
                sdfDrivers[i].ApplyVolumePreset(volumePreset);
            }
        }
    }

    [ContextMenu("Apply Volume Background Ownership")]
    public void ApplyVolumeBackgroundOwnership()
    {
        if (!enforceSingleVolumeBackground)
        {
            return;
        }

        RefreshDrivers();
        if (sharedVolumeProxy != null)
        {
            sharedVolumeProxy.SetSurfaceDrivers(sdfDrivers);
            if (sharedVolumeProxy.VolumeDriver != null)
            {
                sharedVolumeProxy.VolumeDriver.SetVolumeContributionMode(SdfPhase1Driver.VolumeContributionMode.VolumeOnly);
            }

            ApplySurfaceOnlyToDrivers();
            return;
        }

        if (sdfDrivers == null || sdfDrivers.Length <= 0)
        {
            return;
        }

        SdfPhase1Driver owner = FindVolumeBackgroundOwner();
        for (int i = 0; i < sdfDrivers.Length; i++)
        {
            if (sdfDrivers[i] == null)
            {
                continue;
            }

            SdfPhase1Driver.VolumeContributionMode mode = sdfDrivers[i] == owner
                ? SdfPhase1Driver.VolumeContributionMode.Full
                : nonOwnerVolumeContributionMode;
            sdfDrivers[i].SetVolumeContributionMode(mode);
        }
    }

    private void ApplySurfaceOnlyToDrivers()
    {
        if (sdfDrivers == null)
        {
            return;
        }

        for (int i = 0; i < sdfDrivers.Length; i++)
        {
            if (sdfDrivers[i] == null)
            {
                continue;
            }

            sdfDrivers[i].SetVolumeContributionMode(SdfPhase1Driver.VolumeContributionMode.SurfaceOnly);
        }
    }

    private void ApplySharedVolumeCloudController()
    {
        if (sharedVolumeProxy == null)
        {
            return;
        }

        SdfCloudVolumeController cloudController = sharedVolumeProxy.GetComponent<SdfCloudVolumeController>();
        if (cloudController != null)
        {
            cloudController.ApplyToDriver();
        }
    }

    private SdfPhase1Driver FindVolumeBackgroundOwner()
    {
        Bounds combinedBounds = new Bounds();
        bool hasBounds = false;
        for (int i = 0; i < sdfDrivers.Length; i++)
        {
            if (sdfDrivers[i] == null)
            {
                continue;
            }

            Bounds driverBounds = sdfDrivers[i].GetWorldBounds();
            if (!hasBounds)
            {
                combinedBounds = driverBounds;
                hasBounds = true;
                continue;
            }

            combinedBounds.Encapsulate(driverBounds);
        }

        if (!hasBounds)
        {
            return null;
        }

        Vector3 targetCenter = combinedBounds.center;
        SdfPhase1Driver bestDriver = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < sdfDrivers.Length; i++)
        {
            if (sdfDrivers[i] == null)
            {
                continue;
            }

            Bounds driverBounds = sdfDrivers[i].GetWorldBounds();
            float distance = (driverBounds.center - targetCenter).sqrMagnitude;
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestDriver = sdfDrivers[i];
        }

        return bestDriver;
    }

    private void ResolveSharedVolumeProxy(bool allowSceneMutation)
    {
        if (sharedVolumeProxy == null)
        {
            SdfSharedVolumeProxy[] foundProxies = Object.FindObjectsByType<SdfSharedVolumeProxy>(FindObjectsSortMode.None);
            if (foundProxies.Length > 0)
            {
                sharedVolumeProxy = foundProxies[0];
            }
        }

        if (sharedVolumeProxy != null || !allowSceneMutation)
        {
            return;
        }

        GameObject proxyObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        proxyObject.name = "SdfSharedVolumeProxy";
        proxyObject.transform.SetParent(transform, false);

        Collider generatedCollider = proxyObject.GetComponent<Collider>();
        if (generatedCollider != null)
        {
            if (Application.isPlaying)
            {
                Destroy(generatedCollider);
            }
            else
            {
                DestroyImmediate(generatedCollider);
            }
        }

        MeshRenderer proxyRenderer = proxyObject.GetComponent<MeshRenderer>();
        Material sourceMaterial = GetFirstSdfMaterial();
        if (proxyRenderer != null && sourceMaterial != null)
        {
            proxyRenderer.sharedMaterial = sourceMaterial;
        }

        SdfPhase1Driver proxyDriver = proxyObject.AddComponent<SdfPhase1Driver>();
        proxyDriver.SetVolumeContributionMode(SdfPhase1Driver.VolumeContributionMode.VolumeOnly);
        sharedVolumeProxy = proxyObject.AddComponent<SdfSharedVolumeProxy>();
        sharedVolumeProxy.SetSurfaceDrivers(sdfDrivers);
    }

    private Material GetFirstSdfMaterial()
    {
        if (sdfDrivers == null)
        {
            return null;
        }

        for (int i = 0; i < sdfDrivers.Length; i++)
        {
            if (sdfDrivers[i] == null)
            {
                continue;
            }

            Material material = sdfDrivers[i].GetSharedMaterial();
            if (material != null)
            {
                return material;
            }
        }

        return null;
    }

    private void SyncSharedVolumeProxySources()
    {
        if (sharedVolumeProxy != null)
        {
            sharedVolumeProxy.SetSurfaceDrivers(sdfDrivers);
        }
    }

    private void UpdateVirtualPointLight()
    {
        RefreshDrivers();
        if (sdfDrivers == null || sdfDrivers.Length <= 0)
        {
            return;
        }

        Vector3 pointLightPosition = GetVirtualPointLightPosition();
        if (sharedVolumeProxy != null && sharedVolumeProxy.VolumeDriver != null)
        {
            sharedVolumeProxy.VolumeDriver.SetVolumePointLight(
                enableVirtualPointLight,
                pointLightPosition,
                virtualPointLightColor,
                virtualPointLightIntensity,
                virtualPointLightRange);
        }

        for (int i = 0; i < sdfDrivers.Length; i++)
        {
            if (sdfDrivers[i] == null)
            {
                continue;
            }

            sdfDrivers[i].SetVolumePointLight(
                enableVirtualPointLight,
                pointLightPosition,
                virtualPointLightColor,
                virtualPointLightIntensity,
                virtualPointLightRange);
        }
    }

    private Vector3 GetVirtualPointLightPosition()
    {
        if (virtualPointLightAnchor != null)
        {
            return virtualPointLightAnchor.position;
        }

        float angle = 0.0f;
        if (Application.isPlaying && animateVirtualPointLight)
        {
            angle = Time.time * virtualPointLightYawSpeed * Mathf.Deg2Rad;
        }

        Vector3 orbitOffset = new Vector3(
            Mathf.Cos(angle) * virtualPointLightOrbitRadius,
            virtualPointLightHeight,
            Mathf.Sin(angle) * virtualPointLightOrbitRadius);
        return virtualPointLightCenter + orbitOffset;
    }

    private void ApplyDriverDebug(ValidationMode mode)
    {
        SdfPhase1Driver.DebugViewMode debugView = SdfPhase1Driver.DebugViewMode.Lighting;
        switch (mode)
        {
            case ValidationMode.SoftShadow:
                debugView = SdfPhase1Driver.DebugViewMode.SdfSoftShadowReadable;
                break;
            case ValidationMode.CutSurface:
                debugView = SdfPhase1Driver.DebugViewMode.CutDominance;
                break;
            case ValidationMode.Volume:
                debugView = SdfPhase1Driver.DebugViewMode.VolumeLight;
                break;
            case ValidationMode.VolumeDensity:
                debugView = SdfPhase1Driver.DebugViewMode.VolumeDensity;
                break;
            case ValidationMode.VolumeTransmittance:
                debugView = SdfPhase1Driver.DebugViewMode.VolumeTransmittance;
                break;
            case ValidationMode.VolumeShadow:
                debugView = SdfPhase1Driver.DebugViewMode.VolumeShadow;
                break;
            case ValidationMode.VolumeComposite:
                debugView = SdfPhase1Driver.DebugViewMode.VolumeLight;
                break;
            case ValidationMode.VolumeGeometryShadow:
                debugView = SdfPhase1Driver.DebugViewMode.VolumeGeometryShadow;
                break;
            case ValidationMode.VolumeMediaTransmittance:
                debugView = SdfPhase1Driver.DebugViewMode.VolumeMediaTransmittance;
                break;
            case ValidationMode.VolumeSigmaA:
                debugView = SdfPhase1Driver.DebugViewMode.VolumeSigmaA;
                break;
            case ValidationMode.VolumeSigmaS:
                debugView = SdfPhase1Driver.DebugViewMode.VolumeSigmaS;
                break;
            case ValidationMode.VolumeSigmaT:
                debugView = SdfPhase1Driver.DebugViewMode.VolumeSigmaT;
                break;
            case ValidationMode.VolumeShapeMask:
                debugView = SdfPhase1Driver.DebugViewMode.VolumeShapeMask;
                break;
        }

        ApplyDebugView(debugView, IsVolumeValidationMode(mode));
    }

    private void HandleRuntimeDebugHotkeys()
    {
        if (Input.GetKeyDown(lightingDebugKey))
        {
            ApplyRuntimeDebugMode(ValidationMode.FinalLighting);
        }
        else if (Input.GetKeyDown(volumeDensityDebugKey))
        {
            ApplyRuntimeDebugMode(ValidationMode.VolumeDensity);
        }
        else if (Input.GetKeyDown(volumeTransmittanceDebugKey))
        {
            ApplyRuntimeDebugMode(ValidationMode.VolumeTransmittance);
        }
        else if (Input.GetKeyDown(volumeShadowDebugKey))
        {
            ApplyRuntimeDebugMode(ValidationMode.VolumeShadow);
        }
        else if (Input.GetKeyDown(volumeCompositeDebugKey))
        {
            ApplyRuntimeDebugMode(ValidationMode.VolumeComposite);
        }
        else if (Input.GetKeyDown(volumeGeometryShadowDebugKey))
        {
            ApplyRuntimeDebugMode(ValidationMode.VolumeGeometryShadow);
        }
        else if (Input.GetKeyDown(volumeMediaShadowDebugKey))
        {
            ApplyRuntimeDebugMode(ValidationMode.VolumeMediaTransmittance);
        }
    }

    private void ApplyRuntimeDebugMode(ValidationMode mode)
    {
        validationMode = mode;
        ApplyCurrentMode();
    }

    private void ApplyDebugView(SdfPhase1Driver.DebugViewMode debugView, bool volumeMode)
    {
        RefreshDrivers();
        if (sharedVolumeProxy != null && sharedVolumeProxy.VolumeDriver != null)
        {
            sharedVolumeProxy.VolumeDriver.SetDebugView(debugView);
        }

        if (sdfDrivers == null || sdfDrivers.Length <= 0)
        {
            return;
        }

        for (int i = 0; i < sdfDrivers.Length; i++)
        {
            if (sdfDrivers[i] == null)
            {
                continue;
            }

            sdfDrivers[i].SetDebugView(volumeMode && sharedVolumeProxy != null
                ? SdfPhase1Driver.DebugViewMode.Lighting
                : debugView);
        }
    }

    private void RefreshDrivers()
    {
        sdfDrivers = SdfSceneDriverUtility.FindSurfaceDrivers();
    }

    private void RefreshDustRoot()
    {
        if (dustVisualizationRoot != null)
        {
            return;
        }

        SdfCutDustFieldController[] dustControllers = Object.FindObjectsByType<SdfCutDustFieldController>(FindObjectsSortMode.None);
        SdfCutDustFieldController dustController = dustControllers.Length > 0 ? dustControllers[0] : null;

        if (dustController != null && dustController.DustRoot != null)
        {
            dustVisualizationRoot = dustController.DustRoot.gameObject;
        }
    }

    private void RefreshValidationCameras()
    {
        if (validationCameras != null && validationCameras.Length > 0)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            validationCameras = new[] { mainCamera };
            return;
        }

        Camera[] foundCameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        List<Camera> sceneCameras = new List<Camera>(foundCameras.Length);
        for (int i = 0; i < foundCameras.Length; i++)
        {
            if (foundCameras[i] != null && foundCameras[i].gameObject.scene.IsValid())
            {
                sceneCameras.Add(foundCameras[i]);
            }
        }

        validationCameras = sceneCameras.ToArray();
    }

    private void EnsureBackdropExists()
    {
        Transform existingBackdrop = transform.Find("SdfValidationBackdrop");
        if (existingBackdrop != null)
        {
            backdropRenderer = existingBackdrop.GetComponent<Renderer>();
            return;
        }

        GameObject backdrop = new GameObject("SdfValidationBackdrop");
        backdrop.name = "SdfValidationBackdrop";
        backdrop.transform.SetParent(transform, false);
        backdrop.transform.localPosition = new Vector3(0.0f, 0.0f, 5.5f);
        backdrop.transform.localRotation = Quaternion.identity;
        backdrop.transform.localScale = new Vector3(8.0f, 8.0f, 1.0f);

        MeshFilter meshFilter = backdrop.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = GetOrCreateBackdropMesh();
        backdropRenderer = backdrop.AddComponent<MeshRenderer>();
        if (backdropRenderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            generatedBackdropMaterial = new Material(shader)
            {
                name = "SdfValidationBackdropMaterial",
                color = normalBackdropColor
            };
            backdropRenderer.sharedMaterial = generatedBackdropMaterial;
            backdropRenderer.enabled = false;
        }
    }

    private Mesh GetOrCreateBackdropMesh()
    {
        if (generatedBackdropMesh != null)
        {
            return generatedBackdropMesh;
        }

        generatedBackdropMesh = new Mesh
        {
            name = "SdfValidationBackdropMesh"
        };

        generatedBackdropMesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0.0f),
            new Vector3(0.5f, -0.5f, 0.0f),
            new Vector3(-0.5f, 0.5f, 0.0f),
            new Vector3(0.5f, 0.5f, 0.0f)
        };
        generatedBackdropMesh.uv = new[]
        {
            new Vector2(0.0f, 0.0f),
            new Vector2(1.0f, 0.0f),
            new Vector2(0.0f, 1.0f),
            new Vector2(1.0f, 1.0f)
        };
        generatedBackdropMesh.triangles = new[]
        {
            0, 2, 1,
            2, 3, 1
        };
        generatedBackdropMesh.RecalculateNormals();
        generatedBackdropMesh.RecalculateBounds();
        return generatedBackdropMesh;
    }

    private Texture2D GetOrCreateCookie()
    {
        if (generatedCookie != null && generatedCookie.width == cookieResolution)
        {
            return generatedCookie;
        }

        generatedCookie = new Texture2D(cookieResolution, cookieResolution, TextureFormat.RGBA32, false, true)
        {
            name = "SdfValidationCookie",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float radiusScale = Mathf.Lerp(1.1f, 0.35f, cookieEdgeSoftness);
        for (int y = 0; y < cookieResolution; y++)
        {
            for (int x = 0; x < cookieResolution; x++)
            {
                float2 uv = new float2(
                    (x + 0.5f) / cookieResolution * 2.0f - 1.0f,
                    (y + 0.5f) / cookieResolution * 2.0f - 1.0f);
                float radial = Mathf.Sqrt(uv.x * uv.x + uv.y * uv.y) / radiusScale;
                float vignette = 1.0f - Mathf.SmoothStep(cookieCenterBias, 1.0f, radial);
                float streak = 0.5f + 0.5f * Mathf.Sin((uv.x + uv.y * 0.35f) * 7.0f);
                float cookie = Mathf.Clamp01(vignette * Mathf.Lerp(0.75f, 1.0f, streak));
                generatedCookie.SetPixel(x, y, new Color(cookie, cookie, cookie, cookie));
            }
        }

        generatedCookie.Apply(false, false);
        return generatedCookie;
    }

    private readonly struct float2
    {
        public readonly float x;
        public readonly float y;

        public float2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }
    }
}
