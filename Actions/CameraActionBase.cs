using Foundry.CameraSystem;
using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Actions
{
    [System.Serializable]
    public abstract class CameraActionBase : FrameAction
    {
        [HideLabel]
        [SuffixLabel("Camera Core", Overlay = true)]
        public CameraCore cameraCore;

        protected CameraCore ResolveCameraCore()
        {
            if (cameraCore != null)
                return cameraCore;

            return Foundry.Camera;
        }

        protected bool TryGetCamera(out CameraCore resolvedCamera)
        {
            resolvedCamera = ResolveCameraCore();

            if (resolvedCamera == null)
            {
                Debug.LogWarning($"[{ActionType}] >> No CameraCore assigned or found via FoundryCore on action: {actionName}");
                return false;
            }

            return true;
        }
    }
}