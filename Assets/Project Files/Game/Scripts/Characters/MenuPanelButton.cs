using UnityEngine;
using UnityEngine.UI;

namespace Watermelon.SquadShooter
{
    public abstract class MenuPanelButton : MonoBehaviour
    {
        [SerializeField] protected Button button;
        [SerializeField] protected Image tabImage;
        [SerializeField] protected Color defaultColor;
        [SerializeField] protected Color notificationColor;
        [SerializeField] protected Color disabledColor;
        [SerializeField] protected GameObject notificationObject;

        private TweenCase movementTweenCase;

        private Vector2 defaultAnchoredPosition;

        private RectTransform rectTransform;
        public RectTransform RectTransform => rectTransform;

        public Button Button => button;

        private UIGamepadButton gamepadButton;
        public UIGamepadButton GamepadButton => gamepadButton;

        private CanvasGroup canvasGroup;

        private bool isActive;

        public virtual void Init()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            gamepadButton = GetComponent<UIGamepadButton>();

            rectTransform = (RectTransform)button.transform;

            defaultAnchoredPosition = rectTransform.anchoredPosition;

            button.onClick.AddListener(OnButtonClicked);

            isActive = true;
        }

        public virtual bool IsActive()
        {
            return true;
        }

        public void OnWindowOpened()
        {
            if (!isActive)
                return;

            movementTweenCase.KillActive();

            rectTransform.anchoredPosition = defaultAnchoredPosition;
            tabImage.color = defaultColor;

            if (IsHighlightRequired())
            {
                notificationObject.SetActive(true);

                movementTweenCase = tabImage.DOColor(notificationColor, 0.3f, 0.3f).OnComplete(delegate
                {
                    movementTweenCase = new TabAnimation(rectTransform, new Vector2(defaultAnchoredPosition.x, defaultAnchoredPosition.y + 30)).SetDuration(1.2f).SetUnscaledMode(false).SetUpdateMethod(UpdateMethod.Update).SetEasing(Ease.Type.QuadOutIn).StartTween();
                });
            }
            else
            {
                notificationObject.SetActive(false);
            }
        }

        public void OnWindowClosed()
        {
            movementTweenCase.KillActive();

            rectTransform.anchoredPosition = defaultAnchoredPosition;
        }

        public void Disable()
        {
            isActive = false;

            button.enabled = false;

            tabImage.color = disabledColor;
            rectTransform.anchoredPosition = defaultAnchoredPosition;

            notificationObject.SetActive(false);

            canvasGroup.alpha = 0.5f;

            movementTweenCase.KillActive();
        }

        public void Activate()
        {
            isActive = true;

            button.enabled = true;

            canvasGroup.alpha = 1.0f;

            OnWindowOpened();
        }

        protected abstract bool IsHighlightRequired();
        protected abstract void OnButtonClicked();
    }
}