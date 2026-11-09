using System;
using UnityEngine;

namespace DriveSim.Menu
{
    // Pièces, expérience, véhicules et records de ce joueur (local + sync serveur).
    public sealed class GarageProgress
    {
        public const int ModeCount = 4;
        public const int LevelCount = 5;
        public const int PageSize = 5;
        public const int PageCount = 1;
        public const int TitleCount = 4;

        public static readonly string[] TransportNames = { "Avion", "Bateau", "Vélo", "Voiture", "Sous-marin" };
        public static readonly string[] TransportHints =
        {
            "Décollage, pièces et atterrissage.",
            "Traverse l'océan et ramasse les jetons.",
            "Roule en forêt et ramasse les pièces.",
            "Conduis en ville avec le gyroscope.",
            "Plonge et explore les fonds marins."
        };

        public static readonly string[] TitleNames = { "Recrue", "Pilote", "Flèche", "Légende" };
        public static readonly int[] TitlePrice = { 0, 800, 2200, 6000 };

        [Serializable]
        public class Save
        {
            public int coins;
            public int xp;
            public int lifetime;
            public int missions;
            public int bestStars;
            public int records;
            public int title;
            public int owned;
            public int[] unlocked;
            public int[] stars;
            public int[] bestScore;
            public int[] attempts;
            public float[] bestTime;
        }

        public struct Outcome
        {
            public bool FirstClear;
            public bool NewRecord;
            public int BestScore;
            public float BestTime;
            public int Reward;
        }

        readonly string _key;
        Save _save;

        GarageProgress(string key)
        {
            _key = key;
        }

        public static GarageProgress Load(string playerId)
        {
            var key = "drivesim.garage." + (string.IsNullOrEmpty(playerId) ? "local" : playerId);
            var progress = new GarageProgress(key);
            var json = PlayerPrefs.GetString(key, "");
            progress._save = string.IsNullOrEmpty(json) ? new Save() : JsonUtility.FromJson<Save>(json);
            if (progress._save == null)
                progress._save = new Save();
            progress.Normalize();
            return progress;
        }

        public int Coins => _save.coins;
        public int Xp => _save.xp;
        public int Lifetime => _save.lifetime;
        public int Missions => _save.missions;
        public int BestStars => _save.bestStars;
        public int Records => _save.records;
        public int Title => Mathf.Clamp(_save.title, 0, TitleCount - 1);

        public static string TransportName(int level)
        {
            int index = Mathf.Clamp(level, 1, LevelCount) - 1;
            return TransportNames[index];
        }

        public static string TransportHint(int level)
        {
            int index = Mathf.Clamp(level, 1, LevelCount) - 1;
            return TransportHints[index];
        }

        public static bool IsPlane(int level) => level == 1;
        public static bool IsBoat(int level) => level == 2;
        public static bool IsBike(int level) => level == 3;
        public static bool IsCar(int level) => level == 4;
        public static bool IsSubmarine(int level) => level == 5;
        public static bool IsPlayable(int level) => IsPlane(level) || IsBoat(level) || IsBike(level) || IsCar(level) || IsSubmarine(level);

        public int CloudLevel
        {
            get
            {
                int best = 1;
                for (int mode = 0; mode < ModeCount; mode++)
                    best = Mathf.Max(best, Unlocked(mode));
                return Mathf.Clamp(best, 1, LevelCount);
            }
        }

        public int CloudScore
        {
            get
            {
                int best = 0;
                for (int mode = 0; mode < ModeCount; mode++)
                    best = Mathf.Max(best, BestScore(mode));
                return best;
            }
        }

        public void ApplyCloud(int coins, int level, int score, int bestPlane = 0, int bestBoat = 0, int bestCar = 0, int bestBike = 0)
        {
            _save.coins = Mathf.Max(_save.coins, Mathf.Max(0, coins));
            level = Mathf.Clamp(level, 1, LevelCount);
            for (int mode = 0; mode < ModeCount; mode++)
                _save.unlocked[mode] = Mathf.Max(_save.unlocked[mode], level);
            bool legacyScore = bestPlane <= 0 && bestBoat <= 0 && bestCar <= 0 && bestBike <= 0;
            if (legacyScore && score > CloudScore)
                _save.bestScore[0] = Mathf.Max(_save.bestScore[0], score);
            KeepVehicleBest(1, bestPlane);
            KeepVehicleBest(2, bestBoat);
            KeepVehicleBest(3, bestBike);
            KeepVehicleBest(4, bestCar);
            Store();
        }

        public int BestScoreAt(int mode, int level)
        {
            return _save.bestScore[Index(mode, level)];
        }

        public int BestScoreForVehicle(int level)
        {
            int best = 0;
            for (int mode = 0; mode < ModeCount; mode++)
                best = Mathf.Max(best, BestScoreAt(mode, level));
            return best;
        }

        void KeepVehicleBest(int level, int score)
        {
            if (score <= 0)
                return;
            int index = Index(0, level);
            if (score > _save.bestScore[index])
                _save.bestScore[index] = score;
        }

        public int Unlocked(int mode)
        {
            return _save.unlocked[Mathf.Clamp(mode, 0, ModeCount - 1)];
        }

        public bool IsUnlocked(int mode, int level)
        {
            return level >= 1 && level <= LevelCount && level <= Unlocked(mode);
        }

        public int Stars(int mode, int level)
        {
            return _save.stars[Index(mode, level)];
        }

        public int Attempts(int mode, int level)
        {
            return _save.attempts[Index(mode, level)];
        }

        public int PreviewStars(int mode, int level)
        {
            return Mathf.Clamp(Attempts(mode, level) + 1, 1, 3);
        }

        public int BestScore(int mode)
        {
            int best = 0;
            int start = Mathf.Clamp(mode, 0, ModeCount - 1) * LevelCount;
            for (int i = 0; i < LevelCount; i++)
            {
                if (_save.bestScore[start + i] > best)
                    best = _save.bestScore[start + i];
            }
            return best;
        }

        public float BestTime(int mode)
        {
            float best = 0f;
            int start = Mathf.Clamp(mode, 0, ModeCount - 1) * LevelCount;
            for (int i = 0; i < LevelCount; i++)
            {
                float time = _save.bestTime[start + i];
                if (time > 0f && (best <= 0f || time < best))
                    best = time;
            }
            return best;
        }

        public bool ReachedFifth()
        {
            for (int mode = 0; mode < ModeCount; mode++)
            {
                if (Unlocked(mode) >= LevelCount)
                    return true;
            }
            return false;
        }

        public bool ClearedEveryMode()
        {
            for (int mode = 0; mode < ModeCount; mode++)
            {
                if (Stars(mode, 1) <= 0)
                    return false;
            }
            return true;
        }

        public bool Owns(int title)
        {
            if (title < 0 || title >= TitleCount)
                return false;
            return (_save.owned & (1 << title)) != 0;
        }

        public void AddCoins(int amount)
        {
            if (amount <= 0)
                return;
            _save.coins += amount;
            _save.lifetime += amount;
            Store();
        }

        public bool BuyOrEquip(int title)
        {
            if (title < 0 || title >= TitleCount)
                return false;
            if (!Owns(title))
            {
                if (_save.coins < TitlePrice[title])
                    return false;
                _save.coins -= TitlePrice[title];
                _save.owned |= 1 << title;
            }
            _save.title = title;
            Store();
            return true;
        }

        // Victoire : pièces de la course créditées, meilleur score conservé, véhicule suivant débloqué.
        public Outcome Complete(int mode, int level, int stars, int score, float time, int coinsCollected = 0)
        {
            stars = Mathf.Clamp(stars, 1, 3);
            score = Mathf.Max(0, score);
            time = Mathf.Max(0.1f, time);
            coinsCollected = Mathf.Max(0, coinsCollected);
            int index = Index(mode, level);
            bool first = _save.stars[index] == 0;
            int previousBest = _save.bestScore[index];
            bool record = previousBest > 0 && score > previousBest;
            if (score > previousBest)
                _save.bestScore[index] = score;
            if (_save.bestTime[index] <= 0f || time < _save.bestTime[index])
                _save.bestTime[index] = time;
            if (stars > _save.stars[index])
                _save.stars[index] = stars;
            if (stars > _save.bestStars)
                _save.bestStars = stars;
            int slot = Mathf.Clamp(mode, 0, ModeCount - 1);
            if (level >= _save.unlocked[slot] && level < LevelCount)
                _save.unlocked[slot] = level + 1;
            int reward = coinsCollected > 0 ? coinsCollected : 350 * stars * Mathf.Max(1, level);
            _save.coins += reward;
            _save.lifetime += reward;
            _save.xp += 20 * stars * Mathf.Max(1, level);
            _save.missions += 1;
            _save.attempts[index] += 1;
            if (record)
                _save.records += 1;
            Store();
            return new Outcome
            {
                FirstClear = first,
                NewRecord = record,
                BestScore = _save.bestScore[index],
                BestTime = _save.bestTime[index],
                Reward = reward
            };
        }

        // Échec ou abandon : conserve les pièces collectées et le meilleur score.
        public void BankRun(int mode, int level, int score, int coinsCollected)
        {
            coinsCollected = Mathf.Max(0, coinsCollected);
            score = Mathf.Max(0, score);
            if (coinsCollected > 0)
            {
                _save.coins += coinsCollected;
                _save.lifetime += coinsCollected;
            }
            int index = Index(mode, level);
            if (score > _save.bestScore[index])
                _save.bestScore[index] = score;
            _save.attempts[index] += 1;
            Store();
        }

        void Normalize()
        {
            _save.unlocked = Fit(_save.unlocked, ModeCount, 1);
            const int withCarCount = 5;
            bool dropCar = _save.stars != null && _save.stars.Length == ModeCount * withCarCount;
            if (dropCar)
            {
                _save.stars = RemapDropCar(_save.stars);
                _save.bestScore = RemapDropCar(_save.bestScore);
                _save.attempts = RemapDropCar(_save.attempts);
                _save.bestTime = RemapDropCarFloat(_save.bestTime);
                for (int i = 0; i < ModeCount; i++)
                {
                    int unlocked = _save.unlocked[i];
                    // Ancien : 1 Avion, 2 Bateau, 3 Voiture, 4 Vélo, 5 Camion.
                    if (unlocked >= 5)
                        _save.unlocked[i] = 4;
                    else if (unlocked == 4)
                        _save.unlocked[i] = 3;
                    else if (unlocked == 3)
                        _save.unlocked[i] = 4;
                }
            }
            else
            {
                _save.stars = Fit(_save.stars, ModeCount * LevelCount, 0);
                _save.bestScore = Fit(_save.bestScore, ModeCount * LevelCount, 0);
                _save.attempts = Fit(_save.attempts, ModeCount * LevelCount, 0);
                _save.bestTime = FitFloat(_save.bestTime, ModeCount * LevelCount);
            }
            for (int i = 0; i < ModeCount; i++)
                _save.unlocked[i] = Mathf.Clamp(Mathf.Max(_save.unlocked[i], LevelCount), 1, LevelCount);
            if (_save.owned == 0)
                _save.owned = 1;
            if (!Owns(_save.title))
                _save.title = 0;
        }

        int Index(int mode, int level)
        {
            mode = Mathf.Clamp(mode, 0, ModeCount - 1);
            level = Mathf.Clamp(level, 1, LevelCount);
            return mode * LevelCount + (level - 1);
        }

        void Store()
        {
            PlayerPrefs.SetString(_key, JsonUtility.ToJson(_save));
            PlayerPrefs.Save();
        }

        // Ancien ordre : Avion, Bateau, Voiture, Vélo, Camion -> Avion, Bateau, Vélo, Voiture.
        static int[] RemapDropCar(int[] source)
        {
            const int oldCount = 5;
            int[] map = { 0, 1, 3, 2, -1 };
            var data = new int[ModeCount * LevelCount];
            if (source == null)
                return data;
            for (int mode = 0; mode < ModeCount; mode++)
            {
                for (int oldLevel = 0; oldLevel < oldCount; oldLevel++)
                {
                    int dest = map[oldLevel];
                    if (dest < 0 || dest >= LevelCount)
                        continue;
                    int oldIndex = mode * oldCount + oldLevel;
                    if (oldIndex >= source.Length)
                        continue;
                    data[mode * LevelCount + dest] = source[oldIndex];
                }
            }
            return data;
        }

        static float[] RemapDropCarFloat(float[] source)
        {
            const int oldCount = 5;
            int[] map = { 0, 1, 3, 2, -1 };
            var data = new float[ModeCount * LevelCount];
            if (source == null)
                return data;
            for (int mode = 0; mode < ModeCount; mode++)
            {
                for (int oldLevel = 0; oldLevel < oldCount; oldLevel++)
                {
                    int dest = map[oldLevel];
                    if (dest < 0 || dest >= LevelCount)
                        continue;
                    int oldIndex = mode * oldCount + oldLevel;
                    if (oldIndex >= source.Length)
                        continue;
                    data[mode * LevelCount + dest] = source[oldIndex];
                }
            }
            return data;
        }

        static int[] Fit(int[] source, int size, int fill)
        {
            var data = new int[size];
            for (int i = 0; i < size; i++)
                data[i] = fill;
            if (source == null)
                return data;
            int count = Mathf.Min(size, source.Length);
            for (int i = 0; i < count; i++)
                data[i] = source[i];
            return data;
        }

        static float[] FitFloat(float[] source, int size)
        {
            var data = new float[size];
            if (source == null)
                return data;
            int count = Mathf.Min(size, source.Length);
            for (int i = 0; i < count; i++)
                data[i] = source[i];
            return data;
        }
    }
}
