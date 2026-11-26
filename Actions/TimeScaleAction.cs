using Foundry.Timing;
using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Actions
{
    // Slow motion and hit-stop from an event (see Foundry.Timing.TimeScale):
    //  - Slow: ease time down to Scale, hold for Duration real seconds (0 = until a Restore), ease back.
    //  - Hit Stop: snap to Scale (usually 0) for Duration real seconds - a freeze-frame on a big impact.
    //  - Restore: ease back the last slow-down a Time Scale action started (or every slow-down, with All).
    [System.Serializable]
    public class TimeScaleAction : FrameAction
    {
        public enum Mode { Slow, HitStop, Restore }

        public override string ActionType => "Time Scale";

        public Mode mode = Mode.Slow;

        [HideIf(nameof(mode), Mode.Restore)]
        [Tooltip("Time scale to go to: 0.3 = 30% speed, 0 = frozen.")]
        [Range(0f, 1f)] public float scale = 0.3f;

        [HideIf(nameof(mode), Mode.Restore)]
        [Tooltip("Real seconds to hold it (Slow: 0 = until a Restore action).")]
        [Min(0f)] public float duration = 1f;

        [ShowIf(nameof(mode), Mode.Slow)]
        [Tooltip("Real seconds to ease in and out.")]
        [Min(0f)] public float easeIn = 0.15f;

        [ShowIf(nameof(mode), Mode.Slow)]
        [Min(0f)] public float easeOut = 0.2f;

        [ShowIf(nameof(mode), Mode.Restore)]
        [Tooltip("Ease back every slow-down (any source), not just the ones Slow actions started.")]
        public bool all;

        // The last request a Slow / Hit Stop action started - what a Restore eases back
        private static TimeScaleRequest _lastStarted;

        protected override void Activate()
        {
            switch (mode)
            {
                case Mode.Slow:
                    _lastStarted = TimeScale.Slow(scale, easeIn, easeOut, duration, actionName);
                    break;
                case Mode.HitStop:
                    if (duration <= 0f)
                    {
                        LogWarning("[TimeScaleAction] Hit Stop needs a Duration above 0");
                        return;
                    }
                    _lastStarted = TimeScale.HitStop(duration, scale, actionName);
                    break;
                case Mode.Restore:
                    if (all) TimeScale.ReleaseAll();
                    else TimeScale.Release(_lastStarted);
                    break;
            }
        }
    }
}
