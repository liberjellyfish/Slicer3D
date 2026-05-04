using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
[DisallowMultipleComponent]
public class SdfValidationEnvironmentController : MonoBehaviour
{
    public enum ValidationMode
    {
        Normal = 0,
        SoftShadow = 1,
        CutSurface = 2,
        Volume = 3
    }

    [Header("References")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private SdfLightingValidationRig lightingValidationRig;
    [SerializeField] private Renderer backdropRenderer;
    [SerializeField] private GameObject dustVisualizationRoot;
    [SerializeField] private SdfPhase1Driver[] sdfDrivers = System.Array.Empty<SdfPhase1Driver>();

    [Header("Mode")]
    [SerializeField] private ValidationMode validationMode = ValidationMode.Normal;
    [SerializeField] private bool applyOnEnable = true;
    [SerializeField] private bool applyOnValidate = true;

    [Header("Lighting")]
    [SerializeField] private float normalAmbientIntensity = 1.0f;
    [SerializeField] private float validationAmbientIntensity = 0.35f;
    [SerializeField] private float normalLightIntensity = 1.0f;
    [SerializeField] private float validationLightIntensity = 1.45f;
    [SerializeField] private Color normalLightColor = new Color(1.0f, 0.95686275f, 0.8392157f, 1.0f);
    [SerializeField] private Color validationLightColor = new Color(0.97f, 0.97f, 1.0f, 1.0f);
    [SerializeField] private bool useValidationCookie = true;
    [SerializeField] [Range(0.1f, 1.0f)] private float cookieEdgeSoftness = 0.72f;
    [SerializeField] [Range(0.0f, 1.0f)] private float cookieCenterBias = 0.35f;
    [SerializeField] [Min(16)] private int cookieResolution = 128;

    [Header("Backdrop")]
    [SerializeField] private bool showBackdropInValidationModes = true;
    [SerializeField] private Color normalBackdropColor = new Color(0.28f, 0.29f, 0.32f, 1.0f);
    [SerializeField] private Color validationBackdropColor = new Color(0.12f, 0.14f, 0.18f, 1.0f);

    [Header("Rig")]
    [SerializeField] private bool animateLightInValidationModes = true;
    [SerializeField] private bool pulseLightIntensityInVolumeMode = false;

    [Header("Runtime Debug")]
    [SerializeField] private bool enableRuntimeDebugHotkeys = true;
    [SerializeField] private KeyCode lightingDebugKey = KeyCode.F1;
    [SerializeField] private KeyCode volumeDensityDebugKey = KeyCode.F2;
    [SerializeField] private KeyCode volumeTransmittanceDebugKey = KeyCode.F3;
    [SerializeField] private KeyCode volumeShadowDebugKey = KeyCode.F4;
    [SerializeField] private KeyCode volumeCompositeDebugKey = KeyCode.F5;

    private Texture2D generatedCookie;
    private Mesh generatedBackdropMesh;
    private LightShadows cachedShadowMode;
    private float cachedAmbientIntensity = -1.0f;
    private Material generatedBackdropMaterial;

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
        AutoResolveReferences(false);
        if (Application.isPlaying && applyOnValidate)
        {
            ApplyCurrentMode();
        }
    }

    private void Update()
    {
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

        bool validationActive = validationMode != ValidationMode.Normal;
        ApplyLighting(validationActive);
        ApplyBackdrop(validationActive);
        ApplyDust(validationActive && validationMode == ValidationMode.Volume);
        ApplyRig(validationActive);
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
        RefreshDustRoot();
    }

    private void CacheDefaults()
    {
        if (cachedAmbientIntensity < 0.0f)
        {
            cachedAmbientIntensity = RenderSettings.ambientIntensity;
        }

        if (directionalLight != null)
        {
            cachedShadowMode = directionalLight.shadows;
        }
    }

    private void ApplyLighting(bool validationActive)
    {
        RenderSettings.ambientIntensity = validationActive ? validationAmbientIntensity : normalAmbientIntensity;

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
            enableIntensityPulse: validationMode == ValidationMode.Volume && pulseLightIntensityInVolumeMode);
    }

    private void ApplyDriverDebug(ValidationMode mode)
    {
        SdfPhase1Driver.DebugViewMode debugView = SdfPhase1Driver.DebugViewMode.Lighting;
        switch (mode)
        {
            case ValidationMode.SoftShadow:
                debugView = SdfPhase1Driver.DebugViewMode.TotalShadow;
                break;
            case ValidationMode.CutSurface:
                debugView = SdfPhase1Driver.DebugViewMode.CutDominance;
                break;
            case ValidationMode.Volume:
                debugView = SdfPhase1Driver.DebugViewMode.VolumeLight;
                break;
        }

        ApplyDebugView(debugView);
    }

    private void HandleRuntimeDebugHotkeys()
    {
        if (Input.GetKeyDown(lightingDebugKey))
        {
            ApplyDebugView(SdfPhase1Driver.DebugViewMode.Lighting);
        }
        else if (Input.GetKeyDown(volumeDensityDebugKey))
        {
            ApplyDebugView(SdfPhase1Driver.DebugViewMode.VolumeDensity);
        }
        else if (Input.GetKeyDown(volumeTransmittanceDebugKey))
        {
            ApplyDebugView(SdfPhase1Driver.DebugViewMode.VolumeTransmittance);
        }
        else if (Input.GetKeyDown(volumeShadowDebugKey))
        {
            ApplyDebugView(SdfPhase1Driver.DebugViewMode.VolumeShadow);
        }
        else if (Input.GetKeyDown(volumeCompositeDebugKey))
        {
            ApplyDebugView(SdfPhase1Driver.DebugViewMode.VolumeLight);
        }
    }

    private void ApplyDebugView(SdfPhase1Driver.DebugViewMode debugView)
    {
        RefreshDrivers();
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

            sdfDrivers[i].SetDebugView(debugView);
        }
    }

    private void RefreshDrivers()
    {
        SdfPhase1Driver[] foundDrivers = Object.FindObjectsByType<SdfPhase1Driver>(FindObjectsSortMode.None);
        if (foundDrivers == null || foundDrivers.Length <= 0)
        {
            return;
        }

        List<SdfPhase1Driver> runtimeDrivers = new List<SdfPhase1Driver>(foundDrivers.Length);
        for (int i = 0; i < foundDrivers.Length; i++)
        {
            if (foundDrivers[i] != null && foundDrivers[i].gameObject.scene.IsValid())
            {
                runtimeDrivers.Add(foundDrivers[i]);
            }
        }

        sdfDrivers = runtimeDrivers.ToArray();
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
