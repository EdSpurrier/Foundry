using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Triggers
{
    // Redirects wind that hits this object. When a WindTrigger3D with this object's layer in its Blocked By mask blows
    // into it, the wind stops here and a new wind zone blows out of the hit point in Outgoing Direction - e.g. a
    // horizontal fan hitting an angled rock becomes an updraft at the rock. The new zone is an ordinary
    // WindTrigger3D (the player can glide on it, it blows eggs and particles, it gets its own WindVisual if the
    // source has one), and it can itself hit another reflector, up to MAX_CHAIN redirections.
    //
    // Strength comes from the incoming wind where it hits (so with falloff on the source, closer = stronger), scaled
    // by how much of the incoming beam this object covers and by Strength %.
    [DefaultExecutionOrder(50)] // After wind zones have reported this physics step's hits
    public class WindReflector : MonoBehaviour
    {
        public const int MAX_CHAIN = 3;

        public struct IncomingWind
        {
            public WindTrigger3D Source;
            public Vector3 Point;
            public Vector3 Direction;
            public float Speed;
            public Vector3 CrossAxisA;
            public float CrossSizeA;
            public Vector3 CrossAxisB;
            public float CrossSizeB;
            public int Depth;
            public float Coverage;              // fraction of the incoming beam this object blocks (already in Speed)
            public float Remaining;             // how much further (m) the incoming wind would have blown past the hit point
            public ReflectedFalloff Falloff;    // the incoming wind's falloff, carrying on from where it hits
        }

        // A redirected zone's falloff over distance from its base. StartDistance > 0 carries on a curve part-way
        // through (an exact reflection continuing its source's fade), normalised so the base is full strength - the
        // zone's velocity is already the speed at the hit.
        public struct ReflectedFalloff
        {
            public bool Use;
            public AnimationCurve Curve;
            public float MaxDistance;
            public float StartDistance;

            public float Evaluate(float distanceFromBase)
            {
                if (!Use || Curve == null || MaxDistance <= 0f)
                    return 1f;

                float value = Curve.Evaluate(Mathf.Clamp01((StartDistance + distanceFromBase) / MaxDistance));
                if (StartDistance > 0f)
                {
                    float atBase = Curve.Evaluate(Mathf.Clamp01(StartDistance / MaxDistance));
                    value = atBase > 0.0001f ? value / atBase : 0f;
                }

                return Mathf.Clamp01(value);
            }
        }

        public enum ReachMode
        {
            Fixed,
            MatchSource
        }

        [Title("Redirect")]
        [Tooltip("Which way the redirected wind blows - any direction. Self: relative to this object's rotation (it turns with the object). World: fixed in world space.")]
        [SerializeField] private Vector3 outgoingDirection = Vector3.up;

        [SerializeField] private Space directionSpace = Space.World;

        [Tooltip("Multiplies the redirected wind's strength: 100% passes on everything that hits it, lower dampens it, higher amplifies it.")]
        [SuffixLabel("%", Overlay = true)]
        [SerializeField, Min(0f)] private float strengthPercent = 100f;

        [Title("Redirected Zone")]
        [EnumToggleButtons]
        [Tooltip("Fixed: the redirected wind reaches up to Max Reach, with its own falloff. Match Source: an exact reflection - it carries on for as far as the incoming wind would have gone past this object, fading with the source's own falloff from where it hit.")]
        [SerializeField] private ReachMode reachMode = ReachMode.Fixed;

        [Tooltip("Scales how far the reflection carries on, with its fade stretched to match - for a bigger effect from a short fan or in a tight space. Match Source: 1 = exactly as far as the incoming wind would have gone, 2 = twice as far. Fixed: multiplies Max Reach. How strong it is stays with Strength %.")]
        [SuffixLabel("x", Overlay = true)]
        [SerializeField, Min(0.1f)] private float reflectionMultiplier = 1f;

        [ShowIf(nameof(reachMode), ReachMode.Fixed)]
        [LabelText("Max Reach")]
        [Tooltip("How far (m) the redirected wind reaches at most.")]
        [SerializeField, Min(0.1f)] private float reach = 4f;

        [ShowIf(nameof(reachMode), ReachMode.Fixed)]
        [Tooltip("Shorter reach the further this object is from the source: scaled by the source's falloff where the wind hits (with no falloff on the source, always Max Reach). How much of the beam this object covers weakens the redirected wind but doesn't shorten it.")]
        [SerializeField] private bool scaleReachWithStrength = true;

        // Renamed from the old float startOffset - a changed type under the same name breaks existing prefab overrides
        [LabelText("Start Offset")]
        [Tooltip("Where the redirected wind starts, as an offset (m) from the point the incoming wind hits - in the same space as Outgoing Direction (Self: turns with this object, ignoring its scale; World: world axes). E.g. (0, 0.5, 0) starts an updraft half a metre up, above this object's surface.")]
        [SerializeField] private Vector3 startOffsetVector;

        [Tooltip("Use a fixed cross-section instead of matching the incoming wind's.")]
        [SerializeField] private bool overrideWidth;

        [ShowIf(nameof(overrideWidth))]
        [Tooltip("Cross-section (m): X = across the plane the wind turns in (in a 2.5D level, the X width of an updraft column), Y = the other way (its Z depth).")]
        [SerializeField] private Vector2 width = new(1f, 1f);

        [ShowIf(nameof(reachMode), ReachMode.Fixed)]
        [Tooltip("Fade the redirected wind out over its reach (strongest at this object), so things settle into a float near the top instead of being launched. (Match Source uses the source's falloff instead.)")]
        [SerializeField] private bool useFalloff = true;

        [ShowIf(nameof(ShowFalloffCurve))]
        [SerializeField] private AnimationCurve falloffCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Title("Events")]
        [SerializeField, HideLabel] private FrameCoreEvent onRedirectStart = new() { eventName = "Reflector - Redirect Start" };

        [SerializeField, HideLabel] private FrameCoreEvent onRedirectEnd = new() { eventName = "Reflector - Redirect End" };

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private bool redirecting;

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private float redirectedSpeed;

        [FoldoutGroup("Debug")]
        [Tooltip("Draw the redirect gizmo (incoming wind, bend, redirected zone) even when this object isn't selected - handy while laying out a wind puzzle.")]
        [SerializeField] private bool gizmoWhenNotSelected;

        private const float MIN_SPEED = 0.1f;

        private IncomingWind _strongest;
        private bool _receivedThisStep;
        private WindTrigger3D _zone;
        private WindVisual _zoneVisual;

        private bool ShowFalloffCurve => reachMode == ReachMode.Fixed && useFalloff;

        public bool IsRedirecting => redirecting;
        public Vector3 OutgoingDirection => (directionSpace == Space.Self ? transform.TransformDirection(outgoingDirection) : outgoingDirection).normalized;

        // Called by a WindTrigger3D blowing into this object during its FixedUpdate. With several sources this step,
        // the strongest wins.
        internal void Receive(IncomingWind incoming)
        {
            if (incoming.Depth >= MAX_CHAIN || !isActiveAndEnabled)
                return;

            if (_receivedThisStep && incoming.Speed <= _strongest.Speed)
                return;

            _strongest = incoming;
            _receivedThisStep = true;
        }

        private void FixedUpdate()
        {
            bool hit = _receivedThisStep;
            _receivedThisStep = false;

            if (!hit || !ComputeRedirect(_strongest, out Redirect redirect))
            {
                StopRedirecting();
                return;
            }

            if (_zone == null)
                CreateZone(_strongest);

            _zone.transform.SetPositionAndRotation(redirect.BasePoint, redirect.Rotation);
            _zone.UpdateReflection(redirect.Speed, redirect.Strength, redirect.Size, redirect.Falloff);
            redirectedSpeed = redirect.Speed;
            _lastIncoming = _strongest;
            _lastRedirect = redirect;

            if (!redirecting)
            {
                redirecting = true;
                onRedirectStart?.Activate();
            }
        }

        private void OnDisable()
        {
            _receivedThisStep = false;
            StopRedirecting();
        }

        private void OnDestroy()
        {
            if (_zone != null)
                Destroy(_zone.gameObject);
        }

        private void StopRedirecting()
        {
            redirectedSpeed = 0f;
            if (!redirecting)
                return;

            redirecting = false;
            if (_zone != null)
                _zone.StopReflection();

            onRedirectEnd?.Activate();
        }

        private void CreateZone(IncomingWind incoming)
        {
            // Its own object (not a child) so this object's scale can't distort it; it's repositioned every step
            GameObject zoneObject = new($"{name} - Redirected Wind") { layer = incoming.Source.gameObject.layer };

            // A kinematic body makes a trigger that moves every step cheap for the physics engine
            Rigidbody body = zoneObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            BoxCollider box = zoneObject.AddComponent<BoxCollider>();
            box.isTrigger = true;

            _zone = zoneObject.AddComponent<WindTrigger3D>();
            _zone.InitialiseAsReflection(incoming.Source, this, incoming.Depth + 1);

            // Same look as the source's, fed only by the source's particles bending round the corner (no emitter of its own)
            if (incoming.Source.TryGetComponent(out WindVisual sourceVisual) && sourceVisual.HasParticles)
            {
                _zoneVisual = zoneObject.AddComponent<WindVisual>();
                _zoneVisual.CopyLookFrom(sourceVisual, handOffOnly: true);
            }
        }

        // Where and how the redirected wind blows for a given incoming wind. Pure, so the gizmos can preview it in
        // edit mode. False if it would be too weak to bother with.
        private struct Redirect
        {
            public Vector3 BasePoint;
            public Quaternion Rotation;     // local Z = outgoing direction
            public Vector3 Size;            // (across the turn, along the turn axis, reach) in world units
            public float Speed;
            public float Strength;
            public ReflectedFalloff Falloff;

            // How the incoming beam maps onto the redirected zone, for bending its particles: the incoming cross axis
            // that folds into the turn (signed toward the outgoing direction), where that ends up across the new zone,
            // and the axis the turn happens around (unchanged by it)
            public Vector3 FoldedAxis;
            public Vector3 AcrossAxis;
            public Vector3 TurnAxis;
        }

        private bool ComputeRedirect(in IncomingWind incoming, out Redirect redirect)
        {
            redirect = default;
            float speed = incoming.Speed * strengthPercent * 0.01f;
            if (speed < MIN_SPEED || incoming.Source == null)
                return false;

            Vector3 outgoing = OutgoingDirection;
            float length;
            if (reachMode == ReachMode.MatchSource)
            {
                // Exact reflection: the wind carries on as far as it would have, still fading as it would have - both
                // stretched by the multiplier (scaling the falloff's start and end keeps the same curve, just longer)
                length = Mathf.Max(incoming.Remaining * reflectionMultiplier, 0.1f);
                redirect.Falloff = incoming.Falloff;
                redirect.Falloff.StartDistance *= reflectionMultiplier;
                redirect.Falloff.MaxDistance *= reflectionMultiplier;
            }
            else
            {
                // Scaled by distance from the source (its falloff where it hits) only - not by coverage, which would
                // jump the reach from nothing to full as the object slides across the beam
                float distanceScale = incoming.Falloff.Use && incoming.Falloff.MaxDistance > 0f
                    ? Mathf.Clamp01(incoming.Falloff.Curve.Evaluate(Mathf.Clamp01(incoming.Falloff.StartDistance / incoming.Falloff.MaxDistance)))
                    : 1f;
                length = Mathf.Max((scaleReachWithStrength ? reach * distanceScale : reach) * reflectionMultiplier, 0.1f);
                redirect.Falloff = new ReflectedFalloff { Use = useFalloff, Curve = falloffCurve, MaxDistance = length };
            }

            // Keeps the zone's axes lined up with the incoming beam: the incoming cross axis that's most at right angles
            // to the turn stays as it is (the Z depth, for a 2.5D fan turned upward), the other folds into the turn
            Vector3 turnAxis = Vector3.Cross(incoming.Direction, outgoing);
            if (turnAxis.sqrMagnitude < 1e-6f)
                turnAxis = incoming.CrossAxisB;
            turnAxis.Normalize();

            bool aIsTurnAxis = Mathf.Abs(Vector3.Dot(incoming.CrossAxisA, turnAxis)) > Mathf.Abs(Vector3.Dot(incoming.CrossAxisB, turnAxis));
            float keptSize = aIsTurnAxis ? incoming.CrossSizeA : incoming.CrossSizeB;
            float foldedSize = aIsTurnAxis ? incoming.CrossSizeB : incoming.CrossSizeA;
            Vector2 crossSection = overrideWidth ? width : new Vector2(foldedSize, keptSize);

            // Local Z along the outgoing wind, local Y along the turn axis (kept size), local X across (folded size)
            redirect.Rotation = Quaternion.LookRotation(outgoing, turnAxis);
            redirect.BasePoint = incoming.Point + (directionSpace == Space.Self ? transform.rotation * startOffsetVector : startOffsetVector);
            redirect.Size = new Vector3(crossSection.x, crossSection.y, length);
            redirect.Speed = speed;

            // Same drag per m/s as the source, so a given object reacts to it the same way at the same speed
            redirect.Strength = incoming.Source.Velocity > 0f ? incoming.Source.Strength * speed / incoming.Source.Velocity : incoming.Source.Strength;

            Vector3 folded = aIsTurnAxis ? incoming.CrossAxisB : incoming.CrossAxisA;
            redirect.FoldedAxis = Vector3.Dot(folded, outgoing) < 0f ? -folded : folded;
            redirect.TurnAxis = turnAxis;

            // The incoming direction with its outgoing part removed - e.g. a fan blowing +X turned upward: the top of the
            // beam (folded toward up) comes out on the +X (downwind) side of the column
            Vector3 across = incoming.Direction - outgoing * Vector3.Dot(incoming.Direction, outgoing);
            redirect.AcrossAxis = across.sqrMagnitude > 1e-6f ? across.normalized : redirect.Rotation * Vector3.right;
            return true;
        }

        private IncomingWind _lastIncoming;
        private Redirect _lastRedirect;

        // The redirected zone's WindVisual while redirecting - the source's particles are handed on to it at the bend
        internal WindVisual RedirectVisual => redirecting ? _zoneVisual : null;

        // Where a particle crossing the incoming wind's blocking point should continue from in the redirected zone:
        // its position across the incoming beam is turned through the bend (see Redirect), so the stream keeps its
        // shape round the corner instead of bunching up
        internal Vector3 BendPosition(Vector3 crossing)
        {
            Redirect redirect = _lastRedirect;
            Vector3 relative = crossing - _lastIncoming.Point;

            float folded = Mathf.Clamp(Vector3.Dot(relative, redirect.FoldedAxis), -redirect.Size.x * 0.5f, redirect.Size.x * 0.5f);
            float kept = Mathf.Clamp(Vector3.Dot(relative, redirect.TurnAxis), -redirect.Size.y * 0.5f, redirect.Size.y * 0.5f);

            return redirect.BasePoint + redirect.AcrossAxis * folded + redirect.TurnAxis * kept;
        }

#if UNITY_EDITOR
        private static readonly Color GizmoRedirectColor = new(0.35f, 0.95f, 0.45f);

        private void OnDrawGizmos()
        {
            if (gizmoWhenNotSelected)
                DrawReflectorGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            if (!gizmoWhenNotSelected)
                DrawReflectorGizmos();
        }

        // Selected on its own: find the wind blowing into it (the real one in play, a live prediction in edit mode)
        // and draw the bend. With nothing hitting it, just show which way it would send wind.
        private void DrawReflectorGizmos()
        {
            if (TryFindIncoming(out IncomingWind incoming))
            {
                DrawRedirectGizmo(incoming);
                return;
            }

            float previewLength = reachMode == ReachMode.Fixed ? reach * reflectionMultiplier : 2f;
            WindTrigger3D.DrawGizmoArrow(transform.position, OutgoingDirection, previewLength, WindTrigger3D.WithAlpha(GizmoRedirectColor, 0.45f));
            WindTrigger3D.DrawGizmoLabel(transform.position + OutgoingDirection * previewLength, $"{name}: no wind hitting it\nwould redirect {OutgoingDirection:F1} at {strengthPercent:0}%");
        }

        private bool TryFindIncoming(out IncomingWind incoming)
        {
            incoming = default;
            if (Application.isPlaying)
            {
                incoming = _lastIncoming;
                return redirecting && incoming.Source != null;
            }

            // Strongest wind zone in the scene currently blowing into this object
            bool found = false;
            foreach (WindTrigger3D wind in FindObjectsByType<WindTrigger3D>())
            {
                if (!wind.TryPredictIncoming(this, out IncomingWind candidate)) continue;
                if (found && candidate.Speed <= incoming.Speed) continue;

                incoming = candidate;
                found = true;
            }

            return found;
        }

        // The bend: incoming wind arriving at the hit point, then the redirected zone - its box (reach and width), flow
        // lines fading with its falloff, and a label with its strength. Also called by the source wind's own gizmo.
        internal void DrawRedirectGizmo(in IncomingWind incoming)
        {
            // Incoming wind arriving at the hit point
            float incomingLength = Mathf.Min(2f, Mathf.Max(0.5f, reach * 0.5f));
            WindTrigger3D.DrawGizmoArrow(incoming.Point - incoming.Direction * incomingLength, incoming.Direction, incomingLength, WindTrigger3D.GizmoBlockedColor);
            Gizmos.color = WindTrigger3D.GizmoBlockedColor;
            Gizmos.DrawSphere(incoming.Point, 0.08f);

            if (!ComputeRedirect(incoming, out Redirect redirect))
            {
                WindTrigger3D.DrawGizmoLabel(incoming.Point, $"{name}: incoming {incoming.Speed:0.#} m/s - too weak to redirect");
                return;
            }

            // The redirected zone's box
            Gizmos.matrix = Matrix4x4.TRS(redirect.BasePoint, redirect.Rotation, Vector3.one);
            Vector3 boxCenter = new(0f, 0f, redirect.Size.z * 0.5f);
            Gizmos.color = WindTrigger3D.WithAlpha(GizmoRedirectColor, 0.07f);
            Gizmos.DrawCube(boxCenter, redirect.Size);
            Gizmos.color = WindTrigger3D.WithAlpha(GizmoRedirectColor, 0.9f);
            Gizmos.DrawWireCube(boxCenter, redirect.Size);
            Gizmos.matrix = Matrix4x4.identity;

            // Flow lines up the redirected wind, fading with its falloff
            Vector3 outgoing = redirect.Rotation * Vector3.forward;
            Vector3 across = redirect.Rotation * Vector3.right;
            const int LINES = 3;
            const int SEGMENTS = 6;
            for (int line = 0; line < LINES; line++)
            {
                float offset = (line / (float)(LINES - 1) - 0.5f) * redirect.Size.x * 0.8f;
                Vector3 start = redirect.BasePoint + across * offset;

                for (int i = 0; i < SEGMENTS; i++)
                {
                    float t0 = i / (float)SEGMENTS;
                    float t1 = (i + 1) / (float)SEGMENTS;
                    float strengthHere = redirect.Falloff.Evaluate(redirect.Size.z * (t0 + t1) * 0.5f);
                    Gizmos.color = Color.Lerp(WindTrigger3D.WithAlpha(GizmoRedirectColor, 0.15f), GizmoRedirectColor, strengthHere);
                    Gizmos.DrawLine(start + outgoing * (redirect.Size.z * t0), start + outgoing * (redirect.Size.z * t1));
                }

                WindTrigger3D.DrawGizmoArrowHead(start + outgoing * redirect.Size.z, outgoing, 0.15f);
            }

            float percentOfIncoming = incoming.Speed > 0f ? redirect.Speed / incoming.Speed : 0f;
            string source = incoming.Source != null ? incoming.Source.name : "wind";
            WindTrigger3D.DrawGizmoLabel(
                redirect.BasePoint + outgoing * redirect.Size.z,
                $"{name}: {redirect.Speed:0.#} m/s ({percentOfIncoming:P0} of {source}'s {incoming.Speed:0.#} m/s)\n" +
                $"reach {redirect.Size.z:0.0} m{(reachMode == ReachMode.MatchSource ? " (matches source)" : "")}{(Mathf.Approximately(reflectionMultiplier, 1f) ? "" : $" ×{reflectionMultiplier:0.##}")} · width {redirect.Size.x:0.0} × {redirect.Size.y:0.0} m");
        }
#endif
    }
}
