# The Silent Descent — simulateur sous-marin

Fosse abyssale, boîte noire à récupérer, créature aveugle qui chasse au son.
Le simulateur est intégré à Drive Sim : la session de pilote vient de `AuthSession`.

## Lancer le jeu

### Depuis l'écran de compte
1. Ouvrir `Assets/Scenes/SampleScene.unity`, puis Play.
2. Se connecter, ou cliquer **MODE ENTRAÎNEMENT — SANS COMPTE**.
3. Sur l'écran d'accueil : **ENTRER DANS LE SIMULATEUR**.

### Directement dans la fosse
Ouvrir `Assets/Scenes/SubmarineScene.unity`, puis Play.
`SubmarineBoot` construit la scène au démarrage : submersible, fosse, créatures,
boîte noire, HUD, commandes tactiles. Aucun câblage manuel n'est nécessaire.

La scène est propre : caméra, lumière directionnelle et l'objet `SubmarineBoot`.
Tout le reste (fosse, submersible, créatures, boîte noire, HUD, commandes) est
construit au lancement. Si vous câblez la scène à la main, `SubmarineBoot`
détecte le `SubmarineController` existant et relie seulement les références
manquantes.

Pour régénérer la scène : menu **Drive Sim > Rebuild Submarine Scene**.

## Boucle de jeu

- Descendre dans la fosse jusqu'à la boîte noire (390 m).
- Rester à moins de 15 m pendant 30 s pour l'extraire.
- Le sonar **actif** (PING) révèle les obstacles mais fait monter le niveau de
  bruit ; au-dessus du seuil, la créature vous traque.
- Le sonar **passif** écoute les contacts mobiles sans bruit.
- La coque, l'oxygène et la batterie se vident. Réparer la coque consomme une
  des deux réparations disponibles.

## Commandes

### Clavier
| Touche | Action |
|---|---|
| W / S | Puissance avant / arrière |
| A / D | Barre (lacet) |
| Q / E ou ↑ / ↓ | Profondeur (pitch) |
| Maj / Ctrl | Ballast : plonger / remonter |
| Espace | Ping sonar |
| T | Marche silencieuse |
| L | Phares |
| R | Réparer la coque |

### Tactile
- **Manche flottant** : moitié gauche de l'écran.
- **Rail MOTEUR** : bord droit, de -100 % à +100 %.
- **SONAR / SILENCE / PHARES / RÉPARER** : boutons en verre en bas à droite.
  L'anneau du SONAR se vide pendant le temps de recharge.

## Interface

| Élément | Rôle |
|---|---|
| `SubmarineHUD` | Jauges radiales coque/O2/énergie, sonar animé, alertes, extraction, écran de fin |
| `SubmarineInputUI` | Manche, rail de puissance, boutons avec retour d'état |
| `AuthScreen` | Compte : dégradés, halos, verre, spinner, écran d'accueil |
| `SubmarineBoot` | Construction de la scène, démarrage de mission, passation avec `AuthFlow` |

## Systèmes

| Script | Rôle |
|---|---|
| `SubmarineController` | Déplacement inertiel, ballast, profondeur, bruit |
| `HullSystem` | Intégrité, fuites, pression, réparations |
| `BatterySystem` | Phares, sonar, moteur, pannes |
| `OxygenSystem` | Consommation, seuils bas/critique |
| `SonarSystem` | Ping actif, écoute passive, niveau de détection |
| `LeviathanAI` | Dormant → curieux → pistage → chasse → attaque → recherche |
| `DepthZoneManager` | Brouillard, lumière et ambiance par zone |
| `GameManager` | Objectif, extraction, score, victoire/défaite |
| `SubmarineCamera` | Caméra troisième personne, inertie, secousses |
| `SubmarineAudio` | Moteur, océan, ping, craquements (clips générés au lancement) |
| `MarineLife` | Bancs de poissons et plancton bioluminescent |
| `BlackBoxObjective` | Balise, halo, extraction |
| `SubmarineInput` | Clavier + API tactile |
| `SubmarineBoot` | Point d'entrée et construction procédurale |

## Zones de profondeur

| Zone | Profondeur | Ambiance |
|---|---|---|
| Surface | 0–200 m | Bleu clair, poissons |
| Crépuscule | 200–500 m | Lumière qui meurt — zone de la boîte noire |
| Minuit | 500–1500 m | Noir, sonar indispensable |
| Abyssal | 1500–3000 m | Bioluminescence |
| Hadal | 3000–4500 m | Zone de pression mortelle |

## Rendu

- URP : bloom sur les émissions, tonemapping ACES, vignette, grain et brouillard exponentiel.
- Cinq zones de profondeur qui font varier brouillard, lumière et ambiance.
- Projecteur avant asservi à la batterie, hublots et accents émissifs.
- Balise acoustique : la distance à la boîte noire apparaît sous 320 m.

## Backend

Le compte et les scores passent par `Backend/server.js` :

```
GET  /health
POST /auth/register  POST /auth/login  POST /auth/refresh  POST /auth/logout
GET  /users/me
POST /scores         GET  /scores
```

Lancer le serveur : `cd Backend && npm install && npm start`.
Sans serveur, le mode entraînement reste accessible.
