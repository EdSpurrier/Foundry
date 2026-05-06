using Sirenix.OdinInspector;

namespace Foundry.Actions
{
    [System.Serializable]
    public class CameraZoom : CameraActionBase
    {
        public override string ActionType => "Camera Zoom";

        [HideLabel]
        [SuffixLabel("Zoom", Overlay = true)]
        public float zoom = 5f;

        [ToggleLeft]
        public bool snap;

        protected override void Activate()
        {
            if (!TryGetCamera(out var cam))
                return;

            cam.SetZoom(zoom, snap);
        }
    }
}