using TMPro;
using UnityEngine;
using Watermelon;

namespace Watermelon.SquadShooter
{
    public class FloatingTextHitBehaviour : FloatingTextBaseBehavior
    {
        [Space]
        [SerializeField] float delay;
        [SerializeField] float disableDelay;
        [SerializeField] float scale;
        [SerializeField] float time;
        [SerializeField] Ease.Type easing;

        [Space]
        [SerializeField] float scaleTime;
        [SerializeField] Ease.Type scaleEasing;

        private Vector3 defaultScale;

        private TweenCaseCollection tweenCaseCollection;

        private void Awake()
        {
            defaultScale = transform.localScale;
        }

        private void OnDestroy()
        {
            tweenCaseCollection.KillActive();
        }

        public override void Activate(string text, float scaleMultiplier, Color color)
        {
            tweenCaseCollection.KillActive();
            tweenCaseCollection = new TweenCaseCollection();

            textRef.text = text;

            int sign = Random.value >= 0.5f ? 1 : -1;

            transform.localScale = defaultScale * scale * this.scale * scaleMultiplier;
            transform.localRotation = Quaternion.Euler(70, 0, 18 * sign);

            tweenCaseCollection += Tween.DelayedCall(delay, delegate
            {
                tweenCaseCollection += transform.DOLocalRotate(Quaternion.Euler(70, 0, 0), time).SetEasing(easing).OnComplete(delegate
                {
                    tweenCaseCollection += Tween.DelayedCall(disableDelay, delegate
                    {
                        gameObject.SetActive(false);
                    });
                });

                tweenCaseCollection += transform.DOScale(defaultScale, scaleTime).SetEasing(scaleEasing);
            });
        }
    }
}