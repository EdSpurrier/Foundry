using System.Collections.Generic;
using Foundry.Damage;
using Foundry.Fracture;
using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Actions
{
    // Breaks Fracturables apart from any event - e.g. a switch that collapses a bridge
    [System.Serializable]
    public class FractureAction : FrameAction
    {
        public override string ActionType => "Fracture";

        [Title("Fracture Settings")]
        [ListDrawerSettings(DefaultExpandedState = true)]
        public List<Fracturable> fracturables = new();

        [Tooltip("Break from where each one was last hit (if it has a Life that's been damaged), rather than from its middle.")]
        public bool fromLastHit = true;

        protected override void Activate()
        {
            if (fracturables == null || fracturables.Count == 0)
            {
                Debug.LogWarning($"[FractureAction] No Fracturables set - {actionName}");
                return;
            }

            foreach (Fracturable fracturable in fracturables)
            {
                if (fracturable == null) continue;

                if (fromLastHit && fracturable.TryGetComponent(out Life life) && life.LastDamage != null && life.LastDamage.point != Vector3.zero)
                    fracturable.Break(life.LastDamage.point);
                else
                    fracturable.Break();
            }
        }
    }
}
