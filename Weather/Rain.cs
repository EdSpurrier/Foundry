using System.Collections.Generic;
using Foundry.Particles;
using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Weather
{
    // Rain over an area: drops fall from the top of the box (Area, sitting on this object) and stop on whatever they hit
    // in Blocking Layers - a roof keeps the room under it dry, knock a hole in it and the rain comes through. Builds its
    // own particles (streaks + little splashes); give it an alpha-blended particle material.
    //
    // Other things ask it whether a point is getting wet - IsRainingOn(point) / Rain.AnyRainingOn(point): inside the
    // area, raining, and nothing in Blocking Layers between the point and the sky along the drops' path. A Fire uses
    // that to go out.
    public class Rain : MonoBehaviour
    {
        private static readonly List<Rain> ActiveRain = new();
        public static IReadOnlyList<Rain> All => ActiveRain;

        [Title("Area")]
        [Tooltip("Width (X), height (Y) and depth (Z) of the rain. It sits on this object: drops start at the top and land across the bottom.")]
        [SerializeField] private Vector3 area = new(40f, 20f, 6f);

        [SerializeField] private bool rainOnStart;

        [Tooltip("What stops the drops (roofs, ground...) - and keeps what's under it dry.")]
        [SerializeField] private LayerMask blockingLayers = ~0;

        [Title("Drops")]
        [SerializeField, Min(0f)] private float dropsPerSecond = 350f;

        [SuffixLabel("m/s", Overlay = true)]
        [SerializeField, Min(1f)] private float fallSpeed = 20f;

        [Tooltip("Sideways speed (m/s, + = to the right) - slants the rain.")]
        [SuffixLabel("m/s", Overlay = true)]
        [SerializeField] private float drift = 4f;

        [SerializeField, Min(0.01f)] private float dropLength = 0.8f;
        [SerializeField, Min(0.001f)] private float dropWidth = 0.05f;
        [SerializeField] private Color color = new(0.75f, 0.83f, 1f, 0.6f);
        [SerializeField] private bool splashes = true;

        [Tooltip("Alpha-blended particle material (e.g. URP Particles/Unlit, Transparent, Alpha).")]
        [SerializeField, Required] private Material material;

        [Tooltip("How long it takes to build up or die away.")]
        [SuffixLabel("s", Overlay = true)]
        [SerializeField, Min(0f)] private float fadeTime = 1.5f;

        [Title("Events")]
        [SerializeField, HideLabel] private FrameCoreEvent onStart = new() { eventName = "Rain - Start" };
        [SerializeField, HideLabel] private FrameCoreEvent onStop = new() { eventName = "Rain - Stop" };

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private bool raining;

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly, Range(0f, 1f)] private float intensity;

        private ParticleSystem _drops;
        private ParticleSystem _splashes;

        public bool IsRaining => raining;
        public float Intensity => intensity;
        private float FallTime => area.y / fallSpeed;

        private void Awake()
        {
            raining = false;
            intensity = 0f;
            Build();
        }

        private void OnEnable()
        {
            if (!ActiveRain.Contains(this)) ActiveRain.Add(this);
        }

        private void OnDisable()
        {
            ActiveRain.Remove(this);
        }

        private void Start()
        {
            if (rainOnStart) StartRain();
        }

        public void StartRain()
        {
            if (raining) return;
            raining = true;
            if (_drops != null && !_drops.isPlaying) _drops.Play(true);
            onStart?.Activate();
        }

        public void StopRain()
        {
            if (!raining) return;
            raining = false;
            onStop?.Activate();
        }

        public void Toggle()
        {
            if (raining) StopRain();
            else StartRain();
        }

        private void Update()
        {
            float goal = raining ? 1f : 0f;
            intensity = fadeTime > 0f ? Mathf.MoveTowards(intensity, goal, Time.deltaTime / fadeTime) : goal;
            if (_drops == null) return;
            var emission = _drops.emission;
            emission.rateOverTime = dropsPerSecond * intensity;
        }

        // Is rain landing on this point right now? (Raining, inside the area, and open to the sky along the drops' path.)
        public bool IsRainingOn(Vector3 point)
        {
            if (intensity < 0.3f || !isActiveAndEnabled) return false;
            Vector3 bottom = transform.position;
            float top = bottom.y + area.y;
            if (point.y < bottom.y - 0.5f || point.y > top) return false;
            if (Mathf.Abs(point.z - bottom.z) > area.z * 0.5f) return false;

            // follow the drop back up to where it started and check that's somewhere drops come from
            float t = (top - point.y) / fallSpeed;
            Vector3 origin = new(point.x - drift * t, top, point.z);
            float startShift = -drift * FallTime;
            if (Mathf.Abs(origin.x - (bottom.x + startShift)) > area.x * 0.5f) return false;

            return !Physics.Linecast(origin, point, blockingLayers, QueryTriggerInteraction.Ignore);
        }

        public static bool AnyRainingOn(Vector3 point)
        {
            foreach (Rain rain in ActiveRain)
                if (rain != null && rain.IsRainingOn(point))
                    return true;
            return false;
        }

        // --- Particles

        private void Build()
        {
            if (material == null) return;
            float fallTime = FallTime;
            int max = Mathf.CeilToInt(dropsPerSecond * fallTime * 1.3f) + 50;
            _drops = FxBuilder.Create(transform, "FX Rain", material, max);
            _drops.transform.localPosition = new Vector3(-drift * fallTime, area.y, 0f);

            var main = _drops.main;
            main.startLifetime = fallTime * 1.05f;
            main.startSize = dropWidth;
            main.startColor = color;

            var emission = _drops.emission;
            emission.rateOverTime = 0f;

            var shape = _drops.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(area.x, 0f, area.z);

            var velocity = _drops.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = drift;
            velocity.y = -fallSpeed;
            velocity.z = 0f;

            var renderer = _drops.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1f;
            renderer.velocityScale = dropLength / fallSpeed;

            var collision = _drops.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.collidesWith = blockingLayers;
            collision.quality = ParticleSystemCollisionQuality.High;
            collision.lifetimeLoss = 1f;
            collision.bounce = 0f;
            collision.dampen = 1f;
            collision.radiusScale = 0.5f;
            collision.enableDynamicColliders = true;

            if (!splashes) return;
            _splashes = FxBuilder.Create(_drops.transform, "FX Splashes", material, 400);
            var sMain = _splashes.main;
            sMain.loop = false;
            sMain.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            sMain.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            sMain.startSize = new ParticleSystem.MinMaxCurve(dropWidth * 1.2f, dropWidth * 2f);
            sMain.startColor = color;
            sMain.gravityModifier = 1.2f;
            var sEmission = _splashes.emission;
            sEmission.rateOverTime = 0f;
            sEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 2, 3) });
            var sShape = _splashes.shape;
            sShape.shapeType = ParticleSystemShapeType.Cone;
            sShape.angle = 35f;
            sShape.radius = 0.02f;
            sShape.rotation = new Vector3(-90f, 0f, 0f); // point up
            var sub = _drops.subEmitters;
            sub.enabled = true;
            sub.AddSubEmitter(_splashes, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.6f, 1f, 0.8f);
            Vector3 b = transform.position;
            Gizmos.DrawWireCube(b + Vector3.up * area.y * 0.5f, area);
            float fallTime = FallTime;
            Gizmos.DrawLine(b + new Vector3(-drift * fallTime, area.y, 0f), b);
        }
    }
}
