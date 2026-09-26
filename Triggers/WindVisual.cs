using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Foundry.Triggers
{
    // Makes a WindTrigger3D's zone visible: drives a particle system so blown debris streams out of the zone's
    // base, across its whole cross-section, along its Direction at the wind's speed, and fades out at the far end.
    // It only sets what has to match the zone (placement, emitter size, speed, lifetime, rate, on/off with the wind) -
    // the look (material, colour, size, spin, flutter) is the particle system's own and is never overwritten, so
    // swap in a leaf sprite material etc. freely. Fitting runs on Awake; use "Fit To Zone" to preview in the editor.
    [RequireComponent(typeof(WindTrigger3D))]
    public class WindVisual : MonoBehaviour
    {
        [Tooltip("The particle system to drive. Use Create Particles to make one with a blown-debris starting look.")]
        [SerializeField] private ParticleSystem particles;

        [Tooltip("Particles emitted per second for each square metre of the zone's cross-section.")]
        [SerializeField, Min(0f)] private float density = 2f;

        [Tooltip("Particle speed as a fraction of the wind's Velocity. Lifetime is set so they still just reach the far end of the zone.")]
        [SerializeField, Range(0.05f, 2f)] private float speedScale = 1f;

        private const float EMITTER_THICKNESS = 0.1f;

        private WindTrigger3D _wind;
        private bool _emitting = true;

        private void Awake()
        {
            _wind = GetComponent<WindTrigger3D>();
            FitToZone();
        }

        private void Update()
        {
            if (particles == null)
                return;

            // Emission off rather than Stop(), so particles already in the air finish their flight
            bool shouldEmit = _wind.IsActive;
            if (shouldEmit == _emitting)
                return;

            _emitting = shouldEmit;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = shouldEmit;
        }

        [Button("Fit To Zone")]
        [ShowIf(nameof(particles))]
        public void FitToZone()
        {
            if (particles == null)
                return;

            WindTrigger3D wind = _wind != null ? _wind : GetComponent<WindTrigger3D>();
            Vector3 direction = wind.Direction;
            MeasureZone(GetComponent<Collider>(), direction, out Vector3 center, out Quaternion rotation, out Vector2 crossSection, out float length);

            // Emitter is a thin slab across the zone's base, facing along Direction (a Box shape emits along its local Z)
            Transform emitter = particles.transform;
            emitter.SetPositionAndRotation(center - direction * (length * 0.5f - EMITTER_THICKNESS * 0.5f), rotation);
            emitter.localScale = Vector3.one;

            ParticleSystem.MainModule main = particles.main;
            // Local scaling ignores the wind object's own scale, so sizes and the shape are in world units
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            float speed = Mathf.Max(wind.Velocity * speedScale, 0.01f);
            main.startSpeed = speed;
            main.startLifetime = Mathf.Max(length / speed, 0.1f);

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;
            shape.scale = new Vector3(crossSection.x, crossSection.y, EMITTER_THICKNESS);

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = density * crossSection.x * crossSection.y;
        }

        // Where the zone is and how big, in a frame whose Z runs along Direction. A BoxCollider is measured in its own
        // (possibly rotated) frame - its axis closest to Direction is the length. Anything else uses its world bounds.
        private static void MeasureZone(Collider zone, Vector3 direction, out Vector3 center, out Quaternion rotation, out Vector2 crossSection, out float length)
        {
            if (zone is BoxCollider box)
            {
                Transform t = box.transform;
                Vector3 scale = t.lossyScale;
                Vector3[] axes = { t.right, t.up, t.forward };
                float[] sizes = { Mathf.Abs(box.size.x * scale.x), Mathf.Abs(box.size.y * scale.y), Mathf.Abs(box.size.z * scale.z) };

                int lengthAxis = 0;
                for (int i = 1; i < 3; i++)
                {
                    if (Mathf.Abs(Vector3.Dot(axes[i], direction)) > Mathf.Abs(Vector3.Dot(axes[lengthAxis], direction)))
                        lengthAxis = i;
                }

                int crossA = (lengthAxis + 1) % 3;
                int crossB = (lengthAxis + 2) % 3;

                center = t.TransformPoint(box.center);
                rotation = Quaternion.LookRotation(direction, axes[crossB]);
                // The emitter's local X lines up with whichever cross axis isn't its up
                crossSection = new Vector2(sizes[crossA], sizes[crossB]);
                length = sizes[lengthAxis];
                return;
            }

            Bounds bounds = zone.bounds;
            Vector3 upHint = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            rotation = Quaternion.LookRotation(direction, upHint);

            center = bounds.center;
            crossSection = new Vector2(ExtentAlong(bounds, rotation * Vector3.right), ExtentAlong(bounds, rotation * Vector3.up));
            length = ExtentAlong(bounds, direction);
        }

        private static float ExtentAlong(Bounds bounds, Vector3 axis)
        {
            Vector3 size = bounds.size;
            return Mathf.Abs(axis.x) * size.x + Mathf.Abs(axis.y) * size.y + Mathf.Abs(axis.z) * size.z;
        }

#if UNITY_EDITOR
        [Button("Create Particles")]
        [HideIf(nameof(particles))]
        private void CreateParticles()
        {
            GameObject child = new("Wind Particles");
            UnityEditor.Undo.RegisterCreatedObjectUndo(child, "Create Wind Particles");
            child.transform.SetParent(transform, false);

            particles = child.AddComponent<ParticleSystem>();
            ApplyDebrisLook(particles);
            FitToZone();

            UnityEditor.EditorUtility.SetDirty(this);
        }

        // A starting look for blown debris - small, tumbling, fluttering, fading in and out. Only applied on creation.
        private static void ApplyDebrisLook(ParticleSystem system)
        {
            ParticleSystem.MainModule main = system.main;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.66f, 0.45f), new Color(0.55f, 0.62f, 0.35f));
            main.gravityModifier = 0f;
            main.maxParticles = 500;

            ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);

            ParticleSystem.NoiseModule flutter = system.noise;
            flutter.enabled = true;
            flutter.strength = 0.6f;
            flutter.frequency = 0.6f;
            flutter.scrollSpeed = 0.5f;

            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            Gradient alpha = new();
            alpha.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = alpha;

            // The active render pipeline's own default particle material (URP: ParticlesUnlit), so it renders in
            // any pipeline instead of the built-in one showing pink
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            renderer.sharedMaterial = pipeline != null
                ? pipeline.defaultParticleMaterial
                : UnityEditor.AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
        }
#endif
    }
}
