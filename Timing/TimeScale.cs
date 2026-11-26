using System.Collections.Generic;
using UnityEngine;

namespace Foundry.Timing
{
    // One owner for Time.timeScale: slow motion and hit-stop requests from anywhere, combined so the slowest wins, each
    // easing in and out on real (unscaled) time. Physics keeps its real-time step rate: Time.fixedDeltaTime is scaled
    // with the time scale (so a slowed scene still steps 50 times a real second and stays smooth), and put back when
    // nothing is slowing time.
    //
    //   var slow = TimeScale.Slow(0.3f, easeIn: 0.15f, easeOut: 0.2f, maxDuration: 3f);   // hold the handle
    //   TimeScale.Release(slow);                                                       // ease back
    //   TimeScale.HitStop(0.08f);                                                      // fire and forget
    //
    // Everything on scaled time slows with it: physics, animation, particles, the camera's smoothing, Time.time-based
    // timers and delayed FrameCoreEvents. Audio doesn't (Unity doesn't pitch AudioSources by time scale).
    // While no request is active it leaves Time.timeScale alone, so a pause menu that sets it directly still works -
    // but while one is active it rewrites Time.timeScale every frame.
    public static class TimeScale
    {
        private static readonly List<TimeScaleRequest> Requests = new();
        private static TimeScaleRunner _runner;
        private static float _baseFixedDeltaTime = -1f;

        // Lowest scale fixedDeltaTime is scaled by (a 0 hit-stop doesn't step physics at all anyway)
        private const float MIN_FIXED_SCALE = 0.01f;

        // The scale the requests add up to right now (1 when nothing is slowing time)
        public static float Current { get; private set; } = 1f;

        // True while any request is easing in, holding or easing out
        public static bool Active => Requests.Count > 0;

        // Physics' step before any slow motion (Time.fixedDeltaTime when the first request started)
        public static float BaseFixedDeltaTime => _baseFixedDeltaTime > 0f ? _baseFixedDeltaTime : Time.fixedDeltaTime;

        // Slow time to `scale` (0-1), easing in over `easeIn` real seconds. It holds until released, or for
        // `maxDuration` real seconds (0 = no limit), then eases back over `easeOut`. Keep the handle to release it.
        public static TimeScaleRequest Slow(float scale, float easeIn = 0.15f, float easeOut = 0.2f, float maxDuration = 0f, string label = null)
        {
            var request = new TimeScaleRequest(Mathf.Clamp01(scale), Mathf.Max(0f, easeIn), Mathf.Max(0f, easeOut), Mathf.Max(0f, maxDuration), label);
            if (request.EaseIn <= 0f)
                request.Weight = 1f; // no ease: in effect from this frame

            if (Requests.Count == 0)
                _baseFixedDeltaTime = Time.fixedDeltaTime;

            Requests.Add(request);
            EnsureRunner();
            Apply();
            return request;
        }

        // A short freeze (or near-freeze) on an impact: snaps to `scale` for `duration` real seconds, then snaps back
        public static TimeScaleRequest HitStop(float duration, float scale = 0f, string label = "Hit Stop")
        {
            return Slow(scale, 0f, 0f, Mathf.Max(0.0001f, duration), label);
        }

        // Eases the request back out (safe to call with null, or more than once)
        public static void Release(TimeScaleRequest request)
        {
            if (request != null)
                request.Releasing = true;
        }

        public static void ReleaseAll()
        {
            foreach (TimeScaleRequest request in Requests)
                request.Releasing = true;
        }

        // Drops every request and puts time back to normal straight away (e.g. on loading a level)
        public static void Clear()
        {
            foreach (TimeScaleRequest request in Requests)
                request.Finished = true;
            Requests.Clear();
            Restore();
        }

        internal static void Tick(float unscaledDelta)
        {
            if (Requests.Count == 0)
                return;

            for (int i = Requests.Count - 1; i >= 0; i--)
            {
                TimeScaleRequest request = Requests[i];
                request.Age += unscaledDelta;

                if (request.MaxDuration > 0f && request.Age >= request.MaxDuration)
                    request.Releasing = true;

                if (request.Releasing)
                {
                    request.Weight = request.EaseOut > 0f ? Mathf.MoveTowards(request.Weight, 0f, unscaledDelta / request.EaseOut) : 0f;
                    if (request.Weight <= 0f)
                    {
                        request.Finished = true;
                        Requests.RemoveAt(i);
                    }
                }
                else
                {
                    request.Weight = request.EaseIn > 0f ? Mathf.MoveTowards(request.Weight, 1f, unscaledDelta / request.EaseIn) : 1f;
                }
            }

            if (Requests.Count == 0)
                Restore();
            else
                Apply();
        }

        private static void Apply()
        {
            float scale = 1f;
            foreach (TimeScaleRequest request in Requests)
            {
                float eased = Mathf.SmoothStep(0f, 1f, request.Weight);
                scale = Mathf.Min(scale, Mathf.Lerp(1f, request.Scale, eased));
            }

            Current = scale;
            Time.timeScale = scale;
            Time.fixedDeltaTime = BaseFixedDeltaTime * Mathf.Max(scale, MIN_FIXED_SCALE);
        }

        private static void Restore()
        {
            Current = 1f;
            Time.timeScale = 1f;
            if (_baseFixedDeltaTime > 0f)
                Time.fixedDeltaTime = _baseFixedDeltaTime;
            _baseFixedDeltaTime = -1f;
        }

        private static void EnsureRunner()
        {
            if (_runner != null)
                return;

            var go = new GameObject("[Foundry TimeScale]") { hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave };
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<TimeScaleRunner>();
        }

        internal static void RunnerGone(TimeScaleRunner runner)
        {
            if (_runner != runner)
                return;

            _runner = null;
            Clear();
        }

        // Fresh state each Play session (also with domain reload off)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Requests.Clear();
            _runner = null;
            _baseFixedDeltaTime = -1f;
            Current = 1f;
        }
    }

    // A handle on one slow-motion / hit-stop request
    public sealed class TimeScaleRequest
    {
        public float Scale { get; }
        public float EaseIn { get; }
        public float EaseOut { get; }
        public float MaxDuration { get; }
        public string Label { get; }

        // 0 = no effect, 1 = fully at Scale (before easing)
        public float Weight { get; internal set; }
        // Real seconds since it started
        public float Age { get; internal set; }
        public bool Releasing { get; internal set; }
        public bool Finished { get; internal set; }

        internal TimeScaleRequest(float scale, float easeIn, float easeOut, float maxDuration, string label)
        {
            Scale = scale;
            EaseIn = easeIn;
            EaseOut = easeOut;
            MaxDuration = maxDuration;
            Label = label;
        }

        public override string ToString() => $"{Label ?? "Slow"} x{Scale:0.##} w{Weight:0.##}{(Releasing ? " (releasing)" : "")}";
    }

    // Ticks TimeScale on real time; created on the first request, lives across scene loads
    [AddComponentMenu("")]
    internal class TimeScaleRunner : MonoBehaviour
    {
        private void Update() => TimeScale.Tick(Time.unscaledDeltaTime);

        private void OnDestroy() => TimeScale.RunnerGone(this);
    }
}
