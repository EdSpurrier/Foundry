using System.Collections.Generic;
using Foundry.Data;
using Foundry.Particles;
using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Foundry.Triggers
{
    // A wind zone blowing in any direction - an updraft vent to glide on, a mechanical fan blowing across a level, a
    // gust through a gap. Anything inside with an IWindReceiver is told about the wind and decides for itself what to
    // do (the player only rides it while gliding). Any other non-kinematic Rigidbody inside is blown along Direction in
    // proportion to how light it is, and as an IParticleAffector it blows the particles of any ParticlePhysics system
    // too. The zone is this object's trigger collider - use a Box, Sphere, Capsule or convex Mesh collider. With Fit
    // Collider To Wind, a BoxCollider is sized and placed for you: from this object's pivot, Length along Direction.
    //
    // With a Blocked By mask (and a BoxCollider), the wind stops at the first thing in its path: the box is shortened
    // to end there, so nothing beyond is blown. A WindReflector on what it hits redirects the wind into a new zone.
    public class WindTrigger3D : VolumeTrigger3D, IParticleAffector
    {
        [Title("Wind")]
        [Tooltip("Which way it blows. With Direction Space = Self it's relative to this object's rotation, so a fan can be aimed just by rotating it.")]
        [SerializeField] private Vector3 direction = Vector3.up;

        [Tooltip("Self: Direction turns with this object's rotation (aim a fan by rotating it). World: Direction is fixed in world space.")]
        [SerializeField] private Space directionSpace = Space.Self;

        [Tooltip("Wind speed (m/s) along Direction at full strength.")]
        [SerializeField] private float velocity = 15f;

        [Title("Zone")]
        [Tooltip("Size and place the Box Collider from the settings below: it starts at this object's pivot (put the pivot at the fan's mouth / vent's base) and extends Length along Direction, Width x Depth across it. Kept in sync as you edit. Off = size the collider by hand.")]
        [InfoBox("Direction isn't along one of this object's axes, so the box can't line up with the wind - rotate the object to aim it instead (Direction Space = Self, Direction along an axis).", InfoMessageType.Warning, nameof(IsFitOffAxis))]
        [OnValueChanged(nameof(OnFitToggled))]
        [SerializeField] private bool fitColliderToWind;

        [ShowIf(nameof(fitColliderToWind))]
        [Tooltip("How far the wind blows from this object's pivot (m).")]
        [SerializeField, Min(0.05f)] private float length = 5f;

        [ShowIf(nameof(fitColliderToWind))]
        [LabelText("Width x Depth")]
        [Tooltip("Cross-section (m). Width: across the wind in the X/Y plane (the 2.5D view). Depth: along Z, into the screen (for wind blowing along Z: Width is X, Depth is Y).")]
        [SerializeField] private Vector2 crossSection = new(2f, 1f);

        [ShowIf(nameof(fitColliderToWind))]
        [Tooltip("Make the falloff fade out exactly at the far end of the zone (Max Distance = Length, measured from the pivot).")]
        [SerializeField] private bool falloffMatchesLength = true;

        [Title("Onset")]
        [EnumToggleButtons]
        [HideLabel]
        [SerializeField] private WindOnsetMode onsetMode = WindOnsetMode.Smooth;

        [ShowIf(nameof(onsetMode), WindOnsetMode.Smooth)]
        [Tooltip("How quickly velocity is pulled toward direction*velocity. Ignored when Onset is Instant.")]
        [SerializeField] private float onsetAcceleration = 40f;

        [Title("Falloff")]
        [SerializeField] private bool useFalloff = false;

        [ShowIf(nameof(useFalloff))]
        [Tooltip("Reference point distance is measured from - typically the base of the vent/fan. Defaults to this object's own transform if left empty.")]
        [SerializeField] private Transform origin;

        [ShowIf(nameof(ShowMaxDistance))]
        [Tooltip("Distance along Direction from Origin at which the falloff curve reaches its end (t=1). Set this to roughly match the collider's length along Direction.")]
        [SerializeField] private float maxDistance = 5f;

        [ShowIf(nameof(useFalloff))]
        [Tooltip("Push strength multiplier over normalized distance from Origin (0 = at Origin, 1 = at Max Distance). Author starting at 1 (full push near the base) easing to 0 (no push at the top), so the receiver settles into a float rather than being launched indefinitely. Also sets how strong reflected wind is: the closer a reflector is to the base, the stronger the wind it redirects.")]
        [SerializeField] private AnimationCurve falloffCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Title("Rigidbodies")]
        [Tooltip("Blow any non-kinematic Rigidbody inside (that isn't an IWindReceiver, like the player) along Direction, in proportion to how light it is.")]
        [SerializeField] private bool pushRigidbodies = true;

        [ShowIf(nameof(pushRigidbodies))]
        [Tooltip("How strong the wind is, as the heaviest mass (kg) it could hold up against gravity at full strength - blowing upward, anything lighter rises and anything heavier doesn't lift. Blowing sideways the same force applies: a light egg is carried along toward Velocity, while a heavy rock gets a push too small to beat its ground friction. Nothing is ever pushed faster than Velocity.")]
        [SuffixLabel("kg", Overlay = true)]
        [FormerlySerializedAs("maxMassLifted")]
        [SerializeField, Min(0f)] private float strength = 1f;

        [Title("Particles")]
        [Tooltip("Blow the particles of any particle system with a ParticlePhysics component while they're inside this zone.")]
        [SerializeField] private bool pushParticles = true;

        [ShowIf(nameof(pushParticles))]
        [Tooltip("How quickly particles in this zone catch up to the wind's speed, per second, at Sensitivity 1 (each particle system's ParticlePhysics Sensitivity scales it). Push only - particles already outrunning the wind aren't slowed.")]
        [SerializeField, Min(0f)] private float particleCatchUp = 6f;

        [Title("Blocking")]
        [Tooltip("The wind stops at the first thing in its path on these layers - nothing beyond it is blown. Something with a WindReflector redirects the wind; anything else just absorbs it. Nothing = never blocked. Needs a BoxCollider zone, blowing roughly along one of its axes.")]
        [SerializeField] private LayerMask blockedBy;

        [ShowIf(nameof(IsBlockable))]
        [Tooltip("Rays cast along each side of the zone's cross-section to find what's in the way (this squared in total). More = small blockers are found more reliably, and partial cover is measured more finely.")]
        [SerializeField, Range(1, 5)] private int blockSamples = 3;

        [ShowIf(nameof(IsBlockable))]
        [Tooltip("What happens when something is pushed right up against the wind's source (closer than Stall Distance). Off (default): nothing special - a reflector there gets the wind at full strength, the strongest it can redirect. On: the source is fully blocked and stalls - no wind at all, nothing redirected.")]
        [SerializeField] private bool stallWhenBlocked;

        [ShowIf(nameof(stallWhenBlocked))]
        [Tooltip("How close (m) to the wind's source something must be to stall it.")]
        [SerializeField, Min(0f)] private float stallDistance = 0.3f;

        [ShowIf(nameof(IsBlockable))]
        [FoldoutGroup("Blocking Events")]
        [SerializeField, HideLabel] private FrameCoreEvent onObstructed = new() { eventName = "Wind - Obstructed" };

        [ShowIf(nameof(IsBlockable))]
        [FoldoutGroup("Blocking Events")]
        [SerializeField, HideLabel] private FrameCoreEvent onCleared = new() { eventName = "Wind - Cleared" };

        [ShowIf(nameof(stallWhenBlocked))]
        [FoldoutGroup("Blocking Events")]
        [SerializeField, HideLabel] private FrameCoreEvent onStalled = new() { eventName = "Wind - Stalled" };

        [ShowIf(nameof(stallWhenBlocked))]
        [FoldoutGroup("Blocking Events")]
        [SerializeField, HideLabel] private FrameCoreEvent onResumed = new() { eventName = "Wind - Resumed" };

        [Title("Debug")]
        [Tooltip("Draw the wind in the Scene view: the zone actually blowing (shortened where something blocks it), flow lines fading with falloff, hit points, the stall slice, and - through a WindReflector - where it bends. Previews in edit mode too.")]
        [SerializeField] private bool drawGizmo = true;

        [ShowIf(nameof(drawGizmo))]
        [Tooltip("Draw it even when this object isn't selected - handy while laying out a wind puzzle.")]
        [SerializeField] private bool gizmoWhenNotSelected;

        [SerializeField, ReadOnly] private bool obstructed;
        [SerializeField, ReadOnly] private bool stalled;
        [SerializeField, ReadOnly] private float currentLength;
        [SerializeField, ReadOnly] private float coverage;

        // Rays start this far behind the zone's upwind face, so something pushed flush against the face is still hit
        // (a ray doesn't detect a collider it starts inside). The source's own housing is either behind the ray's
        // start or contains it, so it's never hit.
        private const float RAY_BACKOFF = 0.05f;

        // The shortened zone reaches this far past the blocker's surface, so the blocker itself is inside the wind
        // (and feels it as an IWindReceiver, e.g. a pushable being pushed back)
        private const float BLOCKER_OVERLAP = 0.05f;

        private const float MIN_LENGTH = 0.02f;

        private readonly List<GameObject> _staleTracked = new();
        private readonly RaycastHit[] _rayHits = new RaycastHit[8];
        private readonly Vector3[] _laneOrigins = new Vector3[25];
        private readonly float[] _laneDistances = new float[25];
        private readonly Object[] _laneKeys = new Object[25];
        private Collider _zone;
        private BoxCollider _box;
        private Vector3 _authoredCenter;
        private Vector3 _authoredSize;

        // Set when this zone is the redirected wind of a WindReflector
        private WindReflector _ignoreReflector;
        private int _reflectionDepth;
        private bool _isReflection;
        private WindReflector.ReflectedFalloff _reflectedFalloff;

        private bool IsBlockable => blockedBy != 0;
        private bool FalloffFromLength => fitColliderToWind && falloffMatchesLength;
        private bool ShowMaxDistance => useFalloff && !FalloffFromLength;

        protected override void Awake()
        {
            base.Awake();
            _zone = GetComponent<Collider>();

            // Picks up the object's scale/rotation as spawned
            if (fitColliderToWind && TryGetFittedBox(out BoxCollider box, out Vector3 size, out Vector3 center))
            {
                box.size = size;
                box.center = center;
            }

            CacheAuthoredZone();
        }

        protected override void Reset()
        {
            base.Reset();
            blockedBy = LayerMask.GetMask("Ground", "Blockage - Player", "Interactable", "Moveable");
            fitColliderToWind = true;
#if UNITY_EDITOR
            ScheduleEditorFit();
#endif
        }

        private void OnEnable()
        {
            ParticleAffectors.Register(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ParticleAffectors.Unregister(this);
        }

        // Blowing right now - switched on and not stalled
        public bool IsBlowing => active && !stalled;

        public bool IsAffecting => IsBlowing && pushParticles;

        public Bounds AffectBounds => _zone.bounds;

        // World space, normalized
        public Vector3 Direction => (directionSpace == Space.Self ? transform.TransformDirection(direction) : direction).normalized;
        public float Velocity => velocity;
        public float Strength => strength;
        public WindOnsetMode OnsetMode => onsetMode;
        public float OnsetAcceleration => onsetAcceleration;
        public Transform Origin => origin != null ? origin : transform;
        // Fitted to the zone: the falloff ends exactly at the zone's far end, wherever Origin is along the wind
        public float MaxDistance => FalloffFromLength
            ? Mathf.Max(0.01f, length - Vector3.Dot(Origin.position - transform.position, Direction))
            : maxDistance;
        public bool IsObstructed => obstructed;

        // Bumped whenever the zone's shape changes (e.g. shortened by a blocker) - WindVisual refits when it does
        public int ZoneVersion { get; private set; }

        // Same wind as everything else: pulls the particle's speed along Direction up toward the wind's speed at its
        // position (with falloff), at particleCatchUp scaled by the receiving system's sensitivity
        public Vector3 Affect(Vector3 position, Vector3 particleVelocity, float sensitivity, float deltaTime)
        {
            if (!TryGetWindAt(position, out float windSpeed))
                return particleVelocity;

            Vector3 axis = Direction;
            float along = Vector3.Dot(particleVelocity, axis);
            if (along >= windSpeed)
                return particleVelocity;

            float blend = Mathf.Clamp01(particleCatchUp * sensitivity * deltaTime);
            return particleVelocity + axis * ((windSpeed - along) * blend);
        }

        // Wind speed along Direction at a world position, with falloff applied. False if the position is outside the
        // zone's collider (ClosestPoint returns the point itself when it's inside a convex collider).
        public bool TryGetWindAt(Vector3 position, out float windSpeed)
        {
            windSpeed = 0f;
            if ((_zone.ClosestPoint(position) - position).sqrMagnitude > 0.0001f)
                return false;

            windSpeed = velocity * CalculateFalloff(position);
            return true;
        }

        private void FixedUpdate()
        {
            UpdateBlocking();

            if (!IsBlowing || trackedObjects.Count == 0)
                return;

            Vector3 windDirection = Direction;

            foreach (GameObject tracked in trackedObjects)
            {
                // Destroyed or deactivated inside the zone (e.g. despawned to a pool) - there's no exit event for
                // that, so drop it here or a pooled object would still be pushed after respawning elsewhere
                if (tracked == null || !tracked.activeInHierarchy)
                {
                    _staleTracked.Add(tracked);
                    continue;
                }

                float falloff = CalculateFalloff(tracked.transform.position);

                if (tracked.TryGetComponent(out IWindReceiver receiver))
                {
                    WindData windData = new WindData
                    {
                        source = gameObject,
                        direction = windDirection,
                        velocity = velocity * falloff,
                        fullVelocity = velocity,
                        strength = strength,
                        onsetMode = onsetMode,
                        onsetAcceleration = onsetAcceleration
                    };

                    receiver.OnWind(windData, Time.fixedDeltaTime);
                }
                else if (pushRigidbodies && tracked.TryGetComponent(out Rigidbody body) && !body.isKinematic)
                {
                    PushRigidbody(body, velocity * falloff, windDirection);
                }
            }

            RemoveStaleTracked();
        }

        // Air drag toward the wind's speed along Direction, scaled so a body of exactly `strength` kg hovers at full
        // strength (drag * windSpeed = m*g) - see WindData.PushBody
        private void PushRigidbody(Rigidbody body, float windSpeed, Vector3 axis)
        {
            float drag = strength * Physics.gravity.magnitude / Mathf.Max(velocity, 0.01f);
            WindData.PushBody(body, axis, windSpeed, drag, Time.fixedDeltaTime);
        }

        private void RemoveStaleTracked()
        {
            if (_staleTracked.Count == 0)
                return;

            foreach (GameObject stale in _staleTracked)
                trackedObjects.Remove(stale);

            _staleTracked.Clear();
            RefreshTrackedState();
        }

        private float CalculateFalloff(Vector3 targetPosition)
        {
            if (_isReflection)
                return _reflectedFalloff.Evaluate(Vector3.Dot(targetPosition - transform.position, Direction));

            if (!useFalloff)
                return 1f;

            // Distance projected along the push direction only - lateral drift inside the column doesn't reduce the push, just how far along it you've travelled.
            float distanceAlongAxis = Vector3.Dot(targetPosition - Origin.position, Direction);
            float falloffDistance = MaxDistance;
            float normalizedDistance = falloffDistance > 0f ? Mathf.Clamp01(distanceAlongAxis / falloffDistance) : 0f;

            return Mathf.Clamp01(falloffCurve.Evaluate(normalizedDistance));
        }

        #region Blocking

        // The zone as a box in its own local space: which local axis the wind runs along (and which way), and the two
        // axes across it
        private struct ZoneFrame
        {
            public int LengthAxis;
            public float Sign;
            public int CrossA;
            public int CrossB;
        }

        private void CacheAuthoredZone()
        {
            _box = _zone as BoxCollider;
            if (_box == null)
                return;

            _authoredCenter = _box.center;
            _authoredSize = _box.size;
            currentLength = FullLength;
        }

        private ZoneFrame GetFrame()
        {
            Vector3 windDirection = Direction;
            int lengthAxis = 0;
            float bestDot = 0f;

            for (int i = 0; i < 3; i++)
            {
                float dot = Vector3.Dot(transform.TransformDirection(Axis(i)), windDirection);
                if (Mathf.Abs(dot) > Mathf.Abs(bestDot))
                {
                    bestDot = dot;
                    lengthAxis = i;
                }
            }

            return new ZoneFrame
            {
                LengthAxis = lengthAxis,
                Sign = bestDot >= 0f ? 1f : -1f,
                CrossA = (lengthAxis + 1) % 3,
                CrossB = (lengthAxis + 2) % 3
            };
        }

        // The zone's full (unblocked) length along the wind, in world units
        public float FullLength
        {
            get
            {
                if (_box == null) return 0f;
                ZoneFrame frame = GetFrame();
                return transform.TransformVector(Axis(frame.LengthAxis) * _authoredSize[frame.LengthAxis]).magnitude;
            }
        }

        // The zone's current length along the wind (shorter than FullLength while something blocks it)
        public float CurrentLength => currentLength;

        // Result of checking the zone's lanes for blockers. Per-lane detail is left in _laneOrigins/_laneDistances/
        // _laneKeys (the gizmos draw it).
        private struct BlockScan
        {
            public ZoneFrame Frame;
            public int LaneCount;
            public float FullLength;
            public Collider Blocker;    // nearest thing in the way, null = clear
            public float Nearest;       // its distance along the wind from the upwind face
            public float Coverage;      // fraction of lanes hitting that same blocker
            public Vector3 HitPoint;    // average hit point on it
            public float HitDistance;   // average distance to it along the wind from the upwind face
            public bool Stall;
            public float Length;        // how long the zone should be given all that
        }

        private void UpdateBlocking()
        {
            CurrentReflector = null;

            if (!IsBlockable || !active || !ScanForBlockers(out BlockScan scan))
            {
                coverage = 0f;
                SetBlocked(false, false, FullLength);
                return;
            }

            coverage = scan.Coverage;
            SetBlocked(scan.Blocker != null, scan.Stall, scan.Length);

            if (scan.Blocker == null || scan.Stall || _reflectionDepth >= WindReflector.MAX_CHAIN)
                return;

            WindReflector reflector = scan.Blocker.GetComponentInParent<WindReflector>();
            if (reflector == null)
                return;

            reflector.Receive(BuildIncoming(scan));
            CurrentReflector = reflector;
        }

        // The reflector this wind is blowing into right now (null if clear, stalled, or blocked by something that
        // just absorbs it) - WindVisual hands its particles on to that reflector's redirected wind
        internal WindReflector CurrentReflector { get; private set; }

        // Casts each lane along the wind from the upwind face and works out what (if anything) the zone stops at. No
        // side effects beyond the lane arrays, so the gizmos can run it in edit mode too. False if there's no box zone.
        private bool ScanForBlockers(out BlockScan scan)
        {
            scan = default;
            if (_box == null)
                return false;

            Vector3 windDirection = Direction;
            int samples = Mathf.Max(1, blockSamples);
            scan.Frame = GetFrame();
            scan.LaneCount = samples * samples;
            scan.FullLength = FullLength;
            scan.Nearest = float.MaxValue;
            scan.Length = scan.FullLength;

            for (int lane = 0; lane < scan.LaneCount; lane++)
            {
                _laneOrigins[lane] = LaneOrigin(scan.Frame, lane % samples, lane / samples, samples);
                _laneDistances[lane] = float.MaxValue;
                _laneKeys[lane] = null;

                if (!IsBlockable || !CastLane(_laneOrigins[lane] - windDirection * RAY_BACKOFF, windDirection, scan.FullLength + RAY_BACKOFF, out RaycastHit hit))
                    continue;

                float distance = Mathf.Max(0f, hit.distance - RAY_BACKOFF);
                _laneDistances[lane] = distance;
                _laneKeys[lane] = BlockerKey(hit.collider);

                if (distance < scan.Nearest)
                {
                    scan.Nearest = distance;
                    scan.Blocker = hit.collider;
                }
            }

            if (scan.Blocker == null)
                return true;

            // Partial cover: the whole zone stops at the blocker, and how much of the beam it covers scales anything
            // it redirects
            Object blockerKey = BlockerKey(scan.Blocker);
            Vector3 hitPointSum = Vector3.zero;
            float hitDistanceSum = 0f;
            int covered = 0;
            for (int lane = 0; lane < scan.LaneCount; lane++)
            {
                if (_laneKeys[lane] != blockerKey) continue;

                covered++;
                hitPointSum += _laneOrigins[lane] + windDirection * _laneDistances[lane];
                hitDistanceSum += _laneDistances[lane];
            }

            scan.Coverage = (float)covered / scan.LaneCount;
            scan.HitPoint = hitPointSum / covered;
            scan.HitDistance = hitDistanceSum / covered;
            scan.Stall = stallWhenBlocked && scan.Nearest <= stallDistance;
            scan.Length = scan.Stall ? MIN_LENGTH : Mathf.Min(scan.FullLength, scan.Nearest + BLOCKER_OVERLAP);
            return true;
        }

        private WindReflector.IncomingWind BuildIncoming(in BlockScan scan)
        {
            return new WindReflector.IncomingWind
            {
                Source = this,
                Point = scan.HitPoint,
                Direction = Direction,
                Speed = velocity * CalculateFalloff(scan.HitPoint) * scan.Coverage,
                CrossAxisA = transform.TransformDirection(Axis(scan.Frame.CrossA)),
                CrossSizeA = transform.TransformVector(Axis(scan.Frame.CrossA) * _authoredSize[scan.Frame.CrossA]).magnitude,
                CrossAxisB = transform.TransformDirection(Axis(scan.Frame.CrossB)),
                CrossSizeB = transform.TransformVector(Axis(scan.Frame.CrossB) * _authoredSize[scan.Frame.CrossB]).magnitude,
                Depth = _reflectionDepth,
                Coverage = scan.Coverage,
                Remaining = Mathf.Max(0f, scan.FullLength - scan.HitDistance),
                Falloff = FalloffContinuingFrom(scan.HitPoint)
            };
        }

        // This zone's falloff, as it carries on past a point - so an exact reflection keeps fading the same way
        private WindReflector.ReflectedFalloff FalloffContinuingFrom(Vector3 point)
        {
            if (_isReflection)
            {
                WindReflector.ReflectedFalloff continued = _reflectedFalloff;
                continued.StartDistance += Mathf.Max(0f, Vector3.Dot(point - transform.position, Direction));
                return continued;
            }

            return new WindReflector.ReflectedFalloff
            {
                Use = useFalloff,
                Curve = falloffCurve,
                MaxDistance = MaxDistance,
                StartDistance = Mathf.Max(0f, Vector3.Dot(point - Origin.position, Direction))
            };
        }

        // For a reflector's gizmo: what this zone would currently send into it (works in edit mode too)
        internal bool TryPredictIncoming(WindReflector reflector, out WindReflector.IncomingWind incoming)
        {
            incoming = default;
            EnsureZoneCached();

            if (!IsBlockable || !ScanForBlockers(out BlockScan scan) || scan.Blocker == null || scan.Stall)
                return false;
            if (_reflectionDepth >= WindReflector.MAX_CHAIN || scan.Blocker.GetComponentInParent<WindReflector>() != reflector)
                return false;

            incoming = BuildIncoming(scan);
            return true;
        }

        // Awake hasn't run in edit mode - read the zone straight from the collider (it's never shortened there)
        private void EnsureZoneCached()
        {
            if (Application.isPlaying)
                return;

            _zone = GetComponent<Collider>();
            CacheAuthoredZone();
        }

        // Start of a lane on the zone's upwind face, spread across its cross-section (inset slightly from the edges)
        private Vector3 LaneOrigin(ZoneFrame frame, int indexA, int indexB, int samples)
        {
            float ta = samples == 1 ? 0f : Mathf.Lerp(-1f, 1f, (float)indexA / (samples - 1));
            float tb = samples == 1 ? 0f : Mathf.Lerp(-1f, 1f, (float)indexB / (samples - 1));
            const float EDGE_INSET = 0.9f;

            Vector3 local = _authoredCenter
                            - Axis(frame.LengthAxis) * (frame.Sign * _authoredSize[frame.LengthAxis] * 0.5f)
                            + Axis(frame.CrossA) * (ta * _authoredSize[frame.CrossA] * 0.5f * EDGE_INSET)
                            + Axis(frame.CrossB) * (tb * _authoredSize[frame.CrossB] * 0.5f * EDGE_INSET);

            return transform.TransformPoint(local);
        }

        private bool CastLane(Vector3 laneOrigin, Vector3 windDirection, float length, out RaycastHit nearestHit)
        {
            nearestHit = default;
            int count = Physics.RaycastNonAlloc(laneOrigin, windDirection, _rayHits, length, blockedBy, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _rayHits[i];
                if (hit.distance >= best) continue;

                // Redirected wind starts at its own reflector's surface - never let it block itself
                if (_ignoreReflector != null && hit.collider.GetComponentInParent<WindReflector>() == _ignoreReflector) continue;

                best = hit.distance;
                nearestHit = hit;
            }

            return best < float.MaxValue;
        }

        // Lanes hitting the same reflector (or, without one, the same collider) count as the same blocker
        private static Object BlockerKey(Collider collider)
        {
            WindReflector reflector = collider.GetComponentInParent<WindReflector>();
            return reflector != null ? reflector : collider;
        }

        private void SetBlocked(bool isObstructed, bool isStalled, float length)
        {
            if (isObstructed != obstructed)
            {
                obstructed = isObstructed;
                if (isObstructed) onObstructed?.Activate();
                else onCleared?.Activate();
            }

            if (isStalled != stalled)
            {
                stalled = isStalled;
                if (isStalled) onStalled?.Activate();
                else onResumed?.Activate();
            }

            SetLength(length);
        }

        // Shortens (or restores) the box along the wind, keeping its upwind face where it is
        private void SetLength(float worldLength)
        {
            if (_box == null)
                return;

            float fullLength = FullLength;
            worldLength = Mathf.Clamp(worldLength, MIN_LENGTH, fullLength);
            if (Mathf.Abs(worldLength - currentLength) < 0.001f && ZoneVersion > 0)
                return;

            ZoneFrame frame = GetFrame();
            int axis = frame.LengthAxis;
            float fullLocal = _authoredSize[axis];
            float localLength = fullLength > 0f ? fullLocal * (worldLength / fullLength) : fullLocal;

            Vector3 size = _authoredSize;
            size[axis] = localLength;

            Vector3 center = _authoredCenter;
            center[axis] += frame.Sign * (localLength - fullLocal) * 0.5f;

            _box.size = size;
            _box.center = center;
            currentLength = worldLength;
            ZoneVersion++;
        }

        private static Vector3 Axis(int index) => index switch
        {
            0 => Vector3.right,
            1 => Vector3.up,
            _ => Vector3.forward
        };

        #endregion

        #region Fitting the collider

        // The BoxCollider as Length and Width x Depth say it should be: its upwind face centred on this object's pivot,
        // extending Length along Direction. Sizes are world metres, divided through by this object's scale.
        private bool TryGetFittedBox(out BoxCollider box, out Vector3 size, out Vector3 center)
        {
            size = center = Vector3.zero;
            box = GetComponent<BoxCollider>();
            if (box == null)
                return false;

            ZoneFrame frame = GetFrame();
            int depthAxis = DepthAxis(frame);
            int widthAxis = depthAxis == frame.CrossA ? frame.CrossB : frame.CrossA;
            Vector3 scale = transform.lossyScale;

            size[frame.LengthAxis] = length / ScaleOn(scale, frame.LengthAxis);
            size[widthAxis] = Mathf.Max(0.05f, crossSection.x) / ScaleOn(scale, widthAxis);
            size[depthAxis] = Mathf.Max(0.05f, crossSection.y) / ScaleOn(scale, depthAxis);
            center[frame.LengthAxis] = frame.Sign * size[frame.LengthAxis] * 0.5f;
            return true;
        }

        // Which of the zone's two cross axes is Depth: the one running into the screen (world Z) - or, for wind that
        // itself blows along Z, the vertical one
        private int DepthAxis(ZoneFrame frame)
        {
            Vector3 reference = Mathf.Abs(Vector3.Dot(Direction, Vector3.forward)) > 0.7f ? Vector3.up : Vector3.forward;
            float a = Mathf.Abs(Vector3.Dot(transform.TransformDirection(Axis(frame.CrossA)), reference));
            float b = Mathf.Abs(Vector3.Dot(transform.TransformDirection(Axis(frame.CrossB)), reference));
            return a >= b ? frame.CrossA : frame.CrossB;
        }

        private static float ScaleOn(Vector3 scale, int axis) => Mathf.Max(Mathf.Abs(scale[axis]), 0.0001f);

        // A box can't turn within its object, so a fitted zone only lines up with wind along one of its local axes
        private bool IsFitOffAxis()
        {
            if (!fitColliderToWind)
                return false;

            Vector3 local = transform.InverseTransformDirection(Direction).normalized;
            return Mathf.Max(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z)) < 0.999f;
        }

        // Switching fitting on keeps the zone's current size (read off the collider) - it just moves to start at the pivot
        private void OnFitToggled()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (!fitColliderToWind || box == null)
                return;

            ZoneFrame frame = GetFrame();
            int depthAxis = DepthAxis(frame);
            int widthAxis = depthAxis == frame.CrossA ? frame.CrossB : frame.CrossA;
            Vector3 scale = transform.lossyScale;

            length = Mathf.Max(0.05f, box.size[frame.LengthAxis] * Mathf.Abs(scale[frame.LengthAxis]));
            crossSection = new Vector2(box.size[widthAxis] * Mathf.Abs(scale[widthAxis]), box.size[depthAxis] * Mathf.Abs(scale[depthAxis]));
        }

        private static bool Matches(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 0.000001f;

        [Button("Fit Collider To Wind")]
        [ShowIf(nameof(fitColliderToWind))]
        private void FitColliderButton()
        {
#if UNITY_EDITOR
            ApplyEditorFit();
#endif
        }

#if UNITY_EDITOR
        private bool _editorFitScheduled;

        private void OnValidate()
        {
            crossSection = Vector2.Max(crossSection, new Vector2(0.05f, 0.05f));

            if (!fitColliderToWind)
                return;

            if (Application.isPlaying)
            {
                // Tuning in play mode: refit the full zone (blocking shortens it again next step) and let WindVisual refit
                if (_zone == null || !TryGetFittedBox(out BoxCollider box, out Vector3 size, out Vector3 center)) return;
                if (Matches(size, _authoredSize) && Matches(center, _authoredCenter)) return;

                box.size = size;
                box.center = center;
                CacheAuthoredZone();
                ZoneVersion++;
                return;
            }

            ScheduleEditorFit();
        }

        // Changing the collider straight from OnValidate/gizmo drawing isn't safe - do it just after
        private void ScheduleEditorFit()
        {
            if (_editorFitScheduled)
                return;

            _editorFitScheduled = true;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                _editorFitScheduled = false;
                if (fitColliderToWind && !Application.isPlaying) ApplyEditorFit();
            };
        }

        private void ApplyEditorFit()
        {
            if (!TryGetFittedBox(out BoxCollider box, out Vector3 size, out Vector3 center))
                return;
            if (Matches(size, box.size) && Matches(center, box.center))
                return;

            UnityEditor.Undo.RecordObject(box, "Fit Wind Collider");
            box.size = size;
            box.center = center;

            if (TryGetComponent(out WindVisual visual))
                visual.FitToZone();
        }

        // Rotating (with World-space Direction) or scaling the object changes the fit without an OnValidate
        private void RefitIfTransformChanged()
        {
            if (Application.isPlaying || !fitColliderToWind || !TryGetFittedBox(out BoxCollider box, out Vector3 size, out Vector3 center))
                return;
            if (!Matches(size, box.size) || !Matches(center, box.center))
                ScheduleEditorFit();
        }
#endif

        #endregion

        #region Reflection zones

        // Turns this zone into a WindReflector's redirected wind: copies the source's behaviour, blows along its own
        // forward, and ignores its reflector when checking for blockers
        internal void InitialiseAsReflection(WindTrigger3D source, WindReflector reflector, int depth)
        {
            _ignoreReflector = reflector;
            _reflectionDepth = depth;
            _isReflection = true;
            fitColliderToWind = false;

            direction = Vector3.forward;
            directionSpace = Space.Self;
            onsetMode = source.onsetMode;
            onsetAcceleration = source.onsetAcceleration;
            pushRigidbodies = source.pushRigidbodies;
            pushParticles = source.pushParticles;
            particleCatchUp = source.particleCatchUp;
            blockedBy = source.blockedBy;
            blockSamples = source.blockSamples;
            detectionMask = source.detectionMask;
            stallWhenBlocked = false;
            origin = null;
            drawGizmo = source.drawGizmo;
        }

        // Updates the redirected wind each physics step. Size is (across, across, length) in world units along this
        // object's local X, Y and Z (it blows along local Z from its base at this transform's position). Velocity is
        // the speed at the base; the falloff fades it from there.
        internal void UpdateReflection(float reflectedVelocity, float reflectedStrength, Vector3 size, in WindReflector.ReflectedFalloff falloff)
        {
            velocity = reflectedVelocity;
            strength = reflectedStrength;
            _reflectedFalloff = falloff;
            useFalloff = falloff.Use;
            falloffCurve = falloff.Curve;
            maxDistance = Mathf.Max(0.01f, falloff.MaxDistance - falloff.StartDistance);

            if (_box == null)
                CacheAuthoredZone();

            Vector3 authoredCenter = new(0f, 0f, size.z * 0.5f);
            if (_box != null && (size != _authoredSize || authoredCenter != _authoredCenter))
            {
                _authoredSize = size;
                _authoredCenter = authoredCenter;
                _box.size = size;
                _box.center = authoredCenter;
                currentLength = size.z;
                ZoneVersion++;
            }

            active = true;
        }

        internal void StopReflection()
        {
            active = false;
        }

        #endregion

#if UNITY_EDITOR
        internal static readonly Color GizmoWindColor = new(0.25f, 0.8f, 1f);
        internal static readonly Color GizmoBlockedColor = new(1f, 0.5f, 0.1f);
        internal static readonly Color GizmoStallColor = new(1f, 0.2f, 0.2f);
        private static GUIStyle _gizmoLabelStyle;

        private void OnDrawGizmos()
        {
            if (drawGizmo && gizmoWhenNotSelected)
                DrawWindGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            if (drawGizmo && !gizmoWhenNotSelected)
                DrawWindGizmos();
        }

        // Shows the wind as it would actually blow right now - including where a blocker stops it and, through a
        // reflector, where it bends. Runs the same scan as the game, so it previews correctly in edit mode too.
        private void DrawWindGizmos()
        {
            RefitIfTransformChanged();
            EnsureZoneCached();
            if (_zone == null)
                return;

            Vector3 windDirection = Direction;

            if (useFalloff)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(Origin.position + windDirection * MaxDistance, 0.15f);
            }

            if (_box == null)
            {
                // Not a box - no blocking, just show its extent and direction
                Gizmos.color = GizmoWindColor;
                Gizmos.DrawWireCube(_zone.bounds.center, _zone.bounds.size);
                DrawGizmoArrow(_zone.bounds.center, windDirection, Mathf.Max(1f, _zone.bounds.extents.magnitude), GizmoWindColor);
                return;
            }

            ScanForBlockers(out BlockScan scan);
            bool blocked = scan.Blocker != null;
            bool willStall = blocked && scan.Stall;

            // In play the box really is shortened; in edit mode show where it would be
            float length = Application.isPlaying ? currentLength : scan.Length;
            bool isBlowing = Application.isPlaying ? IsBlowing : active && !willStall;

            // The whole authored zone, faintly, then the part actually blowing
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = WithAlpha(GizmoWindColor, 0.18f);
            Gizmos.DrawWireCube(_authoredCenter, _authoredSize);

            GetZoneBox(scan.Frame, length, out Vector3 localCenter, out Vector3 localSize);
            Color zoneColor = !isBlowing ? GizmoStallColor : GizmoWindColor;
            Gizmos.color = WithAlpha(zoneColor, 0.06f);
            Gizmos.DrawCube(localCenter, localSize);
            Gizmos.color = WithAlpha(zoneColor, 0.9f);
            Gizmos.DrawWireCube(localCenter, localSize);
            Gizmos.matrix = Matrix4x4.identity;

            // Flow along each lane, fading with falloff, ending at what it hits
            for (int lane = 0; lane < scan.LaneCount; lane++)
            {
                bool laneHit = _laneKeys[lane] != null && _laneDistances[lane] <= length;
                float laneLength = laneHit ? _laneDistances[lane] : length;
                DrawFlowLine(_laneOrigins[lane], windDirection, laneLength, !laneHit && isBlowing);

                if (laneHit)
                {
                    Gizmos.color = GizmoBlockedColor;
                    Gizmos.DrawSphere(_laneOrigins[lane] + windDirection * laneLength, 0.06f);
                }
            }

            if (stallWhenBlocked && IsBlockable)
            {
                // Anything reaching this slice stalls the source
                Gizmos.matrix = transform.localToWorldMatrix;
                GetZoneBox(scan.Frame, stallDistance, out Vector3 stallCenter, out Vector3 stallSize);
                Gizmos.color = WithAlpha(GizmoStallColor, 0.5f);
                Gizmos.DrawWireCube(stallCenter, stallSize);
                Gizmos.matrix = Matrix4x4.identity;
            }

            string label = $"{name}: {velocity:0.#} m/s";
            if (!blocked)
            {
                label += $" · {length:0.0} m";
            }
            else
            {
                label += willStall
                    ? $"\nSTALLED by {scan.Blocker.name}"
                    : $"\nBlocked by {scan.Blocker.name} at {scan.Nearest:0.0} m · covers {scan.Coverage:P0}";

                WindReflector reflector = scan.Blocker.GetComponentInParent<WindReflector>();
                if (reflector != null && !willStall && _reflectionDepth < WindReflector.MAX_CHAIN)
                {
                    WindReflector.IncomingWind incoming = BuildIncoming(scan);
                    label += $"\n→ hits at {incoming.Speed:0.#} m/s, redirected by {reflector.name}";
                    reflector.DrawRedirectGizmo(incoming);
                }
            }

            Vector3 labelPoint = transform.TransformPoint(localCenter) + windDirection * (length * 0.5f);
            DrawGizmoLabel(labelPoint, label);
        }

        // The box (local space) for the zone at a given length along the wind, keeping its upwind face in place
        private void GetZoneBox(ZoneFrame frame, float worldLength, out Vector3 center, out Vector3 size)
        {
            int axis = frame.LengthAxis;
            float fullLength = FullLength;
            float fullLocal = _authoredSize[axis];
            float localLength = fullLength > 0f ? fullLocal * (Mathf.Clamp(worldLength, MIN_LENGTH, fullLength) / fullLength) : fullLocal;

            size = _authoredSize;
            size[axis] = localLength;
            center = _authoredCenter;
            center[axis] += frame.Sign * (localLength - fullLocal) * 0.5f;
        }

        // A lane's line, shaded by the falloff strength along it (bright = strong)
        private void DrawFlowLine(Vector3 start, Vector3 windDirection, float length, bool arrowHead)
        {
            const int SEGMENTS = 6;
            for (int i = 0; i < SEGMENTS; i++)
            {
                Vector3 a = start + windDirection * (length * i / SEGMENTS);
                Vector3 b = start + windDirection * (length * (i + 1) / SEGMENTS);
                float strengthHere = CalculateFalloff((a + b) * 0.5f);
                Gizmos.color = Color.Lerp(WithAlpha(GizmoWindColor, 0.15f), GizmoWindColor, strengthHere);
                Gizmos.DrawLine(a, b);
            }

            if (arrowHead)
                DrawGizmoArrowHead(start + windDirection * length, windDirection, 0.15f);
        }

        internal static void DrawGizmoArrow(Vector3 from, Vector3 direction, float length, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(from, from + direction * length);
            DrawGizmoArrowHead(from + direction * length, direction, Mathf.Min(0.3f, length * 0.25f));
        }

        internal static void DrawGizmoArrowHead(Vector3 tip, Vector3 direction, float size)
        {
            Vector3 side = Vector3.Cross(direction, Mathf.Abs(direction.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 back = tip - direction * size;
            Gizmos.DrawLine(tip, back + side * (size * 0.5f));
            Gizmos.DrawLine(tip, back - side * (size * 0.5f));
        }

        internal static void DrawGizmoLabel(Vector3 position, string text)
        {
            _gizmoLabelStyle ??= new GUIStyle(UnityEditor.EditorStyles.helpBox) { fontSize = 10, richText = false };
            UnityEditor.Handles.Label(position, text, _gizmoLabelStyle);
        }

        internal static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
#endif
    }
}
