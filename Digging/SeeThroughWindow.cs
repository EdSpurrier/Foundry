using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Digging
{
    // Opens a see-through window in "Foundry/Dirt (See-Through)" dirt around this object (e.g. the chicken), so it can
    // be seen inside Rounded DiggableTerrain - dirt in front of it is cut away in a soft circle, and inside the circle
    // a slice of the dirt at its plane shows: solid where undug, the rounded tunnels where dug. Put one on the player.
    // Only one window at a time (it sets global shader values); with none, the dirt is drawn whole.
    public class SeeThroughWindow : MonoBehaviour
    {
        private static readonly int WindowId = Shader.PropertyToID("_FoundrySeeThrough");
        private static readonly int SoftnessId = Shader.PropertyToID("_FoundrySeeThroughSoftness");

        [Tooltip("Radius (m) of the window around this object.")]
        [SerializeField, Min(0.1f)] private float radius = 1.4f;

        [Tooltip("Width (m) of the soft edge of the window.")]
        [SerializeField, Min(0f)] private float softness = 0.35f;

        [Tooltip("Centre of the window relative to this object - e.g. up to the middle of the chicken's body.")]
        [SerializeField] private Vector3 offset = new(0f, 0.7f, 0f);

        private void LateUpdate()
        {
            Vector3 center = transform.position + offset;
            Shader.SetGlobalVector(WindowId, new Vector4(center.x, center.y, center.z, radius));
            Shader.SetGlobalFloat(SoftnessId, softness);
        }

        // No window: radius 0 - the dirt draws whole and the slice is hidden
        private void OnDisable()
        {
            Shader.SetGlobalVector(WindowId, Vector4.zero);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
            Vector3 center = transform.position + offset;
            const int STEPS = 32;
            Vector3 previous = center + new Vector3(radius, 0f, 0f);
            for (int s = 1; s <= STEPS; s++)
            {
                float angle = s * Mathf.PI * 2f / STEPS;
                Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                Gizmos.DrawLine(previous, point);
                previous = point;
            }
        }
#endif
    }
}
