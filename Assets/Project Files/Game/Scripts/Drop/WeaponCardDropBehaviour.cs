using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace Watermelon.SquadShooter
{
    /// <summary>
    /// Manages the behavior of weapon card drops, including setting card data and applying rewards.
    /// </summary>
    public class WeaponCardDropBehavior : BaseDropBehavior
    {
        [SerializeField] Image itemImage;
        [SerializeField] Image backImage;
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] GameObject particleObject;
        [SerializeField] List<ParticleSystem> rarityParticles = new List<ParticleSystem>();

        private TweenCase scaleTweenCase;

        /// <summary>
        /// Sets the data for the weapon card, including visuals and particle effects.
        /// </summary>
        /// <param name="weapon">The weapon data to set.</param>
        public void SetCardData(WeaponData weapon)
        {
            if (weapon == null)
            {
                Debug.LogError("Weapon data is null!");

                return;
            }

            itemImage.sprite = weapon.Icon;
            backImage.color = weapon.RarityData.MainColor;
            titleText.text = weapon.WeaponName;

            if (rarityParticles != null && rarityParticles.Count > 0)
            {
                foreach (ParticleSystem particle in rarityParticles)
                {
                    if (particle != null)
                    {
                        ParticleSystem.MainModule main = particle.main;
                        main.startColor = weapon.RarityData.MainColor.SetAlpha(main.startColor.color.a);
                    }
                }
            }
        }

        /// <summary>
        /// Applies the reward for picking up the weapon card drop.
        /// </summary>
        /// <param name="autoReward">Whether to automatically reward the player.</param>
        public override void ApplyReward(bool autoReward = false)
        {
            
        }

        /// <summary>
        /// Unloads the weapon card drop, stopping any active tweens and destroying the game object.
        /// </summary>
        public override void Unload()
        {
            scaleTweenCase.KillActive();

            // Destroy game object
            base.Unload();
        }

        /// <summary>
        /// Called when the weapon card drop lands, triggering particle effects.
        /// </summary>
        public override void OnItemLanded()
        {
            if (particleObject != null)
            {
                scaleTweenCase = particleObject.transform.DOScale(7f, 0.2f).SetEasing(Ease.Type.SineOut);
            }
        }
    }
}