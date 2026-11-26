using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.PhysicsSystem
{
    // Draws where a thrown / shot Rigidbody will go: a (dotted) line along its ballistic arc - gravity and linear
    // damping, stepped the way the physics engine steps it - stopping at the first thing it would hit.
    // Call Show(...) every frame while aiming and Hide() when done; whoever aims decides the start and velocity.
    // Needs a LineRenderer (added automatically); with no Material set it makes its own unlit one.
    [RequireComponent(typeof(LineRenderer))]
    public class TrajectoryPreview : MonoBehaviour
    {
        [BoxGroup("Path")]
        [Tooltip("Seconds of flight to draw.")]
        [SerializeField, Min(0.05f)] private float maxTime = 1.2f;

        [BoxGroup("Path")]
        [Tooltip("Simulation step (s). 0.02 matches Unity's default physics rate.")]
        [SerializeField, Range(0.005f, 0.1f)] private float simulationStep = 0.02f;

        [BoxGroup("Path")]
        [Tooltip("Stop the line at the first collider along the path (on the layers Show is given).")]
        [SerializeField] private bool stopAtHit = true;

        [BoxGroup("Path")]
        [Tooltip("Radius of the moving object, for the hit test.")]
        [SerializeField, Min(0f)] private float hitRadius = 0.1f;

        [BoxGroup("Look")]
        [SerializeField, Min(0.001f)] private float width = 0.07f;

        [BoxGroup("Look")]
        [SerializeField] private Gradient colour = DefaultGradient();

        [BoxGroup("Look")]
        [SerializeField] private bool dotted = true;

        [BoxGroup("Look"), ShowIf(nameof(dotted))]
        [SerializeField, Min(0.1f)] private float dotsPerMetre = 5f;

        [BoxGroup("Look")]
        [Tooltip("Optional. Empty = a plain unlit material made at runtime.")]
        [SerializeField] private Material material;

        [BoxGroup("Look")]
        [Tooltip("Optional marker placed where the path hits something (hidden when it hits nothing).")]
        [SerializeField] private Transform hitMarker;

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private bool showing;

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private bool hasHit;

        [FoldoutGroup("Debug")]
        [SerializeField, ReadOnly] private Vector3 hitPoint;

        public bool Showing => showing;
        public bool HasHit => hasHit;
        public Vector3 HitPoint => hitPoint;
        public Collider HitCollider { get; private set; }

        private const int MAX_POINTS = 512;
        private readonly Vector3[] _points = new Vector3[MAX_POINTS];
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private LineRenderer _line;
        private Material _runtimeMaterial;
        private Texture2D _dotTexture;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = 0;
            _line.enabled = false;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            ApplyLook();
        }

        private void OnDisable() => Hide();

        private void OnDestroy()
        {
            if (_runtimeMaterial != null) Destroy(_runtimeMaterial);
            if (_dotTexture != null) Destroy(_dotTexture);
        }

        // Turns the force a launcher applies into the starting velocity it gives a Rigidbody of this mass
        public static Vector3 LaunchVelocity(Vector3 force, ForceMode mode, float mass)
        {
            mass = Mathf.Max(mass, 0.0001f);
            return mode switch
            {
                ForceMode.Impulse => force / mass,
                ForceMode.VelocityChange => force,
                ForceMode.Force => force * Time.fixedDeltaTime / mass,
                ForceMode.Acceleration => force * Time.fixedDeltaTime,
                _ => force
            };
        }

        // Every layer a Rigidbody on this layer collides with (from the physics collision matrix)
        public static LayerMask CollidesWith(int layer)
        {
            int mask = 0;
            for (int i = 0; i < 32; i++)
            {
                if (!Physics.GetIgnoreLayerCollision(layer, i))
                    mask |= 1 << i;
            }

            return mask;
        }

        // Draws the arc from start with this velocity. ignoreRoot: colliders under it (the thrower) don't stop the line.
        public void Show(Vector3 start, Vector3 velocity, Vector3 gravity, float linearDamping, LayerMask layers, Transform ignoreRoot = null)
        {
            if (_line == null) Awake();

            int steps = Mathf.Clamp(Mathf.CeilToInt(maxTime / simulationStep), 1, MAX_POINTS - 1);
            float dt = simulationStep;
            Vector3 position = start;
            int count = 0;
            _points[count++] = position;
            hasHit = false;
            HitCollider = null;

            for (int i = 0; i < steps; i++)
            {
                // The same order PhysX steps a body: gravity, then damping, then move
                velocity += gravity * dt;
                velocity *= 1f / (1f + linearDamping * dt);
                Vector3 next = position + velocity * dt;

                if (stopAtHit && TryHit(position, next, layers, ignoreRoot, out RaycastHit hit))
                {
                    hasHit = true;
                    hitPoint = hit.point;
                    HitCollider = hit.collider;
                    _points[count++] = position + (next - position).normalized * hit.distance;
                    break;
                }

                position = next;
                _points[count++] = position;
            }

            _line.positionCount = count;
            _line.SetPositions(_points);
            _line.enabled = true;
            showing = true;

            if (hitMarker != null)
            {
                hitMarker.gameObject.SetActive(hasHit);
                if (hasHit) hitMarker.position = hitPoint;
            }
        }

        public void Hide()
        {
            showing = false;
            hasHit = false;
            if (_line != null)
            {
                _line.enabled = false;
                _line.positionCount = 0;
            }

            if (hitMarker != null)
                hitMarker.gameObject.SetActive(false);
        }

        private bool TryHit(Vector3 from, Vector3 to, LayerMask layers, Transform ignoreRoot, out RaycastHit nearest)
        {
            nearest = default;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.00001f)
                return false;

            int count = hitRadius > 0f
                ? Physics.SphereCastNonAlloc(from, hitRadius, delta / distance, _hits, distance, layers, QueryTriggerInteraction.Ignore)
                : Physics.RaycastNonAlloc(from, delta / distance, _hits, distance, layers, QueryTriggerInteraction.Ignore);

            bool found = false;
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = _hits[i];
                if (ignoreRoot != null && h.transform.IsChildOf(ignoreRoot))
                    continue;
                // Already touching at the start (distance 0) - e.g. the thrower's hand - isn't a hit along the path
                if (h.distance <= 0f)
                    continue;
                if (h.distance < best)
                {
                    best = h.distance;
                    nearest = h;
                    found = true;
                }
            }

            return found;
        }

        private void OnValidate()
        {
            if (_line != null) ApplyLook();
        }

        private void ApplyLook()
        {
            _line.widthMultiplier = width;
            _line.colorGradient = colour;
            _line.numCapVertices = 2;

            Material source = material;
            if (source == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) return;
                if (_runtimeMaterial == null || _runtimeMaterial.shader != shader)
                    _runtimeMaterial = new Material(shader) { name = "Trajectory Preview (runtime)" };
                source = _runtimeMaterial;
            }
            else if (dotted)
            {
                // Don't change the shared asset - dot a copy
                if (_runtimeMaterial == null || _runtimeMaterial.shader != material.shader)
                    _runtimeMaterial = new Material(material) { name = material.name + " (dotted)" };
                source = _runtimeMaterial;
            }

            if (dotted)
            {
                if (_dotTexture == null)
                {
                    // One opaque pixel, one clear: tiled along the line it makes dots
                    _dotTexture = new Texture2D(2, 1, TextureFormat.RGBA32, false)
                    {
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Repeat,
                        name = "Trajectory Dots"
                    };
                    _dotTexture.SetPixels(new[] { Color.white, Color.clear });
                    _dotTexture.Apply();
                }

                source.mainTexture = _dotTexture;
                _line.textureMode = LineTextureMode.Tile;
                _line.textureScale = new Vector2(dotsPerMetre, 1f);
            }
            else
            {
                _line.textureMode = LineTextureMode.Stretch;
                _line.textureScale = Vector2.one;
            }

            _line.sharedMaterial = source;
        }

        private static Gradient DefaultGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }
    }
}
