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

## Où trouver les composants

| Élément | Emplacement |
|---|---|
| Rigidbody avion | Même GameObject que `PlaneController` |
| Capteurs téléphone | `PlaneInput.FindSensor` |
| Win / Fail | `LandingMission` (+ `PlaneHealth` crash) |
| Retour hub | `PlaneMenu.Quit` → `FinishFromPlane` |
| Chargement scène | `TransportSession.LaunchPlane` |

---

## Permissions

Accéléromètre / attitude : en général **sans** permission runtime.  
Vibration Android possible (haptics crash).

---

## Questions possibles & réponses (validation)

**Q1. Comment l’avion est-il lancé depuis le hub ?**  
**R :** Véhicule ID **1** → `TransportSession.LaunchPlane()` charge `Plane_Scene` en additif, puis `BindMission()` sur `LandingMission` / `ScoreManager`.

**Q2. Quelle physique utilise l’avion ?**  
**R :** Un **Rigidbody dynamique** avec gravité. Pas de WheelCollider. Forces : `AddForce(thrust + lift + drag)` + `AddRelativeTorque` pour tourner.

**Q3. Quels capteurs téléphone pour piloter ?**  
**R :** Dans `PlaneInput` : d’abord `AttitudeSensor`, sinon `GravitySensor`, sinon `Accelerometer`. Il faut `InputSystem.EnableDevice(...)` sinon les valeurs restent à 0.

**Q4. Différence entre `PlaneInput` et `PlaneSensors` ?**  
**R :** `PlaneInput` = vrais capteurs téléphone / stick. `PlaneSensors` = instruments **simulés** (raycasts AGL, G, fuel) — pas le gyro du téléphone.

**Q5. Comment gagne-t-on la mission ?**  
**R :** Collecter / passer les objectifs puis atterrir correctement dans `LandingZone` → `LandingMission.CompleteLanding()` → état `Won`.

**Q6. Comment perd-on ?**  
**R :** Crash via `PlaneHealth.OnCrashed` → état `Failed`.

**Q7. Qui renvoie le résultat au hub ?**  
**R :** `PlaneMenu.Quit` (ou fin) → `TransportSession.FinishFromPlane(won)` → formule d’étoiles → `GameHub.ReportResult`.

**Q8. Comment sont calculées les étoiles ?**  
**R :** 1★ si atterrissage réussi ; 2★ si ≥ 25 pièces ; 3★ si ≥ 40 pièces (`FinishPlaneRoutine`).

**Q9. Où est le joystick tactile ?**  
**R :** Script `VirtualJoystick` + `ThrottleSlider` pour les gaz. Branchés dans `PlaneInput` en mode Touch.

**Q10. Que fait `ContinuousDynamic` sur le Rigidbody ?**  
**R :** Détection de collision continue : limite le tunnelage à haute vitesse (avion rapide vs sol / montagnes).

**Q11. Pourquoi le Rigidbody devient kinematic à la fin ?**  
**R :** Pour figer l’avion sur l’écran de victoire/défaite, sans continuer à simuler les forces.

**Q12. Qui gère la caméra ?**  
**R :** `PlaneCameraRig` (Chase / Cockpit / Orbit), switchable via l’action CameraSwitch.

**Q13. Comment relancer sans revenir au hub ?**  
**R :** `TransportSession.RestartPlane()` décharge puis recharge `Plane_Scene`.

**Q14. Quels layers physiques sont utilisés ?**  
**R :** Plane, Ground, Checkpoint, Cargo — configurés dans `TransportSession.ConfigurePhysicsLayers`.

**Q15. En éditeur sans téléphone, comment piloter ?**  
**R :** Clavier / manette via la map Input System `"Plane"`, ou stick/souris selon le mode.
