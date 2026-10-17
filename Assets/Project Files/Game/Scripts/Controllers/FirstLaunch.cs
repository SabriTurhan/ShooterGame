using UnityEngine;
using Watermelon.LevelSystem;

namespace Watermelon.SquadShooter
{
    // Until the first level (the tutorial) has been beaten, the game starts straight in that level instead of
    // the main menu; completing it returns to the menu as usual (GameController.OnLevelCompleteClosed).
    public static class FirstLaunch
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            GameLoading.FirstSceneOverride = ResolveFirstScene;
        }

        private static string ResolveFirstScene()
        {
            if (!SaveController.IsSaveLoaded) return null;

            LevelSave levelSave = SaveController.GetSaveObject<LevelSave>("level");
            return levelSave.WorldIndex == 0 && levelSave.LevelIndex == 0 ? "Game" : null;
        }
    }
}
