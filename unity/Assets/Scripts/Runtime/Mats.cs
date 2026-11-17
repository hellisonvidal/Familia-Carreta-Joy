using System.Collections.Generic;
using UnityEngine;

namespace CarretaJoy
{
    /// <summary>
    /// Materiais criados em tempo de execução a partir de modelos salvos em Resources/Materials
    /// (o Builder do editor cria esses modelos, assim as variantes de shader entram no APK).
    /// </summary>
    public static class Mats
    {
        static Material lit, glow;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        public static readonly List<Material> NightGlows = new List<Material>();   // acendem à noite
        static readonly List<Color> nightBase = new List<Color>();
        static bool isNight;

        static Material Template(ref Material t, string name)
        {
            if (t == null)
            {
                t = Resources.Load<Material>("Materials/" + name);
                if (t == null) t = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            }
            return t;
        }

        public static Material Lit(Color c, float smooth = 0.25f, float metal = 0f)
        {
            string k = "L" + ColorUtility.ToHtmlStringRGBA(c) + smooth.ToString("0.00") + metal.ToString("0.00");
            if (cache.TryGetValue(k, out var m)) return m;
            m = new Material(Template(ref lit, "Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            cache[k] = m;
            return m;
        }

        /// <summary>Material que brilha (luzes, janelas, neon). intensity em HDR (>1 acende o bloom).</summary>
        public static Material Glow(Color c, float intensity, bool nightOnly = false)
        {
            var m = new Material(Template(ref glow, "Glow"));
            m.SetColor("_BaseColor", c * 0.6f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            SetGlow(m, c, intensity);
            if (nightOnly) { NightGlows.Add(m); nightBase.Add(c * intensity); if (!isNight) m.SetColor("_EmissionColor", c * intensity * 0.04f); }
            return m;
        }

        public static void SetGlow(Material m, Color c, float intensity)
        {
            m.SetColor("_EmissionColor", c * intensity);
        }

        /// <summary>Janelas e postes: fracos de dia, fortes de noite.</summary>
        public static void ApplyNight(bool night)
        {
            isNight = night;
            for (int i = 0; i < NightGlows.Count; i++)
                NightGlows[i].SetColor("_EmissionColor", nightBase[i] * (night ? 1f : 0.04f));
        }
    }
}
