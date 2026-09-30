using FrameCoreU.Events;
using Foundry.Common;
using Foundry.Data;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Triggers
{
    [RequireComponent(typeof(Collider))]
    public class ImpactTrigger3D : MonoBehaviour
    {
        [BoxGroup("Events")]
        [FoldoutGroup("Events/On Impact")]
        [HideLabel]
        [SerializeField] protected FrameCoreEvent onImpact;
        
        [Title("Settings")]
        [SerializeField] protected bool active = true;

        [Title("Filter")]
        [SerializeField] protected LayerMask detectionMask = ~0;

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

            ImpactData impactData = BuildImpactData(collision);

            if (impactData == null || !impactData.IsValid)
                return;

            lastImpactObject = impactData.target;
            lastImpactStrength = impactData.force;

            NotifyImpactReceiver(impactData);

            Impact(impactData);
            onImpact?.Activate();
        }

        protected virtual bool PassesFilter(Collision collision)
        {
            return collision != null && detectionMask.Contains(collision.gameObject);
        }

        protected virtual float GetImpactStrength(Collision collision)
        {
            if (collision == null)
                return 0f;

            return collision.relativeVelocity.magnitude;
        }

        protected virtual ImpactData BuildImpactData(Collision collision)
        {
            if (collision == null || collision.gameObject == null)
                return null;

            ContactPoint contact = collision.contactCount > 0 ? collision.GetContact(0) : default;

            return new ImpactData
            {
                source = gameObject,
                target = collision.gameObject,
                force = GetImpactStrength(collision),
                point = contact.point,
                normal = contact.normal,
                collider3D = collision.collider,
                collision3D = collision,
                is2D = false,
            };
        }

        // Tells every enabled IImpactReceiver on the nearest object that has any, starting from the collider that was
        // hit and working up its parents (so a receiver can sit on a parent of the collider) - same as strikes
        protected virtual void NotifyImpactReceiver(ImpactData impactData)
        {
            if (impactData == null || impactData.target == null)
                return;

            Transform from = impactData.collider3D != null ? impactData.collider3D.transform : impactData.target.transform;
            for (Transform current = from; current != null; current = current.parent)
            {
                current.GetComponents(ReceiverBuffer);
                ReceiverBuffer.RemoveAll(receiver => receiver is Behaviour behaviour && !behaviour.isActiveAndEnabled);
                if (ReceiverBuffer.Count == 0)
                    continue;

                IImpactReceiver[] receivers = ReceiverBuffer.ToArray();
                ReceiverBuffer.Clear();
                foreach (IImpactReceiver receiver in receivers)
                    receiver.OnImpact(impactData);
                return;
            }
        }

        private static readonly System.Collections.Generic.List<IImpactReceiver> ReceiverBuffer = new();

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
