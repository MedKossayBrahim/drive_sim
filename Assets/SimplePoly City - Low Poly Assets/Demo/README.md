# Scène SimplePoly City — Voiture

**Fichier :** `Assets/SimplePoly City - Low Poly Assets/Demo/SimplePoly City - Low Poly Assets_Demo Scene.unity`  
**Lancement hub :** Véhicule **4** → `TransportSession.LaunchCar()`  
**Retour hub :** `TransportSession.FinishFromCar(won)`

---

## Déroulement

1. Le hub charge la scène ville SimplePoly en additif.
2. `TransportSession` crée un GameObject `CarRide` s’il n’existe pas.
3. `DriveControlsUI` (bootstrap) crée l’UI pédales / boîte R-N-D.
4. Le joueur conduit avec le **gyroscope** (attitude) + pédales tactiles.
5. Optionnel : `GPSWorldTracker` mappe la position GPS réelle dans le monde.
6. Bouton **TERMINER** ou Escape → `FinishFromCar` → retour hub.
7. « Victoire soft » : distance ≥ **200 m** (même sans objectif de fin explicite).

---

## Physique (Rigidbody)

Géré par `GyroCarController` (`Assets/Scripts/GyroCarController.cs`) :

| Propriété | Valeur / comportement |
|---|---|
| Rigidbody | Dynamique, non kinematic |
| Constraints | `FreezeRotationX` + `FreezeRotationZ` |
| Centre de masse | Offset Y pour stabilité |
| Accélération | `AddForce(..., ForceMode.Acceleration)` |
| Frein / ralentissement | Écriture de `linearVelocity` |
| Direction | `MoveRotation` (yaw uniquement) |
| Roues | **Pas de WheelCollider** (arcade force + rotation) |

Si GPS actif sur un Rigidbody cible : `MovePosition` pour le suivi monde (sans écraser la velocity de conduite).

---

## Capteurs & contrôles

### Gyroscope / attitude — `GyroCarController`

| Élément | API | Rôle |
|---|---|---|
| Capteur | **`AttitudeSensor.current`** | Quaternion d’orientation téléphone |
| Activation | `InputSystem.EnableDevice(attitudeSensor)` | Obligatoire |
| Lecture | `attitude.ReadValue()` | Roll → angle de braquage |
| Réglage | `InputSystem.settings.compensateForScreenOrientation = false` | Stable en paysage |
| Fallback clavier | A / D | Steer sans capteur |
| Gaz / frein | Pédales UI ou W / S | Accel / brake |
| Boîte | 1 / 2 / 3 ou levier UI | R / N / D |

### GPS (optionnel) — `GPSWorldTracker`

| Élément | API |
|---|---|
| Démarrage | `Input.location.Start(accuracy, distance)` |
| Lecture | `Input.location.lastData` (lat/lon/alt) |
| Arrêt | `Input.location.Stop()` |
| Android | `Permission.FineLocation` / `CoarseLocation` + `RequestUserPermission` |
| Affichage arabe | `ArabicTextSupport` (HUD adresse / statut) |

### Permissions capteurs voiture

`AndroidPermissionRequester` demande **`BODY_SENSORS`** (gyroscope Android).

---

## Scripts importants

| Script | Chemin | Rôle |
|---|---|---|
| `GyroCarController` | `Assets/Scripts/GyroCarController.cs` | Conduite gyro + forces |
| `CarRide` | `Assets/Scripts/CarRide.cs` | Distance, score, Terminer → hub |
| `DriveControlsUI` | `Assets/Scripts/DriveControlsUI.cs` | Pédales + levier R/N/D |
| `GPSWorldTracker` | `Assets/Scripts/GPSWorldTracker.cs` | GPS → offset monde + reverse geocode |
| `CameraFollow` | `Assets/Scripts/CameraFollow.cs` | Suivi SmoothDamp + anti-obstacle |
| `AndroidPermissionRequester` | `Assets/Scripts/AndroidPermissionRequester.cs` | BODY_SENSORS |
| `ArabicTextSupport` | `Assets/Scripts/ArabicTextSupport.cs` | Shaping / bidi texte arabe (HUD GPS) |

Assets visuels ville : pack **SimplePoly City - Low Poly Assets** (bâtiments, routes, props).

---

## Caméra & UI

- Caméra : `CameraFollow` (offset typique `(0, 5, -10)`).
- UI runtime : GameObject `DriveControls` (pédales, boîte).
- HUD IMGUI `CarRide` : distance, score, meilleur score, bouton TERMINER.
- Meilleur score local : `PlayerPrefs` clé `CarRunner.BestScore` (+ cloud `BestCar` si connecté).

---

## Score & étoiles (hub)

- Distance = mètres parcourus (intégrale du déplacement du véhicule).
- Coins hub = `Distance / 40`.
- Score ≈ distance (ou valeur `CarRide.Score`).

`TransportSession.FinishCarRoutine` :

| Étoiles | Condition |
|---|---|
| 1 | `won` **ou** distance ≥ **200** |
| 2 | Distance ≥ **500** |
| 3 | Distance ≥ **1000** |

`CarRide.Finish` passe `won || Distance >= 200` à `FinishFromCar`.

---

## Où trouver les composants

| Question | Réponse |
|---|---|
| Rigidbody voiture ? | Sur le véhicule avec `GyroCarController` |
| Gyroscope ? | `GyroCarController` → `AttitudeSensor` + `EnableDevice` |
| Pédales ? | GameObject runtime `DriveControls` (`DriveControlsUI`) |
| GPS ? | Composant `GPSWorldTracker` (si présent / auto-bootstrappé) |
| Qui calcule la distance ? | `CarRide.Update` (delta de position) |
| Qui renvoie au hub ? | `CarRide.Finish` → `TransportSession.FinishFromCar` |

---

## Permissions

| Permission | Pourquoi |
|---|---|
| `BODY_SENSORS` (Android) | Gyroscope / attitude |
| Localisation Fine/Coarse | GPS voiture |
| iOS `NSLocationWhenInUseUsageDescription` | Ajoutée par `IosAuthBuild` |
