using UnityEngine;

[DisallowMultipleComponent]
public class OrbitFocusAnchor : MonoBehaviour
{
    [Header("Initialization")]
    [SerializeField] private Transform initialFocusSource;
    [SerializeField] private Vector3 sourceOffset = Vector3.zero;
    [SerializeField] private bool initializeOnEnable = true;

    private void OnEnable()
    {
        if (initializeOnEnable)
        {
            SyncToInitialSource();
        }
    }

    [ContextMenu("Sync To Initial Source")]
    public void SyncToInitialSource()
    {
        if (initialFocusSource == null)
        {
            return;
        }

        if (TryGetWorldBounds(initialFocusSource, out Bounds bounds))
        {
            transform.position = bounds.center + sourceOffset;
            return;
        }

        transform.position = initialFocusSource.position + sourceOffset;
    }

    public void SetWorldPosition(Vector3 worldPosition)
    {
        transform.position = worldPosition;
    }

    public bool SetFromTransforms(params Transform[] sources)
    {
        if (sources == null || sources.Length == 0)
        {
            return false;
        }

        bool hasBounds = false;
        Bounds combinedBounds = default;

        Vector3 positionSum = Vector3.zero;
        int positionCount = 0;

        for (int i = 0; i < sources.Length; i++)
        {
            Transform source = sources[i];
            if (source == null)
            {
                continue;
            }

            if (TryGetWorldBounds(source, out Bounds bounds))
            {
                if (!hasBounds)
                {
                    combinedBounds = bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(bounds);
                }
            }

            positionSum += source.position;
            positionCount++;
        }

        if (hasBounds)
        {
            transform.position = combinedBounds.center;
            return true;
        }

        if (positionCount > 0)
        {
            transform.position = positionSum / positionCount;
            return true;
        }

        return false;
    }

    private bool TryGetWorldBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length <= 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return true;
    }
}
