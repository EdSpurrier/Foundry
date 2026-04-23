using System.Collections.Generic;
using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Triggers
{
    public abstract class VolumeTriggerBase : MonoBehaviour
    {
        [Title("Settings")]
        [SerializeField] protected bool active = true;

        [Title("Filter")]
        [SerializeField] protected LayerMask detectionMask = ~0;

        [BoxGroup("Threshold")]
        [SerializeField] protected bool useThreshold = false;

        [BoxGroup("Threshold")]
        [ShowIf(nameof(useThreshold))]
        [MinValue(1)]
        [SuffixLabel("Required Count", Overlay = true)]
        [SerializeField] protected int thresholdCount = 1;

        [ShowIf(nameof(useThreshold))]
        [BoxGroup("Threshold")]
        [SerializeField] protected bool triggerOnThresholdReached = true;

        [ShowIf(nameof(triggerOnThresholdReached))]
        [FoldoutGroup("Threshold/On Threshold Reached")]
        [HideLabel]
        [SerializeField] protected FrameCoreEvent onThresholdReached;

        [ShowIf(nameof(useThreshold))]
        [BoxGroup("Threshold")]
        [SerializeField] protected bool triggerOnThresholdLost = true;

        [ShowIf(nameof(triggerOnThresholdLost))]
        [FoldoutGroup("Threshold/On Threshold Lost")]
        [HideLabel]
        [SerializeField] protected FrameCoreEvent onThresholdLost;

        [BoxGroup("Events")]
        [SerializeField] protected bool triggerOnEnter = true;

        [ShowIf(nameof(triggerOnEnter))]
        [FoldoutGroup("Events/Enter Trigger")]
        [HideLabel]
        [SerializeField] protected FrameCoreEvent onEnter;

        [BoxGroup("Events")]
        [SerializeField] protected bool triggerOnExit = true;

        [ShowIf(nameof(triggerOnExit))]
        [FoldoutGroup("Events/Exit Trigger")]
        [HideLabel]
        [SerializeField] protected FrameCoreEvent onExit;

        [Title("System")]
        [ReadOnly]
        [SerializeField] protected int trackedCount = 0;

        [ReadOnly]
        [SerializeField] protected List<GameObject> trackedObjectsDebug = new();

        protected readonly HashSet<GameObject> trackedObjects = new();

        protected virtual void Awake()
        {
            RefreshTrackedState();
        }

        protected virtual void OnDisable()
        {
            trackedObjects.Clear();
            RefreshTrackedState();
        }

        protected void HandleEnter(Component other)
        {
            if (!active)
                return;

            if (!PassesFilter(other))
                return;

            GameObject target = GetTrackedObject(other);

            if (target == null)
                return;

            bool wasAtOrAboveThreshold = IsAtOrAboveThreshold();
            bool added = trackedObjects.Add(target);

            if (!added)
                return;

            RefreshTrackedState();

            Enter(target, other);
            if (triggerOnEnter)
            {
                onEnter?.Activate();
            }

            if (useThreshold && triggerOnThresholdReached)
            {
                bool isAtOrAboveThreshold = IsAtOrAboveThreshold();

                if (!wasAtOrAboveThreshold && isAtOrAboveThreshold)
                {
                    ThresholdReached();
                    onThresholdReached?.Activate();
                }
            }
        }

        protected void HandleExit(Component other)
        {
            if (!active)
                return;

            if (!PassesFilter(other))
                return;

            GameObject target = GetTrackedObject(other);

            if (target == null)
                return;

            bool wasAtOrAboveThreshold = IsAtOrAboveThreshold();
            bool removed = trackedObjects.Remove(target);

            if (!removed)
                return;

            RefreshTrackedState();

            Exit(target, other);
            if (triggerOnExit)
            {
                onExit?.Activate();
            }

            if (useThreshold && triggerOnThresholdLost)
            {
                bool isAtOrAboveThreshold = IsAtOrAboveThreshold();

                if (wasAtOrAboveThreshold && !isAtOrAboveThreshold)
                {
                    ThresholdLost();
                    onThresholdLost?.Activate();
                }
            }
        }

        protected virtual bool PassesFilter(Component other)
        {
            return other != null && ((1 << other.gameObject.layer) & detectionMask.value) != 0;
        }

        protected virtual GameObject GetTrackedObject(Component other)
        {
            if (other == null)
                return null;

            return other.transform.root.gameObject;
        }

        protected virtual void Enter(GameObject target, Component other)
        {
        }

        protected virtual void Exit(GameObject target, Component other)
        {
        }

        protected virtual void ThresholdReached()
        {
        }

        protected virtual void ThresholdLost()
        {
        }

        protected bool IsAtOrAboveThreshold()
        {
            if (!useThreshold)
                return false;

            return trackedObjects.Count >= thresholdCount;
        }

        public int GetTrackedCount()
        {
            return trackedObjects.Count;
        }

        public bool Contains(GameObject target)
        {
            return target != null && trackedObjects.Contains(target);
        }

        public void Activate()
        {
            active = true;
        }

        public void Deactivate()
        {
            active = false;
        }

        protected void RefreshTrackedState()
        {
            trackedCount = trackedObjects.Count;
            RefreshDebugList();
        }

        protected void RefreshDebugList()
        {
            trackedObjectsDebug.Clear();

            foreach (GameObject trackedObject in trackedObjects)
            {
                if (trackedObject != null)
                {
                    trackedObjectsDebug.Add(trackedObject);
                }
            }
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (thresholdCount < 1)
            {
                thresholdCount = 1;
            }
        }
#endif
    }
}