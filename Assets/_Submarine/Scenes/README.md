# Scène SubmarineScene — Sous-marin

**Fichier :** `Assets/_Submarine/Scenes/SubmarineScene.unity`  
**Lancement hub :** Véhicule **5** → `TransportSession.LaunchSubmarine()`  
**Retour hub :** `TransportSession.FinishFromSubmarine(won)` (Menu / Escape)

---

## Déroulement

1. Le hub charge `SubmarineScene` en additif.
2. `SubmarineBoot` détecte la scène active et construit / câble le monde si besoin (`BuildWorld` + `Wire` + `StartGame`).
3. Objectif : **descendre en profondeur**, trouver la **boîte noire**, rester dans le rayon ~**30 s** pour l’extraire, puis **remonter** au-dessus de `surfaceDepth` (50 m) → **Won**.
4. Échec si coque / oxygène / batterie tombent à zéro → **Lost**.
5. Sonar actif / passif + **micro ambiant** + **boussole** aident la navigation.
6. Bouton **Menu** ou Escape → `FinishFromSubmarine` → hub (+ éventuel `POST /scores`).

États `GameManager` : `MainMenu` → `Playing` → `Won` | `Lost` | `Paused` | `GameOver`.

---

## Physique (Rigidbody)

`SubmarineController` (`[RequireComponent(typeof(Rigidbody))]`) :

| Propriété | Valeur / comportement |
|---|---|
| Masse | ~9–12 tonnes (setup Boot) |
| `useGravity` | **`false`** (gravité simulée manuellement) |
| Interpolation | `Interpolate` |
| Collision | `ContinuousDynamic` |
| Collider | `CapsuleCollider` |
| Poussée | `AddForce(thrustVector, ForceMode.Force)` |
| Gravité simulée | `AddForce(down * mass * 9.81)` |
| Flottabilité | `AddForce(up * mass * 9.81 * 0.95)` |
| Ballast | Force verticale additionnelle |
| Drag | Force opposée à la vitesse |
| Orientation | `AddTorque` yaw/pitch + auto-level |

Créatures (`LeviathanAI`) : Rigidbody sans gravité, ajouté automatiquement si absent.  
Collisions → `HullSystem.TakeDamage`.

---

## Capteurs & contrôles

### Microphone — `MicrophoneSensor.cs`

| API | Usage |
|---|---|
| `Microphone.devices` | Détecte le micro |
| `Microphone.Start(device, true, 1, 44100)` | Boucle 1 s @ 44.1 kHz |
| `Microphone.GetPosition` + `AudioClip.GetData` | Niveau de bruit ambiant |
| `Microphone.End` | Arrêt |

Le bruit réel alimente le sonar passif (`SonarSystem` lit `_mic.NoiseLevel`) : silence / bruit influence la détection.

### Boussole — `CompassSensor.cs`

| API | Usage |
|---|---|
| `Input.compass.enabled = true` | Active le magnétomètre |
| `Input.compass.trueHeading` | Cap affiché au HUD |

### Sonar — `SonarSystem.cs`

- Passif / ping actif via `Physics.OverlapSphere`
- Combine distance physique + bruit moteur + micro

### Input joueur

| Source | Détail |
|---|---|
| Clavier | WASD, Shift/Ctrl ballast, Space ping, etc. (`SubmarineInput`) |
| UI tactile | `SubmarineInputUI` (ballast, throttle, sonar) |
| Boot | Peut forcer `enableTouchControls = false` au profit de l’UI dédiée |

---

## Scripts importants

| Script | Chemin | Rôle |
|---|---|---|
| `SubmarineBoot` | `_Submarine/Scripts/Submarine/SubmarineBoot.cs` | Monde procédural + StartGame |
| `GameManager` | `_Submarine/Scripts/Submarine/GameManager.cs` | Objectifs, win/lose, score, retour hub |
| `SubmarineController` | `_Submarine/Scripts/Submarine/SubmarineController.cs` | Physique poussée / ballast |
| `SubmarineInput` / `SubmarineInputUI` | `_Submarine/Scripts/Submarine/` | Clavier + UI tactile |
| `MicrophoneSensor` | `_Submarine/Scripts/Submarine/MicrophoneSensor.cs` | Bruit ambiant réel |
| `CompassSensor` | `_Submarine/Scripts/Submarine/CompassSensor.cs` | Cap magnétique |
| `SonarSystem` | `_Submarine/Scripts/Submarine/SonarSystem.cs` | Ping / détection |
| `BlackBoxObjective` | `_Submarine/Scripts/Submarine/BlackBoxObjective.cs` | Objectif boîte noire |
| `HullSystem` / `OxygenSystem` / `BatterySystem` | `_Submarine/Scripts/Submarine/` | Conditions de mort |
| `DepthZoneManager` | `_Submarine/Scripts/Submarine/DepthZoneManager.cs` | Zones de profondeur |
| `LeviathanAI` | `_Submarine/Scripts/Submarine/LeviathanAI.cs` | Créature hostile |
| `SubmarineCamera` / `SubmarineHUD` | `_Submarine/Scripts/Submarine/` | Caméra + instruments |
| `MarineLife` / `HeadlightBeam` | `_Submarine/Scripts/Submarine/` | Ambiance / phare |

Namespace C# : `DriveSim.Submarine`.

---

## Caméra & UI

- Caméra : `SubmarineCamera` sur `Camera.main` (offset typique `(0, 4.5, -13)`).
- HUD : profondeur, coque, O₂, batterie, boussole, annonces.
- Bouton runtime **Menu** (`ReturnMenuCanvas`) pour quitter vers le hub.

---

## Score & étoiles (hub)

`GameManager` suit `Score`, `MaxDepthReached`, `BlackBoxCollected`.  
Victoire in-game peut envoyer `POST /scores` au serveur (profondeur, temps, score).

`TransportSession.FinishSubmarineRoutine` :

| Étoiles | Condition |
|---|---|
| 1 | Victoire **ou** boîte noire collectée |
| 2 | Profondeur ≥ **500** **ou** score ≥ **3000** |
| 3 | (Victoire **et** boîte) **ou** score ≥ **7000** |

Pièces hub synthétiques : `étoiles * 10 + (boîte ? 25 : 0)`.

> La victoire in-game **n’appelle pas toute seule** le hub : le joueur quitte via Menu/Escape après Won/Lost. `FinishFromSubmarine(_state == Won)`.

---

## Où trouver les composants

| Question | Réponse |
|---|---|
| Rigidbody sous-marin ? | Sur le GO `Submarine` avec `SubmarineController` |
| Micro ? | `MicrophoneSensor` (souvent ajouté au boot) |
| Boussole ? | `CompassSensor` → `Input.compass` |
| Boîte noire ? | `BlackBoxObjective` / objet BlackBox spawné |
| Qui décide Won ? | `GameManager` (extraction + remontée ≤ `surfaceDepth`) |
| Qui renvoie au hub ? | `GameManager` Menu/Escape → `TransportSession.FinishFromSubmarine` |
| Qui construit le monde ? | `SubmarineBoot.BuildWorld()` si le contrôleur est absent |

---

## Permissions

| Permission | Pourquoi |
|---|---|
| Microphone (`NSMicrophoneUsageDescription`) | Bruit ambiant / sonar |
| Boussole | Cap HUD (souvent sans permission runtime) |
| Réseau | `POST /scores` si connecté |
