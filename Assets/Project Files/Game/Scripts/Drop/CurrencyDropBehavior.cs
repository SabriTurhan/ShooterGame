using UnityEngine;
using Watermelon.LevelSystem;

namespace Watermelon.SquadShooter
{
    /// <summary>
    /// Handles the behavior for currency drops in the game, including applying rewards and playing pickup sounds.
    /// </summary>
    public class CurrencyDropBehavior : BaseDropBehavior
    {
        [SerializeField] CurrencyType currencyType;
        [SerializeField] int amount;

        public void SetCurrencyData(CurrencyType currencyType, int amount)
        {
            this.currencyType = currencyType;
            this.amount = amount;
        }

        public override void ApplyReward(bool autoReward = false)
        {
            if (currencyType == CurrencyType.Coins)
            {
                if (IsRewarded)
                {
                    LevelController.OnRewardedCoinPicked(amount);
                }
                else
                {
                    LevelController.OnCoinPicked(amount);
                }
            }
            else
            {
                CurrencyController.Add(currencyType, amount);
            }

            if (!autoReward)
            {
                Currency currency = CurrencyController.GetCurrency(currencyType);
                if (currency != null)
                {
                    AudioClip pickUpSound = currency.Data.DropPickupSound;
                    if (pickUpSound != null)
                    {
                        AudioController.PlaySound(pickUpSound);
                    }
                }
            }
        }
    }
}