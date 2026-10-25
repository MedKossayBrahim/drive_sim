using System;

namespace DriveSim.User
{
    // Profil renvoyé par le serveur après une connexion acceptée.
    public sealed class PlayerProfile
    {
        public string Id { get; }
        public string Email { get; }
        public string DisplayName { get; }

        public PlayerProfile(string id, string email, string displayName)
        {
            Id = id;
            Email = email;
            DisplayName = displayName;
        }
    }

    // État du joueur connecté. L'équipe course lit cette classe, elle n'appelle pas Face ID.
    public static class AuthSession
    {
        public static bool IsSignedIn => Player != null && !string.IsNullOrEmpty(AccessToken);
        public static PlayerProfile Player { get; private set; }
        public static string AccessToken { get; private set; }

        public static event Action SignedIn;
        public static event Action SignedOut;

        internal static void Open(PlayerProfile player, string accessToken)
        {
            Player = player;
            AccessToken = accessToken;
            SignedIn?.Invoke();
        }

        internal static void Close()
        {
            Player = null;
            AccessToken = null;
            SignedOut?.Invoke();
        }
    }
}
