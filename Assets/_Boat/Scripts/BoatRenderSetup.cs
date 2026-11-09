using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Corrige le rendu bateau sous URP + espace linéaire (le projet original était Built-in + Gamma).
public static class BoatRenderSetup
{
    static readonly Color AmbientSky = new Color(0.42f, 0.62f, 0.72f, 1f);
    static readonly Color AmbientEquator = new Color(0.28f, 0.42f, 0.48f, 1f);
    static readonly Color AmbientGround = new Color(0.1f, 0.16f, 0.2f, 1f);

    public static void Apply()
    {
        ApplyLighting();
        UpgradeCamerasAndLights();
        UpgradeRenderers();
    }

    public static void ApplyLighting()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = AmbientSky;
        RenderSettings.ambientEquatorColor = AmbientEquator;
        RenderSettings.ambientGroundColor = AmbientGround;
        RenderSettings.ambientIntensity = 1.15f;
        RenderSettings.fog = false;

        var sky = Resources.FindObjectsOfTypeAll<Material>();
        foreach (var material in sky)
        {
            if (material != null && material.name == "BoatRunnerSky")
            {
                RenderSettings.skybox = material;
                break;
            }
        }

        DynamicGI.UpdateEnvironment();
    }

    public static Shader Unlit()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        return shader;
    }

    public static Shader LitOrUnlit()
    {
        var shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        return shader != null ? shader : Unlit();
    }

    public static void Paint(Material material, Color color)
    {
        if (material == null)
            return;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.color = color;
    }

    static void UpgradeCamerasAndLights()
    {
        var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        foreach (var camera in cameras)
        {
            if (camera.gameObject.scene.name != "BoatRunner")
                continue;
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null)
                data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderType = CameraRenderType.Base;
            data.renderShadows = true;
            // Fond mer si le skybox custom échoue.
            if (camera.clearFlags == CameraClearFlags.Skybox)
                camera.backgroundColor = new Color(0.32f, 0.58f, 0.72f, 1f);
        }

        var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
        foreach (var light in lights)
        {
            if (light.gameObject.scene.name != "BoatRunner")
                continue;
            if (light.GetComponent<UniversalAdditionalLightData>() == null)
                light.gameObject.AddComponent<UniversalAdditionalLightData>();
            if (light.type == LightType.Directional)
            {
                light.intensity = Mathf.Max(light.intensity, 1.35f);
                light.color = new Color(0.9f, 0.95f, 1f, 1f);
                light.shadows = LightShadows.Soft;
            }
        }
    }

    static void UpgradeRenderers()
    {
        var lit = LitOrUnlit();
        var unlit = Unlit();
        if (lit == null && unlit == null)
            return;

        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        foreach (var renderer in renderers)
        {
            if (renderer.gameObject.scene.name != "BoatRunner")
                continue;
            var materials = renderer.materials;
            var changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                var next = Upgrade(materials[i], lit, unlit);
                if (next == materials[i])
                    continue;
                materials[i] = next;
                changed = true;
            }
            if (changed)
                renderer.materials = materials;
        }
    }

    static Material Upgrade(Material source, Shader lit, Shader unlit)
    {
        if (source == null || source.shader == null)
            return source;

        var name = source.shader.name ?? string.Empty;
        if (name.StartsWith("Universal Render Pipeline/") || name.StartsWith("BoatRunner/"))
            return source;

        bool broken = name.Contains("InternalErrorShader") || name.Contains("Hidden/Universal Render Pipeline/FallbackError");
        bool transparent = name.Contains("Particle") || name.Contains("Transparent") || name.StartsWith("Sprites/") || name.StartsWith("Unlit/");
        var shader = transparent ? unlit : lit;
        if (shader == null)
            shader = unlit != null ? unlit : lit;
        if (shader == null)
            return source;

        Color color = Color.white;
        if (source.HasProperty("_BaseColor"))
            color = source.GetColor("_BaseColor");
        else if (source.HasProperty("_Color"))
            color = source.color;
        if (broken && color == Color.white && source.HasProperty("_Color"))
            color = source.GetColor("_Color");

        // Couleurs conçues en Gamma : léger lift sous Linear.
        if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            color = new Color(Mathf.Pow(color.r, 0.85f), Mathf.Pow(color.g, 0.85f), Mathf.Pow(color.b, 0.85f), color.a);

        float metallic = source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0f;
        float gloss = source.HasProperty("_Glossiness")
            ? source.GetFloat("_Glossiness")
            : (source.HasProperty("_Smoothness") ? source.GetFloat("_Smoothness") : 0.5f);

        var material = new Material(shader) { name = source.name + " (URP)" };
        Paint(material, color);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", gloss);
        if (transparent)
        {
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        return material;
    }
}
