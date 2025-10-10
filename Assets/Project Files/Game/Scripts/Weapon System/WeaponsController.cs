using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Watermelon.LevelSystem;

namespace Watermelon.SquadShooter
{
    public class WeaponsController : MonoBehaviour
    {
        private static WeaponDatabase database;

        private static WeaponsController instance;

        private static GlobalWeaponsSave save;
        private static List<WeaponUpgrade> keyUpgradeStages = new List<WeaponUpgrade>();

        private static WeaponData[] weapons;
        public static WeaponData[] Weapons => weapons;

        public static int BasePower { get; private set; }
        public static int SelectedWeaponIndex
        {
            get { return save.selectedWeaponIndex; }
            private set { save.selectedWeaponIndex = value; }
        }

        public static event SimpleCallback NewWeaponSelected;
        public static event SimpleCallback WeaponUpgraded;
        public static event SimpleCallback WeaponCardsAmountChanged;
        public static event WeaponDelagate WeaponUnlocked;

        public void Init(WeaponDatabase database)
        {
            instance = this;

            WeaponsController.database = database;

            save = SaveController.GetSaveObject<GlobalWeaponsSave>("weapon_save");

            weapons = database.Weapons;

            for (int i = 0; i < weapons.Length; i++)
            {
                weapons[i].Init();

                for (int j = 0; j < weapons[i].Upgrades.Length; j++)
                {
                    WeaponUpgrade currentStage = weapons[i].Upgrades[j];

                    if (currentStage.KeyUpgradeNumber != -1)
                    {
                        keyUpgradeStages.Add(currentStage);
                    }

                    if (currentStage.KeyUpgradeNumber == 0)
                    {
                        BasePower = currentStage.Power;
                    }
                }
            }

            keyUpgradeStages.OrderBy(s => s.KeyUpgradeNumber);

            CheckWeaponUpdateState();
        }

        public static int GetCeilingKeyPower(int currentKeyUpgrade)
        {
            for (int i = keyUpgradeStages.Count - 1; i >= 0; i--)
            {
                if (keyUpgradeStages[i].KeyUpgradeNumber <= currentKeyUpgrade)
                {
                    return keyUpgradeStages[i].Power;
                }
            }

            return keyUpgradeStages[0].Power;
        }

        public void CheckWeaponUpdateState()
        {
            for (int i = 0; i < weapons.Length; i++)
            {
                WeaponData weapon = weapons[i];

                if (weapon.UpgradeLevel == 0 && weapons[i].CardsAmount >= weapon.GetNextUpgrade().Price)
                {
                    weapon.Upgrade();

                    WeaponUnlocked?.Invoke(weapons[i]);
                }
            }
        }

        public static void SelectWeapon(WeaponData weapon)
        {
            int weaponIndex = 0;
            for (int i = 0; i < database.Weapons.Length; i++)
            {
                if (database.Weapons[i] == weapon)
                {
                    weaponIndex = i;

                    break;
                }
            }

            SelectWeapon(weaponIndex);
        }

        public static void SelectWeapon(int weaponIndex)
        {
            SelectedWeaponIndex = weaponIndex;

            CharacterBehaviour characterBehavior = CharacterBehaviour.GetBehaviour();
            if(characterBehavior != null)
            {
                WeaponData weapon = GetCurrentWeapon();

                characterBehavior.SetGun(weapon, weapon.GetCurrentUpgrade(), true);
                characterBehavior.Graphics.Grunt();
            }

            NewWeaponSelected?.Invoke();
        }

        public static void AddCard(WeaponData weapon, int amount)
        {
            weapon.Save.CardsAmount += amount;

            WeaponCardsAmountChanged?.Invoke();
        }

        public static void AddCards(List<WeaponData> weapons)
        {
            if (weapons.IsNullOrEmpty())
                return;

            for (int i = 0; i < weapons.Count; i++)
            {
                weapons[i].Save.CardsAmount += 1;
            }

            WeaponCardsAmountChanged?.Invoke();
        }

        public static WeaponData GetCurrentWeapon()
        {
            return database.GetWeaponByIndex(save.selectedWeaponIndex);
        }

        public static WeaponData GetWeapon(string weaponID)
        {
            return database.GetWeapon(weaponID);
        }

        public static RarityData GetRarityData(Rarity rarity)
        {
            return database.GetRarityData(rarity);
        }

        public static void OnWeaponUpgraded(WeaponData weapon)
        {
            AudioController.PlaySound(AudioController.AudioClips.upgrade);

            CharacterBehaviour characterBehaviour = CharacterBehaviour.GetBehaviour();
            if(characterBehaviour != null)
            {
                WeaponData currentWeapon = GetCurrentWeapon();

                characterBehaviour.SetGun(currentWeapon, currentWeapon.GetCurrentUpgrade(), true, true, true);
            }

            WeaponUpgraded?.Invoke();
        }

        public static void UnlockAllWeaponsDev()
        {
            for (int i = 0; i < database.Weapons.Length; i++)
            {
                database.Weapons[i].Upgrade();
            }
        }

        public static bool IsWeaponUnlocked(WeaponData weapon)
        {
            return weapon.Save.UpgradeLevel > 0;
        }

        [System.Serializable]
        public class GlobalWeaponsSave : ISaveObject
        {
            public int selectedWeaponIndex;

            public GlobalWeaponsSave()
            {
                selectedWeaponIndex = 0;
            }

            public void Flush()
            {

            }
        }

        public delegate void WeaponDelagate(WeaponData weapon);
    }
}