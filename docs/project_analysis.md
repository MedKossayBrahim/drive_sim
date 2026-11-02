# Sky Courier — analyse du projet

État inspecté le **2 novembre 2026**. Analyse des fichiers présents dans le dépôt, et non des seules fonctionnalités demandées dans le chat.

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
