using FrameCoreU.Events;
using Foundry.Data;
using Foundry.Transformers;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Triggers
{
    public abstract class RayTriggerBase : Transformer
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

        [Title("System")]
        [ReadOnly]
        [SerializeField] protected GameObject currentTarget;

        [ReadOnly]
        [SerializeField] protected GameObject previousTarget;

        [ReadOnly]
        [SerializeField] protected bool hasHit;

        protected RaycastData currentHitData;
        protected RaycastData previousHitData;
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
            currentHitData = null;
            previousHitData = null;
            currentTarget = null;
            previousTarget = null;
            hasHit = false;
            hasTriggeredOnce = false;
        }

        protected override void Process()
        {
            if (triggerOnce && hasTriggeredOnce)
                return;

            previousHitData = currentHitData;
            previousTarget = currentTarget;

            if (TryGetHit(out RaycastData hitData))
            {
                hasHit = true;
                currentHitData = hitData;
                currentTarget = hitData != null ? hitData.target : null;

                if (currentTarget == null)
                    return;

                if (requireHitChange && currentTarget == previousTarget)
                    return;

                if (currentTarget != previousTarget)
                {
                    HitEnter(currentHitData);
                    onHitEnter?.Activate();
                }

                Hit(currentHitData);
                onHit?.Activate();

                hasTriggeredOnce = true;
            }
            else
            {
                hasHit = false;
                currentHitData = null;
                currentTarget = null;

                if (previousTarget != null)
                {
                    HitExit(previousHitData);
                    onHitExit?.Activate();
                }

                if (triggerNoHitEvent)
                {
                    NoHit();
                    onNoHit?.Activate();
                }
            }
        }

        protected virtual Vector3 GetRayOrigin()
        {
            return rayOrigin.position;
        }

        protected virtual Vector3 GetRayDirection()
        {
            return rayOrigin.TransformDirection(localDirection.normalized);
        }

        protected abstract bool TryGetHit(out RaycastData hitData);

        protected virtual void Hit(RaycastData hitData)
        {
        }

        protected virtual void HitEnter(RaycastData hitData)
        {
        }

        protected virtual void HitExit(RaycastData previousHitData)
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

        protected virtual void OnDrawGizmosSelected()
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