using System.Collections.Generic;
using Foundry.Weather;
using FrameCoreU.Events;
using Sirenix.OdinInspector;

namespace Foundry.Actions
{
    // Starts, stops or toggles Rain - e.g. the storm starting up as the player walks in
    [System.Serializable]
    public class RainAction : FrameAction
    {
        public enum Mode { Start, Stop, Toggle }

        public override string ActionType => "Rain";

        public Mode mode = Mode.Start;

        [ListDrawerSettings(DefaultExpandedState = true)]
        public List<Rain> rain = new();

        protected override void Activate()
        {
            if (rain == null || rain.Count == 0)
            {
                LogWarning($"[RainAction] No Rain set - {actionName}");
                return;
            }

            foreach (Rain r in rain)
            {
                if (r == null) continue;
                switch (mode)
                {
                    case Mode.Start: r.StartRain(); break;
                    case Mode.Stop: r.StopRain(); break;
                    case Mode.Toggle: r.Toggle(); break;
                }
            }
        }
    }
}
