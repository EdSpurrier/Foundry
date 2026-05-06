using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Actions
{
    [System.Serializable]
    public class CameraFocusTemporary : CameraActionBase
    {
        public override string ActionType => "Camera Focus Temporary";

        [HideLabel]
        [SuffixLabel("Focus Target", Overlay = true)]
        public Transform target;

        [MinValue(0f)]
        [SuffixLabel("Duration", Overlay = true)]
        public float duration = 1f;

        [ToggleLeft]
        public bool snap;

        [ToggleLeft]
        public bool disableLookAhead = true;

        [ToggleLeft]
        public bool restorePreviousState = true;

        [Title("Optional Overrides")]
        [ToggleLeft]
        public bool overrideOffset;

        [ShowIf(nameof(overrideOffset))]
        public Vector3 focusOffset = new(0f, 1f, -10f);

        [ToggleLeft]
        public bool overrideZoom;

        [ShowIf(nameof(overrideZoom))]
        public float focusZoom = 5f;

        protected override void Activate()
        {
            if (!TryGetCamera(out var cam))
                return;

            if (target == null)
            {
                Debug.LogWarning($"CameraFocusTemporary >> No target assigned on action: {actionName}");
                return;
            }

            float? zoomOverride = overrideZoom ? focusZoom : null;

            if (overrideOffset || overrideZoom)
            {
                Vector3 offsetToUse = overrideOffset ? focusOffset : cam.MainRig.Offset;

                cam.FocusTemporary(
                    target,
                    offsetToUse,
                    zoomOverride,
                    duration,
                    snap,
                    disableLookAhead,
                    restorePreviousState);
            }
            else
            {
                cam.FocusTemporary(
                    target,
                    duration,
                    snap,
                    disableLookAhead,
                    restorePreviousState);
            }
        }
    }
}