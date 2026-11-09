using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Plane
{
    /// <summary>Rubans de capture pour l’aimant et éclats prismatiques pour le multiplicateur.</summary>
    internal sealed class FlightBonusBurst : MonoBehaviour
    {
        private const float Duration = .6f;
        private const int RibbonSamples = 24;
        private readonly LineRenderer[] ribbons = new LineRenderer[4];
        private Material glow;
        private Transform aircraft;
        private Color accent;
        private bool magnet;
        private float startedAt;

        internal static void Play(Transform parent, Vector3 position, Quaternion rotation, FlightBonusKind kind,
            Color color, Transform aircraft)
        {
            // Resources inclut le shader dans le build Android sans réglage global.
            var shader = Resources.Load<Shader>("FlightBonusGlow");
            if (shader == null) return;

            bool isMagnet = kind == FlightBonusKind.Magnet;
            var root = new GameObject(isMagnet ? "MagnetCapture" : "DoublePointsPrism");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);
            var burst = root.AddComponent<FlightBonusBurst>();
            burst.glow = new Material(shader);
            burst.accent = color;
            burst.magnet = isMagnet;
            burst.aircraft = aircraft;
            burst.startedAt = Time.time;
            for (int i = 0; i < burst.ribbons.Length; i++)
                burst.ribbons[i] = burst.CreateRibbon();
            burst.CreateSparks();
            burst.LateUpdate();
        }

        private LineRenderer CreateRibbon()
        {
            var obj = new GameObject(magnet ? "Ruban" : "Arête");
            obj.transform.SetParent(transform, false);
            var line = obj.AddComponent<LineRenderer>();
            line.sharedMaterial = glow;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.useWorldSpace = false;
            line.positionCount = magnet ? RibbonSamples : 3;
            return line;
        }

        private void CreateSparks()
        {
            var obj = new GameObject("Étincelles");
            obj.transform.SetParent(transform, false);
            var particles = obj.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = Duration;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.22f, .42f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(20f, 38f);
            main.startSize = new ParticleSystem.MinMaxCurve(.28f, .78f);
            main.startColor = new ParticleSystem.MinMaxGradient(accent, Color.Lerp(accent, Color.white, .72f));
            main.maxParticles = 24;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.8f;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(magnet ? 12 : 24)) });
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = fade;
            var renderer = obj.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = glow;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = .06f;
            renderer.lengthScale = .85f;
            particles.Play();
        }

        private void LateUpdate()
        {
            float elapsed = Time.time - startedAt;
            if (elapsed >= Duration)
            {
                Destroy(gameObject);
                return;
            }

            if (magnet) AnimateCapture(elapsed);
            else AnimatePrism(elapsed);
        }

        private void AnimateCapture(float elapsed)
        {
            if (aircraft == null) return;
            Vector3 destination = transform.InverseTransformPoint(aircraft.position);
            destination = Vector3.ClampMagnitude(destination, 36f);
            for (int i = 0; i < ribbons.Length; i++)
            {
                var ribbon = ribbons[i];
                float progress = Mathf.Clamp01((elapsed - i * .024f) / .47f);
                float head = Mathf.Lerp(.12f, 1f, progress * progress * (3f - 2f * progress));
                float tail = Mathf.Max(0f, head - Mathf.Lerp(.4f, .025f, progress * progress));
                for (int j = 0; j < RibbonSamples; j++)
                {
                    float along = Mathf.Lerp(tail, head, j / (RibbonSamples - 1f));
                    float angle = i * Mathf.PI * .5f + along * 4.8f + progress * 2.2f;
                    float radius = Mathf.Lerp(10f, .6f, along);
                    var curl = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                    ribbon.SetPosition(j, destination * along + curl);
                }
                float intensity = Mathf.Clamp01(progress * 12f) * (1f - progress * progress);
                ribbon.startColor = WithAlpha(accent, intensity * .06f);
                ribbon.endColor = WithAlpha(Color.Lerp(accent, Color.white, .65f), intensity);
                ribbon.startWidth = .16f;
                ribbon.endWidth = Mathf.Lerp(1f, .28f, progress);
            }
        }

        private void AnimatePrism(float elapsed)
        {
            float progress = Mathf.Clamp01(elapsed / .52f);
            float expansion = 1f - Mathf.Pow(1f - progress, 3f);
            float radius = Mathf.Lerp(4f, 16f, expansion);
            float halfArc = Mathf.Lerp(44f, 10f, progress);
            for (int i = 0; i < ribbons.Length; i++)
            {
                var edge = ribbons[i];
                float angle = i * 90f + 58f * expansion;
                edge.SetPosition(0, PrismPoint(angle - halfArc, radius * .74f));
                edge.SetPosition(1, PrismPoint(angle, radius));
                edge.SetPosition(2, PrismPoint(angle + halfArc, radius * .74f));
                edge.startColor = WithAlpha(Color.Lerp(Color.white, accent, expansion), 1f - progress);
                edge.endColor = WithAlpha(accent, (1f - progress) * .65f);
                edge.widthMultiplier = Mathf.Lerp(1.3f, .06f, expansion);
            }
        }

        private static Vector3 PrismPoint(float degrees, float radius)
        {
            float angle = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), Mathf.Sin(angle * 2f) * .18f) * radius;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private void OnDestroy()
        {
            if (glow != null) Destroy(glow);
        }
    }
}
