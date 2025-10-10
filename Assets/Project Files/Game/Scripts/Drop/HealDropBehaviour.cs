using UnityEngine;

namespace Watermelon.SquadShooter
{
    /// <summary>
    /// HealDropBehaviour is responsible for applying a healing reward to the character
    /// and determining if the drop is pickable based on the character's health status.
    /// </summary>
    public class HealDropBehaviour : BaseDropBehavior
    {
        [SerializeField] int amount;

        public void SetData(int amount)
        {
            this.amount = amount;
        }

        public override void ApplyReward(bool autoReward = false)
        {
            CharacterBehaviour characterBehaviour = CharacterBehaviour.GetBehaviour();
            if (characterBehaviour != null)
            {
                characterBehaviour.Heal(amount);
            }
        }

        public override bool IsPickable(CharacterBehaviour characterBehaviour)
        {
            return !characterBehaviour.FullHealth;
        }
    }
}