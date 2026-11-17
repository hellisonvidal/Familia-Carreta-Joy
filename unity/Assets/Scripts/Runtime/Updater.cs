using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace CarretaJoy
{
    /// <summary>
    /// Atualizações por dentro do jogo: procura a última versão "unity-vX.Y.Z" no GitHub,
    /// baixa dentro do jogo (sem navegador) e instala. Android: PackageInstaller (plugin Java);
    /// Windows: abre o instalador novo e fecha o jogo.
    /// </summary>
    public class Updater : MonoBehaviour
    {
        const string API = "https://api.github.com/repos/hellisonvidal/Familia-Carreta-Joy/releases?per_page=20";
        public string Latest, Url, Status = "";
        public float Progress;
        public bool Busy, Available, Checked;

        [Serializable] class Asset { public string name; public string browser_download_url; public long size; }
        [Serializable] class Release { public string tag_name; public bool draft; public Asset[] assets; }
        [Serializable] class List { public Release[] items; }

        public static Version Parse(string s)
        {
            s = s.Replace("unity-v", "").Replace("v", "").Trim();
            return Version.TryParse(s, out var v) ? v : new Version(0, 0, 0);
        }

        public void Check() { if (!Busy) StartCoroutine(CheckCo()); }

        IEnumerator CheckCo()
        {
            Busy = true; Status = "Procurando atualização...";
            using (var r = UnityWebRequest.Get(API))
            {
                r.SetRequestHeader("Accept", "application/vnd.github+json");
                r.timeout = 20;
                yield return r.SendWebRequest();
                Busy = false; Checked = true;
                if (r.result != UnityWebRequest.Result.Success) { Status = "Sem internet para procurar atualização"; yield break; }
                List list;
                try { list = JsonUtility.FromJson<List>("{\"items\":" + r.downloadHandler.text + "}"); }
                catch (Exception e) { Status = "Erro ao ler versões: " + e.Message; yield break; }
                var cur = Parse(Application.version); Version best = cur; Release bestR = null;
                bool win = Application.platform == RuntimePlatform.WindowsPlayer;
                foreach (var rel in list.items ?? new Release[0])
                {
                    if (rel.draft || rel.tag_name == null || !rel.tag_name.StartsWith("unity-v")) continue;
                    var v = Parse(rel.tag_name);
                    if (v <= best) continue;
                    foreach (var a in rel.assets ?? new Asset[0])
                        if (win ? a.name.EndsWith(".exe") : a.name.EndsWith(".apk")) { best = v; bestR = rel; Url = a.browser_download_url; }
                }
                Available = bestR != null;
                Latest = Available ? best.ToString() : null;
                Status = Available ? "Nova versão " + Latest + " disponível!" : "Você já tem a versão mais nova (" + Application.version + ")";
            }
        }

        public void DownloadAndInstall() { if (!Busy && Available) StartCoroutine(InstallCo()); }

        IEnumerator InstallCo()
        {
            Busy = true; Progress = 0;
            bool win = Application.platform == RuntimePlatform.WindowsPlayer;
            string dir = Path.Combine(win ? Path.GetTempPath() : Application.persistentDataPath, "atualizacao");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, win ? "CarretaJoy3D-Setup.exe" : "CarretaJoy3D.apk");
            if (File.Exists(file)) File.Delete(file);
            Status = "Baixando versão " + Latest + "...";
            using (var r = new UnityWebRequest(Url, UnityWebRequest.kHttpVerbGET))
            {
                r.downloadHandler = new DownloadHandlerFile(file) { removeFileOnAbort = true };
                r.redirectLimit = 10; r.timeout = 0;
                var op = r.SendWebRequest();
                while (!op.isDone) { Progress = r.downloadProgress; Status = "Baixando " + Mathf.RoundToInt(Progress * 100) + "%"; yield return null; }
                if (r.result != UnityWebRequest.Result.Success) { Status = "Falha ao baixar: " + r.error; Busy = false; yield break; }
            }
            Progress = 1;
            if (win)
            {
                Status = "Abrindo o instalador...";
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file) { UseShellExecute = true }); Application.Quit(); }
                catch (Exception e) { Status = "Erro ao abrir o instalador: " + e.Message; }
                Busy = false; yield break;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var up = new AndroidJavaClass("com.familia.carretajoy.CarretaUpdater"))
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var act = player.GetStatic<AndroidJavaObject>("currentActivity");
                if (!up.CallStatic<bool>("canInstall", act))
                {
                    Status = "Permita \"instalar apps\" para a Carreta Joy 3D e volte ao jogo";
                    up.CallStatic("openInstallPermission", act);
                    float t = 0;
                    while (!up.CallStatic<bool>("canInstall", act) && t < 120f) { t += 0.5f; yield return new WaitForSecondsRealtime(0.5f); }
                    if (!up.CallStatic<bool>("canInstall", act)) { Status = "Sem permissão para instalar"; Busy = false; yield break; }
                }
                Status = "Instalando...";
                up.CallStatic("install", act, file);
                float w = 0;
                while (w < 300f)
                {
                    string st = up.CallStatic<string>("getStatus");
                    if (st == "confirmar") Status = "Toque em ATUALIZAR na janela do Android";
                    else if (st.StartsWith("erro")) { Status = "Não instalou: " + st; break; }
                    else if (st == "ok") { Status = "Atualizado! Abra o jogo de novo."; break; }
                    w += 0.5f; yield return new WaitForSecondsRealtime(0.5f);
                }
            }
#else
            Status = "Arquivo baixado em " + file;
#endif
            Busy = false;
        }
    }
}
