using System.Linq;
using UnityEngine;
using Watermelon;

namespace Watermelon.SquadShooter
{
    [CreateAssetMenu(fileName = "Character Database", menuName = "Data/Characters/Character Database")]
    public class CharactersDatabase : ScriptableObject
    {
        [SerializeField] CharacterData[] characters;
        public CharacterData[] Characters => characters;

        public void Init()
        {
            characters.OrderBy(c => c.RequiredLevel);

            for (int i = 0; i < characters.Length; i++)
            {
                characters[i].Init();
            }
        }

        public CharacterData GetDefaultCharacter()
        {
            return characters.First();
        }

        public CharacterData GetCharacter(string characterID)
        {
            for (int i = 0; i < characters.Length; i++)
            {
                if (characters[i].ID == characterID)
                    return characters[i];
            }

            return null;
        }

        public CharacterData GetLastUnlockedCharacter()
        {
            for (int i = 0; i < characters.Length; i++)
            {
                if (characters[i].RequiredLevel > ExperienceController.CurrentLevel)
                {
                    return characters[Mathf.Clamp(i - 1, 0, characters.Length - 1)];
                }
            }

            return null;
        }

        public CharacterData GetNextCharacterToUnlock()
        {
            for (int i = 0; i < characters.Length; i++)
            {
                if (characters[i].RequiredLevel > ExperienceController.CurrentLevel)
                {
                    return characters[i];
                }
            }

            return null;
        }
    }
}