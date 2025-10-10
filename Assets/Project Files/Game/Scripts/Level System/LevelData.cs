using System.Collections.Generic;
using UnityEngine;
using Watermelon.SquadShooter;

namespace Watermelon.LevelSystem
{
    [System.Serializable]
    public class LevelData
    {
        [SerializeField, LevelEditorSetting] LevelType type;
        public LevelType Type => type;

        [SerializeField, LevelEditorSetting] RoomData[] rooms;
        public RoomData[] Rooms => rooms;

        [Space]
        [SerializeField, LevelEditorSetting] int xpAmount;
        public int XPAmount => xpAmount;

        [SerializeField, LevelEditorSetting] int requiredUpg;
        public int RequiredUpg => requiredUpg;

        [SerializeField, LevelEditorSetting] int enemiesLevel;
        public int EnemiesLevel => enemiesLevel;

        [SerializeField, LevelEditorSetting] bool hasCharacterSuggestion;
        public bool HasCharacterSuggestion => hasCharacterSuggestion;

        [SerializeField, LevelEditorSetting, Range(0.0f, 1.0f)] float healSpawnPercent = 0.5f;
        public float HealSpawnPercent => healSpawnPercent;

        [SerializeField, LevelEditorSetting] List<DropData> dropData = new List<DropData>();
        public List<DropData> DropData => dropData;

        [SerializeField, LevelEditorSetting] LevelSpecialBehaviour[] specialBehaviours;
        public LevelSpecialBehaviour[] SpecialBehaviours => specialBehaviours;

        private WorldData world;
        public WorldData World => world;

        public void Init(WorldData world)
        {
            this.world = world;
        }

        #region Special Behaviours callbacks
        public void OnLevelInitialised()
        {
            for (int i = 0; i < specialBehaviours.Length; i++)
            {
                specialBehaviours[i].OnLevelInitialised();
            }
        }

        public void OnLevelLoaded()
        {
            for (int i = 0; i < specialBehaviours.Length; i++)
            {
                specialBehaviours[i].OnLevelLoaded();
            }
        }

        public void OnLevelUnloaded()
        {
            for (int i = 0; i < specialBehaviours.Length; i++)
            {
                specialBehaviours[i].OnLevelUnloaded();
            }
        }

        public void OnLevelStarted()
        {
            for (int i = 0; i < specialBehaviours.Length; i++)
            {
                specialBehaviours[i].OnLevelStarted();
            }
        }

        public void OnLevelFailed()
        {
            for (int i = 0; i < specialBehaviours.Length; i++)
            {
                specialBehaviours[i].OnLevelFailed();
            }
        }

        public void OnLevelCompleted()
        {
            for (int i = 0; i < specialBehaviours.Length; i++)
            {
                specialBehaviours[i].OnLevelCompleted();
            }
        }

        public void OnRoomEntered()
        {
            for (int i = 0; i < specialBehaviours.Length; i++)
            {
                specialBehaviours[i].OnRoomEntered();
            }
        }

        public void OnRoomLeaved()
        {
            for (int i = 0; i < specialBehaviours.Length; i++)
            {
                specialBehaviours[i].OnRoomLeaved();
            }
        }
        #endregion

        public int GetChestsAmount(bool includeRewarded = false)
        {
            int finalAmount = 0;

            for (int i = 0; i < rooms.Length; i++)
            {
                var room = rooms[i];
                if (room.ChestEntities != null)
                {
                    for (int j = 0; j < room.ChestEntities.Length; j++)
                    {
                        var chest = room.ChestEntities[j];

                        if (chest.IsInited && (includeRewarded || chest.ChestType != LevelChestType.Rewarded))
                        {
                            finalAmount++;
                        }
                    }
                }
            }

            return finalAmount;
        }

        public int GetCoinsReward()
        {
            for (int i = 0; i < dropData.Count; i++)
            {
                if (dropData[i].DropType == DropableItemType.Currency && dropData[i].CurrencyType == CurrencyType.Coins)
                    return dropData[i].Amount;
            }

            return 0;
        }

        public List<WeaponData> GetCardsReward()
        {
            List<WeaponData> result = new List<WeaponData>();

            for (int i = 0; i < dropData.Count; i++)
            {
                if (dropData[i].DropType == DropableItemType.WeaponCard)
                {
                    WeaponData weapon = dropData[i].Weapon;

                    bool isWeaponUnlocked = WeaponsController.IsWeaponUnlocked(weapon);

                    if (!isWeaponUnlocked)
                    {
                        for (int j = 0; j < dropData[i].Amount; j++)
                        {
                            result.Add(weapon);
                        }
                    }
                }
            }

            return result;
        }
    }
}