using UnityEngine;

namespace Watermelon.SquadShooter
{
    public class PedestalBehavior : MonoBehaviour
    {
        [SerializeField] CharacterBehaviour characterBehaviour;

        public void Init()
        {
            CharacterData character = CharactersController.SelectedCharacter;

            CharacterStageData characterStage = character.GetCurrentStage();
            CharacterUpgrade characterUpgrade = character.GetCurrentUpgrade();

            characterBehaviour.SetStats(characterUpgrade.Stats);
            characterBehaviour.Init();
            characterBehaviour.DisableAgent();
            characterBehaviour.SetGraphics(characterStage.Prefab, false, false);

            WeaponData weapon = WeaponsController.GetCurrentWeapon();

            characterBehaviour.SetGun(weapon, weapon.GetCurrentUpgrade());
        }
    }
}