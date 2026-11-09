# Drive Sim

Jeu mobile pour Android et iPhone, projet de groupe (6 personnes).

Le téléphone vérifie le visage (Face ID sur iPhone, biométrie sur Android). Un petit serveur vérifie le compte. Le visage ne quitte jamais le téléphone : le serveur reçoit un e-mail, un mot de passe, puis un jeton. Il ne reçoit ni photo ni empreinte.

Unity **6000.6.0f1** (Universal Render Pipeline). Package `com.esprit.drivesim`. Android min SDK **26**, iOS **15.0**+.

## Où en est le projet

| Partie | État |
|---|---|
| Compte joueur : inscription, connexion, biométrie, déconnexion | En place |
| Garage 3D derrière l’écran de compte | En place |
| Accueil, missions, niveaux, boutique, classement, succès | En place |
| Serveur de comptes (`Backend`) | En place |
| Sync progression (pièces, niveau, meilleurs scores) | En place |
| Avion | En place (`Assets/_Plane`) |
| Bateau | En place (`Assets/_Boat`) |
| Vélo | En place (`Assets/BikeGame`) |
| Voiture (gyro + GPS) | En place (SimplePoly City) |
| Sous-marin (sonar, micro, boîte noire) | En place (`Assets/_Submarine`) |
| Build Android APK (`Builds/Android/drive_sim.apk`) | En place |

L’écran de compte se construit tout seul au lancement. Il n’y a pas de scène « Login » à remplir à la main. La scène d’entrée est `Assets/Scenes/SampleScene.unity` (caméra, lumière, volume). Dans Cursor, les fichiers `.unity` et `.meta` sont masqués par `.vscode/settings.json`. Ils sont visibles dans Unity et dans le Finder.

Quand l’accueil du jeu s’affiche, le joueur est connecté. Les courses (avion, bateau, vélo, voiture, sous-marin) peuvent alors lire son profil. Elles n’appellent pas Face ID.

Les 6 scènes du build (Editor Build Settings) :

1. `Assets/Scenes/SampleScene.unity`
2. `Assets/_Plane/Scenes/Plane_Scene.unity`
3. `Assets/_Boat/Scenes/BoatRunner.unity`
4. `Assets/BikeGame/Scenes/ForestBikeRide.unity`
5. `Assets/SimplePoly City - Low Poly Assets/Demo/SimplePoly City - Low Poly Assets_Demo Scene.unity`
6. `Assets/_Submarine/Scenes/SubmarineScene.unity`

## Documentation technique par scène (validation)

Chaque scène a un README avec déroulement, Rigidbody, capteurs (API Unity exactes), scripts, score/étoiles et questions possibles :

| Scène | README technique |
|---|---|
| Hub / compte | [`Assets/Scenes/README.md`](Assets/Scenes/README.md) |
| Avion | [`Assets/_Plane/Scenes/README.md`](Assets/_Plane/Scenes/README.md) |
| Bateau | [`Assets/_Boat/Scenes/README.md`](Assets/_Boat/Scenes/README.md) |
| Vélo | [`Assets/BikeGame/Scenes/README.md`](Assets/BikeGame/Scenes/README.md) |
| Voiture | [`Assets/SimplePoly City - Low Poly Assets/Demo/README.md`](Assets/SimplePoly%20City%20-%20Low%20Poly%20Assets/Demo/README.md) |
| Sous-marin | [`Assets/_Submarine/Scenes/README.md`](Assets/_Submarine/Scenes/README.md) |

## Véhicules jouables

| Véhicule | Scène | Lancement | Contrôles / particularités |
|---|---|---|---|
| 1 Avion | `Assets/_Plane/Scenes/Plane_Scene.unity` | Hub → Véhicules → Avion → Jouer | Joystick / touch, checkpoints, atterrissage, pièces |
| 2 Bateau | `Assets/_Boat/Scenes/BoatRunner.unity` | Hub → Véhicules → Bateau → Jouer | Runner océan, obstacles, jetons de route |
| 3 Vélo | `Assets/BikeGame/Scenes/ForestBikeRide.unity` | Hub → Véhicules → Vélo → Jouer | Inclinaison + joystick, cœurs, barrières, rampes, pièces |
| 4 Voiture | `Assets/SimplePoly City - Low Poly Assets/Demo/SimplePoly City - Low Poly Assets_Demo Scene.unity` | Hub → Véhicules → Voiture → Jouer | Gyroscope + pédales ; GPS optionnel (`GPSWorldTracker`) |
| 5 Sous-marin | `Assets/_Submarine/Scenes/SubmarineScene.unity` | Hub → Véhicules → Sous-marin → Jouer | Profondeur, sonar, micro ambiant, boussole, boîte noire |

Le hub écoute `GameHub.PlayRequested(mode, véhicule)`. `TransportSession` charge la scène en additif, puis renvoie le résultat avec `GameHub.ReportResult`.

Les 5 véhicules sont jouables depuis le hub. Les modes (Classic, Capture the flag, Slalom, Fee Cash) sont des choix d’écran ; le lancement véhicule reste le même.

## Checklist validation finale (APK)

1. `npm start` sur le Mac (même Wi‑Fi que le téléphone).
2. Copier l’IP affichée dans `Assets/Scripts/User/AuthConfig.cs` → `PhoneServerUrl` (actuellement `http://172.20.10.3:5080`), puis rebuild APK.
3. Dans Unity : menu **Drive Sim > Build Android APK** (sortie `Builds/Android/drive_sim.apk`), ou en batch :
   ```bash
   /Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity \
     -quit -batchmode -nographics \
     -projectPath "$(pwd)" -buildTarget Android \
     -executeMethod DriveSim.Editor.DriveSimAndroidBuild.BuildFromCommandLine \
     -logFile Builds/Android/build.log
   ```
4. Installer l’APK : `adb install -r Builds/Android/drive_sim.apk`
5. Démo : inscription → hub → Avion / Bateau / Vélo / Voiture / Sous-marin → retour hub → déconnexion → biométrie.
6. Garder le projet Unity ouvert sur un PC pendant la soutenance.

## Lancer le projet

1. Installer [Node.js 18 ou plus récent](https://nodejs.org) (bouton LTS).
2. Installer Unity Hub, puis l’éditeur **6000.6.0f1** (version écrite dans `ProjectSettings/ProjectVersion.txt`).
3. Dans Unity Hub : **Open**, et choisir le dossier qui contient `Assets` et `ProjectSettings`. Ne pas ouvrir le dossier `Assets` tout seul.
4. La première ouverture peut prendre plusieurs minutes. Si une fenêtre « URP Empty Template » apparaît, la fermer.

Depuis la racine du projet, dans un terminal :

```bash
npm start
```

La première fois, si le serveur ne démarre pas, installer ses bibliothèques puis relancer :

```bash
cd Backend
npm install
cd ..
npm start
```

Le terminal doit afficher :

```text
Serveur Drive Sim prêt.
  Sur cet ordinateur : http://localhost:5080
  Sur le téléphone    : http://192.168.x.x:5080
```

Laisser cette fenêtre ouverte. `Ctrl + C` arrête le serveur. Si le système demande d’autoriser les connexions entrantes pour `node`, accepter : le téléphone en a besoin.

Dans Unity, onglet **Game**, triangle **Play**. Le texte doit devenir « Serveur prêt ».

## Tester le compte sur l’ordinateur

L’éditeur Unity ne montre pas Face ID. Les boutons **Simuler la biométrie**, **Biométrie réussie** et **Biométrie refusée** remplacent la fenêtre du téléphone.

1. Onglet **Game**, puis **Play**.
2. **Créer un compte**. Exemple : pseudo `Pilote`, e-mail `pilote@esprit.tn`, mot de passe `secret123`.
3. **Créer le compte**, puis **Activer**.
4. L’accueil s’affiche avec le pseudo. Le terminal montre `POST /auth/register` puis `GET /users/me`.
5. Arrêter Play, le relancer, **Simuler la biométrie**, puis **Biométrie réussie**. La session revient sans mot de passe.
6. **Réglages**, puis **Se déconnecter**.

`200` dans le terminal signifie que le serveur a accepté. `401` signifie un refus (mot de passe ou session).

`Backend/data/users.json` contient l’e-mail, le pseudo, un mot de passe illisible et des jetons illisibles. Il n’y a pas de photo. Ce fichier est ignoré par Git.

## Pour l’équipe course

Après la connexion, ces données sont disponibles :

```csharp
using DriveSim.User;
using UnityEngine;

public class ExempleCourse : MonoBehaviour
{
    void Start()
    {
        if (!AuthSession.IsSignedIn)
            return;

        Debug.Log(AuthSession.Player.DisplayName);
        Debug.Log(AuthSession.Player.Id);
    }
}
```

| Accès | Usage |
|---|---|
| `AuthSession.IsSignedIn` | Vrai quand l’accueil du jeu est atteint |
| `AuthSession.Player.Id` | Identifiant stable, pour une sauvegarde ou un classement |
| `AuthSession.Player.DisplayName` | Pseudo affiché |
| `AuthSession.Player.Email` | E-mail du compte |
| `AuthSession.AccessToken` | Jeton à envoyer au serveur pour une future requête protégée |
| `AuthSession.SignedIn` / `SignedOut` | Événements de connexion et de déconnexion |

Les scripts du compte sont dans `Assets/Scripts/User`. Le menu est dans `Assets/Scripts/Menu`. Le lancement des courses est dans `Assets/Scripts/Race/TransportSession.cs`.

Contrats hub :

- Écouter `GameHub.PlayRequested(mode, véhicule)` : modes 0 Classic, 1 Capture the flag, 2 Slalom, 3 Fee Cash ; véhicules 1 Avion, 2 Bateau, 3 Vélo, 4 Voiture, 5 Sous-marin.
- À la fin, appeler `GameHub.ReportResult(étoiles, score, secondes, pièces)`. Zéro étoile = abandon.
- Fin de course côté véhicule : `TransportSession.FinishFromPlane|Boat|Bike|Car|Submarine(won)`.

Navigation hub : Accueil / Missions / Niveaux, puis barre **BOUTIQUE**, **RÉGLAGES**, **CLASSEMENT**, **SUCCÈS**.

## Installer sur un téléphone

Le téléphone et l’ordinateur doivent être sur le même Wi-Fi. Avant le build, ouvrir `Assets/Scripts/User/AuthConfig.cs` et mettre dans `PhoneServerUrl` l’adresse affichée par `npm start`, par exemple `http://192.168.10.58:5080`. Cette adresse change si le Wi-Fi change. Il faut alors la modifier et refaire le build.

Dans l’éditeur, l’application parle à `http://localhost:5080`. Sur un téléphone, `localhost` désignerait le téléphone, pas l’ordinateur.

Permissions demandées selon la plateforme :

| Capteur / accès | Usage |
|---|---|
| Biométrie / Face ID | Connexion sans mot de passe |
| Gyroscope / BODY_SENSORS | Voiture et vélo |
| GPS (position) | Course voiture (`GPSWorldTracker`) |
| Microphone | Mission sous-marin (bruit ambiant / sonar) |
| Réseau local | Atteindre le serveur Node sur le Wi‑Fi |

### Android

1. Unity Hub, engrenage de l’installation, **Add modules** : **Android Build Support**, OpenJDK, Android SDK & NDK, Android SDK Command Line Tools.
2. Sur le téléphone : Paramètres, À propos, appuyer sept fois sur le numéro de build, puis activer le débogage USB.
3. Unity : **File > Build Profiles > Android > Switch Platform**, puis **Build and Run** — ou menu **Drive Sim > Build Android APK**.

La fenêtre biométrique est celle d’Android : visage, empreinte ou code, selon le téléphone. Sans biométrie configurée, le mot de passe de l’application reste disponible.

### iPhone

Face ID ne s’affiche que dans l’application installée sur l’iPhone, ou dans le simulateur. Le bouton Play de Unity ne l’installe pas.

1. Installer Xcode depuis l’App Store, et le module **iOS Build Support** dans Unity Hub.
2. Sur l’iPhone : **Réglages > Confidentialité et sécurité > Mode développeur**, l’activer, puis redémarrer.
3. Face ID doit déjà être réglé dans **Réglages > Face ID et code**.
4. Unity : **File > Build Profiles > iOS > Switch Platform**, puis **Build** dans un dossier vide, par exemple `Builds/iOS`.
5. Ouvrir le `.xcodeproj`. Brancher l’iPhone, le déverrouiller, et accepter « Faire confiance ».
6. En haut de Xcode, choisir cet iPhone.
7. **Xcode > Settings > Accounts** : ajouter un identifiant Apple. Sur la cible **Unity-iPhone**, onglet **Signing & Capabilities**, cocher **Automatically manage signing** et choisir cette équipe.
8. Si `com.esprit.drivesim` est refusé, le remplacer par un identifiant unique, dans Xcode et dans Unity : **Edit > Project Settings > Player**, identification Android et iOS.
9. Triangle **Run** de Xcode.
10. Si l’iPhone bloque l’ouverture : **Réglages > Général > VPN et gestion de l’appareil**, faire confiance à l’identifiant Apple, puis ouvrir l’icône.

`Assets/Editor/IosAuthBuild.cs` ajoute au build iOS les phrases Face ID, GPS, micro et réseau local, plus ATS HTTP pour le serveur de salle.

Fermer l’application et la rouvrir : Face ID doit ramener l’accueil, et le terminal doit recevoir `POST /auth/refresh`.

Simulateur : **Features > Face ID > Enrolled**, puis **Matching Face** au moment de la demande.

## Démonstration

1. Le terminal du serveur est ouvert.
2. Inscription sur le téléphone, puis l’accueil avec le pseudo.
3. Fermer l’application et la rouvrir : Face ID ou biométrie Android, sans mot de passe.
4. Montrer `Backend/data/users.json` : compte et jetons, aucune donnée biométrique.
5. Refuser la biométrie, puis se connecter avec le mot de passe.
6. **Se déconnecter** ramène au formulaire.
7. Depuis l’accueil : lancer Avion, Bateau, Vélo, Voiture ou Sous-marin, puis revenir au hub.

Le capteur vérifie le visage sur l’appareil. Le serveur vérifie le jeton du compte.

## Fichiers utiles

| Fichier | Rôle |
|---|---|
| `Backend/server.js` | Comptes, mot de passe, jetons, progression, scores. Port **5080**. |
| `Assets/Scripts/User/AuthConfig.cs` | Adresse du serveur : éditeur et téléphone. |
| `Assets/Scripts/User/AuthApiClient.cs` | Appels HTTP vers le serveur (auth + profil + progression). |
| `Assets/Scripts/User/AuthSession.cs` | Joueur connecté, à lire par la course. |
| `Assets/Scripts/User/AuthScreen.cs` | Carte, champs et boutons. |
| `Assets/Scripts/User/AuthBackdrop.cs` | Garage 3D : plateau et pistes. |
| `Assets/Scripts/User/AuthFlow.cs` | Ordre des écrans. |
| `Assets/Scripts/User/DeviceAuth.cs` | Face ID, biométrie Android, ou simulation dans l’éditeur. |
| `Assets/Scripts/Menu/GameHub.cs` | Accueil, missions, niveaux, boutique, classement, succès. |
| `Assets/Scripts/Menu/GarageProgress.cs` | Pièces, étoiles, meilleurs scores et véhicules. |
| `Assets/Scripts/Race/TransportSession.cs` | Charge avion / bateau / vélo / voiture / sous-marin et renvoie le score. |
| `Assets/Scripts/CarRide.cs` | Bridge hub pour la course voiture (distance, Terminer). |
| `Assets/Scripts/GyroCarController.cs` | Conduite gyroscope + pédales. |
| `Assets/Scripts/GPSWorldTracker.cs` | Position GPS (ou simulation) pour la voiture. |
| `Assets/Scripts/DriveControlsUI.cs` | UI de conduite voiture. |
| `Assets/Scripts/ArabicTextSupport.cs` | Affichage texte arabe (HUD GPS). |
| `Assets/Scripts/AndroidPermissionRequester.cs` | Demandes de permissions Android (capteurs). |
| `Assets/Plugins/iOS/BiometricAuth.mm` | Face ID et trousseau iPhone. |
| `Assets/Plugins/Android/.../BiometricPlugin.java` | Fenêtre biométrique Android. |
| `Assets/Editor/IosAuthBuild.cs` | Phrases Face ID / GPS / micro / réseau + ATS au build iPhone. |
| `Assets/Editor/DriveSimAndroidBuild.cs` | Menu et CLI de build APK. |

Le serveur expose :

| Requête | Rôle |
|---|---|
| `GET /health` | Le serveur répond |
| `POST /auth/register` | Crée le compte |
| `POST /auth/login` | E-mail et mot de passe |
| `POST /auth/refresh` | Nouvelle session après la biométrie |
| `POST /auth/logout` | Ferme les sessions du compte |
| `GET /users/me` | Profil, avec le jeton d’accès |
| `PUT /users/progress` | Sync pièces / niveau / meilleurs scores (avion, bateau, vélo, voiture, sous-marin) |
| `POST /scores` | Enregistre un score de course (sous-marin) |
| `GET /scores` | Petit classement des scores |

## Problèmes fréquents

| Ce qui s’affiche | Quoi faire |
|---|---|
| `Missing script: "start"` | La commande a été lancée au mauvais endroit. Depuis la racine du projet : `npm start`. |
| « Le serveur ne répond pas » | Le terminal doit encore afficher « Serveur Drive Sim prêt ». |
| Le téléphone ne joint pas le serveur | Même Wi-Fi. `PhoneServerUrl` dans `AuthConfig.cs` égal à l’adresse du terminal, puis nouveau build. Autoriser `node` dans le pare-feu. |
| Rien à l’écran au Play | Onglet **Game**, pas **Scene**. Dans **Game**, un format téléphone (par exemple 1080x1920) montre mieux le garage. |
| `npm` introuvable | Installer Node.js LTS, fermer le terminal, le rouvrir. |
| Le port 5080 est déjà utilisé | `Ctrl + C` dans l’ancien terminal, puis `npm start`. |
| Build Android : `compileSdk` | Dans `Assets/Plugins/Android/BiometricAuth.androidlib/build.gradle`, remplacer `35` par le numéro indiqué dans l’erreur. |
| Face ID échoue tout de suite | Code et Face ID activés sur l’iPhone. L’application doit être lancée depuis Xcode. |
| Xcode refuse la signature | Changer le Package Name pour un identifiant unique. |
| GPS / micro refusés | Accepter les pop-ups permissions sur le téléphone ; sinon la course continue sans ces capteurs. |
| Message rouge dans la Console Unity | Le message indique le fichier et la ligne. |
