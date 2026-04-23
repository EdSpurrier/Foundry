using UnityEngine;

namespace Foundry.Triggers
{
    [RequireComponent(typeof(Collider))]
    public class VolumeTrigger3D : VolumeTriggerBase
    {
        protected virtual void Reset()
        {
            Collider triggerCollider = GetComponent<Collider>();

            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            HandleEnter(other);
        }

        protected virtual void OnTriggerExit(Collider other)
        {
            HandleExit(other);
        }

        protected override GameObject GetTrackedObject(Component other)
        {
            Collider collider = other as Collider;

            if (collider == null)
                return base.GetTrackedObject(other);

            if (collider.attachedRigidbody != null)
                return collider.attachedRigidbody.gameObject;

            return collider.transform.root.gameObject;
        }
    }
}