using System.Collections.Generic;
using Foundry.Interaction;
using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Actions
{
    // Uses or switches Interactables from any event - e.g. a VolumeTrigger3D, a timer, or another switch (two levers
    // that stay in sync, a master switch that resets a puzzle)
    [System.Serializable]
    public class InteractableAction : FrameAction
    {
        public enum Operation
        {
            Use,        // as if pressed/pecked: its mode's action (press, toggle, use once)
            TurnOn,     // Toggle: switch on (events fire if it changes)
            TurnOff,    // Toggle: switch off
            Flip,       // Toggle: switch to the other state, without counting as a use (no cooldown, no On Interact)
            Reset       // back to how it started
        }

        public override string ActionType => "Interactable";

        [Title("Interactable Settings")]
        [ListDrawerSettings(DefaultExpandedState = true)]
        public List<Interactable> interactables = new();

        [EnumToggleButtons]
        public Operation operation = Operation.Use;

        protected override void Activate()
        {
            if (interactables == null || interactables.Count == 0)
            {
                Debug.LogWarning($"[InteractableAction] No Interactables set - {actionName}");
                return;
            }

            foreach (Interactable interactable in interactables)
            {
                if (interactable == null) continue;

                switch (operation)
                {
                    case Operation.Use: interactable.Interact(); break;
                    case Operation.TurnOn: interactable.SetOn(true); break;
                    case Operation.TurnOff: interactable.SetOn(false); break;
                    case Operation.Flip: interactable.SetOn(!interactable.IsOn); break;
                    case Operation.Reset: interactable.ResetInteractable(); break;
                }
            }
        }
    }
}
