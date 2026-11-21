using UnityEngine;

namespace Foundry.Particles
{
    // Small helpers for effects that build their own particle systems in code (Fire, Rain): a stopped, world-space
    // system on a child, and quick gradients/curves. Keeps those components drop-in - no particle prefabs to wire up.
    public static class FxBuilder
    {
        public static ParticleSystem Create(Transform parent, string name, Material material, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.SetActive(false); // configure before the system's first update
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = maxParticles;
            main.startSpeed = 0f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            go.SetActive(true);
            return system;
        }

        // Colour keys at even-ish times; alpha keys as (alpha, time) pairs
        public static Gradient MakeGradient(Color[] colors, float[] colorTimes, float[] alphaPairs)
        {
            var colorKeys = new GradientColorKey[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                colorKeys[i] = new GradientColorKey(colors[i], colorTimes[i]);
            var alphaKeys = new GradientAlphaKey[alphaPairs.Length / 2];
            for (int i = 0; i < alphaKeys.Length; i++)
                alphaKeys[i] = new GradientAlphaKey(alphaPairs[i * 2], alphaPairs[i * 2 + 1]);
            var gradient = new Gradient();
            gradient.SetKeys(colorKeys, alphaKeys);
            return gradient;
        }

        public static AnimationCurve MakeCurve(params float[] timeValuePairs)
        {
            var keys = new Keyframe[timeValuePairs.Length / 2];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new Keyframe(timeValuePairs[i * 2], timeValuePairs[i * 2 + 1]);
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
                curve.SmoothTangents(i, 0f);
            return curve;
        }

        public static void SetRate(ParticleSystem system, float perSecond)
        {
            if (system == null) return;
            var emission = system.emission;
            emission.rateOverTime = Mathf.Max(0f, perSecond);
            if (perSecond > 0f && !system.isPlaying) system.Play(true);
        }
    }
}
