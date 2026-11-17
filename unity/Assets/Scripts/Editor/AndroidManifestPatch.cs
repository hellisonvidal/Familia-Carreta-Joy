#if UNITY_ANDROID
using System.IO;
using UnityEditor.Android;

namespace CarretaJoy.EditorTools
{
    /// <summary>Permissões do atualizador no AndroidManifest gerado (instalar a versão nova do próprio jogo).</summary>
    public class AndroidManifestPatch : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 10;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string mf = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(mf)) return;
            string s = File.ReadAllText(mf);
            string[] perms = { "android.permission.REQUEST_INSTALL_PACKAGES", "android.permission.UPDATE_PACKAGES_WITHOUT_USER_ACTION", "android.permission.INTERNET" };
            foreach (var p in perms)
            {
                if (s.Contains(p)) continue;
                int i = s.IndexOf("<application");
                if (i < 0) continue;
                s = s.Insert(i, "<uses-permission android:name=\"" + p + "\" />\n  ");
            }
            File.WriteAllText(mf, s);
        }
    }
}
#endif
