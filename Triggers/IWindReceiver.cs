using Foundry.Data;

namespace Foundry.Triggers
{
    // Implement to decide for yourself how a WindTrigger3D affects you (e.g. the player only rides it while
    // gliding). Receivers are told about the wind every FixedUpdate while inside, and are not also pushed as plain
    // Rigidbodies.
    public interface IWindReceiver
    {
        void OnWind(WindData windData, float deltaTime);
    }
}
