using UnityEngine;

namespace BikeGame
{
    /// <summary>
    /// Small one-shot golden burst when a coin is collected. The effect is only
    /// created when needed and destroys itself, so idle coins cost nothing.
    /// </summary>
    public static class CoinCollectEffect
    {
        private static Material fallbackMaterial;

        public static void Play(Vector3 position, Material material)
        {
            GameObject effect = new GameObject("Coin Collect Effect");
            effect.transform.position = position;

            ParticleSystem particles = effect.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.duration = 0.25f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 3.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.9f, 0.3f, 1f),
                new Color(1f, 0.62f, 0.05f, 1f));
            main.maxParticles = 16;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.gravityModifier = 0.6f;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;

            ParticleSystemRenderer renderer = effect.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material != null ? material : GetFallbackMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            particles.Play();
            Object.Destroy(effect, 0.9f);
        }

        private static Material GetFallbackMaterial()
        {
            if (fallbackMaterial != null) return fallbackMaterial;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

            fallbackMaterial = new Material(shader)
            {
                name = "Coin Collect Effect (Fallback)"
            };
            fallbackMaterial.color = new Color(1f, 0.8f, 0.15f, 1f);
            return fallbackMaterial;
        }
    }
}
