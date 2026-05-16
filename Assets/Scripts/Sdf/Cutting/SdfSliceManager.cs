using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SliceInputHandler))]
public class SdfSliceManager : MonoBehaviour
{
    [Header("Layer")]
    [SerializeField] private LayerMask sliceableLayer = ~0;

    [Header("Ray Sweep")]
    [SerializeField] [Min(1.0f)] private float raycastStepSize = 15.0f;
    [SerializeField] [Min(1)] private int maxHitsPerRay = 32;
    [SerializeField] [Min(0.5f)] private float raycastDistance = 1000.0f;

    [Header("Separation")]
    [SerializeField] [Min(0.0f)] private float separationDistance = 0.2f;
    [SerializeField] [Min(0.01f)] private float separationDuration = 0.15f;

    [Header("Orbit Focus")]
    [SerializeField] private OrbitFocusAnchor orbitFocusAnchor = null;
    [SerializeField] private bool updateOrbitFocusDuringSeparation = true;

    [Header("Validation")]
    [SerializeField] private SdfCutDustFieldController cutDustFieldController = null;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private Color debugGizmoColor = Color.cyan;

    private SliceInputHandler inputHandler;
    private RaycastHit[] raycastBuffer;
    private Vector3 debugStartPoint;
    private Vector3 debugEndPoint;
    private bool hasDebugLine;

    private void Awake()
    {
        inputHandler = GetComponent<SliceInputHandler>();
        raycastBuffer = new RaycastHit[maxHitsPerRay];
        RegisterEvents();
    }

    private void OnDestroy()
    {
        UnregisterEvents();
    }

    private void RegisterEvents()
    {
        if (inputHandler != null)
        {
            inputHandler.OnSliceEnd += HandleSliceEnd;
        }
    }

    private void UnregisterEvents()
    {
        if (inputHandler != null)
        {
            inputHandler.OnSliceEnd -= HandleSliceEnd;
        }
    }

    private void HandleSliceEnd(Vector2 startScreen, Vector2 endScreen)
    {
        if (Camera.main == null)
        {
            return;
        }

        if (showDebugGizmos)
        {
            Ray startRay = Camera.main.ScreenPointToRay(startScreen);
            Ray endRay = Camera.main.ScreenPointToRay(endScreen);
            debugStartPoint = startRay.GetPoint(2.0f);
            debugEndPoint = endRay.GetPoint(2.0f);
            hasDebugLine = true;
        }

        if (!CalculateSlicePlane(startScreen, endScreen, out Plane slicePlane))
        {
            return;
        }

        HashSet<SdfSliceablePiece> targets = ScanForTargets(startScreen, endScreen);
        foreach (SdfSliceablePiece target in targets)
        {
            if (target == null)
            {
                continue;
            }

            if (target.TrySplit(slicePlane, out SdfSliceablePiece positivePiece, out SdfSliceablePiece negativePiece))
            {
                if (cutDustFieldController != null)
                {
                    cutDustFieldController.EmitForCut(
                        slicePlane,
                        positivePiece != null ? positivePiece.transform : null,
                        negativePiece != null ? negativePiece.transform : null);
                }

                UpdateOrbitFocus(positivePiece != null ? positivePiece.transform : null,
                    negativePiece != null ? negativePiece.transform : null);

                StartCoroutine(AnimateSeparatedPair(
                    positivePiece != null ? positivePiece.transform : null,
                    negativePiece != null ? negativePiece.transform : null,
                    slicePlane.normal));
            }
        }
    }

    private bool CalculateSlicePlane(Vector2 startScreen, Vector2 endScreen, out Plane plane)
    {
        plane = new Plane();

        Ray startRay = Camera.main.ScreenPointToRay(startScreen);
        Ray endRay = Camera.main.ScreenPointToRay(endScreen);

        Vector3 startDirection = startRay.direction;
        Vector3 endDirection = endRay.direction;
        Vector3 planeNormal = Vector3.Cross(startDirection, endDirection).normalized;

        if (planeNormal.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        plane = new Plane(planeNormal, Camera.main.transform.position);
        return true;
    }

    private HashSet<SdfSliceablePiece> ScanForTargets(Vector2 startScreen, Vector2 endScreen)
    {
        HashSet<SdfSliceablePiece> targets = new HashSet<SdfSliceablePiece>();

        float distance = Vector2.Distance(startScreen, endScreen);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / raycastStepSize));

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector2 currentPos = Vector2.Lerp(startScreen, endScreen, t);
            Ray ray = Camera.main.ScreenPointToRay(currentPos);

            int hitCount = Physics.RaycastNonAlloc(ray, raycastBuffer, raycastDistance, sliceableLayer);
            for (int j = 0; j < hitCount; j++)
            {
                Collider hitCollider = raycastBuffer[j].collider;
                if (hitCollider == null)
                {
                    continue;
                }

                SdfSliceablePiece piece = hitCollider.GetComponentInParent<SdfSliceablePiece>();
                if (piece != null && piece.CanSplit)
                {
                    targets.Add(piece);
                }
            }
        }

        return targets;
    }

    private IEnumerator AnimateSeparatedPair(Transform positive, Transform negative, Vector3 worldNormal)
    {
        if (positive == null || negative == null)
        {
            yield break;
        }

        float elapsed = 0.0f;
        Vector3 startPosA = positive.position;
        Vector3 startPosB = negative.position;
        Vector3 targetPosA = startPosA + worldNormal * separationDistance;
        Vector3 targetPosB = startPosB - worldNormal * separationDistance;

        while (elapsed < separationDuration)
        {
            if (positive == null || negative == null)
            {
                yield break;
            }

            float t = elapsed / separationDuration;
            float eased = Mathf.Sin(t * Mathf.PI * 0.5f);

            positive.position = Vector3.Lerp(startPosA, targetPosA, eased);
            negative.position = Vector3.Lerp(startPosB, targetPosB, eased);

            if (updateOrbitFocusDuringSeparation)
            {
                UpdateOrbitFocus(positive, negative);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (positive != null)
        {
            positive.position = targetPosA;
        }

        if (negative != null)
        {
            negative.position = targetPosB;
        }

        UpdateOrbitFocus(positive, negative);
    }

    private void UpdateOrbitFocus(Transform positive, Transform negative)
    {
        if (orbitFocusAnchor == null)
        {
            return;
        }

        orbitFocusAnchor.SetFromTransforms(positive, negative);
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !hasDebugLine)
        {
            return;
        }

        Gizmos.color = debugGizmoColor;
        Gizmos.DrawLine(debugStartPoint, debugEndPoint);
        Gizmos.DrawWireSphere(debugStartPoint, 0.05f);
        Gizmos.DrawWireSphere(debugEndPoint, 0.05f);
    }
}
