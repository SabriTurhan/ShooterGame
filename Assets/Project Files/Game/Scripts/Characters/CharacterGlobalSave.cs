using Watermelon;

namespace Watermelon.SquadShooter
{
    [System.Serializable]
    public class CharacterGlobalSave : ISaveObject
    {
        public string SelectedCharacterID;

        public void Flush()
        {

        }
    }
}