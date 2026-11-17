using UnityEngine;

namespace CarretaJoy
{
    /// <summary>Ajustes do jogador, guardados no aparelho.</summary>
    public static class Settings
    {
        public static float Music = 0.7f;
        public static int Quality = 1;        // 0 leve, 1 médio, 2 alto
        public static int Fps = 60;
        public static int CamMode = 0;        // 0 TPS (ombro), 1 normal, 2 perto, 3 longe, 4 aérea
        public static int CrewMode = 0;       // 0 vão na carreta, 1 me seguem, 2 ficam parados
        public static bool CrewFollows => CrewMode == 1;
        public static float Sensitivity = 1f;
        public static int StartStop = 0;
        public static int StartChar = 0;
        public static int Truck = 0;          // variação da carreta (Truck.Variants)
        public static int TruckSpeed = 0;     // 0 devagar (a turma acompanha a pé), 1 normal, 2 rápida
        public static readonly string[] TruckSpeedNames = { "Devagar", "Normal", "Rápida" };
        public static float TruckSpeedValue => TruckSpeed == 0 ? 3.2f : TruckSpeed == 1 ? 5.5f : 9f;

        public static readonly string[] QualityNames = { "Leve", "Médio", "Alto" };
        public static readonly string[] CamNames = { "TPS", "Normal", "Perto", "Longe", "Aérea" };
        public static readonly string[] CrewNames = { "Na carreta", "Me seguem", "Parados" };

        public static void Load()
        {
            Music = PlayerPrefs.GetFloat("music", Music);
            Quality = PlayerPrefs.GetInt("quality", Application.isMobilePlatform ? 1 : 2);
            Fps = PlayerPrefs.GetInt("fps", 60);
            CamMode = Mathf.Clamp(PlayerPrefs.GetInt("cam2", 0), 0, 4);
            CrewMode = Mathf.Clamp(PlayerPrefs.GetInt("crew", 0), 0, 2);
            Sensitivity = PlayerPrefs.GetFloat("sens", 1f);
            StartStop = PlayerPrefs.GetInt("startStop", 0);
            StartChar = PlayerPrefs.GetInt("startChar", 0);
            Truck = PlayerPrefs.GetInt("truck", 0);
            TruckSpeed = Mathf.Clamp(PlayerPrefs.GetInt("truckSpeed", 0), 0, 2);
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat("music", Music); PlayerPrefs.SetInt("quality", Quality); PlayerPrefs.SetInt("fps", Fps);
            PlayerPrefs.SetInt("cam2", CamMode); PlayerPrefs.SetInt("crew", CrewMode); PlayerPrefs.SetFloat("sens", Sensitivity);
            PlayerPrefs.SetInt("startStop", StartStop); PlayerPrefs.SetInt("startChar", StartChar);
            PlayerPrefs.SetInt("truck", Truck); PlayerPrefs.SetInt("truckSpeed", TruckSpeed);
            PlayerPrefs.Save();
        }
    }
}
