using UnityEngine;
using Watermelon.LevelSystem;

namespace Watermelon.SquadShooter
{
    public class RangeEnemyBehaviour : BaseEnemyBehavior
    {
        [Header("Fighting")]
        [SerializeField] GameObject bulletPrefab;
        [SerializeField] float bulletSpeed;

        [Header("Weapon")]
        [SerializeField] Transform shootPointTransform;

        [Space]
        [SerializeField] ParticleSystem gunFireParticle;

        [Space]
        [SerializeField] bool canReload;
        public bool CanReload => canReload;

        public override void Attack()
        {
            animatorRef.SetTrigger("Shoot");
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            if (!LevelController.IsGameplayActive)
                return;

            healthbarBehaviour.FollowUpdate();
        }

        public override void OnAnimatorCallback(EnemyCallbackType enemyCallbackType)
        {
            if (enemyCallbackType == EnemyCallbackType.Hit)
            {
                EnemyBulletBehavior bullet = Instantiate(bulletPrefab).SetPosition(shootPointTransform.position).SetEulerAngles(shootPointTransform.eulerAngles).GetComponent<EnemyBulletBehavior>();
                bullet.transform.forward = transform.forward.SetY(0).normalized;
                bullet.Init(GetCurrentDamage(), bulletSpeed, 200);

                gunFireParticle.Play();

                AudioController.PlaySound(AudioController.AudioClips.enemyShot);
            }
            else if (enemyCallbackType == EnemyCallbackType.HitFinish)
            {
                InvokeOnAttackFinished();
            }
            else if (enemyCallbackType == EnemyCallbackType.ReloadFinished)
            {
                InvokeOnReloadFinished();
            }
        }
    }
}