using FrameCoreU.Events;
using Foundry.Transformers;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Triggers
{
    public class RayTrigger : Transformer
    {
        [Title("Ray")]
        [SerializeField] protected Transform rayOrigin;
        [SerializeField] protected Vector3 localDirection = Vector3.forward;
        [SerializeField] protected float rayDistance = 10f;

        [Title("Filter")]
        [SerializeField] protected LayerMask detectionMask = ~0;

        [Title("Settings")]
        [SerializeField] protected bool triggerOnce = false;
        [SerializeField] protected bool requireHitChange = false;
        [SerializeField] protected bool triggerNoHitEvent = false;

        [Title("Events")]
        [SerializeField] protected FrameCoreEvent onHit;
        [SerializeField] protected FrameCoreEvent onHitEnter;
        [SerializeField] protected FrameCoreEvent onHitExit;
        [SerializeField] protected FrameCoreEvent onNoHit;

        [Title("Debug")]
        [SerializeField] protected bool drawRay = true;

        protected RaycastHit currentHit;
        protected Collider currentCollider;
        protected Collider previousCollider;
        protected bool hasHit;
        protected bool hasTriggeredOnce;

        protected override void Awake()
        {
            base.Awake();

            if (rayOrigin == null)
            {
                rayOrigin = transform;
            }
        }

        public override void Initialize()
        {
            currentHit = default;
            currentCollider = null;
            previousCollider = null;
            hasHit = false;
            hasTriggeredOnce = false;
        }

        protected override void Process()
        {
            if (triggerOnce && hasTriggeredOnce)
                return;

            Vector3 origin = rayOrigin.position;
            Vector3 direction = rayOrigin.TransformDirection(localDirection.normalized);

            previousCollider = currentCollider;

            if (Physics.Raycast(origin, direction, out currentHit, rayDistance, detectionMask))
            {
                hasHit = true;
                currentCollider = currentHit.collider;

                if (requireHitChange && currentCollider == previousCollider)
                    return;

                if (currentCollider != previousCollider)
                {
                    HitEnter(currentHit);
                    onHitEnter?.Activate();
                }

                Hit(currentHit);
                onHit?.Activate();

                hasTriggeredOnce = true;
            }
            else
            {
                hasHit = false;
                currentCollider = null;

                if (previousCollider != null)
                {
                    HitExit(previousCollider);
                    onHitExit?.Activate();
                }

                if (triggerNoHitEvent)
                {
                    NoHit();
                    onNoHit?.Activate();
                }
            }
        }

        protected virtual void Hit(RaycastHit hit)
        {
        }

        protected virtual void HitEnter(RaycastHit hit)
        {
        }

        protected virtual void HitExit(Collider previousHitCollider)
        {
        }

        protected virtual void NoHit()
        {
        }

#if UNITY_EDITOR
        protected override void ValidateData()
        {
            if (rayOrigin == null)
            {
                rayOrigin = transform;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawRay)
                return;

            Transform originTransform = rayOrigin != null ? rayOrigin : transform;
            Vector3 origin = originTransform.position;
            Vector3 direction = originTransform.TransformDirection(localDirection.normalized);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + direction * rayDistance);
        }
#endif
    }
}