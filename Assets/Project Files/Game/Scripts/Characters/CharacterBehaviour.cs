using UnityEngine;
using UnityEngine.AI;
using Watermelon;
using Watermelon.LevelSystem;

namespace Watermelon.SquadShooter
{
    public class CharacterBehaviour : MonoBehaviour, IEnemyDetector, IHealth, INavMeshAgent
    {
        private static readonly int SHADER_HIT_SHINE_COLOR_HASH = Shader.PropertyToID("_EmissionColor");

        private static CharacterBehaviour characterBehaviour;

        [SerializeField] NavMeshAgent agent;
        [SerializeField] EnemyDetector enemyDetector;

        [Header("Health")]
        [SerializeField] HealthbarBehaviour healthbarBehaviour;
        public HealthbarBehaviour HealthbarBehaviour => healthbarBehaviour;

        [SerializeField] ParticleSystem healingParticle;
        [SerializeField] ParticleSystem godModeParticle;

        [Header("Target")]
        [SerializeField] GameObject targetRingPrefab;
        [SerializeField] Color targetRingActiveColor;
        [SerializeField] Color targetRingDisabledColor;
        [SerializeField] Color targetRingSpecialColor;

        [Space(5)]
        [SerializeField] AimRingBehavior aimRingBehavior;

        // Character Graphics
        private BaseCharacterGraphics graphics;
        public BaseCharacterGraphics Graphics => graphics;

        private GameObject graphicsPrefab;
        private SkinnedMeshRenderer characterMeshRenderer;

        private MaterialPropertyBlock hitShinePropertyBlock;
        private TweenCase hitShineTweenCase;

        private CharacterStats stats;
        public CharacterStats Stats => stats;

        // Gun
        private BaseGunBehavior gunBehaviour;
        public BaseGunBehavior Weapon => gunBehaviour;

        private GameObject gunPrefabGraphics;

        // Health
        private float currentHealth;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => stats.Health;
        public bool FullHealth => currentHealth == stats.Health;

        public bool IsInvulnerable { get; private set; }

        public bool IsActive => isActive;
        private bool isActive;

        public static Transform Transform => characterBehaviour.transform;

        // Movement
        private MovementSettings movementSettings;
        private MovementSettings movementAimingSettings;

        private MovementSettings activeMovementSettings;
        public MovementSettings MovementSettings => activeMovementSettings;

        private bool isMoving;
        private float speed = 0;

        private Vector3 movementVelocity;
        public Vector3 MovementVelocity => movementVelocity;

        public EnemyDetector EnemyDetector => enemyDetector;

        public bool IsCloseEnemyFound => closestEnemyBehaviour != null;
        public bool IsAttackingAllowed { get; private set; } = true;

        private BaseEnemyBehavior closestEnemyBehaviour;
        public BaseEnemyBehavior ClosestEnemyBehaviour => closestEnemyBehaviour;

        private Transform playerTarget;
        private GameObject targetRing;
        private Renderer targetRingRenderer;
        private TweenCase ringTweenCase;

        private bool isMovementActive = false;
        public bool IsMovementActive => isMovementActive;

        public static bool IsDead { get; private set; } = false;

        public static SimpleCallback OnDied;

        private void Awake()
        {
            agent.enabled = false;
        }

        public void Init()
        {
            characterBehaviour = this;

            hitShinePropertyBlock = new MaterialPropertyBlock();

            isActive = false;
            enabled = false;

            // Create target
            GameObject tempTarget = new GameObject("[TARGET]");
            tempTarget.transform.position = transform.position;
            tempTarget.SetActive(true);

            playerTarget = tempTarget.transform;

            // Initialise enemy detector
            enemyDetector.Init(this);

            // Set health
            currentHealth = MaxHealth;

            // Initialise healthbar
            healthbarBehaviour.Init(transform, this, true, CharactersController.SelectedCharacter.GetCurrentStage().HealthBarOffset);

            aimRingBehavior.Init(transform);

            targetRing = Instantiate(targetRingPrefab, new Vector3(0f, 0f, -999f), Quaternion.identity);
            targetRingRenderer = targetRing.GetComponent<Renderer>();

            aimRingBehavior.Hide();

            IsDead = false;

            GameSettings settings = GameSettings.GetSettings();
            IsAttackingAllowed = !settings.UseAttackButton;
            if (settings.UseAttackButton)
            {
                AttackButtonBehavior.onStatusChanged += OnAttackButtonStatusChanged;
            }
        }

        private void OnAttackButtonStatusChanged(bool isPressed)
        {
            IsAttackingAllowed = isPressed;
        }

        public void Reload(bool resetHealth = true)
        {
            // Set health
            if (resetHealth)
            {
                currentHealth = MaxHealth;
            }

            IsDead = false;

            healthbarBehaviour.EnableBar();
            healthbarBehaviour.RedrawHealth();

            enemyDetector.Reload();

            enemyDetector.gameObject.SetActive(false);

            graphics.DisableRagdoll();
            graphics.Reload();

            gunBehaviour.Reload();

            gameObject.SetActive(true);
        }

        public void ResetDetector()
        {
            var radius = enemyDetector.DetectorRadius;
            enemyDetector.SetRadius(0);
            Tween.NextFrame(() => enemyDetector.SetRadius(radius), framesOffset: 2, updateMethod: UpdateMethod.FixedUpdate);
        }

        public void Unload()
        {
            if (graphics != null)
                graphics.Unload();

            if (playerTarget != null)
                Destroy(playerTarget.gameObject);

            if (aimRingBehavior != null)
                Destroy(aimRingBehavior.gameObject);

            if (healthbarBehaviour != null)
                healthbarBehaviour.Destroy();
        }

        public void OnLevelLoaded()
        {
            if (gunBehaviour != null)
                gunBehaviour.OnLevelLoaded();
        }

        public void OnNavMeshUpdated()
        {
            if (agent.isOnNavMesh)
            {
                agent.enabled = true;
                agent.isStopped = false;
            }
        }

        public void ActivateAgent()
        {
            agent.enabled = true;
            agent.isStopped = false;
        }

        public static void DisableNavmeshAgent()
        {
            characterBehaviour.agent.enabled = false;
        }

        public void MakeInvulnerable(float duration)
        {
            IsInvulnerable = true;

            godModeParticle.Play();

            Tween.DelayedCall(duration, () => {
                IsInvulnerable = false;

                godModeParticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            });
        }

        // Returns true if taken damage
        public virtual bool TakeDamage(float damage)
        {
            if (currentHealth <= 0 || IsInvulnerable)
                return false;

            currentHealth = Mathf.Clamp(currentHealth - damage, 0, MaxHealth);

            healthbarBehaviour.OnHealthChanged();

            VirtualCamera gameCameraCase = CameraController.GetCamera(CameraType.Gameplay);
            gameCameraCase.Shake(0.04f, 0.04f, 0.3f, 1.4f);

            if (currentHealth <= 0)
            {
                healthbarBehaviour.DisableBar();
                OnCloseEnemyChanged(null);

                isActive = false;
                enabled = false;

                enemyDetector.gameObject.SetActive(false);
                aimRingBehavior.Hide();

                OnDeath();

                graphics.EnableRagdoll();

                OnDied?.Invoke();

#if MODULE_HAPTIC
                Haptic.Play(Haptic.HAPTIC_MEDIUM);
#endif
            }

            HitEffect();

            AudioController.PlaySound(AudioController.AudioClips.characterHit.GetRandomItem());

#if MODULE_HAPTIC
            Haptic.Play(Haptic.HAPTIC_LIGHT);
#endif

            FloatingTextController.SpawnFloatingText("PlayerHit", "-" + damage.ToString("F0"), transform.position + new Vector3(Random.Range(-0.3f, 0.3f), 3.75f, Random.Range(-0.1f, 0.1f)), Quaternion.identity, 1.0f, Color.white);

            return true;
        }

        public void OnDeath()
        {
            graphics.OnDeath();

            IsDead = true;

            Tween.DelayedCall(0.5f, LevelController.OnPlayerDied);
        }

        public void SetPosition(Vector3 position)
        {
            playerTarget.position = position.AddToZ(10f);
            transform.position = position;
            transform.rotation = Quaternion.identity;

            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.Warp(position);
            }
        }

        protected void HitEffect()
        {
            hitShineTweenCase.KillActive();

            characterMeshRenderer.GetPropertyBlock(hitShinePropertyBlock);
            hitShinePropertyBlock.SetColor(SHADER_HIT_SHINE_COLOR_HASH, Color.white);
            characterMeshRenderer.SetPropertyBlock(hitShinePropertyBlock);

            hitShineTweenCase = characterMeshRenderer.DOPropertyBlockColor(SHADER_HIT_SHINE_COLOR_HASH, hitShinePropertyBlock, Color.black, 0.32f);

            graphics.PlayHitAnimation();
        }

        #region Gun
        public void SetGun(WeaponData weapon, WeaponUpgrade weaponUpgrade, bool playBounceAnimation = false, bool playAnimation = false, bool playParticle = false)
        {
            if (gunBehaviour != null)
                gunBehaviour.OnGunUnloaded();

            // Check if graphics isn't exist already
            if (gunPrefabGraphics != weaponUpgrade.WeaponPrefab)
            {
                // Store prefab link
                gunPrefabGraphics = weaponUpgrade.WeaponPrefab;

                if (gunBehaviour != null)
                {
                    Destroy(gunBehaviour.gameObject);
                }

                if (gunPrefabGraphics != null)
                {
                    GameObject gunObject = Instantiate(gunPrefabGraphics);
                    gunObject.SetActive(true);

                    gunBehaviour = gunObject.GetComponent<BaseGunBehavior>();

                    if (graphics != null)
                    {
                        gunBehaviour.InitCharacter(graphics);
                        gunBehaviour.PlaceGun(graphics);

                        graphics.SetShootingAnimation(gunBehaviour.GetShootAnimationClip());

                        gunBehaviour.UpdateHandRig();
                    }
                }
            }

            if (gunBehaviour != null)
            {
                gunBehaviour.Init(this, weapon);

                Vector3 defaultScale = gunBehaviour.transform.localScale;

                if (playAnimation)
                {
                    gunBehaviour.transform.localScale = defaultScale * 0.8f;
                    gunBehaviour.transform.DOScale(defaultScale, 0.15f).SetEasing(Ease.Type.BackOut);
                }

                if (playBounceAnimation)
                    gunBehaviour.PlayBounceAnimation();

                if (playParticle)
                    gunBehaviour.PlayUpgradeParticle();
            }

            enemyDetector.SetRadius(weaponUpgrade.RangeRadius);
            aimRingBehavior.SetRadius(weaponUpgrade.RangeRadius);
        }

        public void OnGunShooted()
        {
            graphics.OnShoot();
        }
        #endregion

        #region Graphics
        public void SetStats(CharacterStats stats)
        {
            this.stats = stats;

            currentHealth = stats.Health;

            if (healthbarBehaviour != null)
                healthbarBehaviour.OnHealthChanged();
        }

        public void SetGraphics(GameObject newGraphicsPrefab, bool playParticle, bool playAnimation)
        {
            // Check if graphics isn't exist already
            if (graphicsPrefab != newGraphicsPrefab)
            {
                // Store prefab link
                graphicsPrefab = newGraphicsPrefab;

                AnimatorParameters animatorParameters = null;

                if (graphics != null)
                {
                    animatorParameters = new AnimatorParameters(graphics.CharacterAnimator);

                    if (gunBehaviour != null)
                        gunBehaviour.transform.SetParent(null);

                    graphics.Unload();

                    Destroy(graphics.gameObject);
                }

                GameObject graphicObject = Instantiate(newGraphicsPrefab);
                graphicObject.transform.SetParent(transform);
                graphicObject.transform.ResetLocal();
                graphicObject.SetActive(true);

                graphics = graphicObject.GetComponent<BaseCharacterGraphics>();
                graphics.Init(this);

                movementSettings = graphics.MovementSettings;
                movementAimingSettings = graphics.MovementAimingSettings;

                activeMovementSettings = movementSettings;

                characterMeshRenderer = graphics.MeshRenderer;

                if (gunBehaviour != null)
                {
                    gunBehaviour.InitCharacter(graphics);
                    gunBehaviour.PlaceGun(graphics);

                    graphics.SetShootingAnimation(gunBehaviour.GetShootAnimationClip());

                    gunBehaviour.UpdateHandRig();
                }

                if (playParticle)
                    graphics.PlayUpgradeParticle();

                if (playAnimation)
                    graphics.PlayBounceAnimation();

                if (animatorParameters != null)
                    animatorParameters.ApplyTo(graphics.CharacterAnimator);
            }
        }
        #endregion

        public void Activate(bool check = true)
        {
            if (check && isActive)
                return;

            isActive = true;
            enabled = true;

            enemyDetector.gameObject.SetActive(true);

            aimRingBehavior.Show();

            graphics.Activate();

            NavMeshController.InvokeOrSubscribe(this);
        }

        public void Disable()
        {
            if (!isActive)
                return;

            isActive = false;
            enabled = false;

            agent.enabled = false;

            aimRingBehavior.Hide();

            targetRing.SetActive(false);
            targetRing.transform.SetParent(null);

            graphics.Disable();

            closestEnemyBehaviour = null;

            if (isMoving)
            {
                isMoving = false;

                speed = 0;
            }
        }

        public void MoveForwardAndDisable(float duration)
        {
            agent.enabled = false;

            transform.DOMove(transform.position + Vector3.forward * activeMovementSettings.MoveSpeed * duration, duration).OnComplete(() =>
            {
                Disable();
            });
        }

        public void DisableAgent()
        {
            agent.enabled = false;
        }

        public void ActivateMovement()
        {
            isMovementActive = true;

            aimRingBehavior.Show();
        }

        private void Update()
        {
            if (gunBehaviour != null)
                gunBehaviour.UpdateHandRig();

            if (!isActive)
                return;

            var joystick = Control.CurrentControl;

            if (joystick.IsMovementInputNonZero && joystick.MovementInput.sqrMagnitude > 0.1f)
            {
                if (!isMoving)
                {
                    isMoving = true;

                    speed = 0;

                    graphics.OnMovingStarted();
                }

                float maxAlowedSpeed = Mathf.Clamp01(joystick.MovementInput.magnitude) * activeMovementSettings.MoveSpeed;

                if (speed > maxAlowedSpeed)
                {
                    speed -= activeMovementSettings.Acceleration * Time.deltaTime;
                    if (speed < maxAlowedSpeed)
                    {
                        speed = maxAlowedSpeed;
                    }
                }
                else
                {
                    speed += activeMovementSettings.Acceleration * Time.deltaTime;
                    if (speed > maxAlowedSpeed)
                    {
                        speed = maxAlowedSpeed;
                    }
                }

                movementVelocity = transform.forward * speed;

                transform.position += joystick.MovementInput * Time.deltaTime * speed;

                graphics.OnMoving(Mathf.InverseLerp(0, activeMovementSettings.MoveSpeed, speed), joystick.MovementInput, IsCloseEnemyFound);

                if (!IsCloseEnemyFound)
                {
                    transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(joystick.MovementInput.normalized), Time.deltaTime * activeMovementSettings.RotationSpeed);
                }
            }
            else
            {
                if (isMoving)
                {
                    isMoving = false;

                    movementVelocity = Vector3.zero;

                    graphics.OnMovingStoped();

                    speed = 0;
                }
            }

            if (IsCloseEnemyFound)
            {
                playerTarget.position = Vector3.Lerp(playerTarget.position, new Vector3(closestEnemyBehaviour.transform.position.x, transform.position.y, closestEnemyBehaviour.transform.position.z), Time.deltaTime * activeMovementSettings.RotationSpeed);

                transform.LookAt(new Vector3(playerTarget.position.x, transform.position.y, playerTarget.position.z));
            }

            targetRing.transform.rotation = Quaternion.identity;

            if (healthbarBehaviour != null)
                healthbarBehaviour.FollowUpdate();

            aimRingBehavior.UpdatePosition();
        }

        private void FixedUpdate()
        {
            graphics.CustomFixedUpdate();

            if (gunBehaviour != null)
                gunBehaviour.GunUpdate();
        }

        public void OnCloseEnemyChanged(BaseEnemyBehavior enemyBehavior)
        {
            if (!isActive) return;

            if (enemyBehavior != null)
            {
                if (closestEnemyBehaviour == null)
                {
                    playerTarget.position = transform.position + transform.forward * 5;
                }

                activeMovementSettings = movementAimingSettings;

                closestEnemyBehaviour = enemyBehavior;

                targetRing.SetActive(true);
                targetRing.transform.rotation = Quaternion.identity;

                ringTweenCase.KillActive();

                targetRing.transform.SetParent(enemyBehavior.transform);
                targetRing.transform.localScale = Vector3.one * enemyBehavior.Stats.TargetRingSize * 1.4f;
                targetRing.transform.localPosition = Vector3.zero;

                ringTweenCase = targetRing.transform.DOScale(Vector3.one * enemyBehavior.Stats.TargetRingSize, 0.2f).SetEasing(Ease.Type.BackIn);

                // old camera
                //CameraController.SetEnemyTarget(enemyBehavior);

                SetTargetActive();

                return;
            }

            activeMovementSettings = movementSettings;

            closestEnemyBehaviour = null;
            targetRing.SetActive(false);
            targetRing.transform.SetParent(null);

            // old camera
            //CameraController.SetEnemyTarget(null);
        }

        public static BaseEnemyBehavior GetClosestEnemy()
        {
            return characterBehaviour.enemyDetector.ClosestEnemy;
        }

        public static CharacterBehaviour GetBehaviour()
        {
            return characterBehaviour;
        }

        public void TryAddClosestEnemy(BaseEnemyBehavior enemy)
        {
            EnemyDetector.TryAddClosestEnemy(enemy);
        }

        public void SetTargetActive()
        {
            if (closestEnemyBehaviour != null && closestEnemyBehaviour.Tier == EnemyTier.Elite)
            {
                targetRingRenderer.material.color = targetRingSpecialColor;
            }
            else
            {
                targetRingRenderer.material.color = targetRingActiveColor;
            }
        }

        public void SetTargetUnreachable()
        {
            targetRingRenderer.material.color = targetRingDisabledColor;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(PhysicsHelper.TAG_ITEM))
            {
                BaseDropBehavior item = other.GetComponent<BaseDropBehavior>();
                if (item.IsPickable(this) && !item.IsPicked)
                {
                    item.Pick();
                }
            }
            else if (other.CompareTag(PhysicsHelper.TAG_CHEST))
            {
                other.GetComponent<AbstractChestBehavior>().ChestApproached();
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (other.CompareTag(PhysicsHelper.TAG_ITEM))
            {
                BaseDropBehavior item = other.GetComponent<BaseDropBehavior>();
                if (item.IsPickable(this) && !item.IsPicked)
                {
                    item.Pick();
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(PhysicsHelper.TAG_CHEST))
            {
                other.GetComponent<AbstractChestBehavior>().ChestLeft();
            }
        }

        public void Heal(int healAmount)
        {
            currentHealth = Mathf.Clamp(currentHealth + healAmount, 0, MaxHealth);
            healthbarBehaviour.OnHealthChanged();
            healingParticle.Play();
        }

        public void Jump()
        {
            graphics.Jump();
            gunBehaviour.transform.localScale = Vector3.zero;
            gunBehaviour.gameObject.SetActive(false);
        }

        public void SpawnWeapon()
        {
            graphics.EnableRig();
            gunBehaviour.gameObject.SetActive(true);
            gunBehaviour.DOScale(1, 0.2f).SetCustomEasing(Ease.GetCustomEasingFunction("BackOutLight"));
        }

        private void OnDestroy()
        {
            if (healthbarBehaviour.HealthBarTransform != null)
                Destroy(healthbarBehaviour.HealthBarTransform.gameObject);

            if (aimRingBehavior != null)
                aimRingBehavior.OnPlayerDestroyed();

            AttackButtonBehavior.onStatusChanged -= OnAttackButtonStatusChanged;
        }
    }
}