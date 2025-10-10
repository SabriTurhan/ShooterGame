using System.Collections.Generic;
using UnityEngine;
using Watermelon.SquadShooter;

namespace Watermelon.LevelSystem
{
    public static class LevelController
    {
        public const string AREA_TEXT = "AREA {0}-{1}";

        private static LevelsDatabase levelsDatabase;
        public static LevelsDatabase LevelsDatabase => levelsDatabase;

        private static GameSettings levelSettings;
        public static GameSettings LevelSettings => levelSettings;

        private static GameObject levelGameObject;
        public static GameObject LevelGameObject => levelGameObject;

        private static GameObject backWallCollider;

        private static bool isLevelLoaded;
        private static LevelData loadedLevel;

        private static LevelSave levelSave;

        private static LevelData currentLevelData;
        public static LevelData CurrentLevelData => currentLevelData;

        private static int currentRoomIndex;

        // Player
        private static CharacterBehaviour characterBehaviour;
        private static GameObject playerObject;

        // World Data
        private static WorldData activeWorldData;

        // Gameplay
        private static bool manualExitActivation;
        private static bool isExitEntered;

        private static int lastLevelMoneyCollected;

        private static bool isGameplayActive;
        public static bool IsGameplayActive => isGameplayActive;

        private static bool needCharacterSugession;
        public static bool NeedCharacterSugession => needCharacterSugession;

        // Drop
        private static List<List<DropData>> roomRewards;
        private static List<List<DropData>> roomChestRewards;

        // Events
        public static event SimpleCallback OnPlayerExitLevelEvent;
        public static event SimpleCallback OnPlayerDiedEvent;

        public static void Init()
        {
            isLevelLoaded = false;
            isGameplayActive = false;
            isExitEntered = false;
            manualExitActivation = false;

            lastLevelMoneyCollected = 0;
            currentRoomIndex = 0;

            levelSettings = GameSettings.GetSettings();

            levelsDatabase = levelSettings.LevelsDatabase;
            levelsDatabase.Init();

            levelSave = SaveController.GetSaveObject<LevelSave>("level");

            // Store current level
            currentLevelData = levelsDatabase.GetLevel(levelSave.WorldIndex, levelSave.LevelIndex);
        }

        public static void CreateLevelObject()
        {
            levelGameObject = new GameObject("[LEVEL]");
            levelGameObject.transform.ResetGlobal();

            backWallCollider = MonoBehaviour.Instantiate(levelSettings.BackWallCollider, Vector3.forward * -1000f, Quaternion.identity, levelGameObject.transform);

            NavMeshController.Init(levelGameObject, levelSettings.NavMeshData);

            ActiveRoom.Init(levelGameObject);
        }

        public static void Unload()
        {
            // Unload active preset
            if (activeWorldData != null)
            {
                activeWorldData.UnloadWorld();
            }

            isLevelLoaded = false;
            isGameplayActive = false;
            isExitEntered = false;
            manualExitActivation = false;

            lastLevelMoneyCollected = 0;
            currentRoomIndex = 0;
        }

        public static CharacterBehaviour SpawnPlayer()
        {
            CharacterData character = CharactersController.SelectedCharacter;
            
            CharacterStageData characterStage = character.GetCurrentStage();
            CharacterUpgrade characterUpgrade = character.GetCurrentUpgrade();

            // Spawn player
            playerObject = Object.Instantiate(levelSettings.PlayerPrefab);
            playerObject.name = "[CHARACTER]";

            characterBehaviour = playerObject.GetComponent<CharacterBehaviour>();
            characterBehaviour.SetStats(characterUpgrade.Stats);
            characterBehaviour.Init();

            characterBehaviour.SetGraphics(characterStage.Prefab, false, false);

            WeaponData weapon = WeaponsController.GetCurrentWeapon();

            characterBehaviour.SetGun(weapon, weapon.GetCurrentUpgrade());

            return characterBehaviour;
        }

        public static void LoadCurrentLevel()
        {
            LoadLevel(levelSave.WorldIndex, levelSave.LevelIndex);

            SavePresets.CreateSave("Level " + (levelSave.WorldIndex + 1).ToString("00") + "-" + (levelSave.LevelIndex + 1).ToString("00"), "Levels");
        }

        public static void LoadLevel(int worldIndex, int levelIndex)
        {
            if (isLevelLoaded)
                return;

            isLevelLoaded = true;
            isExitEntered = false;

            LevelData levelData = levelsDatabase.GetLevel(worldIndex, levelIndex);

            ActiveRoom.SetLevelData(levelData);

            currentLevelData = levelData;
            currentLevelData.OnLevelInitialised();

            ActiveRoom.SetLevelData(worldIndex, levelIndex);

            WorldData world = levelData.World;
            ActivateWorld(world);

            BalanceController.UpdateDifficulty(false);

            lastLevelMoneyCollected = 0;

            Control.DisableMovementControl();

            UIGame uiGame = UIController.GetPage<UIGame>();
            uiGame.UpdateCoinsText(CurrencyController.Get(CurrencyType.Coins) + lastLevelMoneyCollected);
            uiGame.InitRoomsUI(levelData.Rooms);

            currentRoomIndex = 0;
            DistributeRewardBetweenRooms();

            // Load first room
            LoadRoom(currentRoomIndex);

            characterBehaviour.DisableAgent();
        }

        public static void ActivateLevel(SimpleCallback completeCallback = null)
        {
            EnemyController.OnLevelWillBeStarted();

            GameController.PlayCustomMusic(activeWorldData.UniqueWorldMusicClip);

            isGameplayActive = true;

            CameraController.EnableCamera(CameraType.Gameplay);

            currentRoomIndex = 0;
            lastLevelMoneyCollected = 0;

            UIGame uiGame = UIController.GetPage<UIGame>();
            uiGame.UpdateCoinsText(CurrencyController.Get(CurrencyType.Coins) + lastLevelMoneyCollected);

            characterBehaviour.SetPosition(CurrentLevelData.Rooms[currentRoomIndex].SpawnPoint);

            NavMeshController.InvokeOrSubscribe(new NavMeshCallback(delegate
            {
                characterBehaviour.Activate();
                characterBehaviour.ActivateMovement();
                characterBehaviour.ActivateAgent(); 
                
                ActiveRoom.ActivateEnemies();

                Control.EnableMovementControl();

                currentLevelData.OnLevelStarted();

                UIGamepadButton.DisableAllTags();
                UIGamepadButton.EnableTag(UIGamepadButtonTag.Game);

                completeCallback?.Invoke();
            }));
        }

        private static void DistributeRewardBetweenRooms()
        {
            int roomsAmount = currentLevelData.Rooms.Length;
            int chestsAmount = currentLevelData.GetChestsAmount();

            List<int> moneyPerRoomOrChest = new List<int>();
            DropData coinsReward;

            // find coins reward amount
            coinsReward = currentLevelData.DropData.Find(d => d.DropType == DropableItemType.Currency && d.CurrencyType == CurrencyType.Coins);

            if (coinsReward != null)
            {
                // split coins reward equally between all rooms and chests 
                moneyPerRoomOrChest = SplitIntEqually(coinsReward.Amount, roomsAmount + chestsAmount);
            }

            roomRewards = new List<List<DropData>>();
            roomChestRewards = new List<List<DropData>>();

            // creating reward for each room individually
            for (int i = 0; i < roomsAmount; i++)
            {
                roomRewards.Add(new List<DropData>());

                // if threre is money reward - assign this room's part
                if (moneyPerRoomOrChest.Count > 0)
                {
                    if (moneyPerRoomOrChest[i] > 0)
                    {
                        roomRewards[i].Add(new DropData() { DropType = DropableItemType.Currency, CurrencyType = CurrencyType.Coins, Amount = moneyPerRoomOrChest[i] });
                    }
                }

                // if room is last - give special reward
                if (i == roomsAmount - 1)
                {
                    for (int j = 0; j < currentLevelData.DropData.Count; j++)
                    {
                        // if it's not coins - then it's a special reward
                        if (!(currentLevelData.DropData[j].DropType == DropableItemType.Currency && currentLevelData.DropData[j].CurrencyType == CurrencyType.Coins))
                        {
                            bool skipThisReward = false;

                            // skip weapon card if weapon is already unlocked
                            if (currentLevelData.DropData[j].DropType == DropableItemType.WeaponCard && WeaponsController.IsWeaponUnlocked(currentLevelData.DropData[j].Weapon))
                            {
                                skipThisReward = true;
                            }

                            if (!skipThisReward)
                                roomRewards[i].Add(currentLevelData.DropData[j]);
                        }
                    }
                }
            }

            int chestsSpawned = 0;

            for (int i = 0; i < roomsAmount; i++)
            {
                var room = currentLevelData.Rooms[i];

                if (room.ChestEntities != null && room.ChestEntities.Length > 0)
                {
                    for (int j = 0; j < room.ChestEntities.Length; j++)
                    {
                        var chest = room.ChestEntities[j];

                        if (chest.IsInited)
                        {
                            if (chest.ChestType == LevelChestType.Standart)
                            {
                                roomChestRewards.Add(new List<DropData>()
                                {
                                    new DropData() { DropType = DropableItemType.Currency, CurrencyType = CurrencyType.Coins, Amount = moneyPerRoomOrChest[roomsAmount + chestsSpawned] }
                                });

                                chestsSpawned++;
                            }
                            else
                            {
                                roomChestRewards.Add(new List<DropData>()
                                {
                                    new DropData() { DropType = DropableItemType.Currency, CurrencyType = CurrencyType.Coins, Amount = coinsReward.Amount }
                                });
                            }
                        }
                        else
                        {
                            roomChestRewards.Add(new List<DropData>());
                        }
                    }
                }
                else
                {
                    roomChestRewards.Add(new List<DropData>());
                }
            }
        }

        private static bool DoesNextRoomExist()
        {
            if (isLevelLoaded)
            {
                return currentLevelData.Rooms.IsInRange(currentRoomIndex + 1);
            }

            return false;
        }

        private static void LoadRoom(int index)
        {
            RoomData roomData = currentLevelData.Rooms[index];

            ActiveRoom.SetRoomData(roomData);

            Drop.OnRoomLoaded();

            backWallCollider.transform.localPosition = roomData.SpawnPoint;

            manualExitActivation = false;
            isExitEntered = false;

            // Reposition player
            characterBehaviour.SetPosition(roomData.SpawnPoint);
            characterBehaviour.Reload(index == 0);

            NavMeshController.InvokeOrSubscribe(characterBehaviour);

            ItemEntityData[] items = roomData.ItemEntities;
            if(!items.IsNullOrEmpty())
            {
                for (int i = 0; i < items.Length; i++)
                {
                    LevelItem itemData = activeWorldData.GetLevelItem(items[i].Hash);

                    if (itemData == null)
                    {
                        Debug.Log("[Level Controller] Not found item with hash: " + items[i].Hash + " for the world: " + activeWorldData.name);
                        continue;
                    }

                    ActiveRoom.SpawnItem(itemData, items[i]);
                }
            }

            EnemyEntityData[] enemies = roomData.EnemyEntities;
            if(!enemies.IsNullOrEmpty())
            {
                for (int i = 0; i < enemies.Length; i++)
                {
                    ActiveRoom.SpawnEnemy(EnemyController.Database.GetEnemyData(enemies[i].EnemyType), enemies[i], false);
                }
            }

            if (!roomData.ChestEntities.IsNullOrEmpty())
            {
                for (int i = 0; i < roomData.ChestEntities.Length; i++)
                {
                    var chest = roomData.ChestEntities[i];

                    if (chest.IsInited)
                    {
                        ActiveRoom.SpawnChest(chest, LevelSettings.GetChestData(chest.ChestType));
                    }
                }
            }

            CustomObjectData[] roomCustomObjects = roomData.RoomCustomObjects;
            if(!roomCustomObjects.IsNullOrEmpty())
            {
                for (int i = 0; i < roomCustomObjects.Length; i++)
                {
                    ActiveRoom.SpawnCustomObject(roomCustomObjects[i]);
                }
            }

            CustomObjectData[] worldCustomObjects = levelsDatabase.GetWorld(levelSave.WorldIndex).WorldCustomObjects;
            if(!worldCustomObjects.IsNullOrEmpty())
            {
                for (int i = 0; i < worldCustomObjects.Length; i++)
                {
                    ActiveRoom.SpawnCustomObject(worldCustomObjects[i]);
                }
            }

            ActiveRoom.InitDrop(roomRewards[index], roomChestRewards[index]);

            currentLevelData.OnLevelLoaded();
            currentLevelData.OnRoomEntered();

            loadedLevel = currentLevelData;

            NavMeshController.RecalculateNavMesh(null);

            GameLoading.MarkAsReadyToHide();
        }

        public static void ReviveCharacter()
        {
            characterBehaviour.SetPosition(CurrentLevelData.Rooms[currentRoomIndex].SpawnPoint);

            isGameplayActive = true;

            characterBehaviour.Reload();
            characterBehaviour.Activate(false);
            characterBehaviour.SetPosition(CurrentLevelData.Rooms[currentRoomIndex].SpawnPoint);
            characterBehaviour.ResetDetector();

            if (levelSettings.InvulnerabilityAfrerReviveDuration > 0) characterBehaviour.MakeInvulnerable(levelSettings.InvulnerabilityAfrerReviveDuration);

            Control.EnableMovementControl();
        }

        public static void OnLevelFailed()
        {
            currentLevelData.OnLevelFailed();
        }

        public static void ReloadRoom()
        {
            if (!isLevelLoaded)
                return;

            NavMeshController.ClearAgents();

            characterBehaviour.Disable();
            characterBehaviour.Reload();

            // Remove all enemies
            ActiveRoom.ClearEnemies();

            Drop.OnRoomLoaded();

            isExitEntered = false;

            currentRoomIndex = 0;

            UIGame uiGame = UIController.GetPage<UIGame>();
            uiGame.UpdateReachedRoomUI(currentRoomIndex);

            RoomData roomData = currentLevelData.Rooms[currentRoomIndex];

            EnemyEntityData[] enemies = roomData.EnemyEntities;
            for (int i = 0; i < enemies.Length; i++)
            {
                ActiveRoom.SpawnEnemy(EnemyController.Database.GetEnemyData(enemies[i].EnemyType), enemies[i], false);
            }

            ActiveRoom.InitDrop(roomRewards[currentRoomIndex], roomChestRewards[currentRoomIndex]);

            currentLevelData.OnRoomEntered();

            characterBehaviour.gameObject.SetActive(true);
            characterBehaviour.SetPosition(roomData.SpawnPoint);

            NavMeshController.InvokeOrSubscribe(characterBehaviour);
        }

        public static void UnloadLevel()
        {
            if (!isLevelLoaded)
                return;

            NavMeshController.Reset();

            Drop.DestroyActiveObjects();

            characterBehaviour.Disable();

            loadedLevel.OnLevelUnloaded();

            ActiveRoom.Unload();

            isLevelLoaded = false;
            isExitEntered = false;
            loadedLevel = null;
        }

        private static void ActivateWorld(WorldData data)
        {
            if (activeWorldData != null && activeWorldData.Equals(data))
                return;

            // Unload active preset
            if (activeWorldData != null)
            {
                activeWorldData.UnloadWorld();
            }

            // Get new preset from database
            activeWorldData = data;

            // Activate new preset
            activeWorldData.LoadWorld();
        }

        public static void EnableManualExitActivation()
        {
            manualExitActivation = true;
        }

        public static void ActivateExit()
        {
            if (ActiveRoom.AreAllEnemiesDead())
            {
                List<ExitPointBehaviour> exitPoints = ActiveRoom.ExitPoints;
                if (!exitPoints.IsNullOrEmpty())
                {
                    foreach (ExitPointBehaviour exitPoint in exitPoints)
                    {
                        exitPoint.OnExitActivated();
                    }
                }
                else
                {
                    AudioController.PlaySound(AudioController.AudioClips.complete);

                    LevelController.OnPlayerExitLevel();
                }

#if MODULE_HAPTIC
                Haptic.Play(Haptic.HAPTIC_MEDIUM);
#endif
            }
        }

        public static void OnPlayerExitLevel()
        {
            if (isExitEntered) return;

            isExitEntered = true;

            Drop.AutoCollect();

            OnPlayerExitLevelEvent?.Invoke();

            characterBehaviour.MoveForwardAndDisable(0.3f);

            Control.DisableMovementControl();

            currentRoomIndex++;

            currentLevelData.OnRoomLeaved();

            if (currentLevelData.Rooms.IsInRange(currentRoomIndex))
            {
                Overlay.Show(0.3f, () =>
                {
                    UIGame uiGame = UIController.GetPage<UIGame>();
                    uiGame.UpdateReachedRoomUI(currentRoomIndex);

                    ActiveRoom.Unload();

                    NavMeshController.Reset();

                    LoadRoom(currentRoomIndex);

                    NavMeshController.InvokeOrSubscribe(new NavMeshCallback(delegate
                    {
                        Control.EnableMovementControl();

                        characterBehaviour.Activate();
                        characterBehaviour.ActivateAgent();
                        ActiveRoom.ActivateEnemies();
                    }));

                    Overlay.Hide(0.3f, null);
                });
            }
            else
            {
                UIGame uiGame = UIController.GetPage<UIGame>();
                uiGame.UpdateReachedRoomUI(currentRoomIndex);

                OnLevelCompleted();
            }
        }

        public static void OnEnemyKilled(BaseEnemyBehavior enemyBehavior)
        {
            if (!manualExitActivation)
            {
                ActivateExit();
            }
        }

        public static void OnCoinPicked(int amount)
        {
            lastLevelMoneyCollected += amount;

            UIGame uiGame = UIController.GetPage<UIGame>();
            uiGame.UpdateCoinsText(CurrencyController.Get(CurrencyType.Coins) + lastLevelMoneyCollected);
        }

        public static void OnRewardedCoinPicked(int amount)
        {
            CurrencyController.Add(CurrencyType.Coins, amount);

            UIGame uiGame = UIController.GetPage<UIGame>();
            uiGame.UpdateCoinsText(CurrencyController.Get(CurrencyType.Coins) + lastLevelMoneyCollected);
        }

        public static void OnLevelCompleted()
        {
            isGameplayActive = false;

            // applying reward
            CurrencyController.Add(CurrencyType.Coins, CurrentLevelData.GetCoinsReward());

            WeaponsController.AddCards(CurrentLevelData.GetCardsReward());

            UIComplete uiComplete = UIController.GetPage<UIComplete>();
            uiComplete.UpdateExperienceLabel(currentLevelData.XPAmount);

            InitCharacterSuggestion();

            IncreaseLevelInSave();

            SaveController.MarkAsSaveIsRequired();

            GameController.LevelComplete();

            currentLevelData.OnLevelCompleted();
        }

        public static void DisableCharacterSuggestion()
        {
            needCharacterSugession = false;
        }

        private static void InitCharacterSuggestion()
        {
            if (!currentLevelData.HasCharacterSuggestion)
            {
                needCharacterSugession = false;

                return;
            }

            CharacterData lastUnlockedCharacter = CharactersController.LastUnlockedCharacter;
            CharacterData nextCharacterToUnlock = CharactersController.NextCharacterToUnlock;

            if (lastUnlockedCharacter == null || nextCharacterToUnlock == null)
            {
                needCharacterSugession = false;

                return;
            }

            int lastXpRequirement = ExperienceController.GetXpPointsRequiredForLevel(lastUnlockedCharacter.RequiredLevel);
            int nextXpRequirement = ExperienceController.GetXpPointsRequiredForLevel(nextCharacterToUnlock.RequiredLevel);

            float lastProgression = (float)(ExperienceController.ExperiencePoints - lastXpRequirement) / (nextXpRequirement - lastXpRequirement);
            float currentProgression = (float)(ExperienceController.ExperiencePoints + currentLevelData.XPAmount - lastXpRequirement) / (nextXpRequirement - lastXpRequirement);

            UICharacterSuggestion.SetData(lastProgression, currentProgression, nextCharacterToUnlock);

            needCharacterSugession = true;
        }

        private static void IncreaseLevelInSave()
        {
            if (levelsDatabase.DoesNextLevelExist(levelSave.WorldIndex, levelSave.LevelIndex))
            {
                levelSave.LevelIndex++;
            }
            else
            {
                levelSave.WorldIndex++;
                levelSave.LevelIndex = 0;
            }
        }

        public static void OnPlayerDied()
        {
            if (!IsGameplayActive)
                return;

            isGameplayActive = false;

            OnPlayerDiedEvent?.Invoke();

            Control.DisableMovementControl();

            GameController.OnLevelFailded();
        }

        public static List<int> SplitIntEqually(int value, int partsAmount)
        {
            float floatPart = (float)value / partsAmount;
            int part = Mathf.FloorToInt(floatPart);

            List<int> result = new List<int>();
            if (partsAmount > 0)
            {
                int sum = 0;

                for (int i = 0; i < partsAmount; i++)
                {
                    result.Add(part);
                    sum += part;
                }

                if (sum < value)
                {
                    result[result.Count - 1] += value - sum;
                }
            }

            return result;
        }

        #region Dev

        public static void NextLevelDev()
        {
            needCharacterSugession = false;
            IncreaseLevelInSave();
            GameController.OnLevelCompleteClosed();
        }

        public static void PrevLevelDev()
        {
            needCharacterSugession = false;
            DecreaseLevelInSaveDev();
            GameController.OnLevelCompleteClosed();
        }

        private static void DecreaseLevelInSaveDev()
        {
            levelSave.LevelIndex--;

            if (levelSave.LevelIndex < 0)
            {
                levelSave.LevelIndex = 0;

                levelSave.WorldIndex--;

                if (levelSave.WorldIndex < 0)
                {
                    levelSave.WorldIndex = 0;
                }
            }
        }

        #endregion
    }
}