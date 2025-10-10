using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Watermelon.SquadShooter
{
    public class WeaponPanelUI : UIUpgradeAbstractPanel
    {
        [SerializeField] TextMeshProUGUI weaponName;
        [SerializeField] Image weaponImage;
        [SerializeField] Image weaponBackImage;
        [SerializeField] TextMeshProUGUI rarityText;

        [Header("Locked State")]
        [SerializeField] GameObject lockedStateObject;
        [SerializeField] SlicedFilledImage cardsFillImage;
        [SerializeField] TextMeshProUGUI cardsAmountText;

        [Header("Upgrade State")]
        [SerializeField] TextMeshProUGUI levelText;
        [SerializeField] GameObject upgradeStateObject;
        [SerializeField] TextMeshProUGUI upgradePriceText;
        [SerializeField] Image upgradeCurrencyImage;

        [Space]
        [SerializeField] Color upgradeStateActiveColor = Color.white;
        [SerializeField] Color upgradeStateUnactiveColor = Color.white;
        [SerializeField] Image[] upgradesStatesImages;

        public WeaponData Data { get; private set; }

        [Space]
        [SerializeField] Button upgradesBuyButton;
        [SerializeField] Image upgradesBuyButtonImage;
        [SerializeField] TextMeshProUGUI upgradesBuyButtonText;
        [SerializeField] Sprite upgradesBuyButtonActiveSprite;
        [SerializeField] Sprite upgradesBuyButtonDisableSprite;

        [Space]
        [SerializeField] GameObject upgradesMaxObject;

        public override bool IsUnlocked => Data.UpgradeLevel > 0;
        private int weaponIndex;
        public int WeaponIndex => weaponIndex;

        private UIGamepadButton gamepadButton;
        public UIGamepadButton GamepadButton => gamepadButton;

        public Transform UpgradeButtonTransform => upgradesBuyButton.transform;

        public void Init(WeaponData data, int weaponIndex)
        {
            Data = data;
            panelRectTransform = (RectTransform)transform;
            gamepadButton = upgradesBuyButton.GetComponent<UIGamepadButton>();

            this.weaponIndex = weaponIndex;

            weaponName.text = data.WeaponName;
            weaponImage.sprite = data.Icon;
            weaponBackImage.color = data.RarityData.MainColor;
            rarityText.text = data.RarityData.Name;
            rarityText.color = data.RarityData.TextColor;

            UpdateUI();
            UpdateSelectionState();

            WeaponsController.NewWeaponSelected += UpdateSelectionState;
        }

        public bool IsNextUpgradeCanBePurchased()
        {
            if (IsUnlocked)
            {
                if (!Data.IsMaxUpgrade())
                {
                    if (CurrencyController.HasAmount(CurrencyType.Coins, Data.GetNextUpgrade().Price))
                        return true;
                }
            }

            return false;
        }

        public void UpdateUI()
        {
            if (IsUnlocked)
            {
                UpdateUpgradeState();
            }
            else
            {
                UpdateLockedState();
            }
        }

        private void UpdateSelectionState()
        {
            if (weaponIndex == WeaponsController.SelectedWeaponIndex)
            {
                selectionImage.gameObject.SetActive(true);
                backgroundTransform.localScale = Vector3.one;
            }
            else
            {
                selectionImage.gameObject.SetActive(false);
                backgroundTransform.localScale = Vector3.one;
            }

            UpdateUI();
        }

        private void UpdateLockedState()
        {
            lockedStateObject.SetActive(true);
            upgradeStateObject.SetActive(false);

            int currentAmount = Data.CardsAmount;
            int target = Data.GetNextUpgrade().Price;

            cardsFillImage.fillAmount = (float)currentAmount / target;
            cardsAmountText.text = currentAmount + "/" + target;

            powerObject.SetActive(false);
            powerText.gameObject.SetActive(false);
        }

        private void UpdateUpgradeState()
        {
            lockedStateObject.SetActive(false);
            upgradeStateObject.SetActive(true);

            WeaponUpgrade nextUpgrade = Data.GetNextUpgrade();
            if (nextUpgrade != null)
            {
                upgradePriceText.text = nextUpgrade.Price.ToString();
                upgradeCurrencyImage.sprite = CurrencyController.GetCurrency(nextUpgrade.CurrencyType).Icon;
            }
            else
            {
                upgradePriceText.text = "MAXED OUT";
                upgradeCurrencyImage.gameObject.SetActive(false);
            }

            powerObject.SetActive(true);
            powerText.gameObject.SetActive(true);
            powerText.text = Data.GetCurrentUpgrade().Power.ToString();

            RedrawUpgradeElements();
        }

        private void RedrawUpgradeElements()
        {
            levelText.text = "LEVEL " + Data.UpgradeLevel;

            if (!Data.IsMaxUpgrade())
            {
                upgradesMaxObject.SetActive(false);
                upgradesBuyButton.gameObject.SetActive(true);

                RedrawUpgradeButton();
            }
            else
            {
                upgradesMaxObject.SetActive(true);
                upgradesBuyButton.gameObject.SetActive(false);

                if (gamepadButton != null)
                    gamepadButton.SetFocus(false);
            }
        }

        protected override void RedrawUpgradeButton()
        {
            if (!Data.IsMaxUpgrade())
            {
                WeaponUpgrade nextUpgrade = Data.GetNextUpgrade();

                int price = nextUpgrade.Price;
                CurrencyType currencyType = nextUpgrade.CurrencyType;

                if (CurrencyController.HasAmount(currencyType, price))
                {
                    upgradesBuyButtonImage.sprite = upgradesBuyButtonActiveSprite;

                    if (gamepadButton != null)
                        gamepadButton.SetFocus(weaponIndex == WeaponsController.SelectedWeaponIndex);
                }
                else
                {
                    upgradesBuyButtonImage.sprite = upgradesBuyButtonDisableSprite;

                    if (gamepadButton != null)
                        gamepadButton.SetFocus(false);
                }

                upgradesBuyButtonText.text = CurrencyHelper.Format(price);

            }
        }

        public override void Select()
        {
            if (IsUnlocked)
            {
                if (weaponIndex != WeaponsController.SelectedWeaponIndex)
                {
                    AudioController.PlaySound(AudioController.AudioClips.buttonSound);

                    WeaponsController.SelectWeapon(weaponIndex);
                }
            }
        }

        public void UpgradeButton()
        {
            WeaponUpgrade nextUpgrade = Data.GetNextUpgrade();
            if (nextUpgrade.Price <= CurrencyController.GetCurrency(nextUpgrade.CurrencyType).Amount)
            {
                Select();

                CurrencyController.Substract(nextUpgrade.CurrencyType, nextUpgrade.Price);

                Data.Upgrade();

                AudioController.PlaySound(AudioController.AudioClips.buttonSound);
            }
        }

        private void OnDisable()
        {
            WeaponsController.NewWeaponSelected -= UpdateSelectionState;
        }
    }
}