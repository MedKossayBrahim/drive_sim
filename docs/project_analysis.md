# Sky Courier — analyse du projet

État inspecté le **2 novembre 2026**. Analyse des fichiers présents dans le dépôt, et non des seules fonctionnalités demandées dans le chat.

## Scénario final — collecte et atterrissage à l’arrivée

Ajout du 2 novembre 2026. Cette section remplace les anciennes observations d’absence de fin de mission et de simple respawn automatique pour **Plane_Scene**.

### Règles de jeu et configuration

1. Décoller de la piste de départ, collecter **15 pièces minimum** parmi les 55 disponibles, puis rejoindre l’aéroport d’arrivée. Les anneaux restent un parcours de guidage et de points ; leur validation intégrale n’est pas obligatoire par défaut.
2. L’aéroport final est centré en **(0, 0, 3000)**, avec une piste de **48 × 900 m**, de Z=2550 à Z=3450, au niveau MSL 0. Apron, taxiway, hangar, tour et bandes lumineuses complètent l’arrivée. Aucun asset d’aéroport à licence inconnue utilisé.
3. Seule cette piste donne la victoire : contact physique réel, avion aligné à moins de 20°, roulis ≤12°, vitesse verticale ≤2 m/s, gaz ≤15 % et vitesse au sol ≤5 m/s pendant **2 secondes**. Le survol, même à 12 cm du sol, et le posé sur la piste de départ sont refusés.
4. Un freinage de roues de **8 m/s²** aide à l’arrêt après le posé, uniquement sur la piste, en assiette correcte et gaz coupés. Il n’agit pas en vol.
5. Un atterrissage valide ajoute **500 points** et affiche MISSION RÉUSSIE. L’avion est immobilisé ; Recommencer recharge la scène et remet score, pièces et mission à zéro.
6. Le contact avec une montagne est mortel dans ce scénario : explosion de PlaneHealth, commandes arrêtées et écran CRASH — MISSION ÉCHOUÉE. Le coordinateur arrête les coroutines de respawn de PlaneHealth via l’API MonoBehaviour publique ; son script et ses seuils restent inchangés. Recommencer reste disponible.
7. Une ceinture périphérique continue de montagnes ferme la vallée : pied à **3600 m** du centre, crête à **3850 m**, sommets de **1050 à environ 1260 m**, donc au-dessus du plafond de vol de 800 m. Le pied du massif commence environ **150 m après le bout de la piste finale**. Cette barrière comporte un MeshCollider statique fermé, un visuel de 384 triangles et le matériau partagé du relief. Le collider utilise une copie subdivisée de 6144 triangles (arête maximale 357,85 m), afin d’éviter l’avertissement PhysX sur les triangles dépassant 500 m, sans alourdir le rendu. Les collines intérieures restent des obstacles solides ; la ceinture périphérique empêche la sortie par survol.
8. Les montagnes intérieures sont régénérées en préservant la piste finale et son approche. Les layers et la matrice de collision globale ne sont pas modifiés.
9. Le brouillard des presets est multiplié par **0,1** et plafonné à **0,0006** pour conserver la visibilité des massifs : Jour 0,00015 ; Coucher 0,0003 ; Nuit 0,0004 ; Orage 0,0006. Les assets SkyPreset et EXR restent conservés.

Les paramètres de l’objectif, du posé, du freinage et de la ceinture sont dans `Assets/_Plane/Prefabs/FlightMissionSettings.asset`. Le générateur refuse un objectif supérieur au nombre de pièces disponibles et des rayons de relief incohérents.

### Architecture et hiérarchie ajoutées

- `LandingMission` : état Collecting → Approach → Won/Failed, décollage préalable, bonus, arrêt terminal et événements publics. Aucun accès aux champs privés de PlaneHealth ou de CheckpointManager.
- `LandingZone` : validation du posé et freinage dans FixedUpdate, sur le collider exact de la piste d’arrivée.
- `PlaneLandingContact` : contacts physiques de support, invalidés au reset ; un simple raycast de proximité ne suffit plus pour gagner.
- `PlaneMountainCollision` et `MountainHazard` : adaptateur sur l’instance avion et marqueurs de massif, sans Update par montagne. La piste conserve les règles normales d’impact de PlaneHealth.
- `LandingMissionHUD` : objectif visible dès le départ et écrans terminaux dans son propre Canvas, indépendants du HUD généré. Les boutons recherchent le PlaneMenu courant, y compris après une reconstruction de Build HUD.
- La navigation de PlaneHUD pointe vers l’aéroport dès que la collecte est suffisante. Le passage des huit anneaux ne se présente plus comme la fin du jeu lorsque ce coordinateur est présent.

```text
Gameplay/FinalMission (LandingMission)
  MissionCanvas (LandingMissionHUD)
    SafeArea/Objective
    Result/Panel : titre, bilan, RECOMMENCER, QUITTER
Environment/ArrivalAirport (LandingZone, instance de prefab)
  Runway_Arrival, Apron_Arrival, Taxiway_Arrival, marquages, bâtiments
Environment/MountainBoundary (MeshCollider, MountainHazard)
Environment/Mountains (MountainHazard sur la racine uniquement)
Plane : PlaneLandingContact et PlaneMountainCollision sur l’instance de scène
```

Menu idempotent : **Sky Courier/Scenario/Build Final Flight Mission**. Il reconstruit ses racines, réutilise ses assets et dégage les montagnes existantes de l’arrivée. Il ne modifie ni TagManager, ni DynamicsManager, ni PlayerSettings, ni les scènes de build.

### Vérification de cette tâche

- Générateur exécuté deux fois : mêmes nombres d’objets, une mission, un aéroport d’arrivée, une ceinture et 55 pièces.
- **432 raycasts** sur le collider de la ceinture, répartis autour de la vallée et aux altitudes 2, 100 et 799,85 m : aucun passage détecté. Visuel de ceinture : **384 triangles**, hauteur maximale mesurée **1259,555 m**. Après subdivision du collider : 432 sondes supplémentaires réussies, impact physique à 799,85 m et échec terminal revérifiés ; collider de 6144 triangles, arête maximale 357,85 m.
- **41 assertions Play réussies** : décollage, collecte insuffisante puis suffisante, refus de victoire en vol, refus du survol bas y compris à 12 cm, refus de l’autre piste, freinage/arrêt sur la bonne piste, victoire sans crash, bonus unique, écran de réussite, Restart, alerte TERRAIN, vraie collision à 799,85 m, explosion, échec terminal sans respawn, écran d’échec, brouillard des quatre presets, Restart après mort et contact de montagne mortel à 2 m/s.
- Les ramassages de cette suite utilisent l’API des Collectible ; les phases d’approche sont imposées au Rigidbody avec le contrôleur désactivé pour isoler les collisions et la validation. Le posé, le freinage, les contacts et les impacts sont simulés par PhysX. Ce n’est pas une mission complète pilotée au joystick ni un test sur téléphone.
- Scripts temporaires de test supprimés. Compilations runtime et Editor effectuées. Les empreintes de PlaneController, PlaneInput, PlaneHealth, Checkpoint, CheckpointManager, FallCrashDetector, ProjectSettings et des autres scènes sont inchangées par cette tâche.

### Fichiers de cette tâche et étapes manuelles

Créés dans Scripts : `FlightMissionSettings.cs`, `LandingMission.cs`, `LandingZone.cs`, `LandingMissionHUD.cs`, `MountainHazard.cs`, `PlaneMountainCollision.cs`, `PlaneLandingContact.cs`.

Créé dans Editor : `FinalFlightMissionBuilder.cs`. Nouveaux assets : `Prefabs/FlightMissionSettings.asset`, `Prefabs/Airport/ArrivalAirport.prefab`, `Models/Procedural/FinalMission/MountainBoundary.asset`, `Models/Procedural/FinalMission/MountainBoundary_Collision.asset`, `Materials/Arrival_Lights.mat`, et leurs métadonnées.

Modifiés : `PlaneHUD.cs`, `SkyboxSwitcher.cs`, `PlaneHUDSetup.cs`, `MountainBuilder.cs`, `Plane_Scene.unity`, ce document et les données de relief nécessaires à la génération. Les scripts de vol, de santé et de checkpoints n’ont pas été modifiés dans cette tâche.

Aucune assignation Unity nécessaire : le scénario est déjà enregistré dans **Assets/_Plane/Scenes/Plane_Scene.unity**. Pour tester : décoller, collecter 15 pièces, suivre la flèche d’arrivée, s’aligner, réduire les gaz et s’arrêter avant les montagnes. Les seuils se règlent dans FlightMissionSettings ; relancer le menu après une modification de la géométrie.

Android : reconstruire l’APK de cette scène avec le builder Sky Courier existant, puis tester la lisibilité de l’approche, le freinage, les écrans de fin et les performances sur appareil. Aucun nouvel APK ni mesure FPS Android réalisé pour cette tâche. Le builder Android change les réglages globaux lorsqu’il est exécuté : annoncer son lancement avant de l’utiliser.

---

## Mise à jour — score, pièces, montagnes et vibrations

Implémentation du 2 novembre 2026 sur `feature/plane`. Cette section complète l’audit initial ci-dessous ; ses anciennes observations sur le score, la sauvegarde Git et les modules absents décrivent l’état antérieur. Le socle a depuis été enregistré dans le commit local `35bc45f`. Les nouveaux modules de cette section ne sont pas encore commités.

### Nouveaux modules et réglages

- **ScoreManager**, sur `Gameplay/Score`, possède le score total et le nombre de pièces. Il lit le compteur public du CheckpointManager et écoute `OnAllPassed`, sans écrire son score. Valeurs Inspector : **100 points/anneau**, **5 points/seconde restante entière** à la réussite et **0 point de pénalité/crash**. `AddScore(int, string)`, `OnScoreChanged` et `OnPointsAwarded` constituent l’API. Le record utilise `SkyCourier.BestScore`.
- Le score et les pièces restent acquis après crash/respawn. Restart recharge la scène et recommence à zéro ; le record reste enregistré. La désactivation/réactivation du composant ne repointe pas les anneaux déjà récompensés. Un appel isolé à `RestartCourse()` ne constitue pas une nouvelle partie de score : employer le Restart complet pour cela.
- **ScoreHUD** possède un Canvas séparé sous `Gameplay/Score/ScoreCanvas`. Total, record, compteur et retour `+100`/`+25` ont leur propre emplacement : ils ne remplacent aucun message de chute/plafond. Le panneau suit la Safe Area, se masque avant Décoller et pendant les réglages, et survit à Build HUD. Le HUD de vol lit lui aussi le score global s’il est présent.
- **Collectible** : pièce procédurale de **48 triangles**, valeur **25**, SphereCollider de rayon **4 m**, rotation/oscillation du visuel, pop de 0,2 s, puis désactivation. Le pickup est verrouillé avant l’attribution des points, donc plusieurs colliders ne doublent pas le gain. Un AudioSource est optionnel ; aucun son ajouté.
- **CollectibleSpawner** : données et instances sérialisées dans la scène ; aucun double spawn au démarrage. Le générateur place **55 pièces** dans des lignes/arcs entre les huit anneaux et trois bonus près de chacun. Les pièces reviennent au Restart, restent collectées au respawn.
- Les pièces restent sur Default, avec **includeLayers = Plane**, **excludeLayers = ~Plane**, priorité locale 100, et vérification du Rigidbody exact de l’avion. Cargo et les autres objets sont refusés. Aucun nouveau layer ni changement de matrice globale.
- **WorldModuleSettings**, asset `Assets/_Plane/Prefabs/WorldModuleSettings.asset` : seed **1741**, espacement des pièces **65 m**, valeur/rayon/bonus réglables ; 28 montagnes, rayons principaux 1800–3500 m et quelques groupes plus proches, tailles et dégagement réglables.
- **MountainBuilder** : racine `Environment/Mountains`, **28 montagnes de 168 triangles chacune**, un matériau partagé et shader URP à couleurs d’altitude (base verte, roche, neige), éclairage ambiant/soleil et brouillard du ciel. Objets statiques, pas de script par montagne, aucune ombre temps réel. Chaque MeshCollider statique réutilise le mesh basse résolution sur Ground.
- Mesures après génération : **4704 triangles au total**, plus haut sommet **515,72 m**, sommet inférieur à l’alerte 600 m ; rayon externe réel **3725,62 m**, inférieur à la limite 4000 m ; marge horizontale minimale entre une base conservatrice et un segment du parcours **344,51 m**. La génération évite aussi l’aéroport et RefuelZone. Un paramétrage impossible saute un emplacement avec avertissement au lieu de bloquer le parcours.
- **CrashHaptics**, ajouté au prefab Plane, écoute les événements de terrain, chute, décrochage et crash sans modifier leurs scripts. Impulsions de **35–90 ms**, intervalle **1,1–0,22 s** selon le danger ; impact final **400 ms**. Coupure lors de pause, perte de focus, désactivation ou disparition du danger. Vibration OFF/ON dans les réglages, préférence `SkyCourier.Haptics`, activée par défaut.
- Le pont JNI est limité à `UNITY_ANDROID && !UNITY_EDITOR`, met en cache Vibrator/VibrationEffect, utilise l’API 26+ puis le repli historique, et Handheld.Vibrate en dernier recours. Le repli Handheld a une durée dépendante du téléphone et ne permet pas le même contrôle fin d’annulation que Vibrator.
- **HapticsAndroidManifest** ajoute `android.permission.VIBRATE` au manifeste de unityLibrary pendant le callback post-génération Gradle, de manière idempotente. Aucun manifeste global, package, PlayerSettings ou liste de scènes modifié. La permission finale dans un nouvel APK reste à vérifier lors du rebuild ; aucun nouvel APK produit pour cette tâche.

### Menus et hiérarchie

Les nouveaux menus ne modifient aucun réglage global :

1. `Sky Courier/Gameplay/Setup Score And Haptics` : raccorde le score et son Canvas, ajoute l’haptique au prefab Plane.
2. `Sky Courier/Gameplay/Build Collectibles` : remplace uniquement `Gameplay/Collectibles` et ses assets propres. Requiert le ScoreManager existant.
3. `Sky Courier/Environment/Build Mountains` : remplace uniquement `Environment/Mountains` et ses assets propres.
4. Les menus existants **9 - Build HUD** et **16 - Setup Mobile UI** recréent correctement les références de score et l’option Vibration.

```text
Gameplay
  Score (ScoreManager)
    ScoreCanvas (ScoreHUD, CanvasScaler)
      SafeArea
        ScorePanel : Total / Coins / Points
  Collectibles (CollectibleSpawner)
    Coin_001 ... Coin_055
  Checkpoints (conservé)
  Plane (prefab, avec CrashHaptics)
Environment
  Mountains
    Mountain_01 ... Mountain_28
HUD/PauseMenu/SafeArea/Panel/Viewport/Content/Vibration
```

### Vérification effectuée

- Assemblies runtime et Editor compilées ; la branche conditionnelle **UNITY_ANDROID sans UNITY_EDITOR** compile également avec les références Unity installées. Cela ne constitue pas une construction IL2CPP/APK.
- ScoreSetup, CollectibleSetup, MountainBuilder, Build HUD et MobileUISetup exécutés **deux fois chacun** : mêmes nombres d’objets, mêmes positions et mêmes tailles de meshes ; une racine Score, un spawner, un Canvas de score, un composant haptique et une ligne Vibration. Résultat final : 55 pièces et 28 montagnes, sans doublons.
- **35 assertions Play réussies** : nouvelle partie à zéro ; présence haptique ; interrupteur et préférence ; pickup interdit avant Décoller et en Pause ; Time.timeScale = 1 à la reprise ; Touch ; repli Touch lorsque Tilt n’a pas de capteur ; refus Cargo ; filtres locaux ; vrai ramassage physique Plane +25 ; refus d’un deuxième gain ; fin du pop ; franchissement physique de chaque anneau ; +800 et bonus de temps exact ; réabonnement sans double gain ; expiration sans bonus ; moteur coupé au réservoir vide ; ravitaillement par API ; crash avec commandes coupées et pénalité 0 ; explosion ; respawn avec santé/commandes restaurées et score conservé ; record ; Restart avec score/pièces/anneaux/carburant/gaz réinitialisés et toutes les pièces réapparues.
- Pour isoler les triggers, le test des anneaux positionne le Rigidbody et lui donne une vitesse positive, avec contrôleur et gravité temporairement désactivés en Play. Le test de respawn de cette suite provoque le crash par `PlaneHealth.ApplyDamage` : ce n’est pas une validation du pilotage Tilt sur appareil ni une simulation de mission complète.
- **Six assertions Play supplémentaires réussies sur une montagne réelle** : raycast du collider, alerte TERRAIN par SphereCast avant impact, vrai choc physique déclenchant `PlaneHealth.OnCrashed`, impact normal **40,20 m/s**, explosion et conservation du score avec pénalité 0. La trajectoire est imposée au Rigidbody pour isoler le relief ; le pilotage mobile n’est pas simulé. Total : **41 assertions Play réussies**.
- Validation géométrique réelle dans Unity : triangles, matériau unique, colliders, Ground, absence de scripts et d’ombres, dégagement du parcours/plafond et absence de diagnostic d’erreur du shader montagne. Prévisualisation du HUD et des montagnes en Game View effectuée.
- Permission VIBRATE : ajout exécuté deux fois sur un manifeste de test, une seule déclaration obtenue. La fusion finale Gradle dans un nouvel APK n’a pas encore été exécutée.
- Empreintes des scripts protégés, des réglages ProjectSettings, de SampleScene et de la scène homonyme hors `_Plane` comparées à l’état de départ. Aucun changement causé par les générateurs du module.
- Les préférences utilisateur sont sauvegardées avant la suite de tests et restaurées ensuite. Les outils temporaires de validation sont retirés à la fin de la tâche.
- **Pas de test de vibration sur téléphone, de capture de Profiler Android ni de mesure FPS garantie.** Les teintes utilisent le soleil/brouillard actifs ; vérifier visuellement les quatre ciels sur le GPU cible.

### Fichiers et étapes manuelles

Créés dans Scripts : `ScoreManager.cs`, `ScoreHUD.cs`, `Collectible.cs`, `CollectibleSpawner.cs`, `CrashHaptics.cs`, `WorldModuleSettings.cs`.

Créés dans Editor : `ScoreSetup.cs`, `CollectibleSetup.cs`, `MountainBuilder.cs`, `ModuleSceneUtility.cs`, `HapticsAndroidManifest.cs`.

Autres créations : `VFX/MountainVertexColor.shader`, matériaux Coin_Gold et Mountain_Altitude, `Models/Procedural/Coin.asset`, les 28 meshes `Models/Procedural/Mountains/Mountain_*.asset`, `Prefabs/Gameplay/Collectible_Coin.prefab`, `Prefabs/WorldModuleSettings.asset`, et leurs `.meta`.

Modifiés : `PlaneHUD.cs` (source de score global), `PlaneMenu.cs` (Vibration), `PlaneHUDSetup.cs` (raccordement du score après reconstruction), `MobileUISetup.cs` (ligne Vibration et référence), Plane.prefab (CrashHaptics), Plane_Scene (nouveaux objets et UI), ce document. Aucun script de vol, santé, chute ou checkpoint modifié.

Aucune assignation Unity nécessaire pour utiliser la scène préparée. Pour changer le placement, régler WorldModuleSettings puis relancer uniquement le générateur concerné. Les valeurs du score et des vibrations se règlent sur leurs composants ; les réglages utilisateur restent dans PlayerPrefs.

Sur téléphone :

1. Reconstruire un nouvel APK de **`Assets/_Plane/Scenes/Plane_Scene.unity`** ; l’ancien APK 0.1.0 ne contient pas ces modules. Le builder Android existant modifie PlayerSettings et la liste de scènes lorsqu’il est exécuté : prévenir avant son lancement.
2. Vérifier dans le manifeste fusionné/APK `android.permission.VIBRATE`, puis tester ON/OFF, les impulsions avant impact, leur arrêt après récupération/Pause et l’impulsion de crash sur un vrai téléphone équipé d’un vibreur.
3. Tester Décoller/calibration, Touch/Tilt et multitouch, ramassage, crash/respawn, Restart et persistance du record/réglages après fermeture de l’application.
4. Vérifier les monts, les pièces et le brouillard sous Jour/Coucher/Nuit/Orage ; l’absence de collision sur le parcours et les colliders du relief lors d’un détour.
5. Profiler CPU/GPU, mémoire et FPS à 30 puis 60 sur appareil. La géométrie nouvelle est limitée, mais le coût du jeu complet et du HUD nécessite une mesure réelle.

---

## Conclusion

Le projet est un **prototype de vol jouable, organisé autour d'une scène avion**, avec commandes Android, télémétrie, HUD, parcours d'anneaux, carburant, crash et respawn. Il possède de bons points d'extension publics. La boucle de livraison de colis reste à développer : le décor et le modèle du colis existent, mais `DeliveryZone` est vide.

Avant d'ajouter un module, stabiliser le processus de build, retirer l'application automatique temporaire des correctifs après validation, et sauvegarder les fichiers actuellement non suivis par Git. Pour un module de gameplay, privilégier de nouveaux composants plutôt que de nouvelles corrections de forces superposées au contrôleur.

## Périmètre et niveau de vérification

- Lecture de `AGENTS.md`, des documents d'assets, des 29 scripts runtime et des 17 scripts Editor de `_Plane`, des scènes/prefabs, des packages et réglages du projet.
- Compilation indépendante des deux assemblies C# avec le compilateur et les références déjà générées par Unity : **runtime et Editor réussis, sans diagnostic**. Les sorties de cette vérification sont uniquement dans `/tmp`.
- Résolution statique des GUID de `Plane_Scene`, de `Plane.prefab` et d'`Airport.prefab`, en incluant les packages officiels du cache : aucun GUID externe non résolu. Aucun `fileID` local non nul absent dans la scène et le prefab Plane inspectés. Cela ne vérifie pas les sous-assets importés ni tous les états d'exécution.
- Vérification de la matrice physique enregistrée : le layer Checkpoint ne rencontre que Plane.
- Lecture directe du manifeste de l'APK existant avec `aapt`.
- **Aucune nouvelle session Play, aucun test sur téléphone, aucun profilage CPU/GPU/RAM, aucun nouveau build APK dans cette analyse.** La compilation C# ne valide pas le shader sur GPU, IL2CPP ou les gestes d'un vrai téléphone.
- Les anciennes erreurs du journal concernent notamment des outils temporaires désormais absents. Elles ne prouvent pas une erreur actuelle ; elles ne sont pas présentées comme des régressions actives.
- Aucun script, réglage, asset de jeu ou scène modifié par cette analyse.

## Environnement réel

- Version enregistrée : **Unity 6000.6.2f1**, alors que les consignes historiques citent 6000.6.0f1.
- URP **17.6.0**, Input System **1.20.0**, uGUI **2.6.0**, glTFast **6.20.0**.
- Autres packages officiels déclarés : AI Navigation, Timeline, Visual Scripting, Test Framework, intégrations IDE et contrôle de version. Leur installation ne signifie pas qu'un module de jeu les utilise.
- Active Input Handling : **nouveau Input System**, valeur enregistrée `1`.
- Android sélectionne la qualité **Mobile** et son asset URP : Render Scale 0,8, shadowmap principale 1024, ombres des lumières additionnelles désactivées, distance d'ombres 50 m. Le profil PC utilise notamment Render Scale 1 et shadowmap 2048. Le pipeline vient des profils de qualité, même si la référence globale GraphicsSettings est nulle.
- Aucun `.asmdef` dans Assets : les scripts runtime utilisent l'assembly générale ; les scripts Editor sont séparés par leur dossier et leurs directives.
- Aucun test C# persistant découvert dans Assets.

## Scènes et organisation

**Scène du jeu :** `Assets/_Plane/Scenes/Plane_Scene.unity`.

Elle référence un prefab Plane, un prefab Airport et huit instances de `Checkpoint_Ring`. Les racines explicites sont TouchControls, Main Camera, Directional Light, Environment, Global Volume, EventSystem, Gameplay et HUD. Les instances de prefabs complètent cette hiérarchie.

Les **198 GameObjects explicitement sérialisés dans la scène ne sont pas le total des objets chargés** : les enfants des prefabs sont définis dans leurs propres fichiers. Airport possède à lui seul 207 références d'instances de prefabs imbriquées.

**Deux autres scènes existent :**

- `Assets/Scenes/SampleScene.unity` : scène d'exemple, seule scène activée dans `EditorBuildSettings.asset`.
- `Assets/Plane/Scenes/Plane_Scene.unity` : ancienne scène homonyme, beaucoup plus petite, avec caméra, lumière et volume ; pas le jeu actuel.

Les outils Editor utilisent généralement le chemin complet `_Plane/Scenes/Plane_Scene.unity`. Un nouveau module doit conserver cette précision pour éviter d'ouvrir la scène homonyme.

**Organisation `_Plane` conservée :** Models, Skyboxes, Materials, Prefabs, Scenes, Scripts, Editor, VFX, UI, Audio, Textures. Le dossier Audio ne contient actuellement que `.gitkeep`.

## Modules existants et points d'entrée

### Pilotage et simulation

- `PlaneInput` : clavier/manette pour les essais, Touch et Tilt ; copie privée de l'InputActionAsset à l'exécution ; calibration et compensation paysage ; repli tactile en l'absence de capteur ; slider de gaz ; événements de changement de mode et de reset. Sur mobile, Tilt remplace le mode Keyboard initial et la mission attend « Décoller ».
- `PlaneController` : Rigidbody, poussée, portance, traînée, couples, assistance Facile/Pro, commande d'assiette en Tilt, virage coordonné et plané moteur coupé.
- `PlaneSettings` : configuration de vol partagée. Le prefab référence `Prefabs/Plane_DefaultSettings.asset`.
- `PlaneFlightEnvelope` : correction supplémentaire du piqué et du décrochage, exécutée après le contrôleur. **Présent dans le prefab actuel.**
- `FallCrashDetector` : chute prolongée, avertissement, demande de piqué, vérification d'un impact dur via l'API publique de santé.
- `FlightCeiling` : plafond vertical et retour horizontal ; ajoute aussi des forces et corrige la montée.
- `PlaneAutoConfiguration` : train et volets automatiques via les propriétés publiques. Les boutons ont disparu de l'UI mobile ; les fonctions physiques existent toujours.
- `ControlSurfaceAnimator` : script disponible pour gouvernes et hélice, mais **aucun composant de ce type n'est référencé dans le prefab Plane ou la scène inspectés**. L'animation visuelle n'est donc pas branchée par ces fichiers.

**Ordre des traitements physiques :** PlaneSensors (-200) → EngineCutoff (-100) → FallCrashDetector (-20) → PlaneController (0) → PlaneFlightEnvelope (400) → FlightCeiling (500). Les composants à ordre 0 partagent cet ordre sans garantie relative.

Un nouveau module de vent, de dégâts aérodynamiques ou d'autopilote doit tenir compte de cette chaîne : plusieurs composants consultent les forces accumulées et les compensent. Ajouter une force isolée ne garantit pas son effet final.

### Télémétrie, carburant et santé

- `PlaneSensors` : vitesse, altitude ALT/SOL, cap, assiette, G, angle d'attaque, état de stall du contrôleur, navigation vers le prochain anneau, prévision TERRAIN. Les requêtes sol/obstacles ont une période de 0,1 s, avec tableaux NonAlloc.
- API disponible : `Refuel(float)`, `RefillTank()`, `SetEngineEnabled(bool)`, `IsEngineActive()`, événements OnStall, OnLowFuel, OnFuelEmpty, OnTerrainWarning.
- `EngineCutoff` : impose Throttle=0 quand le moteur n'est plus actif, avant la physique du contrôleur.
- `RefuelZone` : ravitaillement lent au sol, vérification des colliders, du layer, de la vitesse et de la santé ; évite la recharge multiple par plusieurs colliders.
- `PlaneHealth` : dégâts et crash sur vitesse normale d'impact ; explosion Particle System créée par code ; désactive les commandes puis réutilise `PlaneInput.ResetPlane()` pour le respawn.
- API santé : `ApplyDamage(float)`, événements OnDamaged, OnCrashed, OnRespawned. Pas d'API publique de multiplicateur d'explosion.
- `PlaneInput.ResetPlane()` remet position, rotation, vitesses, gaz et état de vol à zéro. **Ce reset seul ne remet ni carburant ni parcours à zéro.** Le reset de mission complet passe actuellement par le rechargement de scène du menu.

### Parcours et mission

- `Checkpoint` : Inactive/Active/Passed, matériaux, pulse, trigger sphérique ; validation géométrique du plan central dans le bon sens et vitesse positive selon Z local ; gestion des colliders multiples.
- `CheckpointManager` : liste ordonnée de huit anneaux, chronomètre, score, `RestartCourse()`, `OnAllPassed`.
- Valeurs enregistrées : limite **180 s**, **100 points par anneau**, maximum actuel **800 points**.
- `PlaneMissionLaunch` : retient le chronomètre avant « Décoller » sur mobile, puis calibre et démarre.
- Les UnityEvents de fin et d'expiration sont **sans callback persistant dans la scène**. Le HUD affiche la réussite, mais aucune récompense, transition de niveau ou interface d'échec dédiée n'est raccordée ici.
- `PlaneSensors.NextCheckpoint` et les propriétés publiques du manager évitent de recalculer la progression dans un nouveau module.
- **`DeliveryZone` est un marqueur vide** : pas de détection de colis, de validation de livraison ou de score de livraison.

### Interface et caméra

- HUD uGUI/TMP : vitesse, altitude, compas, horizon artificiel, gaz, carburant, G, alertes, anneau suivant, timer, score et `ShowMessage(string, float)`.
- Canvas HUD distinct de TouchControls ; Safe Area et adaptation de layout ; joystick visible en Touch, slider conservé en Tilt.
- Réglages : reprise, Touch/Tilt, calibration conditionnelle, Facile/Pro, inversion du pitch, 30/60 fps, recommencer et quitter.
- Préférences PlayerPrefs sous le préfixe `SkyCourier.` : mode, assistance, inversion, cadence. **Pas de sauvegarde de progression de campagne ou de colis.**
- `PlaneCameraRig` : Poursuite, Cockpit, Orbite ; suivi LateUpdate, FOV variable, stabilisation du roulis et raycast anti-clipping ; références avion et CockpitView enregistrées dans la scène.
- `PlaneMobileSettings` : paysage gauche puis autorotation paysage, écran éveillé, cible 30/60 fps, diagnostics désactivés et curseur caché sur téléphone.
- Les références HUD et tactiles essentielles sont renseignées dans la scène. Les références joystick/slider nulles dans le prefab sont normales : elles pointent vers des objets de scène et sont affectées par les overrides de l'instance.

### Ciel et environnement

- Quatre SkyPreset et quatre matériaux Sky_* : Jour, Coucher, Nuit, Orage.
- `SkyboxSwitcher` : transition d'environ une seconde, cycle automatique optionnel de 600 s, raccourcis 1–4 réservés à l'éditeur.
- Scène enregistrée sur Sunset ; cycle automatique désactivé.
- Le shader `VFX/PanoramicBlend.shader` et le switcher contiennent le masquage du disque HDRI original et un soleil/lune directionnel à une élévation minimale de **45°**, sans dépendance à la position de l'avion. Les champs correspondants sont bien enregistrés dans la scène.
- Les directions de masquage sont calibrées pour les HDRI actuels. Remplacer une image de ciel exige de recalibrer ces directions pour éviter un double soleil.
- Le rendu effectif de ce correctif sur le GPU Android n'a pas été testé pendant cet audit.

## Valeurs réellement configurées

Dans `Plane_DefaultSettings.asset` : masse 1200 kg ; poussée maximale 18000 N ; vitesse de décrochage **22 m/s**, soit 79,2 km/h ; autorité complète à 30 m/s ; finesse de plané 8 ; inertie moteur 1 s ; roulis Tilt cible 35° ; pitch Tilt cible 25° ; assistance Facile 1,2 et Pro 0,18.

Le seuil actuel de décrochage n'est donc pas le seuil de 32 m/s évoqué dans d'anciennes demandes. Les distances et trajectoires doivent être équilibrées sur les valeurs de l'asset actuel.

Autres valeurs du prefab : santé 100 ; premier seuil de dégâts 5 m/s ; crash à 20 m/s d'impact normal ; respawn après 3 s ; chute prolongée 4 s ou vitesse verticale ≤ -40 m/s ; carburant 120 L avec consommation 0,08 L/s à plein gaz ; plafond alerte 600 m / limite 800 m ; rayon horizontal 4000 m.

À consommation maximale constante, 120 L représentent théoriquement **1500 s, soit 25 min**, sans ravitaillement. C'est une valeur calculée, pas une autonomie mesurée en vol.

## Assets et coût potentiel sur Android

- 10 GLB, 112 FBX Kenney, 150 prefabs, 358 matériaux `.mat`, 4 panoramas EXR. Les nombres comptent les fichiers présents et ne représentent pas les assets réellement chargés simultanément.
- Les quatre EXR sources occupent **295,02 Mio**. Leur import enregistré est limité à 2048 sur Standalone et 1024 sur Android, Read/Write désactivé. La taille source ne mesure ni la RAM GPU ni la taille dans l'APK.
- Les GLB représentent environ 35,25 Mio ; les modèles complets, licences et textures Kenney environ 39,89 Mio.
- Triangles des GLB, recalculés depuis les accessors du chunk JSON : pine_tree 38148 ; hangar 34522 ; rocks 20180 ; control_tower 17692 ; crate 17118 ; drop_target 10950 ; plane_body 4580 ; wind_turbine 4040 ; checkpoint_ring 2696 ; plane_propeller 2482.
- **Aucun GLB `_lod0` présent**. Les propositions de décimation ne sont pas des optimisations déjà réalisées dans les fichiers inspectés.
- Plusieurs centaines d'objets et beaucoup de matériaux peuvent augmenter les draw calls et l'overdraw UI. Seuls un Profiler Android et un Frame Debugger peuvent chiffrer le coût réel ; aucun budget FPS garanti ici.
- Kenney est documenté CC0 ; les modèles générés et l'anneau gardent une licence « à vérifier » dans `Models/LICENSES.txt`. Les assets d'aéroport en attente restent exclus du flux de travail proposé.

## Points à traiter avant un nouveau module

### Priorité élevée

1. **Build standard sur la mauvaise scène.** `EditorBuildSettings.asset` ne contient que SampleScene. Le builder Android personnalisé corrige les scènes pendant sa configuration ; le build standard ne bénéficie pas automatiquement de cette sélection. Conséquence : jeu absent ou Restart indisponible selon la méthode de build.
2. **APK disponible antérieur aux derniers correctifs.** Le manifeste de `Builds/Android/SkyCourier-test.apk` annonce 0.1.0/code 1, environ 72,81 Mio. Le builder actuel prépare 0.1.1/code 2 et les fichiers du correctif vol/soleil sont plus récents que cet APK. Il ne faut pas utiliser cet APK comme preuve de validation du code actuel sur téléphone.
3. **Outil temporaire encore actif.** `Editor/TemporaryApplyFlightFix.cs` est InitializeOnLoad : hors batchmode, il peut appeler le setup, modifier/sauvegarder prefab et scène une fois par session. Son marqueur est un SessionState, pas une preuve persistante de migration. À retirer après validation ; ne pas le considérer comme une simple classe de test passive.
4. **Sauvegarde Git incomplète.** `Assets/_Plane/`, `Assets/Plane/`, `docs/` et AGENTS.md sont actuellement non suivis. Plusieurs réglages globaux sont modifiés. Les fichiers existent localement, mais ne sont pas encore protégés par un commit. Les règles LFS présentes ne prouvent pas que l'extension LFS est installée ou les fichiers publiés.

### Points d'architecture et d'intégration

5. **Forces de vol réparties sur plusieurs composants.** Controller, Envelope et Ceiling forment ensemble le comportement final ; moteur coupé et FallingNoseDown ajoutent des états transversaux. Un nouveau module de physique doit être testé avec Tilt, Touch, stall, plafond et reset.
6. **Générateurs idempotents mais destructifs sur leur propre hiérarchie.** Build HUD recrée HUD, l'outil d'inputs recrée TouchControls, AirportBuilder remplace Airport et MobileUISetup reconstruit le contenu des réglages. Une UI ajoutée manuellement sous ces branches peut disparaître au prochain menu. Préférer une racine séparée ou étendre explicitement le générateur propriétaire.
7. **Certains menus modifient des réglages globaux.** LayerSetup écrit TagManager et DynamicsManager ; AirportBuilder peut créer un layer ; AndroidApkBuilder modifie PlayerSettings et la liste des scènes. Respecter l'information préalable exigée par AGENTS avant de les exécuter dans une future tâche.
8. **Mono-scène et avion unique.** Plusieurs outils exigent exactement un avion, un manager, un ciel. Les propriétés publiques facilitent une extension locale ; une flotte, un changement d'avion ou du multijoueur nécessiteraient un travail de découplage supplémentaire.
9. **État de mission dispersé.** Pause dans PlaneInput, démarrage dans PlaneMissionLaunch, santé/respawn dans PlaneHealth, parcours dans CheckpointManager et reset complet par rechargement dans PlaneMenu. Introduire un coordinateur léger pour un module de missions, sans créer un second timer ou score concurrent.
10. **État terminal peu présenté.** Le parcours s'arrête à 180 s, mais le HUD montre surtout le temps écoulé ; aucun panneau d'expiration n'est câblé. Prévoir une présentation cohérente avant plusieurs missions.
11. **Animabilité et son incomplets.** ControlSurfaceAnimator existe sans branchement dans le Plane inspecté ; aucun asset audio propre au jeu. Un module audio/retour visuel doit d'abord raccorder ces éléments.
12. **Documentation historique à distinguer de l'état réel.** L'inventaire décrit l'état avant import et affirme encore que glTFast n'est pas installé. Le manifest et les assets actuels font foi. Deux menus différents utilisent aussi le numéro 17 : Tilt Flight et Flight Ceiling.

## Comment ajouter un module sans casser l'existant

### Option la plus prête : livraison de colis

Le module peut s'appuyer sur le modèle `crate`, le layer Cargo et les zones de livraison déjà placées.

Créer par exemple `CargoController`, `CargoItem`, `DeliveryObjective`, `DeliveryMissionDefinition` (ScriptableObject) et `DeliveryMissionManager`, plus un menu Editor propre au module. La configuration de mission doit contenir l'identifiant, la destination, les conditions, le temps et les points.

- `CargoController` reçoit une demande de largage depuis un bouton tactile ; ne pas réutiliser une touche de test comme seule commande.
- `CargoItem` possède son Rigidbody et ses colliders sur Cargo. La matrice exclut Cargo des triggers Checkpoint, ce qui évite de valider les anneaux avec un colis.
- La validation de livraison doit accepter Cargo et contrôler l'impact, la cible et une validation unique. `DeliveryZone` peut devenir la porte d'entrée, ou recevoir un composant voisin si son API doit rester intacte.
- Un registre de mission indépendant additionne les points de livraison ; il ne peut pas écrire `CheckpointManager.Score`, dont le setter est privé. Utiliser une somme affichée par un adaptateur de HUD ou un score global explicite.
- Raccorder OnCrashed/OnRespawned et OnResetCompleted pour annuler/reconstituer le colis correctement. Le respawn existant conserve le parcours ; le Restart recharge toute la scène.
- Utiliser `PlaneHUD.ShowMessage` pour les retours brefs, puis une zone de suivi dédiée pour les objectifs persistants : les avertissements chute/plafond peuvent prendre la priorité sur ce message.
- Placer les objets du module sous une racine distincte, par exemple `Gameplay/Delivery`, et son UI sous un Canvas propre ou dans le générateur HUD adapté. Ne pas déposer un élément manuel dans une branche reconstruite sans prévoir sa recréation.

### Autres extensions possibles

- **Missions et progression :** coordinateur de mission + définitions ScriptableObject + sauvegarde versionnée. Consommer OnAllPassed et OnCrashed, ajouter une fin de mission explicite et un affichage d'expiration.
- **Audio :** composant abonné à EngineThrottle, OnStall et aux événements santé ; sons officiels/libres documentés ; volume et pause gérés séparément.
- **Carte/minicarte :** adapter la télémétrie et NextCheckpoint ; conserver Z+ comme nord et une origine cohérente.
- **Vent/météo physique :** plus risqué, car le ciel actuel est visuel et les forces correctrices peuvent annuler une partie des rafales. Définir un point d'application et des tests avant ajout.
- **Boutique/compte/multijoueur :** aucun socle spécifique observé ; ce serait un nouveau système, pas un raccordement déjà prêt.

### Contrat recommandé pour chaque ajout

Code dans `Assets/_Plane/Scripts`, namespace `Project.Plane`, outils dans Editor. Sérialiser les références et paramètres, écouter les événements publics, équilibrer abonnements OnEnable/OnDisable, appliquer les forces dans FixedUpdate. Ne modifier les quatre scripts protégés qu'après information explicite. Ne pas changer SampleScene ni les scènes des autres véhicules.

Vérifier : mission non démarrée, Pause/Reprendre, changement Touch/Tilt, orientation paysage, absence de capteur, crash/respawn, Restart, carburant vide, parcours terminé/expiré, génération Editor exécutée deux fois et build de la bonne scène.

## Travail effectué et suite manuelle

**Créé :** ce document `docs/project_analysis.md`, accompagné d'une synthèse Canvas hors du projet. **Aucun fichier existant modifié.**

Aucune étape Unity nécessaire pour lire ce rapport. Avant le développement du prochain module : choisir son périmètre, sécuriser le travail dans Git, vérifier Plane_Scene dans le chemin de build retenu et valider une version APK reconstruite avec les derniers correctifs. Ces actions ne sont pas exécutées dans cet audit.
