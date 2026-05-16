using UnityEngine;

[DisallowMultipleComponent]
public class SdfCutDustFieldController : MonoBehaviour
{
    [Header("Activation")]
    [SerializeField] private bool enableDustField = true;
    [SerializeField] private bool keepOnlyLatestField = true;

    [Header("Field Shape")]
    [SerializeField] [Min(0.05f)] private float fieldSizeMultiplier = 1.1f;
    [SerializeField] [Min(0.01f)] private float fieldThickness = 0.18f;
    [SerializeField] [Min(0.0f)] private float fieldPadding = 0.05f;

    [Header("Particles")]
    [SerializeField] [Min(1)] private int burstCount = 96;
    [SerializeField] [Min(0.1f)] private float particleLifetime = 5.0f;
    [SerializeField] [Min(0.0f)] private float particleStartSpeed = 0.04f;
    [SerializeField] [Min(0.001f)] private float particleStartSize = 0.03f;
    [SerializeField] private Color particleColor = new Color(1.0f, 0.97f, 0.9f, 0.12f);
    [SerializeField] [Min(0.0f)] private float noiseStrength = 0.2f;

    [Header("Hierarchy")]
    [SerializeField] private Transform dustRoot;

    private Material dustMaterial;
    private ParticleSystem latestParticleSystem;

    public Transform DustRoot => dustRoot;

    public void EmitForCut(Plane slicePlane, Transform positivePiece, Transform negativePiece)
    {
        if (!enableDustField)
        {
            return;
        }

        if (!TryBuildBounds(positivePiece, negativePiece, out Bounds combinedBounds))
        {
            return;
        }

        EnsureDustRoot();
        if (keepOnlyLatestField && latestParticleSystem != null)
        {
            if (Application.isPlaying)
            {
                Destroy(latestParticleSystem.gameObject);
            }
            else
            {
                DestroyImmediate(latestParticleSystem.gameObject);
            }

            latestParticleSystem = null;
        }

        GameObject fieldObject = new GameObject("SdfCutDustField");
        fieldObject.transform.SetParent(dustRoot, false);
        fieldObject.transform.position = combinedBounds.center;
        fieldObject.transform.rotation = Quaternion.LookRotation(slicePlane.normal, Vector3.up);

        ParticleSystem particleSystem = fieldObject.AddComponent<ParticleSystem>();
        ParticleSystemRenderer particleRenderer = fieldObject.GetComponent<ParticleSystemRenderer>();
        ConfigureParticleSystem(particleSystem, particleRenderer, combinedBounds);

        particleSystem.Play(true);
        latestParticleSystem = particleSystem;
    }

    private void EnsureDustRoot()
    {
        if (dustRoot != null)
        {
            return;
        }

        GameObject rootObject = new GameObject("SdfDustFields");
        rootObject.transform.SetParent(transform, false);
        dustRoot = rootObject.transform;
    }

    private bool TryBuildBounds(Transform positivePiece, Transform negativePiece, out Bounds combinedBounds)
    {
        combinedBounds = default;
        bool hasBounds = false;

        if (TryGetBounds(positivePiece, out Bounds positiveBounds))
        {
            combinedBounds = positiveBounds;
            hasBounds = true;
        }

        if (TryGetBounds(negativePiece, out Bounds negativeBounds))
        {
            if (!hasBounds)
            {
                combinedBounds = negativeBounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(negativeBounds);
            }
        }

        return hasBounds;
    }

    private bool TryGetBounds(Transform target, out Bounds bounds)
    {
        bounds = default;
        if (target == null)
        {
            return false;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length <= 0)
        {
            return false;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return true;
    }

    private void ConfigureParticleSystem(ParticleSystem particleSystem, ParticleSystemRenderer particleRenderer, Bounds combinedBounds)
    {
        float majorSize = Mathf.Max(combinedBounds.size.x, Mathf.Max(combinedBounds.size.y, combinedBounds.size.z));
        float fieldSize = majorSize * fieldSizeMultiplier + fieldPadding;

        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = particleLifetime;
        main.startSpeed = particleStartSpeed;
        main.startSize = particleStartSize;
        main.startColor = particleColor;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = burstCount;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = 0.0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0.0f, (short)burstCount) });

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(fieldSize, fieldSize, fieldThickness);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient alphaGradient = new Gradient();
        alphaGradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0.0f),
                new GradientColorKey(Color.white, 1.0f)
            },
            new[]
            {
                new GradientAlphaKey(0.0f, 0.0f),
                new GradientAlphaKey(particleColor.a, 0.18f),
                new GradientAlphaKey(particleColor.a * 0.8f, 0.72f),
                new GradientAlphaKey(0.0f, 1.0f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(alphaGradient);

        ParticleSystem.NoiseModule noise = particleSystem.noise;
        noise.enabled = noiseStrength > 0.0f;
        noise.strength = noiseStrength;
        noise.frequency = 0.3f;
        noise.scrollSpeed = 0.15f;
        noise.damping = true;

        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.sharedMaterial = GetOrCreateDustMaterial();
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
    }

    private Material GetOrCreateDustMaterial()
    {
        if (dustMaterial != null)
        {
            return dustMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        dustMaterial = new Material(shader)
        {
            name = "SdfCutDustFieldMaterial"
        };
        dustMaterial.color = Color.white;
        return dustMaterial;
    }
}
