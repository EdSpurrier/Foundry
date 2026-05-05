using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Common
{
    [System.Serializable]
    public class Cooldown
    {
        [FoldoutGroup("Cooldown")]
        public enum TimeMode
        {
            Scaled,
            Unscaled
        }

        [HorizontalGroup("Cooldown/Split", 0.5f)]
        [HideLabel]
        [SuffixLabel("duration", true)]
        [SerializeField] private float duration = 0.5f;
        [HorizontalGroup("Cooldown/Split", 0.5f)]
        [HideLabel]
        [SerializeField] private TimeMode timeMode = TimeMode.Scaled;

        private float lastUseTime = -999f;

        private float CurrentTime =>
            timeMode == TimeMode.Unscaled
                ? Time.unscaledTime
                : Time.time;

        public bool Ready => CurrentTime >= lastUseTime + duration;

        public bool TryUse()
        {
            if (!Ready)
                return false;

            lastUseTime = CurrentTime;
            return true;
        }

        public void Reset()
        {
            lastUseTime = CurrentTime;
        }

        public void Clear()
        {
            lastUseTime = -999f;
        }
    }
}