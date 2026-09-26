using UnityEngine;

namespace Foundry.Data
{
    public enum WindOnsetMode
    {
        Smooth,
        Instant
    }

    [System.Serializable]
    public class WindData
    {
        public GameObject source;

        // World space, normalized
        public Vector3 direction;

        // Wind speed along direction at the receiver's position, falloff already applied
        public float velocity;

        public WindOnsetMode onsetMode;

        [Tooltip("Only used when onsetMode is Smooth - how quickly velocity is pulled toward direction*velocity.")]
        public float onsetAcceleration;
    }
}
