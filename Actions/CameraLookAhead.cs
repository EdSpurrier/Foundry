using Sirenix.OdinInspector;

namespace Foundry.Actions
{
    [System.Serializable]
    public class CameraLookAhead : CameraActionBase
    {
        public override string ActionType => "Camera Look Ahead";

        [ToggleLeft]
        public bool enabled = true;

        protected override void Activate()
        {
            if (!TryGetCamera(out var cam))
                return;

            cam.SetLookAhead(enabled);
        }
    }
}