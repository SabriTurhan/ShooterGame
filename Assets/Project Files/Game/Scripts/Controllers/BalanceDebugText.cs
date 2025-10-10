using TMPro;
using UnityEngine;

namespace Watermelon.SquadShooter
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class BalanceDebugText : MonoBehaviour
    {
        private TextMeshProUGUI difficultyText;

        private LevelSave levelSave;

        private void OnEnable()
        {
            BalanceController.BalanceUpdated += OnBalanceUpdated;
        }

        private void OnDisable()
        {
            BalanceController.BalanceUpdated -= OnBalanceUpdated;
        }

        private void Awake()
        {
            difficultyText = GetComponent<TextMeshProUGUI>();

            levelSave = SaveController.GetSaveObject<LevelSave>("level");
        }

        private void UpdateText()
        {
            if (difficultyText != null)
            {
                difficultyText.SetText("lvl: " + (levelSave.WorldIndex + 1) + "-" + (levelSave.LevelIndex + 1)
                    + "\npwr: " + BalanceController.CurrentGeneralPower + "/" + BalanceController.PowerRequirement
                    + "\nupg: " + BalanceController.UpgradesDifference
                    + "\ndif: " + BalanceController.CurrentDifficulty.Note);
            }
        }

        private void OnBalanceUpdated(bool highlight)
        {
            UpdateText();
        }

        public static BalanceDebugText Create()
        {
            GameObject canvasObject = new GameObject("[Balance Canvas]");
            canvasObject.transform.SetParent(UIController.MainCanvas.transform);
            canvasObject.transform.ResetLocal();

            RectTransform rectTransform = canvasObject.GetOrSetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 99;

            GameObject devTextObject = new GameObject("[Dev Text]");
            devTextObject.transform.SetParent(canvasObject.transform);
            devTextObject.transform.ResetLocal();

            RectTransform devRectTransform = devTextObject.AddComponent<RectTransform>();
            devRectTransform.anchorMin = new Vector2(0, 1);
            devRectTransform.anchorMax = new Vector2(0, 1);
            devRectTransform.pivot = new Vector2(0, 1);

            devRectTransform.sizeDelta = new Vector2(300, 145);
            devRectTransform.anchoredPosition = new Vector2(35, -325);

            TextMeshProUGUI text = devTextObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 28;

            BalanceDebugText balanceDebugText = devTextObject.AddComponent<BalanceDebugText>();
            balanceDebugText.UpdateText();

            return balanceDebugText;
        }
    }
}