using UnityEngine;

namespace Watermelon.SquadShooter
{
    public class TeslaGunBehavior : BaseGunBehavior
    {
        [LineSpacer]
        [SerializeField] ParticleSystem shootParticleSystem;
        [SerializeField] GameObject lightningLoopParticle;

        [SerializeField] LayerMask targetLayers;
        [SerializeField] float chargeDuration;
        [SerializeField] DuoInt targetsHitGoal;

        private DuoFloat bulletSpeed;

        private Pool bulletPool;

        private TweenCase shootTweenCase;
        private Vector3 shootDirection;

        private bool isCharging;
        private bool isCharged;
        private bool isChargeParticleActivated;
        private float fullChargeTime;
        private float startChartgeTime;

        public override void Init(CharacterBehaviour characterBehaviour, WeaponData weapon)
        {
            base.Init(characterBehaviour, weapon);

            WeaponUpgrade currentUpgrade = weapon.GetCurrentUpgrade();

            GameObject bulletObj = currentUpgrade.BulletPrefab;

            bulletPool = new Pool(bulletObj, bulletObj.name);

            RecalculateDamage();
        }

        private void OnDestroy()
        {
            if(bulletPool != null)
                PoolManager.DestroyPool(bulletPool);
        }

        public override void OnLevelLoaded()
        {
            RecalculateDamage();
        }

        public override void RecalculateDamage()
        {
            WeaponUpgrade currentUpgrade = weapon.GetCurrentUpgrade();

            damage = currentUpgrade.Damage;
            bulletSpeed = currentUpgrade.BulletSpeed;
        }

        public override void GunUpdate()
        {
            if(!isCharging && !isCharged)
            {
                AttackButtonBehavior.SetReloadFill(1);
            }

            // if no enemy - cancel charge
            if (!characterBehaviour.IsCloseEnemyFound)
            {
                if (isCharging || isCharged)
                {
                    CancelCharge();
                }

                return;
            }

            // if not charging - start
            if (!isCharging && !isCharged)
            {
                isCharging = true;
                isChargeParticleActivated = false;
                fullChargeTime = Time.timeSinceLevelLoad + chargeDuration;
                startChartgeTime = Time.timeSinceLevelLoad;
            }

            // wait for full charge
            if (fullChargeTime >= Time.timeSinceLevelLoad)
            {
                AttackButtonBehavior.SetReloadFill(1 - (Time.timeSinceLevelLoad - startChartgeTime) / (fullChargeTime - startChartgeTime));

                // start charge particle 0.5 sec before charge complete
                if (!isChargeParticleActivated && fullChargeTime - Time.timeSinceLevelLoad <= 0.5f)
                {
                    isChargeParticleActivated = true;
                    shootParticleSystem.Play();
                }

                if (IsEnemyVisible())
                {
                    characterBehaviour.SetTargetActive();
                }
                else
                {
                    characterBehaviour.SetTargetUnreachable();
                }

                return;
            }
            // activate loop particle once charged
            else if (!isCharged)
            {
                AttackButtonBehavior.SetReloadFill(0);
                isCharged = true;
                lightningLoopParticle.SetActive(true);
            }

            if (IsEnemyVisible() && characterBehaviour.IsAttackingAllowed)
            {
                characterBehaviour.SetTargetActive();

                shootTweenCase.KillActive();

                shootTweenCase = transform.DOLocalMoveZ(-0.15f, chargeDuration * 0.3f).OnComplete(delegate
                {
                    shootTweenCase = transform.DOLocalMoveZ(0, chargeDuration * 0.6f);
                });

                int bulletsNumber = weapon.GetCurrentUpgrade().BulletsPerShot.Random();

                for (int k = 0; k < bulletsNumber; k++)
                {
                    TeslaBulletBehavior bullet = bulletPool.GetPooledObject().SetPosition(shootPoint.position).SetEulerAngles(characterBehaviour.transform.eulerAngles).GetComponent<TeslaBulletBehavior>();
                    bullet.Init(damage.Random() * characterBehaviour.Stats.BulletDamageMultiplier, bulletSpeed.Random(), characterBehaviour.ClosestEnemyBehaviour, 5f, false);
                    bullet.SetTargetsHitGoal(targetsHitGoal.Random());
                }

                characterBehaviour.OnGunShooted();

                VirtualCamera gameCameraCase = CameraController.GetCamera(CameraType.Gameplay);
                gameCameraCase.Shake(0.04f, 0.04f, 0.3f, 0.8f);

                CancelCharge();

                AudioController.PlaySound(AudioController.AudioClips.shotTesla, volumePercentage: 0.8f);
            }
            else
            {
                characterBehaviour.SetTargetUnreachable();
            }
        }

        public bool IsEnemyVisible()
        {
            if (!characterBehaviour.IsCloseEnemyFound)
                return false;

            shootDirection = characterBehaviour.ClosestEnemyBehaviour.transform.position.SetY(shootPoint.position.y) - shootPoint.position;

            RaycastHit hitInfo;
            if (Physics.Raycast(shootPoint.position - shootDirection.normalized * 1.5f, shootDirection, out hitInfo, 300f, targetLayers) ||
                Physics.Raycast(shootPoint.position, shootDirection, out hitInfo, 300f, targetLayers)
            )
            {
                if (hitInfo.collider.gameObject.layer == PhysicsHelper.LAYER_ENEMY)
                {
                    if (Vector3.Angle(shootDirection, transform.forward) < 40f)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        private void CancelCharge()
        {
            isCharging = false;
            isCharged = false;
            isChargeParticleActivated = false;
            lightningLoopParticle.SetActive(false);
            shootParticleSystem.Stop();
        }

        private void OnDrawGizmos()
        {
            if (characterBehaviour == null)
                return;

            if (characterBehaviour.ClosestEnemyBehaviour == null)
                return;

            Color defCol = Gizmos.color;
            Gizmos.color = Color.red;

            Vector3 shootDirection = characterBehaviour.ClosestEnemyBehaviour.transform.position.SetY(shootPoint.position.y) - shootPoint.position;

            Gizmos.DrawLine(shootPoint.position - shootDirection.normalized * 1.5f, characterBehaviour.ClosestEnemyBehaviour.transform.position.SetY(shootPoint.position.y));

            Gizmos.color = defCol;
        }

        public override void OnGunUnloaded()
        {
            // Destroy bullets pool
            if (bulletPool != null)
            {
                PoolManager.DestroyPool(bulletPool);

                bulletPool = null;
            }
        }

        public override void PlaceGun(BaseCharacterGraphics characterGraphics)
        {
            transform.SetParent(characterGraphics.TeslaHolderTransform);
            transform.ResetLocal();
        }

        public override void Reload()
        {
            bulletPool.ReturnToPoolEverything();
        }
    }
}