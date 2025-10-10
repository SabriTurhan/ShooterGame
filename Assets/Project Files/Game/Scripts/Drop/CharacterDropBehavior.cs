using UnityEngine;

namespace Watermelon.SquadShooter
{
    public class CharacterDropBehavior : BaseDropBehavior
    {
        [SerializeField] CharacterData character;
        [SerializeField] int characterLevel;

        public void SetCharacterData(CharacterData character, int characterLevel)
        {
            if (character == null)
            {
                Debug.LogError("Character data is null!");

                return;
            }

            this.character = character;
            this.characterLevel = characterLevel;
        }

        /// <summary>
        /// Applies the reward for picking up the weapon card drop.
        /// </summary>
        /// <param name="autoReward">Whether to automatically reward the player.</param>
        public override void ApplyReward(bool autoReward = false)
        {
            CharacterBehaviour characterBehaviour = CharacterBehaviour.GetBehaviour();
            if (characterBehaviour != null)
            {
                CharacterStageData currentStage = character.GetStage(characterLevel);
                CharacterUpgrade currentUpgrade = character.GetUpgrade(characterLevel);

                characterBehaviour.SetGraphics(currentStage.Prefab, false, false);
                characterBehaviour.SetStats(currentUpgrade.Stats);
            }
        }
    }
}