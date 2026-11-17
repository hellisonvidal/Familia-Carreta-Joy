using UnityEngine;

namespace CarretaJoy
{
    /// <summary>Ajustes do jogador, guardados no aparelho.</summary>
    public static class Settings
    {
        public static float Music = 0.7f;
        public static int Quality = 1;        // 0 leve, 1 médio, 2 alto
        public static int Fps = 60;
        public static int CamMode = 0;        // 0 normal, 1 perto, 2 longe, 3 aérea
        public static bool CrewFollows = false;
        public static float Sensitivity = 1f;
        public static int StartStop = 0;
        public static int StartChar = 0;

        public static readonly string[] QualityNames = { "Leve", "Médio", "Alto" };
        public static readonly string[] CamNames = { "Normal", "Perto", "Longe", "Aérea" };

        public static void Load()
        {
            Music = PlayerPrefs.GetFloat("music", Music);
            Quality = PlayerPrefs.GetInt("quality", Application.isMobilePlatform ? 1 : 2);
            Fps = PlayerPrefs.GetInt("fps", 60);
            CamMode = PlayerPrefs.GetInt("cam", 0);
            CrewFollows = PlayerPrefs.GetInt("follow", 0) == 1;
            Sensitivity = PlayerPrefs.GetFloat("sens", 1f);
            StartStop = PlayerPrefs.GetInt("startStop", 0);
            StartChar = PlayerPrefs.GetInt("startChar", 0);
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat("music", Music); PlayerPrefs.SetInt("quality", Quality); PlayerPrefs.SetInt("fps", Fps);
            PlayerPrefs.SetInt("cam", CamMode); PlayerPrefs.SetInt("follow", CrewFollows ? 1 : 0); PlayerPrefs.SetFloat("sens", Sensitivity);
            PlayerPrefs.SetInt("startStop", StartStop); PlayerPrefs.SetInt("startChar", StartChar);
            PlayerPrefs.Save();
        }
    }
}
