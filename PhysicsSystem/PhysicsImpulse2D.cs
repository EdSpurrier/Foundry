using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.PhysicsSystem
{
    public class PhysicsImpulse2D : MonoBehaviour, IPhysicsAction
    {
        [Title("Settings")]
        [SerializeField] private bool active = true;

        [Title("Origin")]
        [SerializeField] private Transform origin;

        [Title("Filter")]
        [SerializeField] private LayerMask detectionMask = ~0;

        [Title("Radius")]
        [MinValue(0f)]
        [SerializeField] private float minRadius = 0f;

        [MinValue(0.01f)]
        [SerializeField] private float maxRadius = 5f;

        [Title("Force")]
        [SerializeField] private float force = 10f;
        [SerializeField] private ForceMode2D forceMode = ForceMode2D.Impulse;

        [Tooltip("Adds upward lift to the impulse direction. Useful for explosion pop.")]
        [SerializeField] private float upwardBias = 0f;

        [SerializeField] private bool resetVelocity = false;

        [Title("Options")]
        [SerializeField] private bool includeTriggers = false;

        [Title("Debug")]
        [SerializeField] private bool drawDebug = true;
        [SerializeField] private bool drawOnlyWhenSelected = true;
        [SerializeField] private Color minRadiusColor = Color.yellow;
        [SerializeField] private Color maxRadiusColor = Color.red;

        public Transform Origin => origin;
        public float MinRadius => minRadius;
        public float MaxRadius => maxRadius;
        public float Force => force;

        [Button("Activate Impulse")]
        public void Activate()
        {
            if (!active)
                return;

            Vector2 center = GetOriginPosition();

            bool previousHitTriggers = Physics2D.queriesHitTriggers;
            Physics2D.queriesHitTriggers = includeTriggers;

            Collider2D[] hits = Physics2D.OverlapCircleAll(center, maxRadius, detectionMask);

            Physics2D.queriesHitTriggers = previousHitTriggers;

            foreach (Collider2D hit in hits)
            {
                if (hit == null || hit.attachedRigidbody == null)
                    continue;

                ApplyImpulse(hit.attachedRigidbody, center);
            }
        }

        private void ApplyImpulse(Rigidbody2D rb, Vector2 center)
        {
            Vector2 targetPos = rb.worldCenterOfMass;
            Vector2 fromCenter = targetPos - center;

            float distance = fromCenter.magnitude;

            if (distance <= 0.001f)
                fromCenter = Vector2.up;

            Vector2 direction = fromCenter.normalized;

            if (upwardBias != 0f)
            {
                direction += Vector2.up * upwardBias;
                direction.Normalize();
            }

            float falloff = CalculateFalloff(distance);
            float finalForce = force * falloff;

            if (finalForce <= 0f)
                return;

            if (resetVelocity)
                rb.linearVelocity = Vector2.zero;

            rb.AddForce(direction * finalForce, forceMode);
            
            Debug.Log("Applying Impulse: direction:" + direction + ",  finalForce:" + finalForce + ", calculation:" + direction * finalForce +  ", forceMode:" + forceMode + ", rb:" +rb.gameObject.name );
        }

        private float CalculateFalloff(float distance)
        {
            if (distance <= minRadius)
                return 1f;

            if (distance >= maxRadius)
                return 0f;

            return 1f - Mathf.InverseLerp(minRadius, maxRadius, distance);
        }

        private Vector2 GetOriginPosition()
        {
            return origin != null ? origin.position : transform.position;
        }

        private void OnValidate()
        {
            if (origin == null)
                origin = transform;

            if (maxRadius < minRadius)
                maxRadius = minRadius + 0.01f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawDebug || drawOnlyWhenSelected)
                return;

            DrawRadiusGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebug)
                return;

            DrawRadiusGizmos();
        }

        private void DrawRadiusGizmos()
        {
            Vector2 center = GetOriginPosition();

            Gizmos.color = minRadiusColor;
            Gizmos.DrawWireSphere(center, minRadius);

            Gizmos.color = maxRadiusColor;
            Gizmos.DrawWireSphere(center, maxRadius);
        }
#endif
    }
}