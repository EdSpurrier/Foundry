using Foundry.CameraSystem;
using Sirenix.OdinInspector;

namespace Foundry
{
    public static class Foundry
    {
        [BoxGroup("Core")]
        public static FoundryCore Core { get; private set; }

        [BoxGroup("Camera")]
        public static CameraCore Camera => Core != null ? Core.Camera : null;

        internal static void SetCore(FoundryCore core)
        {
            Core = core;
        }

        internal static void ClearCore(FoundryCore core)
        {
            if (Core == core)
                Core = null;
        }
    }
}