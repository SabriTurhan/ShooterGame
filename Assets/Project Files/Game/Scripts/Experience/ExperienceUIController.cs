using TMPro;
using UnityEngine;

namespace Watermelon
{
    public class ExperienceUIController : MonoBehaviour
    {
        [SerializeField] SlicedFilledImage expProgressFillImage;
        [SerializeField] SlicedFilledImage expProgressBackFillImage;
        [SerializeField] TextMeshProUGUI expLevelText;
        [SerializeField] TextMeshProUGUI expProgressText;

        [Space]
        [SerializeField] ExperienceStarsManager starsManager;

        private int displayedExpPoints;

        private int hittedStarsAmount = 0;
        private int fixedStarsAmount;
        private float currentFillAmount;
        private float targetFillAmount;

        private TweenCase whiteFillbarCase;
        private TweenCase fillTweenCase;
        private TweenCase floatTweenCase;

        public void Init()
        {
            starsManager.Init(this);

            UpdateUI(true);

            ExperienceController.ExperienceGained += OnExperienceGained;
        }

        private void OnDestroy()
        {
            whiteFillbarCase.KillActive();
            fillTweenCase.KillActive();
            floatTweenCase.KillActive();

            ExperienceController.ExperienceGained -= OnExperienceGained;
        }

        private void OnExperienceGained(int experience)
        {
            PlayXpGainedAnimation(experience, () =>
            {
                UpdateUI(false);
            });
        }

        public void PlayXpGainedAnimation(int starsAmount, System.Action OnComplete = null)
        {
            hittedStarsAmount = 0;
            fixedStarsAmount = starsAmount;

            int currentLevelExp = ExperienceController.CurrentLevelData.ExperienceRequired;
            int requiredExp = ExperienceController.NextLevelData.ExperienceRequired;

            targetFillAmount = Mathf.InverseLerp(currentLevelExp, requiredExp, ExperienceController.ExperiencePoints);
            currentFillAmount = expProgressFillImage.fillAmount;

            Camera mainCamera = Camera.main;

            starsManager.PlayXpGainedAnimation(starsAmount, new Vector3(0.5f, 0.5f, mainCamera.nearClipPlane), () => UpdateUI(false, OnComplete));
        }

        public void OnStarHitted()
        {
            hittedStarsAmount++;

            if (whiteFillbarCase != null)
                whiteFillbarCase.Kill();

            expProgressBackFillImage.gameObject.SetActive(true);
            whiteFillbarCase = expProgressBackFillImage.DOFillAmount(Mathf.Lerp(currentFillAmount, targetFillAmount, Mathf.InverseLerp(0, fixedStarsAmount, hittedStarsAmount)), 0.1f).SetEasing(Ease.Type.SineIn);
        }

        public void UpdateUI(bool instantly, System.Action OnComplete = null)
        {
            int currentLevelExp = ExperienceController.CurrentLevelData.ExperienceRequired;
            int requiredExp = ExperienceController.NextLevelData.ExperienceRequired;

            int firstValue = ExperienceController.ExperiencePoints - currentLevelExp;
            int secondValue = requiredExp - currentLevelExp;

            float fillAmount = Mathf.InverseLerp(currentLevelExp, requiredExp, ExperienceController.ExperiencePoints);
            if (instantly)
            {
                expProgressBackFillImage.fillAmount = fillAmount;
                expProgressFillImage.fillAmount = fillAmount;

                expProgressBackFillImage.gameObject.SetActive(false);

                expLevelText.text = ExperienceController.CurrentLevel.ToString();
                expProgressText.text = firstValue + "/" + secondValue;

                OnComplete?.Invoke();
            }
            else
            {
                RunFillAnimation(fillAmount, secondValue, displayedExpPoints, firstValue, OnComplete);
            }

            displayedExpPoints = firstValue;
        }

        private void RunFillAnimation(float newFillAmount, float requiredExp, int displayedExpPoints, int currentExpPoints, System.Action OnComplete = null)
        {
            fillTweenCase = Tween.DelayedCall(0.5f, () =>
            {
                fillTweenCase = expProgressFillImage.DOFillAmount(newFillAmount, 0.3f).SetEasing(Ease.Type.SineIn).OnComplete(() =>
                {
                    expLevelText.text = ExperienceController.CurrentLevel.ToString();

                    OnComplete?.Invoke();

                    expProgressBackFillImage.fillAmount = expProgressFillImage.fillAmount;
                    expProgressBackFillImage.gameObject.SetActive(false);
                });

                floatTweenCase = Tween.DoFloat(displayedExpPoints, currentExpPoints, 0.3f, (value) =>
                {
                    expProgressText.text = (int)value + "/" + requiredExp;
                }).SetEasing(Ease.Type.SineIn);
            });
        }
    }
}