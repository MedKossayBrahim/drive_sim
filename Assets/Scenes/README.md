# Scène SampleScene — Hub, compte et garage

**Fichier :** `Assets/Scenes/SampleScene.unity`  
**Rôle :** scène d’entrée du projet. Elle ne contient presque rien dans le Hierarchy Unity : le compte, le garage 3D et le hub se construisent **au runtime**.

---

## Déroulement (à raconter au prof)

1. Unity charge `SampleScene` (caméra URP, lumière, volume).
2. Au chargement, deux bootstraps s’exécutent automatiquement :
   - `AuthFlow.Bootstrap` → crée `AuthBridge` (écran compte)
   - `TransportSession.Bootstrap` → crée `TransportSession` (lanceur de courses)
3. Le joueur s’inscrit / se connecte / active la biométrie.
4. `GameHub.Open` affiche l’accueil (missions, véhicules, boutique…).
5. Le joueur choisit un véhicule → `GameHub.PlayRequested(mode, véhicule)`.
6. `TransportSession` charge la course **en additif**, cache le hub, puis au retour appelle `GameHub.ReportResult`.

**Chaîne d’appels :**
```
Play → GameHub.PlayRequested(mode, véhicule)
    → TransportSession.OnPlayRequested → LaunchAvion|Bateau|Vélo|Voiture|Sous-marin
    → FinishFrom* → UnloadRide → GameHub.ReportResult(étoiles, score, secondes, pièces)
```

---

## Ce qu’il y a vraiment dans la scène Unity

| Objet Hierarchy | Rôle |
|---|---|
| Main Camera | Caméra URP de base (souvent remplacée / désactivée par le garage) |
| Directional Light | Éclairage |
| Global Volume | Post-process URP |

**Objets créés au Play (DontDestroyOnLoad) :**

| Nom runtime | Créé par | Rôle |
|---|---|---|
| `AuthBridge` | `AuthFlow` | UI compte + orchestration |
| `AuthGarage` | `AuthBackdrop` | Garage 3D procédural derrière le login |
| `GameHub` | `GameHub.Create` | Menu principal (inactif jusqu’à connexion) |
| `TransportSession` | `TransportSession.Bootstrap` | Charge / décharge les 5 courses |

> En Play Mode, chercher ces noms dans Hierarchy (souvent en haut, objets persistants).

---

## Scripts utilisés

| Script | Chemin | Rôle |
|---|---|---|
| `AuthFlow` | `Assets/Scripts/User/AuthFlow.cs` | Ordre des écrans : inscription → login → biométrie → hub |
| `AuthScreen` | `Assets/Scripts/User/AuthScreen.cs` | Champs e-mail / mot de passe / boutons |
| `AuthSession` | `Assets/Scripts/User/AuthSession.cs` | Joueur connecté (`IsSignedIn`, `Player`, `AccessToken`) |
| `AuthApiClient` | `Assets/Scripts/User/AuthApiClient.cs` | HTTP vers le serveur Node (port 5080) |
| `AuthConfig` | `Assets/Scripts/User/AuthConfig.cs` | `localhost:5080` (éditeur) / `PhoneServerUrl` (téléphone) |
| `DeviceAuth` | `Assets/Scripts/User/DeviceAuth.cs` | Face ID / biométrie Android / simulation éditeur |
| `AuthBackdrop` | `Assets/Scripts/User/AuthBackdrop.cs` | Garage 3D (plateau, pistes, caméra orbitale) |
| `GameHub` | `Assets/Scripts/Menu/GameHub.cs` | Accueil, missions, niveaux, boutique, classement, succès |
| `GarageProgress` | `Assets/Scripts/Menu/GarageProgress.cs` | Pièces, étoiles, meilleurs scores, IDs véhicules 1–5 |
| `TransportSession` | `Assets/Scripts/Race/TransportSession.cs` | Lanceur commun des 5 scènes de course |
| `UiKit` | `Assets/Scripts/Menu/UiKit.cs` | Construction UI en code |

---

## Capteurs / physique

| Élément | Utilisé ici ? | Détail |
|---|---|---|
| Rigidbody joueur | Non | Aucune physique de jeu dans SampleScene |
| Accéléromètre / gyro | Non | Réservés aux courses |
| Biométrie | Oui | Face ID (iOS) / BiometricPrompt (Android) via plugins natifs |
| Réseau | Oui | Auth HTTP vers `Backend/server.js` |

Plugins :
- iOS : `Assets/Plugins/iOS/BiometricAuth.mm`
- Android : `Assets/Plugins/Android/.../BiometricPlugin.java`

---

## Véhicules lancés depuis ce hub

| ID | Nom | Scène chargée |
|---|---|---|
| 1 | Avion | `Assets/_Plane/Scenes/Plane_Scene.unity` |
| 2 | Bateau | `Assets/_Boat/Scenes/BoatRunner.unity` |
| 3 | Vélo | `Assets/BikeGame/Scenes/ForestBikeRide.unity` |
| 4 | Voiture | SimplePoly City Demo Scene |
| 5 | Sous-marin | `Assets/_Submarine/Scenes/SubmarineScene.unity` |

Modes UI : 0 Classic, 1 Capture the flag, 2 Slalom, 3 Fee Cash (choix d’écran ; le lancement véhicule est le même).

---

## Questions possibles & réponses (validation)

**Q1. Pourquoi SampleScene est presque vide dans Hierarchy ?**  
**R :** Parce que le compte, le garage et le hub sont créés **au runtime** par `AuthFlow`, `AuthBackdrop` et `GameHub`. La scène ne garde que la base URP (caméra, lumière, volume).

**Q2. Où est le script de login dans la scène ?**  
**R :** Il n’est pas posé à la main. `AuthFlow.Bootstrap` crée `AuthBridge`, puis `AuthScreen` construit l’UI (champs, boutons).

**Q3. Que se passe-t-il exactement au Play ?**  
**R :** `AuthFlow.Bootstrap` + `TransportSession.Bootstrap` (`RuntimeInitializeOnLoadMethod`). Ensuite inscription/login/biométrie → `GameHub.Open`.

**Q4. Comment on lance une course depuis le hub ?**  
**R :** Le joueur choisit mode + véhicule → `GameHub.PlayRequested(mode, véhicule)` → `TransportSession.OnPlayRequested` → `LaunchPlane|Boat|Bike|Car|Submarine`.

**Q5. Comment on revient d’une course au hub ?**  
**R :** La course appelle `TransportSession.FinishFromX(...)`. La scène additive est déchargée (`UnloadRide`), puis `GameHub.ReportResult(étoiles, score, secondes, pièces)`.

**Q6. C’est quoi un chargement « additif » ?**  
**R :** `SceneManager.LoadSceneAsync(..., LoadSceneMode.Additive)` : la course s’ajoute sans détruire SampleScene / les objets DontDestroyOnLoad (`GameHub`, `TransportSession`).

**Q7. Le visage / Face ID part-il sur le serveur ?**  
**R :** Non. La biométrie reste sur le téléphone (`DeviceAuth` + plugins natifs). Le serveur Node reçoit seulement e-mail, mot de passe hashé et jetons JWT.

**Q8. Où est le code biométrie ?**  
**R :** Côté C# : `Assets/Scripts/User/DeviceAuth.cs`. iOS : `Assets/Plugins/iOS/BiometricAuth.mm`. Android : plugin Java `BiometricPlugin`.

**Q9. Où changer l’IP du téléphone ?**  
**R :** `Assets/Scripts/User/AuthConfig.cs` → `PhoneServerUrl` (ex. `http://172.20.10.3:5080`). En éditeur : `EditorServerUrl = http://localhost:5080`.

**Q10. Pourquoi localhost ne marche pas sur le téléphone ?**  
**R :** Sur téléphone, `localhost` = le téléphone lui-même, pas le Mac. Il faut l’IP LAN du Mac affichée par `npm start`.

**Q11. Où sont stockés les comptes ?**  
**R :** `Backend/data/users.json` (ignoré par Git). Contient e-mail, pseudo, hash mot de passe, jetons — **aucune** photo biométrique.

**Q12. Quels objets DontDestroyOnLoad chercher en Play Mode ?**  
**R :** `AuthBridge`, `AuthGarage`, `GameHub`, `TransportSession`.

**Q13. Qui gère pièces / étoiles / meilleurs scores ?**  
**R :** `GarageProgress` en local + sync cloud via `PUT /users/progress`.

**Q14. Les modes Classic / Slalom changent-ils la physique ?**  
**R :** Non. Ce sont des choix d’écran UI. Le lancement véhicule (1–5) reste le même.

**Q15. Y a-t-il un Rigidbody / accéléromètre dans SampleScene ?**  
**R :** Non pour le gameplay hub. La physique et les capteurs de mouvement sont dans les scènes de course.
