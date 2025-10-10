using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Watermelon.LevelSystem;
using Watermelon.SquadShooter;

namespace Watermelon
{
    public class UIMainMenu : UIPage
    {
        [SerializeField] RectTransform safeAreaRectTransform;

        [SerializeField] ExperienceUIController experienceUIController;
        [SerializeField] LevelProgressionPanel levelProgressionPanel;

        [Space]
        [SerializeField] TextMeshProUGUI areaText;
        [SerializeField] TextMeshProUGUI recomendedPowerText;

        [Space]
        [SerializeField] Button settingsButton;
        [SerializeField] Button noAdsButton;
        [SerializeField] Button tapToPlayButton;

        [Space]
        [SerializeField] RectTransform bottomPanelRectTransform;

        [Space]
        [SerializeField] UINoAdsPopUp noAdsPopUp;

        private RectTransform noAdsRectTransform;

        private LevelSave levelSave;

        public ExperienceUIController ExperienceUIController => experienceUIController;
        public LevelProgressionPanel LevelProgressionPanel => levelProgressionPanel;

        private List<MenuPanelButton> panelButtons;

        private UIGamepadButton noAdsGamepadButton;
        public UIGamepadButton NoAdsGamepadButton => noAdsGamepadButton;

        private UIGamepadButton settingsGamepadButton;
        public UIGamepadButton SettingsGamepadButton => settingsGamepadButton;

        private UIGamepadButton playGamepadButton;
        public UIGamepadButton PlayGamepadButton => playGamepadButton;

        public override void Init()
        {
            levelSave = SaveController.GetSaveObject<LevelSave>("level");

            if (Monetization.IsActive)
            {
                noAdsRectTransform = (RectTransform)noAdsButton.transform;
                noAdsButton.onClick.AddListener(() => OnNoAdsButtonClicked());
                noAdsButton.gameObject.SetActive(true);

                noAdsGamepadButton = noAdsButton.GetComponent<UIGamepadButton>();

                noAdsPopUp.Init();
            }
            else
            {
                noAdsButton.gameObject.SetActive(false);
            }

            settingsGamepadButton = settingsButton.GetComponent<UIGamepadButton>();
            playGamepadButton = tapToPlayButton.GetComponent<UIGamepadButton>();

            experienceUIController.Init();

            levelProgressionPanel.Init();
            levelProgressionPanel.LoadPanel();

            int buttonsChildElements = bottomPanelRectTransform.childCount;
            if (buttonsChildElements > 0)
            {
                panelButtons = new List<MenuPanelButton>();
                for (int i = 0; i < buttonsChildElements; i++)
                {
                    MenuPanelButton menuPanelButton = bottomPanelRectTransform.GetChild(i).GetComponent<MenuPanelButton>();
                    if (menuPanelButton != null)
                    {
                        if (menuPanelButton.IsActive())
                        {
                            menuPanelButton.Init();

                            panelButtons.Add(menuPanelButton);
                        }
                        else
                        {
                            Destroy(menuPanelButton.gameObject);
                        }
                    }
                }
            }

            NotchSaveArea.RegisterRectTransform(safeAreaRectTransform);

            if (UIController.IsTablet)
            {
                var scrollSize = bottomPanelRectTransform.sizeDelta;
                scrollSize.y += 60;
                bottomPanelRectTransform.sizeDelta = scrollSize;
            }
        }

        private void OnEnable()
        {
            IAPManager.PurchaseCompleted += OnPurchaseComplete;
        }

        private void OnDisable()
        {
            IAPManager.PurchaseCompleted -= OnPurchaseComplete;
        }

        public void UpdateLevelText()
        {
            areaText.text = string.Format(LevelController.AREA_TEXT, levelSave.WorldIndex + 1, levelSave.LevelIndex + 1);
            recomendedPowerText.text = BalanceController.PowerRequirement.ToString();
        }

        public override void PlayShowAnimation()
        {
            if (!panelButtons.IsNullOrEmpty())
            {
                foreach (MenuPanelButton button in panelButtons)
                {
                    button.OnWindowOpened();
                }
            }

            levelProgressionPanel.Show();

            bottomPanelRectTransform.anchoredPosition = new Vector2(0, -500);
            bottomPanelRectTransform.DOAnchoredPosition(Vector2.zero, 0.3f).SetEasing(Ease.Type.CubicOut).OnComplete(() => { 
                UIController.OnPageOpened(this);

                UIGamepadButton.EnableTag(UIGamepadButtonTag.MainMenu);
            });

            tapToPlayButton.gameObject.SetActive(true);

            if (Monetization.IsActive)
            {
                if (AdsManager.IsForcedAdEnabled())
                {
                    noAdsRectTransform.gameObject.SetActive(true);
                    noAdsRectTransform.anchoredPosition = new Vector2(noAdsRectTransform.sizeDelta.x, noAdsRectTransform.anchoredPosition.y);
                    noAdsRectTransform.DOAnchoredPosition(new Vector2(-35, noAdsRectTransform.anchoredPosition.y), 0.5f).SetEasing(Ease.Type.CubicOut);
                }
                else
                {
                    noAdsRectTransform.gameObject.SetActive(false);
                }
            }

            UpdateLevelText();

            ExperienceController.ApplyExperience();
        }

        public override void PlayHideAnimation()
        {
            tapToPlayButton.gameObject.SetActive(false);

            if (Monetization.IsActive)
            {
                if (AdsManager.IsForcedAdEnabled())
                {
                    noAdsRectTransform.gameObject.SetActive(true);
                    noAdsRectTransform.DOAnchoredPosition(new Vector2(noAdsRectTransform.sizeDelta.x, noAdsRectTransform.anchoredPosition.y), 0.3f).SetEasing(Ease.Type.CubicIn);
                }
                else
                {
                    noAdsRectTransform.gameObject.SetActive(false);
                }
            }

            UIController.OnPageClosed(this);
        }

        private void OnPurchaseComplete(ProductKeyType productKeyType)
        {
            if (productKeyType == ProductKeyType.NoAds)
            {
                noAdsRectTransform.gameObject.SetActive(false);

                AdsManager.DisableForcedAd();
            }
        }

        #region Buttons
        public void OnNoAdsButtonClicked()
        {
            AudioController.PlaySound(AudioController.AudioClips.buttonSound);

            noAdsPopUp.Show();
        }

        public void PlayButton()
        {
            if (!graphicRaycaster.enabled) return;

            graphicRaycaster.enabled = false;

            AudioController.PlaySound(AudioController.AudioClips.buttonSound);

            Overlay.Show(0.3f, () =>
            {
                SceneManager.LoadScene("Game");

                Overlay.Hide(0.3f, null);
            });
        }
        #endregion
    }
}