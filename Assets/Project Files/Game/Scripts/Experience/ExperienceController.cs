using UnityEngine;
using Watermelon.SquadShooter;

namespace Watermelon
{
    public class ExperienceController : MonoBehaviour
    {
        private static readonly int FLOATING_TEXT_HASH = "Stars".GetHashCode();

        private static ExperienceDatabase database;

        private static ExperienceSave save;

        public static int CurrentLevel { get => save.CurrentLevel; private set => save.CurrentLevel = value; }
        public static int ExperiencePoints { get => save.CurrentExperiencePoints; private set => save.CurrentExperiencePoints = value; }
        public static int CollectedExperiencePoints { get => save.CollectedExperiencePoints; private set => save.CollectedExperiencePoints = value; }

        public static ExperienceLevelData CurrentLevelData => database.GetDataForLevel(CurrentLevel);
        public static ExperienceLevelData NextLevelData => database.GetDataForLevel(CurrentLevel + 1);

        public static event SimpleIntCallback ExperienceGained;
        public static event SimpleCallback LevelIncreased;

        public void Init(ExperienceDatabase database)
        {
            ExperienceController.database = database;

            save = SaveController.GetSaveObject<ExperienceSave>("Experience");

            database.Init();
        }

        public static void GainExperience(int amount)
        {
            CollectedExperiencePoints += amount;

            FloatingTextController.SpawnFloatingText(FLOATING_TEXT_HASH, string.Format("+{0}", amount), CharacterBehaviour.Transform.position + new Vector3(3, 6, 0), Quaternion.identity, 1.0f, Color.white);
        }

        public static void ApplyExperience()
        {
            if (CollectedExperiencePoints <= 0) return;

            int collectedPoints = CollectedExperiencePoints;

            ExperiencePoints += collectedPoints;

            CollectedExperiencePoints = 0;

            // new level reached
            if (ExperiencePoints >= NextLevelData.ExperienceRequired)
            {
                // new level reached
                CurrentLevel++;

                LevelIncreased?.Invoke();
            };

            ExperienceGained?.Invoke(collectedPoints);
        }

        public static int GetXpPointsRequiredForLevel(int level)
        {
            return database.GetDataForLevel(level).ExperienceRequired;
        }

        #region Development

        public static void SetLevelDev(int level)
        {
            CurrentLevel = level;
            ExperiencePoints = database.GetDataForLevel(level).ExperienceRequired;
            //instance.expUI.UpdateUI(true);
            LevelIncreased?.Invoke();
        }

        #endregion
    }
}
