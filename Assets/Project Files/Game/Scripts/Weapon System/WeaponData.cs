using UnityEngine;

namespace Watermelon.SquadShooter
{
    [CreateAssetMenu(fileName = "Weapon Data", menuName = "Data/Weapon System/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        [UniqueID]
        [SerializeField] string id;
        public string ID => id;

        [SerializeField] string weaponName;
        public string WeaponName => weaponName;

        [SerializeField] Rarity rarity;
        public Rarity Rarity => rarity;

        [SerializeField] Sprite icon;
        public Sprite Icon => icon;

        [SerializeField] GameObject dropPrefab;
        public GameObject DropPrefab => dropPrefab;

        [SerializeField] WeaponUpgrade[] upgrades;
        public WeaponUpgrade[] Upgrades => upgrades;

        public RarityData RarityData => WeaponsController.GetRarityData(rarity);

        private WeaponSave save;
        public WeaponSave Save => save;

        public int UpgradeLevel => save.UpgradeLevel;
        public int CardsAmount => save.CardsAmount;

        public void Init()
        {
            save = SaveController.GetSaveObject<WeaponSave>($"Weapon_{id}");
        }

        public WeaponUpgrade GetCurrentUpgrade()
        {
            return upgrades[save.UpgradeLevel];
        }

        public WeaponUpgrade GetNextUpgrade()
        {
            if (upgrades.IsInRange(save.UpgradeLevel + 1))
            {
                return upgrades[save.UpgradeLevel + 1];
            }

            return null;
        }

        public WeaponUpgrade GetUpgrade(int index)
        {
            return upgrades[Mathf.Clamp(index, 0, upgrades.Length - 1)];
        }

        public int GetCurrentUpgradeIndex()
        {
            return save.UpgradeLevel;
        }

        public bool IsMaxUpgrade()
        {
            return !upgrades.IsInRange(save.UpgradeLevel + 1);
        }

        public void Upgrade()
        {
            if (upgrades.IsInRange(save.UpgradeLevel + 1))
            {
                save.UpgradeLevel += 1;

                WeaponsController.OnWeaponUpgraded(this);
            }
        }
    }
}