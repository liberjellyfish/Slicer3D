using UnityEngine;
using System.Collections;

[RequireComponent(typeof(LineRenderer))]
public class SlicerGameManager : MonoBehaviour
{
    [Header("Settings")]
    public LayerMask sliceableLayer;
    [Tooltip("分离距离 (米)")]
    public float separationDistance = 0.2f;
    [Tooltip("分离过程持续时间 (秒)")]
    public float separationDuration = 0.1f;

    private Vector3 startPoint;
    private Vector3 endPoint;
    private bool isDragging = false;
    private LineRenderer lineRenderer;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 0;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
        if (lineRenderer.material == null)
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            startPoint = Input.mousePosition;
            isDragging = true;
            lineRenderer.positionCount = 2;
        }

        if (isDragging)
        {
            endPoint = Input.mousePosition;
            Ray startRay = Camera.main.ScreenPointToRay(startPoint);
            Ray endRay = Camera.main.ScreenPointToRay(endPoint);
            lineRenderer.SetPosition(0, startRay.GetPoint(5f));
            lineRenderer.SetPosition(1, endRay.GetPoint(5f));
        }

        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            isDragging = false;
            lineRenderer.positionCount = 0;
            if (Vector3.Distance(startPoint, endPoint) > 10f) PerformSlice();
        }
    }

    void PerformSlice()
    {
        Ray startRay = Camera.main.ScreenPointToRay(startPoint);
        Ray endRay = Camera.main.ScreenPointToRay(endPoint);

        Vector3 vecStart = startRay.direction;
        Vector3 vecEnd = endRay.direction;
        Vector3 planeNormal = Vector3.Cross(vecStart, vecEnd).normalized;
        if (planeNormal.sqrMagnitude < 0.0001f) return;

        Plane slicePlane = new Plane(planeNormal, Camera.main.transform.position);

        // 使用 FindObjectsByType (Unity 6)
        MeshFilter[] targets = FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);

        foreach (var mf in targets)
        {
            if (mf == null) continue;
            if (((1 << mf.gameObject.layer) & sliceableLayer) != 0)
            {
                Bounds bounds = mf.GetComponent<Renderer>().bounds;
                bounds.Expand(0.05f); // 稍微宽容一点的包围盒检测
                if (IsBoundsIntersected(bounds, slicePlane))
                {
                    SliceObject(mf.gameObject, slicePlane);
                }
            }
        }
    }

    bool IsBoundsIntersected(Bounds bounds, Plane plane)
    {
        Vector3 c = bounds.center;
        Vector3 e = bounds.extents;
        bool s = plane.GetSide(c + new Vector3(e.x, e.y, e.z));
        // 只要有一个角点在不同侧，即为相交
        if (plane.GetSide(c + new Vector3(e.x, e.y, -e.z)) != s) return true;
        if (plane.GetSide(c + new Vector3(e.x, -e.y, e.z)) != s) return true;
        if (plane.GetSide(c + new Vector3(e.x, -e.y, -e.z)) != s) return true;
        if (plane.GetSide(c + new Vector3(-e.x, e.y, e.z)) != s) return true;
        if (plane.GetSide(c + new Vector3(-e.x, e.y, -e.z)) != s) return true;
        if (plane.GetSide(c + new Vector3(-e.x, -e.y, e.z)) != s) return true;
        if (plane.GetSide(c + new Vector3(-e.x, -e.y, -e.z)) != s) return true;
        return false;
    }

    void SliceObject(GameObject target, Plane planeWorld)
    {
        Mesh originalMesh = target.GetComponent<MeshFilter>().mesh;

        // 坐标转换
        Vector3 pointOnPlane = planeWorld.normal * -planeWorld.distance;
        Vector3 localPoint = target.transform.InverseTransformPoint(pointOnPlane);
        Vector3 localNormal = target.transform.InverseTransformDirection(planeWorld.normal).normalized;
        Plane localPlane = new Plane(localNormal, localPoint);

        MeshSlicer.SlicedHull result;
        try
        {
            result = MeshSlicer.SliceMesh(originalMesh, localPlane);
        }
        catch
        {
            return; // 切割失败放弃
        }

        if (result.PositiveMesh.vertexCount == 0 || result.NegativeMesh.vertexCount == 0) return;

        GameObject hullPos = CreateHull(target, result.PositiveMesh, "Pos");
        GameObject hullNeg = CreateHull(target, result.NegativeMesh, "Neg");

        StartCoroutine(AnimateSeparation(hullPos.transform, hullNeg.transform, planeWorld.normal));

        Destroy(target);
    }

    GameObject CreateHull(GameObject original, Mesh newMesh, string suffix)
    {
        GameObject hull = new GameObject(original.name + "_" + suffix);
        hull.transform.position = original.transform.position;
        hull.transform.rotation = original.transform.rotation;
        hull.transform.localScale = original.transform.localScale;
        hull.layer = original.layer;

        MeshFilter mf = hull.AddComponent<MeshFilter>();
        mf.mesh = newMesh;

        MeshRenderer mr = hull.AddComponent<MeshRenderer>();
        mr.materials = original.GetComponent<MeshRenderer>().materials;

        MeshCollider mc = hull.AddComponent<MeshCollider>();
        mc.sharedMesh = newMesh;
        mc.convex = true;

        Rigidbody rb = hull.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        return hull;
    }

    IEnumerator AnimateSeparation(Transform t1, Transform t2, Vector3 normal)
    {
        float elapsed = 0f;
        Vector3 startPos1 = t1.position;
        Vector3 startPos2 = t2.position;

        Vector3 targetPos1 = startPos1 + normal * separationDistance;
        Vector3 targetPos2 = startPos2 - normal * separationDistance;

        while (elapsed < separationDuration)
        {
            if (t1 == null || t2 == null) yield break;

            float t = elapsed / separationDuration;
            t = Mathf.Sin(t * Mathf.PI * 0.5f);

            t1.position = Vector3.Lerp(startPos1, targetPos1, t);
            t2.position = Vector3.Lerp(startPos2, targetPos2, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (t1 != null) t1.position = targetPos1;
        if (t2 != null) t2.position = targetPos2;
    }
}