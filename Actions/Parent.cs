using Sirenix.OdinInspector;
using System.Collections.Generic;
using FrameCoreU.Events;
using UnityEngine;

namespace Foundry.Actions
{
    [System.Serializable]
    public class Parent : FrameAction
    {
        public override string ActionType => "Parent";

        [Title("Targets")]
        [Required]
        public List<Transform> targets = new();

        [Title("Parent Settings")]
        [Required]
        public Transform newParent;

        [ToggleLeft]
        public bool worldPositionStays = true;


        protected override void Activate()
        {
            if (targets == null || targets.Count == 0)
            {
                Debug.LogWarning("Action: Parent >> No targets assigned.");
                return;
            }

            if (newParent == null)
            {
                Debug.LogWarning("Action: Parent >> No parent assigned.");
                return;
            }

            foreach (Transform target in targets)
            {
                if (target == null) continue;

                target.SetParent(newParent, worldPositionStays);
            }
        }
    }
}