using UnityEngine;

namespace Watermelon.SquadShooter
{
    public sealed class MenuWeaponButton : MenuPanelButton
    {
        private UIWeaponPage weaponPage;

        public override void Init()
        {
            base.Init();

            weaponPage = UIController.GetPage<UIWeaponPage>();
        }

        protected override bool IsHighlightRequired()
        {
            return weaponPage.IsAnyActionAvailable();
        }

        protected override void OnButtonClicked()
        {
            UIController.HidePage<UIMainMenu>(() =>
            {
                UIController.ShowPage<UIWeaponPage>();
            });

            AudioController.PlaySound(AudioController.AudioClips.buttonSound);
        }
    }
}