using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Watermelon.LevelSystem;
using Watermelon.SquadShooter;

namespace Watermelon
{
    public class UIGame : UIPage
    {
        [SerializeField] RectTransform safeAreaRectTransform;
        [SerializeField] Joystick joystick;
        [SerializeField] AttackButtonBehavior attackButton;
        [SerializeField] RectTransform floatingTextHolder;

        [Space]
        [SerializeField] TextMeshProUGUI areaText;

        [Space]
        [SerializeField] Transform roomsHolder;
        [SerializeField] GameObject roomIndicatorUIPrefab;

        [Space]
        [SerializeField] Image fadeImage;
        [SerializeField] TextMeshProUGUI coinsText;

        [Header("Pause Panel")]
        [SerializeField] Button pauseButton;
        public Button PauseButton => pauseButton;

        [Space]
        [SerializeField] GameObject pausePanelObject;
        [SerializeField] CanvasGroup pausePanelCanvasGroup;
        [SerializeField] Button pauseResumeButton;
        [SerializeField] Button pauseExitButton;

        public Joystick Joystick => joystick;
        public RectTransform FloatingTextHolder => floatingTextHolder;

        private List<UIRoomIndicator> roomIndicators = new List<UIRoomIndicator>();

        private void Start()
        {
            attackButton.gameObject.SetActive(GameSettings.GetSettings().UseAttackButton);
        }

        public void FadeAnimation(float time, float startAlpha, float targetAlpha, Ease.Type easing, SimpleCallback callback, bool disableOnComplete = false)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.color = fadeImage.color.SetAlpha(startAlpha);
            fadeImage.DOFade(targetAlpha, time).SetEasing(easing).OnComplete(delegate
            {
                callback?.Invoke();

                if (disableOnComplete)
                    fadeImage.gameObject.SetActive(false);
            });
        }

        public override void Init()
        {
            pauseButton.onClick.AddListener(OnPauseButtonClicked);
            pauseExitButton.onClick.AddListener(OnPauseExitButtonClicked);
            pauseResumeButton.onClick.AddListener(OnPauseResumeButtonClicked);

            joystick.Init(UIController.MainCanvas);

            NotchSaveArea.RegisterRectTransform(safeAreaRectTransform);
        }

        public override void PlayHideAnimation()
        {
            UIController.OnPageClosed(this);
        }

        public override void PlayShowAnimation()
        {
            pauseButton.gameObject.SetActive(true);

            UIController.OnPageOpened(this);

            Tween.DelayedCall(0.3f, () =>
            {
                UIGamepadButton.EnableTag(UIGamepadButtonTag.Game);
                UIGamepadButton.DisableTag(UIGamepadButtonTag.MainMenu);
            });
        }

        public void InitRoomsUI(RoomData[] rooms)
        {
            roomIndicators.Clear();

            for (int i = 0; i < rooms.Length; i++)
            {
                GameObject indicatorObject = Instantiate(roomIndicatorUIPrefab);
                indicatorObject.transform.SetParent(roomsHolder);

                UIRoomIndicator roomIndicator = indicatorObject.GetComponent<UIRoomIndicator>();
                roomIndicators.Add(roomIndicator);

                roomIndicators[i].Init();

                if (i == 0)
                    roomIndicators[i].SetAsReached();
            }

            areaText.text = string.Format(LevelController.AREA_TEXT, ActiveRoom.CurrentWorldIndex + 1, ActiveRoom.CurrentLevelIndex + 1);
        }

        public void UpdateReachedRoomUI(int roomReachedIndex)
        {
            roomIndicators[roomReachedIndex % roomIndicators.Count].SetAsReached();
        }

        public void UpdateCoinsText(int newAmount)
        {
            coinsText.text = CurrencyHelper.Format(newAmount);
        }

        #region Pause
        private void OnPauseResumeButtonClicked()
        {
            if (!GameController.IsGameActive)
                return;

            Time.timeScale = 1.0f;

            pausePanelCanvasGroup.alpha = 0.0f;
            pausePanelCanvasGroup.DOFade(0.0f, 0.3f, unscaledTime: true).OnComplete(() =>
            {
                pausePanelObject.SetActive(false);
            });
        }

        private void OnPauseExitButtonClicked()
        {
            Overlay.Show(0.3f, () =>
            {
                Time.timeScale = 1.0f;

                LevelController.UnloadLevel();

                GameController.OnLevelExit();

                Overlay.Hide(0.3f, null);
            });
        }

        private void OnPauseButtonClicked()
        {
            Time.timeScale = 0.0f;

            pausePanelObject.SetActive(true);
            pausePanelCanvasGroup.alpha = 0.0f;
            pausePanelCanvasGroup.DOFade(1.0f, 0.3f, unscaledTime: true);

        }
        #endregion
    }
}