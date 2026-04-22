using FrameCoreU.Events;
using Foundry.Interfaces;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Triggers
{
    [RequireComponent(typeof(Collider))]
    public class ImpactTrigger : MonoBehaviour
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
            Collider impactCollider = GetComponent<Collider>();

            if (impactCollider != null)
            {
                impactCollider.isTrigger = false;
            }
        }

        protected virtual void OnCollisionEnter(Collision collision)
        {
            if (!active)
                return;

            if (!PassesFilter(collision))
                return;

            lastImpactObject = collision.gameObject;
            lastImpactStrength = GetImpactStrength(collision);

            NotifyImpactReceiver(collision, lastImpactStrength);

            Impact(collision);
            onImpact?.Activate();
        }

        protected virtual bool PassesFilter(Collision collision)
        {
            if (collision == null || collision.gameObject == null)
                return false;

            return ((1 << collision.gameObject.layer) & detectionMask.value) != 0;
        }

        protected virtual float GetImpactStrength(Collision collision)
        {
            if (collision == null)
                return 0f;

            return collision.relativeVelocity.magnitude;
        }

        protected virtual void NotifyImpactReceiver(Collision collision, float impactStrength)
        {
            if (collision == null || collision.gameObject == null)
                return;

            IImpactReceiver receiver = collision.gameObject.GetComponent<IImpactReceiver>();

            if (receiver != null)
            {
                receiver.OnImpact(impactStrength, collision);
            }
        }

        protected virtual void Impact(Collision collision)
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