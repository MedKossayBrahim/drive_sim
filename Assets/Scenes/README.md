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

## Questions possibles (validation)

**« Où est le script de login dans la scène ? »**  
Il n’est pas posé à la main. `AuthFlow` le crée au runtime sur `AuthBridge`.

**« Comment on passe d’une course au hub ? »**  
La course appelle `TransportSession.FinishFromX(...)`, qui décharge la scène additive et appelle `GameHub.ReportResult`.

**« Le visage part-il sur le serveur ? »**  
Non. Le téléphone valide la biométrie localement ; le serveur ne reçoit que e-mail / mot de passe / jetons.

**« Où changer l’IP du téléphone ? »**  
`Assets/Scripts/User/AuthConfig.cs` → `PhoneServerUrl`.
