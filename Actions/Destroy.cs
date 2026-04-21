using System.Collections.Generic;
using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Actions
{
    [System.Serializable]
    public class Destroy : FrameAction
    {
        public override string ActionType => "Destroy";
        
        public enum DestroyType
        {
            GameObject,
            Component
        }

        [Title("Destroy Settings")]
        public DestroyType destroyType = DestroyType.GameObject;

        [ShowIf(nameof(destroyType), DestroyType.GameObject)]
        [ListDrawerSettings(DefaultExpandedState = true)]
        public List<GameObject> gameObjects = new();

        [ShowIf(nameof(destroyType), DestroyType.Component)]
        [ListDrawerSettings(DefaultExpandedState = true)]
        public List<Component> components = new();
        
        protected override void Activate()
        {
            switch (destroyType)
            {
                case DestroyType.GameObject:
                    DestroyGameObjects();
                    break;

                case DestroyType.Component:
                    DestroyComponents();
                    break;
            }
        }

        private void DestroyGameObjects()
        {
            if (gameObjects == null || gameObjects.Count == 0)
            {
                Debug.LogWarning($"[ActionDestroy] No GameObjects set - {actionName}");
                return;
            }

            foreach (GameObject target in gameObjects)
            {
                if (target == null) continue;
                DestroyTarget(target);
            }
        }

        private void DestroyComponents()
        {
            if (components == null || components.Count == 0)
            {
                Debug.LogWarning($"[ActionDestroy] No Components set - {actionName}");
                return;
            }

            foreach (Component target in components)
            {
                if (target == null) continue;
                DestroyTarget(target);
            }
        }

        private void DestroyTarget(Object target)
        {
            Object.Destroy(target);
        }
    }
}