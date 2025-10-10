using UnityEngine;

namespace Watermelon.LevelSystem
{
    [System.Serializable]
    public class ChestData
    {
        [SerializeField] GameObject prefab;
        public GameObject Prefab => prefab;

        [SerializeField] LevelChestType type;
        public LevelChestType Type => type;

        public void Init()
        {

        }

        public void Unload()
        {

        }
    }
}