using UnityEngine;

namespace Watermelon.SquadShooter
{
    public class WeaponDropBehavior : BaseDropBehavior
    {
        [SerializeField] WeaponData weapon;
        [SerializeField] int weaponLevel;

        public void SetWeaponData(WeaponData weapon, int weaponLevel)
        {
            if (weapon == null)
            {
                Debug.LogError("Weapon data is null!");

                return;
            }

            this.weapon = weapon;
            this.weaponLevel = weaponLevel;
        }

        /// <summary>
        /// Applies the reward for picking up the weapon card drop.
        /// </summary>
        /// <param name="autoReward">Whether to automatically reward the player.</param>
        public override void ApplyReward(bool autoReward = false)
        {
            CharacterBehaviour characterBehaviour = CharacterBehaviour.GetBehaviour();
            if(characterBehaviour != null)
            {
                characterBehaviour.SetGun(weapon, weapon.GetUpgrade(weaponLevel), true, true, false);
            }
        }
    }
}