using System.Collections.Generic;
using Foundry.Triggers;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.CameraSystem
{
    // A trigger volume that frames the camera on an area while the player is inside it, e.g. pulled back over a puzzle
    // so everything it needs is on screen. Two ways to frame it:
    //  - Hold Still: the camera stops following and holds on a fixed point (View Centre), Distance back.
    //  - Follow: the camera keeps following the player, Distance back and shifted by Follow Shift (up to show things
    //    overhead, sideways to look ahead).
    // Zones can touch or overlap: the one entered last wins, and leaving it falls back to a zone the player is still in,
    // or to the camera's defaults when it's in none. On Enter / On Exit still fire for anything extra.
    // Select a zone in the editor to see its frame in the Scene view (drag the handles to move/resize it) and a live
    // preview of the game camera's view (CameraFocusZonePreview).
    [AddComponentMenu("Foundry/Camera/Camera Focus Zone")]
    public class CameraFocusZone : VolumeTrigger3D
    {
        public enum FramingMode { HoldStill, Follow }

        [Title("Camera Framing")]
        [Tooltip("Hold Still: the camera stops on View Centre. Follow: it keeps following the player, pulled back / shifted.")]
        [EnumToggleButtons]
        [SerializeField] private FramingMode mode = FramingMode.HoldStill;

        [Tooltip("How far the camera sits back from the play plane (the normal camera is about 7.5).")]
        [SuffixLabel("m", Overlay = true)]
        [SerializeField, Min(1f)] private float distance = 15f;

        [ShowIf(nameof(mode), FramingMode.HoldStill)]
        [Tooltip("The point the camera holds on, relative to this zone. Drag the handle in the Scene view.")]
        [SerializeField] private Vector3 viewCentre;

        [ShowIf(nameof(mode), FramingMode.Follow)]
        [Tooltip("Camera shift from the player: X looks ahead (right), Y raises the view.")]
        [SerializeField] private Vector2 followShift = new(0f, 3f);

        [ShowIf(nameof(mode), FramingMode.Follow)]
        [Tooltip("Keep the camera's look-ahead while following (Hold Still always turns it off).")]
        [SerializeField] private bool lookAhead = true;

        [FoldoutGroup("Editor Preview")]
        [Tooltip("Where the stand-in player sits for the Follow preview, relative to this zone. Drag it in the Scene view.")]
        [SerializeField] private Vector3 previewPlayerSpot;

        [FoldoutGroup("Editor Preview")]
        [Tooltip("Show a live preview of the game camera in the Scene view while this zone is selected.")]
        [SerializeField] private bool showPreviewWindow = true;

        // Zones the player is inside, most recently entered last
        private static readonly List<CameraFocusZone> Occupied = new();
        public static CameraFocusZone Current => Occupied.Count > 0 ? Occupied[^1] : null;

        private Transform _viewPoint;

        public FramingMode Mode => mode;
        public float Distance => distance;
        public Vector3 ViewCentreWorld => transform.TransformPoint(viewCentre);
        public Vector2 FollowShift => followShift;
        public Vector3 PreviewPlayerWorld => transform.TransformPoint(previewPlayerSpot);
        public bool ShowPreviewWindow => showPreviewWindow;

        // Where the camera sits for a player standing at playerPosition (the rig puts the camera at target + offset)
        public Vector3 CameraPositionFor(Vector3 playerPosition) => mode == FramingMode.HoldStill
            ? ViewCentreWorld + Vector3.back * distance
            : playerPosition + new Vector3(followShift.x, followShift.y, -distance);

        public void SetFraming(FramingMode newMode, float newDistance, Vector3 newViewCentreWorld, Vector2 newFollowShift, bool newLookAhead)
        {
            mode = newMode;
            distance = Mathf.Max(1f, newDistance);
            viewCentre = transform.InverseTransformPoint(newViewCentreWorld);
            followShift = newFollowShift;
            lookAhead = newLookAhead;
        }

        public void SetViewCentreWorld(Vector3 world) => viewCentre = transform.InverseTransformPoint(world);
        public void SetDistance(float value) => distance = Mathf.Max(1f, value);
        public void SetFollowShift(Vector2 value) => followShift = value;
        public void SetPreviewPlayerWorld(Vector3 world) => previewPlayerSpot = transform.InverseTransformPoint(world);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStatics() => Occupied.Clear();

        protected override void Reset()
        {
            base.Reset();
            onEnter = new FrameCoreU.Events.FrameCoreEvent { eventName = "Camera Focus Zone - Enter" };
            onExit = new FrameCoreU.Events.FrameCoreEvent { eventName = "Camera Focus Zone - Exit" };
            int player = LayerMask.NameToLayer("Player");
            if (player >= 0)
                detectionMask = 1 << player;
            if (TryGetComponent(out BoxCollider box))
                previewPlayerSpot = box.center + Vector3.down * (box.size.y * 0.5f - 0.1f);
        }

        protected override void Enter(GameObject target, Component other)
        {
            Occupied.Remove(this);
            Occupied.Add(this);
            Apply();
        }

        protected override void Exit(GameObject target, Component other)
        {
            if (GetTrackedCount() > 0)
                return;
            Leave();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Leave();
        }

        private void Leave()
        {
            bool wasCurrent = Current == this;
            if (!Occupied.Remove(this) || !wasCurrent)
                return;

            if (Current != null)
                Current.Apply();
            else
                Foundry.Camera?.ResetCamera();
        }

        // Frames the camera for this zone
        [Button("Apply Now (Play mode)"), ShowIf("@UnityEngine.Application.isPlaying")]
        public void Apply()
        {
            CameraCore core = Foundry.Camera;
            if (core == null)
                return;

            if (mode == FramingMode.HoldStill)
            {
                if (_viewPoint == null)
                {
                    _viewPoint = new GameObject("View Point (runtime)").transform;
                    _viewPoint.SetParent(transform, false);
                }
                _viewPoint.position = ViewCentreWorld;
                core.SetTarget(_viewPoint);
                core.SetOffset(new Vector3(0f, 0f, -distance));
                core.SetLookAhead(false);
            }
            else
            {
                if (core.DefaultTarget != null)
                    core.SetTarget(core.DefaultTarget);
                core.SetOffset(new Vector3(followShift.x, followShift.y, -distance));
                core.SetLookAhead(lookAhead);
            }
        }

        // Hold Still: centre the view on the zone's box and pull back just far enough to fit all of it
        [Button("Fit View To Zone"), ShowIf(nameof(mode), FramingMode.HoldStill)]
        private void FitViewToZone()
        {
            if (!TryGetComponent(out BoxCollider box))
                return;
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Fit View To Zone");
#endif
            Bounds b = box.bounds;
            viewCentre = transform.InverseTransformPoint(new Vector3(b.center.x, b.center.y, 0f));
            distance = DistanceToFit(b.size.x, b.size.y);
        }

        // How far back a camera must be for a width x height area of the play plane to fill the screen
        public static float DistanceToFit(float width, float height)
        {
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            float fov = cam != null ? cam.fieldOfView : 60f;
            float aspect = cam != null && cam.aspect > 0.1f ? cam.aspect : 16f / 9f;
            float tan = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            return Mathf.Max(width * 0.5f / (tan * aspect), height * 0.5f / tan);
        }

        // Zones show as a faint outline so they're easy to find; selecting one shows its camera frame (see the preview)
        private void OnDrawGizmos()
        {
            if (!TryGetComponent(out BoxCollider box))
                return;
            Gizmos.color = new Color(1f, 0.78f, 0.2f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }

#if UNITY_EDITOR
        // Live tuning: changing the framing in Play mode while the player is in this zone updates the camera
        protected override void OnValidate()
        {
            base.OnValidate();
            if (Application.isPlaying && isActiveAndEnabled && Current == this)
                Apply();
        }
#endif
    }
}
