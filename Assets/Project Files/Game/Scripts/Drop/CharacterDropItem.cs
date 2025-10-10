using UnityEngine;
using Watermelon.SquadShooter;

namespace Watermelon
{
    public class CharacterDropItem : IDropItem
    {
        public DropableItemType DropItemType => DropableItemType.Character;

        public GameObject GetDropObject(DropData dropData)
        {
            CharacterData character = dropData.Character;
            if(character != null)
            {
                return character.DropPrefab;
            }

            return null;
        }

        public void Init()
        {

        }

        public void Unload()
        {

        }
    }
}
