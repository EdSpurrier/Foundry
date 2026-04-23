using FrameCoreU.Events;
using Foundry.Common;
using Foundry.Data;
using Foundry.Interfaces;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Triggers
{
    [RequireComponent(typeof(Collider2D))]
    public class ImpactTrigger2D : MonoBehaviour
    {
        [Title("Settings")]
        [SerializeField] protected bool active = true;

        [Title("Filter")]
        [SerializeField] protected LayerMask detectionMask = ~0;

        [Title("Events")]
        [SerializeField] protected FrameCoreEvent onImpact;

        [Title("System")]
        [ReadOnly]
        [SerializeField] protected GameObject lastImpactObject;

        [ReadOnly]
        [SerializeField] protected float lastImpactStrength;

        protected virtual void Reset()
        {
            Collider2D impactCollider = GetComponent<Collider2D>();

            if (impactCollider != null)
            {
                impactCollider.isTrigger = false;
            }
        }

        protected virtual void OnCollisionEnter2D(Collision2D collision)
        {
            if (!active)
                return;

            if (!PassesFilter(collision))
                return;

            ImpactData impactData = BuildImpactData(collision);

            if (impactData == null || !impactData.IsValid)
                return;

            lastImpactObject = impactData.target;
            lastImpactStrength = impactData.force;

            NotifyImpactReceiver(impactData);

            Impact(impactData);
            onImpact?.Activate();
        }

        protected virtual bool PassesFilter(Collision2D collision)
        {
            return collision != null && detectionMask.Contains(collision.gameObject);
        }

        protected virtual float GetImpactStrength(Collision2D collision)
        {
            if (collision == null)
                return 0f;

            return collision.relativeVelocity.magnitude;
        }

        protected virtual ImpactData BuildImpactData(Collision2D collision)
        {
            if (collision == null || collision.gameObject == null)
                return null;

            ContactPoint2D contact = collision.contactCount > 0 ? collision.GetContact(0) : default;

            return new ImpactData
            {
                source = gameObject,
                target = collision.gameObject,
                force = GetImpactStrength(collision),
                point = contact.point,
                normal = contact.normal,
                collider2D = collision.collider,
                collision2D = collision,
                is2D = true,
            };
        }

        protected virtual void NotifyImpactReceiver(ImpactData impactData)
        {
            if (impactData == null || impactData.target == null)
                return;

            IImpactReceiver receiver = impactData.target.GetComponent<IImpactReceiver>();

            receiver?.OnImpact(impactData);
        }

        protected virtual void Impact(ImpactData impactData)
        {
        }

        public void Activate()
        {
            active = true;
        }

        public void Deactivate()
        {
            active = false;
        }
    }
}
