# Scène Plane_Scene — Avion

**Fichier :** `Assets/_Plane/Scenes/Plane_Scene.unity`  
**Lancement hub :** Véhicule **1** → `TransportSession.LaunchPlane()`  
**Retour hub :** `TransportSession.FinishFromPlane(won)`

---

## Déroulement

1. Le hub charge `Plane_Scene` en **additif**.
2. `TransportSession` lie `LandingMission` + `ScoreManager`.
3. Sur mobile : bouton **Décoller** (`PlaneMissionLaunch` → `PlaneInput.BeginMission`).
4. Phase **Collecting** : récupérer pièces / passer des anneaux (checkpoints).
5. Phase **Approach** : s’aligner sur la piste (`LandingZone`).
6. Atterrissage réussi → état **Won** ; crash → **Failed**.
7. Menu Quit / fin → `FinishFromPlane` → déchargement → étoiles au hub.

États mission (`LandingMission`) : `Collecting` → `Approach` → `Won` | `Failed`.

---

## Physique (Rigidbody)

Composant sur l’avion, géré par `PlaneController` :

| Propriété | Valeur / comportement |
|---|---|
| Type | Rigidbody **dynamique** |
| `useGravity` | `true` |
| Collision | `ContinuousDynamic` |
| Interpolation | activée |
| Forces principales | `AddForce(thrust + lift + drag, ForceMode.Force)` |
| Rotation | `AddRelativeTorque(..., ForceMode.Acceleration)` |
| Steer tactile | `AddForce` latéral (`ForceMode.Acceleration`) |
| Fin de mission | Rigidbody passé en **kinematic** (figé) |

Layers physiques configurés par `TransportSession.ConfigurePhysicsLayers` : **Plane**, **Ground**, **Checkpoint**, **Cargo**.

Pas de `WheelCollider` ni `CharacterController` : vol arcade 100 % forces Rigidbody.

---

## Capteurs & contrôles

Script central : `Assets/_Plane/Scripts/PlaneInput.cs`

| Mode | API Unity | Usage |
|---|---|---|
| Gyroscope / attitude (prioritaire) | `AttitudeSensor.current` + `attitude.ReadValue()` | Inclinaison pitch/roll |
| Gravité | `GravitySensor.current.gravity.ReadValue()` | Fallback |
| Accéléromètre | `Accelerometer.current.acceleration.ReadValue()` | Fallback |
| Activation | `InputSystem.EnableDevice(...)` | Obligatoire sinon lecture à 0 |
| Touch | `VirtualJoystick` + `ThrottleSlider` | Stick + gaz |
| Clavier / manette | Input System map `"Plane"` | Throttle, PitchRoll, Yaw, Flaps, Gear, Camera, Pause |

Sur mobile, le mode par défaut est **Tilt**.

> `PlaneSensors.cs` n’utilise **pas** les capteurs téléphone : ce sont des instruments **simulés** (raycasts AGL, G, fuel, obstacles).

Haptique crash : `CrashHaptics` / vibration appareil.

---

## Scripts importants

| Script | Chemin | Rôle |
|---|---|---|
| `PlaneController` | `_Plane/Scripts/PlaneController.cs` | Physique de vol (thrust, lift, drag, torque) |
| `PlaneInput` | `_Plane/Scripts/PlaneInput.cs` | Capteurs + touch + clavier → axes |
| `LandingMission` | `_Plane/Scripts/LandingMission.cs` | Objectifs collecter → atterrir |
| `ScoreManager` | `_Plane/Scripts/ScoreManager.cs` | Score, pièces, meilleurs scores |
| `LandingZone` | `_Plane/Scripts/LandingZone.cs` | Progression atterrissage sur piste |
| `CheckpointManager` / `Checkpoint` | `_Plane/Scripts/` | Anneaux du parcours |
| `PlaneHealth` | `_Plane/Scripts/PlaneHealth.cs` | Dégâts / crash → Failed |
| `PlaneCameraRig` | `_Plane/Scripts/PlaneCameraRig.cs` | Chase / cockpit / orbit |
| `PlaneHUD` / `LandingMissionHUD` / `ScoreHUD` | `_Plane/Scripts/` | Affichages |
| `VirtualJoystick` | `_Plane/Scripts/VirtualJoystick.cs` | Stick tactile |
| `ThrottleSlider` | `_Plane/Scripts/ThrottleSlider.cs` | Gaz tactile |
| `PlaneMenu` | `_Plane/Scripts/PlaneMenu.cs` | Pause, Restart, Quit → hub |
| `PlaneMissionLaunch` | `_Plane/Scripts/PlaneMissionLaunch.cs` | Confirmation décollage mobile |
| `Collectible` / `CollectibleSpawner` | `_Plane/Scripts/` | Pièces en vol |

---

## Caméra & UI

- Caméra : `PlaneCameraRig` (offset chase typique `(0, 3, -14)`, FOV dynamique).
- UI : objectifs mission, score, stick, gaz, menu pause.
- Redémarrage in-scene : `TransportSession.RestartPlane()` recharge la scène avion.

---

## Score & étoiles (hub)

Calcul dans `TransportSession.FinishPlaneRoutine` :

| Étoiles | Condition |
|---|---|
| 0 | Abandon / échec (`won = false`) |
| 1 | Atterrissage réussi |
| 2 | Réussi **et** ≥ 25 pièces |
| 3 | Réussi **et** ≥ 40 pièces |

Score / pièces lus depuis `ScoreManager`.

---

## Où trouver les composants (validation)

| Question | Réponse |
|---|---|
| Rigidbody de l’avion ? | Sur le GameObject avion (même objet que `PlaneController`) |
| Capteurs téléphone ? | `PlaneInput` → méthodes `FindSensor` / lecture Attitude/Gravity/Accelerometer |
| Qui décide Win/Fail ? | `LandingMission` (+ `PlaneHealth` pour crash) |
| Qui renvoie au hub ? | `PlaneMenu.Quit` → `TransportSession.FinishFromPlane` |
| Qui charge la scène ? | `TransportSession.LaunchPlane` depuis le hub |

---

## Permissions

Accéléromètre / attitude : en général **sans** permission runtime.  
Vibration Android possible (haptics crash).
