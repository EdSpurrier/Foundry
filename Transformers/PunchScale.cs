using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Transformers
{
    // A quick squash-and-wobble on an object's scale, e.g. a fruit getting bitten, a button pressed, a jelly landed on.
    // Activate() plays it (again from the start if it's already playing); Deactivate() stops it and puts the scale back.
    // Use it from any event with the Transformer action.
    public class PunchScale : MonoBehaviour, ITransformerAction
    {
        [Tooltip("What squashes. Empty = this object.")]
        [SerializeField] private Transform target;

        [Tooltip("Change in scale at the peak, per axis: 0.2 = 20% bigger, -0.2 = 20% smaller. The default squashes it flatter and wider.")]
        [SerializeField] private Vector3 punch = new(0.18f, -0.22f, 0.18f);

        [SuffixLabel("s", Overlay = true)]
        [SerializeField, Min(0.05f)] private float duration = 0.4f;

        [Tooltip("How many times it springs back past its normal shape before settling.")]
        [SerializeField, Range(0, 5)] private int wobbles = 2;

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private bool playing;

        private Vector3 _baseScale;
        private bool _hasBase;
        private float _time;

        private Transform Target => target != null ? target : transform;

        private void Awake() => CaptureBase();

        private void CaptureBase()
        {
            if (_hasBase) return;
            _baseScale = Target.localScale;
            _hasBase = true;
        }

        [Button("Play")]
        public void Activate()
        {
            CaptureBase();
            _time = 0f;
            playing = true;
        }

        public void Deactivate()
        {
            playing = false;
            if (_hasBase)
                Target.localScale = _baseScale;
        }

        private void Update()
        {
            if (!playing) return;

            _time += Time.deltaTime;
            float t = Mathf.Clamp01(_time / duration);
            float envelope = (1f - t) * (1f - t);
            float wave = Mathf.Sin(t * Mathf.PI * (1 + 2 * wobbles));
            Vector3 k = punch * (wave * envelope);
            Target.localScale = Vector3.Scale(_baseScale, Vector3.one + k);

            if (t >= 1f)
                Deactivate();
        }

        private void OnDisable()
        {
            if (playing) Deactivate();
        }
    }
}
