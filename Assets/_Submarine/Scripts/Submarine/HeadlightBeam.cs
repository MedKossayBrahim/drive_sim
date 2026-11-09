using UnityEngine;
using UnityEngine.Rendering;

namespace DriveSim.Submarine
{
    /// <summary>
    /// Faux-volumetric headlight shaft.
    /// Builds a stack of camera-facing soft slices along the beam axis. Overlapping
    /// them with additive blending gives a believable cone of scattered light that
    /// works both from the side and when looking straight down the beam.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class HeadlightBeam : MonoBehaviour
    {
        [Header("Source")]
        public Light source;

        [Header("Look")]
        public Color color = new Color(0.55f, 0.82f, 1f);
        public float intensity = 1.6f;
        public float maxLength = 85f;
        public float widthFactor = 0.72f;
        public float corePower = 2.2f;
        public float fadePower = 1.6f;
        public float noiseScale = 0.35f;
        public float noiseSpeed = 0.35f;
        public float softDistance = 2.5f;
        public int sliceCount = 14;

        [Header("Depth shaping")]
        public float depthForTightBeam = 420f;
        public float deepLengthFactor = 0.62f;
        public float deepWidthFactor = 0.8f;

        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private Material _material;
        private Mesh _mesh;
        private Transform _camera;
        private SubmarineController _controller;
        private BatterySystem _battery;
        private Vector3[] _vertices;
        private Vector2[] _uvs;
        private Color[] _colors;
        private float _smooth = 1f;

        void Awake()
        {
            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            _controller = GetComponentInParent<SubmarineController>();
            _battery = GetComponentInParent<BatterySystem>();
            if (source == null) source = GetComponentInParent<Light>();

            sliceCount = Mathf.Max(4, sliceCount);
            BuildMesh();
            _material = BuildMaterial();
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            if (_material == null) enabled = false;
        }

        void BuildMesh()
        {
            int count = sliceCount;
            _vertices = new Vector3[count * 4];
            _uvs = new Vector2[count * 4];
            _colors = new Color[count * 4];

            var triangles = new int[count * 6];
            for (int i = 0; i < count; i++)
            {
                int v = i * 4;
                int t = i * 6;
                triangles[t + 0] = v + 0;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
            }

            _mesh = new Mesh { name = "HeadlightBeamSlices" };
            _mesh.MarkDynamic();
            _mesh.vertices = _vertices;
            _mesh.uv = _uvs;
            _mesh.colors = _colors;
            _mesh.triangles = triangles;
            _filter.sharedMesh = _mesh;
        }

        Material BuildMaterial()
        {
            var shader = Shader.Find("DriveSim/HeadlightBeam");
            if (shader == null)
            {
                Debug.LogWarning("HeadlightBeam: shader 'DriveSim/HeadlightBeam' not found.");
                return null;
            }

            var mat = new Material(shader) { hideFlags = HideFlags.DontSave };
            mat.SetColor("_Color", color);
            mat.SetFloat("_Intensity", intensity);
            mat.SetFloat("_Length", maxLength);
            mat.SetFloat("_CorePower", corePower);
            mat.SetFloat("_FadePower", fadePower);
            mat.SetFloat("_NoiseScale", noiseScale);
            mat.SetFloat("_NoiseSpeed", noiseSpeed);
            mat.SetFloat("_SoftDistance", softDistance);
            return mat;
        }

        void LateUpdate()
        {
            if (source == null || _material == null) return;

            if (_camera == null && Camera.main != null) _camera = Camera.main.transform;
            if (_camera == null) return;

            bool on = source.enabled && source.intensity > 0.02f;
            _smooth = Mathf.MoveTowards(_smooth, on ? 1f : 0f, Time.deltaTime * 6f);
            if (_smooth <= 0.001f)
            {
                _renderer.enabled = false;
                return;
            }
            _renderer.enabled = true;

            Vector3 origin = source.transform.position + source.transform.forward * 0.2f;
            Vector3 dir = source.transform.forward;
            Vector3 toCamera = _camera.position - origin;
            if (toCamera.sqrMagnitude < 0.0001f) return;

            // Keep the mesh in world space; the beam object itself stays at identity.
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;

            float depth = _controller != null ? _controller.Depth : 0f;
            float depth01 = Mathf.Clamp01(depth / Mathf.Max(depthForTightBeam, 1f));

            float length = Mathf.Lerp(maxLength, maxLength * deepLengthFactor, depth01);
            length = Mathf.Min(length, source.range * 0.85f);
            float widthScale = widthFactor * Mathf.Lerp(1f, deepWidthFactor, depth01);

            // Cone radius from the light's actual spot angle.
            float halfAngle = source.spotAngle * 0.5f * Mathf.Deg2Rad;
            float tanHalf = Mathf.Tan(halfAngle);

            Vector3 camRight = _camera.right;
            Vector3 camUp = _camera.up;

            float perSlice = 2.2f / sliceCount;
            int n = sliceCount;
            for (int i = 0; i < n; i++)
            {
                float t = (i + 1f) / n;
                float dist = t * length;
                Vector3 center = origin + dir * dist;

                float radius = Mathf.Min(tanHalf * dist * widthScale, 55f);
                Vector3 right = camRight * radius;
                Vector3 up = camUp * radius;

                int v = i * 4;
                _vertices[v + 0] = center - right - up;
                _vertices[v + 1] = center + right - up;
                _vertices[v + 2] = center - right + up;
                _vertices[v + 3] = center + right + up;

                _uvs[v + 0] = new Vector2(0f, 0f);
                _uvs[v + 1] = new Vector2(1f, 0f);
                _uvs[v + 2] = new Vector2(0f, 1f);
                _uvs[v + 3] = new Vector2(1f, 1f);

                float fade = Mathf.Pow(1f - t, fadePower) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.12f, t));
                float a = perSlice * fade;
                _colors[v + 0] = new Color(1f, 1f, 1f, a);
                _colors[v + 1] = new Color(1f, 1f, 1f, a);
                _colors[v + 2] = new Color(1f, 1f, 1f, a);
                _colors[v + 3] = new Color(1f, 1f, 1f, a);
            }

            _mesh.vertices = _vertices;
            _mesh.uv = _uvs;
            _mesh.colors = _colors;
            _mesh.RecalculateBounds();

            float power = _smooth * intensity;
            if (_battery != null && _battery.BatteryPercent < 0.25f)
                power *= 0.7f + Mathf.Sin(Time.time * 12f) * 0.18f;

            _material.SetFloat("_Intensity", power);
            _material.SetFloat("_Length", length);
            _material.SetFloat("_SoftDistance", softDistance);
            _material.SetFloat("_CorePower", corePower);
            _material.SetFloat("_FadePower", fadePower);
            _material.SetFloat("_NoiseScale", noiseScale);
            _material.SetFloat("_NoiseSpeed", noiseSpeed);
            _material.SetColor("_Color", color);
        }
    }
}
