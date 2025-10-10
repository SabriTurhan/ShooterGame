using UnityEngine;
using UnityEngine.SceneManagement;
using Watermelon.LevelSystem;
using Watermelon.SquadShooter;

namespace Watermelon
{
    [DefaultExecutionOrder(-10)]
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] UIController uiController;
        [SerializeField] PedestalBehavior pedestalBehavior;

        private static WeaponsController weaponsController;
        private static CharactersController charactersController;
        private static BalanceController balanceController;
        private static ExperienceController experienceController;
        private static ParticlesController particlesController;

        private void Awake()
        {
            gameObject.CacheComponent(out weaponsController);
            gameObject.CacheComponent(out charactersController);
            gameObject.CacheComponent(out balanceController);
            gameObject.CacheComponent(out experienceController);
            gameObject.CacheComponent(out particlesController);

            GameSettings gameSettings = GameSettings.GetSettings();

            uiController.Init();

            weaponsController.Init(gameSettings.WeaponDatabase);
            experienceController.Init(gameSettings.ExperienceDatabase);
            charactersController.Init(gameSettings.CharactersDatabase);
            balanceController.Init(gameSettings.BalanceDatabase);

            particlesController.Init();

            pedestalBehavior.Init();

            uiController.InitPages();

            UICharacterSuggestion suggestionUI = UIController.GetPage<UICharacterSuggestion>();
            if(suggestionUI != null && LevelController.NeedCharacterSugession)
            {
                UIController.ShowPage<UICharacterSuggestion>();
            }
            else
            {
                UIController.ShowPage<UIMainMenu>();
            }
        }

        private void Start()
        {
            GameLoading.MarkAsReadyToHide();
        }

        public void LoadLevel(int worldIndex, int levelIndex)
        {
            LevelSave levelSave = SaveController.GetSaveObject<LevelSave>("level");
            levelSave.WorldIndex = worldIndex;
            levelSave.LevelIndex = levelIndex;

            Overlay.Show(0.3f, () =>
            {
                SceneManager.LoadScene("Game");

                Overlay.Hide(0.3f);
            });
        }
    }
}