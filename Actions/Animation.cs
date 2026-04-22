using Foundry.Common;
using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Actions
{
    [System.Serializable]
    public class Animation : FrameAction
    {
        public override string ActionType => "Animation";

        
        [HorizontalGroup("Animatior", 0.35f)]
        [HideLabel]
        public Animator animator;

        [EnumPaging]
        [EnumToggleButtons]
        [HorizontalGroup("Animatior", 0.65f)]
        [HideLabel]
        public AnimationTriggerType triggerType;

        [HorizontalGroup("Parameters", 0.60f)]
        [HideLabel]
        [SuffixLabel("Parameter", Overlay = true)]
        public string parameterName;

        [ShowIf(nameof(triggerType), AnimationTriggerType.Bool)]
        [HorizontalGroup("Parameters", 0.40f)]
        [HideLabel]
        public bool boolValue = true;

        [ShowIf(nameof(triggerType), AnimationTriggerType.Int)]
        [HorizontalGroup("Parameters", 0.40f)]
        [HideLabel]
        public int intValue = 0;

        [ShowIf(nameof(triggerType), AnimationTriggerType.Float)]
        [HorizontalGroup("Parameters", 0.40f)]
        [HideLabel]
        public float floatValue = 0f;

        protected override void Activate()
        {
            if (animator == null)
            {
                Debug.LogWarning($"Action: Animation >> No Animator assigned on action: {actionName}");
                return;
            }

            if (string.IsNullOrWhiteSpace(parameterName))
            {
                Debug.LogWarning($"Action: Animation >> No parameter name assigned on action: {actionName}");
                return;
            }

            switch (triggerType)
            {
                case AnimationTriggerType.Bool:
                    animator.SetBool(parameterName, boolValue);
                    break;

                case AnimationTriggerType.Int:
                    animator.SetInteger(parameterName, intValue);
                    break;

                case AnimationTriggerType.Float:
                    animator.SetFloat(parameterName, floatValue);
                    break;

                case AnimationTriggerType.Trigger:
                    animator.SetTrigger(parameterName);
                    break;
            }
        }
    }
}