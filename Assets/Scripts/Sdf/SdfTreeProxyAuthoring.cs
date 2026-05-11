using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class SdfTreeProxyAuthoring : MonoBehaviour
{
    [Serializable]
    private struct ProxyDefinition
    {
        public string name;
        public SdfPhase1Driver.ShapeMode shapeMode;
        public PrimitiveType primitiveType;
        public Vector3 localCenter;
        public Vector3 localEulerAngles;
        public Vector3 localScale;
    }

    [Header("Source")]
    [SerializeField] private Transform targetRoot;
    [SerializeField] private SdfSharedVolumeProxy sharedVolumeProxy;
    [SerializeField] private Material sdfMaterial;

    [Header("Generation")]
    [SerializeField] private bool generateOnEnable = true;
    [SerializeField] private bool regenerateOnValidate = false;
    [SerializeField] private bool includeGeneratedDriversInSharedVolume = true;
    [SerializeField] private string proxyRootName = "GeneratedSdfTreeProxies";
    [SerializeField] private ProxyDefinition[] proxyDefinitions = Array.Empty<ProxyDefinition>();

    private readonly List<SdfPhase1Driver> generatedDrivers = new List<SdfPhase1Driver>();

    private void OnEnable()
    {
        if (generateOnEnable)
        {
            GenerateOrUpdateProxies();
        }
    }

    private void OnValidate()
    {
        if (regenerateOnValidate && isActiveAndEnabled)
        {
            GenerateOrUpdateProxies();
        }
    }

    [ContextMenu("Generate Or Update Proxies")]
    public void GenerateOrUpdateProxies()
    {
        if (targetRoot == null)
        {
            return;
        }

        ProxyDefinition[] definitions = proxyDefinitions != null && proxyDefinitions.Length > 0
            ? proxyDefinitions
            : GetDefaultCommonTreeDefinitions();
        if (definitions.Length <= 0)
        {
            return;
        }

        Transform proxyRoot = GetOrCreateProxyRoot();
        generatedDrivers.Clear();
        Material material = ResolveSdfMaterial();
        for (int i = 0; i < definitions.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(definitions[i].name))
            {
                continue;
            }

            GameObject proxyObject = GetOrCreateProxyObject(proxyRoot, definitions[i]);
            ConfigureProxyObject(proxyObject, definitions[i], material);
        }

        if (includeGeneratedDriversInSharedVolume)
        {
            SyncSharedVolumeDrivers();
        }
    }

    private Transform GetOrCreateProxyRoot()
    {
        Transform existing = transform.Find(proxyRootName);
        if (existing != null)
        {
            return existing;
        }

        GameObject rootObject = new GameObject(proxyRootName);
        rootObject.transform.SetParent(transform, false);
        return rootObject.transform;
    }

    private GameObject GetOrCreateProxyObject(Transform proxyRoot, ProxyDefinition definition)
    {
        Transform existing = proxyRoot.Find(definition.name);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject proxyObject = GameObject.CreatePrimitive(definition.primitiveType);
        proxyObject.name = definition.name;
        proxyObject.transform.SetParent(proxyRoot, false);
        Collider generatedCollider = proxyObject.GetComponent<Collider>();
        if (generatedCollider != null)
        {
            DestroyImmediateSafe(generatedCollider);
        }

        return proxyObject;
    }

    private void ConfigureProxyObject(GameObject proxyObject, ProxyDefinition definition, Material material)
    {
        proxyObject.layer = gameObject.layer;
        Transform proxyTransform = proxyObject.transform;
        proxyTransform.position = targetRoot.TransformPoint(definition.localCenter);
        proxyTransform.rotation = targetRoot.rotation * Quaternion.Euler(definition.localEulerAngles);
        proxyTransform.localScale = ScaleByTarget(definition.localScale);

        MeshRenderer meshRenderer = proxyObject.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sharedMaterial = material;
            meshRenderer.enabled = true;
            meshRenderer.forceRenderingOff = true;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        SdfPhase1Driver driver = proxyObject.GetComponent<SdfPhase1Driver>();
        if (driver == null)
        {
            driver = proxyObject.AddComponent<SdfPhase1Driver>();
        }

        driver.ConfigureShapeSettings(
            definition.shapeMode,
            Vector3.zero,
            0.5f,
            Vector3.one * 0.5f,
            Vector3.up,
            0.0f);
        driver.SetVolumeContributionMode(SdfPhase1Driver.VolumeContributionMode.SurfaceOnly);
        generatedDrivers.Add(driver);
    }

    private Vector3 ScaleByTarget(Vector3 localScale)
    {
        Vector3 sourceScale = targetRoot.lossyScale;
        return new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(localScale.x * sourceScale.x)),
            Mathf.Max(0.01f, Mathf.Abs(localScale.y * sourceScale.y)),
            Mathf.Max(0.01f, Mathf.Abs(localScale.z * sourceScale.z)));
    }

    private Material ResolveSdfMaterial()
    {
        if (sdfMaterial != null)
        {
            return sdfMaterial;
        }

        if (sharedVolumeProxy != null && sharedVolumeProxy.VolumeDriver != null)
        {
            Material material = sharedVolumeProxy.VolumeDriver.GetSharedMaterial();
            if (material != null)
            {
                return material;
            }
        }

        SdfPhase1Driver[] drivers = SdfSceneDriverUtility.FindSurfaceDrivers();
        for (int i = 0; i < drivers.Length; i++)
        {
            Material material = drivers[i] != null ? drivers[i].GetSharedMaterial() : null;
            if (material != null)
            {
                return material;
            }
        }

        Shader shader = Shader.Find("Custom/Sdf/Phase1Raymarch");
        return shader != null ? new Material(shader) { name = "GeneratedSdfTreeProxyMaterial" } : null;
    }

    private void SyncSharedVolumeDrivers()
    {
        if (sharedVolumeProxy == null || generatedDrivers.Count <= 0)
        {
            return;
        }

        SdfPhase1Driver[] surfaceDrivers = SdfSceneDriverUtility.FindSurfaceDrivers(sharedVolumeProxy.VolumeDriver);
        sharedVolumeProxy.SetSurfaceDrivers(surfaceDrivers);
    }

    private static ProxyDefinition[] GetDefaultCommonTreeDefinitions()
    {
        return new[]
        {
            new ProxyDefinition
            {
                name = "SDF_Trunk",
                shapeMode = SdfPhase1Driver.ShapeMode.ClippedBox,
                primitiveType = PrimitiveType.Cube,
                localCenter = new Vector3(0.0f, 1.15f, 0.0f),
                localEulerAngles = Vector3.zero,
                localScale = new Vector3(0.38f, 2.35f, 0.38f)
            },
            new ProxyDefinition
            {
                name = "SDF_Canopy_Main",
                shapeMode = SdfPhase1Driver.ShapeMode.Sphere,
                primitiveType = PrimitiveType.Sphere,
                localCenter = new Vector3(0.0f, 3.05f, 0.0f),
                localEulerAngles = Vector3.zero,
                localScale = new Vector3(2.25f, 1.45f, 2.0f)
            },
            new ProxyDefinition
            {
                name = "SDF_Canopy_Upper",
                shapeMode = SdfPhase1Driver.ShapeMode.Sphere,
                primitiveType = PrimitiveType.Sphere,
                localCenter = new Vector3(0.1f, 3.85f, -0.15f),
                localEulerAngles = Vector3.zero,
                localScale = new Vector3(1.75f, 1.15f, 1.6f)
            },
            new ProxyDefinition
            {
                name = "SDF_Canopy_Left",
                shapeMode = SdfPhase1Driver.ShapeMode.Sphere,
                primitiveType = PrimitiveType.Sphere,
                localCenter = new Vector3(-0.85f, 3.25f, 0.25f),
                localEulerAngles = Vector3.zero,
                localScale = new Vector3(1.25f, 0.9f, 1.2f)
            },
            new ProxyDefinition
            {
                name = "SDF_Canopy_Right",
                shapeMode = SdfPhase1Driver.ShapeMode.Sphere,
                primitiveType = PrimitiveType.Sphere,
                localCenter = new Vector3(0.85f, 3.15f, -0.2f),
                localEulerAngles = Vector3.zero,
                localScale = new Vector3(1.25f, 0.9f, 1.15f)
            }
        };
    }

    private static void DestroyImmediateSafe(UnityEngine.Object target)
    {
        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(target);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
