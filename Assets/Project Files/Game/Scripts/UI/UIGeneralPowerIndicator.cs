using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Watermelon.SquadShooter
{
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public class UIGeneralPowerIndicator : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI text;
        [SerializeField] Image arrowImage;

        private CanvasGroup canvasGroup;

        private TweenCase fadeTweenCase;
        private TweenCase delayTweenCase;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();

            arrowImage.gameObject.SetActive(false);

            UpdateText();
        }

        private void OnEnable()
        {
            BalanceController.BalanceUpdated += UpdateText;
        }

        private void OnDisable()
        {
            BalanceController.BalanceUpdated -= UpdateText;
        }

        private void OnDestroy()
        {
            fadeTweenCase.KillActive();
            delayTweenCase.KillActive();
        }

        public void UpdateText(bool highlight = false)
        {
            float delay = highlight ? 0.5f : 0f;

            delayTweenCase = Tween.DelayedCall(delay, () =>
            {
                text.text = BalanceController.CurrentGeneralPower.ToString();
            });

            if (highlight)
            {
                arrowImage.gameObject.SetActive(true);
                text.transform.DOPushScale(1.3f, 1f, 0.6f, 0.4f, Ease.Type.SineIn, Ease.Type.SineOut).OnComplete(() =>
                {
                    arrowImage.gameObject.SetActive(false);
                });
            }
        }

        public void Show()
        {
            UpdateText();

            gameObject.SetActive(true);

            fadeTweenCase.KillActive();

            fadeTweenCase = canvasGroup.DOFade(1, 0.3f);
        }

        public void ShowImmediately()
        {
            UpdateText();

            fadeTweenCase.KillActive();

            gameObject.SetActive(true);

            canvasGroup.alpha = 1.0f;
        }

        public void Hide()
        {
            fadeTweenCase.KillActive();

            fadeTweenCase = canvasGroup.DOFade(0, 0.3f).OnComplete(() =>
            {
                gameObject.SetActive(false);
            });
        }
    }
}