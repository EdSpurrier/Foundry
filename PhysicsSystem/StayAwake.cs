using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.PhysicsSystem
{
    // Keeps a Rigidbody awake so it reacts the moment what it rests on goes away - a support switched off, broken or
    // despawned (e.g. a log resting on a pillar the chicken pecks apart). Unity doesn't wake a sleeping body when a
    // collider under it is disabled or destroyed, so without this the body hangs in mid-air.
    // It can go back to sleep once it has fallen and come to rest, so it costs nothing after that.
    [RequireComponent(typeof(Rigidbody))]
    public class StayAwake : MonoBehaviour
    {
        [Title("Stay Awake")]
        [Tooltip("Let it sleep again once it has moved and come to rest for Settle Time. Off (default) = it never sleeps, which costs very little for a few bodies and is the safe choice when it may land on something that later goes away too (e.g. debris that shrinks away).")]
        [SerializeField] private bool sleepOnceSettled;

        [ShowIf(nameof(sleepOnceSettled))]
        [Tooltip("It counts as having moved once it goes faster than this (m/s, or rad/s for spin).")]
        [SuffixLabel("m/s", Overlay = true)]
        [SerializeField, Min(0.01f)] private float movedSpeed = 0.5f;

        [ShowIf(nameof(sleepOnceSettled))]
        [Tooltip("At rest: slower than this (m/s, and rad/s for spin)...")]
        [SuffixLabel("m/s", Overlay = true)]
        [SerializeField, Min(0.001f)] private float settleSpeed = 0.05f;

        [ShowIf(nameof(sleepOnceSettled))]
        [Tooltip("...for this long (s).")]
        [SuffixLabel("s", Overlay = true)]
        [SerializeField, Min(0f)] private float settleTime = 5f;

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private bool moved;

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private bool settled;

        private Rigidbody _body;
        private float _normalSleepThreshold;
        private float _restTime;

        public bool Settled => settled;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _normalSleepThreshold = _body.sleepThreshold;
        }

        private void OnEnable()
        {
            moved = false;
            settled = false;
            _restTime = 0f;
            _body.sleepThreshold = 0f;
            _body.WakeUp();
        }

        private void OnDisable()
        {
            if (_body != null)
                _body.sleepThreshold = _normalSleepThreshold;
        }

        private void FixedUpdate()
        {
            if (!sleepOnceSettled || settled || _body.isKinematic)
                return;

            float speed = Mathf.Max(_body.linearVelocity.magnitude, _body.angularVelocity.magnitude);
            if (!moved)
            {
                moved = speed > movedSpeed;
                return;
            }

            _restTime = speed < settleSpeed ? _restTime + Time.fixedDeltaTime : 0f;
            if (_restTime < settleTime)
                return;

            settled = true;
            _body.sleepThreshold = _normalSleepThreshold;
        }
    }
}
