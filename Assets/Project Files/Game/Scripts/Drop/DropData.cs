using UnityEngine.Serialization;

namespace Watermelon.SquadShooter
{
    [System.Serializable]
    public class DropData
    {
        [FormerlySerializedAs("dropType")]
        public DropableItemType DropType;

        [FormerlySerializedAs("currencyType")]
        public CurrencyType CurrencyType;

        [FormerlySerializedAs("weapon")]
        public WeaponData Weapon;

        [FormerlySerializedAs("amount")]
        public int Amount;

        public CharacterData Character;

        public int Level;

        public DropData() { }

        public DropData Clone()
        {
            DropData data = new DropData();

            data.DropType = DropType;
            data.CurrencyType = CurrencyType;
            data.Weapon = Weapon;
            data.Amount = Amount;
            data.Character = Character;
            data.Level = Level;

            return data;
        }
    }
}