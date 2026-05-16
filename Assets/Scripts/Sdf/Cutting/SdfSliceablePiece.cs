using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SdfRaymarchDriver))]
[RequireComponent(typeof(SdfCutPlaneBufferController))]
[RequireComponent(typeof(BoxCollider))]
public class SdfSliceablePiece : MonoBehaviour
{
    [Header("Split Settings")]
    [SerializeField] [Min(0)] private int remainingSplits = 1;

    private SdfCutPlaneBufferController cutPlaneBufferController;
    private BoxCollider boxCollider;

    public bool CanSplit => remainingSplits > 0;

    private void Awake()
    {
        CacheComponents();
    }

    private void OnValidate()
    {
        CacheComponents();
        SyncColliderToMeshBounds();
    }

    public bool TrySplit(Plane worldPlane, out SdfSliceablePiece positivePiece, out SdfSliceablePiece negativePiece)
    {
        CacheComponents();
        positivePiece = null;
        negativePiece = null;

        if (!CanSplit || cutPlaneBufferController == null)
        {
            return false;
        }

        CutPlaneData[] basePlanes = cutPlaneBufferController.GetUploadedPlanesCopy();
        positivePiece = CreateChildPiece(basePlanes, worldPlane, true, "Positive");
        negativePiece = CreateChildPiece(basePlanes, worldPlane, false, "Negative");

        Destroy(gameObject);
        return positivePiece != null && negativePiece != null;
    }

    private SdfSliceablePiece CreateChildPiece(CutPlaneData[] basePlanes, Plane worldPlane, bool keepPositiveSide, string suffix)
    {
        GameObject clone = Instantiate(gameObject, transform.position, transform.rotation, transform.parent);
        clone.name = $"{gameObject.name}_{suffix}";

        SdfSliceablePiece piece = clone.GetComponent<SdfSliceablePiece>();
        if (piece == null)
        {
            Destroy(clone);
            return null;
        }

        piece.CacheComponents();
        piece.remainingSplits = Mathf.Max(remainingSplits - 1, 0);

        CutPlaneData[] childPlanes = new CutPlaneData[basePlanes.Length + 1];
        Array.Copy(basePlanes, childPlanes, basePlanes.Length);
        childPlanes[basePlanes.Length] = CutPlaneData.FromWorldPlane(piece.transform, worldPlane, keepPositiveSide);

        piece.cutPlaneBufferController.SetRuntimePlanes(childPlanes);
        piece.SyncColliderToMeshBounds();
        return piece;
    }

    private void CacheComponents()
    {
        if (cutPlaneBufferController == null)
        {
            cutPlaneBufferController = GetComponent<SdfCutPlaneBufferController>();
        }

        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider>();
        }
    }

    private void SyncColliderToMeshBounds()
    {
        if (boxCollider == null)
        {
            return;
        }

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return;
        }

        Bounds bounds = meshFilter.sharedMesh.bounds;
        boxCollider.center = bounds.center;
        boxCollider.size = bounds.size;
    }
}
