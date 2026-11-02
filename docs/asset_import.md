# Import des assets Sky Courier

Date : 25 octobre 2026. Sources conservées dans `/Users/mac/Downloads`. Copie binaire vérifiée par SHA-256 ; aucun renommage ni déplacement des sources.

## Package et configuration

- `Packages/manifest.json` : `com.unity.cloud.gltfast` fixé à **6.20.0** (version stable du registre officiel ; minimum Unity 6000.0). Projet local : 6000.6.2f1 ; compatible avec la branche 6000.6 demandée.
- [Registre officiel Unity](https://packages.unity.com/com.unity.cloud.gltfast) et [fonctionnalités glTFast 6.20](https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.20/manual/features.html) : import GLB, URP et `KHR_mesh_quantization` pris en charge.
- JSON du manifeste validé ; dépendances précédentes conservées. Package déclaré, résolution/téléchargement et compilation laissés à Unity. Aucun changement manuel de `Packages/packages-lock.json`.
- `.gitattributes` existant conservé ; ajout de GLB, règles LFS vérifiées pour EXR, GLB, FBX, PNG, JPG, JPEG, TGA, WAV, OGG, MP3. Aucune commande `git lfs track`, installation, indexation ou migration exécutée.
- `.gitignore` : ajout de `/_ImportStaging/`.

## Arborescence

`Assets/_Plane/{Models,Skyboxes,Materials,Prefabs,Scenes,Scripts,Editor,VFX,UI,Audio,Textures}`

`Models/{Plane,Airport,Props,Environment,Kenney,Checkpoint}`, `Textures/Airport` et `Models/Kenney/Textures`. Les dossiers vides sont conservés via `.gitkeep`. Aucun dossier `Art` créé.

## Copies effectuées

136 fichiers copiés : 10 GLB, 4 EXR, 112 FBX Kenney, 3 textures Kenney, 1 licence Kenney, 1 FBX et 5 images réservés au staging.

Les six fichiers du staging restent hors Assets ; les sous-dossiers source sont préservés pour garder séparées les deux images `Airpot copy.png`.

- `Assets/_Plane/Models/Plane/plane_body.glb` ← `avion.glb` — 2032464 octets ; SHA-256 `46da438bc0ff0b82996763c959c210603db7ff341682e5372addb360700f07fa`.
- `Assets/_Plane/Models/Plane/plane_propeller.glb` ← `L’hélice.glb` — 512040 octets ; SHA-256 `3cb024e65160868656c57cf551dc555360df7f6a8053bdd9cab76b14dd3ea110`.
- `Assets/_Plane/Models/Checkpoint/checkpoint_ring.glb` ← `model.glb` — 52744 octets ; SHA-256 `1e6f742a239974bd5cb7dca1eb832a1b6bd9aea5ce4e20dd53b9430b904c83ce`.
- `Assets/_Plane/Models/Props/crate.glb` ← `colis.glb` — 996104 octets ; SHA-256 `5c7e5d40bb162d1896e08d8317bd951f2cc0992ecb3dac5207ef7a1386dc2668`.
- `Assets/_Plane/Models/Props/drop_target.glb` ← `cible.glb` — 2094828 octets ; SHA-256 `768f5cd4dfad4e3f262afc4bef5f2bd17b08b5fd09e178240b4c84e3cd38d9e6`.
- `Assets/_Plane/Models/Airport/hangar.glb` ← `hangar.glb` — 8278840 octets ; SHA-256 `607362b0ff9f1d5a7091efa3563905b0488a287a61ba1d8381453ab28c01ce9b`.
- `Assets/_Plane/Models/Airport/control_tower.glb` ← `tour.glb` — 2349844 octets ; SHA-256 `caeb7d762e91c532613dfc14aa7076cae7755e83f3261654dc4991bc8ecfe338`.
- `Assets/_Plane/Models/Environment/pine_tree.glb` ← `sapin.glb` — 8938516 octets ; SHA-256 `90a9818c0ee99494f2ec49e55923f677830c7dec1e3469f12366b72a15659ecc`.
- `Assets/_Plane/Models/Environment/rocks.glb` ← `rochers.glb` — 7117936 octets ; SHA-256 `a875162e62dc1d250102e097751ee4c7c7dba5581d02444f4ed6dee3ca9e2e94`.
- `Assets/_Plane/Models/Environment/wind_turbine.glb` ← `L’éolienne.glb` — 4589620 octets ; SHA-256 `4e21f274e9d5f23950a90f2b6673e282480f95fc2f3300250cbca2dada2e39ec`.
- `Assets/_Plane/Skyboxes/sky_noon.exr` ← `qwantani_noon_puresky_4k.exr` — 70185754 octets ; SHA-256 `835553a54f8759f3f982e478757c5c3a5b18f3eb2f42661c89a3c130fd492f35`.
- `Assets/_Plane/Skyboxes/sky_sunset.exr` ← `qwantani_sunset_puresky_4k.exr` — 69677404 octets ; SHA-256 `62cdbdcc2c427a68feb08bce1ca572759d8847305bacab3a2112bb88b914f3a5`.
- `Assets/_Plane/Skyboxes/sky_night.exr` ← `qwantani_night_puresky_4k.exr` — 73075343 octets ; SHA-256 `e9044d6a2f6ee25175786c1d72179861520fd4769a7e873d40b1299c7b6da476`.
- `Assets/_Plane/Skyboxes/sky_storm.exr` ← `quarry_cloudy_4k.exr` — 96411052 octets ; SHA-256 `fd1f046b32c6f5777d5ddca33eafa329f1631fb1640e8eeb8177fdc0325ae258`.
- `Assets/_Plane/Models/Kenney/bannerTowerGreen.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/bannerTowerGreen.fbx` — 65294 octets ; SHA-256 `c5ef75c911f5f7163397ae7b53f444dcc8038929cd53a156773d041c6a72deee`.
- `Assets/_Plane/Models/Kenney/bannerTowerRed.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/bannerTowerRed.fbx` — 65268 octets ; SHA-256 `eed616e1ba6b85f35e320ed3cea94d249b129e59e1e807228ee1da3f42019f63`.
- `Assets/_Plane/Models/Kenney/barrierRed.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/barrierRed.fbx` — 13841 octets ; SHA-256 `b3fe77cb7f261746bf320d875586312cc5e67efa84916ceb6522ac061f2aa584`.
- `Assets/_Plane/Models/Kenney/barrierWall.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/barrierWall.fbx` — 18969 octets ; SHA-256 `4e50e2512530a47d002b212e1bfe0c6e9dc8f7ac1aea0e1fc8e24aec7b4604fd`.
- `Assets/_Plane/Models/Kenney/barrierWhite.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/barrierWhite.fbx` — 13859 octets ; SHA-256 `5cb7035a33523471967793c840082977c9da9539c2874672b962e4e5d866e7a1`.
- `Assets/_Plane/Models/Kenney/billboard.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/billboard.fbx` — 26565 octets ; SHA-256 `b28867932e7fee384f07bbb76897f0395e65f0d44a78fed5b53d899b1a603bf8`.
- `Assets/_Plane/Models/Kenney/billboardDouble_exclusive.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/billboardDouble_exclusive.fbx` — 26150 octets ; SHA-256 `6b305e812e74df9f3202bab7edb94b25eb39c53bacf92159e8e29d90501d1aee`.
- `Assets/_Plane/Models/Kenney/billboardLow.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/billboardLow.fbx` — 26614 octets ; SHA-256 `b90b37ee8fb51ee3ae5bdb52ea9083ac28f6f3258074b0bfc83d6d86a1e88bb0`.
- `Assets/_Plane/Models/Kenney/billboardLower.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/billboardLower.fbx` — 22275 octets ; SHA-256 `e67b8798e982e17499616fc3e6a29e113fd3bde877496c1dbb37d132f33a1ea9`.
- `Assets/_Plane/Models/Kenney/camera_exclusive.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/camera_exclusive.fbx` — 29992 octets ; SHA-256 `61a236850228310f0bc096e605ac68d26cee63f974c6848349ccafa91c4cbeb2`.
- `Assets/_Plane/Models/Kenney/fenceCurved.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/fenceCurved.fbx` — 53201 octets ; SHA-256 `2c96672116d72226fd3f5af52eb397e07b738a5bcbb20c38a632029276ff627b`.
- `Assets/_Plane/Models/Kenney/fenceStraight.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/fenceStraight.fbx` — 15425 octets ; SHA-256 `d2a6954b93278cc52925fac9bd17fe19c3483c9eb41435bdda10e2cf1d514983`.
- `Assets/_Plane/Models/Kenney/flagCheckers.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/flagCheckers.fbx` — 39905 octets ; SHA-256 `5789704e3eeb4bd1745aca1a4857b4655b1ff210ebc5b1efb74e7bea52091775`.
- `Assets/_Plane/Models/Kenney/flagCheckersSmall.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/flagCheckersSmall.fbx` — 29867 octets ; SHA-256 `6c1aa18272ce93102921f23d9ac2ac3b3a2781512c3995f39dfaf1a9b5d1af04`.
- `Assets/_Plane/Models/Kenney/flagGreen.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/flagGreen.fbx` — 43536 octets ; SHA-256 `0555f837631ac8ecc6f7e8d40823ea9a7dc0364bfe98674ce7b74dee0883c363`.
- `Assets/_Plane/Models/Kenney/flagRed.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/flagRed.fbx` — 43520 octets ; SHA-256 `6061d7d2680bf59f697cee89505a3c98bb0c62397bbc887610170db916970136`.
- `Assets/_Plane/Models/Kenney/flagTankco.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/flagTankco.fbx` — 40416 octets ; SHA-256 `74444f763fa2853db2a0c2b7ecf4247721af7ea9a79565d6d4bd63f7e771e5c5`.
- `Assets/_Plane/Models/Kenney/grandStand.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/grandStand.fbx` — 29287 octets ; SHA-256 `1d36d34fae305fd8b66c72907813ffdc4a73e9f614680ebd53bbbfd3482e4509`.
- `Assets/_Plane/Models/Kenney/grandStandAwning.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/grandStandAwning.fbx` — 54133 octets ; SHA-256 `c81abf401eca025beaa41b5201f8a163e3d45ad6ecf093267b0d7f1024de2ed7`.
- `Assets/_Plane/Models/Kenney/grandStandCovered.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/grandStandCovered.fbx` — 35490 octets ; SHA-256 `298c407da68575b4447fd0af245e90fbd33d0d505aa3ae5c066b21f1d7b1395d`.
- `Assets/_Plane/Models/Kenney/grandStandCoveredRound.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/grandStandCoveredRound.fbx` — 230431 octets ; SHA-256 `de220d429b994b534324606bd42e1fc6ca9af128cb23a6d4b56840b08b35b1b5`.
- `Assets/_Plane/Models/Kenney/grandStandRound.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/grandStandRound.fbx` — 170779 octets ; SHA-256 `bb08c507c6705115976d2c3bdb37a08722f144e5a8ca3c3902670187fbc530e4`.
- `Assets/_Plane/Models/Kenney/grass.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/grass.fbx` — 11110 octets ; SHA-256 `916874c4c4abe073788b8939aca6b4fb2f25b04c3d141d1a586466cf7b81f169`.
- `Assets/_Plane/Models/Kenney/lightColored.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/lightColored.fbx` — 44331 octets ; SHA-256 `41be1722dbbebd8be141bcf0f021a12ed5a6784f72221fc1144d53d6fff48188`.
- `Assets/_Plane/Models/Kenney/lightPostLarge.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/lightPostLarge.fbx` — 58825 octets ; SHA-256 `f912da3fbb43698c60f7bbcc67f3197c9cd8339fad0930afb455d0de5bc34d42`.
- `Assets/_Plane/Models/Kenney/lightPostModern.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/lightPostModern.fbx` — 41680 octets ; SHA-256 `e837da65bb70c05c44a29ccffa8d4762ec1f7d386dde6c751ab0ef490885c777`.
- `Assets/_Plane/Models/Kenney/lightPost_exclusive.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/lightPost_exclusive.fbx` — 42853 octets ; SHA-256 `5be8471188bf1e05629c9609c1a291d6ac8cf0c147e99dbbbad1a506ec1e0c34`.
- `Assets/_Plane/Models/Kenney/lightRed.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/lightRed.fbx` — 47134 octets ; SHA-256 `49e4aabd3d4f20cac3107a8c9a9968ba0773a286f9362865a2dde7aab77ad809`.
- `Assets/_Plane/Models/Kenney/lightRedDouble.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/lightRedDouble.fbx` — 73365 octets ; SHA-256 `0436d86311c9c57c2f94e6b1f2eed445d3d4bdc7bae7524a47bd76392180e41c`.
- `Assets/_Plane/Models/Kenney/overhead.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/overhead.fbx` — 27228 octets ; SHA-256 `aba8074b743d8167ec55551f2d16c64c458a48679ee81135fe10cb98dff36c55`.
- `Assets/_Plane/Models/Kenney/overheadLights.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/overheadLights.fbx` — 62515 octets ; SHA-256 `22f55fed587bec8a0c2789643a0113a23b55cff0ca8c90f78cecccbbd4d067ca`.
- `Assets/_Plane/Models/Kenney/overheadRound.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/overheadRound.fbx` — 25821 octets ; SHA-256 `432a58c0ea86978268ffe70abc8a851cdf9b2afbfe7b0d743b1f6e01aee3db17`.
- `Assets/_Plane/Models/Kenney/overheadRoundColored.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/overheadRoundColored.fbx` — 23826 octets ; SHA-256 `0a01359a8acbd8fe0f29525811525bb0fe55daac6ba471c118d34a0eec44f2fc`.
- `Assets/_Plane/Models/Kenney/pitsGarage.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/pitsGarage.fbx` — 24431 octets ; SHA-256 `c2ca7920db0d022bb34ebf48345a903498b737b0ed1e8eeb76f26615a4471a64`.
- `Assets/_Plane/Models/Kenney/pitsGarageClosed.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/pitsGarageClosed.fbx` — 31222 octets ; SHA-256 `f141ccbb59bfbba371908d0f58b237309c69f1f056b637cf11158d0b2f0c818f`.
- `Assets/_Plane/Models/Kenney/pitsGarageCorner.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/pitsGarageCorner.fbx` — 23902 octets ; SHA-256 `84d94e8c837cbda9e18664e27d0024b563d0314f9dd6ad2d4780510ba0c38076`.
- `Assets/_Plane/Models/Kenney/pitsOffice.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/pitsOffice.fbx` — 20819 octets ; SHA-256 `32bbd8c7888ddc872ba0d118a44ad5f488bed0bca71fd3c08a5ebd7e46508ebd`.
- `Assets/_Plane/Models/Kenney/pitsOfficeCorner.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/pitsOfficeCorner.fbx` — 25405 octets ; SHA-256 `c4d6e15db721f4afbb1e908cbe2aa0de219bb59ba94677e11d78d8f8f1712f36`.
- `Assets/_Plane/Models/Kenney/pitsOfficeRoof.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/pitsOfficeRoof.fbx` — 28540 octets ; SHA-256 `1492b15fe7dba0baced4ea0dca3972b1589dad250e8101a351015723d30a90db`.
- `Assets/_Plane/Models/Kenney/pylon.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/pylon.fbx` — 25852 octets ; SHA-256 `96263a3a6a8956037ac5156767dc235ee9ab92fe55d161011e84af39987da9c8`.
- `Assets/_Plane/Models/Kenney/raceCarGreen.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/raceCarGreen.fbx` — 271203 octets ; SHA-256 `d33fbc32e89ce6b0b15b70e0cd1b74d620d3c935887c01550d01ddb51836708d`.
- `Assets/_Plane/Models/Kenney/raceCarOrange.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/raceCarOrange.fbx` — 271206 octets ; SHA-256 `d9d7be7783fcc6fa27edd1894daad133ab0d9ef12757f21b13f055a32830fa20`.
- `Assets/_Plane/Models/Kenney/raceCarRed.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/raceCarRed.fbx` — 271190 octets ; SHA-256 `70303da49225289a02d2d3060d99701a321e535102aa71b14e5cb6a30e428929`.
- `Assets/_Plane/Models/Kenney/raceCarWhite.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/raceCarWhite.fbx` — 270833 octets ; SHA-256 `3eb5295f34c3b83b357968a14774bbb54ae63c9d1c5ee02a0b1f7cc5278362ae`.
- `Assets/_Plane/Models/Kenney/radarEquipment.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/radarEquipment.fbx` — 59687 octets ; SHA-256 `c3440a5e8804231a1bec1658ca6a0a9909eafb4918f2978db7cc65b40dbff44e`.
- `Assets/_Plane/Models/Kenney/rail.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/rail.fbx` — 18580 octets ; SHA-256 `274b180787c0ec6a1881e3c05696dc3b55657132ce220676b4c80e2620045bed`.
- `Assets/_Plane/Models/Kenney/railDouble.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/railDouble.fbx` — 23513 octets ; SHA-256 `9b6fd810a66ca3f296867487d7577d3c382292ef96e54fd5d8f3b380e786adea`.
- `Assets/_Plane/Models/Kenney/ramp.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/ramp.fbx` — 21396 octets ; SHA-256 `b8608fa6c51654f0b77d50522eeda0508a79334195cfa4ea4d3f1492912801c9`.
- `Assets/_Plane/Models/Kenney/roadBump.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadBump.fbx` — 21885 octets ; SHA-256 `b4b06f527cb19760aba5d3c932b945a95ecb77eb43279dea58207ab81aa076e6`.
- `Assets/_Plane/Models/Kenney/roadCornerBridgeLarge.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCornerBridgeLarge.fbx` — 52735 octets ; SHA-256 `3a05d90625c840b9dfedda972d7cd65c015115066a12d018c3a21e6cc3ca3b41`.
- `Assets/_Plane/Models/Kenney/roadCornerBridgeLarger.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCornerBridgeLarger.fbx` — 52900 octets ; SHA-256 `e417ab83df80e70cda37e6b5c141e8b6cfa3a63db07e2fe1335d12207ef8737b`.
- `Assets/_Plane/Models/Kenney/roadCornerBridgeSmall.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCornerBridgeSmall.fbx` — 46258 octets ; SHA-256 `2b4b5d07c1e4274ffeeacdf9ffa0081cf0756405c5d45e666c03bc8b4d7d233a`.
- `Assets/_Plane/Models/Kenney/roadCornerLarge.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCornerLarge.fbx` — 38834 octets ; SHA-256 `f66f7eccfdc489b39c614cffc94b0797c057bac56982ea84158ec590f8fedb49`.
- `Assets/_Plane/Models/Kenney/roadCornerLargeBorder.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCornerLargeBorder.fbx` — 21623 octets ; SHA-256 `5f00c7c675ebb28a2d8590e9ca7f17215528655cb95c9d908b755565715213b0`.
- `Assets/_Plane/Models/Kenney/roadCornerLargeBorderInner.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCornerLargeBorderInner.fbx` — 21757 octets ; SHA-256 `7f36614de7da65605014e58881f649b5e82f480e988e3f98fe24603b917ab076`.
- `Assets/_Plane/Models/Kenney/roadCornerLarger.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCornerLarger.fbx` — 32834 octets ; SHA-256 `c3d5813589115b618730f4b995368f994b760c1f7a715cb6d65b4ca426fea838`.
- `Assets/_Plane/Models/Kenney/roadCornerLargerBorder.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCornerLargerBorder.fbx` — 22026 octets ; SHA-256 `12f0afe883d70822aa1b348c92ebfd9588d3b3320351fc6d480102a5ec4ae2d6`.
- `Assets/_Plane/Models/Kenney/roadCornerLargerBorderInner.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCornerLargerBorderInner.fbx` — 21621 octets ; SHA-256 `b075d4ee127a53d8442e2731265b048c09a3f9462d617bb4853bf9d0a6f8bea8`.
- `Assets/_Plane/Models/Kenney/roadCornerLargerSand.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCornerLargerSand.fbx` — 12861 octets ; SHA-256 `926d2641d8df65557b6a147b51148401a9b9665628d9bec87b7ddc43ac30708a`.
- `Assets/_Plane/Models/Kenney/roadCornerLargerSandInner.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCornerLargerSandInner.fbx` — 12906 octets ; SHA-256 `c8256008bdda4b1d1ddde4e666037158c403accbb9c4b7897508dd9547a3d75e`.
- `Assets/_Plane/Models/Kenney/roadCornerLargerWall.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCornerLargerWall.fbx` — 21558 octets ; SHA-256 `7b4caa91ad92c5654ed6a5c9558be1f9fd4bb461b9be2fece0e36f7ac741b670`.
- `Assets/_Plane/Models/Kenney/roadCornerLargerWallInner.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCornerLargerWallInner.fbx` — 21599 octets ; SHA-256 `4794bc3567cebc92d1ff70d3e6d70f93de54b96d3c46c0380942199ddc6b745f`.
- `Assets/_Plane/Models/Kenney/roadCornerLargeSand.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCornerLargeSand.fbx` — 13122 octets ; SHA-256 `0ac227fb97bc8928b1eb4959b38ed40f6ffed8fd22df07522a713d7513c845d7`.
- `Assets/_Plane/Models/Kenney/roadCornerLargeSandInner.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCornerLargeSandInner.fbx` — 13158 octets ; SHA-256 `e7435b5851473c89904a2e1f5b64e8c1711459d0d31d08f889291f79aa82205e`.
- `Assets/_Plane/Models/Kenney/roadCornerLargeWall.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCornerLargeWall.fbx` — 22301 octets ; SHA-256 `1f18d8292c0b0ed96cfde8d818701787e91d959949ca9cf6b44fec547c350570`.
- `Assets/_Plane/Models/Kenney/roadCornerLargeWallInner.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCornerLargeWallInner.fbx` — 23152 octets ; SHA-256 `3315dccdb058cf308bb8b92803fd0316fd8ce52d8278c550a8db6b3c4f991850`.
- `Assets/_Plane/Models/Kenney/roadCornerSmall.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCornerSmall.fbx` — 38219 octets ; SHA-256 `5194f6f4e7d03d3886f62700f36da56c4be9c48217cd6ba218127275888cd4b5`.
- `Assets/_Plane/Models/Kenney/roadCornerSmallBorder.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCornerSmallBorder.fbx` — 21598 octets ; SHA-256 `3e38fb6a96fdacc77ee840245b2c2008e9a18d329f23d08d5184730913c35bff`.
- `Assets/_Plane/Models/Kenney/roadCornerSmallSand.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCornerSmallSand.fbx` — 12926 octets ; SHA-256 `d7dd49816275082781b2bce10b6fc7fce4a9d883aa2f1326aaf908800f0bacc9`.
- `Assets/_Plane/Models/Kenney/roadCornerSmallSquare.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCornerSmallSquare.fbx` — 41376 octets ; SHA-256 `ff7c40979ffb44b76f14db0651bc259a1e192a79a7d808995cad23914e66bfae`.
- `Assets/_Plane/Models/Kenney/roadCornerSmallWall.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCornerSmallWall.fbx` — 21481 octets ; SHA-256 `44018bf1b5cc99841354985c9e2aefcc45437ec36e1d51d2bf2f5708f427004d`.
- `Assets/_Plane/Models/Kenney/roadCrossing.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCrossing.fbx` — 47445 octets ; SHA-256 `ae3c4673f6cf928bb83165b908a14b1442e822a61230ca9da5302ac1b1f42ae4`.
- `Assets/_Plane/Models/Kenney/roadCurved.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCurved.fbx` — 69179 octets ; SHA-256 `e1cc54930ede0ca3f01161ee5e9f381c0a3d33f1e7463a5d84f8cc657f54b0d7`.
- `Assets/_Plane/Models/Kenney/roadCurvedSplit.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadCurvedSplit.fbx` — 91181 octets ; SHA-256 `dc0043ba7ab96fec68aad6b60f3198b57ab67702fdc78e89671d27d344a3361b`.
- `Assets/_Plane/Models/Kenney/roadEnd.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadEnd.fbx` — 26555 octets ; SHA-256 `6847f058bd3b2cd120b52ecaef9cc26fec3c9b0881e841a63aa7545aa15836cc`.
- `Assets/_Plane/Models/Kenney/roadPitEntry.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadPitEntry.fbx` — 16214 octets ; SHA-256 `439441defa8dd9a8399769a2036c38d7330c633a559506970be4c786f6bdc684`.
- `Assets/_Plane/Models/Kenney/roadPitGarage.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadPitGarage.fbx` — 18264 octets ; SHA-256 `98a0e8c0a7e17ae9b6bf333bd46e3e9d5f5307dde97e4903b5fbd5d609669053`.
- `Assets/_Plane/Models/Kenney/roadPitStraight.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadPitStraight.fbx` — 13467 octets ; SHA-256 `906249fb5ca36cfc53b0dba2a8fc1be52de8f2d429756bb0b9e3af635f61262d`.
- `Assets/_Plane/Models/Kenney/roadPitStraightLong.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadPitStraightLong.fbx` — 13824 octets ; SHA-256 `aeb389f7c713b82ee57122943c1450f4452fb6af0daed13c5eb78ee190b82861`.
- `Assets/_Plane/Models/Kenney/roadRamp.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadRamp.fbx` — 14237 octets ; SHA-256 `cc4efb49b543855eb3916c22bcd639faefc63e495fc7be206b169f685a71190e`.
- `Assets/_Plane/Models/Kenney/roadRampLong.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadRampLong.fbx` — 14433 octets ; SHA-256 `b0a1ab50c23438d16c458e856632fd0ad3165143bf56b85901d78b6cd5751e4a`.
- `Assets/_Plane/Models/Kenney/roadRampLongCurved.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadRampLongCurved.fbx` — 71098 octets ; SHA-256 `3d06e18ed38adb913ae9bfea8cf453b0e9cab43a37efdfd56760c46fb9900550`.
- `Assets/_Plane/Models/Kenney/roadRampLongCurvedWall.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadRampLongCurvedWall.fbx` — 76119 octets ; SHA-256 `859f8a69153f5b7b5905e5a4142c49060fd3d04e92e82850a9b3b48253ffc6d7`.
- `Assets/_Plane/Models/Kenney/roadRampLongWall.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadRampLongWall.fbx` — 15397 octets ; SHA-256 `71b7368635dcdb7e61526314e20e19d5fc9f5472c45ec1cf7f72975f97181f77`.
- `Assets/_Plane/Models/Kenney/roadRampWall.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadRampWall.fbx` — 15207 octets ; SHA-256 `47f3933f0309edb4addd1ee9dded18ff001176a4333bc9528fa5a3e5f66a72df`.
- `Assets/_Plane/Models/Kenney/roadSide.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadSide.fbx` — 18750 octets ; SHA-256 `1b0df8985f1ebb93e6522a3d3399d9497ee13c7b8202bef3821df75f805688f2`.
- `Assets/_Plane/Models/Kenney/roadSplit.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadSplit.fbx` — 16659 octets ; SHA-256 `24c1c67306c9f05a9dbf894236846f2b3d97617df3fcacc63ed78e4d0b6dc1af`.
- `Assets/_Plane/Models/Kenney/roadSplitLarge.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadSplitLarge.fbx` — 34951 octets ; SHA-256 `9ee66f8dac0e483141c446918f221d3d5cfe9cba73201afd90533d142be7c034`.
- `Assets/_Plane/Models/Kenney/roadSplitLarger.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadSplitLarger.fbx` — 42059 octets ; SHA-256 `0bd3de628d50ed510e98f7196c1c76ebba5623d3e0fa0cb7c643f23d60cd8117`.
- `Assets/_Plane/Models/Kenney/roadSplitRound.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadSplitRound.fbx` — 60291 octets ; SHA-256 `bace5e63acbe070d588be17d66e459a7a02843e9c961469455f7deec3639d004`.
- `Assets/_Plane/Models/Kenney/roadSplitRoundLarge.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadSplitRoundLarge.fbx` — 70336 octets ; SHA-256 `3bd39f827e549f16b620b6c6368bacdb0b01a8640398c362dd036ad94a880200`.
- `Assets/_Plane/Models/Kenney/roadSplitSmall.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadSplitSmall.fbx` — 29269 octets ; SHA-256 `5cc17b7d40c7ffccf250d0a97a6fa18a399357d9a7997682f68cf84ee7443425`.
- `Assets/_Plane/Models/Kenney/roadStart.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadStart.fbx` — 34054 octets ; SHA-256 `3a8f60887f7cf5816c74f9158bae3b90649f7c8921d0bdd08ea7e91af1d6b4b0`.
- `Assets/_Plane/Models/Kenney/roadStartPositions.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadStartPositions.fbx` — 17969 octets ; SHA-256 `ec246eca620f25ccd87b0ab57d6cacb9412f789cc70cbb74e106711d5744929b`.
- `Assets/_Plane/Models/Kenney/roadStraight.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadStraight.fbx` — 13697 octets ; SHA-256 `eb04b0f1115f3841021e579d9c4401b5c6d79ec60e544fe245636e8e1fe26fd8`.
- `Assets/_Plane/Models/Kenney/roadStraightArrow.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadStraightArrow.fbx` — 14861 octets ; SHA-256 `71f2453e3ca99ed8c04e145eb3e68f05bdfc261a07b6e061c55df9b2595782e6`.
- `Assets/_Plane/Models/Kenney/roadStraightBridge.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadStraightBridge.fbx` — 16110 octets ; SHA-256 `edf2c0d84a7fa27e4cd2a2c1252512570d1afb28bc1c8369323ab1de43b499e2`.
- `Assets/_Plane/Models/Kenney/roadStraightBridgeMid.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadStraightBridgeMid.fbx` — 16165 octets ; SHA-256 `cfb86f34970962f4f66ce0d5fd1c056d271afb1744bc376a0ac29edec4602f64`.
- `Assets/_Plane/Models/Kenney/roadStraightBridgeStart.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadStraightBridgeStart.fbx` — 17495 octets ; SHA-256 `21fe85f90132d5730b2109ca3a7aee718d102726e0390c46529d28c1f4b30e16`.
- `Assets/_Plane/Models/Kenney/roadStraightLong.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadStraightLong.fbx` — 13907 octets ; SHA-256 `1ffe213192fab55d6311b732d4f6cc35aa8322e20ed7f822e7ca56462dbc33c7`.
- `Assets/_Plane/Models/Kenney/roadStraightLongBump.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadStraightLongBump.fbx` — 133013 octets ; SHA-256 `665497008f449bdbd4d5fbe05f9016697e0b9a60d282efb1650266cfe977f498`.
- `Assets/_Plane/Models/Kenney/roadStraightLongBumpRound.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadStraightLongBumpRound.fbx` — 27518 octets ; SHA-256 `8cdc518da5b9702b88bbc8e5c4852abb9caaea3729f2035edd1cc2bfc9015137`.
- `Assets/_Plane/Models/Kenney/roadStraightLongMid.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadStraightLongMid.fbx` — 27213 octets ; SHA-256 `2feff2a0945eadbd65248a583ab36c58dce7d8229e9936267dfc8fd6556f929d`.
- `Assets/_Plane/Models/Kenney/roadStraightSkew.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/roadStraightSkew.fbx` — 14459 octets ; SHA-256 `77d7308c4d50af04b9676f7775f2a25389906d83bc7bed2d66f1f8ed8e3e7196`.
- `Assets/_Plane/Models/Kenney/tent.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/tent.fbx` — 18830 octets ; SHA-256 `40f558f907e41d35c1aedbb6ee2539f7d8390c240fdf90532b3661a260480f42`.
- `Assets/_Plane/Models/Kenney/tentClosed.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/tentClosed.fbx` — 21281 octets ; SHA-256 `df91a9caba603cb40f74fb68c970e45fa36e4d16abaad6fb1bd23162d5c2eb69`.
- `Assets/_Plane/Models/Kenney/tentClosedLong.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/tentClosedLong.fbx` — 21138 octets ; SHA-256 `4532e54c1c498dc7929702333a917dc45eb101abede19a9a08dba0f2da07cc73`.
- `Assets/_Plane/Models/Kenney/tentLong.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/tentLong.fbx` — 18744 octets ; SHA-256 `96e17addf01c98a6d960dd699d23c800370f0f58ce0e0afdc41e2a276ddb9660`.
- `Assets/_Plane/Models/Kenney/tentRoof.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/tentRoof.fbx` — 23233 octets ; SHA-256 `d4d86e982d58bf344a5a6b3f6397276cb26ba8d7fa7ba990e85f6e0df04d8d05`.
- `Assets/_Plane/Models/Kenney/tentRoofDouble.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/tentRoofDouble.fbx` — 33023 octets ; SHA-256 `e01928e384b79f67e27c5733e5be95e9558f6ab940b5c91f0c5a9520644eb5c7`.
- `Assets/_Plane/Models/Kenney/Textures/checkers.png` ← `kenney_racing-kit.zip!/Models/FBX format/Textures/checkers.png` — 116 octets ; SHA-256 `8eb3d9d77c1165cf2b1c68a07f808d83d6565ce3db9e930ca500cbd7296f4c4d`.
- `Assets/_Plane/Models/Kenney/Textures/net.png` ← `kenney_racing-kit.zip!/Models/FBX format/Textures/net.png` — 368 octets ; SHA-256 `c8e5d6fc69bafe6c3df797ae3175d9f1bf606bc1a9df5e12463a74087a4ea3fc`.
- `Assets/_Plane/Models/Kenney/Textures/tankco.png` ← `kenney_racing-kit.zip!/Models/FBX format/Textures/tankco.png` — 2714 octets ; SHA-256 `5355b8f2c0155034d2bc2170517ae815ad0fe659cd2fb5e195245b7c9165d9f5`.
- `Assets/_Plane/Models/Kenney/treeLarge.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/treeLarge.fbx` — 24269 octets ; SHA-256 `bfb597d71b67e633fb7ce6568a25275f90ba7b5965a5bcd322da8f9e089006be`.
- `Assets/_Plane/Models/Kenney/treeSmall.fbx` ← `kenney_racing-kit.zip!/Models/FBX format/treeSmall.fbx` — 24452 octets ; SHA-256 `ef5cb201fe9b3bd03d623a6e93854d2f27cf80a6edbb7445387e6c21a33d1334`.
- `Assets/_Plane/Models/Kenney/License.txt` ← `kenney_racing-kit.zip!/License.txt` — 611 octets ; SHA-256 `905627df4313a9eb7fa9337129e572979ef51cc3857c8d2942e57ae84736096c`.
- `_ImportStaging/airport/source/Open World Base/Airport.fbx` ← `airport/source/Open World Base/Airport.fbx` — 6123024 octets ; SHA-256 `f4de4ca01326a1b126b6a6302dbf64c9d1454bff009bf59f6bb252a6ad62b3ce`.
- `_ImportStaging/airport/source/Open World Base/Airpot copy.png` ← `airport/source/Open World Base/Airpot copy.png` — 4126376 octets ; SHA-256 `6c6f59b19702c377441354c7553f3999cb7660fc0f381220a91cafe8eaf960bb`.
- `_ImportStaging/airport/textures/Airpot copy.png` ← `airport/textures/Airpot copy.png` — 4121437 octets ; SHA-256 `c14de7381303ec435e49eb4fcb9260566ba3df0c21d728180e1cd7acb9ace484`.
- `_ImportStaging/airport/textures/Grass 5.jpeg` ← `airport/textures/Grass 5.jpeg` — 681488 octets ; SHA-256 `f3a43547fbc3550499a90daa1628fea0ca12b59c9f22dbac404071570c8fe355`.
- `_ImportStaging/airport/textures/Road 19.jpeg` ← `airport/textures/Road 19.jpeg` — 663568 octets ; SHA-256 `f197358b9c43b6a19c7b02ba7c57343e8b2642015190bc18caab3860fbffdf27`.
- `_ImportStaging/airport/textures/Tile 44.jpeg` ← `airport/textures/Tile 44.jpeg` — 428133 octets ; SHA-256 `6408d63394bad2326771ba2f9764ee29d3a7ee85292aa3047037f1ff4155c303`.

## Créations et modifications supplémentaires

- Créé : `Assets/_Plane/Models/LICENSES.txt`, avec les origines et statuts demandés ; `.gitkeep` dans les dossiers vides.
- Créé : `docs/asset_import.md` (ce journal).
- Modifiés : `Packages/manifest.json`, `.gitattributes`, `.gitignore`, `docs/asset_inventory.md` (périmètre et chemins actualisés).
- Aucun script de gameplay, prefab, scène ou matériau créé ; aucun fichier `.meta` existant touché. Unity générera les métadonnées lors de son prochain import.

## Éléments ignorés

- Kenney : OBJ, DAE, STL, GLTF, Isometric, Side, aperçus et raccourcis URL. Seuls les FBX, leur dossier Textures et License.txt sont copiés.
- airport : ZIP source et fichiers Finder ignorés ; aucun fichier du téléchargement airport dans Assets. `Textures/Airport` reste vide.
- Autres fichiers de Downloads hors sélection ignorés. Aucune autre arborescence existante du projet modifiée.

## Vérifications manuelles Unity

1. Laisser Unity terminer la résolution du package et l’import ; vérifier glTFast **6.20.0** dans Package Manager. Unity peut mettre à jour son lockfile lors de cette étape.
2. Vérifier la Console : aucune erreur de résolution, compilation, shader ou import ; ne pas masquer les erreurs.
3. Prévisualiser les 10 GLB dans l’Inspector et les glisser temporairement dans une scène : géométrie visible, textures présentes, aucun matériau rose ; vérifier spécialement `checkpoint_ring.glb` (géométrie quantifiée).
4. Vérifier les 112 FBX Kenney et leurs trois textures ; vérifier l’apparence des matériaux avec URP.
5. Vérifier tailles et orientations des modèles : les modèles générés sont souvent normalisés autour de 0,70 m ; les réglages de jeu restent à faire.
6. Vérifier les quatre EXR dans l’Inspector ; créer/configurer plus tard les matériaux Skybox/Panoramic et les résolutions mobiles selon le besoin.
7. Laisser les fichiers du staging hors Assets jusqu’à vérification de licence.

La Console et le rendu ne sont pas validés automatiquement : l’éditeur n’a pas été lancé pendant cette tâche afin de respecter le périmètre des écritures autorisées.
