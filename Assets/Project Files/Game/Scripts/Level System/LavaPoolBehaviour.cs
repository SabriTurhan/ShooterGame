using UnityEngine;

namespace Watermelon.SquadShooter
{
    // world 5 hazard: a walkable lava pool (trigger, not an obstacle) that burns the player while standing in it
    public class LavaPoolBehaviour : MonoBehaviour
    {
        [Tooltip("share of the player's max health lost per tick")]
        [SerializeField] float damagePercent = 0.05f;
        [SerializeField] float tickInterval = 0.5f;

        private float nextTick;

        private void OnEnable()
        {
            nextTick = 0f;
        }

        private void OnTriggerStay(Collider other)
        {
            if (other.gameObject.layer != PhysicsHelper.LAYER_PLAYER || Time.time < nextTick) return;

            CharacterBehaviour character = other.GetComponent<CharacterBehaviour>();
            if (character == null || CharacterBehaviour.IsDead) return;

            nextTick = Time.time + tickInterval;
            character.TakeDamage(character.MaxHealth * damagePercent);
        }
    }
}
