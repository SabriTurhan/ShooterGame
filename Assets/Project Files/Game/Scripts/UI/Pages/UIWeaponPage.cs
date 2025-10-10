using UnityEngine;

namespace Watermelon.SquadShooter
{
    public class UIWeaponPage : UIUpgradesAbstractPage<WeaponPanelUI, WeaponData>
    {
        protected override int SelectedIndex => Mathf.Clamp(WeaponsController.SelectedWeaponIndex, 0, int.MaxValue);

        public void UpdateUI() => itemPanels.ForEach(panel => panel.UpdateUI());

        public override WeaponPanelUI GetPanel(WeaponData weapon)
        {
            for (int i = 0; i < itemPanels.Count; i++)
            {
                if (itemPanels[i].Data == weapon)
                    return itemPanels[i];
            }

            return null;
        }

        public bool IsAnyActionAvailable()
        {
            for (int i = 0; i < itemPanels.Count; i++)
            {
                if (itemPanels[i].IsNextUpgradeCanBePurchased())
                    return true;
            }

            return false;
        }

        protected override void EnableGamepadButtonTag()
        {
            UIGamepadButton.EnableTag(UIGamepadButtonTag.Weapons);
        }

        #region UI Page

        public override void Init()
        {
            base.Init();

            for (int i = 0; i < WeaponsController.Weapons.Length; i++)
            {
                WeaponData weapon = WeaponsController.Weapons[i];

                WeaponPanelUI newPanel = AddNewPanel();
                newPanel.Init(weapon, i);
            }

            WeaponsController.WeaponUnlocked += OnWeaponUnlocked;
            WeaponsController.WeaponUpgraded += UpdateUI;
        }

        public override void PlayShowAnimation()
        {
            base.PlayShowAnimation();

            UpdateUI();
        }

        public override void PlayHideAnimation()
        {
            base.PlayHideAnimation();

            UIController.OnPageClosed(this);
        }

        protected override void HidePage(SimpleCallback onFinish)
        {
            UIController.HidePage<UIWeaponPage>(onFinish);
        }

        private void OnWeaponUnlocked(WeaponData weapon)
        {
            UpdateUI();
        }

        private void OnDestroy()
        {
            WeaponsController.WeaponUnlocked -= OnWeaponUnlocked;
            WeaponsController.WeaponUpgraded -= UpdateUI;
        }
        #endregion
    }
}