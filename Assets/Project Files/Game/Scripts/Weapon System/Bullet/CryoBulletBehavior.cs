using UnityEngine;

namespace Watermelon.SquadShooter
{
    // Cryo Blaster shard: a minigun-style bullet that slows the enemy it hits
    public class CryoBulletBehavior : MinigunBulletBehavior
    {
        [SerializeField, Range(0.1f, 1f)] float slowMultiplier = 0.55f;
        [SerializeField] float slowDuration = 1.6f;

        protected override void OnEnemyHitted(BaseEnemyBehavior baseEnemyBehavior)
        {
            base.OnEnemyHitted(baseEnemyBehavior);

            baseEnemyBehavior.ApplySlow(slowMultiplier, slowDuration);
        }
    }
}
