using System.Collections.Generic;
using UnityEngine;

namespace Foundry.Particles
{
    // Lets the environment move this particle system's particles. Particles have no collider or Rigidbody, so nothing
    // can detect them on its own - instead, once per frame this reads the particles, runs each one that's inside an
    // active IParticleAffector's region (a vent, a current...) through that affector, and writes them back.
    [RequireComponent(typeof(ParticleSystem))]
    public class ParticlePhysics : MonoBehaviour
    {
        [Tooltip("How strongly affectors move these particles. 1 = normal, higher = lighter / more easily moved (dust), lower = heavier (yolk), 0 = unaffected.")]
        [SerializeField, Min(0f)] private float sensitivity = 1f;

        private readonly List<(IParticleAffector Affector, Bounds Bounds)> _overlapping = new();
        private ParticleSystem _system;
        private ParticleSystemRenderer _renderer;
        private ParticleSystem.Particle[] _particles;

        public float Sensitivity => sensitivity;

        private void Awake()
        {
            _system = GetComponent<ParticleSystem>();
            _renderer = GetComponent<ParticleSystemRenderer>();
        }

        private void Update()
        {
            if (_system.particleCount == 0 || !CollectOverlappingAffectors())
                return;

            int capacity = _system.main.maxParticles;
            if (_particles == null || _particles.Length < capacity)
                _particles = new ParticleSystem.Particle[capacity];

            int count = _system.GetParticles(_particles);

            // Particle positions and velocities are in the system's simulation space; affectors work in world space
            Transform space = SimulationSpace();
            float deltaTime = Time.deltaTime;
            bool changed = false;

            for (int i = 0; i < count; i++)
            {
                Vector3 position = space != null ? space.TransformPoint(_particles[i].position) : _particles[i].position;
                Vector3 velocity = space != null ? space.TransformVector(_particles[i].velocity) : _particles[i].velocity;
                Vector3 original = velocity;

                foreach ((IParticleAffector affector, Bounds bounds) in _overlapping)
                {
                    if (bounds.Contains(position))
                        velocity = affector.Affect(position, velocity, sensitivity, deltaTime);
                }

                if (velocity == original)
                    continue;

                _particles[i].velocity = space != null ? space.InverseTransformVector(velocity) : velocity;
                changed = true;
            }

            if (changed)
                _system.SetParticles(_particles, count);
        }

        // Only affectors whose region overlaps where this system's particles currently are - the common case (nothing
        // nearby) then costs no particle reads at all
        private bool CollectOverlappingAffectors()
        {
            _overlapping.Clear();
            Bounds particleBounds = _renderer.bounds;

            foreach (IParticleAffector affector in ParticleAffectors.All)
            {
                if (!affector.IsAffecting)
                    continue;

                // Read once per frame - a collider's bounds aren't free, and this is checked per particle
                Bounds bounds = affector.AffectBounds;
                if (bounds.Intersects(particleBounds))
                    _overlapping.Add((affector, bounds));
            }

            return _overlapping.Count > 0;
        }

        private Transform SimulationSpace()
        {
            ParticleSystem.MainModule main = _system.main;

            return main.simulationSpace switch
            {
                ParticleSystemSimulationSpace.Local => transform,
                ParticleSystemSimulationSpace.Custom => main.customSimulationSpace,
                _ => null
            };
        }
    }
}
