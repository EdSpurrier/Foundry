using System;
using System.Collections.Generic;
using Foundry.Common;
using Foundry.Particles;
using Foundry.Weather;
using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Hazards
{
    // A patch of fire on the ground: flames, embers, smoke and a flickering light, all built in code (give it an
    // additive and an alpha-blended particle material). While it's lit, anything on Touch Layers that touches it fires
    // On Touch (again every Touch Repeat while it stays in) - hook the reaction there, e.g. throw the player back.
    //
    // Rain puts it out: it checks a row of points across itself against every Rain (Rain.AnyRainingOn), gets wetter
    // the more of it is rained on (Douse Time = seconds of rain over all of it), steams, shrinks, and goes out at fully
    // wet. If the rain stops first it dries and recovers. Ignite() / Extinguish() / Douse() from the Fire action.
    [RequireComponent(typeof(BoxCollider))]
    public class Fire : MonoBehaviour
    {
        [Title("Fire")]
        [Tooltip("Width (X), height (Y) and depth (Z) of the fire. It sits on the ground at this object's position.")]
        [SerializeField] private Vector3 size = new(4f, 2.5f, 1.5f);

        [SerializeField] private bool startLit = true;

        [Tooltip("How long it takes to flare up from nothing.")]
        [SuffixLabel("s", Overlay = true)]
        [SerializeField, Min(0f)] private float igniteTime = 0.8f;

        [Tooltip("How long it takes to die down once it's out.")]
        [SuffixLabel("s", Overlay = true)]
        [SerializeField, Min(0f)] private float dieTime = 1.2f;

        [Title("Touching It")]
        [Tooltip("What fires On Touch (e.g. the Player layer).")]
        [SerializeField] private LayerMask touchLayers;

        [Tooltip("While something stays in the fire, On Touch fires again this often.")]
        [SuffixLabel("s", Overlay = true)]
        [SerializeField, Min(0.05f)] private float touchRepeat = 0.6f;

        [Title("Putting It Out")]
        [Tooltip("Seconds of rain over all of it to put it out. Half covered = twice as long.")]
        [SuffixLabel("s", Overlay = true)]
        [SerializeField, Min(0.1f)] private float douseTime = 3f;

        [Tooltip("How fast it dries out again when the rain stops before it's out (per second; 1 = fully).")]
        [SerializeField, Min(0f)] private float recoverRate = 0.15f;

        [Tooltip("How many points across it are checked for rain.")]
        [SerializeField, Range(1, 16)] private int rainSamples = 6;

        [Title("Look")]
        [Tooltip("Additive particle material (flames, embers).")]
        [SerializeField, Required] private Material glowMaterial;

        [Tooltip("Alpha-blended particle material (smoke, steam).")]
        [SerializeField, Required] private Material smokeMaterial;

        [SerializeField] private Color flameColor = new(1f, 0.5f, 0.1f);
        [SerializeField] private bool castLight = true;

        [ShowIf(nameof(castLight))]
        [SerializeField, Min(0f)] private float lightIntensity = 8f;

        [Title("Events")]
        [SerializeField, HideLabel] private FrameCoreEvent onIgnite = new() { eventName = "Fire - Ignite" };
        [SerializeField, HideLabel] private FrameCoreEvent onExtinguished = new() { eventName = "Fire - Extinguished" };
        [SerializeField, HideLabel] private FrameCoreEvent onTouch = new() { eventName = "Fire - Touch" };

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private bool lit;

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly, Range(0f, 1f)] private float heat;

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly, Range(0f, 1f)] private float wetness;

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly, Range(0f, 1f)] private float rainCover;

        // (fire, what touched it)
        public event Action<Fire, GameObject> Touched;
        public event Action<Fire> Extinguished;

        public bool IsLit => lit;
        public float Heat => heat;
        public float Wetness => wetness;

        private BoxCollider _trigger;
        private ParticleSystem _flames, _embers, _smoke, _steam;
        private Light _light;
        private float _afterSmoke;
        private float _lastTouch = float.NegativeInfinity;
        private readonly HashSet<Collider> _inside = new();

        private void Awake()
        {
            _trigger = GetComponent<BoxCollider>();
            _trigger.isTrigger = true;
            _trigger.size = size;
            _trigger.center = new Vector3(0f, size.y * 0.5f, 0f);
            Build();
            lit = false;
            heat = 0f;
            if (startLit) Ignite(instantly: true);
            else _trigger.enabled = false;
            ApplyLook();
        }

        // --- Control

        public void Ignite() => Ignite(false);

        public void Ignite(bool instantly)
        {
            if (lit) return;
            lit = true;
            wetness = 0f;
            if (instantly) heat = 1f;
            _trigger.enabled = true;
            onIgnite?.Activate();
        }

        public void Extinguish()
        {
            if (!lit) return;
            lit = false;
            _afterSmoke = 1f;
            _trigger.enabled = false;
            _inside.Clear();
            if (_steam != null) _steam.Emit(Mathf.CeilToInt(12 * size.x));
            onExtinguished?.Activate();
            Extinguished?.Invoke(this);
        }

        // Make it wetter by this much (1 = puts it right out) - a bucket of water, a splash
        public void Douse(float amount)
        {
            if (!lit) return;
            wetness = Mathf.Clamp01(wetness + amount);
            if (wetness >= 1f) Extinguish();
        }

        // --- Update

        private void Update()
        {
            float dt = Time.deltaTime;
            rainCover = lit ? RainCover() : 0f;
            if (lit)
            {
                if (rainCover > 0f) wetness += rainCover * dt / douseTime;
                else wetness -= recoverRate * dt;
                wetness = Mathf.Clamp01(wetness);
                if (wetness >= 1f) Extinguish();
            }

            float goal = lit ? 1f : 0f;
            float rate = lit ? (igniteTime > 0f ? 1f / igniteTime : 1e6f) : (dieTime > 0f ? 1f / dieTime : 1e6f);
            heat = Mathf.MoveTowards(heat, goal, rate * dt);
            _afterSmoke = Mathf.MoveTowards(_afterSmoke, 0f, dt / 3f);
            ApplyLook();
        }

        private float RainCover()
        {
            if (Rain.All.Count == 0) return 0f;
            int wet = 0;
            for (int i = 0; i < rainSamples; i++)
            {
                float u = (i + 0.5f) / rainSamples - 0.5f;
                Vector3 point = transform.TransformPoint(new Vector3(u * size.x, size.y * 0.5f, 0f));
                if (Rain.AnyRainingOn(point)) wet++;
            }
            return (float)wet / rainSamples;
        }

        // --- Touching

        private void OnTriggerEnter(Collider other) => Touch(other);
        private void OnTriggerStay(Collider other) => Touch(other);

        private void Touch(Collider other)
        {
            if (!lit || heat < 0.3f || other == null || !touchLayers.Contains(other.gameObject)) return;
            if (Time.time - _lastTouch < touchRepeat) return;
            _lastTouch = Time.time;
            onTouch?.Activate();
            Touched?.Invoke(this, other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject);
        }

        // --- Look

        private void ApplyLook()
        {
            float strength = heat * (1f - 0.65f * wetness);
            float width = Mathf.Max(0.5f, size.x);
            FxBuilder.SetRate(_flames, 30f * width * strength);
            FxBuilder.SetRate(_embers, 5f * width * strength);
            FxBuilder.SetRate(_smoke, 4f * width * Mathf.Max(strength, _afterSmoke));
            FxBuilder.SetRate(_steam, 24f * width * (lit ? rainCover : 0f) + 12f * width * _afterSmoke);
            if (_flames != null)
            {
                var main = _flames.main;
                main.startSizeMultiplier = Mathf.Lerp(0.45f, 1f, strength) * FlameScale;
            }
            if (_light != null)
            {
                float flicker = 0.75f + 0.5f * Mathf.PerlinNoise(Time.time * 7f, transform.position.x);
                _light.intensity = lightIntensity * strength * flicker;
                _light.enabled = strength > 0.01f;
            }
        }

        private float FlameScale => Mathf.Clamp(size.y / 2.5f, 0.4f, 3f);

        private void Build()
        {
            if (glowMaterial == null || smokeMaterial == null) return;
            float flameTime = 0.75f;
            float scale = FlameScale;

            // flames: soft blobs rising and shrinking, white-yellow -> orange -> red
            _flames = FxBuilder.Create(transform, "FX Flames", glowMaterial, 400);
            var main = _flames.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(flameTime * 0.75f, flameTime * 1.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f * scale, 1.5f * scale);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            var shape = _flames.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0f, 0.3f * scale, 0f);
            shape.scale = new Vector3(size.x * 0.85f, 0.2f, size.z * 0.5f);
            var velocity = _flames.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            float rise = size.y / flameTime;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            velocity.y = new ParticleSystem.MinMaxCurve(rise * 0.9f, rise * 1.5f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var sizeOver = _flames.sizeOverLifetime;
            sizeOver.enabled = true;
            sizeOver.size = new ParticleSystem.MinMaxCurve(1f, FxBuilder.MakeCurve(0f, 0.6f, 0.25f, 1f, 0.7f, 0.65f, 1f, 0.1f));
            var colorOver = _flames.colorOverLifetime;
            colorOver.enabled = true;
            colorOver.color = FxBuilder.MakeGradient(
                new[] { new Color(1f, 0.95f, 0.7f), flameColor, new Color(0.75f, 0.12f, 0.03f) },
                new[] { 0f, 0.4f, 0.85f },
                new[] { 0f, 0f, 0.9f, 0.12f, 0.7f, 0.6f, 0f, 1f });
            var spin = _flames.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            var noise = _flames.noise;
            noise.enabled = true;
            noise.strength = 0.45f;
            noise.frequency = 1.2f;
            noise.scrollSpeed = 1.5f;

            // embers: bright specks drifting up out of it
            _embers = FxBuilder.Create(transform, "FX Embers", glowMaterial, 120);
            main = _embers.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.startColor = new Color(1f, 0.7f, 0.3f);
            shape = _embers.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0f, size.y * 0.4f, 0f);
            shape.scale = new Vector3(size.x * 0.8f, size.y * 0.4f, size.z * 0.4f);
            velocity = _embers.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            velocity.y = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            colorOver = _embers.colorOverLifetime;
            colorOver.enabled = true;
            colorOver.color = FxBuilder.MakeGradient(new[] { Color.white, Color.white }, new[] { 0f, 1f },
                new[] { 1f, 0f, 1f, 0.6f, 0f, 1f });
            noise = _embers.noise;
            noise.enabled = true;
            noise.strength = 1.5f;
            noise.frequency = 0.8f;

            // smoke: dark puffs from the top of the flames
            _smoke = FxBuilder.Create(transform, "FX Smoke", smokeMaterial, 120);
            ConfigurePuffs(_smoke, new Color(0.12f, 0.11f, 0.1f), 0.3f, size.y * 0.8f, new Vector2(2.4f, 3.4f),
                new Vector2(1f, 1.6f), new Vector2(1.6f * scale, 2.4f * scale), 2.4f);

            // steam: white puffs while the rain hits it, and a big burst as it goes out
            _steam = FxBuilder.Create(transform, "FX Steam", smokeMaterial, 250);
            ConfigurePuffs(_steam, new Color(0.92f, 0.94f, 0.97f), 0.32f, size.y * 0.3f, new Vector2(1.2f, 2f),
                new Vector2(1.5f, 2.6f), new Vector2(1.1f * scale, 1.7f * scale), 2.6f);

            if (castLight)
            {
                var lightObject = new GameObject("FX Light");
                lightObject.transform.SetParent(transform, false);
                lightObject.transform.localPosition = new Vector3(0f, size.y * 0.5f, -1.5f);
                _light = lightObject.AddComponent<Light>();
                _light.type = LightType.Point;
                _light.color = flameColor;
                _light.range = size.x * 1.5f + 5f;
                _light.shadows = LightShadows.None;
                _light.intensity = 0f;
            }
        }

        private void ConfigurePuffs(ParticleSystem system, Color color, float alpha, float height, Vector2 lifetime,
            Vector2 rise, Vector2 startSize, float grow)
        {
            var main = system.main;
            main.maxParticles = 400;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSize = new ParticleSystem.MinMaxCurve(startSize.x, startSize.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0f, height, 0f);
            shape.scale = new Vector3(size.x * 0.8f, 0.3f, size.z * 0.4f);
            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            velocity.y = new ParticleSystem.MinMaxCurve(rise.x, rise.y);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var sizeOver = system.sizeOverLifetime;
            sizeOver.enabled = true;
            sizeOver.size = new ParticleSystem.MinMaxCurve(1f, FxBuilder.MakeCurve(0f, 1f, 1f, grow));
            var colorOver = system.colorOverLifetime;
            colorOver.enabled = true;
            colorOver.color = FxBuilder.MakeGradient(new[] { Color.white, Color.white }, new[] { 0f, 1f },
                new[] { 0f, 0f, alpha, 0.2f, 0f, 1f });
            var spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, size.y * 0.5f, 0f), size);
            Gizmos.color = new Color(0.4f, 0.6f, 1f);
            for (int i = 0; i < rainSamples; i++)
            {
                float u = (i + 0.5f) / rainSamples - 0.5f;
                Gizmos.DrawWireSphere(new Vector3(u * size.x, size.y * 0.5f, 0f), 0.1f);
            }
        }
    }
}
