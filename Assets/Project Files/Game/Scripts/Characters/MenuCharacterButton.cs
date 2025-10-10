namespace Watermelon.SquadShooter
{
    public sealed class MenuCharacterButton : MenuPanelButton
    {
        private UICharactersPanel characterPanel;

        public override void Init()
        {
            base.Init();

            characterPanel = UIController.GetPage<UICharactersPanel>();
        }

        protected override bool IsHighlightRequired()
        {
            return characterPanel.IsAnyActionAvailable();
        }

        protected override void OnButtonClicked()
        {
            UIController.HidePage<UIMainMenu>(() =>
            {
                UIController.ShowPage<UICharactersPanel>();
            });

            AudioController.PlaySound(AudioController.AudioClips.buttonSound);
        }
    }
}