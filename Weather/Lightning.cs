using System.Collections;
using FrameCoreU.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Foundry.Weather
{
    // A lightning strike: a jagged bolt from this object (up in the sky) down to Target, with a double flash of light
    // over the scene. Strike() = bolt + flash, Flash() = just the flash (distant lightning). It can also flash by itself
    // every so often (Ambient) once StartAmbient() is called - a storm in the background. Hook a fire, a thunder
    // sound, a falling branch... to On Strike. Strike / Flash / StartAmbient / StopAmbient from the Lightning action.
    public class Lightning : MonoBehaviour
    {
        [Title("Bolt")]
        [Tooltip("Where the bolt hits. The bolt runs from this object down to here.")]
        [SerializeField] private Transform target;

        [SerializeField, Range(4, 40)] private int segments = 14;

        [Tooltip("How far (m) the bolt zig-zags off a straight line.")]
        [SerializeField, Min(0f)] private float jag = 1.2f;

        [SerializeField, Min(0.01f)] private float width = 0.3f;
        [SerializeField, Range(0, 4)] private int branches = 2;
        [SerializeField] private Color boltColor = new(0.85f, 0.9f, 1f);

        [Tooltip("Additive material for the bolt (vertex colour is used).")]
        [SerializeField] private Material boltMaterial;

        [Title("Flash")]
        [Tooltip("The light to flash. Empty = it makes its own (a directional light, off between flashes).")]
        [SerializeField] private Light flashLight;

        [SerializeField, Min(0f)] private float flashIntensity = 3f;
        [SerializeField] private Color flashColor = new(0.75f, 0.82f, 1f);

        [Title("Ambient")]
        [Tooltip("While ambient is on, a flash comes every this many seconds (random between the two).")]
        [SerializeField] private Vector2 ambientEvery = new(6f, 14f);

        [Tooltip("Ambient flashes also strike with a bolt.")]
        [SerializeField] private bool ambientBolts;

        [SerializeField] private bool ambientOnStart;

        [Title("Events")]
        [SerializeField, HideLabel] private FrameCoreEvent onStrike = new() { eventName = "Lightning - Strike" };
        [SerializeField, HideLabel] private FrameCoreEvent onFlash = new() { eventName = "Lightning - Flash" };

        private LineRenderer[] _lines;
        private float _baseFlashIntensity;
        private bool _ownLight;
        private Coroutine _ambient;
        private Coroutine _flash;
        private Coroutine _bolt;

        private void Awake()
        {
            if (flashLight == null)
            {
                var lightObject = new GameObject("Lightning Flash");
                lightObject.transform.SetParent(transform, false);
                lightObject.transform.rotation = Quaternion.Euler(55f, -25f, 0f);
                flashLight = lightObject.AddComponent<Light>();
                flashLight.type = LightType.Directional;
                flashLight.shadows = LightShadows.None;
                flashLight.color = flashColor;
                flashLight.intensity = 0f;
                flashLight.enabled = false;
                _ownLight = true;
            }
            _baseFlashIntensity = flashLight.intensity;

            if (boltMaterial != null)
            {
                _lines = new LineRenderer[1 + branches];
                for (int i = 0; i < _lines.Length; i++)
                {
                    var lineObject = new GameObject(i == 0 ? "Bolt" : "Bolt Branch");
                    lineObject.transform.SetParent(transform, false);
                    var line = lineObject.AddComponent<LineRenderer>();
                    line.sharedMaterial = boltMaterial;
                    line.useWorldSpace = true;
                    line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    line.receiveShadows = false;
                    line.numCapVertices = 2;
                    line.enabled = false;
                    _lines[i] = line;
                }
            }
        }

        private void OnEnable()
        {
            if (ambientOnStart && Application.isPlaying) StartAmbient();
        }

        private void OnDisable()
        {
            _ambient = null;
            if (flashLight != null) flashLight.intensity = _baseFlashIntensity;
            if (_ownLight && flashLight != null) flashLight.enabled = false;
        }

        public void Strike()
        {
            if (_lines != null && target != null)
            {
                if (_bolt != null) StopCoroutine(_bolt);
                _bolt = StartCoroutine(Bolt());
            }
            Flash(silent: true);
            onStrike?.Activate();
        }

        public void Flash() => Flash(false);

        private void Flash(bool silent)
        {
            if (flashLight == null) return;
            if (_flash != null) StopCoroutine(_flash);
            _flash = StartCoroutine(FlashRoutine());
            if (!silent) onFlash?.Activate();
        }

        public void StartAmbient()
        {
            if (_ambient == null && isActiveAndEnabled) _ambient = StartCoroutine(Ambient());
        }

        public void StopAmbient()
        {
            if (_ambient != null) StopCoroutine(_ambient);
            _ambient = null;
        }

        private IEnumerator Ambient()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(ambientEvery.x, Mathf.Max(ambientEvery.x, ambientEvery.y)));
                if (ambientBolts) Strike();
                else Flash();
            }
        }

        // two quick bright pulses, then gone
        private IEnumerator FlashRoutine()
        {
            if (_ownLight) flashLight.enabled = true;
            float[] times = { 0f, 0.05f, 0.11f, 0.16f, 0.24f, 0.5f };
            float[] levels = { 0f, 1f, 0.25f, 0.85f, 0.3f, 0f };
            float clock = 0f;
            for (int i = 1; i < times.Length; i++)
            {
                while (clock < times[i])
                {
                    float u = Mathf.InverseLerp(times[i - 1], times[i], clock);
                    flashLight.intensity = _baseFlashIntensity + flashIntensity * Mathf.Lerp(levels[i - 1], levels[i], u);
                    clock += Time.deltaTime;
                    yield return null;
                }
            }
            flashLight.intensity = _baseFlashIntensity;
            if (_ownLight) flashLight.enabled = false;
            _flash = null;
        }

        private IEnumerator Bolt()
        {
            Shape();
            Show(1f);
            yield return new WaitForSeconds(0.07f);
            Show(0f);
            yield return new WaitForSeconds(0.05f);
            Shape();
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.25f)
            {
                Show(1f - t * t);
                yield return null;
            }
            Show(0f);
            _bolt = null;
        }

        private void Show(float amount)
        {
            foreach (LineRenderer line in _lines)
            {
                bool isMain = line == _lines[0];
                line.enabled = amount > 0.001f && (isMain || line.positionCount > 1);
                float w = width * amount * (isMain ? 1f : 0.5f);
                line.startWidth = w;
                line.endWidth = w * 0.6f;
                Color c = boltColor;
                c.a = amount;
                line.startColor = c;
                line.endColor = c;
            }
        }

        private void Shape()
        {
            Vector3 from = transform.position;
            Vector3 to = target.position;
            Vector3[] points = Zigzag(from, to, segments, jag);
            _lines[0].positionCount = points.Length;
            _lines[0].SetPositions(points);

            for (int b = 1; b < _lines.Length; b++)
            {
                int start = Random.Range(2, Mathf.Max(3, points.Length - 3));
                Vector3 origin = points[start];
                Vector3 down = (to - from).normalized;
                Vector3 side = Vector3.Cross(down, Vector3.forward).normalized * (Random.value < 0.5f ? -1f : 1f);
                float length = (to - from).magnitude * Random.Range(0.12f, 0.25f);
                Vector3 end = origin + (down * 0.6f + side * 0.8f).normalized * length;
                Vector3[] branch = Zigzag(origin, end, Mathf.Max(3, segments / 3), jag * 0.5f);
                _lines[b].positionCount = branch.Length;
                _lines[b].SetPositions(branch);
            }
        }

        private static Vector3[] Zigzag(Vector3 from, Vector3 to, int count, float amount)
        {
            var points = new Vector3[count + 1];
            Vector3 along = to - from;
            Vector3 side = Vector3.Cross(along.normalized, Vector3.forward).normalized;
            for (int i = 0; i <= count; i++)
            {
                float u = (float)i / count;
                float offset = i == 0 || i == count ? 0f : Random.Range(-amount, amount) * Mathf.Sin(u * Mathf.PI);
                points[i] = from + along * u + side * offset;
            }
            return points;
        }

        private void OnDrawGizmosSelected()
        {
            if (target == null) return;
            Gizmos.color = new Color(0.8f, 0.85f, 1f);
            Gizmos.DrawLine(transform.position, target.position);
            Gizmos.DrawWireSphere(target.position, 0.3f);
        }
    }
}
