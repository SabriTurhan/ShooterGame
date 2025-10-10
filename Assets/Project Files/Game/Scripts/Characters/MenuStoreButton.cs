using Watermelon.IAPStore;

namespace Watermelon.SquadShooter
{
    public sealed class MenuStoreButton : MenuPanelButton
    {
        private UIStore storePanel;

        public override void Init()
        {
            base.Init();

            storePanel = UIController.GetPage<UIStore>();
        }

        public override bool IsActive()
        {
            return Monetization.IsActive;
        }

        protected override bool IsHighlightRequired()
        {
            if(storePanel != null)
                return storePanel.IsFreeCoinsAvailable();

            return false;
        }

        protected override void OnButtonClicked()
        {
            UIController.HidePage<UIMainMenu>(() =>
            {
                UIController.ShowPage<UIStore>();
            });

            AudioController.PlaySound(AudioController.AudioClips.buttonSound);
        }
    }
}