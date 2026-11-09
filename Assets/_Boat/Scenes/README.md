# Scène BoatRunner — Bateau

**Fichier :** `Assets/_Boat/Scenes/BoatRunner.unity`  
**Lancement hub :** Véhicule **2** → `TransportSession.LaunchBoat()` → `BoatGameManager.PlayFromHub()`  
**Retour hub :** `TransportSession.FinishFromBoat(won)`

---

## Déroulement

1. Le hub charge `BoatRunner` en additif.
2. `BoatRenderSetup.Apply()` ajuste le rendu.
3. `BoatGameManager.PlayFromHub()` démarre la course **immédiatement** (pas d’écran start bloquant depuis le hub).
4. Le bateau avance en runner (axe Z+) : obstacles, jetons de route, power-ups.
5. Une collision coûte une vie ; à **0 vies** → `EndGame` → retour hub (`FinishFromBoat(true)` = run terminée).
6. Quitter manuellement → `LeaveToHub` → `FinishFromBoat(false)` (abandon).

---

## Physique (Rigidbody)

Sur le bateau (`BoatController` + `[RequireComponent(typeof(Rigidbody), typeof(Collider))]`) :

| Propriété | Valeur / comportement |
|---|---|
| Gravité | **désactivée** (`useGravity = false` côté setup gameplay) |
| Constraints | `FreezeRotationX` + `FreezeRotationZ` (pas de tonneau) |
| Interpolation | `Interpolate` |
| Collision | `ContinuousDynamic` |
| Déplacement | **`MovePosition` / `MoveRotation`** (pas `AddForce`) |
| Collider coque | souvent en **trigger** ; turbo via `OverlapBoxNonAlloc` |

Les obstacles ont aussi un Rigidbody. Le gameplay est un **endless runner contrôlé**, pas une simulation hydrostatique réaliste.

`OceanSurface` : tuile d’océan qui suit le bateau (illusion de mer infinie).

---

## Capteurs & contrôles

Script : `Assets/_Boat/Scripts/BoatController.cs`

| Plateforme | API | Usage |
|---|---|---|
| Mobile | **`Input.acceleration.x`** (API Legacy) | Inclinaison gauche/droite → steer |
| Check | `SystemInfo.supportsAccelerometer` | Active le tilt seulement si dispo |
| Clavier | `Input.GetKey` A/D (steer), W/S (accel/frein) | Éditeur / fallback |
| Pause | Escape / P | Pause |
| Restart | R | Relancer |
| Feedback hit | `Handheld.Vibrate()` | Vibration collision |

> Ici on utilise l’**accéléromètre Legacy** (`Input.acceleration`), pas le nouveau Input System (`Accelerometer.current`). C’est un point utile en validation.

---

## Scripts importants

| Script | Chemin | Rôle |
|---|---|---|
| `BoatController` | `_Boat/Scripts/BoatController.cs` | Déplacement, tilt, collisions |
| `BoatGameManager` | `_Boat/Scripts/BoatGameManager.cs` | Vies, score, distance, jetons, fin de run |
| `OceanSurface` | `_Boat/Scripts/OceanSurface.cs` | Mer qui suit le bateau |
| `ObstacleSpawner` / `Obstacle` | `_Boat/Scripts/` | Spawn et comportement des obstacles |
| `RouteTokenSpawner` / `RouteToken` | `_Boat/Scripts/` | Jetons de score |
| `PowerUpPickup` | `_Boat/Scripts/PowerUpPickup.cs` | Bonus (turbo, etc.) |
| `FollowBoatCamera` | `_Boat/Scripts/FollowBoatCamera.cs` | Caméra + shake au hit |
| `BoatRuntimeUI` | `_Boat/Scripts/BoatRuntimeUI.cs` | Pause / UI power-ups (ajoutée si absente) |
| `BoatRenderSetup` | `_Boat/Scripts/BoatRenderSetup.cs` | Matériaux / shaders |
| `BoatSoundEffects` | `_Boat/Scripts/BoatSoundEffects.cs` | Sons |
| `BoatDifficultyConfig` | `_Boat/Scripts/BoatDifficultyConfig.cs` | Courbe de difficulté |

---

## Caméra & UI

- Caméra : `Camera.main` + `FollowBoatCamera` (shake via `TriggerHitShake`).
- HUD : Score, Distance, Lives, Speed, Tokens.
- Panneaux Start / Game Over dans la scène ; depuis le hub, `PlayFromHub` court-circuite le start.

---

## Score & étoiles (hub)

Dans `TransportSession.FinishBoatRoutine` :

| Étoiles | Condition |
|---|---|
| 0 | Abandon (`won = false`) ou run sans activité |
| 1 | `won` et (score > 0 **ou** jetons > 0 **ou** distance > 30) |
| 2 | Distance ≥ **400** |
| 3 | Distance ≥ **900** |

- Score / distance / jetons : `BoatGameManager`
- « Coins » hub = `TokensCollected`

Note validation : une mort à 0 vies appelle `FinishFromBoat(true)` (la course est considérée comme **terminée**, pas abandonnée) ; le quit manuel passe `false`.

---

## Où trouver les composants

| Question | Réponse |
|---|---|
| Rigidbody bateau ? | Même GameObject que `BoatController` |
| Accéléromètre ? | `BoatController` → lecture `Input.acceleration.x` |
| Qui démarre depuis le hub ? | `TransportSession.LaunchBoat` → `BoatGameManager.PlayFromHub()` |
| Qui compte les vies ? | `BoatGameManager` |
| Qui renvoie le score ? | `EndGame` / `LeaveToHub` → `FinishFromBoat` |

---

## Permissions

Accéléromètre (souvent sans permission). Vibration Android (`VIBRATE`) pour les hits.
