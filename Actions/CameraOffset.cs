using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Actions
{
    [System.Serializable]
    public class CameraOffset : CameraActionBase
    {
        public override string ActionType => "Camera Offset";

        [HideLabel]
        [SuffixLabel("Offset", Overlay = true)]
        public Vector3 offset = new(0f, 1f, -10f);

        protected override void Activate()
        {
            if (!TryGetCamera(out var cam))
                return;

            cam.SetOffset(offset);
        }
    }
}