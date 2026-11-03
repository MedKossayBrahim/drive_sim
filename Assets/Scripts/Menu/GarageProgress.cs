using System;
using UnityEngine;

namespace DriveSim.Menu
{
    // Pièces, expérience, niveaux et records de ce joueur (local + sync serveur).
    public sealed class GarageProgress
    {
        public const int ModeCount = 4;
        public const int LevelCount = 15;
        public const int PageSize = 5;
        public const int PageCount = 3;
        public const int TitleCount = 4;

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

        // Résumé synchronisé avec le compte backend (coins, level, score).
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

        public void ApplyCloud(int coins, int level, int score)
        {
            _save.coins = Mathf.Max(_save.coins, Mathf.Max(0, coins));
            level = Mathf.Clamp(level, 1, LevelCount);
            for (int mode = 0; mode < ModeCount; mode++)
                _save.unlocked[mode] = Mathf.Max(_save.unlocked[mode], level);
            if (score > CloudScore)
                _save.bestScore[0] = Mathf.Max(_save.bestScore[0], score);
            Store();
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
                if (Unlocked(mode) >= 5)
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

        public Outcome Complete(int mode, int level, int stars, int score, float time)
        {
            stars = Mathf.Clamp(stars, 1, 3);
            score = Mathf.Max(0, score);
            time = Mathf.Max(0.1f, time);
            int index = Index(mode, level);
            bool first = _save.stars[index] == 0;
            bool record = _save.bestScore[index] > 0 && score > _save.bestScore[index];
            if (score > _save.bestScore[index])
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
            int reward = 350 * stars * Mathf.Max(1, level);
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

        void Normalize()
        {
            _save.unlocked = Fit(_save.unlocked, ModeCount, 1);
            _save.stars = Fit(_save.stars, ModeCount * LevelCount, 0);
            _save.bestScore = Fit(_save.bestScore, ModeCount * LevelCount, 0);
            _save.attempts = Fit(_save.attempts, ModeCount * LevelCount, 0);
            _save.bestTime = FitFloat(_save.bestTime, ModeCount * LevelCount);
            for (int i = 0; i < ModeCount; i++)
                _save.unlocked[i] = Mathf.Clamp(_save.unlocked[i], 1, LevelCount);
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
