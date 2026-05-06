using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Actions
{
    [System.Serializable]
    public class CameraSetTarget : CameraActionBase
    {
        public override string ActionType => "Camera Target";

        [HideLabel]
        [SuffixLabel("Target", Overlay = true)]
        public Transform target;

        [ToggleLeft]
        public bool snap;

        protected override void Activate()
        {
            if (!TryGetCamera(out var cam))
                return;

            if (target == null)
            {
                Debug.LogWarning($"CameraSetTarget >> No target assigned on action: {actionName}");
                return;
            }

            cam.SetTarget(target, snap);
        }
    }
}