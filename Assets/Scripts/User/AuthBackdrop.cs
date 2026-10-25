using UnityEngine;
using UnityEngine.Rendering;

namespace DriveSim.User
{
    // Garage 3D derrière les écrans de compte : voiture sur plateau tournant, pistes néon.
    // Tout est construit avec des formes simples de Unity, sans modèle à importer.
    public sealed class AuthBackdrop : MonoBehaviour
    {
        const float IdleSpin = 22f;

        static readonly Color Night = new Color(0.03f, 0.07f, 0.18f);
        static readonly Color Coral = new Color(1f, 0.36f, 0.3f);
        static readonly Color Mint = new Color(0.28f, 0.9f, 0.76f);
        static readonly Color Violet = new Color(0.62f, 0.36f, 1f);
        static readonly Color Lime = new Color(0.55f, 1f, 0.4f);

        public static AuthBackdrop Current { get; private set; }

        Material _base;
        Transform _turntable;
        Transform _car;
        Transform[] _wheels;
        Camera _camera;
        Vector2 _focus = new Vector2(0.5f, 0.72f);
        float _spin = IdleSpin;
        float _time;

        public static AuthBackdrop Build()
        {
            if (Current != null)
                return Current;
            var root = new GameObject("AuthGarage");
            return root.AddComponent<AuthBackdrop>();
        }

        // Point de l'écran (0 à 1) où la voiture doit apparaître, pour laisser la place au formulaire.
        public void SetFocus(Vector2 viewportPoint)
        {
            _focus = viewportPoint;
        }

        public void Celebrate()
        {
            _spin = 320f;
        }

        void Awake()
        {
            Current = this;
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _base = probe.GetComponent<Renderer>().sharedMaterial;
            Destroy(probe);

            SetupCamera();
            SetupLights();
            BuildFloor();
            BuildTurntable();
            _car = BuildCar(_turntable);
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        void Update()
        {
            var dt = Time.deltaTime;
            _time += dt;
            _spin = Mathf.Lerp(_spin, IdleSpin, dt * 1.6f);
            _turntable.Rotate(0f, _spin * dt, 0f, Space.World);
            _car.localPosition = new Vector3(0f, 0.11f + 0.04f * Mathf.Sin(_time * 2.2f), 0f);
            foreach (var wheel in _wheels)
                wheel.Rotate(_spin * 6f * dt, 0f, 0f, Space.Self);
            FrameCamera();
        }

        void SetupCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                _camera = cameraObject.AddComponent<Camera>();
            }
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Night;
            _camera.fieldOfView = 55f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 200f;
            FrameCamera();
        }

        void FrameCamera()
        {
            var sway = Mathf.Sin(_time * 0.35f) * 0.6f;
            var target = new Vector3(0f, 0.75f, 0f);
            _camera.transform.position = new Vector3(sway, 3.1f, -7.4f);
            _camera.transform.LookAt(target);

            var halfV = _camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            var halfH = Mathf.Atan(Mathf.Tan(halfV) * _camera.aspect);
            var pitch = Mathf.Atan((2f * _focus.y - 1f) * Mathf.Tan(halfV)) * Mathf.Rad2Deg;
            var yaw = Mathf.Atan((1f - 2f * _focus.x) * Mathf.Tan(halfH)) * Mathf.Rad2Deg;
            _camera.transform.Rotate(pitch, 0f, 0f, Space.Self);
            _camera.transform.Rotate(0f, yaw, 0f, Space.World);
        }

        void SetupLights()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.28f, 0.45f);

            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                light.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
                light.color = new Color(1f, 0.94f, 0.88f);
                light.intensity = 1.25f;
                light.shadows = LightShadows.Soft;
            }

            AddPointLight(new Vector3(-3.6f, 2.2f, -0.5f), Mint, 7f);
            AddPointLight(new Vector3(3.6f, 2.2f, -0.5f), Violet, 7f);
            AddPointLight(new Vector3(0f, 2.6f, 4.5f), Coral, 6f);
        }

        void AddPointLight(Vector3 position, Color color, float intensity)
        {
            var lightObject = new GameObject("NeonLight");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = 9f;
            light.shadows = LightShadows.None;
        }

        void BuildFloor()
        {
            Part("Floor", PrimitiveType.Plane, transform, new Vector3(0f, 0f, 20f), Vector3.zero,
                new Vector3(12f, 1f, 12f), Paint(new Color(0.05f, 0.11f, 0.26f), 0.35f, 0f));

            var lanes = new[] { Lime, Violet, Coral };
            for (int i = 0; i < 3; i++)
            {
                var x = (i - 1) * 4.2f;
                Part("Lane", PrimitiveType.Cube, transform, new Vector3(x, 0.005f, 30f), Vector3.zero,
                    new Vector3(2.6f, 0.01f, 60f), Paint(Color.Lerp(lanes[i], Night, 0.55f), 0.2f, 0f));
                Part("LaneEdge", PrimitiveType.Cube, transform, new Vector3(x - 1.35f, 0.012f, 30f), Vector3.zero,
                    new Vector3(0.08f, 0.01f, 60f), Paint(lanes[i], 0.6f, 0f));
                Part("LaneEdge", PrimitiveType.Cube, transform, new Vector3(x + 1.35f, 0.012f, 30f), Vector3.zero,
                    new Vector3(0.08f, 0.01f, 60f), Paint(lanes[i], 0.6f, 0f));
            }

            for (int i = 0; i < 6; i++)
            {
                var z = 9f + i * 7f;
                Part("Pillar", PrimitiveType.Cube, transform, new Vector3(-8.5f, 1.6f, z), Vector3.zero,
                    new Vector3(0.35f, 3.2f, 0.35f), Paint(Mint, 0.7f, 0f));
                Part("Pillar", PrimitiveType.Cube, transform, new Vector3(8.5f, 1.6f, z), Vector3.zero,
                    new Vector3(0.35f, 3.2f, 0.35f), Paint(Violet, 0.7f, 0f));
            }
        }

        void BuildTurntable()
        {
            _turntable = new GameObject("Turntable").transform;
            _turntable.SetParent(transform, false);

            Part("Ring", PrimitiveType.Cylinder, transform, new Vector3(0f, 0.02f, 0f), Vector3.zero,
                new Vector3(6.1f, 0.02f, 6.1f), Paint(Mint, 0.8f, 0f));
            Part("Disc", PrimitiveType.Cylinder, _turntable, new Vector3(0f, 0.06f, 0f), Vector3.zero,
                new Vector3(5.7f, 0.05f, 5.7f), Paint(new Color(0.09f, 0.16f, 0.34f), 0.85f, 0.4f));
            for (int i = 0; i < 8; i++)
            {
                var angle = i * 45f;
                var position = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0.115f, 2.45f);
                Part("Marker", PrimitiveType.Cube, _turntable, position, new Vector3(0f, angle, 0f),
                    new Vector3(0.5f, 0.02f, 0.14f), Paint(i % 2 == 0 ? Coral : Color.white, 0.6f, 0f));
            }
        }

        Transform BuildCar(Transform parent)
        {
            var car = new GameObject("Car").transform;
            car.SetParent(parent, false);

            var paint = Paint(Coral, 0.92f, 0.35f);
            var stripe = Paint(Color.white, 0.9f, 0.1f);
            var glass = Paint(new Color(0.06f, 0.1f, 0.2f), 0.97f, 0.6f);
            var tyre = Paint(new Color(0.07f, 0.07f, 0.09f), 0.25f, 0f);
            var rim = Paint(new Color(0.85f, 0.88f, 0.94f), 0.9f, 0.9f);
            var head = Paint(new Color(1f, 0.96f, 0.7f), 1f, 0f);
            var tail = Paint(new Color(1f, 0.12f, 0.2f), 1f, 0f);
            var dark = Paint(new Color(0.1f, 0.12f, 0.18f), 0.5f, 0.2f);

            Part("Body", PrimitiveType.Cube, car, new Vector3(0f, 0.6f, 0f), Vector3.zero, new Vector3(1.9f, 0.5f, 4.1f), paint);
            Part("Nose", PrimitiveType.Cube, car, new Vector3(0f, 0.52f, 1.95f), new Vector3(12f, 0f, 0f), new Vector3(1.86f, 0.34f, 0.7f), paint);
            Part("Cabin", PrimitiveType.Cube, car, new Vector3(0f, 1.08f, -0.25f), Vector3.zero, new Vector3(1.5f, 0.48f, 1.9f), glass);
            Part("Roof", PrimitiveType.Cube, car, new Vector3(0f, 1.34f, -0.3f), Vector3.zero, new Vector3(1.38f, 0.06f, 1.4f), paint);
            Part("StripeL", PrimitiveType.Cube, car, new Vector3(-0.26f, 0.86f, 0f), Vector3.zero, new Vector3(0.2f, 0.02f, 4.12f), stripe);
            Part("StripeR", PrimitiveType.Cube, car, new Vector3(0.26f, 0.86f, 0f), Vector3.zero, new Vector3(0.2f, 0.02f, 4.12f), stripe);
            Part("RoofStripeL", PrimitiveType.Cube, car, new Vector3(-0.26f, 1.375f, -0.3f), Vector3.zero, new Vector3(0.2f, 0.02f, 1.42f), stripe);
            Part("RoofStripeR", PrimitiveType.Cube, car, new Vector3(0.26f, 1.375f, -0.3f), Vector3.zero, new Vector3(0.2f, 0.02f, 1.42f), stripe);
            Part("Spoiler", PrimitiveType.Cube, car, new Vector3(0f, 1.18f, -1.95f), Vector3.zero, new Vector3(2f, 0.08f, 0.45f), paint);
            Part("SpoilerPostL", PrimitiveType.Cube, car, new Vector3(-0.6f, 0.98f, -1.92f), Vector3.zero, new Vector3(0.08f, 0.36f, 0.12f), dark);
            Part("SpoilerPostR", PrimitiveType.Cube, car, new Vector3(0.6f, 0.98f, -1.92f), Vector3.zero, new Vector3(0.08f, 0.36f, 0.12f), dark);
            Part("Splitter", PrimitiveType.Cube, car, new Vector3(0f, 0.34f, 2.15f), Vector3.zero, new Vector3(1.95f, 0.06f, 0.3f), dark);
            Part("Diffuser", PrimitiveType.Cube, car, new Vector3(0f, 0.36f, -2.08f), Vector3.zero, new Vector3(1.8f, 0.14f, 0.12f), dark);
            Part("HeadL", PrimitiveType.Cube, car, new Vector3(-0.62f, 0.62f, 2.06f), Vector3.zero, new Vector3(0.42f, 0.12f, 0.06f), head);
            Part("HeadR", PrimitiveType.Cube, car, new Vector3(0.62f, 0.62f, 2.06f), Vector3.zero, new Vector3(0.42f, 0.12f, 0.06f), head);
            Part("TailL", PrimitiveType.Cube, car, new Vector3(-0.62f, 0.68f, -2.06f), Vector3.zero, new Vector3(0.5f, 0.1f, 0.06f), tail);
            Part("TailR", PrimitiveType.Cube, car, new Vector3(0.62f, 0.68f, -2.06f), Vector3.zero, new Vector3(0.5f, 0.1f, 0.06f), tail);

            _wheels = new Transform[4];
            var index = 0;
            foreach (var side in new[] { -1f, 1f })
            {
                foreach (var axle in new[] { 1.3f, -1.3f })
                {
                    var wheel = new GameObject("Wheel").transform;
                    wheel.SetParent(car, false);
                    wheel.localPosition = new Vector3(side * 0.98f, 0.4f, axle);
                    Part("Tyre", PrimitiveType.Cylinder, wheel, Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(0.8f, 0.14f, 0.8f), tyre);
                    Part("Rim", PrimitiveType.Cylinder, wheel, new Vector3(side * 0.06f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.48f, 0.1f, 0.48f), rim);
                    Part("Spoke", PrimitiveType.Cube, wheel, new Vector3(side * 0.17f, 0f, 0f), Vector3.zero, new Vector3(0.03f, 0.44f, 0.08f), dark);
                    _wheels[index++] = wheel;
                }
            }

            Part("Shadow", PrimitiveType.Cylinder, car, new Vector3(0f, 0.006f, 0f), Vector3.zero, new Vector3(2.6f, 0.005f, 4.6f),
                Paint(new Color(0.02f, 0.04f, 0.1f), 0f, 0f));
            return car;
        }

        GameObject Part(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 euler, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            var collider = part.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localEulerAngles = euler;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        Material Paint(Color color, float smoothness, float metallic)
        {
            var material = new Material(_base);
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            return material;
        }
    }
}
