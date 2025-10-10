using UnityEngine;

namespace Watermelon.SquadShooter
{
    [CreateAssetMenu(fileName = "Character Data", menuName = "Data/Character System/Character Data")]
    public class CharacterData : ScriptableObject
    {
        [UniqueID]
        [SerializeField] string id;
        public string ID => id;

        [SerializeField] string characterName;
        public string CharacterName => characterName;

        [SerializeField] int requiredLevel;
        public int RequiredLevel => requiredLevel;

        [SerializeField] Sprite previewSprite;
        public Sprite PreviewSprite => previewSprite;

        [SerializeField] Sprite lockedSprite;
        public Sprite LockedSprite => lockedSprite;

        [SerializeField] GameObject dropPrefab;
        public GameObject DropPrefab => dropPrefab;

        [SerializeField] CharacterStageData[] stages;
        public CharacterStageData[] Stages => stages;

        [SerializeField] CharacterUpgrade[] upgrades;
        public CharacterUpgrade[] Upgrades => upgrades;

        private CharacterSave save;
        public CharacterSave Save => save;

        public void Init()
        {
            save = SaveController.GetSaveObject<CharacterSave>($"Character_{id}");

#if UNITY_EDITOR
            if (stages.IsNullOrEmpty())
                Debug.LogError("[Character]: Character has no stages!", this);
#endif
        }

        public CharacterStageData GetCurrentStage()
        {
            for (int i = save.UpgradeLevel; i >= 0; i--)
            {
                if (upgrades[i].ChangeStage)
                    return stages[upgrades[i].StageIndex];
            }

            return stages[0];
        }

        public CharacterStageData GetStage(int index)
        {
            index = Mathf.Clamp(index, 0, upgrades.Length - 1);

            for (int i = index; i >= 0; i--)
            {
                if (upgrades[i].ChangeStage)
                    return stages[upgrades[i].StageIndex];
            }

            return stages[0];
        }

        public int GetCurrentStageIndex()
        {
            for (int i = save.UpgradeLevel; i >= 0; i--)
            {
                if (upgrades[i].ChangeStage)
                    return i;
            }

            return 0;
        }

        public CharacterUpgrade GetUpgrade(int index)
        {
            return upgrades[Mathf.Clamp(index, 0, upgrades.Length - 1)];
        }

        public CharacterUpgrade GetCurrentUpgrade()
        {
            return upgrades[save.UpgradeLevel];
        }

        public CharacterUpgrade GetNextUpgrade()
        {
            if (upgrades.IsInRange(save.UpgradeLevel + 1))
            {
                return upgrades[save.UpgradeLevel + 1];
            }

            return null;
        }

        public int GetCurrentUpgradeIndex()
        {
            return save.UpgradeLevel;
        }

        public bool IsMaxUpgrade()
        {
            return !upgrades.IsInRange(save.UpgradeLevel + 1);
        }

        public void UpgradeCharacter()
        {
            if (upgrades.IsInRange(save.UpgradeLevel + 1))
            {
                save.UpgradeLevel += 1;

                CharactersController.OnCharacterUpgraded(this);
            }
        }

        public bool IsSelected()
        {
            return CharactersController.SelectedCharacter == this;
        }

        public bool IsUnlocked()
        {
            return ExperienceController.CurrentLevel >= requiredLevel;
        }
    }
}