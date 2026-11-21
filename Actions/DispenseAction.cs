using System.Collections.Generic;
using Foundry.Machines;
using FrameCoreU.Events;
using Sirenix.OdinInspector;

namespace Foundry.Actions
{
    // Makes Dispensers hand out their next copy - e.g. from an Interactable an egg hits (a hatch drops a crate)
    [System.Serializable]
    public class DispenseAction : FrameAction
    {
        public override string ActionType => "Dispense";

        [ListDrawerSettings(DefaultExpandedState = true)]
        public List<Dispenser> dispensers = new();

        protected override void Activate()
        {
            if (dispensers == null || dispensers.Count == 0)
            {
                LogWarning($"[DispenseAction] No Dispensers set - {actionName}");
                return;
            }

            foreach (Dispenser dispenser in dispensers)
                if (dispenser != null)
                    dispenser.Dispense();
        }
    }
}
