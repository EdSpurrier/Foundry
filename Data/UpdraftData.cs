using UnityEngine;

namespace Foundry.Data
{
    public enum UpdraftOnsetMode
    {
        Smooth,
        Instant
    }

    [System.Serializable]
    public class UpdraftData
    {
        public GameObject source;
        public Vector3 direction;
        public float velocity;

        public UpdraftOnsetMode onsetMode;

        [Tooltip("Only used when onsetMode is Smooth - how quickly velocity is pulled toward direction*velocity.")]
        public float onsetAcceleration;
    }
}
