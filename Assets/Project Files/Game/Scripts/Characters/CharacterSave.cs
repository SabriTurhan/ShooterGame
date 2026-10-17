using Watermelon;

namespace Watermelon.SquadShooter
{
    [System.Serializable]
    public class CharacterSave : ISaveObject
    {
        public int UpgradeLevel = 0;

        // set when the character's world is reached (world heroes unlock with their world, not by XP)
        public bool UnlockedByWorld = false;

        public void Flush()
        {

        }
    }
}