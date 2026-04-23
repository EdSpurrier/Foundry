using UnityEngine;

namespace Foundry.Triggers
{
    [RequireComponent(typeof(Collider2D))]
    public class VolumeTrigger2D : VolumeTriggerBase
    {
        protected virtual void Reset()
        {
            Collider2D triggerCollider = GetComponent<Collider2D>();

            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            HandleEnter(other);
        }

        protected virtual void OnTriggerExit2D(Collider2D other)
        {
            HandleExit(other);
        }

        protected override GameObject GetTrackedObject(Component other)
        {
            Collider2D collider = other as Collider2D;

            if (collider == null)
                return base.GetTrackedObject(other);

            if (collider.attachedRigidbody != null)
                return collider.attachedRigidbody.gameObject;

            return collider.transform.root.gameObject;
        }
    }
}