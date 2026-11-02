# Sky Courier — Inventaire des assets téléchargés

Inspection du 25 octobre 2026. Sources : `/Users/mac/Downloads/`. Aucun fichier source copié, déplacé, extrait sur disque, renommé ou modifié. Seul fichier créé dans le dépôt : `docs/asset_inventory.md`. Les chemins de rangement ont été actualisés selon l’arborescence demandée ; pour l’état actuel des copies, voir `docs/asset_import.md`. Le présent inventaire décrit les sources au moment de l’inspection initiale.

## Méthode et limites

- Script Python standard, sans dépendance : `/tmp/sky_courier_glb_inventory.py`. Il lit les chunks JSON/BIN du GLB et affiche un objet JSON par fichier. Le script auxiliaire de rédaction reste également dans `/tmp`.
- Triangles : somme des primitives TRIANGLES, TRIANGLE_STRIP et TRIANGLE_FAN, via les compteurs des accessors ; les 10 fichiers donnent le même total par meshes et par instances dans leur scène par défaut. Pas de rendu ni de test d’import Unity.
- Dimensions X × Y × Z : boîte englobante des positions réelles de la scène par défaut, après transformations hiérarchiques, en mètres selon la convention glTF ; pose initiale, sans évaluer les animations. Les positions quantifiées normalisées de `model.glb` sont décodées correctement. Une dimension importée ne prouve pas une taille physique réaliste.
- Les noms des nodes sans champ `name` sont repérés par leur index ; ces libellés sont des annotations du script, pas des noms présents dans le fichier. Les tailles des images sont celles du PNG encodé, pas la mémoire GPU.
- Archive Kenney listée avec `unzip -l /Users/mac/Downloads/kenney_racing-kit.zip` ; analyse complémentaire par `zipfile` en mémoire, sans extraction. Archive imbriquée de `airport` également lue en mémoire.

**Compatibilité Unity :** FBX/OBJ sont des formats d’import standard. Les GLB nécessitent un importeur ; aucun package glTF n’est déclaré dans `Packages/manifest.json` au moment de l’inspection. Le package officiel Unity `com.unity.cloud.gltfast` constitue une possibilité future, sans installation dans cette tâche. Les fichiers `.max`/`.blend` ne sont pas des assets autonomes portables : logiciel d’origine et conversion nécessaires. Sources : [formats Unity 6](https://docs.unity3d.com/6000.0/Documentation/Manual/3D-formats.html), [Unity : import GLB via glTFast](https://issuetracker.unity.com/issues/23423/no-message-about-needing-gltfast-package-to-correctly-import-the-glb-file), [documentation glTFast](https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.0/manual/index.html).

## 1. GLB individuels

| Fichier (nom exact) | Taille disque | Meshes | Triangles | Matériaux | Textures embarquées (taille encodée) | Dimensions X × Y × Z (m) | Axe le plus long | Utilisable ? / Où le ranger / Remarques |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `L’éolienne.glb` | 4 589 620 octets (4.38 Mio) | 1 | 4 040 | 1 | image_0 : image/png, 4 484 940 octets (4.28 Mio), 2048 × 2048 px | 0.221811 × 0.700493 × 0.616772 | Y | Conditionnel / `Assets/_Plane/Models/Environment/` / Importeur requis ; licence non fournie dans le fichier. |
| `L’hélice.glb` | 512 040 octets (0.49 Mio) | 1 | 2 482 | 1 | image_0 : image/png, 446 980 octets (0.43 Mio), 512 × 512 px | 0.699653 × 0.149626 × 0.291013 | X | Conditionnel / `Assets/_Plane/Models/Plane/` / Importeur requis ; licence non fournie dans le fichier. |
| `avion.glb` | 2 032 464 octets (1.94 Mio) | 1 | 4 580 | 1 | image_0 : image/png, 1 911 904 octets (1.82 Mio), 1024 × 1024 px | 0.597430 × 0.183067 × 0.699724 | Z | Conditionnel / `Assets/_Plane/Models/Plane/` / Importeur requis ; licence non fournie dans le fichier. |
| `cible.glb` | 2 094 828 octets (2.00 Mio) | 1 | 10 950 | 1 | image_0 : image/png, 1 800 764 octets (1.72 Mio), 1024 × 1024 px | 0.700236 × 0.272485 × 0.700092 | X | Conditionnel / `Assets/_Plane/Models/Props/` / Importeur requis ; licence non fournie dans le fichier. |
| `colis.glb` | 996 104 octets (0.95 Mio) | 1 | 17 118 | 1 | image_0 : image/png, 529 856 octets (0.51 Mio), 512 × 512 px | 0.374294 × 0.385373 × 0.439787 | Z | Conditionnel / `Assets/_Plane/Models/Props/` / Importeur requis ; licence non fournie dans le fichier. |
| `hangar.glb` | 8 278 840 octets (7.90 Mio) | 1 | 34 522 | 1 | image_0 : image/png, 7 347 224 octets (7.01 Mio), 2048 × 2048 px | 0.529359 × 0.385803 × 0.700557 | Z | Conditionnel / `Assets/_Plane/Models/Environment/` / Importeur requis ; licence non fournie dans le fichier. |
| `model.glb` | 52 744 octets (0.05 Mio) | 5 | 2 696 | 4 | Aucune | 4.600000 × 1.310084 × 4.600000 | X = Z | Conditionnel / `Assets/_Plane/Models/Checkpoint/` / Importeur requis ; licence non fournie dans le fichier. Extension KHR_mesh_quantization requise ; 4 matériaux, aucun PNG. |
| `rochers.glb` | 7 117 936 octets (6.79 Mio) | 1 | 20 180 | 1 | image_0 : image/png, 6 618 444 octets (6.31 Mio), 2048 × 2048 px | 0.700659 × 0.251178 × 0.515784 | X | Conditionnel / `Assets/_Plane/Models/Environment/` / Importeur requis ; licence non fournie dans le fichier. |
| `sapin.glb` | 8 938 516 octets (8.52 Mio) | 1 | 38 148 | 1 | image_0 : image/png, 7 697 828 octets (7.34 Mio), 2048 × 2048 px | 0.463597 × 0.699512 × 0.458429 | Y | Conditionnel / `Assets/_Plane/Models/Environment/` / Importeur requis ; licence non fournie dans le fichier. |
| `tour.glb` | 2 349 844 octets (2.24 Mio) | 1 | 17 692 | 1 | image_0 : image/png, 1 862 308 octets (1.78 Mio), 1024 × 1024 px | 0.414702 × 0.700320 × 0.415267 | Y | Conditionnel / `Assets/_Plane/Models/Environment/` / Importeur requis ; licence non fournie dans le fichier. |

Tous les GLB individuels sauf `model.glb` ont 1 objet texture, 1 image PNG embarquée, 1 matériau, 1 mesh sans nom et le générateur `THREE.GLTFExporter`. Aucun champ `copyright` dans leurs métadonnées `asset`. `model.glb` indique le générateur `3dassets.dev ingest`, sans champ `copyright` non plus. Ces chaînes ne constituent pas une licence.

### Noms de meshes et de nodes

| Fichier | Noms des meshes | Nodes, dans l’ordre du JSON | Utilisable ? / Où le ranger / Remarques |
| --- | --- | --- | --- |
| `L’éolienne.glb` | (sans nom) | (node 0 sans nom); (node 1 sans nom); (node 2 sans nom); (node 3 sans nom); (node 4 sans nom); geometry_0; world; (node 7 sans nom) | Même condition d’import ; noms génériques sauf model.glb. |
| `L’hélice.glb` | (sans nom) | (node 0 sans nom); (node 1 sans nom); (node 2 sans nom); (node 3 sans nom); (node 4 sans nom); geometry_0; world; (node 7 sans nom) | Même condition d’import ; noms génériques sauf model.glb. |
| `avion.glb` | (sans nom) | (node 0 sans nom); (node 1 sans nom); (node 2 sans nom); (node 3 sans nom); (node 4 sans nom); geometry_0; world; (node 7 sans nom) | Même condition d’import ; noms génériques sauf model.glb. |
| `cible.glb` | (sans nom) | (node 0 sans nom); (node 1 sans nom); (node 2 sans nom); (node 3 sans nom); (node 4 sans nom); geometry_0; world; (node 7 sans nom) | Même condition d’import ; noms génériques sauf model.glb. |
| `colis.glb` | (sans nom) | (node 0 sans nom); (node 1 sans nom); (node 2 sans nom); (node 3 sans nom); (node 4 sans nom); geometry_0; world; (node 7 sans nom) | Même condition d’import ; noms génériques sauf model.glb. |
| `hangar.glb` | (sans nom) | (node 0 sans nom); (node 1 sans nom); (node 2 sans nom); (node 3 sans nom); geometry_0; world; (node 6 sans nom); (node 7 sans nom) | Même condition d’import ; noms génériques sauf model.glb. |
| `model.glb` | objective-capture-point-ring_0; objective-capture-point-ring_1; ring; objective-capture-point-ring_3; objective-capture-point-ring_4 | objective-capture-point-ring; objective-capture-point-ring_0; objective-capture-point-ring_1; ring; objective-capture-point-ring_3; objective-capture-point-ring_4; (node 6 sans nom) | Même condition d’import ; noms génériques sauf model.glb. |
| `rochers.glb` | (sans nom) | (node 0 sans nom); (node 1 sans nom); (node 2 sans nom); (node 3 sans nom); (node 4 sans nom); geometry_0; world; (node 7 sans nom) | Même condition d’import ; noms génériques sauf model.glb. |
| `sapin.glb` | (sans nom) | (node 0 sans nom); (node 1 sans nom); (node 2 sans nom); (node 3 sans nom); (node 4 sans nom); geometry_0; world; (node 7 sans nom) | Même condition d’import ; noms génériques sauf model.glb. |
| `tour.glb` | (sans nom) | (node 0 sans nom); (node 1 sans nom); (node 2 sans nom); (node 3 sans nom); (node 4 sans nom); geometry_0; world; (node 7 sans nom) | Même condition d’import ; noms génériques sauf model.glb. |

**Identification probable de `model.glb` :** un anneau de capture d’objectif ou de checkpoint, d’après `objective-capture-point-ring` et `ring`, avec une emprise circulaire de 4,60 × 4,60 m et une hauteur de 1,31 m ; cette identification est une inférence, sans aperçu visuel.

`model.glb` fait exactement 52 744 octets, soit 52,744 ko décimaux (≈ 53 KB), ou 51,51 Kio. Il contient aussi une animation ; les dimensions ci-dessus concernent la pose initiale. L’extension de quantification impose de normaliser les SHORT avant les transformations, sinon les dimensions sont fausses. Référence : [KHR_mesh_quantization, Khronos](https://github.com/KhronosGroup/glTF/blob/main/extensions/2.0/Khronos/KHR_mesh_quantization/README.md).

## 2. Dossier `airport`

Taille totale logique : **26 234 906 octets (25.02 Mio)**, ZIP source et fichiers `.DS_Store` inclus.

```text
airport/
  .DS_Store — 10 244 octets (0.01 Mio)
  source/
    .DS_Store — 6 148 octets (0.01 Mio)
    Open World Base/
      Airport.fbx — 6 123 024 octets (5.84 Mio)
      Airpot copy.png — 4 126 376 octets (3.94 Mio)
    Open World Base.zip — 10 074 488 octets (9.61 Mio)
  textures/
    Airpot copy.png — 4 121 437 octets (3.93 Mio)
    Grass 5.jpeg — 681 488 octets (0.65 Mio)
    Road 19.jpeg — 663 568 octets (0.63 Mio)
    Tile 44.jpeg — 428 133 octets (0.41 Mio)
```

| Chemin relatif | Taille | Format | Utilisable ? / Où le ranger / Remarques |
| --- | --- | --- | --- |
| .DS_Store | 10 244 octets (0.01 Mio) |  | Non / Ne pas importer / Métadonnées Finder. |
| source/.DS_Store | 6 148 octets (0.01 Mio) |  | Non / Ne pas importer / Métadonnées Finder. |
| source/Open World Base/Airport.fbx | 6 123 024 octets (5.84 Mio) | .fbx | Oui techniquement / `Assets/_Plane/Models/Airport/` / Seul modèle directement importable présent ; matériaux URP à vérifier ; licence absente. |
| source/Open World Base/Airpot copy.png | 4 126 376 octets (3.94 Mio) | .png | Oui techniquement / `Assets/_Plane/Textures/Airport/` / Texture ; provenance/licence à documenter. |
| source/Open World Base.zip | 10 074 488 octets (9.61 Mio) | .zip | Non directement / Garder hors Assets / Archive source contenant le même FBX et le PNG source. |
| textures/Airpot copy.png | 4 121 437 octets (3.93 Mio) | .png | Oui techniquement / `Assets/_Plane/Textures/Airport/` / Texture ; provenance/licence à documenter. |
| textures/Grass 5.jpeg | 681 488 octets (0.65 Mio) | .jpeg | Oui techniquement / `Assets/_Plane/Textures/Airport/` / Texture ; provenance/licence à documenter. |
| textures/Road 19.jpeg | 663 568 octets (0.63 Mio) | .jpeg | Oui techniquement / `Assets/_Plane/Textures/Airport/` / Texture ; provenance/licence à documenter. |
| textures/Tile 44.jpeg | 428 133 octets (0.41 Mio) | .jpeg | Oui techniquement / `Assets/_Plane/Textures/Airport/` / Texture ; provenance/licence à documenter. |

**Modèles Unity présents :** `source/Open World Base/Airport.fbx` uniquement ; aucun OBJ, GLB, BLEND ou MAX dans l’arborescence ni dans le ZIP imbriqué. Aucun `.max`/`.blend` inutilisable à signaler ici.

**Textures :** dossier dédié `textures/` (un PNG et trois JPEG), plus `source/Open World Base/Airpot copy.png`. Les deux PNG portant le même nom ne sont pas identiques (tailles et SHA-256 différents) ; ne pas les écraser par simple correspondance de nom. Aucun fichier licence/readme trouvé dans `airport` ni dans son ZIP.

| Entrée dans `source/Open World Base.zip` | Taille décompressée | Utilisable ? / Où le ranger / Remarques |
| --- | --- | --- |
| Airport.fbx | 6 123 024 octets (5.84 Mio) | Déjà présent dans le dossier extrait ; aucun besoin de copie pour cet inventaire. |
| Airpot copy.png | 4 126 376 octets (3.94 Mio) | Déjà présent dans le dossier extrait ; aucun besoin de copie pour cet inventaire. |

## 3. Archive Kenney Racing Kit

Archive : **6 082 755 octets (5.80 Mio)**. Liste `unzip -l` : **1274 entrées**, dossiers inclus ; **18 286 592 octets (17.44 Mio)** décompressés. Aucune extraction sur disque.

| Dossier exact | Modèles | Taille avec dépendances du dossier | Utilisable ? / Où le ranger / Remarques |
| --- | --- | --- | --- |
| `Models/FBX format/` | 112 × .fbx | 4 859 578 octets (4.63 Mio) | Oui ; import standard, vérifier les matériaux URP. / `Assets/_Plane/Models/Kenney/` si import futur ; STL hors Assets. |
| `Models/GLTF format/` | 112 × .glb | 1 899 872 octets (1.81 Mio) | Conditionnel ; le dossier nommé GLTF contient des GLB, importeur requis. / `Assets/_Plane/Models/Kenney/` si import futur ; STL hors Assets. |
| `Models/OBJ format/` | 112 × .obj | 2 373 586 octets (2.26 Mio) | Oui ; conserver les MTL et Textures associés. / `Assets/_Plane/Models/Kenney/` si import futur ; STL hors Assets. |
| `Models/DAE format/` | 112 × .dae | 4 717 915 octets (4.50 Mio) | Oui techniquement ; alternative Collada. / `Assets/_Plane/Models/Kenney/` si import futur ; STL hors Assets. |
| `Models/STL format/` | 112 × .stl | 1 240 408 octets (1.18 Mio) | Non directement dans le flux Unity standard ; conversion nécessaire, FBX disponible. / `Assets/_Plane/Models/Kenney/` si import futur ; STL hors Assets. |

Les cinq formats contiennent les **mêmes 112 noms de modèles**, vérifiés par comparaison des listes. FBX, OBJ et DAE ont chacun un sous-dossier `Textures/` : `checkers.png`, `net.png`, `tankco.png`. GLTF n’a pas de dossier de textures externe. Le pack propose aussi `Isometric/` et `Side/` (sprites PNG), `Preview.png`, `Sample.png`, `License.txt`, `Instructions.url`, `Kenney.url`, `Patreon.url`.

| Catégorie | Noms exacts des modèles (extensions selon le format) | Utilisable ? / Où le ranger / Remarques |
| --- | --- | --- |
| Portiques et tours de bannière | `bannerTowerGreen`, `bannerTowerRed`, `overhead`, `overheadLights`, `overheadRound`, `overheadRoundColored` | Oui via FBX/OBJ ; GLB avec importeur / `Assets/_Plane/Models/Kenney/` / Licence CC0 fournie. |
| Barrières, clôtures et rails | `barrierRed`, `barrierWall`, `barrierWhite`, `fenceCurved`, `fenceStraight`, `rail`, `railDouble` | Oui via FBX/OBJ ; GLB avec importeur / `Assets/_Plane/Models/Kenney/` / Licence CC0 fournie. |
| Autres accessoires | `billboard`, `billboardDouble_exclusive`, `billboardLow`, `billboardLower`, `camera_exclusive`, `flagCheckers`, `flagCheckersSmall`, `flagGreen`, `flagRed`, `flagTankco`, `grass`, `radarEquipment`, `ramp`, `treeLarge`, `treeSmall` | Oui via FBX/OBJ ; GLB avec importeur / `Assets/_Plane/Models/Kenney/` / Licence CC0 fournie. |
| Gradins | `grandStand`, `grandStandAwning`, `grandStandCovered`, `grandStandCoveredRound`, `grandStandRound` | Oui via FBX/OBJ ; GLB avec importeur / `Assets/_Plane/Models/Kenney/` / Licence CC0 fournie. |
| Lampadaires et feux | `lightColored`, `lightPostLarge`, `lightPostModern`, `lightPost_exclusive`, `lightRed`, `lightRedDouble` | Oui via FBX/OBJ ; GLB avec importeur / `Assets/_Plane/Models/Kenney/` / Licence CC0 fournie. |
| Garages et bureaux des stands | `pitsGarage`, `pitsGarageClosed`, `pitsGarageCorner`, `pitsOffice`, `pitsOfficeCorner`, `pitsOfficeRoof` | Oui via FBX/OBJ ; GLB avec importeur / `Assets/_Plane/Models/Kenney/` / Licence CC0 fournie. |
| Cône de signalisation | `pylon` | Oui via FBX/OBJ ; GLB avec importeur / `Assets/_Plane/Models/Kenney/` / Licence CC0 fournie. |
| Voitures | `raceCarGreen`, `raceCarOrange`, `raceCarRed`, `raceCarWhite` | Oui via FBX/OBJ ; GLB avec importeur / `Assets/_Plane/Models/Kenney/` / Licence CC0 fournie. |
| Routes, ponts et rampes routières | `roadBump`, `roadCornerBridgeLarge`, `roadCornerBridgeLarger`, `roadCornerBridgeSmall`, `roadCornerLarge`, `roadCornerLargeBorder`, `roadCornerLargeBorderInner`, `roadCornerLargeSand`, `roadCornerLargeSandInner`, `roadCornerLargeWall`, `roadCornerLargeWallInner`, `roadCornerLarger`, `roadCornerLargerBorder`, `roadCornerLargerBorderInner`, `roadCornerLargerSand`, `roadCornerLargerSandInner`, `roadCornerLargerWall`, `roadCornerLargerWallInner`, `roadCornerSmall`, `roadCornerSmallBorder`, `roadCornerSmallSand`, `roadCornerSmallSquare`, `roadCornerSmallWall`, `roadCrossing`, `roadCurved`, `roadCurvedSplit`, `roadEnd`, `roadPitEntry`, `roadPitGarage`, `roadPitStraight`, `roadPitStraightLong`, `roadRamp`, `roadRampLong`, `roadRampLongCurved`, `roadRampLongCurvedWall`, `roadRampLongWall`, `roadRampWall`, `roadSide`, `roadSplit`, `roadSplitLarge`, `roadSplitLarger`, `roadSplitRound`, `roadSplitRoundLarge`, `roadSplitSmall`, `roadStart`, `roadStartPositions`, `roadStraight`, `roadStraightArrow`, `roadStraightBridge`, `roadStraightBridgeMid`, `roadStraightBridgeStart`, `roadStraightLong`, `roadStraightLongBump`, `roadStraightLongBumpRound`, `roadStraightLongMid`, `roadStraightSkew` | Oui via FBX/OBJ ; GLB avec importeur / `Assets/_Plane/Models/Kenney/` / Licence CC0 fournie. |
| Tentes | `tent`, `tentClosed`, `tentClosedLong`, `tentLong`, `tentRoof`, `tentRoofDouble` | Oui via FBX/OBJ ; GLB avec importeur / `Assets/_Plane/Models/Kenney/` / Licence CC0 fournie. |

### Licence locale et liens fournis

`License.txt`, contenu intégral (texte conservé ; fins de ligne CRLF affichées comme sauts de ligne) :

```text
	

	Racing Kit (2.0)

	Created/distributed by Kenney (www.kenney.nl)
	Creation date: 07-11-2018 00:26

			------------------------------

	License: (Creative Commons Zero, CC0)
	http://creativecommons.org/publicdomain/zero/1.0/

	This content is free to use in personal, educational and commercial projects.

	Support us by crediting Kenney or www.kenney.nl (this is not mandatory)

			------------------------------

	Donate:   http://support.kenney.nl
	Request:  http://request.kenney.nl
	Patreon:  http://patreon.com/kenney/

	Follow on Twitter for updates:
	http://twitter.com/KenneyNL
```

`Instructions.url` :

```text
[InternetShortcut]
URL=https://kenney.nl/documentation/model-kits
```

`Kenney.url` :

```text
[InternetShortcut]
URL=http://www.kenney.nl/
```

`Patreon.url` :

```text
[InternetShortcut]
URL=https://www.patreon.com/kenney/
```

La licence locale indique explicitement CC0 et les usages personnels, éducatifs et commerciaux ; attribution facultative selon ce texte. Les suffixes `_exclusive` sont des noms de fichiers ; aucune restriction supplémentaire n’apparaît dans le texte de licence fourni.

## 4. EXR / ciels HDR

| Fichier | Taille disque | Résolution | Ratio | Canaux | Compression EXR | Utilisable ? / Où le ranger / Remarques |
| --- | --- | --- | --- | --- | --- | --- |
| quarry_cloudy_4k.exr | 96 411 052 octets (91.94 Mio) | 4096 × 2048 px | 2:1 exact | A, B, G, R float32 | ZIP | Oui techniquement / `Assets/_Plane/Skyboxes/` / Panorama HDR probable ; régler import et résolution mobile, licence/source à conserver. |
| qwantani_night_puresky_4k.exr | 73 075 343 octets (69.69 Mio) | 4096 × 2048 px | 2:1 exact | B, G, R float32 | PIZ | Oui techniquement / `Assets/_Plane/Skyboxes/` / Panorama HDR probable ; régler import et résolution mobile, licence/source à conserver. |
| qwantani_noon_puresky_4k.exr | 70 185 754 octets (66.93 Mio) | 4096 × 2048 px | 2:1 exact | B, G, R float32 | PIZ | Oui techniquement / `Assets/_Plane/Skyboxes/` / Panorama HDR probable ; régler import et résolution mobile, licence/source à conserver. |
| qwantani_sunset_puresky_4k.exr | 69 677 404 octets (66.45 Mio) | 4096 × 2048 px | 2:1 exact | B, G, R float32 | PIZ | Oui techniquement / `Assets/_Plane/Skyboxes/` / Panorama HDR probable ; régler import et résolution mobile, licence/source à conserver. |

**Rapport 2:1 confirmé pour les quatre fichiers** par lecture binaire de `dataWindow` et `displayWindow` : `(0, 0, 4095, 2047)` ; pixels carrés (`pixelAspectRatio = 1`). Aucun attribut EXR `envmap` n’est présent. Les références HDRI Poly Haven correspondantes sont des **panoramas équirectangulaires 360° × 180°**, selon la [FAQ officielle du fournisseur](https://docs.polyhaven.com/en/faq?q=commercial). L’identification des fichiers locaux à ces références est une inférence fondée sur leurs noms, leurs métadonnées de provenance et leurs dimensions : la projection équirectangulaire est donc très probable, mais elle n’est pas certifiée par l’en-tête seul. Aucun rendu des pixels n’a été effectué.

Les métadonnées des Qwantani référencent un chemin `Poly Haven Dropbox` et `pureskies.blend` ; celles de Quarry référencent `Poly Haven Dropbox` et `Quarry_Cloudy`. Les fiches officielles correspondantes confirment la nature HDRI : [nuit](https://polyhaven.com/a/qwantani_night_puresky), [midi](https://polyhaven.com/a/qwantani_noon_puresky), [coucher de soleil](https://polyhaven.com/a/qwantani_sunset_puresky), [carrière nuageuse](https://polyhaven.com/a/quarry_cloudy). La [licence officielle Poly Haven](https://polyhaven.com/license) annonce CC0 pour ses assets ; la provenance des fichiers locaux est cohérente avec ces sources, sans vérification de hash contre le serveur. Aucun fichier de licence compagnon n’a été inspecté pour ces EXR isolés.

Total EXR : **309 349 553 octets (295.02 Mio)** sur disque. Avant compression GPU et mipmaps, un tampon float32 de cette résolution représente 96 Mio pour RGB, 128 Mio pour RGBA ; les quatre images représentent 416 Mio en float32 brut. Ce ne sont pas des mesures de mémoire Unity après import.

## 5. Fichiers créés et étapes manuelles

- Dans le dépôt : `docs/asset_inventory.md` uniquement. Aucun fichier du jeu modifié.
- Hors dépôt : `/tmp/sky_courier_glb_inventory.py`, `/tmp/sky_courier_build_report.py` et une sortie temporaire initiale `/tmp/sky_courier_glb_results.jsonl` (remplacée par les mesures finales corrigées lors de la génération du rapport).
- Éditeur Unity : **aucune étape manuelle nécessaire pour cet inventaire**. Les chemins et options d’import sont des indications pour une tâche future, pas des actions exécutées.

## 6. Problèmes à résoudre

4. **Textures GLB volumineuses :** PNG embarqués dominants, notamment sapin, hangar et rochers ; taille sur disque distincte du coût GPU. Compression et résolution à décider lors d’une future intégration.
5. **EXR lourds :** 66–92 Mio chacun, près de 295 Mio au total ; choisir les ciels nécessaires et des résolutions/imports adaptés au mobile. Projection équirectangulaire probable, non déclarée dans les en-têtes.
6. **Import GLB non configuré :** aucun package glTF déclaré ; `model.glb` exige en plus `KHR_mesh_quantization`. Aucun package n’a été installé.
7. **Échelles et orientation :** les neuf GLB nommés ont généralement un axe maximal autour de 0,70 m, y compris hangar et tour ; harmonisation des tailles indispensable. L’axe maximal est géométrique et ne prouve pas l’axe avant du véhicule. Cible : X et Z presque égaux ; model.glb : X et Z égaux.
8. **Noms avec accents et apostrophe typographique :** noms réels `L’hélice.glb` et `L’éolienne.glb` (apostrophe U+2019 et accent combinant U+0301, forme Unicode décomposée), différents des apostrophes ASCII de la demande. Envisager plus tard `propeller.glb` et `wind_turbine.glb` ; aucun renommage effectué.
