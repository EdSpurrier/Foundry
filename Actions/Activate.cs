using System.Collections.Generic;
using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Actions
{
    [System.Serializable]
    public class Activator : FrameAction
    {
        public override string ActionType => "Activate";
        
        [Title("Activate Settings")]
        [ListDrawerSettings(DefaultExpandedState = true)]
        public List<GameObject> gameObjects = new();
        
        protected override void Activate()
        {
            ActivateGameObjects();
        }

        private void ActivateGameObjects()
        {
            if (gameObjects == null || gameObjects.Count == 0)
            {
                Debug.LogWarning($"[ActionActivate] No GameObjects set - {actionName}");
                return;
            }

            foreach (GameObject target in gameObjects)
            {
                if (target == null) continue;
                 target.SetActive(true);
            }
        }
    }
}