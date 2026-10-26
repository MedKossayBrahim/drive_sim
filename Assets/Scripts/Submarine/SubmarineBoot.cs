using System.Collections;
using System.Collections.Generic;
using DriveSim.User;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DriveSim.Submarine
{
    /// <summary>
    /// Point d'entrée du simulateur — VERSION HAUTE-FIDÉLITÉ GRAPHIQUE MODERNE.
    /// Construction procédurale AAA :
    /// - Sous-marin ultra-détaillé avec géométrie modulaire, rudders dynamiques, bras robotique et faisceaux volumétriques
    /// - Hélice animée avec traînées de cavitation à haute vitesse
    /// - Éclairage volumétrique doux (spot principal + projecteurs secondaires)
    /// - Poussières de faisceau luminescent (backscatter) et sillage de sédiments au sol (seabed wake)
    /// - Surface océanique ondulante avec diffraction et réfraction lumineuse
    /// - Végétation abyssale, vers tubicoles hydrothermaux, méduses bioluminescentes dérivantes
    /// - Cheminées thermiques avec panaches de fumée noire et étincelles incandescentes
    /// - Post-processing cinématique URP synchronisé avec la profondeur océanique.
    /// </summary>
    public sealed class SubmarineBoot : MonoBehaviour
    {
        public const string SceneName = "SubmarineScene";

        // ── Palette chromatique soignée ─────────────────────────────
        static readonly Color HullDark    = new Color(0.05f, 0.08f, 0.12f);
        static readonly Color HullMid     = new Color(0.09f, 0.13f, 0.18f);
        static readonly Color HullLight   = new Color(0.15f, 0.20f, 0.25f);
        static readonly Color HullBronze  = new Color(0.24f, 0.18f, 0.12f);
        static readonly Color RockDark    = new Color(0.03f, 0.05f, 0.07f);
        static readonly Color RockMid     = new Color(0.06f, 0.09f, 0.12f);
        static readonly Color SandTint    = new Color(0.10f, 0.13f, 0.12f);
        static readonly Color Cyan        = new Color(0.3f, 0.9f, 1f);
        static readonly Color CyanDim     = new Color(0.15f, 0.55f, 0.7f);
        static readonly Color Mint        = new Color(0.3f, 0.95f, 0.75f);
        static readonly Color Amber       = new Color(1f, 0.78f, 0.3f);
        static readonly Color AmberHot    = new Color(1f, 0.52f, 0.12f);
        static readonly Color VentOrange  = new Color(1f, 0.40f, 0.06f);
        static readonly Color Deep        = new Color(0.005f, 0.015f, 0.04f);
        static readonly Color CreatureCol = new Color(0.035f, 0.10f, 0.13f);
        static readonly Color CreatureEye = new Color(0.35f, 1f, 0.85f);
        static readonly Color CreatureVein= new Color(0.18f, 0.85f, 0.7f);
        static readonly Color CoralPink   = new Color(0.78f, 0.22f, 0.38f);
        static readonly Color CoralPurple = new Color(0.42f, 0.15f, 0.58f);
        static readonly Color KelpGreen   = new Color(0.06f, 0.20f, 0.10f);
        static readonly Color WarmWhite   = new Color(1f, 0.94f, 0.82f);
        static readonly Color PortGlass   = new Color(0.68f, 0.88f, 1f);
        static readonly Color JellyGlow   = new Color(0.35f, 0.95f, 0.85f);
        static readonly Color JellyBell   = new Color(0.25f, 0.75f, 0.90f, 0.45f);
        static readonly Color SiltCol     = new Color(0.22f, 0.25f, 0.22f, 0.35f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (SceneManager.GetActiveScene().name != SceneName)
                return;
            if (FindFirstObjectByType<SubmarineBoot>() != null)
                return;
            new GameObject("SubmarineBoot").AddComponent<SubmarineBoot>();
        }

        void Awake()
        {
            EnsureEventSystem();
            ForceLandscape();
        }

        static void ForceLandscape()
        {
            if (!Application.isMobilePlatform) return;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }

        void Start()
        {
            QualitySettings.antiAliasing = 4;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.vSyncCount = 1;
            QualitySettings.shadows = UnityEngine.ShadowQuality.All;
            QualitySettings.shadowResolution = UnityEngine.ShadowResolution.VeryHigh;
            QualitySettings.shadowDistance = 220f;
            if (Camera.main != null) Camera.main.allowHDR = true;

            if (FindFirstObjectByType<SubmarineController>() == null)
                BuildWorld();

            Wire();
            AuthFlow.Dismiss();

            var game = FindFirstObjectByType<GameManager>();
            if (game != null)
                game.StartGame();

            var hud = FindFirstObjectByType<SubmarineHUD>();
            if (hud != null)
            {
                var pilot = AuthSession.Player;
                var hello = pilot == null ? "" : "Pilote " + pilot.DisplayName + " — ";
                hud.Announce(hello + "FOSSE DES OMBRES : boîte noire à 390 m. Le sonar actif vous trahit.", Mint, 9f);
                StartCoroutine(Hints(hud));
            }
        }

        IEnumerator Hints(SubmarineHUD hud)
        {
            yield return new WaitForSeconds(10f);
            hud.Announce("Le sonar passif écoute sans bruit. Le sonar actif révèle... et attire.", Cyan, 7f);
            yield return new WaitForSeconds(12f);
            hud.Announce("Objectif : descendre à 390 m dans la fosse, puis extraire la boîte noire.", Mint, 7f);
        }

        // ------------------------------------------------------------------
        // Câblage
        // ------------------------------------------------------------------

        void Wire()
        {
            var sub = FindFirstObjectByType<SubmarineController>();
            var cam = Camera.main;
            var game = FindFirstObjectByType<GameManager>();
            var box = FindFirstObjectByType<BlackBoxObjective>();
            var depth = FindFirstObjectByType<DepthZoneManager>();
            var input = FindFirstObjectByType<SubmarineInput>();

            if (cam != null && sub != null && cam.GetComponent<SubmarineCamera>() == null)
            {
                var follow = cam.gameObject.AddComponent<SubmarineCamera>();
                follow.target = sub.transform;
                follow.offset = new Vector3(0f, 4.5f, -13f);
            }

            if (depth != null)
            {
                if (depth.mainLight == null) depth.mainLight = FindDirectional();
                if (depth.mainCamera == null) depth.mainCamera = cam;
            }

            if (game != null && box != null)
            {
                if (game.blackBoxLocation == null)
                    game.blackBoxLocation = box.transform;
                game.OnBlackBoxCollected += box.OnExtracted;
            }

            if (input != null)
                input.enableTouchControls = false;

            if (FindFirstObjectByType<SubmarineHUD>() == null)
                gameObject.AddComponent<SubmarineHUD>();

            if (FindFirstObjectByType<SubmarineInputUI>() == null)
                gameObject.AddComponent<SubmarineInputUI>();
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;
            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        static Light FindDirectional()
        {
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional)
                    return light;
            return null;
        }

        // ------------------------------------------------------------------
        // Construction du monde
        // ------------------------------------------------------------------

        void ClearPlaceholders()
        {
            string[] names =
            {
                "Submarine", "SubmarineBody", "SubmarineLight", "Leviathan", "BlackBox",
                "SonarTarget_Terrain", "SonarTarget_Objective", "WaterSurface", "Environment",
                "GameManagers", "GameManager", "MarineLife", "CameraRig", "VolumetricBeam", "BackscatterParticles", "AmbientGlow"
            };

            foreach (var name in names)
            {
                var found = GameObject.Find(name);
                if (found == null || found == gameObject) continue;
                if (found.GetComponent<SubmarineController>() != null) continue;
                if (found.GetComponent<GameManager>() != null) continue;
                found.SetActive(false);
                Destroy(found);
            }
        }

        void BuildWorld()
        {
            ClearPlaceholders();

            // ── Matériaux PBR & Émissifs ─────────────────────────────
            var hullMain     = PBR(HullMid,    0.68f, 0.45f);
            var hullDark     = PBR(HullDark,   0.58f, 0.50f);
            var hullPlate    = PBR(HullLight,  0.62f, 0.35f);
            var hullBronze   = PBR(HullBronze, 0.75f, 0.85f);
            var accentCyan   = Emissive(Cyan, 3.0f);
            var accentDim    = Emissive(CyanDim, 1.4f);
            var rockDark     = PBR(RockDark, 0.15f, 0.08f);
            var rockMid      = PBR(RockMid,  0.22f, 0.06f);
            var sandMat      = PBR(SandTint, 0.10f, 0.04f);
            var creatureMat  = PBR(CreatureCol, 0.42f, 0.28f);
            var creatureVein = Emissive(CreatureVein, 3.8f);
            var boxMat       = Emissive(Amber, 4.5f);
            var glassMat     = Glass(PortGlass, 0.94f);
            var coralPink    = Emissive(CoralPink, 1.4f);
            var coralPurple  = Emissive(CoralPurple, 1.0f);
            var kelpMat      = PBR(KelpGreen, 0.25f, 0.12f);
            var ventMat      = Emissive(VentOrange, 2.8f);

            var submarine = BuildSubmarine(hullMain, hullDark, hullPlate, hullBronze, accentCyan, accentDim, glassMat);
            var blackBox = BuildBlackBox(boxMat);
            var env = BuildEnvironment(rockDark, rockMid, sandMat, coralPink, coralPurple, kelpMat, ventMat);
            BuildWaterSurface(env);
            BuildLeviathan(creatureMat, creatureVein, new Vector3(38f, -235f, 270f), 1f);
            BuildLeviathan(creatureMat, creatureVein, new Vector3(-95f, -360f, 430f), 0.72f);
            BuildCamera(submarine.transform);
            BuildPostFx();
            BuildManagers(blackBox);
            BuildLife();
            BuildAtmosphere(submarine.transform);
            StartCoroutine(DressParticles());
        }

        // ==================================================================
        //  SUBMARINE — HAUTE FIDÉLITÉ
        // ==================================================================

        GameObject BuildSubmarine(Material hullMain, Material hullDark, Material hullPlate, Material hullBronze,
                                   Material accentCyan, Material accentDim, Material glassMat)
        {
            var root = new GameObject("Submarine");
            root.transform.position = new Vector3(0f, -40f, 0f);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 12000f;
            rb.useGravity = false;
            rb.linearDamping = 0.4f;
            rb.angularDamping = 2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 2;
            capsule.radius = 1.15f;
            capsule.height = 6.4f;

            var controller = root.AddComponent<SubmarineController>();
            root.AddComponent<HullSystem>();
            root.AddComponent<OxygenSystem>();
            root.AddComponent<BatterySystem>();
            root.AddComponent<SonarSystem>();
            root.AddComponent<DepthZoneManager>();
            var audio = root.AddComponent<SubmarineAudio>();
            var input = root.AddComponent<SubmarineInput>();
            input.enableTouchControls = false;

            // ── Audio clips ──
            audio.engineLoop = Loop("EngineLoop", 2f, (u, i) =>
                Mathf.Sin(u * Mathf.PI * 2f * 42f) * 0.35f +
                Mathf.Sin(u * Mathf.PI * 2f * 84f) * 0.16f +
                (Random.value - 0.5f) * 0.08f);
            audio.oceanAmbient = Loop("OceanLoop", 4f, (u, i) =>
                Mathf.Sin(u * Mathf.PI * 2f * 18f) * 0.2f +
                Mathf.Sin(u * Mathf.PI * 2f * 27f) * 0.1f +
                (Random.value - 0.5f) * 0.12f);
            audio.sonarPing = Loop("SonarPing", 0.9f, (u, i) =>
                Mathf.Sin(u * Mathf.PI * 2f * (720f + 520f * u)) * Mathf.Exp(-u * 6f) * 0.55f);
            audio.hullCreak = Loop("HullCreak", 1.4f, (u, i) =>
                Mathf.Sin(u * Mathf.PI * 2f * (68f + 16f * Mathf.Sin(u * 9f))) *
                Mathf.Exp(-u * 1.8f) * 0.45f);

            // ── Coque de pression double ──
            var body = Prim(PrimitiveType.Capsule, root.transform, "Hull", Vector3.zero,
                new Vector3(2.3f, 3.3f, 2.3f), hullMain, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var outerCasing = Prim(PrimitiveType.Capsule, root.transform, "OuterCasing", new Vector3(0f, 0f, 0.05f),
                new Vector3(2.42f, 3.0f, 2.42f), hullDark, false);
            outerCasing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // ── Dôme sonar avant (bow) ──
            Prim(PrimitiveType.Sphere, root.transform, "SonarDome", new Vector3(0f, -0.3f, 3.5f),
                new Vector3(1.6f, 1.3f, 1.0f), hullDark, false);

            // Sonar array transducer grill
            for (int g = 0; g < 4; g++)
            {
                Prim(PrimitiveType.Cube, root.transform, "SonarGrill" + g, new Vector3(0f, -0.45f + g * 0.12f, 3.9f),
                    new Vector3(1.1f, 0.03f, 0.1f), accentDim, false);
            }

            // ── Kiosque de commandement (Sail) ──
            Prim(PrimitiveType.Cube, root.transform, "Sail", new Vector3(0f, 1.35f, 0.35f),
                new Vector3(1.2f, 1.5f, 2.3f), hullMain, false);
            Prim(PrimitiveType.Cube, root.transform, "SailCap", new Vector3(0f, 2.1f, 0.35f),
                new Vector3(1.3f, 0.12f, 2.4f), hullPlate, false);

            // Mâts, périscopes, antennes
            Prim(PrimitiveType.Cylinder, root.transform, "Periscope1", new Vector3(0.15f, 2.6f, 0.1f),
                new Vector3(0.08f, 0.5f, 0.08f), hullDark, false);
            Prim(PrimitiveType.Cylinder, root.transform, "Periscope2", new Vector3(-0.15f, 2.45f, 0.5f),
                new Vector3(0.06f, 0.4f, 0.06f), hullDark, false);
            Prim(PrimitiveType.Cylinder, root.transform, "Antenna1", new Vector3(0.3f, 2.8f, -0.2f),
                new Vector3(0.04f, 0.6f, 0.04f), hullDark, false);
            Prim(PrimitiveType.Cylinder, root.transform, "Antenna2", new Vector3(-0.25f, 2.7f, -0.4f),
                new Vector3(0.03f, 0.5f, 0.03f), hullDark, false);

            // Hublots de passerelle
            Prim(PrimitiveType.Sphere, root.transform, "SailWindow1", new Vector3(0f, 1.8f, 1.45f),
                new Vector3(0.24f, 0.18f, 0.08f), Emissive(WarmWhite, 1.5f), false);
            Prim(PrimitiveType.Sphere, root.transform, "SailWindow2", new Vector3(0.25f, 1.8f, 1.3f),
                new Vector3(0.18f, 0.14f, 0.06f), Emissive(WarmWhite, 1.0f), false);

            // ── Sas de secours ──
            Prim(PrimitiveType.Cylinder, root.transform, "EscapeHatch", new Vector3(0f, 2.12f, -0.8f),
                new Vector3(0.5f, 0.04f, 0.5f), hullPlate, false);
            Prim(PrimitiveType.Cylinder, root.transform, "HatchRing", new Vector3(0f, 2.14f, -0.8f),
                new Vector3(0.55f, 0.02f, 0.55f), accentDim, false);

            // ── Gouvernes & plans de plongée arrière en X ──
            Prim(PrimitiveType.Cube, root.transform, "Rudder", new Vector3(0f, 0f, -3.3f),
                new Vector3(2.1f, 1.5f, 0.28f), hullMain, false);
            Prim(PrimitiveType.Cube, root.transform, "SternPlaneT", new Vector3(0f, 0.9f, -3.2f),
                new Vector3(0.15f, 0.9f, 0.6f), hullDark, false);
            Prim(PrimitiveType.Cube, root.transform, "SternPlaneB", new Vector3(0f, -0.9f, -3.2f),
                new Vector3(0.15f, 0.9f, 0.6f), hullDark, false);

            // ── Barres de plongée avant (Dive planes) ──
            Prim(PrimitiveType.Cube, root.transform, "DivePlaneL", new Vector3(-1.55f, 0f, 0.8f),
                new Vector3(1.3f, 0.16f, 0.9f), hullMain, false);
            Prim(PrimitiveType.Cube, root.transform, "DivePlaneR", new Vector3(1.55f, 0f, 0.8f),
                new Vector3(1.3f, 0.16f, 0.9f), hullMain, false);
            Prim(PrimitiveType.Sphere, root.transform, "DiveTipL", new Vector3(-2.2f, 0f, 0.8f),
                new Vector3(0.14f, 0.14f, 0.14f), accentCyan, false);
            Prim(PrimitiveType.Sphere, root.transform, "DiveTipR", new Vector3(2.2f, 0f, 0.8f),
                new Vector3(0.14f, 0.14f, 0.14f), accentCyan, false);

            // ── Ballasts latéraux ──
            var blL = Prim(PrimitiveType.Capsule, root.transform, "BallastL", new Vector3(-1.15f, -0.4f, 0f),
                new Vector3(0.7f, 1.8f, 0.7f), hullDark, false);
            blL.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var blR = Prim(PrimitiveType.Capsule, root.transform, "BallastR", new Vector3(1.15f, -0.4f, 0f),
                new Vector3(0.7f, 1.8f, 0.7f), hullDark, false);
            blR.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // ── Bras robotique de prélèvement (replié sous la proue) ──
            var armBase = Prim(PrimitiveType.Cylinder, root.transform, "ManipArmBase", new Vector3(-0.4f, -0.9f, 2.6f),
                new Vector3(0.25f, 0.08f, 0.25f), hullBronze, false);
            Prim(PrimitiveType.Capsule, armBase.transform, "ManipUpperArm", new Vector3(0f, 0.35f, 0.3f),
                new Vector3(0.12f, 0.45f, 0.12f), hullDark, false)
                .transform.localRotation = Quaternion.Euler(55f, 15f, 0f);
            Prim(PrimitiveType.Capsule, armBase.transform, "ManipClaw", new Vector3(0.1f, 0.2f, 0.8f),
                new Vector3(0.08f, 0.25f, 0.08f), hullBronze, false)
                .transform.localRotation = Quaternion.Euler(-30f, 20f, 0f);

            // ── Hélice de propulsion animée ──
            var propRoot = new GameObject("PropellerAssembly");
            propRoot.transform.SetParent(root.transform, false);
            propRoot.transform.localPosition = new Vector3(0f, 0f, -3.55f);

            var propHub = Prim(PrimitiveType.Cylinder, propRoot.transform, "PropellerHub",
                Vector3.zero, new Vector3(0.38f, 0.14f, 0.38f), hullBronze, false);
            propHub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            for (int b = 0; b < 5; b++)
            {
                float angle = b * 72f;
                var blade = Prim(PrimitiveType.Cube, propRoot.transform, "Blade" + b,
                    Vector3.zero, new Vector3(0.07f, 0.58f, 0.20f), hullBronze, false);
                blade.transform.localRotation = Quaternion.Euler(0f, 0f, angle) * Quaternion.Euler(18f, 0f, 0f);
            }

            var spinner = propRoot.AddComponent<PropellerSpinner>();
            spinner.controller = controller;

            // Traînées de cavitation de l'hélice
            var cavObj = new GameObject("CavitationTrails");
            cavObj.transform.SetParent(propRoot.transform, false);
            cavObj.transform.localPosition = new Vector3(0f, 0f, -0.4f);
            var cavPs = cavObj.AddComponent<ParticleSystem>();
            ConfigureCavitationParticles(cavPs);
            spinner.cavitationParticles = cavPs;

            // ── Hublots latéraux avec bagues métalliques et éclairage intérieur ──
            for (int i = 0; i < 4; i++)
            {
                float z = -1.5f + i * 1.0f;
                // Bâbord (gauche)
                Prim(PrimitiveType.Cylinder, root.transform, "PortRingL" + i, new Vector3(-1.14f, 0.05f, z),
                    new Vector3(0.30f, 0.02f, 0.30f), hullPlate, false)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                Prim(PrimitiveType.Sphere, root.transform, "PortGlassL" + i, new Vector3(-1.13f, 0.05f, z),
                    new Vector3(0.22f, 0.22f, 0.10f), glassMat, false);
                Prim(PrimitiveType.Sphere, root.transform, "PortGlowL" + i, new Vector3(-1.12f, 0.05f, z),
                    new Vector3(0.16f, 0.16f, 0.06f), Emissive(WarmWhite, 2.2f), false);
                // Tribord (droite)
                Prim(PrimitiveType.Cylinder, root.transform, "PortRingR" + i, new Vector3(1.14f, 0.05f, z),
                    new Vector3(0.30f, 0.02f, 0.30f), hullPlate, false)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                Prim(PrimitiveType.Sphere, root.transform, "PortGlassR" + i, new Vector3(1.13f, 0.05f, z),
                    new Vector3(0.22f, 0.22f, 0.10f), glassMat, false);
                Prim(PrimitiveType.Sphere, root.transform, "PortGlowR" + i, new Vector3(1.12f, 0.05f, z),
                    new Vector3(0.16f, 0.16f, 0.06f), Emissive(WarmWhite, 2.2f), false);
            }

            // ── Rainures et rivets de coque ──
            for (int s = 0; s < 6; s++)
            {
                float z = -2.5f + s * 1.0f;
                Prim(PrimitiveType.Cube, root.transform, "SeamH" + s, new Vector3(0f, -0.95f, z),
                    new Vector3(2.35f, 0.03f, 0.03f), hullDark, false);
            }

            // ── Bandes d'accent luminescent cyan ──
            Prim(PrimitiveType.Cube, root.transform, "StripeL", new Vector3(-1.1f, 0.35f, 0.2f),
                new Vector3(0.06f, 0.16f, 4.6f), accentCyan, false);
            Prim(PrimitiveType.Cube, root.transform, "StripeR", new Vector3(1.1f, 0.35f, 0.2f),
                new Vector3(0.06f, 0.16f, 4.6f), accentCyan, false);
            Prim(PrimitiveType.Cube, root.transform, "StripeKeel", new Vector3(0f, -1.1f, 0.2f),
                new Vector3(0.08f, 0.04f, 5.2f), accentDim, false);

            // ── Feux de navigation ──
            CreatePointLight(root.transform, "NavLightPort", new Vector3(-1.2f, 0f, 2.0f),
                new Color(1f, 0.15f, 0.1f), 8f, 1.8f);
            Prim(PrimitiveType.Sphere, root.transform, "NavDotPort", new Vector3(-1.2f, 0f, 2.0f),
                Vector3.one * 0.08f, Emissive(new Color(1f, 0.15f, 0.1f), 4f), false);

            CreatePointLight(root.transform, "NavLightStbd", new Vector3(1.2f, 0f, 2.0f),
                new Color(0.1f, 1f, 0.3f), 8f, 1.8f);
            Prim(PrimitiveType.Sphere, root.transform, "NavDotStbd", new Vector3(1.2f, 0f, 2.0f),
                Vector3.one * 0.08f, Emissive(new Color(0.1f, 1f, 0.3f), 4f), false);

            CreatePointLight(root.transform, "NavLightStern", new Vector3(0f, 0.5f, -3.4f),
                WarmWhite, 6f, 1.2f);
            Prim(PrimitiveType.Sphere, root.transform, "NavDotStern", new Vector3(0f, 0.5f, -3.4f),
                Vector3.one * 0.06f, Emissive(WarmWhite, 3f), false);

            // ── Projecteur principal (Headlight) — puissant pour percer les abysses ──
            var lightObj = new GameObject("Headlight");
            lightObj.transform.SetParent(root.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, -0.1f, 3.1f);
            var spot = lightObj.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 400f;
            spot.spotAngle = 75f;
            spot.innerSpotAngle = 42f;
            spot.intensity = 55f;
            spot.color = new Color(0.72f, 0.9f, 1f);
            spot.shadows = LightShadows.Soft;
            spot.shadowStrength = 0.7f;
            spot.shadowBias = 0.02f;

            // ── Projecteur inférieur (Down-light) pour éclairer le sol ──
            var downLightObj = new GameObject("DownLight");
            downLightObj.transform.SetParent(root.transform, false);
            downLightObj.transform.localPosition = new Vector3(0f, -0.8f, 1.5f);
            downLightObj.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            var downSpot = downLightObj.AddComponent<Light>();
            downSpot.type = LightType.Spot;
            downSpot.range = 180f;
            downSpot.spotAngle = 80f;
            downSpot.innerSpotAngle = 50f;
            downSpot.intensity = 18f;
            downSpot.color = new Color(0.6f, 0.85f, 1f);
            downSpot.shadows = LightShadows.Soft;

            // ── Projecteurs secondaires (Floods latéraux) ──
            var floodL = CreateSpotLight(root.transform, "FloodL", new Vector3(-0.8f, -0.5f, 2.8f),
                Quaternion.Euler(10f, -20f, 0f), new Color(0.6f, 0.85f, 1f), 120f, 55f, 8f);

            var floodR = CreateSpotLight(root.transform, "FloodR", new Vector3(0.8f, -0.5f, 2.8f),
                Quaternion.Euler(10f, 20f, 0f), new Color(0.6f, 0.85f, 1f), 120f, 55f, 8f);

            // ── Halo d'ambiance de coque ──
            CreatePointLight(root.transform, "HullGlow", new Vector3(0f, 0f, 0f), Cyan, 18f, 1.2f);

            var rig = root.AddComponent<SubmarineLightRig>();
            rig.spot = spot;
            rig.downLight = downSpot;
            rig.floodL = floodL;
            rig.floodR = floodR;
            rig.battery = root.GetComponent<BatterySystem>();
            rig.baseIntensity = 55f;
            rig.downBaseIntensity = 18f;
            rig.floodBaseIntensity = 12f;

            // ── Ambient glow light — illuminates the sub's immediate surroundings when lights are on ──
            var ambGlowObj = new GameObject("AmbientGlow");
            ambGlowObj.transform.SetParent(root.transform, false);
            ambGlowObj.transform.localPosition = new Vector3(0f, 0f, 1f);
            var ambGlow = ambGlowObj.AddComponent<Light>();
            ambGlow.type = LightType.Point;
            ambGlow.color = new Color(0.6f, 0.85f, 1f);
            ambGlow.range = 60f;
            ambGlow.intensity = 12f;
            rig.ambientGlow = ambGlow;
            rig.ambientGlowBaseIntensity = 12f;

            // ── Backscatter particles — floating debris that catches the headlight beam ──
            var scatterObj = new GameObject("BackscatterParticles");
            scatterObj.transform.SetParent(root.transform, false);
            scatterObj.transform.localPosition = new Vector3(0f, 0f, 8f);
            var scatterPs = scatterObj.AddComponent<ParticleSystem>();
            var scMain = scatterPs.main;
            scMain.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
            scMain.startSpeed = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            scMain.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
            scMain.startColor = new Color(0.7f, 0.85f, 1f, 0.6f);
            scMain.maxParticles = 300;
            scMain.simulationSpace = ParticleSystemSimulationSpace.World;
            scMain.gravityModifier = -0.02f;
            var scEmission = scatterPs.emission;
            scEmission.rateOverTime = 60f;
            var scShape = scatterPs.shape;
            scShape.shapeType = ParticleSystemShapeType.Box;
            scShape.scale = new Vector3(20f, 12f, 40f);
            var scCol = scatterPs.colorOverLifetime;
            scCol.enabled = true;
            var scGrad = new Gradient();
            scGrad.SetKeys(
                new[] { new GradientColorKey(new Color(0.8f, 0.9f, 1f), 0f), new GradientColorKey(new Color(0.6f, 0.8f, 1f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.2f), new GradientAlphaKey(0.5f, 0.8f), new GradientAlphaKey(0f, 1f) }
            );
            scCol.color = scGrad;

            // ── Traînée de bulles classiques ──
            BuildBubbleTrail(root.transform);

            // ── Sillage de sédiments au sol (Seabed wake) ──
            var wakeObj = new GameObject("SeabedWake");
            wakeObj.transform.SetParent(root.transform, false);
            var wakePs = wakeObj.AddComponent<ParticleSystem>();
            ConfigureSiltParticles(wakePs);
            var seabedWake = root.AddComponent<SeabedWake>();
            seabedWake.controller = controller;
            seabedWake.siltParticles = wakePs;

            return root;
        }

        static void ConfigureCavitationParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
            main.startColor = new Color(0.8f, 0.95f, 1f, 0.5f);
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.55f;
        }

        static void ConfigureSiltParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2.5f);
            main.startColor = SiltCol;
            main.maxParticles = 90;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.02f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.2f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(0.18f, 0.22f, 0.18f), 0f), new GradientColorKey(new Color(0.12f, 0.15f, 0.12f), 1f) },
                new[] { new GradientAlphaKey(0.35f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = grad;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(1f, 3.0f)));
        }

        void BuildBubbleTrail(Transform parent)
        {
            var obj = new GameObject("BubbleTrail");
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = new Vector3(0f, 0.3f, -3.7f);

            var ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.20f);
            main.startColor = new Color(0.6f, 0.85f, 1f, 0.45f);
            main.maxParticles = 220;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.35f;

            var emission = ps.emission;
            emission.rateOverTime = 32f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.35f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(0.7f, 0.9f, 1f), 0f), new GradientColorKey(new Color(0.5f, 0.8f, 1f), 1f) },
                new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = gradient;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(0.5f, 1f), new Keyframe(1f, 1.6f)));
        }

        // ==================================================================
        //  WATER SURFACE — SURFACE DE L'OCÉAN ONDULANTE
        // ==================================================================

        void BuildWaterSurface(Transform parent)
        {
            var surface = new GameObject("WaterSurface");
            surface.transform.position = new Vector3(0f, 0f, 300f);
            surface.transform.SetParent(parent, false);

            var mf = surface.AddComponent<MeshFilter>();
            var mr = surface.AddComponent<MeshRenderer>();

            mf.sharedMesh = CreateWaterGridMesh(1400f, 1400f, 32, 32);

            var mat = Glass(new Color(0.12f, 0.45f, 0.62f, 0.70f), 0.95f);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.06f, 0.30f, 0.45f) * 1.5f);
            }
            mr.sharedMaterial = mat;

            surface.AddComponent<WaterSurfaceAnimator>();
        }

        static Mesh CreateWaterGridMesh(float width, float length, int resX, int resZ)
        {
            var mesh = new Mesh { name = "WaterSurfaceMesh" };
            int vertCount = (resX + 1) * (resZ + 1);
            var vertices = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var triangles = new int[resX * resZ * 6];

            float dx = width / resX;
            float dz = length / resZ;
            float halfW = width * 0.5f;
            float halfL = length * 0.5f;

            for (int z = 0, i = 0; z <= resZ; z++)
            {
                for (int x = 0; x <= resX; x++, i++)
                {
                    vertices[i] = new Vector3(x * dx - halfW, 0f, z * dz - halfL);
                    uvs[i] = new Vector2((float)x / resX, (float)z / resZ);
                }
            }

            int t = 0;
            for (int z = 0, v = 0; z < resZ; z++, v++)
            {
                for (int x = 0; x < resX; x++, v++)
                {
                    triangles[t++] = v;
                    triangles[t++] = v + resX + 1;
                    triangles[t++] = v + 1;

                    triangles[t++] = v + 1;
                    triangles[t++] = v + resX + 1;
                    triangles[t++] = v + resX + 2;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ==================================================================
        //  BLACK BOX — BALISE HAUTE-VISIBILITÉ ABYSSALE
        // ==================================================================

        GameObject BuildBlackBox(Material mat)
        {
            var root = new GameObject("BlackBox");
            root.transform.position = new Vector3(0f, -388f, 330f);

            // ── Boîtier principal plus grand ──
            var visual = Prim(PrimitiveType.Cube, root.transform, "Casing", Vector3.zero,
                new Vector3(2.0f, 2.0f, 2.0f), mat, false);
            visual.transform.localRotation = Quaternion.Euler(12f, 24f, 6f);

            // ── Armature de protection renforcée ──
            Prim(PrimitiveType.Cube, root.transform, "Frame1", new Vector3(0f, 1.1f, 0f),
                new Vector3(2.4f, 0.08f, 2.4f), Emissive(Amber, 4.0f), false);
            Prim(PrimitiveType.Cube, root.transform, "Frame2", new Vector3(0f, -1.1f, 0f),
                new Vector3(2.4f, 0.08f, 2.4f), Emissive(Amber, 4.0f), false);
            Prim(PrimitiveType.Cylinder, root.transform, "FramePost1", new Vector3(1.0f, 0f, 1.0f),
                new Vector3(0.08f, 1.1f, 0.08f), Emissive(Amber, 3.0f), false);
            Prim(PrimitiveType.Cylinder, root.transform, "FramePost2", new Vector3(-1.0f, 0f, 1.0f),
                new Vector3(0.08f, 1.1f, 0.08f), Emissive(Amber, 3.0f), false);
            Prim(PrimitiveType.Cylinder, root.transform, "FramePost3", new Vector3(1.0f, 0f, -1.0f),
                new Vector3(0.08f, 1.1f, 0.08f), Emissive(Amber, 3.0f), false);
            Prim(PrimitiveType.Cylinder, root.transform, "FramePost4", new Vector3(-1.0f, 0f, -1.0f),
                new Vector3(0.08f, 1.1f, 0.08f), Emissive(Amber, 3.0f), false);

            // ── Antenne de balise verticale avec lumière pulsante ──
            Prim(PrimitiveType.Cylinder, root.transform, "BeaconAntenna", new Vector3(0f, 3.5f, 0f),
                new Vector3(0.06f, 2.5f, 0.06f), Emissive(Amber, 2.0f), false);
            Prim(PrimitiveType.Sphere, root.transform, "BeaconTip", new Vector3(0f, 6.0f, 0f),
                new Vector3(0.4f, 0.4f, 0.4f), Emissive(AmberHot, 8.0f), false);

            root.AddComponent<BoxCollider>().size = Vector3.one * 3.0f;

            var objective = root.AddComponent<BlackBoxObjective>();
            objective.glowColor = Amber;
            objective.glowIntensity = 6.0f;
            objective.pulseSpeed = 1.5f;
            objective.pulseAmount = 0.6f;
            objective.pulseInterval = 2.0f;
            objective.beaconPulse = Loop("Beacon", 1.8f, (u, i) =>
                Mathf.Sin(u * Mathf.PI * 2f * (640f - 320f * u)) * Mathf.Exp(-u * 3.4f) * 0.45f);

            // ── Lumière principale de balise — TRÈS PUISSANTE ──
            var boxLight = new GameObject("BeaconLight");
            boxLight.transform.SetParent(root.transform, false);
            boxLight.transform.localPosition = new Vector3(0f, 1f, 0f);
            var light = boxLight.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Amber;
            light.range = 250f;
            light.intensity = 45f;

            // ── Spot vertical vers le haut — colonne de lumière ──
            var beaconSpotObj = new GameObject("BeaconSpotUp");
            beaconSpotObj.transform.SetParent(root.transform, false);
            beaconSpotObj.transform.localPosition = new Vector3(0f, 2f, 0f);
            beaconSpotObj.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var beaconSpot = beaconSpotObj.AddComponent<Light>();
            beaconSpot.type = LightType.Spot;
            beaconSpot.color = AmberHot;
            beaconSpot.range = 350f;
            beaconSpot.spotAngle = 30f;
            beaconSpot.innerSpotAngle = 15f;
            beaconSpot.intensity = 50f;

            // ── Halo au sol ──
            var haloObj = new GameObject("HaloGround");
            haloObj.transform.SetParent(root.transform, false);
            haloObj.transform.localPosition = new Vector3(0f, -1.5f, 0f);
            var haloLight = haloObj.AddComponent<Light>();
            haloLight.type = LightType.Point;
            haloLight.color = AmberHot;
            haloLight.range = 80f;
            haloLight.intensity = 16f;

            // ── 4 lumières orbitales pulsantes ──
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                float rad = angle * Mathf.Deg2Rad;
                var orbLight = new GameObject("OrbitalLight" + i);
                orbLight.transform.SetParent(root.transform, false);
                orbLight.transform.localPosition = new Vector3(Mathf.Cos(rad) * 5f, 2f, Mathf.Sin(rad) * 5f);
                var oLight = orbLight.AddComponent<Light>();
                oLight.type = LightType.Point;
                oLight.color = Amber;
                oLight.range = 30f;
                oLight.intensity = 5f;
            }

            // ── Particules de balise montantes (colonnes de bulles dorées) ──
            var beaconParticles = new GameObject("BeaconParticles");
            beaconParticles.transform.SetParent(root.transform, false);
            beaconParticles.transform.localPosition = Vector3.zero;
            var bps = beaconParticles.AddComponent<ParticleSystem>();
            var bMain = bps.main;
            bMain.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
            bMain.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            bMain.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
            bMain.startColor = new Color(1f, 0.85f, 0.3f, 0.7f);
            bMain.maxParticles = 120;
            bMain.simulationSpace = ParticleSystemSimulationSpace.World;
            bMain.gravityModifier = -0.15f;
            var bEmission = bps.emission;
            bEmission.rateOverTime = 18f;
            var bShape = bps.shape;
            bShape.shapeType = ParticleSystemShapeType.Circle;
            bShape.radius = 2.5f;
            var bCol = bps.colorOverLifetime;
            bCol.enabled = true;
            var bGrad = new Gradient();
            bGrad.SetKeys(
                new[] { new GradientColorKey(AmberHot, 0f), new GradientColorKey(Amber, 0.5f), new GradientColorKey(new Color(1f, 0.9f, 0.5f), 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1f) }
            );
            bCol.color = bGrad;
            var bSol = bps.sizeOverLifetime;
            bSol.enabled = true;
            bSol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.5f, 1.5f), new Keyframe(1f, 0.2f)));

            // ── Anneau de particules orbitales (ring effect) ──
            var ringParticles = new GameObject("BeaconRing");
            ringParticles.transform.SetParent(root.transform, false);
            ringParticles.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            var rps = ringParticles.AddComponent<ParticleSystem>();
            var rMain = rps.main;
            rMain.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
            rMain.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            rMain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
            rMain.startColor = AmberHot;
            rMain.maxParticles = 60;
            rMain.simulationSpace = ParticleSystemSimulationSpace.World;
            var rEmission = rps.emission;
            rEmission.rateOverTime = 12f;
            var rShape = rps.shape;
            rShape.shapeType = ParticleSystemShapeType.Circle;
            rShape.radius = 4.5f;
            rShape.radiusThickness = 0.1f;

            // ── Beacon pulsator component ──
            var pulsator = root.AddComponent<BlackBoxBeacon>();
            pulsator.mainBeaconLight = light;
            pulsator.spotUp = beaconSpot;
            pulsator.haloLight = haloLight;

            return root;
        }

        // ==================================================================
        //  ENVIRONMENT — RICHE ET DÉTAILLÉ
        // ==================================================================

        Transform BuildEnvironment(Material rockDark, Material rockMid, Material sandMat,
                                   Material coralPink, Material coralPurple, Material kelpMat, Material ventMat)
        {
            var env = new GameObject("Environment").transform;

            // ── Fond marin ──
            var seabed = Prim(PrimitiveType.Cube, env, "Seabed", new Vector3(0f, -472f, 220f),
                new Vector3(1000f, 40f, 1500f), sandMat, true);
            AddSonarTarget(seabed, SonarTargetType.Terrain);

            // ── Parois de la fosse abyssale ──
            Prim(PrimitiveType.Cube, env, "TrenchWallL", new Vector3(-155f, -300f, 220f),
                new Vector3(90f, 380f, 1020f), rockDark, true);
            Prim(PrimitiveType.Cube, env, "TrenchWallR", new Vector3(155f, -300f, 220f),
                new Vector3(90f, 380f, 1020f), rockDark, true);

            // ── Corniches et surplombs ──
            Prim(PrimitiveType.Cube, env, "LedgeL", new Vector3(-108f, -300f, 300f),
                new Vector3(22f, 8f, 120f), rockMid, true);
            Prim(PrimitiveType.Cube, env, "LedgeR", new Vector3(104f, -352f, 250f),
                new Vector3(26f, 10f, 90f), rockMid, true);
            Prim(PrimitiveType.Cube, env, "LedgeDeep", new Vector3(-80f, -420f, 350f),
                new Vector3(18f, 6f, 60f), rockMid, true);

            // ── Arche rocheuse naturelle ──
            Prim(PrimitiveType.Cube, env, "ArchPillarL", new Vector3(-40f, -180f, 120f),
                new Vector3(12f, 50f, 12f), rockDark, true);
            Prim(PrimitiveType.Cube, env, "ArchPillarR", new Vector3(40f, -180f, 120f),
                new Vector3(12f, 50f, 12f), rockDark, true);
            var archSpan = Prim(PrimitiveType.Cube, env, "ArchSpan", new Vector3(0f, -158f, 120f),
                new Vector3(92f, 8f, 14f), rockMid, true);
            archSpan.transform.localRotation = Quaternion.Euler(0f, 0f, 3f);

            // ── Formations rocheuses dispersées ──
            var rng = new System.Random(7);
            for (int i = 0; i < 70; i++)
            {
                float x = (float)(rng.NextDouble() * 2.0 - 1.0) * 260f;
                float z = (float)(rng.NextDouble() * 1200.0 - 180.0);
                float y = -450f + (float)rng.NextDouble() * 280f;
                if (Mathf.Abs(x) < 26f && y > -400f) x += x < 0f ? -40f : 40f;

                float s = 3f + (float)rng.NextDouble() * 18f;
                var mat = rng.NextDouble() > 0.5 ? rockDark : rockMid;
                var rock = Prim(PrimitiveType.Cube, env, "Rock" + i, new Vector3(x, y, z),
                    new Vector3(s, s * (0.4f + (float)rng.NextDouble() * 0.8f), s * (0.5f + (float)rng.NextDouble() * 0.7f)),
                    mat, true);
                rock.transform.localRotation = Quaternion.Euler(
                    (float)rng.NextDouble() * 45f,
                    (float)rng.NextDouble() * 360f,
                    (float)rng.NextDouble() * 45f);

                if (i % 4 == 0) AddSonarTarget(rock, SonarTargetType.Terrain);
            }

            // ── Récifs de coraux abyssaux ──
            BuildCoralCluster(env, new Vector3(-85f, -280f, 280f), coralPink, coralPurple, rng);
            BuildCoralCluster(env, new Vector3(72f, -310f, 340f), coralPink, coralPurple, rng);
            BuildCoralCluster(env, new Vector3(-50f, -350f, 380f), coralPurple, coralPink, rng);
            BuildCoralCluster(env, new Vector3(35f, -260f, 200f), coralPink, coralPurple, rng);

            // ── Forêts de laminaires géantes (Kelp) ──
            BuildKelpForest(env, new Vector3(-70f, -240f, 180f), kelpMat, rng, 14);
            BuildKelpForest(env, new Vector3(60f, -280f, 260f), kelpMat, rng, 10);
            BuildKelpForest(env, new Vector3(-100f, -340f, 360f), kelpMat, rng, 8);

            // ── Cheminées hydrothermales (fumeurs noirs) avec étincelles ──
            BuildThermalVent(env, new Vector3(25f, -445f, 280f), ventMat, 1.0f);
            BuildThermalVent(env, new Vector3(-55f, -448f, 360f), ventMat, 0.75f);
            BuildThermalVent(env, new Vector3(80f, -440f, 400f), ventMat, 0.55f);

            // ── Épave brisée ──
            BuildWreck(env, rockMid, rockDark);

            // ── Piédestal d'autel pour la boîte noire ──
            Prim(PrimitiveType.Cube, env, "Pedestal", new Vector3(0f, -420f, 330f),
                new Vector3(26f, 62f, 26f), rockDark, true);
            Prim(PrimitiveType.Cube, env, "PedestalCap", new Vector3(0f, -390f, 330f),
                new Vector3(30f, 3f, 30f), rockMid, true);

            BuildLighting();
            return env;
        }

        void BuildCoralCluster(Transform parent, Vector3 center, Material mat1, Material mat2, System.Random rng)
        {
            for (int i = 0; i < 9; i++)
            {
                float x = center.x + (float)(rng.NextDouble() * 2 - 1) * 12f;
                float z = center.z + (float)(rng.NextDouble() * 2 - 1) * 12f;
                float y = center.y + (float)rng.NextDouble() * 4f;
                float h = 2f + (float)rng.NextDouble() * 6f;
                float r = 0.4f + (float)rng.NextDouble() * 1.2f;
                var mat = rng.NextDouble() > 0.5 ? mat1 : mat2;

                Prim(PrimitiveType.Capsule, parent, "Coral" + i + center.GetHashCode(),
                    new Vector3(x, y + h * 0.5f, z), new Vector3(r, h * 0.5f, r), mat, false);

                if (rng.NextDouble() > 0.4)
                {
                    var branch = Prim(PrimitiveType.Capsule, parent, "CoralBranch" + i + center.GetHashCode(),
                        new Vector3(x + r * 0.8f, y + h * 0.6f, z),
                        new Vector3(r * 0.4f, h * 0.3f, r * 0.4f), mat, false);
                    branch.transform.localRotation = Quaternion.Euler(0f, 0f, 25f + (float)rng.NextDouble() * 20f);
                }
            }
        }

        void BuildKelpForest(Transform parent, Vector3 center, Material mat, System.Random rng, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float x = center.x + (float)(rng.NextDouble() * 2 - 1) * 16f;
                float z = center.z + (float)(rng.NextDouble() * 2 - 1) * 16f;
                float h = 12f + (float)rng.NextDouble() * 28f;
                float w = 0.16f + (float)rng.NextDouble() * 0.3f;

                var stalk = Prim(PrimitiveType.Capsule, parent, "Kelp" + i + center.GetHashCode(),
                    new Vector3(x, center.y + h * 0.5f, z), new Vector3(w, h * 0.5f, w), mat, false);
                stalk.transform.localRotation = Quaternion.Euler(
                    (float)(rng.NextDouble() * 8 - 4), 0f, (float)(rng.NextDouble() * 12 - 6));

                for (int j = 0; j < 3; j++)
                {
                    float leafY = center.y + h * (0.3f + j * 0.25f);
                    Prim(PrimitiveType.Sphere, parent, "KelpLeaf" + i + "_" + j + center.GetHashCode(),
                        new Vector3(x + (float)(rng.NextDouble() - 0.5) * 0.8f, leafY,
                                    z + (float)(rng.NextDouble() - 0.5) * 0.8f),
                        new Vector3(w * 3f, w * 1.5f, w * 3f), mat, false);
                }
            }
        }

        void BuildThermalVent(Transform parent, Vector3 pos, Material ventMat, float scale)
        {
            // Cheminée minérale
            Prim(PrimitiveType.Cylinder, parent, "VentChimney" + pos.GetHashCode(),
                pos, new Vector3(1.6f * scale, 4.5f * scale, 1.6f * scale), ventMat, true);

            Prim(PrimitiveType.Cylinder, parent, "VentRing" + pos.GetHashCode(),
                pos + Vector3.up * 4.5f * scale, new Vector3(2.2f * scale, 0.35f * scale, 2.2f * scale), ventMat, true);

            // Lumière rougeoyante de la bouche volcanique
            CreatePointLight(parent, "VentGlow" + pos.GetHashCode(),
                pos + Vector3.up * 5.2f * scale, VentOrange, 28f * scale, 3.8f);

            // Panache de fluide hydrothermal (fumée noire/orange)
            var plumeObj = new GameObject("VentPlume" + pos.GetHashCode());
            plumeObj.transform.SetParent(parent, false);
            plumeObj.transform.position = pos + Vector3.up * 5.2f * scale;

            var ps = plumeObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 6.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f * scale, 3.2f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f * scale, 2.2f * scale);
            main.startColor = new Color(VentOrange.r, VentOrange.g, VentOrange.b, 0.28f);
            main.maxParticles = 140;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.18f;

            var emission = ps.emission;
            emission.rateOverTime = 18f * scale;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = 0.5f * scale;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(VentOrange, 0f), new GradientColorKey(new Color(0.25f, 0.10f, 0.04f), 1f) },
                new[] { new GradientAlphaKey(0.32f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = grad;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(1f, 3.2f)));

            // Étincelles minérales incandescentes (Embers)
            var emberObj = new GameObject("VentEmbers" + pos.GetHashCode());
            emberObj.transform.SetParent(parent, false);
            emberObj.transform.position = pos + Vector3.up * 5.2f * scale;

            var emberPs = emberObj.AddComponent<ParticleSystem>();
            var eMain = emberPs.main;
            eMain.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4.5f);
            eMain.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.5f);
            eMain.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            eMain.startColor = AmberHot;
            eMain.maxParticles = 40;
            eMain.simulationSpace = ParticleSystemSimulationSpace.World;
            eMain.gravityModifier = -0.08f;

            var eEmission = emberPs.emission;
            eEmission.rateOverTime = 6f * scale;

            // Vers tubicoles géants (Riftia) au pied de la cheminée
            BuildTubeWorms(parent, pos, scale);
        }

        void BuildTubeWorms(Transform parent, Vector3 basePos, float scale)
        {
            var stalkMat = PBR(new Color(0.88f, 0.88f, 0.82f), 0.35f, 0.05f);
            var plumeMat = Emissive(new Color(0.85f, 0.18f, 0.22f), 2.2f);
            var rng = new System.Random(basePos.GetHashCode());

            for (int i = 0; i < 14; i++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = (1.4f + (float)rng.NextDouble() * 1.6f) * scale;
                Vector3 wormPos = basePos + new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                float h = (1.5f + (float)rng.NextDouble() * 2.2f) * scale;
                float r = 0.06f * scale;

                var stalk = Prim(PrimitiveType.Cylinder, parent, "WormStalk" + i + basePos.GetHashCode(),
                    wormPos + Vector3.up * h * 0.5f, new Vector3(r, h * 0.5f, r), stalkMat, false);
                stalk.transform.localRotation = Quaternion.Euler(
                    (float)(rng.NextDouble() * 14 - 7), 0f, (float)(rng.NextDouble() * 14 - 7));

                // Panache rouge au sommet
                Prim(PrimitiveType.Capsule, parent, "WormPlume" + i + basePos.GetHashCode(),
                    wormPos + Vector3.up * h, new Vector3(r * 1.5f, 0.25f * scale, r * 1.5f), plumeMat, false);
            }
        }

        void BuildWreck(Transform parent, Material rockMid, Material rockDark)
        {
            var wreckRoot = new GameObject("Wreck");
            wreckRoot.transform.SetParent(parent, false);
            wreckRoot.transform.position = new Vector3(42f, -424f, 322f);

            var hull1 = Prim(PrimitiveType.Cube, wreckRoot.transform, "WreckHull", Vector3.zero,
                new Vector3(16f, 7f, 34f), rockDark, true);
            hull1.transform.localRotation = Quaternion.Euler(4f, 18f, 9f);

            var mast = Prim(PrimitiveType.Cylinder, wreckRoot.transform, "WreckMast", new Vector3(3f, 5f, -5f),
                new Vector3(0.5f, 6f, 0.5f), rockMid, true);
            mast.transform.localRotation = Quaternion.Euler(35f, 10f, -15f);

            Prim(PrimitiveType.Cube, wreckRoot.transform, "Debris1", new Vector3(-8f, -1f, 10f),
                new Vector3(4f, 2f, 6f), rockDark, true)
                .transform.localRotation = Quaternion.Euler(12f, 45f, 8f);
            Prim(PrimitiveType.Cube, wreckRoot.transform, "Debris2", new Vector3(12f, 0f, -12f),
                new Vector3(3f, 1.5f, 5f), rockMid, true)
                .transform.localRotation = Quaternion.Euler(-8f, 70f, 15f);

            CreatePointLight(wreckRoot.transform, "WreckGlow", new Vector3(2f, 2f, 5f),
                new Color(0.3f, 0.8f, 0.5f), 14f, 1.6f);
        }

        void BuildLighting()
        {
            var light = FindDirectional();
            if (light == null)
            {
                var obj = new GameObject("Directional Light");
                light = obj.AddComponent<Light>();
                light.type = LightType.Directional;
            }
            light.transform.rotation = Quaternion.Euler(52f, -32f, 0f);
            light.intensity = 1.3f;
            light.color = new Color(0.68f, 0.86f, 1f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.65f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0035f;
            RenderSettings.fogColor = new Color(0.06f, 0.28f, 0.44f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.20f, 0.38f, 0.52f);
            RenderSettings.skybox = null;
        }

        // ==================================================================
        //  POST-PROCESSING — CINÉMATIQUE URP
        // ==================================================================

        void BuildPostFx()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    data.renderPostProcessing = true;
                    data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    data.antialiasingQuality = AntialiasingQuality.High;
                }
            }

            var volumeObject = new GameObject("PostFX");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "SubmarinePostFx";
            volume.sharedProfile = profile;

            // Tonemapping ACES
            var tone = profile.Add<Tonemapping>();
            tone.mode.Override(TonemappingMode.ACES);

            // Bloom
            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(0.65f);
            bloom.intensity.Override(1.35f);
            bloom.scatter.Override(0.72f);

            // Color adjustments
            var color = profile.Add<ColorAdjustments>();
            color.contrast.Override(20f);
            color.saturation.Override(16f);
            color.postExposure.Override(0.18f);
            color.colorFilter.Override(new Color(0.85f, 0.94f, 1f));

            // Vignette
            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.38f);
            vignette.smoothness.Override(0.65f);
            vignette.color.Override(new Color(0.01f, 0.04f, 0.10f));

            // Film grain
            var grain = profile.Add<FilmGrain>();
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.20f);

            // Chromatic aberration
            var chroma = profile.Add<ChromaticAberration>();
            chroma.intensity.Override(0.12f);

            // Depth of field
            var dof = profile.Add<DepthOfField>();
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(24f);
            dof.focalLength.Override(50f);
            dof.aperture.Override(5.6f);

            // Motion blur
            var mb = profile.Add<MotionBlur>();
            mb.intensity.Override(0.15f);
            mb.quality.Override(MotionBlurQuality.High);

            // Lens distortion
            var lens = profile.Add<LensDistortion>();
            lens.intensity.Override(-0.10f);

            // Split toning
            var split = profile.Add<SplitToning>();
            split.shadows.Override(new Color(0.08f, 0.14f, 0.32f));
            split.highlights.Override(new Color(0.72f, 0.88f, 1f));
            split.balance.Override(-20f);
        }

        // ==================================================================
        //  LEVIATHAN — CRÉATURE GÉANTE
        // ==================================================================

        void BuildLeviathan(Material bodyMat, Material veinMat, Vector3 position, float s)
        {
            var root = new GameObject("Leviathan");
            root.transform.position = position;

            var body = Prim(PrimitiveType.Capsule, root.transform, "Body", Vector3.zero,
                new Vector3(6f, 9f, 6f) * s, bodyMat, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var mid = Prim(PrimitiveType.Capsule, root.transform, "MidSegment", new Vector3(0f, 0f, -5f) * s,
                new Vector3(5f, 4f, 5f) * s, bodyMat, false);
            mid.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            Prim(PrimitiveType.Cube, root.transform, "Tail", new Vector3(0f, 0f, -9f * s),
                new Vector3(7f, 3f, 0.4f) * s, bodyMat, false);
            Prim(PrimitiveType.Cube, root.transform, "TailFinTop", new Vector3(0f, 2f, -10f) * s,
                new Vector3(0.3f, 2f, 0.2f) * s, bodyMat, false);
            Prim(PrimitiveType.Cube, root.transform, "TailFinBot", new Vector3(0f, -2f, -10f) * s,
                new Vector3(0.3f, 2f, 0.2f) * s, bodyMat, false);

            // Épines dorsales
            Prim(PrimitiveType.Cube, root.transform, "Dorsal", new Vector3(0f, 3f, 0.5f),
                new Vector3(0.5f, 3.5f, 6f) * s, bodyMat, false);
            for (int i = 0; i < 5; i++)
            {
                float z = -6f + i * 3f;
                float h = 1.2f + (i < 3 ? i * 0.5f : (4 - i) * 0.5f);
                Prim(PrimitiveType.Cube, root.transform, "Spine" + i, new Vector3(0f, 3.5f * s + h * 0.5f * s, z * s),
                    new Vector3(0.15f, h, 0.3f) * s, bodyMat, false);
            }

            // Mâchoires mandibulaires
            var mandL = Prim(PrimitiveType.Cube, root.transform, "MandibleL", new Vector3(-1.5f, -0.5f, 5f) * s,
                new Vector3(0.4f, 0.6f, 3f) * s, bodyMat, false);
            mandL.transform.localRotation = Quaternion.Euler(8f, -12f, 0f);
            var mandR = Prim(PrimitiveType.Cube, root.transform, "MandibleR", new Vector3(1.5f, -0.5f, 5f) * s,
                new Vector3(0.4f, 0.6f, 3f) * s, bodyMat, false);
            mandR.transform.localRotation = Quaternion.Euler(8f, 12f, 0f);

            // Veines bioluminescentes
            for (int v = 0; v < 6; v++)
            {
                float z = -7f + v * 2.8f;
                Prim(PrimitiveType.Cube, root.transform, "VeinL" + v, new Vector3(-2.5f * s, 0.5f * s, z * s),
                    new Vector3(0.08f, 0.08f, 2.2f) * s, veinMat, false);
                Prim(PrimitiveType.Cube, root.transform, "VeinR" + v, new Vector3(2.5f * s, 0.5f * s, z * s),
                    new Vector3(0.08f, 0.08f, 2.2f) * s, veinMat, false);
            }
            for (int v = 0; v < 4; v++)
            {
                float z = -5f + v * 3.5f;
                Prim(PrimitiveType.Cube, root.transform, "VeinB" + v, new Vector3(0f, -2.8f * s, z * s),
                    new Vector3(0.06f, 0.06f, 2.8f) * s, veinMat, false);
            }

            // Yeux phosphorescents
            var eyeMat = Emissive(CreatureEye, 6.5f);
            Prim(PrimitiveType.Sphere, root.transform, "EyeL", new Vector3(-2.2f, 0.8f, 4f) * s,
                Vector3.one * 0.65f * s, eyeMat, false);
            Prim(PrimitiveType.Sphere, root.transform, "EyeR", new Vector3(2.2f, 0.8f, 4f) * s,
                Vector3.one * 0.65f * s, eyeMat, false);
            Prim(PrimitiveType.Sphere, root.transform, "PupilL", new Vector3(-2.2f, 0.8f, 4.3f) * s,
                Vector3.one * 0.25f * s, PBR(Color.black, 0.9f, 0.1f), false);
            Prim(PrimitiveType.Sphere, root.transform, "PupilR", new Vector3(2.2f, 0.8f, 4.3f) * s,
                Vector3.one * 0.25f * s, PBR(Color.black, 0.9f, 0.1f), false);

            // Tentacules traînantes
            for (int t = 0; t < 4; t++)
            {
                float xOff = -1.5f + t * 1f;
                for (int seg = 0; seg < 3; seg++)
                {
                    float z = -10f - seg * 2.5f;
                    float thick = 0.18f - seg * 0.04f;
                    Prim(PrimitiveType.Capsule, root.transform, "Tentacle" + t + "_" + seg,
                        new Vector3(xOff * s, -1f * s, z * s),
                        new Vector3(thick, 1.2f, thick) * s, bodyMat, false)
                        .transform.localRotation = Quaternion.Euler(80f + seg * 5f, t * 15f, 0f);
                }
            }

            CreatePointLight(root.transform, "Glow", Vector3.zero, CreatureEye, 36f * s, 3.5f);
            CreatePointLight(root.transform, "BellyGlow", new Vector3(0f, -2f, 0f) * s, CreatureVein, 22f * s, 2f);

            var collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 2;
            collider.radius = 3f * s;
            collider.height = 16f * s;
            collider.isTrigger = true;

            AddSonarTarget(root, SonarTargetType.Creature, true);
            root.AddComponent<LeviathanAI>();
        }

        // ==================================================================
        //  ATMOSPHÈRE — PARTICULES & CAUSTICS
        // ==================================================================

        void BuildAtmosphere(Transform submarine)
        {
            BuildSedimentParticles(submarine);
            BuildLightShafts();

            BuildBubbleColumn(new Vector3(15f, -300f, 200f), 1.0f);
            BuildBubbleColumn(new Vector3(-30f, -350f, 300f), 0.7f);
            BuildBubbleColumn(new Vector3(50f, -380f, 340f), 0.5f);
            BuildBubbleColumn(new Vector3(-70f, -260f, 160f), 0.8f);

            CreatePointLight(null, "DeepGlow1", new Vector3(-20f, -400f, 310f), new Color(0.1f, 0.5f, 0.4f), 30f, 1.5f);
            CreatePointLight(null, "DeepGlow2", new Vector3(40f, -380f, 280f), new Color(0.15f, 0.3f, 0.6f), 25f, 1.2f);
            CreatePointLight(null, "DeepGlow3", new Vector3(-60f, -420f, 380f), new Color(0.2f, 0.6f, 0.5f), 20f, 1.0f);
            CreatePointLight(null, "DeepGlow4", new Vector3(0f, -350f, 240f), new Color(0.1f, 0.4f, 0.7f), 22f, 0.8f);
        }

        void BuildSedimentParticles(Transform submarine)
        {
            var obj = new GameObject("MarineSnow");
            obj.transform.position = submarine.position;
            var follower = obj.AddComponent<AtmosphereFollower>();
            follower.target = submarine;

            var ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startColor = new Color(0.6f, 0.75f, 0.85f, 0.3f);
            main.maxParticles = 650;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.02f;

            var emission = ps.emission;
            emission.rateOverTime = 55f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(85f, 45f, 85f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(0.7f, 0.85f, 1f), 0f), new GradientColorKey(new Color(0.5f, 0.65f, 0.8f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.35f, 0.2f), new GradientAlphaKey(0.35f, 0.8f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = grad;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.1f;
        }

        void BuildLightShafts()
        {
            var obj = new GameObject("LightShafts");
            obj.transform.position = new Vector3(0f, -80f, 100f);

            var ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(4f, 14f);
            main.startColor = new Color(0.4f, 0.7f, 0.9f, 0.07f);
            main.maxParticles = 35;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var emission = ps.emission;
            emission.rateOverTime = 4f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(220f, 12f, 220f);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.3f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.5f)));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(0.5f, 0.8f, 1f), 0f), new GradientColorKey(new Color(0.3f, 0.6f, 0.8f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.09f, 0.3f), new GradientAlphaKey(0.09f, 0.7f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = grad;
        }

        void BuildBubbleColumn(Vector3 pos, float scale)
        {
            var obj = new GameObject("BubbleColumn" + pos.GetHashCode());
            obj.transform.position = pos;

            var ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f * scale, 2f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.25f * scale);
            main.startColor = new Color(0.6f, 0.85f, 1f, 0.35f);
            main.maxParticles = 110;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.4f;

            var emission = ps.emission;
            emission.rateOverTime = 9f * scale;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1f * scale;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(0.7f, 0.9f, 1f), 0f), new GradientColorKey(new Color(0.5f, 0.8f, 1f), 1f) },
                new[] { new GradientAlphaKey(0.4f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = grad;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.8f;
        }

        // ==================================================================
        //  CAMERA
        // ==================================================================

        void BuildCamera(Transform submarine)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var obj = new GameObject("Main Camera");
                obj.tag = "MainCamera";
                cam = obj.AddComponent<Camera>();
            }

            cam.transform.position = submarine.position + new Vector3(0f, 4.5f, -13f);
            cam.transform.rotation = submarine.rotation;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Deep;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 650f;
            cam.fieldOfView = 62f;
            cam.allowHDR = true;
            cam.allowMSAA = true;

            if (FindAnyObjectByType<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();

            var follow = cam.gameObject.AddComponent<SubmarineCamera>();
            follow.target = submarine;
            follow.offset = new Vector3(0f, 4.5f, -13f);
        }

        void BuildManagers(GameObject blackBox)
        {
            var managers = new GameObject("GameManagers");

            var game = managers.AddComponent<GameManager>();
            game.blackBoxLocation = blackBox.transform;

            managers.AddComponent<SubmarineHUD>();
            managers.AddComponent<SubmarineInputUI>();
        }

        // ==================================================================
        //  FAUNE & FLORE SOUS-MARINE
        // ==================================================================

        void BuildLife()
        {
            var template = BuildFishTemplate();

            var positions = new[]
            {
                new Vector3(0f, -140f, 150f),
                new Vector3(-60f, -250f, 350f),
                new Vector3(55f, -330f, 300f),
                new Vector3(30f, -190f, 220f),
                new Vector3(-40f, -170f, 100f),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                var school = new GameObject("FishSchool" + i).AddComponent<MarineLife>();
                school.fishPrefab = template;
                school.schoolSize = 22;
                school.schoolRadius = 14f;
                school.isBioluminescent = true;
                school.glowColor = i % 2 == 0 ? new Color(0.35f, 1f, 0.85f) : new Color(0.25f, 0.7f, 1f);
                school.transform.position = positions[i];
            }

            // Méduses bioluminescentes dérivantes
            BuildJellyfishCluster(new Vector3(-25f, -120f, 180f), 5);
            BuildJellyfishCluster(new Vector3(45f, -220f, 260f), 6);
            BuildJellyfishCluster(new Vector3(-55f, -310f, 320f), 4);

            // Nébuleuses de plancton bioluminescent
            SpawnPlankton(new Vector3(0f, -330f, 300f), 320, 80f, new Color(0.32f, 1f, 0.8f));
            SpawnPlankton(new Vector3(0f, -120f, 80f), 240, 60f, new Color(0.25f, 0.85f, 1f));
            SpawnPlankton(new Vector3(-40f, -400f, 350f), 200, 50f, new Color(0.4f, 1f, 0.6f));
            SpawnPlankton(new Vector3(50f, -260f, 240f), 160, 45f, new Color(0.2f, 0.7f, 1f));
        }

        void BuildJellyfishCluster(Vector3 center, int count)
        {
            var bellMat = Glass(JellyBell, 0.95f);
            var glowMat = Emissive(JellyGlow, 3.2f);
            var rng = new System.Random(center.GetHashCode());

            for (int j = 0; j < count; j++)
            {
                Vector3 pos = center + new Vector3(
                    (float)(rng.NextDouble() * 2 - 1) * 22f,
                    (float)(rng.NextDouble() * 2 - 1) * 12f,
                    (float)(rng.NextDouble() * 2 - 1) * 22f);

                var jelly = new GameObject("Jellyfish" + j + center.GetHashCode());
                jelly.transform.position = pos;

                // Ombrelle (Bell)
                var bell = Prim(PrimitiveType.Sphere, jelly.transform, "Bell", Vector3.zero,
                    new Vector3(1.4f, 0.9f, 1.4f), bellMat, false);

                // Organe central luminescent
                Prim(PrimitiveType.Sphere, jelly.transform, "Nucleus", new Vector3(0f, -0.1f, 0f),
                    new Vector3(0.4f, 0.4f, 0.4f), glowMat, false);

                // Tentacules
                var tentacles = new Transform[4];
                for (int t = 0; t < 4; t++)
                {
                    float a = t * Mathf.PI * 0.5f;
                    Vector3 tPos = new Vector3(Mathf.Cos(a) * 0.45f, -0.8f, Mathf.Sin(a) * 0.45f);
                    var tent = Prim(PrimitiveType.Capsule, jelly.transform, "Tentacle" + t, tPos,
                        new Vector3(0.06f, 1.2f, 0.06f), glowMat, false);
                    tentacles[t] = tent.transform;
                }

                // Lumière ponctuelle douce
                CreatePointLight(jelly.transform, "Glow", Vector3.zero, JellyGlow, 8f, 1.2f);

                var anim = jelly.AddComponent<JellyfishAnimator>();
                anim.bell = bell.transform;
                anim.tentacles = tentacles;
                anim.pulseSpeed = 0.9f + (float)rng.NextDouble() * 0.5f;
            }
        }

        void SpawnPlankton(Vector3 pos, int count, float radius, Color color)
        {
            var p = new GameObject("Plankton" + pos.GetHashCode()).AddComponent<BioluminescentPlankton>();
            p.count = count;
            p.spawnRadius = radius;
            p.glowColor = color;
            p.transform.position = pos;
        }

        GameObject BuildFishTemplate()
        {
            var fish = new GameObject("FishTemplate");
            fish.transform.position = new Vector3(0f, -4000f, 0f);
            fish.transform.localScale = new Vector3(0.22f, 0.6f, 0.22f);
            fish.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var renderer = fish.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Emissive(new Color(0.35f, 1f, 0.85f), 1.8f);
            var filter = fish.AddComponent<MeshFilter>();
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Capsule.fbx");
            return fish;
        }

        static void AddSonarTarget(GameObject target, SonarTargetType type, bool moving = false)
        {
            var sonar = target.AddComponent<SonarTarget>();
            sonar.TargetType = type;
            sonar.IsMoving = moving;
        }

        IEnumerator DressParticles()
        {
            yield return null;
            yield return null;

            var material = ParticleMaterial(new Color(0.32f, 1f, 0.82f, 0.85f));
            if (material == null) yield break;

            foreach (var renderer in FindObjectsByType<ParticleSystemRenderer>(FindObjectsSortMode.None))
            {
                var current = renderer.sharedMaterial;
                if (current == null || current.shader == null ||
                    current.shader.name.Contains("InternalError"))
                {
                    renderer.sharedMaterial = material;
                }
            }
        }

        // ------------------------------------------------------------------
        // Lumières & Matériaux PBR
        // ------------------------------------------------------------------

        static void CreatePointLight(Transform parent, string name, Vector3 pos, Color color, float range, float intensity)
        {
            var obj = new GameObject(name);
            if (parent != null)
            {
                obj.transform.SetParent(parent, false);
                obj.transform.localPosition = pos;
            }
            else
            {
                obj.transform.position = pos;
            }
            var light = obj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        static Light CreateSpotLight(Transform parent, string name, Vector3 localPos, Quaternion localRot,
                                      Color color, float range, float angle, float intensity)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPos;
            obj.transform.localRotation = localRot;
            var light = obj.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.range = range;
            light.spotAngle = angle;
            light.intensity = intensity;
            light.shadows = LightShadows.Soft;
            return light;
        }

        static Material PBR(Color color, float smoothness, float metallic = 0.25f)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            return material;
        }

        static Material Glass(Color color, float smoothness)
        {
            var mat = PBR(color, smoothness, 0.0f);
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_AlphaClip", 0f);
                mat.renderQueue = 3000;
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            var c = color;
            c.a = 0.45f;
            mat.color = c;
            return mat;
        }

        static Material Emissive(Color color, float intensity)
        {
            var material = PBR(color, 0.45f, 0.1f);
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", color * intensity);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return material;
        }

        static Material ParticleMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;

            var material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            material.color = color;
            return material;
        }


        static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 localPosition,
            Vector3 localScale, Material material, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            if (!keepCollider)
            {
                var collider = go.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
            }
            return go;
        }

        static AudioClip Loop(string name, float seconds, System.Func<float, int, float> sample)
        {
            const int rate = 44100;
            int count = Mathf.Max(1, Mathf.RoundToInt(seconds * rate));
            var data = new float[count];
            for (int i = 0; i < count; i++)
                data[i] = Mathf.Clamp(sample(i / (float)count, i), -1f, 1f);

            var clip = AudioClip.Create(name, count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    // ==================================================================
    //  COMPOSANTS D'ANIMATION ET D'ATMOSPHÈRE HAUTE-FIDÉLITÉ
    // ==================================================================

    /// <summary>
    /// Contrôle l'intensité et le clignotement de TOUS les projecteurs du sous-marin.
    /// Gère le toggle on/off avec transition lisse, clignotement hadale, et batterie faible.
    /// </summary>
    public sealed class SubmarineLightRig : MonoBehaviour
    {
        public Light spot;
        public Light downLight;
        public Light floodL;
        public Light floodR;
        public Light ambientGlow;
        public BatterySystem battery;
        public float baseIntensity = 28f;
        public float downBaseIntensity = 10f;
        public float floodBaseIntensity = 8f;
        public float ambientGlowBaseIntensity = 12f;

        private float _smoothOn = 1f; // Smooth transition 0→1
        private Light[] _navLights;
        private Light[] _portGlows;

        void Start()
        {
            // Collect all nav and port lights on the submarine
            var allLights = GetComponentsInChildren<Light>();
            var navList = new System.Collections.Generic.List<Light>();
            var portList = new System.Collections.Generic.List<Light>();
            foreach (var l in allLights)
            {
                if (l == spot || l == downLight || l == floodL || l == floodR) continue;
                string n = l.gameObject.name;
                if (n.StartsWith("NavLight")) navList.Add(l);
                else if (n.StartsWith("PortGlow") || n.Contains("HullGlow")) portList.Add(l);
            }
            _navLights = navList.ToArray();
            _portGlows = portList.ToArray();
        }

        void Update()
        {
            if (spot == null || battery == null) return;

            bool wantOn = battery.LightsOn && !battery.IsCritical;
            float target = wantOn ? 1f : 0f;
            _smoothOn = Mathf.MoveTowards(_smoothOn, target, Time.deltaTime * 4f);

            bool isOn = _smoothOn > 0.01f;
            spot.enabled = isOn;
            if (downLight != null) downLight.enabled = isOn;
            if (floodL != null) floodL.enabled = isOn;
            if (floodR != null) floodR.enabled = isOn;
            if (ambientGlow != null) ambientGlow.enabled = isOn;
 
            if (!isOn)
            {
                // Dim nav lights when main lights off but don't kill them
                if (_navLights != null)
                    foreach (var nl in _navLights) if (nl != null) nl.intensity = 0.4f;
                if (ambientGlow != null) ambientGlow.intensity = 0f;
                return;
            }

            // Restore nav lights
            if (_navLights != null)
                foreach (var nl in _navLights) if (nl != null) nl.intensity = 1.8f;

            float flicker = 1f;
            var controller = battery.GetComponent<SubmarineController>();
            if (controller != null && controller.Depth > 320f)
            {
                flicker = 0.85f + Mathf.PerlinNoise(Time.time * 5f, 0.35f) * 0.15f;
                if (Random.value < 0.002f) flicker *= 0.3f;
            }
            if (battery.BatteryPercent < 0.25f)
            {
                flicker *= 0.6f + Mathf.Sin(Time.time * 12f) * 0.35f;
            }

            float dimFactor = _smoothOn * flicker;
            spot.intensity = baseIntensity * dimFactor;
            if (downLight != null) downLight.intensity = downBaseIntensity * dimFactor;
            if (floodL != null) floodL.intensity = floodBaseIntensity * dimFactor;
            if (floodR != null) floodR.intensity = floodBaseIntensity * dimFactor;
            if (ambientGlow != null)
            {
                ambientGlow.intensity = ambientGlowBaseIntensity * dimFactor;
            }
 
            Color lightColor = Color.Lerp(new Color(0.72f, 0.9f, 1f), new Color(1f, 0.86f, 0.62f),
                battery.BatteryPercent < 0.35f ? 1f : 0f);
            spot.color = lightColor;
            if (downLight != null) downLight.color = lightColor;
            if (ambientGlow != null) ambientGlow.color = lightColor;
        }
    }

    /// <summary>
    /// Pulsation dynamique de la balise de la boîte noire.
    /// Lumière forte pulsante visible de loin dans les abysses.
    /// </summary>
    public sealed class BlackBoxBeacon : MonoBehaviour
    {
        public Light mainBeaconLight;
        public Light spotUp;
        public Light haloLight;

        private float _baseIntensity;
        private float _spotBaseIntensity;
        private float _haloBaseIntensity;
        private Light[] _orbitalLights;

        void Start()
        {
            if (mainBeaconLight != null) _baseIntensity = mainBeaconLight.intensity;
            if (spotUp != null) _spotBaseIntensity = spotUp.intensity;
            if (haloLight != null) _haloBaseIntensity = haloLight.intensity;

            // Find orbital lights
            var orbs = new System.Collections.Generic.List<Light>();
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("OrbitalLight"))
                {
                    var l = child.GetComponent<Light>();
                    if (l != null) orbs.Add(l);
                }
            }
            _orbitalLights = orbs.ToArray();
        }

        void Update()
        {
            float t = Time.time;
            // Main pulse: slow rhythmic breathing
            float pulse = (Mathf.Sin(t * 1.5f) + 1f) * 0.5f;
            // Fast secondary pulse
            float fastPulse = (Mathf.Sin(t * 4f) + 1f) * 0.5f;
            float combined = Mathf.Lerp(0.5f, 1.2f, pulse) + fastPulse * 0.15f;

            if (mainBeaconLight != null)
                mainBeaconLight.intensity = _baseIntensity * combined;
            if (spotUp != null)
                spotUp.intensity = _spotBaseIntensity * Mathf.Lerp(0.3f, 1.1f, pulse);
            if (haloLight != null)
                haloLight.intensity = _haloBaseIntensity * combined;

            // Orbital lights rotate and pulse independently
            if (_orbitalLights != null)
            {
                for (int i = 0; i < _orbitalLights.Length; i++)
                {
                    if (_orbitalLights[i] == null) continue;
                    float orbPulse = (Mathf.Sin(t * 2f + i * Mathf.PI * 0.5f) + 1f) * 0.5f;
                    _orbitalLights[i].intensity = Mathf.Lerp(2f, 7f, orbPulse);

                    // Slowly orbit around the black box
                    float angle = (t * 15f + i * 90f) * Mathf.Deg2Rad;
                    float radius = 5f + Mathf.Sin(t * 0.8f + i) * 1.5f;
                    _orbitalLights[i].transform.localPosition = new Vector3(
                        Mathf.Cos(angle) * radius,
                        2f + Mathf.Sin(t * 1.2f + i * 0.7f) * 2f,
                        Mathf.Sin(angle) * radius);
                }
            }
        }
    }

    /// <summary>
    /// Fait tourner l'hélice de propulsion à une vitesse proportionnelle au throttle/vitesse.
    /// Émet des bulles de cavitation lors des fortes accélérations.
    /// </summary>
    public sealed class PropellerSpinner : MonoBehaviour
    {
        public SubmarineController controller;
        public float spinSpeed = 820f;
        public ParticleSystem cavitationParticles;

        void Update()
        {
            if (controller == null) return;

            float throt = Mathf.Abs(controller.Throttle);
            float spd = Mathf.Clamp01(controller.Speed / 8f);
            float drive = Mathf.Max(throt, spd * 0.5f);

            if (drive > 0.02f)
            {
                transform.Rotate(0f, 0f, drive * spinSpeed * Time.deltaTime, Space.Self);
            }

            if (cavitationParticles != null)
            {
                var em = cavitationParticles.emission;
                em.rateOverTime = drive > 0.35f ? drive * 42f : 0f;
            }
        }
    }

    /// <summary>
    /// Ondulation fluide de la surface de l'océan avec houle sinusoïdale.
    /// </summary>
    public sealed class WaterSurfaceAnimator : MonoBehaviour
    {
        private Mesh _mesh;
        private Vector3[] _baseVertices;
        private Vector3[] _displacedVertices;

        void Start()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter != null)
            {
                _mesh = filter.mesh;
                _baseVertices = _mesh.vertices;
                _displacedVertices = new Vector3[_baseVertices.Length];
            }
        }

        void Update()
        {
            if (_mesh == null || _baseVertices == null) return;
            float time = Time.time * 0.85f;
            for (int i = 0; i < _baseVertices.Length; i++)
            {
                var v = _baseVertices[i];
                float wave = Mathf.Sin(v.x * 0.035f + time) * 1.4f +
                             Mathf.Cos(v.z * 0.030f + time * 1.25f) * 1.1f +
                             Mathf.Sin((v.x + v.z) * 0.018f + time * 0.5f) * 0.9f;
                _displacedVertices[i] = new Vector3(v.x, wave, v.z);
            }
            _mesh.vertices = _displacedVertices;
            _mesh.RecalculateNormals();
        }
    }

    /// <summary>
    /// Déplacement et contraction pulsée réaliste des méduses bioluminescentes.
    /// </summary>
    public sealed class JellyfishAnimator : MonoBehaviour
    {
        public Transform bell;
        public Transform[] tentacles;
        public float pulseSpeed = 1.1f;
        public float driftSpeed = 0.45f;

        private float _phase;
        private Vector3 _startPos;
        private Vector3 _bellBaseScale;

        void Start()
        {
            _phase = Random.value * Mathf.PI * 2f;
            _startPos = transform.position;
            if (bell != null) _bellBaseScale = bell.localScale;
        }

        void Update()
        {
            float t = Time.time * pulseSpeed + _phase;
            float pulse = Mathf.Sin(t);
            float thrust = Mathf.Clamp01(pulse);

            if (bell != null)
            {
                bell.localScale = new Vector3(
                    _bellBaseScale.x * (1f - thrust * 0.18f),
                    _bellBaseScale.y * (1f + thrust * 0.32f),
                    _bellBaseScale.z * (1f - thrust * 0.18f));
            }

            transform.position += (Vector3.up * (thrust * driftSpeed) +
                                   new Vector3(Mathf.Sin(t * 0.45f) * 0.06f, 0f, Mathf.Cos(t * 0.35f) * 0.06f)) * Time.deltaTime;

            if (transform.position.y > _startPos.y + 16f)
                transform.position = _startPos - Vector3.up * 6f;

            if (tentacles != null)
            {
                for (int i = 0; i < tentacles.Length; i++)
                {
                    if (tentacles[i] != null)
                    {
                        float sway = Mathf.Sin(t + i * 0.85f) * 8f;
                        tentacles[i].localRotation = Quaternion.Euler(sway, 0f, sway * 0.5f);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Soulève un nuage de sédiments/sable lorsque le sous-marin navigue près du fond.
    /// </summary>
    public sealed class SeabedWake : MonoBehaviour
    {
        public SubmarineController controller;
        public ParticleSystem siltParticles;
        private float _checkTimer;

        void Update()
        {
            _checkTimer -= Time.deltaTime;
            if (_checkTimer > 0f) return;
            _checkTimer = 0.15f;

            if (controller == null || siltParticles == null) return;
            if (controller.Speed < 0.6f && Mathf.Abs(controller.Throttle) < 0.15f)
            {
                var em = siltParticles.emission;
                em.rateOverTime = 0f;
                return;
            }

            if (Physics.Raycast(transform.position, Vector3.down, out var hit, 16f))
            {
                float dist = hit.distance;
                float intensity = 1f - Mathf.Clamp01(dist / 16f);
                siltParticles.transform.position = hit.point + Vector3.up * 0.5f;

                var em = siltParticles.emission;
                em.rateOverTime = intensity * 36f;
            }
            else
            {
                var em = siltParticles.emission;
                em.rateOverTime = 0f;
            }
        }
    }

    /// <summary>
    /// Garde les particules d'atmosphère (neige marine) centrées sur le sous-marin.
    /// </summary>
    public sealed class AtmosphereFollower : MonoBehaviour
    {
        public Transform target;
        void LateUpdate()
        {
            if (target != null)
                transform.position = Vector3.Lerp(transform.position, target.position, Time.deltaTime * 0.5f);
        }
    }
}
