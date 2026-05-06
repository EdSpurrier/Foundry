using Sirenix.OdinInspector;

namespace Foundry.Actions
{
    [System.Serializable]
    public class CameraReset : CameraActionBase
    {
        public override string ActionType => "Camera Reset";

        [ToggleLeft]
        public bool snap;

        protected override void Activate()
        {
            if (!TryGetCamera(out var cam))
                return;

            cam.ResetCamera(snap);
        }
    }
}