using System.Collections.Generic;
using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Actions
{
    [System.Serializable]
    public class Deactivator : FrameAction
    {
        public override string ActionType => "Deactivate";
        
        [Title("Deactivate Settings")]
        [ListDrawerSettings(DefaultExpandedState = true)]
        public List<GameObject> gameObjects = new();
        
        protected override void Activate()
        {
            DeactivateGameObjects();
        }

        private void DeactivateGameObjects()
        {
            if (gameObjects == null || gameObjects.Count == 0)
            {
                Debug.LogWarning($"[ActionDeactivate] No GameObjects set - {actionName}");
                return;
            }

            foreach (GameObject target in gameObjects)
            {
                if (target == null) continue;
                target.SetActive(false);
            }
        }
    }
}