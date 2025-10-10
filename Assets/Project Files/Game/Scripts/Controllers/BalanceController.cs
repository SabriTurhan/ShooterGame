using System.Linq;
using UnityEngine;
using Watermelon.LevelSystem;

namespace Watermelon.SquadShooter
{
    public class BalanceController : MonoBehaviour
    {
        private static readonly DifficultySettings DEFAULT_DIFFICULTY = new DifficultySettings("Default");

        private BalanceDebugText debugText;

        private static BalanceDatabase database;

        public static DifficultySettings CurrentDifficulty { get; private set; }
        public static int PowerRequirement { get; private set; }
        public static int CurrentGeneralPower => CharactersController.SelectedCharacter.GetCurrentUpgrade().Stats.Power + WeaponsController.GetCurrentWeapon().GetCurrentUpgrade().Power;
        public static int UpgradesDifference { get; private set; }

        public static event SimpleBoolCallback BalanceUpdated;

        public void Init(BalanceDatabase database)
        {
            BalanceController.database = database;

            CharactersController.OnCharacterUpgradedEvent += OnCharacterUpgraded;
            CharactersController.OnCharacterSelectedEvent += OnCharacterSelected;

            WeaponsController.WeaponUpgraded += OnWeaponUpgraded;
            WeaponsController.NewWeaponSelected += OnWeaponSelected;

            UpdateDifficulty(false);

            if (database.ShowDebugText)
                debugText = BalanceDebugText.Create();
        }

        private void OnDestroy()
        {
            CurrentDifficulty = DEFAULT_DIFFICULTY;
            PowerRequirement = 1;

            UpgradesDifference = 0;

            CharactersController.OnCharacterUpgradedEvent -= OnCharacterSelected;
            CharactersController.OnCharacterSelectedEvent -= OnCharacterSelected;

            WeaponsController.WeaponUpgraded -= OnWeaponUpgraded;
            WeaponsController.NewWeaponSelected -= OnWeaponSelected;

            if(debugText != null)
            {
                Destroy(debugText.gameObject);
            }
        }

        public static void UpdateDifficulty(bool highlight)
        {
            if (LevelController.CurrentLevelData == null || database.DifficultyPresets.IsNullOrEmpty() || database.IgnoreDifficulty)
            {
                CurrentDifficulty = DEFAULT_DIFFICULTY;
                PowerRequirement = 1;

                BalanceUpdated?.Invoke(highlight);

                return;
            }

            PowerRequirement = WeaponsController.GetCeilingKeyPower(LevelController.CurrentLevelData.RequiredUpg) + CharactersController.GetCeilingUpgradePower(LevelController.CurrentLevelData.RequiredUpg);

            UpgradesDifference = Mathf.RoundToInt((PowerRequirement - CurrentGeneralPower) / 6f);

            DifficultySettings tempPreset = null;
            DifficultySettings[] presets = database.DifficultyPresets.OrderBy(x => x.UpgradeDifference).ToArray();
            foreach (DifficultySettings preset in presets)
            {
                if (UpgradesDifference < preset.UpgradeDifference)
                {
                    tempPreset = preset;

                    break;
                }
            }

            // Select the last difficulty in the list
            if(tempPreset == null)
                tempPreset = presets[^1];

            CurrentDifficulty = tempPreset;

            BalanceUpdated?.Invoke(highlight);
        }

        private void OnCharacterUpgraded(CharacterData character)
        {
            UpdateDifficulty(true);
        }

        private void OnCharacterSelected(CharacterData character)
        {
            UpdateDifficulty(false);
        }

        private void OnWeaponUpgraded()
        {
            UpdateDifficulty(true);
        }

        private void OnWeaponSelected()
        {
            UpdateDifficulty(false);
        }
    }
}