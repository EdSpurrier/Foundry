using Sirenix.OdinInspector;

namespace Foundry.Actions
{
    [System.Serializable]
    public class CameraStopTemporaryFocus : CameraActionBase
    {
        public override string ActionType => "Camera Stop Temporary Focus";

        [ToggleLeft]
        public bool restorePreviousState = true;

        protected override void Activate()
        {
            if (!TryGetCamera(out var cam))
                return;

            cam.StopTemporaryFocus(restorePreviousState);
        }
    }
}