using System.Collections.Generic;
using FrameCoreU.Events;
using FrameCoreU.Unity;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Actions
{
    // Pool-aware counterpart to Destroy: returns each object to the pool it was spawned from, or destroys it if
    // it didn't come from a pool. Use this (not Destroy) for anything created by Spawn or SpawnObject.
    [System.Serializable]
    public class Despawn : FrameAction
    {
        public override string ActionType => "Despawn";

        [Title("Despawn Settings")]
        [ListDrawerSettings(DefaultExpandedState = true)]
        public List<GameObject> gameObjects = new();

        [Tooltip("Seconds to wait before despawning. 0 = immediately.")]
        [Min(0f)]
        public float delay;

        protected override void Activate()
        {
            if (gameObjects == null || gameObjects.Count == 0)
            {
                Debug.LogWarning($"[ActionDespawn] No GameObjects set - {actionName}");
                return;
            }

            foreach (GameObject target in gameObjects)
            {
                if (target == null) continue;
                target.Despawn(delay);
            }
        }
    }
}
