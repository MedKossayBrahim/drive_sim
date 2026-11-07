using UnityEngine;

namespace Project.Plane
{
    /// <summary>Réglages d'un ciel, de sa lumière principale et du brouillard.</summary>
    [CreateAssetMenu(fileName = "SkyPreset", menuName = "Sky Courier/Sky Preset")]
    public sealed class SkyPreset : ScriptableObject
    {
        [SerializeField] private Material skyMaterial = null;
        [SerializeField] private Color directionalLightColor = Color.white;
        [SerializeField, Min(0f)] private float directionalLightIntensity = 1f;
        [SerializeField] private Vector3 sunEuler = new Vector3(60f, -30f, 0f);
        [SerializeField] private bool fogEnabled = true;
        [SerializeField] private Color fogColor = new Color(0.65f, 0.75f, 0.85f);
        [SerializeField, Min(0f)] private float fogDensity = 0.0015f;
        [SerializeField, Range(0f, 8f)] private float exposure = 1f;
        [SerializeField, Tooltip("Active une rotation horizontale du panorama, indépendante de la rotation du soleil.")]
        private bool useSkyRotationOffset = false;
        [SerializeField, Range(-360f, 360f), Tooltip("Décalage autour de Y en degrés. Réoriente le sol du HDRI sans le supprimer.")]
        private float skyRotationOffset = 0f;

        public Material SkyMaterial => skyMaterial;
        public Color DirectionalLightColor => directionalLightColor;
        public float DirectionalLightIntensity => Mathf.Max(0f, directionalLightIntensity);
        public Vector3 SunEuler => sunEuler;
        public bool FogEnabled => fogEnabled;
        public Color FogColor => fogColor;
        public float FogDensity => Mathf.Max(0f, fogDensity);
        public float Exposure => Mathf.Clamp(exposure, 0f, 8f);
        public float SkyRotationOffset => useSkyRotationOffset ? skyRotationOffset : 0f;

#if UNITY_EDITOR
        /// <summary>Initialisation par le menu éditeur lors de la création de l'asset.</summary>
        public void InitializeDefaults(Material material, Color lightColor, float lightIntensity,
            Vector3 rotation, Color hazeColor, float hazeDensity, bool rotateSky = false, float skyOffset = 0f)
        {
            skyMaterial = material;
            directionalLightColor = lightColor;
            directionalLightIntensity = lightIntensity;
            sunEuler = rotation;
            fogEnabled = true;
            fogColor = hazeColor;
            fogDensity = hazeDensity;
            exposure = 1f;
            useSkyRotationOffset = rotateSky;
            skyRotationOffset = skyOffset;
        }

        /// <summary>Répare la référence du matériau sans écraser les réglages personnalisés.</summary>
        public void SetSkyMaterial(Material material) => skyMaterial = material;
#endif
    }
}
