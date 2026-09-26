using UnityEngine;

namespace Foundry.Particles
{
    // Anything in the environment that moves particles (a vent, a water current, a vortex...). Implementers register
    // themselves with ParticleAffectors while enabled; every ParticlePhysics system then runs its particles through
    // each affector it overlaps, once per frame.
    public interface IParticleAffector
    {
        // False while switched off - skipped entirely
        bool IsAffecting { get; }

        // World-space region it can affect. Particles outside it are never passed to Affect.
        Bounds AffectBounds { get; }

        // Returns the particle's new world-space velocity. sensitivity is the receiving system's (1 = normal,
        // 2 = twice as easily moved, 0 = unaffected) - scale whatever the affector does by it.
        Vector3 Affect(Vector3 position, Vector3 velocity, float sensitivity, float deltaTime);
    }
}
