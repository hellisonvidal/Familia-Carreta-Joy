using UnityEngine;
using UnityEngine.SceneManagement;

namespace FamiliaCarretaJoy
{
    /// <summary>
    /// Bootstrap script that initializes the game on startup.
    /// Attach to a GameObject in the first scene.
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        [Header("References")]
        public GameManager gameManagerPrefab;
        public UIManager uiManagerPrefab;

        private static bool initialized = false;

        private void Awake()
        {
            if (initialized) return;
            initialized = true;

            DontDestroyOnLoad(gameObject);

            // Initialize GameManager
            if (GameManager.Instance == null)
            {
                var gmObj = new GameObject("GameManager");
                var gm = gmObj.AddComponent<GameManager>();
                DontDestroyOnLoad(gmObj);
            }

            // Initialize UIManager
            if (UIManager.Instance == null && uiManagerPrefab != null)
            {
                var uiObj = Instantiate(uiManagerPrefab);
                DontDestroyOnLoad(uiObj.gameObject);
            }

            // Set target frame rate
            Application.targetFrameRate = 60;

            // Screen orientation
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // Show home screen
            GameManager.Instance?.LoadSaveData();
            UIManager.Instance?.ShowHomeScreen();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                GameManager.Instance?.SaveGame();
            }
        }

        private void OnApplicationQuit()
        {
            GameManager.Instance?.SaveGame();
        }
    }

    /// <summary>
    /// Singleton instance access for UIManager
    /// </summary>
    public partial class UIManager
    {
        private static UIManager _instance;
        public static UIManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindObjectOfType<UIManager>();
                return _instance;
            }
        }
    }
}