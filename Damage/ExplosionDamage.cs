using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Damage
{
    public class ExplosionDamage : MonoBehaviour
    {
        [FoldoutGroup("Settings")]
        [SerializeField] private bool active = true;

        [FoldoutGroup("Settings/Damage")]
        [SerializeField] private int damage = 50;

        [FoldoutGroup("Settings/Damage")]
        [SerializeField] private float radius = 5f;

        [FoldoutGroup("Settings/Damage")]
        [SerializeField] private bool useFalloff = true;

        [FoldoutGroup("Settings/Damage")]
        [SerializeField] private LayerMask damageMask = ~0;

        [FoldoutGroup("Debug")]
        [SerializeField] private bool drawDebug = true;

        private readonly Collider[] hits = new Collider[32];

        [Button("Explode")]
        public void Explode()
        {
            if (!active)
                return;

            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                radius,
                hits,
                damageMask
            );

            for (int i = 0; i < count; i++)
            {
                Collider hit = hits[i];

                if (hit == null)
                    continue;

                IDamageReceiver receiver = hit.GetComponentInParent<IDamageReceiver>();

                if (receiver == null)
                    continue;

                Vector3 closestPoint = hit.ClosestPoint(transform.position);
                float distance = Vector3.Distance(transform.position, closestPoint);
                float falloff = useFalloff ? Mathf.Clamp01(1f - distance / radius) : 1f;

                int finalDamage = Mathf.RoundToInt(damage * falloff);

                if (finalDamage <= 0)
                    continue;

                DamageData damageData = new DamageData
                {
                    amount = finalDamage,
                    damageType = DamageType.Explosion,
                    source = gameObject,
                    instigator = gameObject,
                    target = hit.gameObject,
                    point = closestPoint,
                    direction = (hit.transform.position - transform.position).normalized,
                    force = falloff
                };

                receiver.ApplyDamage(damageData);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!drawDebug)
                return;

            Gizmos.DrawWireSphere(transform.position, radius);
        }
#endif
    }
}