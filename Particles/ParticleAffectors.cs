using System.Collections.Generic;

namespace Foundry.Particles
{
    // Registry of active particle affectors. Register in OnEnable, unregister in OnDisable.
    public static class ParticleAffectors
    {
        private static readonly List<IParticleAffector> _all = new();

        public static IReadOnlyList<IParticleAffector> All => _all;

        public static void Register(IParticleAffector affector)
        {
            if (affector != null && !_all.Contains(affector))
                _all.Add(affector);
        }

        public static void Unregister(IParticleAffector affector)
        {
            _all.Remove(affector);
        }
    }
}
