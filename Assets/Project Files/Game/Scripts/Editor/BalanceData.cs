using System;
using System.IO;
using UnityEngine;

namespace Watermelon.SquadShooter
{
    // Mirror of Tools/Balance/balance.json (written by Tools/Balance/balance.py)
    [Serializable]
    public class BalanceData
    {
        public const string JSON_PATH = "Tools/Balance/balance.json";
        public const int LEVELS_PER_WORLD = 20;

        public string[] worlds;
        public WeaponJson[] weapons;
        public CharacterJson[] characters;
        public int[] xpThresholds;
        public LevelJson[] levels;

        public static BalanceData Load()
        {
            string fullPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), JSON_PATH);
            if (!File.Exists(fullPath))
            {
                Debug.LogError("[Balance] " + JSON_PATH + " not found - run python3 Tools/Balance/balance.py first");
                return null;
            }

            return JsonUtility.FromJson<BalanceData>(File.ReadAllText(fullPath));
        }

        public LevelJson GetLevel(int number) => Array.Find(levels, level => level.n == number);

        public static int LevelNumber(int worldIndex, int levelIndex) => worldIndex * LEVELS_PER_WORLD + levelIndex + 1;

        [Serializable] public class WeaponJson { public string name; public int rarity; public float baseStrength; public float dpsFactor; public int cardsToUnlock; public int unlockLevel; public StageJson[] stages; }
        [Serializable] public class StageJson { public int stage; public int price; public int[] damage; public float fireRate; public float strength; public int power; }
        [Serializable] public class CharacterJson { public string name; public float baseStrength; public int requiredLevel; public int worldUnlockLevel; public CharacterStageJson[] stages; }
        [Serializable] public class CharacterStageJson { public int stage; public int price; public int health; public float strength; public int power; }
        [Serializable] public class CardJson { public string weapon; public int amount; }
        [Serializable] public class LevelJson { public int n; public int world; public int levelInWorld; public bool boss; public int rooms; public int coins; public int xp; public CardJson[] cards; public float pressure; public float enemyHpMult; public float enemyDmgMult; public int recommendedPower; }
    }
}
