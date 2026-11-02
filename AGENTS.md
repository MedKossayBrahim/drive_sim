# Consignes du projet

Ces consignes s'appliquent à l'ensemble du dépôt.

## Contexte

- Projet universitaire du cours « Interactive Development ».
- Unity **6000.6.0f1**, **Universal Render Pipeline (URP)** et package **Input System**.
- Cibles : **PC et mobile**.
- Notions couvertes : Transform ; Physics (Rigidbody, Colliders, forces, contraintes, collisions et triggers) ; Input System (clavier/souris, tactile, accéléromètre et gyroscope) ; UI (Canvas, layouts et Canvas Scaler) ; contrôle d'un agent par script.

## Périmètre de travail

- La partie de l'utilisateur est le véhicule **AVION**.
- Les camarades développent la voiture, le camion, le vélo et la gestion utilisateur dans d'autres scènes.
- Placer tout le code de la partie avion dans `Assets/_Plane/Scripts`, avec le namespace `Project.Plane`.
- Ne modifier aucun fichier hors de `Assets/_Plane` sans en informer l'utilisateur avant la modification.
- La création de ce fichier `AGENTS.md` à la racine est explicitement demandée par l'utilisateur.

## Règles techniques

- Utiliser uniquement le nouveau Input System (`UnityEngine.InputSystem`), jamais `UnityEngine.Input`.
- N'utiliser aucun asset payant ni aucun package externe ; seuls les packages Unity officiels sont autorisés.
- Écrire du code propre et commenté en français.
- Exposer les paramètres réglables dans l'Inspector avec des champs privés `[SerializeField]` et des valeurs par défaut adaptées.
- Lire les inputs dans `Update` et appliquer la physique dans `FixedUpdate` ; mémoriser les inputs nécessaires entre les deux.
- Tenir compte des cibles PC et mobile dans les contrôles et l'interface.

## Fin de chaque tâche

- Lister les fichiers créés et, le cas échéant, les fichiers modifiés.
- Lister les étapes manuelles à effectuer dans l'éditeur Unity ; indiquer explicitement lorsqu'aucune étape n'est nécessaire.
