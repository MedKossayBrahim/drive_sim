# Drive Sim

Jeu de course mobile pour Android et iPhone, projet de groupe (6 personnes).

Le téléphone vérifie le visage (Face ID sur iPhone, biométrie sur Android). Un petit serveur vérifie le compte. Le visage ne quitte jamais le téléphone : le serveur reçoit un e-mail, un mot de passe, puis un jeton. Il ne reçoit ni photo ni empreinte.

Unity **6000.6.0f1**. Le rendu est l’Universal Render Pipeline.

## Où en est le projet

| Partie | État |
|---|---|
| Compte joueur : inscription, connexion, biométrie, déconnexion | En place |
| Garage 3D derrière l’écran de compte | En place |
| Serveur de comptes (`Backend`) | En place |
| Course : véhicule, piste, commandes, classement | À faire par le reste de l’équipe |

L’écran de compte se construit tout seul au lancement. Il n’y a pas de scène « Login » à remplir à la main. La seule scène du projet est `Assets/Scenes/SampleScene.unity` (caméra, lumière, volume). Dans Cursor, les fichiers `.unity` et `.meta` sont masqués par `.vscode/settings.json`. Ils sont visibles dans Unity et dans le Finder.

Quand l’écran affiche « Bonjour », le joueur est connecté. La course peut alors lire son profil. Elle n’appelle pas Face ID.

## Lancer le projet

1. Installer [Node.js 18 ou plus récent](https://nodejs.org) (bouton LTS).
2. Installer Unity Hub, puis l’éditeur **6000.6.0f1** (version écrite dans `ProjectSettings/ProjectVersion.txt`).
3. Dans Unity Hub : **Open**, et choisir le dossier qui contient `Assets` et `ProjectSettings`. Ne pas ouvrir le dossier `Assets` tout seul.
4. La première ouverture peut prendre plusieurs minutes. Si une fenêtre « URP Empty Template » apparaît, la fermer.

Depuis la racine du projet, dans un terminal :

```bash
npm start
```

La première fois, si le serveur ne démarre pas, installer ses bibliothèques puis relancer :

```bash
cd Backend
npm install
cd ..
npm start
```

Le terminal doit afficher :

```text
Serveur Drive Sim prêt.
  Sur cet ordinateur : http://localhost:5080
  Sur le téléphone    : http://192.168.x.x:5080
```

Laisser cette fenêtre ouverte. `Ctrl + C` arrête le serveur. Si le système demande d’autoriser les connexions entrantes pour `node`, accepter : le téléphone en a besoin.

Dans Unity, onglet **Game**, triangle **Play**. Le texte doit devenir « Serveur prêt ».

## Tester le compte sur l’ordinateur

L’éditeur Unity ne montre pas Face ID. Les boutons **Simuler la biométrie**, **Biométrie réussie** et **Biométrie refusée** remplacent la fenêtre du téléphone.

1. Onglet **Game**, puis **Play**.
2. **Créer un compte**. Exemple : pseudo `Pilote`, e-mail `pilote@esprit.tn`, mot de passe `secret123`.
3. **Créer le compte**, puis **Activer**.
4. L’écran affiche « Bonjour Pilote ». Le terminal montre `POST /auth/register` puis `GET /users/me`.
5. Arrêter Play, le relancer, **Simuler la biométrie**, puis **Biométrie réussie**. La session revient sans mot de passe.
6. **Se déconnecter**.

`200` dans le terminal signifie que le serveur a accepté. `401` signifie un refus (mot de passe ou session).

`Backend/data/users.json` contient l’e-mail, le pseudo, un mot de passe illisible et des jetons illisibles. Il n’y a pas de photo. Ce fichier est ignoré par Git.

## Pour l’équipe course

Après la connexion, ces données sont disponibles :

```csharp
using DriveSim.User;
using UnityEngine;

public class ExempleCourse : MonoBehaviour
{
    void Start()
    {
        if (!AuthSession.IsSignedIn)
            return;

        Debug.Log(AuthSession.Player.DisplayName);
        Debug.Log(AuthSession.Player.Id);
    }
}
```

| Accès | Usage |
|---|---|
| `AuthSession.IsSignedIn` | Vrai quand l’écran « Bonjour » est atteint |
| `AuthSession.Player.Id` | Identifiant stable, pour une sauvegarde ou un classement |
| `AuthSession.Player.DisplayName` | Pseudo affiché |
| `AuthSession.Player.Email` | E-mail du compte |
| `AuthSession.AccessToken` | Jeton à envoyer au serveur pour une future requête protégée |
| `AuthSession.SignedIn` / `SignedOut` | Événements de connexion et de déconnexion |

Les scripts du compte sont dans `Assets/Scripts/User`. Les nouveaux scripts de course peuvent vivre à côté, dans un autre dossier sous `Assets/Scripts`.

## Installer sur un téléphone

Le téléphone et l’ordinateur doivent être sur le même Wi-Fi. Avant le build, ouvrir `Assets/Scripts/User/AuthConfig.cs` et mettre dans `PhoneServerUrl` l’adresse affichée par `npm start`, par exemple `http://192.168.10.58:5080`. Cette adresse change si le Wi-Fi change. Il faut alors la modifier et refaire le build.

Dans l’éditeur, l’application parle à `http://localhost:5080`. Sur un téléphone, `localhost` désignerait le téléphone, pas l’ordinateur.

### Android

1. Unity Hub, engrenage de l’installation, **Add modules** : **Android Build Support**, OpenJDK, Android SDK & NDK, Android SDK Command Line Tools.
2. Sur le téléphone : Paramètres, À propos, appuyer sept fois sur le numéro de build, puis activer le débogage USB.
3. Unity : **File > Build Profiles > Android > Switch Platform**, puis **Build and Run**.

La fenêtre biométrique est celle d’Android : visage, empreinte ou code, selon le téléphone. Sans biométrie configurée, le mot de passe de l’application reste disponible.

### iPhone

Face ID ne s’affiche que dans l’application installée sur l’iPhone, ou dans le simulateur. Le bouton Play de Unity ne l’installe pas.

1. Installer Xcode depuis l’App Store, et le module **iOS Build Support** dans Unity Hub.
2. Sur l’iPhone : **Réglages > Confidentialité et sécurité > Mode développeur**, l’activer, puis redémarrer.
3. Face ID doit déjà être réglé dans **Réglages > Face ID et code**.
4. Unity : **File > Build Profiles > iOS > Switch Platform**, puis **Build** dans un dossier vide, par exemple `Builds/iOS`.
5. Ouvrir le `.xcodeproj`. Brancher l’iPhone, le déverrouiller, et accepter « Faire confiance ».
6. En haut de Xcode, choisir cet iPhone.
7. **Xcode > Settings > Accounts** : ajouter un identifiant Apple. Sur la cible **Unity-iPhone**, onglet **Signing & Capabilities**, cocher **Automatically manage signing** et choisir cette équipe.
8. Si `com.esprit.drivesim` est refusé, le remplacer par un identifiant unique, dans Xcode et dans Unity : **Edit > Project Settings > Player**, identification Android et iOS.
9. Triangle **Run** de Xcode.
10. Si l’iPhone bloque l’ouverture : **Réglages > Général > VPN et gestion de l’appareil**, faire confiance à l’identifiant Apple, puis ouvrir l’icône.

Fermer l’application et la rouvrir : Face ID doit ramener l’écran « Bonjour », et le terminal doit recevoir `POST /auth/refresh`.

Simulateur : **Features > Face ID > Enrolled**, puis **Matching Face** au moment de la demande.

## Démonstration

1. Le terminal du serveur est ouvert.
2. Inscription sur le téléphone, puis « Bonjour » avec le pseudo.
3. Fermer l’application et la rouvrir : Face ID ou biométrie Android, sans mot de passe.
4. Montrer `Backend/data/users.json` : compte et jetons, aucune donnée biométrique.
5. Refuser la biométrie, puis se connecter avec le mot de passe.
6. **Se déconnecter** ramène au formulaire.

Le capteur vérifie le visage sur l’appareil. Le serveur vérifie le jeton du compte.

## Fichiers utiles

| Fichier | Rôle |
|---|---|
| `Backend/server.js` | Comptes, mot de passe, jetons. Port **5080**. |
| `Assets/Scripts/User/AuthConfig.cs` | Adresse du serveur : éditeur et téléphone. |
| `Assets/Scripts/User/AuthApiClient.cs` | Appels HTTP vers le serveur. |
| `Assets/Scripts/User/AuthSession.cs` | Joueur connecté, à lire par la course. |
| `Assets/Scripts/User/AuthScreen.cs` | Carte, champs et boutons. |
| `Assets/Scripts/User/AuthBackdrop.cs` | Garage 3D : voiture, plateau, pistes. |
| `Assets/Scripts/User/AuthFlow.cs` | Ordre des écrans. |
| `Assets/Scripts/User/DeviceAuth.cs` | Face ID, biométrie Android, ou simulation dans l’éditeur. |
| `Assets/Plugins/iOS/BiometricAuth.mm` | Face ID et trousseau iPhone. |
| `Assets/Plugins/Android/.../BiometricPlugin.java` | Fenêtre biométrique Android. |
| `Assets/Editor/IosAuthBuild.cs` | Phrase Face ID ajoutée au build iPhone. |

Le serveur expose :

| Requête | Rôle |
|---|---|
| `GET /health` | Le serveur répond |
| `POST /auth/register` | Crée le compte |
| `POST /auth/login` | E-mail et mot de passe |
| `POST /auth/refresh` | Nouvelle session après la biométrie |
| `POST /auth/logout` | Ferme les sessions du compte |
| `GET /users/me` | Profil, avec le jeton d’accès |

## Problèmes fréquents

| Ce qui s’affiche | Quoi faire |
|---|---|
| `Missing script: "start"` | La commande a été lancée au mauvais endroit. Depuis la racine du projet : `npm start`. |
| « Le serveur ne répond pas » | Le terminal doit encore afficher « Serveur Drive Sim prêt ». |
| Le téléphone ne joint pas le serveur | Même Wi-Fi. `PhoneServerUrl` dans `AuthConfig.cs` égal à l’adresse du terminal, puis nouveau build. Autoriser `node` dans le pare-feu. |
| Rien à l’écran au Play | Onglet **Game**, pas **Scene**. Dans **Game**, un format téléphone (par exemple 1080x1920) montre mieux le garage. |
| `npm` introuvable | Installer Node.js LTS, fermer le terminal, le rouvrir. |
| Le port 5080 est déjà utilisé | `Ctrl + C` dans l’ancien terminal, puis `npm start`. |
| Build Android : `compileSdk` | Dans `Assets/Plugins/Android/BiometricAuth.androidlib/build.gradle`, remplacer `35` par le numéro indiqué dans l’erreur. |
| Face ID échoue tout de suite | Code et Face ID activés sur l’iPhone. L’application doit être lancée depuis Xcode. |
| Xcode refuse la signature | Changer le Package Name pour un identifiant unique. |
| Message rouge dans la Console Unity | Le message indique le fichier et la ligne. |
