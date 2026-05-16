using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Light))]
public class SdfLightingValidationRig : MonoBehaviour
{
    [Header("Activation")]
    [SerializeField] private bool animateInPlayMode = true;

    [Header("Sweep")]
    [SerializeField] private bool rotateYaw = true;
    [SerializeField] private float yawSpeed = 18.0f;
    [SerializeField] private bool oscillatePitch = true;
    [SerializeField] private float pitchAmplitude = 18.0f;
    [SerializeField] [Min(0.01f)] private float pitchFrequency = 0.12f;

    [Header("Intensity Pulse")]
    [SerializeField] private bool pulseIntensity = false;
    [SerializeField] [Min(0.0f)] private float minIntensity = 0.9f;
    [SerializeField] [Min(0.0f)] private float maxIntensity = 1.8f;
    [SerializeField] [Min(0.01f)] private float pulseFrequency = 0.2f;

    private Light cachedLight;
    private Vector3 initialEulerAngles;
    private float initialIntensity;
    private bool hasInitialState;

    public bool AnimateInPlayMode
    {
        get => animateInPlayMode;
        set => animateInPlayMode = value;
    }

    public bool RotateYaw
    {
        get => rotateYaw;
        set => rotateYaw = value;
    }

    public bool OscillatePitch
    {
        get => oscillatePitch;
        set => oscillatePitch = value;
    }

    public bool PulseIntensity
    {
        get => pulseIntensity;
        set => pulseIntensity = value;
    }

    private void OnEnable()
    {
        CacheLight();
        CaptureInitialState();
    }

    private void OnValidate()
    {
        CacheLight();
        maxIntensity = Mathf.Max(minIntensity, maxIntensity);
    }

    private void Update()
    {
        if (!Application.isPlaying || !animateInPlayMode)
        {
            return;
        }

        if (!hasInitialState)
        {
            CaptureInitialState();
        }

        ApplyAnimation(Time.time);
    }

    [ContextMenu("Capture Initial State")]
    public void CaptureInitialState()
    {
        CacheLight();
        initialEulerAngles = transform.eulerAngles;
        initialIntensity = cachedLight != null ? cachedLight.intensity : 1.0f;
        hasInitialState = true;
    }

    [ContextMenu("Restore Initial State")]
    public void RestoreInitialState()
    {
        if (!hasInitialState)
        {
            return;
        }

        transform.rotation = Quaternion.Euler(initialEulerAngles);
        if (cachedLight != null)
        {
            cachedLight.intensity = initialIntensity;
        }
    }

    public void ConfigureSweep(bool enableYawRotation, bool enablePitchOscillation, bool enableIntensityPulse)
    {
        rotateYaw = enableYawRotation;
        oscillatePitch = enablePitchOscillation;
        pulseIntensity = enableIntensityPulse;
    }

    private void CacheLight()
    {
        if (cachedLight == null)
        {
            cachedLight = GetComponent<Light>();
        }
    }

    private void ApplyAnimation(float time)
    {
        Vector3 eulerAngles = initialEulerAngles;
        if (rotateYaw)
        {
            eulerAngles.y = Mathf.Repeat(initialEulerAngles.y + time * yawSpeed, 360.0f);
        }

        if (oscillatePitch)
        {
            float pitchOffset = Mathf.Sin(time * pitchFrequency * Mathf.PI * 2.0f) * pitchAmplitude;
            eulerAngles.x = initialEulerAngles.x + pitchOffset;
        }

        transform.rotation = Quaternion.Euler(eulerAngles);

        if (cachedLight != null && pulseIntensity)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * pulseFrequency * Mathf.PI * 2.0f);
            cachedLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, pulse);
        }
    }
}
