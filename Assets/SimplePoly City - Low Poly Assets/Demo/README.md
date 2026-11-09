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

| Élément | Emplacement |
|---|---|
| Rigidbody voiture | GO avec `GyroCarController` |
| Gyroscope | `GyroCarController` → `AttitudeSensor` |
| Pédales | GO runtime `DriveControls` |
| GPS | `GPSWorldTracker` |
| Distance | `CarRide.Update` |
| Retour hub | `CarRide.Finish` → `FinishFromCar` |

---

## Permissions

| Permission | Pourquoi |
|---|---|
| `BODY_SENSORS` (Android) | Gyroscope / attitude |
| Localisation Fine/Coarse | GPS voiture |
| iOS `NSLocationWhenInUseUsageDescription` | Ajoutée par `IosAuthBuild` |

---

## Questions possibles & réponses (validation)

**Q1. Comment la voiture est lancée depuis le hub ?**  
**R :** Véhicule ID **4** → `TransportSession.LaunchCar()` charge la scène SimplePoly en additif et crée `CarRide` si besoin.

**Q2. Quel capteur pour braquer ?**  
**R :** **`AttitudeSensor`** (Input System) dans `GyroCarController` : le roll du téléphone devient l’angle de direction. Activation via `InputSystem.EnableDevice`.

**Q3. Différence gyro voiture vs accel bateau ?**  
**R :** Voiture = `AttitudeSensor` (orientation complète, nouveau Input System). Bateau = `Input.acceleration.x` (accéléromètre Legacy, un seul axe).

**Q4. Y a-t-il des WheelColliders ?**  
**R :** Non. Conduite arcade : `AddForce` pour avancer, `MoveRotation` pour tourner, contraintes X/Z gelées.

**Q5. À quoi servent les pédales UI ?**  
**R :** `DriveControlsUI` fournit accélérateur / frein + levier **R / N / D**. Sans elles (éditeur), W/S et 1/2/3 clavier marchent aussi.

**Q6. Comment le GPS est utilisé ?**  
**R :** `GPSWorldTracker` appelle `Input.location.Start`, lit `lastData` (lat/lon), et peut déplacer une cible monde. Sur Android : permission Fine/Coarse Location.

**Q7. Le GPS est-il obligatoire pour jouer ?**  
**R :** Non. La course marche avec gyro + pédales. Le GPS est une couche optionnelle (tracking / affichage position).

**Q8. Pourquoi `BODY_SENSORS` sur Android ?**  
**R :** Permission demandée par `AndroidPermissionRequester` pour accéder correctement aux capteurs de mouvement (gyro/attitude) sur certains appareils.

**Q9. Comment la distance / le score sont calculés ?**  
**R :** `CarRide` intègre le déplacement du véhicule chaque frame. Coins hub ≈ `Distance / 40`. Meilleur score : `PlayerPrefs` + cloud `BestCar`.

**Q10. Comment obtenir 3 étoiles ?**  
**R :** Parcourir au moins **1000 m** (`FinishCarRoutine`). 1★ dès 200 m (ou `won`), 2★ dès 500 m.

**Q11. Qui renvoie au hub ?**  
**R :** Bouton TERMINER ou Escape → `CarRide.Finish` → `TransportSession.FinishFromCar(won || Distance >= 200)`.

**Q12. Qui suit la voiture à la caméra ?**  
**R :** `CameraFollow` (SmoothDamp, offset ~`(0,5,-10)`, anti-obstacle).

**Q13. Pourquoi `compensateForScreenOrientation = false` ?**  
**R :** Pour que le roll du téléphone reste stable en mode paysage ; sinon Unity réoriente les axes et le steer devient incohérent.

**Q14. Où est le texte arabe du HUD GPS ?**  
**R :** `ArabicTextSupport` : shaping + bidi pour afficher correctement les labels arabes.

**Q15. La scène ville vient d’où ?**  
**R :** Pack asset **SimplePoly City - Low Poly Assets** ; la logique Drive Sim (gyro, hub, GPS) est dans `Assets/Scripts/`.
