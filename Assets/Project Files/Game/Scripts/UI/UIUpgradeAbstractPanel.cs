using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Watermelon
{
    public abstract class UIUpgradeAbstractPanel : MonoBehaviour
    {
        [Header("Power")]
        [SerializeField] protected GameObject powerObject;
        [SerializeField] protected TextMeshProUGUI powerText;

        [Header("Selection")]
        [SerializeField] protected Image selectionImage;
        [SerializeField] protected Transform backgroundTransform;

        protected RectTransform panelRectTransform;
        public RectTransform RectTransform => panelRectTransform;

        public abstract bool IsUnlocked { get; }

        protected bool isUpgradeAnimationPlaying;

        public void OnMoneyAmountChanged()
        {
            if (isUpgradeAnimationPlaying)
                return;

            RedrawUpgradeButton();
        }

        protected virtual void RedrawUpgradeButton()
        {

        }

        public virtual void OnPanelOpened()
        {

        }

        public abstract void Select();
    }
}