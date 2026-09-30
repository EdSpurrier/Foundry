using System;
using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Digging
{
    // Something buried in a DiggableTerrain - a key, a bone, a lost egg. It's held in place (its Rigidbody kinematic)
    // until enough of the dirt around it has been dug away, then it's freed: it drops/rolls out under physics and
    // On Revealed fires. Place it inside the dirt's box.
    public class BuriedObject : MonoBehaviour
    {
        [Tooltip("The dirt it's buried in. Empty = found automatically (the DiggableTerrain whose box it's in).")]
        [SerializeField] private DiggableTerrain terrain;

        [Tooltip("How much of the dirt around it must be dug out to free it: 0.5 = half, 1 = all of it.")]
        [SerializeField, Range(0.05f, 1f)] private float revealAmount = 0.5f;

        [Tooltip("How far (m) around its position is checked - about its size.")]
        [SerializeField, Min(0.01f)] private float revealRadius = 0.3f;

        [Tooltip("Hold its Rigidbody still (kinematic) until it's revealed.")]
        [SerializeField] private bool freezeUntilRevealed = true;

        [SerializeField, HideLabel] private FrameCoreEvent onRevealed = new() { eventName = "Buried - Revealed" };

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private bool revealed;

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private float exposed;

        private Rigidbody _body;
        private bool _wasKinematic;

        public bool IsRevealed => revealed;
        public event Action<BuriedObject> Revealed;

        private void Start()
        {
            if (terrain == null)
            {
                foreach (DiggableTerrain candidate in FindObjectsByType<DiggableTerrain>())
                {
                    if (!candidate.Contains(transform.position)) continue;
                    terrain = candidate;
                    break;
                }
            }

            if (TryGetComponent(out _body))
            {
                _wasKinematic = _body.isKinematic;
                if (freezeUntilRevealed)
                    _body.isKinematic = true;
            }

            if (terrain == null)
            {
                Debug.LogWarning($"BuriedObject >> {name} isn't inside any DiggableTerrain - revealing it.", this);
                Reveal();
                return;
            }

            terrain.Dug += OnDug;
            CheckExposure();
        }

        private void OnDestroy()
        {
            if (terrain != null)
                terrain.Dug -= OnDug;
        }

        private void OnDug(Vector3 point, float radius)
        {
            if (revealed)
                return;

            // Only bites near it can uncover it
            if (Vector3.Distance(point, transform.position) <= radius + revealRadius * 2f)
                CheckExposure();
        }

        // Samples a ring of points around it (and its centre): the share that are dug out
        private void CheckExposure()
        {
            const int RING = 8;
            Vector3 center = transform.position;
            int open = terrain.IsSolidAt(center) ? 0 : 1;

            for (int s = 0; s < RING; s++)
            {
                float angle = s * Mathf.PI * 2f / RING;
                Vector3 point = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * revealRadius;
                if (!terrain.IsSolidAt(point)) open++;
            }

            exposed = open / (RING + 1f);
            if (exposed >= revealAmount)
                Reveal();
        }

        [Button]
        public void Reveal()
        {
            if (revealed)
                return;

            revealed = true;
            if (_body != null && freezeUntilRevealed)
                _body.isKinematic = _wasKinematic;

            onRevealed?.Activate();
            Revealed?.Invoke(this);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.75f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, revealRadius);
        }
#endif
    }
}
