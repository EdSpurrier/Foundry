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

        [Title("Debug")]
        [SerializeField] private bool drawGizmo = true;

        public Vector3 Direction => direction.normalized;
        public float Velocity => velocity;
        public UpdraftOnsetMode OnsetMode => onsetMode;
        public float OnsetAcceleration => onsetAcceleration;

        private void FixedUpdate()
        {
            if (!active || trackedObjects.Count == 0)
                return;

            UpdraftData updraftData = new UpdraftData
            {
                source = gameObject,
                direction = Direction,
                velocity = velocity,
                onsetMode = onsetMode,
                onsetAcceleration = onsetAcceleration
            };

            foreach (GameObject tracked in trackedObjects)
            {
                if (tracked == null)
                    continue;

                if (tracked.TryGetComponent(out IUpdraftReceiver receiver))
                {
                    receiver.OnUpdraft(updraftData, Time.fixedDeltaTime);
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo)
                return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, Direction * 2f);
        }
#endif
    }
}
