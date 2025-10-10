using UnityEngine;
using Watermelon.LevelSystem;

namespace Watermelon.SquadShooter
{
    public abstract class BaseDropBehavior : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] Collider itemCollider;

        [Space]
        [SerializeField] bool useAutoPickup = true;
        public bool IsAutoPickable => useAutoPickup;

        public bool IsRewarded { get; set; } = false;

        protected bool isPicked = false;
        public bool IsPicked => isPicked;

        public GameObject GameObject => gameObject;

        protected DropData dropData;
        public DropData DropData => dropData;

        public int DropAmount => dropData.Amount;
        public DropableItemType DropType => dropData.DropType;

        protected float availableToPickDelay;
        protected float autoPickDelay;

        private TweenCaseCollection throwTweenCase;

        /// <summary>
        /// Initializes the drop with the specified data and delays.
        /// </summary>
        /// <param name="dropData">The data for the drop.</param>
        /// <param name="availableToPickDelay">The delay before the item can be picked up.</param>
        /// <param name="autoPickDelay">The delay before the item is automatically picked up.</param>
        public virtual void Init(DropData dropData, float availableToPickDelay = -1f, float autoPickDelay = -1f)
        {
            this.dropData = dropData;
            this.availableToPickDelay = availableToPickDelay;
            this.autoPickDelay = autoPickDelay;

            isPicked = false;

            animator.enabled = true;
            itemCollider.enabled = true;
        }

        /// <summary>
        /// Throws the drop to the specified position with the given animation and time.
        /// </summary>
        /// <param name="position">The target position.</param>
        /// <param name="dropAnimation">The animation for the drop.</param>
        /// <param name="time">The duration of the throw.</param>
        public virtual void Throw(Vector3 position, DropAnimation dropAnimation, float time)
        {
            animator.enabled = false;
            itemCollider.enabled = false;

            throwTweenCase.KillActive();

            throwTweenCase = Tween.BeginTweenCaseCollection();
            transform.DOMoveXZ(position.x, position.z, time).SetCurveEasing(dropAnimation.FallAnimationCurve);
            transform.DOMoveY(position.y, time).SetCurveEasing(dropAnimation.FallYAnimationCurve).OnComplete(delegate
            {
                animator.enabled = true;

                if (availableToPickDelay != -1f)
                {
                    throwTweenCase += Tween.DelayedCall(availableToPickDelay, () =>
                    {
                        itemCollider.enabled = true;
                    });
                }
                else
                {
                    itemCollider.enabled = true;
                }

                if (autoPickDelay != -1f)
                {
                    throwTweenCase += Tween.DelayedCall(autoPickDelay, () =>
                    {
                        Pick();
                    });
                }

                OnItemLanded();
            });
            Tween.EndTweenCaseCollection();
        }

        /// <summary>
        /// Picks up the drop, optionally moving it to the player.
        /// </summary>
        /// <param name="moveToPlayer">Whether to move the drop to the player.</param>
        public virtual void Pick(bool moveToPlayer = true)
        {
            if (isPicked) return;

            isPicked = true;

            // Kill movement tweens
            throwTweenCase.KillActive();

            animator.enabled = false;
            itemCollider.enabled = false;

            CharacterBehaviour characterBehaviour = CharacterBehaviour.GetBehaviour();
            if (moveToPlayer && characterBehaviour != null)
            {
                throwTweenCase += transform.DOMove(characterBehaviour.transform.position.SetY(0.625f), 0.3f).SetEasing(Ease.Type.SineIn).OnComplete(() =>
                {
                    ApplyReward();
                    DestoryObject();
                });
            }
            else
            {
                ApplyReward();
                DestoryObject();
            }
        }

        /// <summary>
        /// Destroys the drop object and removes it from the drop manager.
        /// </summary>
        public void DestoryObject()
        {
            Unload();

            Drop.RemoveObject(this);
        }

        /// <summary>
        /// Unloads the drop, destroying the game object.
        /// </summary>
        public virtual void Unload()
        {
            throwTweenCase.KillActive();

            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Determines if the drop is pickable by the specified character.
        /// </summary>
        /// <param name="characterBehaviour">The character behavior.</param>
        /// <returns>True if the drop is pickable, otherwise false.</returns>
        public virtual bool IsPickable(CharacterBehaviour characterBehaviour) => true;

        /// <summary>
        /// Called when the item lands after being thrown.
        /// </summary>
        public virtual void OnItemLanded() { }

        /// <summary>
        /// Applies the reward for picking up the drop.
        /// </summary>
        /// <param name="autoReward">Whether to automatically reward the player.</param>
        public abstract void ApplyReward(bool autoReward = false);
    }
}