using Foundry.Data;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Triggers
{
    public class UpdraftTrigger3D : VolumeTrigger3D
    {
        [Title("Updraft")]
        [SerializeField] private Vector3 direction = Vector3.up;
        [SerializeField] private float velocity = 15f;

        [Title("Onset")]
        [EnumToggleButtons]
        [HideLabel]
        [SerializeField] private UpdraftOnsetMode onsetMode = UpdraftOnsetMode.Smooth;

        [ShowIf(nameof(onsetMode), UpdraftOnsetMode.Smooth)]
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

        [Title("Debug")]
        [SerializeField] private bool drawGizmo = true;

        public Vector3 Direction => direction.normalized;
        public float Velocity => velocity;
        public UpdraftOnsetMode OnsetMode => onsetMode;
        public float OnsetAcceleration => onsetAcceleration;
        public Transform Origin => origin != null ? origin : transform;
        public float MaxDistance => maxDistance;

        private void FixedUpdate()
        {
            if (!active || trackedObjects.Count == 0)
                return;

            foreach (GameObject tracked in trackedObjects)
            {
                if (tracked == null)
                    continue;

                if (!tracked.TryGetComponent(out IUpdraftReceiver receiver))
                    continue;

                float falloff = CalculateFalloff(tracked.transform.position);

                UpdraftData updraftData = new UpdraftData
                {
                    source = gameObject,
                    direction = Direction,
                    velocity = velocity * falloff,
                    onsetMode = onsetMode,
                    onsetAcceleration = onsetAcceleration
                };

                receiver.OnUpdraft(updraftData, Time.fixedDeltaTime);
            }
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
