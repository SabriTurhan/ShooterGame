using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Watermelon.LevelSystem;

namespace Watermelon.SquadShooter
{
    // Applies Tools/Balance/balance.json (written by Tools/Balance/balance.py) to the game data:
    // weapon and character upgrade tables, the XP curve, per-level rewards and difficulty, difficulty presets
    // and the rewarded-video setup.
    public static class BalanceImporter
    {
        private const string DATA = "Assets/Project Files/Data";

        [MenuItem("Tools/Balance/Import Balance")]
        public static void Import()
        {
            BalanceData balance = BalanceData.Load();
            if (balance == null) return;

            Dictionary<string, WeaponData> weapons = ImportWeapons(balance);
            ImportCharacters(balance);
            ImportExperience(balance);
            ImportLevels(balance, weapons);
            ImportDifficultyPresets();
            EnableRewardedVideo();

            AssetDatabase.SaveAssets();
            Debug.Log("[Balance] imported " + BalanceData.JSON_PATH);
        }

        private static Dictionary<string, WeaponData> ImportWeapons(BalanceData balance)
        {
            Dictionary<string, WeaponData> result = new Dictionary<string, WeaponData>();

            foreach (WeaponData weapon in LoadAll<WeaponData>())
            {
                BalanceData.WeaponJson data = Array.Find(balance.weapons, w => string.Equals(w.name, weapon.WeaponName, StringComparison.OrdinalIgnoreCase));
                if (data == null)
                {
                    Debug.LogWarning("[Balance] no balance entry for weapon " + weapon.WeaponName);
                    continue;
                }

                result[data.name] = weapon;

                SerializedObject serialized = new SerializedObject(weapon);
                serialized.FindProperty("enemyHpScale").floatValue = 1f / data.baseStrength;

                // index 0 is the locked state and index 1 the first owned stage; both cost the unlock cards
                SerializedProperty upgrades = serialized.FindProperty("upgrades");
                Resize(upgrades, data.stages.Length + 1);

                for (int i = 0; i < upgrades.arraySize; i++)
                {
                    BalanceData.StageJson stage = data.stages[Mathf.Max(0, i - 1)];
                    SerializedProperty upgrade = upgrades.GetArrayElementAtIndex(i);

                    upgrade.FindPropertyRelative("price").intValue = stage.price;
                    upgrade.FindPropertyRelative("damage.firstValue").intValue = stage.damage[0];
                    upgrade.FindPropertyRelative("damage.secondValue").intValue = stage.damage[1];
                    upgrade.FindPropertyRelative("fireRate").floatValue = stage.fireRate;
                    upgrade.FindPropertyRelative("power").intValue = stage.power;
                    upgrade.FindPropertyRelative("keyUpgradeNumber").intValue = data.cardsToUnlock == 0 && i <= 1 ? 0 : -1;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(weapon);
            }

            return result;
        }

        private static void ImportCharacters(BalanceData balance)
        {
            foreach (CharacterData character in LoadAll<CharacterData>())
            {
                SerializedObject serialized = new SerializedObject(character);
                string characterName = serialized.FindProperty("characterName").stringValue;

                BalanceData.CharacterJson data = Array.Find(balance.characters, c => string.Equals(c.name, characterName, StringComparison.OrdinalIgnoreCase));
                if (data == null)
                {
                    Debug.LogWarning("[Balance] no balance entry for character " + characterName);
                    continue;
                }

                serialized.FindProperty("requiredLevel").intValue = data.requiredLevel;

                SerializedProperty upgrades = serialized.FindProperty("upgrades");
                Resize(upgrades, data.stages.Length);

                for (int i = 0; i < upgrades.arraySize; i++)
                {
                    BalanceData.CharacterStageJson stage = data.stages[i];
                    SerializedProperty upgrade = upgrades.GetArrayElementAtIndex(i);

                    upgrade.FindPropertyRelative("price").intValue = stage.price;
                    upgrade.FindPropertyRelative("stats.health").intValue = stage.health;
                    upgrade.FindPropertyRelative("stats.power").intValue = stage.power;
                    upgrade.FindPropertyRelative("stats.keyUpgradeNumber").intValue = data.requiredLevel <= 1 && i == 0 ? 0 : -1;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(character);
            }
        }

        private static void ImportExperience(BalanceData balance)
        {
            ExperienceDatabase database = AssetDatabase.LoadAssetAtPath<ExperienceDatabase>(DATA + "/Experience System/Experience Database.asset");
            SerializedObject serialized = new SerializedObject(database);

            SerializedProperty levels = serialized.FindProperty("experienceData");
            Resize(levels, balance.xpThresholds.Length);

            for (int i = 0; i < levels.arraySize; i++)
                levels.GetArrayElementAtIndex(i).FindPropertyRelative("experienceRequired").intValue = balance.xpThresholds[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
        }

        private static void ImportLevels(BalanceData balance, Dictionary<string, WeaponData> weapons)
        {
            LevelsDatabase database = AssetDatabase.LoadAssetAtPath<LevelsDatabase>(DATA + "/Level System/Levels Database.asset");

            for (int w = 0; w < database.Worlds.Length; w++)
            {
                WorldData world = database.Worlds[w];
                SerializedObject serialized = new SerializedObject(world);
                SerializedProperty levels = serialized.FindProperty("levels");

                for (int l = 0; l < levels.arraySize; l++)
                {
                    int number = BalanceData.LevelNumber(w, l);
                    BalanceData.LevelJson data = balance.GetLevel(number);
                    if (data == null) continue;

                    WriteLevelBalance(levels.GetArrayElementAtIndex(l), data, weapons);
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(world);
            }
        }

        // rewards (coins + weapon cards), XP and the difficulty curve of one level
        public static void WriteLevelBalance(SerializedProperty level, BalanceData.LevelJson data, Dictionary<string, WeaponData> weapons)
        {
            level.FindPropertyRelative("xpAmount").intValue = data.xp;
            level.FindPropertyRelative("enemyHealthMult").floatValue = data.enemyHpMult;
            level.FindPropertyRelative("enemyDamageMult").floatValue = data.enemyDmgMult;
            level.FindPropertyRelative("recommendedPower").intValue = data.recommendedPower;
            level.FindPropertyRelative("enemiesLevel").intValue = data.n;

            SerializedProperty drops = level.FindPropertyRelative("dropData");
            List<(int type, WeaponData weapon, int amount)> rewards = new List<(int, WeaponData, int)> { ((int)DropableItemType.Currency, null, data.coins) };
            foreach (BalanceData.CardJson card in data.cards)
            {
                if (weapons.TryGetValue(card.weapon, out WeaponData weapon))
                    rewards.Add(((int)DropableItemType.WeaponCard, weapon, card.amount));
            }

            drops.arraySize = rewards.Count;
            for (int i = 0; i < rewards.Count; i++)
            {
                SerializedProperty drop = drops.GetArrayElementAtIndex(i);
                drop.FindPropertyRelative("DropType").intValue = rewards[i].type;
                drop.FindPropertyRelative("CurrencyType").intValue = (int)CurrencyType.Coins;
                drop.FindPropertyRelative("Weapon").objectReferenceValue = rewards[i].weapon;
                drop.FindPropertyRelative("Amount").intValue = rewards[i].amount;
            }
        }

        public static Dictionary<string, WeaponData> WeaponsByName(BalanceData balance)
        {
            Dictionary<string, WeaponData> result = new Dictionary<string, WeaponData>();
            foreach (WeaponData weapon in LoadAll<WeaponData>())
            {
                BalanceData.WeaponJson data = Array.Find(balance.weapons, w => string.Equals(w.name, weapon.WeaponName, StringComparison.OrdinalIgnoreCase));
                if (data != null) result[data.name] = weapon;
            }

            return result;
        }

        // UpgradesDifference is the shortfall to the recommended power in 5% steps; enemies only get slightly
        // tougher for heavily under-upgraded players, the level curve itself carries the difficulty
        private static void ImportDifficultyPresets()
        {
            BalanceDatabase database = AssetDatabase.LoadAssetAtPath<BalanceDatabase>(DATA + "/Balance Database.asset");
            SerializedObject serialized = new SerializedObject(database);
            SerializedProperty presets = serialized.FindProperty("difficultyPresets");

            (string note, float health, float damage, float heal, int difference)[] values =
            {
                ("Easy", 1f, 1f, 1f, 3),
                ("Normal", 1f, 1f, 1f, 7),
                ("Medium", 1.05f, 1.05f, 1f, 13),
                ("Hard", 1.1f, 1.1f, 0.9f, 999),
            };

            presets.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                SerializedProperty preset = presets.GetArrayElementAtIndex(i);
                preset.FindPropertyRelative("note").stringValue = values[i].note;
                preset.FindPropertyRelative("healthMult").floatValue = values[i].health;
                preset.FindPropertyRelative("damageMult").floatValue = values[i].damage;
                preset.FindPropertyRelative("restoredHpMult").floatValue = values[i].heal;
                preset.FindPropertyRelative("upgradeDifference").intValue = values[i].difference;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
        }

        // Revive and rewarded chests run through rewarded video; until a real ad SDK is added the Dummy provider
        // shows a test ad. Banners and interstitials stay off.
        private static void EnableRewardedVideo()
        {
            MonetizationSettings settings = AssetDatabase.LoadAssetAtPath<MonetizationSettings>(DATA + "/Monetization Settings.asset");
            SerializedObject serialized = new SerializedObject(settings);

            serialized.FindProperty("isModuleActive").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);

            // the ad providers live in the "Ads Settings" sub-asset
            SerializedObject ads = new SerializedObject(settings.AdsSettings);
            ads.FindProperty("bannerType").intValue = (int)AdProvider.Disable;
            ads.FindProperty("interstitialType").intValue = (int)AdProvider.Disable;
            ads.FindProperty("rewardedVideoType").intValue = (int)AdProvider.Dummy;
            ads.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings.AdsSettings);
        }

        private static void Resize(SerializedProperty array, int size)
        {
            // growing duplicates the last element, which keeps prefab/sprite references of the template entries
            while (array.arraySize < size) array.InsertArrayElementAtIndex(array.arraySize - 1);
            array.arraySize = size;
        }

        private static List<T> LoadAll<T>() where T : UnityEngine.Object
        {
            List<T> assets = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { DATA }))
                assets.Add(AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)));

            return assets;
        }

    }
}
