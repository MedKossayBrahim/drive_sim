using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

static class BoatRunnerSceneUpgrade
{
    const string ScenePath = "Assets/Scenes/BoatRunner.unity";
    const string ModelFolder = "Assets/BoatRunnerModels";
    const string MaterialFolder = "Assets/Materials";

    [MenuItem("Tools/Boat Runner/Upgrade BoatRunner Scene")]
    static void Upgrade()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogError("Open Assets/Scenes/BoatRunner.unity before applying the BoatRunner upgrade.");
            return;
        }
        EnsureFolder(ModelFolder);
        EnsureFolder(MaterialFolder);
        var materials = CreateMaterials();
        var hullMesh = LoadOrCreateMesh(ModelFolder + "/MotorboatHull.asset", MakeHullMesh());
        var deckMesh = LoadOrCreateMesh(ModelFolder + "/MotorboatDeck.asset", MakeDeckMesh());
        var waterMesh = LoadOrCreateMesh(ModelFolder + "/OceanSurface.asset", MakeWaterMesh());
        UpgradeBoat(hullMesh, deckMesh, materials);
        UpgradeOcean(waterMesh, materials.ocean);
        UpgradeCamera();
        UpgradeLighting();
        UpgradeObstacles(materials);
        UpgradeHud();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("BoatRunner scene upgraded: PC controls, gameplay reset, stylized visuals, lighting and HUD saved.");
    }

    struct Materials
    {
        public Material ocean, hull, deck, trim, cockpit, glass, engine, rock, wood, buoy, foam;
    }

    static Materials CreateMaterials()
    {
        return new Materials
        {
            ocean = MaterialAsset("Ocean", "BoatRunner/Ocean", new Color(0.02f, 0.15f, 0.24f), 0.15f, 0.82f),
            hull = MaterialAsset("Hull Deep Teal", "Standard", new Color(0.025f, 0.16f, 0.21f), 0.28f, 0.78f),
            deck = MaterialAsset("Deck Warm Ivory", "Standard", new Color(0.89f, 0.79f, 0.60f), 0.05f, 0.46f),
            trim = MaterialAsset("Trim Coral", "Standard", new Color(0.97f, 0.36f, 0.15f), 0.24f, 0.68f),
            cockpit = MaterialAsset("Cockpit Navy", "Standard", new Color(0.035f, 0.09f, 0.12f), 0.04f, 0.35f),
            glass = MaterialAsset("Windshield Sea Glass", "Standard", new Color(0.14f, 0.49f, 0.56f), 0.28f, 0.88f),
            engine = MaterialAsset("Motor Graphite", "Standard", new Color(0.09f, 0.12f, 0.13f), 0.45f, 0.56f),
            rock = MaterialAsset("Hazard Slate", "Standard", new Color(0.31f, 0.39f, 0.39f), 0.08f, 0.35f),
            wood = MaterialAsset("Hazard Driftwood", "Standard", new Color(0.38f, 0.22f, 0.12f), 0f, 0.3f),
            buoy = MaterialAsset("Hazard Signal Orange", "Standard", new Color(0.95f, 0.25f, 0.08f), 0.04f, 0.48f),
            foam = FoamAsset()
        };
    }

    static Material MaterialAsset(string name, string shaderName, Color color, float metallic, float smoothness)
    {
        string path = MaterialFolder + "/" + name.Replace(' ', '_') + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new System.InvalidOperationException("Could not find a shader for " + name);
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.name = name; mat.color = color; mat.enableInstancing = true;
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
        if (name == "Ocean")
        {
            mat.SetColor("_DeepColor", new Color(0.018f, 0.12f, 0.2f));
            mat.SetColor("_MidColor", new Color(0.025f, 0.36f, 0.42f));
            mat.SetColor("_CrestColor", new Color(0.27f, 0.76f, 0.73f));
            mat.SetFloat("_WaveHeight", 0.2f);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material FoamAsset()
    {
        string path = MaterialFolder + "/Wake_Foam.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = new Color(0.72f, 0.94f, 0.89f, 0.56f);
        mat.renderQueue = 3000;
        if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 3f);
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Mesh LoadOrCreateMesh(string path, Mesh candidate)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) { Object.DestroyImmediate(candidate); return existing; }
        AssetDatabase.CreateAsset(candidate, path);
        return candidate;
    }

    static Mesh MakeHullMesh()
    {
        float[,] stations = {
            { -1.86f, .64f, -.24f, -.015f }, { -1.42f, .79f, -.38f, -.01f },
            { -.56f, .85f, -.46f, .00f }, { .46f, .74f, -.39f, .025f },
            { 1.20f, .51f, -.25f, .055f }, { 1.72f, .24f, -.10f, .085f }, { 2.02f, .025f, .015f, .11f }
        };
        int count = stations.GetLength(0);
        var vertices = new Vector3[count * 4];
        var uv = new Vector2[vertices.Length];
        for (int i = 0; i < count; i++)
        {
            float z = stations[i, 0], w = stations[i, 1], bottom = stations[i, 2], top = stations[i, 3];
            int o = i * 4;
            vertices[o] = new Vector3(-w, top, z);
            vertices[o + 1] = new Vector3(-w * .77f, bottom, z);
            vertices[o + 2] = new Vector3(w * .77f, bottom, z);
            vertices[o + 3] = new Vector3(w, top, z);
            for (int j = 0; j < 4; j++) uv[o + j] = new Vector2(j / 3f, i / (float)(count - 1));
        }
        var triangles = new List<int>();
        for (int i = 0; i < count - 1; i++)
        for (int edge = 0; edge < 3; edge++)
        {
            int a = i * 4 + edge, b = a + 1, c = a + 4, d = c + 1;
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(b); triangles.Add(d); triangles.Add(c);
        }
        var mesh = new Mesh { name = "Motorboat Hull", vertices = vertices, uv = uv, triangles = triangles.ToArray() };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh MakeDeckMesh()
    {
        float[,] stations = {
            { -1.79f, .61f, .015f }, { -1.4f, .76f, .015f }, { -.56f, .82f, .02f },
            { .46f, .71f, .045f }, { 1.2f, .48f, .075f }, { 1.72f, .22f, .105f }, { 2.0f, .025f, .13f }
        };
        int count = stations.GetLength(0);
        var verts = new Vector3[count * 2]; var uv = new Vector2[verts.Length]; var tris = new List<int>();
        for (int i = 0; i < count; i++)
        {
            verts[i * 2] = new Vector3(-stations[i, 1], stations[i, 2], stations[i, 0]);
            verts[i * 2 + 1] = new Vector3(stations[i, 1], stations[i, 2], stations[i, 0]);
            uv[i * 2] = new Vector2(0, i / (float)(count - 1)); uv[i * 2 + 1] = new Vector2(1, i / (float)(count - 1));
        }
        for (int i = 0; i < count - 1; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d);
        }
        var mesh = new Mesh { name = "Motorboat Deck", vertices = verts, uv = uv, triangles = tris.ToArray() };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }

    static Mesh MakeWaterMesh()
    {
        const int xCount = 140, zCount = 190;
        const float width = 260f, length = 380f;
        var vertices = new Vector3[xCount * zCount]; var uv = new Vector2[vertices.Length];
        for (int z = 0; z < zCount; z++)
        for (int x = 0; x < xCount; x++)
        {
            int i = z * xCount + x;
            vertices[i] = new Vector3((x / (float)(xCount - 1) - .5f) * width, 0f, (z / (float)(zCount - 1) - .5f) * length);
            uv[i] = new Vector2(x / (float)(xCount - 1), z / (float)(zCount - 1));
        }
        var triangles = new int[(xCount - 1) * (zCount - 1) * 6]; int t = 0;
        for (int z = 0; z < zCount - 1; z++)
        for (int x = 0; x < xCount - 1; x++)
        {
            int a = z * xCount + x, b = a + 1, c = a + xCount, d = c + 1;
            triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
            triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
        }
        var mesh = new Mesh { name = "Ocean Surface Grid", vertices = vertices, uv = uv, triangles = triangles };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }

    static void UpgradeBoat(Mesh hull, Mesh deck, Materials m)
    {
        var boat = GameObject.Find("Boat");
        if (boat == null) throw new System.InvalidOperationException("Boat was not found in BoatRunner.unity.");
        boat.transform.position = new Vector3(0f, .47f, 0f); boat.transform.rotation = Quaternion.identity; boat.transform.localScale = Vector3.one;
        foreach (var capsule in boat.GetComponents<CapsuleCollider>()) Undo.DestroyObjectImmediate(capsule);
        var rootRenderer = boat.GetComponent<MeshRenderer>(); if (rootRenderer != null) rootRenderer.enabled = false;
        var box = boat.GetComponent<BoxCollider>(); if (box == null) box = Undo.AddComponent<BoxCollider>(boat);
        box.center = new Vector3(0f, .02f, 0f); box.size = new Vector3(1.65f, .65f, 3.45f); box.isTrigger = true;
        var rb = boat.GetComponent<Rigidbody>(); if (rb == null) rb = Undo.AddComponent<Rigidbody>(boat);
        rb.useGravity = false; rb.mass = 4.2f; rb.linearDamping = 1.2f; rb.angularDamping = 3.5f;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        var controller = boat.GetComponent<BoatController>(); if (controller == null) controller = Undo.AddComponent<BoatController>(boat);
        var oldVisuals = boat.transform.Find("BoatVisuals"); if (oldVisuals != null) Undo.DestroyObjectImmediate(oldVisuals.gameObject);
        var art = new GameObject("BoatVisuals"); art.transform.SetParent(boat.transform, false);
        MeshPart(art.transform, "Tapered Hull", hull, m.hull);
        MeshPart(art.transform, "Deck", deck, m.deck);
        Cube(art.transform, "Cockpit Well", new Vector3(0f, .13f, -.32f), new Vector3(.92f, .12f, .96f), m.cockpit, Quaternion.identity);
        Cube(art.transform, "Cushion", new Vector3(0f, .25f, -.63f), new Vector3(.66f, .16f, .42f), m.trim, Quaternion.identity);
        Cube(art.transform, "Seat Back", new Vector3(0f, .43f, -.86f), new Vector3(.62f, .44f, .14f), m.cockpit, Quaternion.Euler(-7f, 0f, 0f));
        Cube(art.transform, "Helm Console", new Vector3(0f, .37f, .04f), new Vector3(.48f, .35f, .4f), m.deck, Quaternion.identity);
        Cube(art.transform, "Dashboard", new Vector3(0f, .56f, .23f), new Vector3(.42f, .13f, .08f), m.cockpit, Quaternion.identity);
        Cube(art.transform, "Windshield", new Vector3(0f, .68f, .43f), new Vector3(.78f, .38f, .07f), m.glass, Quaternion.Euler(-17f, 0f, 0f));
        Cube(art.transform, "Port Stripe", new Vector3(-.765f, -.035f, -.14f), new Vector3(.055f, .11f, 1.72f), m.trim, Quaternion.identity);
        Cube(art.transform, "Starboard Stripe", new Vector3(.765f, -.035f, -.14f), new Vector3(.055f, .11f, 1.72f), m.trim, Quaternion.identity);
        Cube(art.transform, "Bow Accent", new Vector3(0f, .045f, 1.28f), new Vector3(.12f, .055f, .65f), m.trim, Quaternion.identity);
        Cube(art.transform, "Port Gunwale", new Vector3(-.55f, .12f, -.36f), new Vector3(.07f, .12f, .92f), m.deck, Quaternion.identity);
        Cube(art.transform, "Starboard Gunwale", new Vector3(.55f, .12f, -.36f), new Vector3(.07f, .12f, .92f), m.deck, Quaternion.identity);
        Cube(art.transform, "Outboard Cowling", new Vector3(0f, .09f, -1.45f), new Vector3(.56f, .42f, .46f), m.engine, Quaternion.identity);
        Cube(art.transform, "Motor Highlight", new Vector3(0f, .19f, -1.69f), new Vector3(.28f, .08f, .07f), m.trim, Quaternion.identity);
        Cylinder(art.transform, "Outboard Leg", new Vector3(0f, -.17f, -1.51f), new Vector3(.11f, .38f, .11f), m.engine, Quaternion.identity);
        Cube(art.transform, "Propeller Blade A", new Vector3(0f, -.38f, -1.55f), new Vector3(.62f, .07f, .13f), m.engine, Quaternion.identity);
        Cube(art.transform, "Propeller Blade B", new Vector3(0f, -.38f, -1.55f), new Vector3(.13f, .07f, .62f), m.engine, Quaternion.identity);
        var wakeObject = new GameObject("Wake Trail"); wakeObject.transform.SetParent(art.transform, false); wakeObject.transform.localPosition = new Vector3(0f, -.39f, -1.58f);
        var trail = wakeObject.AddComponent<TrailRenderer>(); trail.time = 3.4f; trail.minVertexDistance = .13f; trail.widthMultiplier = 1.05f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, .08f), new Keyframe(.2f, .55f), new Keyframe(1f, 1f));
        trail.startColor = new Color(.82f, .97f, .94f, .72f); trail.endColor = new Color(.8f, .96f, .93f, 0f);
        trail.sharedMaterial = m.foam; trail.shadowCastingMode = ShadowCastingMode.Off; trail.receiveShadows = false;
        EditorUtility.SetDirty(boat); EditorUtility.SetDirty(controller);
    }

    static void UpgradeOcean(Mesh mesh, Material material)
    {
        var oldWater = GameObject.Find("Open Water");
        var ocean = GameObject.Find("Ocean Surface");
        if (ocean == null) ocean = new GameObject("Ocean Surface");
        if (oldWater != null && oldWater != ocean) Undo.DestroyObjectImmediate(oldWater);
        ocean.transform.position = new Vector3(0f, .005f, 0f);
        var filter = ocean.GetComponent<MeshFilter>(); if (filter == null) filter = Undo.AddComponent<MeshFilter>(ocean);
        var renderer = ocean.GetComponent<MeshRenderer>(); if (renderer == null) renderer = Undo.AddComponent<MeshRenderer>(ocean);
        filter.sharedMesh = mesh; renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        var surface = ocean.GetComponent<OceanSurface>(); if (surface == null) surface = Undo.AddComponent<OceanSurface>(ocean);
        surface.SetTarget(GameObject.Find("Boat").transform);
        var collider = ocean.GetComponent<Collider>(); if (collider != null) Undo.DestroyObjectImmediate(collider);
        EditorUtility.SetDirty(ocean);
    }

    static void UpgradeCamera()
    {
        var cameraGo = GameObject.Find("Main Camera");
        var boat = GameObject.Find("Boat");
        var camera = cameraGo.GetComponent<Camera>(); camera.orthographic = false; camera.fieldOfView = 50f; camera.nearClipPlane = .1f; camera.farClipPlane = 260f;
        camera.clearFlags = CameraClearFlags.Skybox;
        var follow = cameraGo.GetComponent<FollowBoatCamera>(); if (follow == null) follow = Undo.AddComponent<FollowBoatCamera>(cameraGo);
        follow.Configure(boat.transform, new Vector3(0f, 8f, -9.5f), new Vector3(0f, .4f, 8f));
        cameraGo.transform.position = new Vector3(0f, 8f, -9.5f);
        cameraGo.transform.rotation = Quaternion.Euler(32f, 0f, 0f);
        EditorUtility.SetDirty(cameraGo);
    }

    static void UpgradeLighting()
    {
        var lightGo = GameObject.Find("Directional Light");
        if (lightGo == null) lightGo = new GameObject("Directional Light");
        var light = lightGo.GetComponent<Light>(); if (light == null) light = Undo.AddComponent<Light>(lightGo);
        light.type = LightType.Directional; light.color = new Color(1f, .84f, .66f); light.intensity = 1.25f;
        light.shadows = LightShadows.Soft; light.shadowStrength = .72f;
        lightGo.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.46f, .66f, .74f);
        RenderSettings.ambientEquatorColor = new Color(.22f, .36f, .39f);
        RenderSettings.ambientGroundColor = new Color(.09f, .14f, .16f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(.38f, .61f, .65f); RenderSettings.fogDensity = .0017f;
        EditorUtility.SetDirty(lightGo);
    }

    static void UpgradeObstacles(Materials m)
    {
        var boat = GameObject.Find("Boat");
        var manager = GameObject.Find("Game Manager").GetComponent<BoatGameManager>();
        var go = GameObject.Find("Obstacle Spawner");
        var spawner = go.GetComponent<ObstacleSpawner>();
        spawner.Configure(boat.transform, manager, m.rock, m.wood, m.buoy);
        EditorUtility.SetDirty(spawner);
    }

    static void UpgradeHud()
    {
        var canvasGo = GameObject.Find("Game UI");
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = .45f;
        var canvas = canvasGo.GetComponent<Canvas>(); canvas.sortingOrder = 10;
        var score = GameObject.Find("Distance").GetComponent<Text>();
        StyleText(score, 50, TextAnchor.UpperLeft, new Color(.95f, .98f, .94f));
        SetRect(score.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(52f, -42f), new Vector2(430f, 66f), new Vector2(0f, 1f));
        var lives = GameObject.Find("Lives").GetComponent<Text>();
        StyleText(lives, 46, TextAnchor.UpperRight, new Color(1f, .76f, .5f));
        SetRect(lives.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-52f, -42f), new Vector2(280f, 66f), new Vector2(1f, 1f));
        var calibrate = GameObject.Find("Calibrate"); if (calibrate != null) Undo.DestroyObjectImmediate(calibrate);
        var hint = GameObject.Find("Controls Hint"); if (hint == null) hint = MakeText(canvasGo.transform, "Controls Hint");
        var hintText = hint.GetComponent<Text>(); hintText.text = "A / D   STEER        W   ACCELERATE        S   BRAKE";
        StyleText(hintText, 28, TextAnchor.MiddleCenter, new Color(.82f, .91f, .9f));
        SetRect(hintText.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 26f), new Vector2(1100f, 42f), new Vector2(.5f, 0f));
        var speed = GameObject.Find("Speed"); if (speed == null) speed = MakeText(canvasGo.transform, "Speed");
        var speedText = speed.GetComponent<Text>(); speedText.text = "SPEED  29 km/h";
        StyleText(speedText, 30, TextAnchor.LowerLeft, new Color(.71f, .91f, .88f));
        SetRect(speedText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(52f, 88f), new Vector2(340f, 52f), new Vector2(0f, 0f));
        var panel = canvasGo.transform.Find("Game Over Panel").gameObject; panel.SetActive(false);
        var panelImage = panel.GetComponent<Image>(); panelImage.color = new Color(.015f, .055f, .075f, .9f);
        var title = panel.transform.Find("Game Over").GetComponent<Text>(); title.text = "VOYAGE OVER";
        StyleText(title, 76, TextAnchor.MiddleCenter, new Color(.97f, .84f, .64f));
        SetRect(title.rectTransform, new Vector2(.5f, .66f), new Vector2(.5f, .66f), Vector2.zero, new Vector2(900f, 130f), new Vector2(.5f, .5f));
        var final = panel.transform.Find("Final Distance").GetComponent<Text>();
        StyleText(final, 42, TextAnchor.MiddleCenter, Color.white);
        SetRect(final.rectTransform, new Vector2(.5f, .53f), new Vector2(.5f, .53f), Vector2.zero, new Vector2(760f, 80f), new Vector2(.5f, .5f));
        var restartGo = panel.transform.Find("Restart").gameObject; var restart = restartGo.GetComponent<Button>();
        var restartImage = restartGo.GetComponent<Image>(); restartImage.color = new Color(.9f, .34f, .16f, 1f);
        SetRect(restartGo.GetComponent<RectTransform>(), new Vector2(.5f, .37f), new Vector2(.5f, .37f), Vector2.zero, new Vector2(370f, 105f), new Vector2(.5f, .5f));
        var restartLabel = restartGo.transform.Find("Label").GetComponent<Text>(); restartLabel.text = "SAIL AGAIN  (R)";
        StyleText(restartLabel, 30, TextAnchor.MiddleCenter, Color.white);
        SetRect(restartLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(.5f, .5f));
        var manager = GameObject.Find("Game Manager").GetComponent<BoatGameManager>();
        var spawner = GameObject.Find("Obstacle Spawner").GetComponent<ObstacleSpawner>();
        manager.Initialize(GameObject.Find("Boat").GetComponent<BoatController>(), score, lives, panel, final, spawner);
        for (int i = restart.onClick.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(restart.onClick, i);
        UnityEventTools.AddPersistentListener(restart.onClick, manager.Restart);
        EditorUtility.SetDirty(restart); EditorUtility.SetDirty(manager); EditorUtility.SetDirty(scaler);
    }

    static GameObject MakeText(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.raycastTarget = false;
        return go;
    }
    static void StyleText(Text text, int size, TextAnchor anchor, Color color)
    {
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size; text.fontStyle = FontStyle.Bold;
        text.alignment = anchor; text.color = color; text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow; text.raycastTarget = false;
        EditorUtility.SetDirty(text);
    }
    static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.anchoredPosition = pos; rect.sizeDelta = size; rect.pivot = pivot;
        EditorUtility.SetDirty(rect);
    }
    static void MeshPart(Transform parent, string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = material;
        var renderer = go.GetComponent<MeshRenderer>(); renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
    }
    static void Cube(Transform parent, string name, Vector3 position, Vector3 scale, Material material, Quaternion rotation)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = scale; go.transform.localRotation = rotation;
        var collider = go.GetComponent<Collider>(); if (collider != null) Object.DestroyImmediate(collider);
        go.GetComponent<Renderer>().sharedMaterial = material;
    }
    static void Cylinder(Transform parent, string name, Vector3 position, Vector3 scale, Material material, Quaternion rotation)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = scale; go.transform.localRotation = rotation;
        var collider = go.GetComponent<Collider>(); if (collider != null) Object.DestroyImmediate(collider);
        go.GetComponent<Renderer>().sharedMaterial = material;
    }
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string name = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}



