using Sirenix.OdinInspector;
using UnityEngine;
using FrameCoreU.Audio;
using FrameCoreU.Events;

namespace Foundry.Actions
{
    [System.Serializable]
    public class Sound : FrameAction
    {
        public override string ActionType => "Sound";
        
        [HideLabel]
        [SuffixLabel("Sound Bank", Overlay = true)]
        public SoundBank soundBank;

        [HorizontalGroup("Sound Select", 0.1f)]
        [Button("<<")]
        private void Previous()
        {
            Select(-1);
        }

        [HorizontalGroup("Sound Select", 0.15f)]
        [HideLabel]
        [SuffixLabel("Id", Overlay = true)]
        public int soundId = 0;

        [HorizontalGroup("Sound Select", 0.65f)]
        [ShowInInspector, ReadOnly]
        [HideLabel]
        public string soundName = "No Sound Selected...";

        [HorizontalGroup("Sound Select", 0.1f)]
        [Button(">>")]
        private void Next()
        {
            Select(1);
        }

        public override void Reset()
        {
        }

        protected override void Activate()
        {
            if (soundBank == null)
            {
                Debug.LogWarning($"Action_Sound >> No SoundBank assigned on action: {actionName}");
                return;
            }

            soundBank.Play(soundId);
        }

        private void Select(int direction)
        {
            if (soundBank == null || soundBank.soundPoints == null || soundBank.soundPoints.Count == 0)
            {
                soundId = 0;
                soundName = "No Sounds Found!";
                return;
            }

            int count = soundBank.soundPoints.Count;
            soundId += direction;

            if (soundId >= count)
                soundId = 0;
            else if (soundId < 0)
                soundId = count - 1;

            RefreshSoundName();
        }

        private void RefreshSoundName()
        {
            if (soundBank == null || soundBank.soundPoints == null || soundBank.soundPoints.Count == 0)
            {
                soundName = "No Sounds Found!";
                soundId = 0;
                return;
            }

            if (soundId < 0)
                soundId = 0;

            if (soundId >= soundBank.soundPoints.Count)
                soundId = soundBank.soundPoints.Count - 1;

            SoundPointData soundPoint = soundBank.soundPoints[soundId];
            soundName = soundPoint != null ? soundPoint.soundName : "Null Sound";
        }

#if UNITY_EDITOR
        public override void ValidateData()
        {
            base.ValidateData();
            RefreshSoundName();
        }
#endif
    }
}