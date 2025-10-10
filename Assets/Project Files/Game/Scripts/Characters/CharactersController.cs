using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Watermelon.SquadShooter
{
    public class CharactersController : MonoBehaviour
    {
        public static int BasePower { get; private set; }

        private static CharacterData selectedCharacter;
        public static CharacterData SelectedCharacter => selectedCharacter;

        public static CharacterData LastUnlockedCharacter => database.GetLastUnlockedCharacter();
        public static CharacterData NextCharacterToUnlock => database.GetNextCharacterToUnlock();

        private static CharacterGlobalSave characterSave;

        public static event CharacterCallback OnCharacterSelectedEvent;
        public static event CharacterCallback OnCharacterUpgradedEvent;

        private static List<CharacterUpgrade> keyUpgrades;

        private static CharactersDatabase database;

        public void Init(CharactersDatabase database)
        {
            CharactersController.database = database;

            // Initialise characters database
            database.Init();

            // Get global save
            characterSave = SaveController.GetSaveObject<CharacterGlobalSave>("characters");

            CharacterData saveCharacter = GetCharacter(characterSave.SelectedCharacterID);

            keyUpgrades = new List<CharacterUpgrade>();
            for (int i = 0; i < database.Characters.Length; i++)
            {
                CharacterData character = database.Characters[i];

                for (int j = 0; j < character.Upgrades.Length; j++)
                {
                    if (character.Upgrades[j].Stats.KeyUpgradeNumber != -1)
                    {
                        keyUpgrades.Add(character.Upgrades[j]);

                        if (character.Upgrades[j].Stats.KeyUpgradeNumber == 0)
                        {
                            BasePower = character.Upgrades[j].Stats.Power;
                        }
                    }
                }
            }

            keyUpgrades.OrderBy(u => u.Stats.KeyUpgradeNumber);

            // Check if character from save is unlocked
            if (IsCharacterUnlocked(saveCharacter))
            {
                // Load selected character from save
                selectedCharacter = saveCharacter;
            }
            else
            {
                // Select default character
                selectedCharacter = database.GetDefaultCharacter();
            }
        }

        private void OnDestroy()
        {
            BasePower = 0;

            selectedCharacter = null;
            characterSave = null;

            keyUpgrades = null;

            database = null;

            OnCharacterSelectedEvent = null;
            OnCharacterUpgradedEvent = null;
        }

        public static bool IsCharacterUnlocked(CharacterData character)
        {
            if (character != null)
                return character.IsUnlocked();

            return false;
        }

        public static void SelectCharacter(CharacterData character)
        {
            if (selectedCharacter == character)
                return;

            if (character != null)
            {
                selectedCharacter = character;

                characterSave.SelectedCharacterID = character.ID;

                CharacterBehaviour characterBehaviour = CharacterBehaviour.GetBehaviour();
                if(characterBehaviour != null)
                {
                    CharacterStageData characterStage = character.GetCurrentStage();
                    CharacterUpgrade characterUpgrade = character.GetCurrentUpgrade();

                    characterBehaviour.SetStats(characterUpgrade.Stats);
                    characterBehaviour.SetGraphics(characterStage.Prefab, false, false);
                }

                // Invoke select character callback
                OnCharacterSelectedEvent?.Invoke(selectedCharacter);
            }
        }

        public static void OnCharacterUpgraded(CharacterData character)
        {
            AudioController.PlaySound(AudioController.AudioClips.upgrade);

            OnCharacterUpgradedEvent?.Invoke(character);
        }

        public static CharactersDatabase GetDatabase()
        {
            return database;
        }

        public static CharacterData GetCharacter(string characterID)
        {
            return database.GetCharacter(characterID);
        }

        public static int GetCharacterIndex(CharacterData character)
        {
            return System.Array.FindIndex(database.Characters, x => x == character);
        }

        public static int GetCeilingUpgradePower(int currentKeyUpgrade)
        {
            for (int i = keyUpgrades.Count - 1; i >= 0; i--)
            {
                if (keyUpgrades[i].Stats.KeyUpgradeNumber <= currentKeyUpgrade)
                {
                    return keyUpgrades[i].Stats.Power;
                }
            }

            return keyUpgrades[0].Stats.Power;
        }

        public delegate void CharacterCallback(CharacterData character);
    }
}