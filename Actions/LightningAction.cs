using System.Collections.Generic;
using Foundry.Weather;
using FrameCoreU.Events;
using Sirenix.OdinInspector;

namespace Foundry.Actions
{
    // Makes Lightning strike or flash, or starts/stops its ambient flashes (a storm in the background)
    [System.Serializable]
    public class LightningAction : FrameAction
    {
        public enum Mode { Strike, Flash, StartAmbient, StopAmbient }

        public override string ActionType => "Lightning";

        public Mode mode = Mode.Strike;

        [ListDrawerSettings(DefaultExpandedState = true)]
        public List<Lightning> lightning = new();

        protected override void Activate()
        {
            if (lightning == null || lightning.Count == 0)
            {
                LogWarning($"[LightningAction] No Lightning set - {actionName}");
                return;
            }

            foreach (Lightning l in lightning)
            {
                if (l == null) continue;
                switch (mode)
                {
                    case Mode.Strike: l.Strike(); break;
                    case Mode.Flash: l.Flash(); break;
                    case Mode.StartAmbient: l.StartAmbient(); break;
                    case Mode.StopAmbient: l.StopAmbient(); break;
                }
            }
        }
    }
}
