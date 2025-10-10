using UnityEngine;
using Watermelon.SquadShooter;

namespace Watermelon
{
    public class WeaponDropItem : IDropItem
    {
        public DropableItemType DropItemType => DropableItemType.Weapon;

        public GameObject GetDropObject(DropData dropData)
        {
            WeaponData weaponData = dropData.Weapon;
            if (weaponData != null)
            {
                return weaponData.DropPrefab;
            }

            return null;
        }

        public void Init()
        {

        }

        public void Unload()
        {

        }
    }
}
