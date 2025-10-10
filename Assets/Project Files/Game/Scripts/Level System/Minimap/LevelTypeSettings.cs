using UnityEngine;

namespace Watermelon.SquadShooter
{
    [System.Serializable]
    public class LevelTypeSettings
    {
        [SerializeField] LevelType levelType;
        public LevelType LevelType => levelType;

        [SerializeField] GameObject previewObject;
        public GameObject PreviewObject => previewObject;

        public void Init()
        {

        }

        public void Unload()
        {

        }
    }
}