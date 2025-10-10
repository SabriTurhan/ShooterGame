using UnityEngine;
using UnityEngine.UI;
using Watermelon.LevelSystem;

namespace Watermelon.SquadShooter
{
    public class LevelProgressionPanel : MonoBehaviour
    {
        [SerializeField] Transform levelPreviewContainer;

        [Space]
        [SerializeField] GameObject currentWorldObject;
        [SerializeField] Image currentWorldImage;

        [SerializeField] GameObject nextWorldObject;
        [SerializeField] Image nextWorldImage;

        [Space]
        [SerializeField] RectTransform arrowRectTransform;

        private LevelsDatabase levelsDatabase;
        private GameSettings levelSettings;

        private PreviewCase[] previewCases;

        private CanvasGroup canvasGroup;
        private LevelSave levelSave;

        private TweenCase fadeTweenCase;

        public void Init()
        {
            canvasGroup = GetComponent<CanvasGroup>();

            levelSettings = GameSettings.GetSettings();
            levelsDatabase = levelSettings.LevelsDatabase;

            levelSave = SaveController.GetSaveObject<LevelSave>("level");
        }

        public void LoadPanel()
        {
            int currentWorldIndex = levelSave.WorldIndex;
            int currentLevelIndex = levelSave.LevelIndex;

            WorldData currentWorld = levelsDatabase.GetWorld(currentWorldIndex);
            WorldData nextWorld = levelsDatabase.GetWorld(currentWorldIndex + 1);

            // Reset pool objects
            if (previewCases != null)
            {
                for (int i = 0; i < previewCases.Length; i++)
                {
                    previewCases[i].Reset();
                }
            }

            // Reset arrow object
            arrowRectTransform.SetParent(transform);

            if (currentWorld != null)
            {
                // Enable panel
                gameObject.SetActive(true);

                // Set current world preview image
                currentWorldImage.sprite = currentWorld.PreviewSprite != null ? currentWorld.PreviewSprite : levelSettings.DefaultWorldSprite;

                // Set next world preview image
                if (nextWorld != null)
                {
                    nextWorldImage.sprite = nextWorld.PreviewSprite != null ? nextWorld.PreviewSprite : levelSettings.DefaultWorldSprite;
                    nextWorldObject.SetActive(true);
                }
                else
                {
                    nextWorldObject.SetActive(false);
                }

                previewCases = new PreviewCase[currentWorld.Levels.Length];
                for (int i = 0; i < previewCases.Length; i++)
                {
                    LevelTypeSettings levelTypeSettings = levelSettings.GetLevelSettings(currentWorld.Levels[i].Type);

                    GameObject previewObject = Instantiate(levelTypeSettings.PreviewObject);
                    previewObject.transform.SetParent(levelPreviewContainer);
                    previewObject.transform.ResetLocal();
                    previewObject.transform.localScale = Vector3.one;
                    previewObject.transform.SetAsLastSibling();

                    previewCases[i] = new PreviewCase(previewObject, levelTypeSettings);

                    if (currentLevelIndex == i)
                    {
                        previewCases[i].PreviewBehaviour.Activate(true);

                        arrowRectTransform.SetParent(previewCases[i].RectTransform);
                        arrowRectTransform.ResetLocal();
                    }
                    else if (currentLevelIndex > i)
                    {
                        previewCases[i].PreviewBehaviour.Complete();
                    }
                    else if (currentLevelIndex < i)
                    {
                        previewCases[i].PreviewBehaviour.Lock();
                    }
                }

                nextWorldObject.transform.SetAsLastSibling();
            }
            else
            {
                // Disable panel
                gameObject.SetActive(false);
            }
        }

        public void Show()
        {
            fadeTweenCase.KillActive();

            fadeTweenCase = canvasGroup.DOFade(1.0f, 0.3f).SetEasing(Ease.Type.CircIn);
        }

        public void Hide()
        {
            fadeTweenCase.KillActive();

            fadeTweenCase = canvasGroup.DOFade(0.0f, 0.3f).SetEasing(Ease.Type.CircIn);
        }
    }
}