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

        // The zone's full-strength wind speed (no falloff) and strength (kg it can hold up against gravity) - what a
        // receiver needs to work out a mass-based push the same way the zone pushes plain Rigidbodies
        public float fullVelocity;
        public float strength;

        public WindOnsetMode onsetMode;

        [Tooltip("Only used when onsetMode is Smooth - how quickly velocity is pulled toward direction*velocity.")]
        public float onsetAcceleration;

        // Air drag coefficient (N per m/s of speed difference), set so a body of `strength` kg hovers at full strength
        public float DragCoefficient => strength * Physics.gravity.magnitude / Mathf.Max(fullVelocity, 0.01f);

        // Push-only drag force on a body moving at bodyVelocity: proportional to how much slower than the wind it's
        // moving along direction, zero if it's already outrunning the wind
        public Vector3 ForceOn(Vector3 bodyVelocity)
        {
            float gap = velocity - Vector3.Dot(bodyVelocity, direction);
            return gap > 0f ? direction * (gap * DragCoefficient) : Vector3.zero;
        }

        // 0-1: how hard the wind is blowing at this spot against something at rest, relative to its full strength
        public float Intensity => fullVelocity > 0f ? Mathf.Clamp01(velocity / fullVelocity) : 0f;

        // Blows a physics-driven Rigidbody the way a wind zone blows any loose body - for receivers that are sometimes
        // under physics (e.g. a ball rolling free)
        public void PushRigidbody(Rigidbody body, float deltaTime) => PushBody(body, direction, velocity, DragCoefficient, deltaTime);

        // Air drag toward windSpeed along direction, applied as a velocity change capped at closing the whole speed gap
        // in one step - so very light bodies (egg shell pieces weigh 1e-7 kg) match the wind instead of being fired off
        // at absurd speed. Push only: a body already moving faster than the wind along direction isn't slowed.
        public static void PushBody(Rigidbody body, Vector3 direction, float windSpeed, float dragCoefficient, float deltaTime)
        {
            float gap = windSpeed - Vector3.Dot(body.linearVelocity, direction);
            if (gap <= 0f)
                return;

            float response = Mathf.Clamp01(dragCoefficient * deltaTime / Mathf.Max(body.mass, 1e-9f));
            body.AddForce(direction * (gap * response), ForceMode.VelocityChange);
        }
    }
}
