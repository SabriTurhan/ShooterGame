using UnityEngine;
using Watermelon.LevelSystem;

namespace Watermelon.SquadShooter
{
    // Every world brings its own hero and weapon: the first time the player reaches a world they unlock and get
    // equipped, so each world is played with its own pair (the balance curve assumes it, see Tools/Balance).
    public static class WorldHeroes
    {
        [System.Serializable]
        private class WorldHeroesSave : ISaveObject
        {
            public int LastAppliedWorld = -1;

            public void Flush() { }
        }

        // world reached by this call (for menus that want to celebrate it), null when nothing changed
        public static WorldData JustUnlocked { get; private set; }

        public static void Apply(LevelsDatabase database)
        {
            JustUnlocked = null;
            if (database == null || database.Worlds.IsNullOrEmpty()) return;

            LevelSave levelSave = SaveController.GetSaveObject<LevelSave>("level");
            WorldHeroesSave save = SaveController.GetSaveObject<WorldHeroesSave>("world_heroes");
            if (levelSave.WorldIndex <= save.LastAppliedWorld) return;

            // earlier worlds too, in case the player skipped ahead
            int last = Mathf.Min(levelSave.WorldIndex, database.Worlds.Length - 1);
            for (int i = 0; i <= last; i++)
                Unlock(database.Worlds[i]);

            WorldData current = database.Worlds[last];
            if (current.FeaturedWeapon != null) WeaponsController.SelectWeapon(current.FeaturedWeapon);
            if (current.FeaturedCharacter != null) CharactersController.SelectCharacter(current.FeaturedCharacter);

            save.LastAppliedWorld = levelSave.WorldIndex;
            JustUnlocked = current;

            SaveController.MarkAsSaveIsRequired();
        }

        private static void Unlock(WorldData world)
        {
            WeaponData weapon = world.FeaturedWeapon;
            if (weapon != null && weapon.Save.UpgradeLevel <= 0)
            {
                int cards = weapon.Upgrades.Length > 1 ? weapon.Upgrades[1].Price : 0;
                weapon.Save.CardsAmount = Mathf.Max(weapon.Save.CardsAmount, cards);
                weapon.Save.UpgradeLevel = 1;
            }

            CharacterData character = world.FeaturedCharacter;
            if (character != null) character.Save.UnlockedByWorld = true;
        }
    }
}
