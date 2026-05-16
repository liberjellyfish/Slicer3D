using UnityEngine;

[DisallowMultipleComponent]
public class OrbitValidationCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target = null;
    [SerializeField] private Vector3 targetOffset = Vector3.zero;
    [SerializeField] private bool autoInitializeFromCurrentTransform = true;

    [Header("Orbit")]
    [SerializeField] [Min(0.1f)] private float radius = 6.0f;
    [SerializeField] private float yaw = 0.0f;
    [SerializeField] [Range(-89.0f, 89.0f)] private float pitch = 20.0f;
    [SerializeField] [Min(1.0f)] private float horizontalSpeed = 120.0f;
    [SerializeField] [Min(1.0f)] private float verticalSpeed = 120.0f;

    [Header("Zoom")]
    [SerializeField] [Min(0.01f)] private float zoomSpeed = 4.0f;
    [SerializeField] [Min(0.1f)] private float minRadius = 1.5f;
    [SerializeField] [Min(0.1f)] private float maxRadius = 12.0f;

    [Header("Limits")]
    [SerializeField] [Range(-89.0f, 89.0f)] private float minPitch = -70.0f;
    [SerializeField] [Range(-89.0f, 89.0f)] private float maxPitch = 80.0f;

    [Header("Input")]
    [SerializeField] private bool allowKeyboardControl = true;
    [SerializeField] private bool allowScrollZoom = true;
    [SerializeField] private KeyCode rotateLeftKey = KeyCode.A;
    [SerializeField] private KeyCode rotateRightKey = KeyCode.D;
    [SerializeField] private KeyCode rotateUpKey = KeyCode.W;
    [SerializeField] private KeyCode rotateDownKey = KeyCode.S;
    [SerializeField] private KeyCode resetKey = KeyCode.R;

    private float initialRadius;
    private float initialYaw;
    private float initialPitch;
    private bool hasInitialState;
    private Vector3 lastValidFocusPoint;
    private bool hasLastValidFocusPoint;

    private void OnEnable()
    {
        ClampSettings();

        if (autoInitializeFromCurrentTransform)
        {
            SyncOrbitFromTransform();
        }

        CacheInitialState();
        ApplyOrbit();
    }

    private void OnValidate()
    {
        ClampSettings();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        HandleInput();
        ApplyOrbit();
    }

    private void HandleInput()
    {
        if (allowKeyboardControl)
        {
            float horizontalInput = 0.0f;
            if (Input.GetKey(rotateLeftKey))
            {
                horizontalInput -= 1.0f;
            }

            if (Input.GetKey(rotateRightKey))
            {
                horizontalInput += 1.0f;
            }

            float verticalInput = 0.0f;
            if (Input.GetKey(rotateUpKey))
            {
                verticalInput += 1.0f;
            }

            if (Input.GetKey(rotateDownKey))
            {
                verticalInput -= 1.0f;
            }

            yaw += horizontalInput * horizontalSpeed * Time.deltaTime;
            pitch += verticalInput * verticalSpeed * Time.deltaTime;
        }

        if (allowScrollZoom)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (!Mathf.Approximately(scroll, 0.0f))
            {
                radius -= scroll * zoomSpeed;
            }
        }

        if (Input.GetKeyDown(resetKey))
        {
            ResetOrbit();
        }

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        radius = Mathf.Clamp(radius, minRadius, maxRadius);
    }

    private void ApplyOrbit()
    {
        Vector3 focusPoint = GetFocusPoint();
        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0.0f);
        Vector3 orbitOffset = orbitRotation * new Vector3(0.0f, 0.0f, -radius);

        transform.position = focusPoint + orbitOffset;
        transform.rotation = Quaternion.LookRotation(focusPoint - transform.position, Vector3.up);
    }

    private void SyncOrbitFromTransform()
    {
        Vector3 focusPoint = GetFocusPoint();
        Vector3 toCamera = transform.position - focusPoint;
        float distance = toCamera.magnitude;
        if (distance < 1e-4f)
        {
            return;
        }

        radius = distance;
        Vector3 direction = toCamera / distance;
        pitch = Mathf.Asin(direction.y) * Mathf.Rad2Deg;
        yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
    }

    private void CacheInitialState()
    {
        initialRadius = radius;
        initialYaw = yaw;
        initialPitch = pitch;
        hasInitialState = true;
    }

    private void ResetOrbit()
    {
        if (!hasInitialState)
        {
            return;
        }

        radius = initialRadius;
        yaw = initialYaw;
        pitch = initialPitch;
    }

    private Vector3 GetFocusPoint()
    {
        if (target != null)
        {
            lastValidFocusPoint = target.position + targetOffset;
            hasLastValidFocusPoint = true;
            return lastValidFocusPoint;
        }

        if (hasLastValidFocusPoint)
        {
            return lastValidFocusPoint;
        }

        return targetOffset;
    }

    private void ClampSettings()
    {
        minRadius = Mathf.Max(0.1f, minRadius);
        maxRadius = Mathf.Max(minRadius, maxRadius);
        radius = Mathf.Clamp(radius, minRadius, maxRadius);

        if (maxPitch < minPitch)
        {
            maxPitch = minPitch;
        }

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }
}
