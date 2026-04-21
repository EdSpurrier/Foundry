using Sirenix.OdinInspector;
using System.Collections.Generic;
using FrameCoreU.Events;
using UnityEngine;

namespace Foundry.Actions
{
    [System.Serializable]
    public class Unparent : FrameAction
    {
        public override string ActionType => "Unparent";

        [Title("Targets")]
        [Required]
        public List<Transform> targets = new();

        [ToggleLeft]
        public bool worldPositionStays = true;


        protected override void Activate()
        {
            if (targets == null || targets.Count == 0)
            {
                Debug.LogWarning("Action: Unparent >> No targets assigned.");
                return;
            }
            
            foreach (Transform target in targets)
            {
                if (target == null) continue;

                target.SetParent(null, worldPositionStays);
            }
        }
    }
}