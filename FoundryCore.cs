using Foundry.CameraSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry
{
    [DefaultExecutionOrder(-400)]
    public class FoundryCore : MonoBehaviour
    {
        [Title("Cores")]
        [SerializeField] private CameraCore camera;

        public CameraCore Camera => camera;

        private bool systemError;

        private void Awake()
        {
            if (Foundry.Core != null && Foundry.Core != this)
            {
                Debug.LogError("FoundryCore [ERROR] >> More than one FoundryCore found in scene.");
                Destroy(gameObject);
                return;
            }

            Foundry.SetCore(this);

            CheckSetup();

            Debug.Log("FoundryCore Started...");
        }

        private void OnDestroy()
        {
            Foundry.ClearCore(this);
        }

        private void CheckSetup()
        {
            systemError = false;

            if (camera == null)
            {
                Debug.LogError("FoundryCore [ERROR] >> CameraCore is not attached.");
                systemError = true;
            }

            if (systemError)
            {
                Debug.LogError("FoundryCore incorrectly setup.");
                Debug.Break();
            }
        }
    }
}