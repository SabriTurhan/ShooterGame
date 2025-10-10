using UnityEngine;

namespace Watermelon.SquadShooter
{
    public interface IDropItem
    {
        public DropableItemType DropItemType { get; }

        public void Init();
        public void Unload();

        public GameObject GetDropObject(DropData dropData);
    }
}