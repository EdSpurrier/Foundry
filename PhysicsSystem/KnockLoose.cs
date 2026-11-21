using System.Collections;
using System.Collections.Generic;
using Foundry.Data;
using Foundry.Interaction;
using Foundry.Triggers;
using FrameCoreU.Events;
using FrameCoreU.Unity;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.PhysicsSystem
{
    // Something held in place until it's hit hard enough, then it drops: a loose roof tile, a wobbly brick, a shelf
    // plank. It's kinematic until then. An impact (anything with an ImpactTrigger - e.g. an egg) or a strike (a peck)
    // knocks it loose: it turns physical with Knock Velocity and a spin, ignores Pass Through Layers for a moment so it
    // can drop clear of the things around it (the neighbouring tiles), then - if Remove After is set - shrinks away.
    // Takes With knocks other loose things down with it.
    [RequireComponent(typeof(Rigidbody))]
    public class KnockLoose : MonoBehaviour, IImpactReceiver, IStrikeReceiver
    {
        [Title("Knocking It")]
        [Tooltip("Hits at least this hard (impact speed, m/s) knock it loose. 0 = any hit.")]
        [SerializeField, Min(0f)] private float minForce = 1.5f;

        [SerializeField] private bool byImpacts = true;
        [SerializeField] private bool byStrikes = true;

        [Tooltip("Other loose things that come down with it.")]
        [SerializeField] private List<KnockLoose> takesWith = new();

        [Title("Falling")]
        [Tooltip("Velocity (m/s, world) it gets when knocked loose.")]
        [SerializeField] private Vector3 knockVelocity = new(0f, -2f, 0f);

        [Tooltip("Spin (degrees/s) - the direction depends on which side it was hit.")]
        [SerializeField, Min(0f)] private float spin = 90f;

        [Tooltip("Ignores these layers for Pass Through Time after it's knocked, to fall clear of what's around it.")]
        [SerializeField] private LayerMask passThroughLayers;

        [SuffixLabel("s", Overlay = true)]
        [SerializeField, Min(0f)] private float passThroughTime = 0.4f;

        [Tooltip("Shrinks away and is removed this long after it's knocked loose. 0 = it stays.")]
        [SuffixLabel("s", Overlay = true)]
        [SerializeField, Min(0f)] private float removeAfter = 3f;

        [ShowIf("@removeAfter > 0")]
        [SuffixLabel("s", Overlay = true)]
        [SerializeField, Min(0.01f)] private float shrinkTime = 0.4f;

        [Title("Events")]
        [SerializeField, HideLabel] private FrameCoreEvent onKnocked = new() { eventName = "Knock Loose - Knocked" };

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private bool loose;

        public bool IsLoose => loose;
        public event System.Action<KnockLoose> Knocked;

        private Rigidbody _body;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
        }

        public void OnImpact(ImpactData impactData)
        {
            if (!byImpacts || loose || impactData == null || impactData.force < minForce) return;
            Knock(SpinSign(impactData.point));
        }

        public void OnStrike(in StrikeData strike)
        {
            if (!byStrikes || loose) return;
            Knock(SpinSign(strike.Point));
        }

        public void Knock() => Knock(Random.value < 0.5f ? -1f : 1f);

        private void Knock(float spinSign)
        {
            if (loose) return;
            loose = true;
            _body.isKinematic = false;
            _body.useGravity = true;
            _body.linearVelocity = knockVelocity;
            _body.angularVelocity = new Vector3(0f, 0f, spin * Mathf.Deg2Rad * spinSign);
            if (passThroughLayers.value != 0 && passThroughTime > 0f) StartCoroutine(PassThrough());
            if (removeAfter > 0f) StartCoroutine(Remove());
            onKnocked?.Activate();
            Knocked?.Invoke(this);
            foreach (KnockLoose other in takesWith)
                if (other != null && other != this)
                    other.Knock();
        }

        // hit left of the middle spins it one way, right the other (in X, the 2.5D plane)
        private float SpinSign(Vector3 point) => point.x < transform.position.x ? -1f : 1f;

        private IEnumerator PassThrough()
        {
            var colliders = GetComponentsInChildren<Collider>();
            var before = new LayerMask[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
            {
                before[i] = colliders[i].excludeLayers;
                colliders[i].excludeLayers = before[i] | passThroughLayers;
            }
            yield return new WaitForSeconds(passThroughTime);
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null)
                    colliders[i].excludeLayers = before[i];
        }

        private IEnumerator Remove()
        {
            yield return new WaitForSeconds(removeAfter);
            Vector3 from = transform.localScale;
            for (float t = 0f; t < 1f; t += Time.deltaTime / shrinkTime)
            {
                transform.localScale = from * (1f - t * t);
                yield return null;
            }
            transform.localScale = Vector3.zero;
            gameObject.Despawn();
        }
    }
}
