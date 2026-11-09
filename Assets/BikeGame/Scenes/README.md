# Scène ForestBikeRide — Vélo

**Fichier :** `Assets/BikeGame/Scenes/ForestBikeRide.unity`  
**Lancement hub :** Véhicule **3** → `TransportSession.LaunchBike()`  
**Retour hub :** `TransportSession.FinishFromBike(won)`

> Doc gameplay détaillée aussi dans `Assets/BikeGame/README.md` (EN). Ce fichier sert à la **validation technique**.

---

## Déroulement

1. Le hub charge `ForestBikeRide` en additif.
2. Si absent, `TransportSession` crée un GameObject `BikeRide` (pont hub).
3. `BikeGameBootstrap` assemble au runtime : vélo, rider, caméra, cœurs, contrôles, barrières.
4. Le joueur roule sur l’île forestière : pièces, 2 rampes, 4 barrières, boule d’arrivée.
5. **3 cœurs** : barrière ou mauvaise réception de rampe = −1 cœur.
6. Boule finale → **LEVEL COMPLETE** → `FinishFromBike(true)`.
7. 0 cœur → **GAME OVER** → `FinishFromBike(false)`.
8. Bouton TERMINER / Escape → retour hub.

---

## Physique (Rigidbody + WheelCollider)

Physique fournie par `SimpleBike` (`Assets/SimpleBikeControl/Scripts/SimpleBike.cs`) :

| Élément | Détail |
|---|---|
| Rigidbody | Sur le vélo ; centre de masse ajusté |
| Roue avant | `WheelCollider` → `steerAngle`, frein |
| Roue arrière | `WheelCollider` → `motorTorque`, frein |
| Frein | `brakeTorque` avant + arrière |
| Marche arrière | `motorTorque` négatif |
| Fin de partie | `SimpleBike.Freeze` → Rigidbody **kinematic** + vitesses à 0 |

Assists gameplay :
- `BikeStabilityAssist` : couple anti-roll pour rester droit
- `BikeWheelie` : wheelie par inclinaison arrière
- Pas de `CharacterController` pour la conduite (rider monté sur le vélo)

---

## Capteurs & contrôles

### Direction (tilt) — `TiltSteering.cs`

| Capteur | API Input System | Rôle |
|---|---|---|
| Gravité (préféré) | `GravitySensor.current` + `gravity.ReadValue()` | Incliner gauche/droite → steer |
| Accéléromètre | `Accelerometer.current` + `acceleration.ReadValue()` | Fallback |
| Activation | **`InputSystem.EnableDevice(...)`** | Sans ça, les lectures restent à 0 |

Mode Touch / Tilt mémorisé dans `PlayerPrefs`.

### Gaz / frein — `MobileBikeControls.cs`

- Joystick tactile (forward / reverse)
- Bouton BOOST
- Mode tilt : pad **PEDAL** + auto-pedal en montée
- Passe les axes à `InputManager.SetMobileInput` (SimpleBikeControl)

---

## Scripts importants

| Script | Chemin | Rôle |
|---|---|---|
| `BikeGameBootstrap` | `BikeGame/Scripts/BikeGameBootstrap.cs` | Spawn vélo/rider/cam/UI/barrières |
| `BikeRide` | `BikeGame/Scripts/BikeRide.cs` | Pont hub : coins, score, Finish |
| `SimpleBike` | `SimpleBikeControl/Scripts/SimpleBike.cs` | WheelColliders + Rigidbody |
| `TiltSteering` | `BikeGame/Scripts/TiltSteering.cs` | Accéléro/gravité → direction |
| `MobileBikeControls` | `BikeGame/Scripts/MobileBikeControls.cs` | Joystick / pédale / boost |
| `HeartSystem` | `BikeGame/Scripts/HeartSystem.cs` | 3 vies, game over, rampe |
| `BarrierCollision` | `BikeGame/Scripts/BarrierCollision.cs` | Hit barrière → −1 cœur |
| `LevelCompleteTrigger` | `BikeGame/Scripts/LevelCompleteTrigger.cs` | Boule d’arrivée |
| `BikeChaseCamera` | `BikeGame/Scripts/BikeChaseCamera.cs` | Caméra 3e personne |
| `CoinCollectible` / `CoinCounter` | `BikeGame/Scripts/` | Pièces |
| `BikeVibration` / `BikeImpactFeedback` | `BikeGame/Scripts/` | Retours haptiques / impacts |

Barrières runtime : `Resources/BikeBarriers/...` via `BikeGameBootstrap.EnsureRoadBarriers` (sources aussi dans le dossier racine `read road bar/`).

---

## Caméra & UI

- Caméra : `BikeChaseCamera` suit le Rigidbody du vélo.
- UI : cœurs (haut droite), contrôles IMGUI, HUD pièces/score (`BikeRide`), overlays Level Complete / Game Over.

---

## Score & étoiles (hub)

`BikeRide` : `Score = Coins * 100` (+ **500** si niveau terminé).

`TransportSession.FinishBikeRoutine` :

| Étoiles | Condition |
|---|---|
| 1 | Victoire **ou** ≥ 8 pièces |
| 2 | ≥ 15 pièces |
| 3 | ≥ 25 pièces |

---

## Où trouver les composants

| Question | Réponse |
|---|---|
| WheelColliders ? | Sur le prefab vélo, référencés par `SimpleBike` (front/rear) |
| Accéléromètre ? | `TiltSteering` → `EnableDevice` + lecture Gravity/Accelerometer |
| Qui spawn le niveau ? | `BikeGameBootstrap` (exécution runtime) |
| Qui enlève un cœur ? | `BarrierCollision` / logique rampe dans `HeartSystem` |
| Qui appelle le hub ? | `LevelCompleteTrigger` (true) / `HeartSystem` game over (false) / `BikeRide.Finish` |

---

## Permissions

Capteurs de mouvement (souvent sans permission).  
`android.permission.VIBRATE` pour les impacts.
