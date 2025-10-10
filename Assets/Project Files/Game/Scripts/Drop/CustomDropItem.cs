using UnityEngine;
using Watermelon;

namespace Watermelon.SquadShooter
{
    [System.Serializable]
    public class CustomDropItem : IDropItem
    {
        [SerializeField] DropableItemType dropableItemType;
        public DropableItemType DropItemType => dropableItemType;

        [SerializeField] GameObject prefab;
        public GameObject DropPrefab => prefab;

        public CustomDropItem(DropableItemType dropableItemType, GameObject prefab)
        {
            this.dropableItemType = dropableItemType;
            this.prefab = prefab;
        }

        public void Init()
        {

        }

        public void Unload()
        {

        }

        public GameObject GetDropObject(DropData dropData)
        {
            return prefab;
        }
    }
}