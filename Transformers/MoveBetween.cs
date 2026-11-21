using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Transformers
{
    // Slides an object between where it starts (closed) and an offset from there (open): a door, a gate, a drawbridge,
    // a lift. Activate() opens it, Deactivate() closes it - from wherever it is, so it can turn round half-way. Use it
    // from any event with the Transformer action (Action State Update on = open, off = close), e.g. an Interactable's
    // On Turned On / On Turned Off.
    public class MoveBetween : MonoBehaviour, ITransformerAction
    {
        [Tooltip("What moves. Empty = this object.")]
        [SerializeField] private Transform target;

        [Tooltip("How far it moves to open, in its parent's space (e.g. 0, 4, 0 = up 4 m).")]
        [SerializeField] private Vector3 openOffset = new(0f, 3f, 0f);

        [SuffixLabel("s", Overlay = true)]
        [SerializeField, Min(0.01f)] private float openTime = 0.4f;

        [SuffixLabel("s", Overlay = true)]
        [SerializeField, Min(0.01f)] private float closeTime = 0.6f;

        [Tooltip("Start open.")]
        [SerializeField] private bool startOpen;

        [Title("Events")]
        [SerializeField, HideLabel] private FrameCoreEvent onOpened = new() { eventName = "Move Between - Opened" };
        [SerializeField, HideLabel] private FrameCoreEvent onClosed = new() { eventName = "Move Between - Closed" };

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly, Range(0f, 1f)] private float amountOpen;

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private bool opening;

        private Vector3 _closedPosition;
        private bool _ready;
        private bool _settled = true;

        private Transform Target => target != null ? target : transform;
        public bool IsOpen => amountOpen >= 1f;
        public bool IsClosed => amountOpen <= 0f;

        private void Awake()
        {
            _closedPosition = Target.localPosition;
            _ready = true;
            opening = startOpen;
            amountOpen = startOpen ? 1f : 0f;
            Apply();
        }

        [Button("Open")]
        public void Activate()
        {
            opening = true;
            _settled = false;
        }

        [Button("Close")]
        public void Deactivate()
        {
            opening = false;
            _settled = false;
        }

        public void Toggle()
        {
            if (opening) Deactivate(); else Activate();
        }

        private void Update()
        {
            if (!_ready || _settled) return;

            float goal = opening ? 1f : 0f;
            float time = opening ? openTime : closeTime;
            amountOpen = Mathf.MoveTowards(amountOpen, goal, Time.deltaTime / time);
            Apply();

            if (Mathf.Approximately(amountOpen, goal))
            {
                _settled = true;
                if (opening) onOpened?.Activate();
                else onClosed?.Activate();
            }
        }

        private void Apply()
        {
            Target.localPosition = _closedPosition + openOffset * Mathf.SmoothStep(0f, 1f, amountOpen);
        }

        private void OnDrawGizmosSelected()
        {
            Transform t = Target;
            Vector3 closed = Application.isPlaying && _ready
                ? (t.parent != null ? t.parent.TransformPoint(_closedPosition) : _closedPosition)
                : t.position;
            Vector3 open = closed + (t.parent != null ? t.parent.TransformVector(openOffset) : openOffset);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(closed, open);
            Gizmos.DrawWireSphere(open, 0.15f);
        }
    }
}
