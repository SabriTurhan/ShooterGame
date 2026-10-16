using UnityEngine;
using Watermelon.LevelSystem;

namespace Watermelon.SquadShooter
{
    public class EnemyController : MonoBehaviour
    {
        [SerializeField] EnemiesDatabase database;
        public static EnemiesDatabase Database => instance.database;

        private static EnemyController instance;

        public void Init()
        {
            instance = this;

            CharacterData baseCharacter = CharactersController.GetDatabase().GetDefaultCharacter();
            database.InitStatsRealation(baseCharacter.Upgrades[0].Stats.Health);
        }

        // set the level's difficulty - to be used in stats calculation for enemies that will be spawned in a moment
        public static void OnLevelWillBeStarted()
        {
            LevelData level = LevelController.CurrentLevelData;
            float weaponScale = WeaponsController.GetCurrentWeapon().EnemyHpScale;

            Database.SetLevelStats(level.EnemyHealthMult * weaponScale, level.EnemyDamageMult);
        }
    }
}