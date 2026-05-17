using System;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SdfRaymarchDriver))]
public sealed class SdfObjectRuntime : MonoBehaviour
{
    [Header("Scene SDF")]
    [SerializeField] private bool participatesInSceneSdf = true;

    [Header("Runtime Identity")]
    [SerializeField, HideInInspector] private string objectId;
    [SerializeField, HideInInspector] private string lineageId;
    [SerializeField, HideInInspector] private string parentObjectId;

    [Header("Runtime Stats")]
    [SerializeField] private int dataVersion = 1;
    [SerializeField] private int transformVersion = 1;
    [SerializeField] private int shapeVersion;
    [SerializeField] private int cutPlaneVersion;

    private SdfRaymarchDriver driver;
    private SdfCutPlaneBufferController cutPlaneBufferController;
    private int lastTransformHash = int.MinValue;
    private int lastShapeVersion = int.MinValue;
    private int lastCutPlaneVersion = int.MinValue;
    private bool lastParticipatesInSceneSdf;

    public SdfRaymarchDriver Driver
    {
        get
        {
            CacheComponents();
            return driver;
        }
    }

    public bool ParticipatesInSceneSdf => participatesInSceneSdf;
    public string ObjectId => objectId;
    public string LineageId => lineageId;
    public string ParentObjectId => parentObjectId;
    public int DataVersion => dataVersion;
    public int TransformVersion => transformVersion;
    public int ShapeVersion => shapeVersion;
    public int CutPlaneVersion => cutPlaneVersion;

    private void OnEnable()
    {
        CacheComponents();
        EnsureIdentity();
        lastParticipatesInSceneSdf = participatesInSceneSdf;
        RefreshVersions(true);
        SdfSceneRegistry.Register(this);
    }

    private void OnDisable()
    {
        SdfSceneRegistry.Unregister(this);
    }

    private void OnValidate()
    {
        CacheComponents();
        EnsureIdentity();
        RefreshParticipationState(false);
        if (RefreshVersions(true) && isActiveAndEnabled)
        {
            SdfSceneRegistry.MarkDirty(this);
        }
    }

    private void Update()
    {
        RefreshParticipationState(false);
        if (RefreshVersions(false))
        {
            SdfSceneRegistry.MarkDirty(this);
        }
    }

    public void SetParticipatesInSceneSdf(bool participates)
    {
        if (participatesInSceneSdf == participates)
        {
            return;
        }

        participatesInSceneSdf = participates;
        RefreshParticipationState(true);
    }

    public void InitializePieceLineage(string parentId, string lineageIdOverride = null)
    {
        parentObjectId = parentId ?? string.Empty;
        lineageId = string.IsNullOrEmpty(lineageIdOverride) ? CreateId() : lineageIdOverride;
        objectId = CreateId();
        IncrementDataVersion();
        SdfSceneRegistry.MarkDirty(this);
    }

    private void CacheComponents()
    {
        if (driver == null)
        {
            driver = GetComponent<SdfRaymarchDriver>();
        }

        if (cutPlaneBufferController == null)
        {
            cutPlaneBufferController = GetComponent<SdfCutPlaneBufferController>();
        }
    }

    private void EnsureIdentity()
    {
        if (string.IsNullOrEmpty(objectId))
        {
            objectId = CreateId();
        }

        if (string.IsNullOrEmpty(lineageId))
        {
            lineageId = objectId;
        }
    }

    private bool RefreshVersions(bool force)
    {
        CacheComponents();

        int currentTransformHash = CalculateTransformHash();
        int currentShapeVersion = driver != null ? driver.SceneDataVersion : 0;
        int currentCutPlaneVersion = cutPlaneBufferController != null ? cutPlaneBufferController.DataVersion : 0;
        bool transformChanged = force || currentTransformHash != lastTransformHash;
        bool shapeChanged = force || currentShapeVersion != lastShapeVersion;
        bool cutPlaneChanged = force || currentCutPlaneVersion != lastCutPlaneVersion;

        if (!transformChanged && !shapeChanged && !cutPlaneChanged)
        {
            return false;
        }

        if (transformChanged)
        {
            transformVersion = IncrementVersionValue(transformVersion);
            lastTransformHash = currentTransformHash;
        }

        if (shapeChanged)
        {
            shapeVersion = currentShapeVersion;
            lastShapeVersion = currentShapeVersion;
        }

        if (cutPlaneChanged)
        {
            cutPlaneVersion = currentCutPlaneVersion;
            lastCutPlaneVersion = currentCutPlaneVersion;
        }

        IncrementDataVersion();
        return true;
    }

    private bool RefreshParticipationState(bool force)
    {
        if (!force && lastParticipatesInSceneSdf == participatesInSceneSdf)
        {
            return false;
        }

        lastParticipatesInSceneSdf = participatesInSceneSdf;
        if (isActiveAndEnabled)
        {
            SdfSceneRegistry.MarkMembershipDirty(this);
        }

        return true;
    }

    private int CalculateTransformHash()
    {
        Matrix4x4 matrix = transform.localToWorldMatrix;
        unchecked
        {
            int hash = 17;
            for (int i = 0; i < 16; i++)
            {
                hash = hash * 31 + matrix[i].GetHashCode();
            }

            return hash;
        }
    }

    private void IncrementDataVersion()
    {
        dataVersion = IncrementVersionValue(dataVersion);
    }

    private static int IncrementVersionValue(int value)
    {
        unchecked
        {
            value++;
            return value > 0 ? value : 1;
        }
    }

    private static string CreateId()
    {
        return Guid.NewGuid().ToString("N");
    }
}
