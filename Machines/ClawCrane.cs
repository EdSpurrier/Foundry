using System;
using System.Collections;
using System.Collections.Generic;
using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Machines
{
    // A claw on a cable, hanging from a trolley that runs along a rail - a background machine that tidies up. It waits
    // for a loose Rigidbody (on Load Layers) to come to rest inside the Pickup Area, runs over it, lowers the claw onto
    // it, closes its fingers, lifts it, carries it along the rail to the Drop Point, sets it down on whatever is there
    // (so loads stack into a pile), lets go and goes back home. Then it waits for the next one.
    //
    // It's all movement, no physics of its own: the trolley, head, cable and fingers are plain transforms it places
    // every frame (give them no colliders), and a carried load is made kinematic and parented to the head until it's
    // let go. Build the rig in the scene (any look you like): this object holds the rail ends; the trolley, head, cable
    // and fingers are assigned below. Their starting poses don't matter - the head hangs under the trolley at Home Drop.
    public class ClawCrane : MonoBehaviour
    {
        public enum State { Idle, ToPickup, Lowering, Gripping, Lifting, ToDrop, Dropping, Releasing, Raising, ToHome }

        [Serializable]
        public class Finger
        {
            public Transform finger;
            [Tooltip("Local rotation when open.")] public Vector3 openEuler;
            [Tooltip("Local rotation when closed on a load.")] public Vector3 closedEuler;
        }

        [Title("Rig")]
        [Tooltip("Runs along the rail; the cable hangs from it.")]
        [SerializeField, Required] private Transform trolley;

        [Tooltip("Hangs on the cable and carries the load (the fingers are usually its children).")]
        [SerializeField, Required] private Transform head;

        [Tooltip("Optional: the cable or chain - a piece 1 m tall with its pivot in the middle (e.g. a thin box), stretched between trolley and head every frame.")]
        [SerializeField] private Transform cable;

        [ShowIf(nameof(cable))]
        [Tooltip("The cable starts this far below the trolley's pivot and ends this far above the head's.")]
        [SerializeField] private Vector2 cableInset = new(0.2f, 0.2f);

        [Tooltip("Where the top of a load sits when it's gripped - a child of the head at its underside. Empty = the head's pivot.")]
        [SerializeField] private Transform grabPoint;

        [SerializeField] private List<Finger> fingers = new();

        [Title("Rail")]
        [SerializeField, Required] private Transform railStart;
        [SerializeField, Required] private Transform railEnd;

        [Tooltip("Where it parks along the rail (0 = Rail Start, 1 = Rail End).")]
        [SerializeField, Range(0f, 1f)] private float home;

        [Tooltip("Cable length (m) while parked or travelling empty.")]
        [SerializeField, Min(0.1f)] private float homeDrop = 2f;

        [Title("Pick Up")]
        [Tooltip("Loads resting inside this collider's bounds get picked up (a trigger box is handy).")]
        [SerializeField, Required] private Collider pickupArea;

        [SerializeField] private LayerMask loadLayers;

        [Tooltip("A load counts as resting when it's slower than this (m/s)...")]
        [SuffixLabel("m/s", Overlay = true)]
        [SerializeField, Min(0.01f)] private float settleSpeed = 0.3f;

        [Tooltip("...for this long (s).")]
        [SuffixLabel("s", Overlay = true)]
        [SerializeField, Min(0f)] private float settleTime = 0.75f;

        [Title("Drop")]
        [Tooltip("Where loads are set down (a pile builds up here).")]
        [SerializeField, Required] private Transform dropPoint;

        [Tooltip("Each drop lands up to this far (m) either side of the Drop Point along the rail, so the loads make a pile rather than a tower.")]
        [SerializeField, Min(0f)] private float dropSpread = 0.6f;

        [Tooltip("Gap (m) left under the load when it's let go.")]
        [SerializeField, Min(0f)] private float dropGap = 0.05f;

        [Tooltip("What a load can be set down on (the floor, other loads).")]
        [SerializeField] private LayerMask dropSurfaceLayers = ~0;

        [Title("Motion")]
        [SuffixLabel("m/s", Overlay = true)]
        [SerializeField, Min(0.1f)] private float travelSpeed = 4f;

        [Tooltip("How fast the cable reels in and out.")]
        [SuffixLabel("m/s", Overlay = true)]
        [SerializeField, Min(0.1f)] private float cableSpeed = 3f;

        [Tooltip("How high (m) it lifts a load before carrying it off (it also lifts it high enough to clear the pile).")]
        [SerializeField, Min(0f)] private float liftHeight = 3f;

        [Tooltip("Time (s) the fingers take to open or close.")]
        [SuffixLabel("s", Overlay = true)]
        [SerializeField, Min(0.01f)] private float gripTime = 0.4f;

        [Tooltip("A short pause (s) between moves, so it reads as a machine.")]
        [SuffixLabel("s", Overlay = true)]
        [SerializeField, Min(0f)] private float pauseTime = 0.25f;

        [Title("Events")]
        [SerializeField, HideLabel] private FrameCoreEvent onGrab = new() { eventName = "Claw Crane - Grab" };
        [SerializeField, HideLabel] private FrameCoreEvent onRelease = new() { eventName = "Claw Crane - Release" };
        [SerializeField, HideLabel] private FrameCoreEvent onHome = new() { eventName = "Claw Crane - Home" };

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private State state;

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private Rigidbody load;

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private int delivered;

        private readonly Dictionary<Rigidbody, float> _restingFor = new();
        private readonly Collider[] _overlaps = new Collider[16];
        private float _railT;
        private float _cableLength;
        private float _grip;          // 0 = open, 1 = closed
        private float _grabOffset;    // head pivot -> grab point (down, m)
        private Transform _loadParent;
        private RigidbodyInterpolation _loadInterpolation;

        public State CurrentState => state;
        public Rigidbody Load => load;
        public int Delivered => delivered;

        private float RailLength => Vector3.Distance(railStart.position, railEnd.position);

        private void Awake()
        {
            _railT = home;
            _cableLength = homeDrop;
            _grip = 0f;
            _grabOffset = grabPoint != null ? head.position.y - grabPoint.position.y : 0f;
            ApplyRig();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
                StartCoroutine(Run());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (load != null)
                LetGo();
        }

        // --- The job, over and over

        private IEnumerator Run()
        {
            while (true)
            {
                state = State.Idle;
                _restingFor.Clear();
                Rigidbody target = null;
                while (target == null)
                {
                    yield return null;
                    target = FindRestingLoad(Time.deltaTime);
                }

                // Over it
                state = State.ToPickup;
                Vector3 planned = target.worldCenterOfMass;
                yield return MoveAlongRail(RailT(planned));
                if (!StillThere(target, planned)) { yield return GoHome(); continue; }
                yield return Pause();

                // Down onto it - following its top as it settles
                state = State.Lowering;
                bool lost = false;
                while (true)
                {
                    if (!StillThere(target, planned)) { lost = true; break; }
                    float goal = CableToReach(TopOf(target) + _grabOffset);
                    _cableLength = Mathf.MoveTowards(_cableLength, goal, cableSpeed * Time.deltaTime);
                    ApplyRig();
                    if (Mathf.Approximately(_cableLength, goal)) break;
                    yield return null;
                }
                if (lost) { yield return GoHome(); continue; }

                // Grab
                state = State.Gripping;
                yield return Grip(1f);
                if (!StillThere(target, planned)) { yield return Grip(0f); yield return GoHome(); continue; }
                Take(target);
                onGrab?.Activate();
                yield return Pause();

                // Up a little, and high enough to clear the pile
                state = State.Lifting;
                float dropT = RailT(dropPoint.position + RailDirection * UnityEngine.Random.Range(-dropSpread, dropSpread));
                Vector3 dropAt = RailPoint(dropT);
                float loadBottom = head.position.y - BoundsOf(load).min.y;      // head pivot -> load bottom (down, m)
                float surface = SurfaceBelow(new Vector3(dropAt.x, 0f, dropAt.z), BoundsOf(load).extents);
                float carryHead = Mathf.Max(head.position.y + liftHeight, surface + loadBottom + 0.75f);
                yield return ReelTo(CableToReach(carryHead));

                // Along to the pile
                state = State.ToDrop;
                yield return MoveAlongRail(dropT);
                yield return Pause();

                // Down onto the pile (re-measured now we're over it)
                state = State.Dropping;
                surface = SurfaceBelow(new Vector3(dropAt.x, 0f, dropAt.z), BoundsOf(load).extents);
                yield return ReelTo(CableToReach(surface + dropGap + loadBottom));

                // Let go
                state = State.Releasing;
                LetGo();
                yield return Grip(0f);
                onRelease?.Activate();
                delivered++;
                yield return Pause();

                yield return GoHome();
                onHome?.Activate();
            }
        }

        private IEnumerator GoHome()
        {
            state = State.Raising;
            yield return ReelTo(homeDrop);
            state = State.ToHome;
            yield return MoveAlongRail(home);
        }

        private IEnumerator MoveAlongRail(float t)
        {
            float length = Mathf.Max(RailLength, 0.01f);
            while (!Mathf.Approximately(_railT, t))
            {
                _railT = Mathf.MoveTowards(_railT, t, travelSpeed / length * Time.deltaTime);
                ApplyRig();
                yield return null;
            }
        }

        private IEnumerator ReelTo(float length)
        {
            while (!Mathf.Approximately(_cableLength, length))
            {
                _cableLength = Mathf.MoveTowards(_cableLength, length, cableSpeed * Time.deltaTime);
                ApplyRig();
                yield return null;
            }
        }

        private IEnumerator Grip(float to)
        {
            while (!Mathf.Approximately(_grip, to))
            {
                _grip = Mathf.MoveTowards(_grip, to, Time.deltaTime / gripTime);
                ApplyRig();
                yield return null;
            }
        }

        private IEnumerator Pause()
        {
            if (pauseTime > 0f)
                yield return new WaitForSeconds(pauseTime);
        }

        // --- Loads

        // The first load that has rested in the pickup area for Settle Time
        private Rigidbody FindRestingLoad(float dt)
        {
            Bounds area = pickupArea.bounds;
            int count = Physics.OverlapBoxNonAlloc(area.center, area.extents, _overlaps, Quaternion.identity, loadLayers, QueryTriggerInteraction.Ignore);

            Rigidbody found = null;
            for (int i = 0; i < count; i++)
            {
                Rigidbody body = _overlaps[i].attachedRigidbody;
                if (body == null || body.isKinematic) continue;

                float rest = body.linearVelocity.magnitude < settleSpeed && body.angularVelocity.magnitude < settleSpeed * 2f
                    ? (_restingFor.TryGetValue(body, out float r) ? r : 0f) + dt
                    : 0f;
                _restingFor[body] = rest;
                if (rest >= settleTime && found == null)
                    found = body;
            }
            return found;
        }

        private bool StillThere(Rigidbody target, Vector3 planned)
        {
            if (target == null || target.isKinematic) return false;
            Vector3 offset = target.worldCenterOfMass - planned;
            offset.y = 0f;
            return offset.magnitude < 0.75f && pickupArea.bounds.Contains(target.worldCenterOfMass);
        }

        private void Take(Rigidbody body)
        {
            load = body;
            _loadParent = body.transform.parent;
            _loadInterpolation = body.interpolation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.interpolation = RigidbodyInterpolation.None;
            body.isKinematic = true;
            body.transform.SetParent(head, true);
        }

        private void LetGo()
        {
            Rigidbody body = load;
            load = null;
            if (body == null) return;
            body.transform.SetParent(_loadParent, true);
            body.isKinematic = false;
            body.interpolation = _loadInterpolation;
            body.linearVelocity = Vector3.zero;
            body.WakeUp();
        }

        private static Bounds BoundsOf(Rigidbody body)
        {
            Bounds bounds = new(body.worldCenterOfMass, Vector3.zero);
            bool any = false;
            foreach (Collider c in body.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger || c.attachedRigidbody != body) continue;
                if (!any) { bounds = c.bounds; any = true; }
                else bounds.Encapsulate(c.bounds);
            }
            return bounds;
        }

        private static float TopOf(Rigidbody body) => BoundsOf(body).max.y;

        // Highest surface under a load-sized footprint at this spot (ignoring the load and the crane itself)
        private float SurfaceBelow(Vector3 at, Vector3 extents)
        {
            float best = float.NegativeInfinity;
            float fromY = trolley.position.y - 0.25f;
            Vector2[] spots = { Vector2.zero, new(1f, 1f), new(-1f, 1f), new(1f, -1f), new(-1f, -1f) };
            foreach (Vector2 s in spots)
            {
                Vector3 origin = new(at.x + s.x * extents.x * 0.9f, fromY, at.z + s.y * extents.z * 0.9f);
                foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, fromY + 100f, dropSurfaceLayers, QueryTriggerInteraction.Ignore))
                {
                    if (load != null && hit.rigidbody == load) continue;
                    if (hit.transform.IsChildOf(transform)) continue;
                    best = Mathf.Max(best, hit.point.y);
                }
            }
            return float.IsNegativeInfinity(best) ? at.y : best;
        }

        // --- Rig

        private Vector3 RailDirection => (railEnd.position - railStart.position).normalized;

        private Vector3 RailPoint(float t) => Vector3.Lerp(railStart.position, railEnd.position, t);

        // Where along the rail is over this point
        private float RailT(Vector3 point)
        {
            Vector3 a = railStart.position, ab = railEnd.position - a;
            Vector3 flat = new(ab.x, 0f, ab.z);
            if (flat.sqrMagnitude < 1e-6f) return 0f;
            Vector3 ap = point - a;
            return Mathf.Clamp01(Vector3.Dot(new Vector3(ap.x, 0f, ap.z), flat) / flat.sqrMagnitude);
        }

        // Cable length that puts the head's pivot at this height
        private float CableToReach(float headY) => Mathf.Max(0.3f, trolley.position.y - headY);

        private void ApplyRig()
        {
            if (trolley == null || head == null || railStart == null || railEnd == null) return;

            trolley.position = RailPoint(_railT);
            head.position = trolley.position + Vector3.down * _cableLength;

            if (cable != null)
            {
                Vector3 top = trolley.position + Vector3.down * cableInset.x;
                Vector3 bottom = head.position + Vector3.up * cableInset.y;
                cable.position = (top + bottom) * 0.5f;
                Vector3 scale = cable.localScale;
                scale.y = Mathf.Max(0.01f, top.y - bottom.y);
                cable.localScale = scale;
            }

            foreach (Finger f in fingers)
                if (f.finger != null)
                    f.finger.localRotation = Quaternion.Slerp(Quaternion.Euler(f.openEuler), Quaternion.Euler(f.closedEuler), _grip);
        }

        private void OnValidate()
        {
            if (!Application.isPlaying && trolley != null && head != null && railStart != null && railEnd != null)
            {
                _railT = home;
                _cableLength = homeDrop;
                _grip = 0f;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (railStart != null && railEnd != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(railStart.position, railEnd.position);
                Gizmos.DrawWireSphere(RailPoint(home), 0.25f);
            }
            if (pickupArea != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(pickupArea.bounds.center, pickupArea.bounds.size);
            }
            if (dropPoint != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(dropPoint.position, 0.3f);
                if (railStart != null && railEnd != null)
                    Gizmos.DrawLine(dropPoint.position - RailDirection * dropSpread, dropPoint.position + RailDirection * dropSpread);
            }
        }
    }
}
