using System.Collections.Generic;
using UnityEngine;
using Watermelon.LevelSystem;

namespace Watermelon.SquadShooter
{
    public abstract class AbstractChestBehavior : MonoBehaviour
    {
        protected static readonly int IDLE_HASH = Animator.StringToHash("Idle");
        protected static readonly int SHAKE_HASH = Animator.StringToHash("Shake");
        protected static readonly int OPEN_HASH = Animator.StringToHash("Open");

        [SerializeField] protected Animator animatorRef;
        [SerializeField] protected GameObject particle;

        public delegate void OnChestOpenedCallback(AbstractChestBehavior chest);

        protected List<DropData> dropData;
        protected DuoInt itemsAmountRange;

        public static event OnChestOpenedCallback OnChestOpenedEvent;

        protected bool opened;
        protected bool isRewarded;

        public virtual void Init(List<DropData> drop)
        {
            opened = false;
            dropData = drop;
            particle.SetActive(true);

            animatorRef.SetTrigger(IDLE_HASH);

            itemsAmountRange = new DuoInt(9, 11);
        }

        public abstract void ChestApproached();
        public abstract void ChestLeft();

        protected void DropResources()
        {
            if (!LevelController.IsGameplayActive)
                return;

            Vector3 dropCenter = transform.position + Vector3.forward * -1f;

            if (!dropData.IsNullOrEmpty())
            {
                for (int i = 0; i < dropData.Count; i++)
                {
                    Drop.SpawnDropItem(dropData[i], dropCenter, Vector3.zero, isRewarded, (drop, fallingStyle) =>
                    {
                        Drop.ThrowItem(drop, fallingStyle);
                    });
                }

                AudioController.PlaySound(AudioController.AudioClips.chestOpen);
            }

            OnChestOpenedEvent?.Invoke(this);
        }
    }
}