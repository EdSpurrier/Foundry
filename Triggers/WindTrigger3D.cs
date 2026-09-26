using System.Collections.Generic;
using Foundry.Data;
using Foundry.Particles;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Foundry.Triggers
{
    // A wind zone blowing in any direction - an updraft vent to glide on, a mechanical fan blowing across a level, a
    // gust through a gap. Anything inside with an IWindReceiver is told about the wind and decides for itself what to
    // do (the player only rides it while gliding). Any other non-kinematic Rigidbody inside is blown along Direction in
    // proportion to how light it is, and as an IParticleAffector it blows the particles of any ParticlePhysics system
    // too. The zone is this object's trigger collider - use a Box, Sphere, Capsule or convex Mesh collider.
    public class WindTrigger3D : VolumeTrigger3D, IParticleAffector
    {
        [Title("Wind")]
        [Tooltip("Which way it blows. With Direction Space = Self it's relative to this object's rotation, so a fan can be aimed just by rotating it.")]
        [SerializeField] private Vector3 direction = Vector3.up;

        [Tooltip("Self: Direction turns with this object's rotation (aim a fan by rotating it). World: Direction is fixed in world space.")]
        [SerializeField] private Space directionSpace = Space.Self;

        [Tooltip("Wind speed (m/s) along Direction at full strength.")]
        [SerializeField] private float velocity = 15f;

        [Title("Onset")]
        [EnumToggleButtons]
        [HideLabel]
        [SerializeField] private WindOnsetMode onsetMode = WindOnsetMode.Smooth;

        [ShowIf(nameof(onsetMode), WindOnsetMode.Smooth)]
        [Tooltip("How quickly velocity is pulled toward direction*velocity. Ignored when Onset is Instant.")]
        [SerializeField] private float onsetAcceleration = 40f;

        [Title("Falloff")]
        [SerializeField] private bool useFalloff = false;

        [ShowIf(nameof(useFalloff))]
        [Tooltip("Reference point distance is measured from - typically the base of the vent/fan. Defaults to this object's own transform if left empty.")]
        [SerializeField] private Transform origin;

        [ShowIf(nameof(useFalloff))]
        [Tooltip("Distance along Direction from Origin at which the falloff curve reaches its end (t=1). Set this to roughly match the collider's length along Direction.")]
        [SerializeField] private float maxDistance = 5f;

        [ShowIf(nameof(useFalloff))]
        [Tooltip("Push strength multiplier over normalized distance from Origin (0 = at Origin, 1 = at Max Distance). Author starting at 1 (full push near the base) easing to 0 (no push at the top), so the receiver settles into a float rather than being launched indefinitely.")]
        [SerializeField] private AnimationCurve falloffCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Title("Rigidbodies")]
        [Tooltip("Blow any non-kinematic Rigidbody inside (that isn't an IWindReceiver, like the player) along Direction, in proportion to how light it is.")]
        [SerializeField] private bool pushRigidbodies = true;

        [ShowIf(nameof(pushRigidbodies))]
        [Tooltip("How strong the wind is, as the heaviest mass (kg) it could hold up against gravity at full strength - blowing upward, anything lighter rises and anything heavier doesn't lift. Blowing sideways the same force applies: a light egg is carried along toward Velocity, while a heavy rock gets a push too small to beat its ground friction. Nothing is ever pushed faster than Velocity.")]
        [SuffixLabel("kg", Overlay = true)]
        [FormerlySerializedAs("maxMassLifted")]
        [SerializeField, Min(0f)] private float strength = 1f;

        [Title("Particles")]
        [Tooltip("Blow the particles of any particle system with a ParticlePhysics component while they're inside this zone.")]
        [SerializeField] private bool pushParticles = true;

        [ShowIf(nameof(pushParticles))]
        [Tooltip("How quickly particles in this zone catch up to the wind's speed, per second, at Sensitivity 1 (each particle system's ParticlePhysics Sensitivity scales it). Push only - particles already outrunning the wind aren't slowed.")]
        [SerializeField, Min(0f)] private float particleCatchUp = 6f;

        [Title("Debug")]
        [SerializeField] private bool drawGizmo = true;

        private readonly List<GameObject> _staleTracked = new();
        private Collider _zone;

        protected override void Awake()
        {
            base.Awake();
            _zone = GetComponent<Collider>();
        }

        private void OnEnable()
        {
            ParticleAffectors.Register(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ParticleAffectors.Unregister(this);
        }

        public bool IsAffecting => active && pushParticles;

        public Bounds AffectBounds => _zone.bounds;

        // Same wind as everything else: pulls the particle's speed along Direction up toward the wind's speed at its
        // position (with falloff), at particleCatchUp scaled by the receiving system's sensitivity
        public Vector3 Affect(Vector3 position, Vector3 particleVelocity, float sensitivity, float deltaTime)
        {
            if (!TryGetWindAt(position, out float windSpeed))
                return particleVelocity;

            Vector3 axis = Direction;
            float along = Vector3.Dot(particleVelocity, axis);
            if (along >= windSpeed)
                return particleVelocity;

            float blend = Mathf.Clamp01(particleCatchUp * sensitivity * deltaTime);
            return particleVelocity + axis * ((windSpeed - along) * blend);
        }

        // Wind speed along Direction at a world position, with falloff applied. False if the position is outside the
        // zone's collider (ClosestPoint returns the point itself when it's inside a convex collider).
        public bool TryGetWindAt(Vector3 position, out float windSpeed)
        {
            windSpeed = 0f;
            if ((_zone.ClosestPoint(position) - position).sqrMagnitude > 0.0001f)
                return false;

            windSpeed = velocity * CalculateFalloff(position);
            return true;
        }

        // World space, normalized
        public Vector3 Direction => (directionSpace == Space.Self ? transform.TransformDirection(direction) : direction).normalized;
        public float Velocity => velocity;
        public WindOnsetMode OnsetMode => onsetMode;
        public float OnsetAcceleration => onsetAcceleration;
        public Transform Origin => origin != null ? origin : transform;
        public float MaxDistance => maxDistance;

        private void FixedUpdate()
        {
            if (!active || trackedObjects.Count == 0)
                return;

            foreach (GameObject tracked in trackedObjects)
            {
                // Destroyed or deactivated inside the zone (e.g. despawned to a pool) - there's no exit event for
                // that, so drop it here or a pooled object would still be pushed after respawning elsewhere
                if (tracked == null || !tracked.activeInHierarchy)
                {
                    _staleTracked.Add(tracked);
                    continue;
                }

                float falloff = CalculateFalloff(tracked.transform.position);

                if (tracked.TryGetComponent(out IWindReceiver receiver))
                {
                    WindData windData = new WindData
                    {
                        source = gameObject,
                        direction = Direction,
                        velocity = velocity * falloff,
                        onsetMode = onsetMode,
                        onsetAcceleration = onsetAcceleration
                    };

                    receiver.OnWind(windData, Time.fixedDeltaTime);
                }
                else if (pushRigidbodies && tracked.TryGetComponent(out Rigidbody body) && !body.isKinematic)
                {
                    PushRigidbody(body, velocity * falloff);
                }
            }

            RemoveStaleTracked();
        }

        // Air drag toward the wind's speed along Direction: force proportional to how much slower than the wind the
        // body is moving, scaled so a body of exactly `strength` kg hovers at full strength (drag * windSpeed = m*g).
        // Applied as a velocity change capped at closing the whole gap in one step, so very light bodies (egg shell
        // pieces weigh 1e-7 kg) match the wind instead of being fired off at absurd speed. Push only - a body already
        // moving faster than the wind along Direction isn't slowed.
        private void PushRigidbody(Rigidbody body, float windSpeed)
        {
            Vector3 axis = Direction;
            float gap = windSpeed - Vector3.Dot(body.linearVelocity, axis);
            if (gap <= 0f)
                return;

            float drag = strength * Physics.gravity.magnitude / Mathf.Max(velocity, 0.01f);
            float response = Mathf.Clamp01(drag * Time.fixedDeltaTime / Mathf.Max(body.mass, 1e-9f));

            body.AddForce(axis * (gap * response), ForceMode.VelocityChange);
        }

        private void RemoveStaleTracked()
        {
            if (_staleTracked.Count == 0)
                return;

            foreach (GameObject stale in _staleTracked)
                trackedObjects.Remove(stale);

            _staleTracked.Clear();
            RefreshTrackedState();
        }

        private float CalculateFalloff(Vector3 targetPosition)
        {
            if (!useFalloff)
                return 1f;

            // Distance projected along the push direction only - lateral drift inside the column doesn't reduce the push, just how far along it you've travelled.
            float distanceAlongAxis = Vector3.Dot(targetPosition - Origin.position, Direction);
            float normalizedDistance = maxDistance > 0f ? Mathf.Clamp01(distanceAlongAxis / maxDistance) : 0f;

            return Mathf.Clamp01(falloffCurve.Evaluate(normalizedDistance));
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo)
                return;

            Vector3 originPosition = Origin.position;

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(originPosition, Direction * (useFalloff ? maxDistance : 2f));

            if (useFalloff)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(originPosition + Direction * maxDistance, 0.25f);
            }
        }
#endif
    }
}
