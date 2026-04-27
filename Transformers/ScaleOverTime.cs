using System.Collections.Generic;
using FrameCoreU.Events;
using FrameCoreU.Timing;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Transformers
{
    public enum ScaleCompleteAction
    {
        None,
        Deactivate,
        Destroy
    }

    public class ScaleOverTime : MonoBehaviour, ITransformerAction
    {
        [Title("Settings")]
        [SerializeField] private bool active = true;

        [Title("Performance")]
        [HideLabel]
        [SerializeField] private TickRateLimiter tickRate = new();

        [Title("Targets")]
        [Tooltip("All objects in this list will scale at the same time.")]
        [SerializeField] private List<GameObject> targets = new();

        [Title("Scale")]
        [SerializeField] private bool useCurrentScaleAsStart = true;

        [HideIf(nameof(useCurrentScaleAsStart))]
        [SerializeField] private Vector3 startScale = Vector3.one;

        [SerializeField] private Vector3 endScale = Vector3.zero;

        [Title("Timing")]
        [MinValue(0.01f)]
        [SerializeField] private float duration = 0.25f;

        [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Title("Complete")]
        [SerializeField] private ScaleCompleteAction completeAction = ScaleCompleteAction.None;

        [SerializeField] private FrameCoreEvent onComplete;

        [Title("System")]
        [SerializeField, ReadOnly] private bool isScaling;
        [SerializeField, ReadOnly] private float timer;

        private readonly List<Transform> validTargets = new();
        private readonly List<Vector3> fromScales = new();

        public IReadOnlyList<GameObject> Targets => targets;
        public float Duration => duration;
        public Vector3 EndScale => endScale;
        public bool IsScaling => isScaling;

        [Button("Activate Scale")]
        public void Activate()
        {
            if (isScaling)
                return;

            active = true;

            EnsureTargets();
            CacheTargets();

            if (validTargets.Count == 0)
                return;

            timer = 0f;
            isScaling = true;
            tickRate?.Reset();

            ApplyScale(0f);
        }

        public void Deactivate()
        {
            active = false;
            Stop();
        }

        public void Stop()
        {
            isScaling = false;
            timer = 0f;
        }

        private void Update()
        {
            if (!active || !isScaling)
                return;

            timer += Time.deltaTime;

            if (timer >= duration)
            {
                FinishScale();
                return;
            }

            if (tickRate != null && !tickRate.CanTick())
                return;

            float t = Mathf.Clamp01(timer / duration);
            ApplyScale(t);
        }

        private void ApplyScale(float t)
        {
            float curvedT = curve != null ? curve.Evaluate(t) : t;

            for (int i = 0; i < validTargets.Count; i++)
            {
                if (validTargets[i] == null)
                    continue;

                validTargets[i].localScale =
                    Vector3.LerpUnclamped(fromScales[i], endScale, curvedT);
            }
        }

        private void FinishScale()
        {
            ApplyFinalScale();

            isScaling = false;
            timer = 0f;

            onComplete?.Activate();

            Complete(validTargets);
        }

        private void ApplyFinalScale()
        {
            foreach (Transform target in validTargets)
            {
                if (target != null)
                    target.localScale = endScale;
            }
        }

        private void CacheTargets()
        {
            validTargets.Clear();
            fromScales.Clear();

            foreach (GameObject target in targets)
            {
                if (target == null)
                    continue;

                Transform targetTransform = target.transform;

                validTargets.Add(targetTransform);
                fromScales.Add(useCurrentScaleAsStart ? targetTransform.localScale : startScale);
            }
        }

        private void Complete(List<Transform> completedTargets)
        {
            foreach (Transform target in completedTargets)
            {
                if (target == null)
                    continue;

                switch (completeAction)
                {
                    case ScaleCompleteAction.Deactivate:
                        target.gameObject.SetActive(false);
                        break;

                    case ScaleCompleteAction.Destroy:
                        Destroy(target.gameObject);
                        break;
                }
            }
        }

        private void EnsureTargets()
        {
            targets ??= new List<GameObject>();
            targets.RemoveAll(target => target == null);

            if (targets.Count == 0)
                targets.Add(gameObject);
        }

        private void OnValidate()
        {
            if (duration <= 0f)
                duration = 0.01f;

            targets ??= new List<GameObject>();

            if (targets.Count == 0)
                targets.Add(gameObject);
        }
    }
}