using System;
using System.Collections.Generic;
using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Ropes
{
    // A physics rope, cable, cord or string: a chain of small jointed segments that sags and swings, drawn as a line.
    // It hangs from this object's position - to a Rigidbody (a crate, a cage, a counterweight: it swings on the rope),
    // to a fixed End Anchor (a cable strung across a gap), or loose. Striking a segment (e.g. the chicken pecking it)
    // cuts the rope there, after Strikes To Cut - whatever hung from it falls, and both ends react.
    //
    // Built when the game starts (the gizmo previews it). Keep the rope object itself still - its segments are its
    // children; to hang it from something that moves, give that something a Rigidbody and make it this object's parent.
    public class Rope : MonoBehaviour
    {
        [Title("Shape")]
        [Tooltip("What hangs from the end: a Rigidbody (swings on the rope, falls when it's cut). Leave empty for a fixed End Anchor or a loose end.")]
        [SerializeField] private Rigidbody endBody;

        [ShowIf(nameof(endBody))]
        [Tooltip("Tie the rope to the surface of the End Body nearest the rope (e.g. the top of a crate). Off = to its centre.")]
        [SerializeField] private bool attachToSurface = true;

        [HideIf(nameof(endBody))]
        [Tooltip("A fixed point the far end is tied to (a cable across a gap). Leave empty (and no End Body) for a rope hanging loose.")]
        [SerializeField] private Transform endAnchor;

        [HideIf(nameof(HasEnd))]
        [Tooltip("Length (m) of a loose rope, hanging straight down.")]
        [SerializeField, Min(0.1f)] private float length = 3f;

        [ShowIf(nameof(HasEnd))]
        [Tooltip("Extra length beyond the straight distance to the end, so it sags: 1 = taut, 1.2 = 20% longer.")]
        [SerializeField, Range(1f, 2f)] private float slack = 1.05f;

        [Tooltip("Length (m) of each segment. Shorter = smoother and more bendy, but more physics objects.")]
        [SerializeField, Range(0.05f, 1f)] private float segmentLength = 0.25f;

        [Tooltip("Keep it swinging in the X/Y plane only (2.5D). Off = it swings in 3D.")]
        [SerializeField] private bool planar = true;

        [Title("Physics")]
        [Tooltip("Mass (kg) of each segment. Much lighter than what hangs from it can make it stretch and jitter - raise Solver Iterations, or the segments' mass.")]
        [SerializeField, Min(0.001f)] private float segmentMass = 0.1f;

        [SerializeField, Min(0f)] private float segmentDrag = 0.1f;

        [Tooltip("Physics accuracy per segment - higher holds heavy loads more steadily.")]
        [SerializeField, Range(4, 60)] private int solverIterations = 20;

        [Tooltip("How hard a strike (e.g. a peck) swings the rope: the most speed (m/s) it gives the segment it hits. A peck's full launch would yank the light segment and upset the whole chain.")]
        [SuffixLabel("m/s", Overlay = true)]
        [SerializeField, Min(0f)] private float strikeSwing = 1.5f;

        [Tooltip("Collide with the world (bump into walls, drape over things). Off = the segments are triggers: cheaper, never snags on anything, and can still be pecked.")]
        [SerializeField] private bool collideWithWorld;

        [Tooltip("Collider radius (m) of each segment - also how easy it is to peck.")]
        [SerializeField, Min(0.01f)] private float thickness = 0.08f;

        [Tooltip("Layer for the segments. Must be one the peck (or other striker) can hit.")]
        [SerializeField, ValueDropdown(nameof(LayerNames))] private int segmentLayer;

        [Title("Cutting")]
        [Tooltip("Striking a segment (e.g. a peck) cuts the rope there.")]
        [SerializeField] private bool cutByStrike = true;

        [ShowIf(nameof(cutByStrike))]
        [Tooltip("Only strikes of this kind cut it (e.g. \"Peck\"). Empty = any strike.")]
        [SerializeField] private string strikeKind;

        [Tooltip("Something thrown or shot through it fast enough (e.g. an egg) cuts it - flying straight through it if the segments are triggers (the default), or hitting it if they Collide With World.")]
        [SerializeField] private bool cutByImpact = true;

        [ShowIf(nameof(cutByImpact))]
        [Tooltip("Which thrown objects cut it, by their layer. Empty = the Egg layer.")]
        [SerializeField] private LayerMask impactLayers;

        [ShowIf(nameof(cutByImpact))]
        [Tooltip("How fast (m/s) it must be hit - an egg drifting into it doesn't cut it.")]
        [SuffixLabel("m/s", Overlay = true)]
        [SerializeField, Min(0f)] private float minImpactSpeed = 4f;

        [ShowIf(nameof(CanBeCut))]
        [LabelText("Hits To Cut")]
        [Tooltip("How many hits (pecks and/or thrown objects, anywhere along it) it takes to cut - a tough cable might take several.")]
        [SerializeField, Min(1)] private int strikesToCut = 1;

        [ShowIf(nameof(CanBeCut))]
        [Tooltip("How many times it can be cut. 1 = once (the usual - it snaps and what it held falls); more = can be chopped into pieces.")]
        [SerializeField, Min(1)] private int maxCuts = 1;

        [Title("Look")]
        [Tooltip("The line's material. Empty = a plain unlit one tinted Line Color (Sprites/Default - make sure that shader is in builds, or set a material).")]
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Color lineColor = new(0.55f, 0.42f, 0.25f);

        [Tooltip("Line width (m). 0 = twice the segments' Thickness.")]
        [SerializeField, Min(0f)] private float lineWidth;

        [Title("Events")]
        [Tooltip("A strike that didn't cut it yet (Strikes To Cut > 1).")]
        [SerializeField, HideLabel] private FrameCoreEvent onFray = new() { eventName = "Rope - Fray" };

        [SerializeField, HideLabel] private FrameCoreEvent onCut = new() { eventName = "Rope - Cut" };

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private int segmentCount;

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private int cuts;

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private int strikesTaken;

        private readonly List<Rigidbody> _segments = new();
        private readonly List<Joint> _upJoints = new();     // segment i to the one above it (or the start, for 0)
        private readonly List<bool> _linked = new();        // whether segment i is still joined to the one above
        private readonly List<LineRenderer> _lines = new();
        private readonly List<Vector3> _points = new();
        private Joint _endJoint;
        private Transform _segmentRoot;
        private Material _runtimeMaterial;
        private float _halfSegment;

        // A joint between bodies much heavier than each other is unstable - past this ratio the end joint treats the
        // heavier End Body as lighter, so a heavy crate on a light rope doesn't set the chain shaking
        private const float MAX_MASS_RATIO = 5f;

        public float StrikeSwing => strikeSwing;
        public int SegmentCount => _segments.Count;
        public int Cuts => cuts;
        public bool IsCut => cuts > 0;

        // Fires with this rope and the index of the segment it was cut above
        public event Action<Rope, int> Cut;

        private bool HasEnd => endBody != null || endAnchor != null;
        private bool CanBeCut => cutByStrike || cutByImpact;

        // An egg flying through crosses several segments - one throw counts as one hit
        private const float SAME_THROW_WINDOW = 0.3f;
        private GameObject _lastThrown;
        private float _lastThrownTime = -1f;

        private void Reset()
        {
            segmentLayer = gameObject.layer;
        }

        private void Awake()
        {
            if (impactLayers.value == 0)
                impactLayers = LayerMask.GetMask("Egg");

            Build();
        }

        private void LateUpdate()
        {
            DrawLines();
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
                Destroy(_runtimeMaterial);
        }

        #region Building

        // Start and end of the rope as laid out, before physics takes over
        private void GetEnds(out Vector3 start, out Vector3 end, out float ropeLength)
        {
            start = transform.position;

            if (endBody != null || endAnchor != null)
            {
                end = endBody != null ? (attachToSurface ? SurfacePoint(endBody, start) : endBody.position) : endAnchor.position;
                ropeLength = Vector3.Distance(start, end) * slack;
            }
            else
            {
                end = start + Vector3.down * length;
                ropeLength = length;
            }
        }

        // The point on a body's own solid colliders nearest a point (e.g. the top of a crate, from the rope above it)
        private static Vector3 SurfacePoint(Rigidbody body, Vector3 toward)
        {
            Vector3 best = body.position;
            float bestDistance = float.MaxValue;

            foreach (Collider collider in body.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger || !collider.enabled || collider.attachedRigidbody != body)
                    continue;

                // ClosestPoint only works on convex shapes
                bool convex = collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider || (collider is MeshCollider mesh && mesh.convex);
                Vector3 point = convex ? collider.ClosestPoint(toward) : collider.ClosestPointOnBounds(toward);
                float distance = (point - toward).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;

                best = point;
                bestDistance = distance;
            }

            return best;
        }

        private void Build()
        {
            GetEnds(out Vector3 start, out Vector3 end, out float ropeLength);
            int count = Mathf.Clamp(Mathf.CeilToInt(ropeLength / segmentLength), 1, 200);
            float pieceLength = ropeLength / count;
            _halfSegment = pieceLength * 0.5f;
            segmentCount = count;

            _segmentRoot = new GameObject("Segments").transform;
            _segmentRoot.SetParent(transform, false);

            // Laid out along a gentle sag between the ends (straight when taut), so it starts close to how it settles
            Vector3 chord = end - start;
            float sagDepth = Mathf.Sqrt(Mathf.Max(0f, ropeLength * ropeLength - chord.sqrMagnitude)) * 0.5f;
            Vector3 previousPoint = start;
            Rigidbody startBody = transform.parent != null ? transform.parent.GetComponentInParent<Rigidbody>() : null;

            for (int i = 0; i < count; i++)
            {
                float t = (i + 1f) / count;
                Vector3 point = Vector3.Lerp(start, end, t) + Vector3.down * (sagDepth * 4f * t * (1f - t));
                if (!HasEnd) point = start + Vector3.down * (pieceLength * (i + 1));

                Rigidbody segment = CreateSegment(i, previousPoint, point, pieceLength);
                Rigidbody above = i == 0 ? startBody : _segments[i - 1];
                _upJoints.Add(Link(segment, above, previousPoint));
                _linked.Add(true);
                previousPoint = point;
            }

            // Tie the end on
            Rigidbody last = _segments[count - 1];
            if (endBody != null)
                _endJoint = Link(last, endBody, previousPoint, atBottom: true);
            else if (endAnchor != null)
                _endJoint = Link(last, endAnchor.GetComponentInParent<Rigidbody>(), previousPoint, atBottom: true);
        }

        private Rigidbody CreateSegment(int index, Vector3 top, Vector3 bottom, float pieceLength)
        {
            GameObject segmentObject = new($"Segment {index}") { layer = segmentLayer };
            segmentObject.transform.SetParent(_segmentRoot, false);

            Vector3 along = bottom - top;
            segmentObject.transform.SetPositionAndRotation((top + bottom) * 0.5f,
                along.sqrMagnitude > 1e-6f ? Quaternion.FromToRotation(Vector3.down, along) : Quaternion.identity);

            Rigidbody body = segmentObject.AddComponent<Rigidbody>();
            body.mass = segmentMass;
            body.linearDamping = segmentDrag;
            body.angularDamping = segmentDrag;
            body.solverIterations = solverIterations;
            body.solverVelocityIterations = Mathf.Max(4, solverIterations / 2);
            body.interpolation = RigidbodyInterpolation.Interpolate;

            // Safety limits: a hard knock can't fling a segment fast enough to tear the chain apart
            body.maxLinearVelocity = 30f;
            body.maxAngularVelocity = 25f;
            body.maxDepenetrationVelocity = 2f;
            if (planar)
                body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;

            CapsuleCollider capsule = segmentObject.AddComponent<CapsuleCollider>();
            capsule.direction = 1; // local Y runs along the rope
            capsule.radius = thickness;
            capsule.height = pieceLength + thickness * 2f;
            capsule.isTrigger = !collideWithWorld;

            RopeSegment ropeSegment = segmentObject.AddComponent<RopeSegment>();
            ropeSegment.Initialise(this, index);

            _segments.Add(body);
            return body;
        }

        // Joins a segment's top (or bottom) to a point on another body, or to the world if there's no body. Linear
        // movement is locked (it can't stretch apart); rotation is free - only around Z when planar.
        private Joint Link(Rigidbody segment, Rigidbody other, Vector3 worldPoint, bool atBottom = false)
        {
            ConfigurableJoint joint = segment.gameObject.AddComponent<ConfigurableJoint>();
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = segment.transform.InverseTransformPoint(worldPoint);
            joint.connectedBody = other;
            joint.connectedAnchor = other != null ? other.transform.InverseTransformPoint(worldPoint) : worldPoint;

            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;

            // Unity's advice for chains that jitter or explode
            joint.enablePreprocessing = false;

            if (planar)
            {
                // Its primary axis along world Z, so angular X is the in-plane swing
                joint.axis = segment.transform.InverseTransformDirection(Vector3.forward);
                joint.secondaryAxis = segment.transform.InverseTransformDirection(Vector3.up);
                joint.angularXMotion = ConfigurableJointMotion.Free;
                joint.angularYMotion = ConfigurableJointMotion.Locked;
                joint.angularZMotion = ConfigurableJointMotion.Locked;
            }
            else
            {
                joint.angularXMotion = ConfigurableJointMotion.Free;
                joint.angularYMotion = ConfigurableJointMotion.Free;
                joint.angularZMotion = ConfigurableJointMotion.Free;
            }

            // Whatever hangs from the end stays free to turn as it swings - and if it's much heavier than a segment,
            // the joint treats it as lighter (scaling up its inverse mass), keeping the ratio solvable
            if (atBottom && other != null)
            {
                float ratio = other.mass / Mathf.Max(segment.mass, 0.0001f);
                if (ratio > MAX_MASS_RATIO)
                    joint.connectedMassScale = ratio / MAX_MASS_RATIO;

                joint.angularXMotion = ConfigurableJointMotion.Free;
                joint.angularYMotion = ConfigurableJointMotion.Free;
                joint.angularZMotion = ConfigurableJointMotion.Free;
            }

            return joint;
        }

        #endregion

        #region Cutting

        // A strike on one of its segments (from RopeSegment)
        internal void OnSegmentStruck(int index, string kind)
        {
            if (!cutByStrike)
                return;
            if (!string.IsNullOrEmpty(strikeKind) && kind != strikeKind)
                return;

            RegisterHit(index);
        }

        // Something thrown into / through one of its segments (from RopeSegment): the thrown object, its layer, and how
        // fast it was going relative to the segment
        internal void OnSegmentThrownInto(int index, GameObject thrown, int layer, float speed)
        {
            if (!cutByImpact || thrown == null || speed < minImpactSpeed)
                return;
            if ((impactLayers.value & (1 << layer)) == 0 && (impactLayers.value & (1 << thrown.layer)) == 0)
                return;
            if (thrown == _lastThrown && Time.time - _lastThrownTime < SAME_THROW_WINDOW)
                return;

            _lastThrown = thrown;
            _lastThrownTime = Time.time;
            RegisterHit(index);
        }

        private void RegisterHit(int index)
        {
            if (cuts >= maxCuts)
                return;

            strikesTaken++;
            if (strikesTaken < strikesToCut)
            {
                onFray?.Activate();
                return;
            }

            strikesTaken = 0;
            CutAt(index);
        }

        // Cuts the rope just above a segment: the part below (and whatever hangs from it) comes free
        [Button]
        public void CutAt(int segmentIndex)
        {
            if (segmentIndex < 0 || segmentIndex >= _segments.Count || !_linked[segmentIndex])
                return;

            if (_upJoints[segmentIndex] != null)
                Destroy(_upJoints[segmentIndex]);

            _linked[segmentIndex] = false;
            cuts++;
            onCut?.Activate();
            Cut?.Invoke(this, segmentIndex);
        }

        // Unties whatever hangs from the end, without cutting the rope
        [Button]
        public void DetachEnd()
        {
            if (_endJoint != null)
                Destroy(_endJoint);
            _endJoint = null;
        }

        #endregion

        #region Drawing

        // One line per unbroken piece of rope
        private void DrawLines()
        {
            int line = 0;
            int i = 0;
            while (i < _segments.Count)
            {
                _points.Clear();
                _points.Add(SegmentEnd(i, top: true));

                do
                {
                    _points.Add(SegmentEnd(i, top: false));
                    i++;
                } while (i < _segments.Count && _linked[i]);

                LineRenderer renderer = GetLine(line++);
                renderer.positionCount = _points.Count;
                for (int p = 0; p < _points.Count; p++)
                    renderer.SetPosition(p, _points[p]);
            }

            for (int unused = line; unused < _lines.Count; unused++)
                _lines[unused].enabled = false;
        }

        private Vector3 SegmentEnd(int index, bool top)
        {
            return _segments[index].transform.TransformPoint(new Vector3(0f, top ? _halfSegment : -_halfSegment, 0f));
        }

        private LineRenderer GetLine(int index)
        {
            while (_lines.Count <= index)
            {
                GameObject lineObject = new($"Line {_lines.Count}");
                lineObject.transform.SetParent(transform, false);

                LineRenderer renderer = lineObject.AddComponent<LineRenderer>();
                renderer.useWorldSpace = true;
                renderer.widthMultiplier = lineWidth > 0f ? lineWidth : thickness * 2f;
                renderer.numCapVertices = 2;
                renderer.numCornerVertices = 2;
                renderer.startColor = renderer.endColor = lineColor;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.sharedMaterial = LineMaterial;
                _lines.Add(renderer);
            }

            LineRenderer line = _lines[index];
            line.enabled = true;
            return line;
        }

        private Material LineMaterial
        {
            get
            {
                if (lineMaterial != null)
                    return lineMaterial;

                // No material set: a plain unlit one that takes the line's colour
                if (_runtimeMaterial == null)
                    _runtimeMaterial = new Material(Shader.Find("Sprites/Default"));
                return _runtimeMaterial;
            }
        }

        #endregion

        private static IEnumerable<ValueDropdownItem<int>> LayerNames()
        {
            for (int layer = 0; layer < 32; layer++)
            {
                string layerName = LayerMask.LayerToName(layer);
                if (!string.IsNullOrEmpty(layerName))
                    yield return new ValueDropdownItem<int>(layerName, layer);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (Application.isPlaying)
                return;

            // Preview of the rope as it'll be laid out
            GetEnds(out Vector3 start, out Vector3 end, out float ropeLength);
            Vector3 chord = end - start;
            float sagDepth = Mathf.Sqrt(Mathf.Max(0f, ropeLength * ropeLength - chord.sqrMagnitude)) * 0.5f;

            Gizmos.color = lineColor;
            Vector3 previous = start;
            const int STEPS = 16;
            for (int i = 1; i <= STEPS; i++)
            {
                float t = i / (float)STEPS;
                Vector3 point = HasEnd ? Vector3.Lerp(start, end, t) + Vector3.down * (sagDepth * 4f * t * (1f - t)) : Vector3.Lerp(start, end, t);
                Gizmos.DrawLine(previous, point);
                previous = point;
            }

            Gizmos.DrawWireSphere(start, thickness * 1.5f);
            Gizmos.DrawWireSphere(end, thickness * 1.5f);
        }
#endif
    }
}
