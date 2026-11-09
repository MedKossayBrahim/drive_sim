using System;

namespace DriveSim.User
{
    // Profil renvoyé par le serveur après une connexion acceptée.
    public sealed class PlayerProfile
    {
        public string Id { get; }
        public string Email { get; }
        public string DisplayName { get; }
        public int Coins { get; }
        public int Level { get; }
        public int Score { get; }
        public int BestPlane { get; }
        public int BestBoat { get; }
        public int BestCar { get; }
        public int BestBike { get; }
        public int BestSubmarine { get; }

        public PlayerProfile(string id, string email, string displayName, int coins = 0, int level = 1, int score = 0, int bestPlane = 0, int bestBoat = 0, int bestCar = 0, int bestBike = 0, int bestSubmarine = 0)
        {
            Id = id;
            Email = email;
            DisplayName = displayName;
            Coins = Math.Max(0, coins);
            Level = Math.Max(1, level);
            Score = Math.Max(0, score);
            BestPlane = Math.Max(0, bestPlane);
            BestBoat = Math.Max(0, bestBoat);
            BestCar = Math.Max(0, bestCar);
            BestBike = Math.Max(0, bestBike);
            BestSubmarine = Math.Max(0, bestSubmarine);
        }

        public PlayerProfile WithProgress(int coins, int level, int score, int bestPlane, int bestBoat, int bestCar, int bestBike, int bestSubmarine)
        {
            return new PlayerProfile(Id, Email, DisplayName, coins, level, score, bestPlane, bestBoat, bestCar, bestBike, bestSubmarine);
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

        public static void UpdateProgress(int coins, int level, int score, int bestPlane = 0, int bestBoat = 0, int bestCar = 0, int bestBike = 0, int bestSubmarine = 0)
        {
            if (Player == null) return;
            Player = Player.WithProgress(coins, level, score, bestPlane, bestBoat, bestCar, bestBike, bestSubmarine);
        }

        internal static void Close()
        {
            Player = null;
            AccessToken = null;
            SignedOut?.Invoke();
        }
    }
}
