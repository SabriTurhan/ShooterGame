#pragma warning disable 649
using UnityEngine;

namespace Watermelon.LevelSystem
{
    public class LevelEditorChest : MonoBehaviour
    {
        public LevelChestType type;
        public CurrencyType rewardCurrency;
        public int rewardValue = 5;
        public int droppedCurrencyItemsAmount = 5;
    }
}