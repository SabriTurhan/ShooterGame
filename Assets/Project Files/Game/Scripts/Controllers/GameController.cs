using UnityEngine;
using UnityEngine.SceneManagement;
using Watermelon.LevelSystem;
using Watermelon.SquadShooter;

namespace Watermelon
{
    [DefaultExecutionOrder(-10)]
    public class GameController : MonoBehaviour
    {
        [Header("Refferences")]
        [SerializeField] UIController uiController;
        [SerializeField] CameraController cameraController;

        private static ParticlesController particlesController;
        private static FloatingTextController floatingTextController;
        private static ExperienceController experienceController;
        private static WeaponsController weaponsController;
        private static CharactersController charactersController;
        private static BalanceController balanceController;
        private static EnemyController enemyController;
        private static TutorialController tutorialController;

        private static bool isGameActive;
        public static bool IsGameActive => isGameActive;

        private void Awake()
        {
            // Cache components
            gameObject.CacheComponent(out particlesController);
            gameObject.CacheComponent(out floatingTextController);
            gameObject.CacheComponent(out experienceController);
            gameObject.CacheComponent(out weaponsController);
            gameObject.CacheComponent(out charactersController);
            gameObject.CacheComponent(out balanceController);
            gameObject.CacheComponent(out enemyController);
            gameObject.CacheComponent(out tutorialController);

            GameSettings gameSettings = GameSettings.GetSettings();

            uiController.Init();

            tutorialController.Init();
            particlesController.Init();
            floatingTextController.Init();

            LevelController.CreateLevelObject();

            experienceController.Init(gameSettings.ExperienceDatabase);
            charactersController.Init(gameSettings.CharactersDatabase);
            weaponsController.Init(gameSettings.WeaponDatabase);
            balanceController.Init(gameSettings.BalanceDatabase);

            enemyController.Init();

            CharacterBehaviour characterBehaviour = LevelController.SpawnPlayer();

            cameraController.Initialise();
            CameraController.GetCamera(CameraType.Gameplay).SetTarget(characterBehaviour.transform);

            uiController.InitPages();

            UIController.ShowPage<UIGame>();

            LevelController.LoadCurrentLevel();
            LevelController.ActivateLevel(() =>
            {
                isGameActive = true;
            });
        }

        private void OnDestroy()
        {
            LevelController.Unload();

            Tween.RemoveAll();
        }

        public static void LevelComplete()
        {
            if (!isGameActive) return;

            LevelData currentLevel = LevelController.CurrentLevelData;

            UIComplete completePage = UIController.GetPage<UIComplete>();
            completePage.SetData(ActiveRoom.CurrentWorldIndex + 1, ActiveRoom.CurrentLevelIndex + 1, currentLevel.GetCoinsReward(), currentLevel.XPAmount, currentLevel.GetCardsReward());

            UIController.HidePage<UIGame>();
            UIController.ShowPage<UIComplete>();

            ExperienceController.GainExperience(LevelController.CurrentLevelData.XPAmount);

            LevelController.UnloadLevel();

            isGameActive = false;
        }

        public static void OnLevelCompleteClosed()
        {
            Overlay.Show(0.3f, () =>
            {
                SaveController.Save(true);

                SceneManager.LoadScene("Menu");

                Overlay.Hide(0.3f);
            });
        }

        public static void OnLevelExit()
        {
            isGameActive = false;

            LevelController.DisableCharacterSuggestion();

            SceneManager.LoadScene("Menu");
        }

        public static void OnLevelFailded()
        {
            if (!isGameActive) return;

            UIController.HidePage<UIGame>(() =>
            {
                UIController.ShowPage<UIGameOver>();
                UIController.PageOpened += OnFailedPageOpened;
            });

            LevelController.OnLevelFailed();

            isGameActive = false;
        }

        private static void OnFailedPageOpened(UIPage page, System.Type pageType)
        {
            if (pageType == typeof(UIGameOver))
            {
                AdsManager.ShowInterstitial(null);

                UIController.PageOpened -= OnFailedPageOpened;
            }
        }

        public static void OnReplayLevel()
        {
            isGameActive = true;

            Overlay.Show(0.3f, () =>
            {
                LevelController.UnloadLevel();

                SceneManager.LoadScene("Game");

                Overlay.Hide(0.3f);
            });
        }

        public static void OnRevive()
        {
            isGameActive = true;

            UIController.HidePage<UIGameOver>(() =>
            {
                LevelController.ReviveCharacter();

                UIController.ShowPage<UIGame>();
            });
        }

        public static void PlayCustomMusic(AudioClip music)
        {
            if (music == null) return;

            MusicSource musicSource = MusicSource.ActiveMusicSource;
            if (musicSource != null)
            {
                musicSource.AudioSource.clip = music;
                musicSource.Activate();
            }
        }
    }
}