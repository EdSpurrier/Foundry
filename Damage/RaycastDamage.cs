using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Damage
{
    public class RaycastDamage : MonoBehaviour
    {
        [FoldoutGroup("Settings")]
        [SerializeField] private bool active = true;

        [FoldoutGroup("Settings/Raycast")]
        [SerializeField] private Transform rayOrigin;

        [FoldoutGroup("Settings/Raycast")]
        [SerializeField] private float range = 100f;

        [FoldoutGroup("Settings/Raycast")]
        [SerializeField] private LayerMask hitMask = ~0;

        [FoldoutGroup("Settings/Damage")]
        [SerializeField] private int damage = 25;

        [FoldoutGroup("Settings/Physics")]
        [SerializeField] private bool applyForce = true;

        [FoldoutGroup("Settings/Physics")]
        [SerializeField] private float force = 10f;

        [FoldoutGroup("Debug")]
        [SerializeField] private bool drawDebug = true;

        private void Awake()
        {
            if (rayOrigin == null)
                rayOrigin = transform;
        }

        [Button("Fire")]
        public void Fire()
        {
            if (!active || rayOrigin == null)
                return;

            Vector3 origin = rayOrigin.position;
            Vector3 direction = rayOrigin.forward;

            if (!Physics.Raycast(origin, direction, out RaycastHit hit, range, hitMask))
            {
                if (drawDebug)
                    Debug.DrawRay(origin, direction * range, Color.white, 1f);

                return;
            }

            if (drawDebug)
                Debug.DrawLine(origin, hit.point, Color.red, 1f);

            IDamageReceiver receiver = hit.collider.GetComponentInParent<IDamageReceiver>();

            if (receiver != null)
            {
                DamageData damageData = new DamageData
                {
                    amount = damage,
                    damageType = DamageType.Bullet,
                    source = gameObject,
                    instigator = gameObject,
                    target = hit.collider.gameObject,
                    point = hit.point,
                    direction = direction,
                    normal = hit.normal,
                    force = force
                };

                receiver.ApplyDamage(damageData);
            }

            if (applyForce && hit.rigidbody != null)
                hit.rigidbody.AddForceAtPosition(direction * force, hit.point, ForceMode.Impulse);
        }
    }
}