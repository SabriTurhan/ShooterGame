using UnityEngine;
using Watermelon.LevelSystem;

namespace Watermelon
{
    [RegisterModule("Game Settings")]
    public class GameInitModule : InitModule
    {
        public override string ModuleName => "Game Settings";

        [SerializeField] GameSettings gameSettings;

        public override void CreateComponent()
        {
            gameSettings.Init();

            LevelController.Init();
        }
    }
}