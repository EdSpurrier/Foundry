using System.Collections.Generic;
using Foundry.Hazards;
using FrameCoreU.Events;
using Sirenix.OdinInspector;

namespace Foundry.Actions
{
    // Lights, puts out or wets Fires - e.g. a lightning strike setting the barn alight
    [System.Serializable]
    public class FireAction : FrameAction
    {
        public enum Mode { Ignite, Extinguish, Douse }

        public override string ActionType => "Fire";

        public Mode mode = Mode.Ignite;

        [ShowIf(nameof(mode), Mode.Douse)]
        [UnityEngine.Tooltip("How much wetter (1 = puts it right out).")]
        [UnityEngine.Range(0f, 1f)]
        public float amount = 0.35f;

        [ListDrawerSettings(DefaultExpandedState = true)]
        public List<Fire> fires = new();

        protected override void Activate()
        {
            if (fires == null || fires.Count == 0)
            {
                LogWarning($"[FireAction] No Fires set - {actionName}");
                return;
            }

            foreach (Fire fire in fires)
            {
                if (fire == null) continue;
                switch (mode)
                {
                    case Mode.Ignite: fire.Ignite(); break;
                    case Mode.Extinguish: fire.Extinguish(); break;
                    case Mode.Douse: fire.Douse(amount); break;
                }
            }
        }
    }
}
