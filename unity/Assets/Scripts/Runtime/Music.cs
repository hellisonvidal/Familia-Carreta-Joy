using UnityEngine;

namespace CarretaJoy
{
    /// <summary>
    /// Música da carreta: lista de músicas de verdade (CC0, Resources/Music) tocadas em ordem embaralhada,
    /// com a batida (BPM) de cada uma para as luzes piscarem no ritmo. Se não houver arquivos,
    /// usa a batida sintetizada de reserva.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class Music : MonoBehaviour
    {
        public float bpm = 126f;
        public volatile float volume = 0.55f;
        public bool on = true;
        double sr = 48000;
        long n;
        uint seed = 12345;
        double phK, phB, phL, phP;
        float lp;

        // tamborzão: bumbo em 16 passos
        static readonly int[] KICK = { 1, 0, 0, 1, 0, 0, 1, 0, 0, 0, 1, 0, 1, 0, 0, 0 };
        static readonly int[] CLAP = { 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1 };
        // acordes (raízes em semitons a partir de Dó): C  Am  F  G
        static readonly int[] ROOT = { 0, -3, 5, 7 };
        static readonly int[] BASS = { 0, -1, 0, 12, -1, 0, 7, -1, 0, -1, 12, 0, -1, 7, 12, -1 };
        static readonly int[] MEL = {
            12, -1, 16, -1, 19, 16, -1, 14, 12, -1, 9, -1, 12, 14, 16, -1,
            12, -1, 16, -1, 19, 21, -1, 19, 16, -1, 14, -1, 12, -1, -1, -1,
            17, -1, 16, -1, 14, 12, -1, 14, 16, -1, 19, -1, 16, 14, 12, -1,
            14, -1, 16, -1, 19, -1, 21, 19, 16, -1, 14, -1, 12, -1, -1, -1 };

        public float Beat { get; private set; }
        public string NowPlaying { get; private set; } = "";
        public System.Action<string> OnTrack;

        // músicas de verdade
        readonly System.Collections.Generic.List<(string file, float bpm, string title)> tracks = new System.Collections.Generic.List<(string, float, string)>();
        int[] order; int cur = -1;
        AudioSource src;
        volatile bool synth = true;

        void Awake()
        {
            sr = AudioSettings.outputSampleRate;
            src = GetComponent<AudioSource>();
            src.spatialBlend = 0f; src.volume = 1f;
            var list = Resources.Load<TextAsset>("Music/musicas");
            if (list != null)
                foreach (var line in list.text.Split('\n'))
                {
                    var p = line.Trim().Split(';');
                    if (p.Length < 3) continue;
                    float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float b);
                    tracks.Add((p[0], b > 0 ? b : 120f, p[2]));
                }
            if (tracks.Count > 0)
            {
                synth = false;
                order = new int[tracks.Count];
                for (int i = 0; i < order.Length; i++) order[i] = i;
                for (int i = order.Length - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (order[i], order[j]) = (order[j], order[i]); }
                src.loop = false;
                Next();
            }
            else { src.playOnAwake = true; src.loop = true; src.Play(); }
        }

        /// <summary>Próxima música da lista.</summary>
        public void Next()
        {
            if (synth || tracks.Count == 0) return;
            var old = src.clip;
            cur = (cur + 1) % tracks.Count;
            var t = tracks[order[cur]];
            var clip = Resources.Load<AudioClip>("Music/" + t.file);
            if (clip == null) return;
            src.clip = clip; src.time = 0; src.Play();
            if (old != null && old != clip) Resources.UnloadAsset(old);
            bpm = t.bpm; NowPlaying = t.title;
            OnTrack?.Invoke(t.title);
        }

        void Update()
        {
            if (synth) { Beat = (float)(n / sr * bpm / 60.0); return; }
            src.volume = on ? volume : 0f;
            Beat = src.time * bpm / 60f;
            // acabou: a próxima (o jogo pausado não conta)
            if (!src.isPlaying && src.clip != null && Time.timeScale > 0 && src.time <= 0.01f) Next();
        }

        float Noise() { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed / 4294967295f) * 2f - 1f; }
        static double Hz(int semi) => 261.63 * System.Math.Pow(2, semi / 12.0);

        void OnAudioFilterRead(float[] data, int ch)
        {
            if (!synth) return;   // tocando arquivo: o som passa direto
            float vol = on ? volume : 0f;
            double spb = sr * 60.0 / bpm, sps = spb / 4.0; // amostras por batida / por passo
            for (int i = 0; i < data.Length; i += ch)
            {
                double stepF = n / sps;
                long step = (long)stepF;
                int s16 = (int)(step % 16), bar = (int)((step / 16) % 4), s64 = (int)(step % 64);
                float ts = (float)((stepF - step) * sps / sr); // segundos desde o início do passo
                float o = 0;

                if (KICK[s16] == 1)
                {
                    double f = 45 + 110 * System.Math.Exp(-ts * 28);
                    phK += 2 * System.Math.PI * f / sr;
                    o += (float)System.Math.Sin(phK) * Mathf.Exp(-ts * 9f) * 0.9f;
                }
                else phK = 0;
                if (CLAP[s16] == 1) o += Noise() * Mathf.Exp(-ts * 22f) * 0.35f;
                if (s16 % 2 == 1) { float nz = Noise(); o += (nz - lp) * Mathf.Exp(-ts * 70f) * 0.18f; lp = nz; }

                int bn = BASS[s16];
                if (bn >= 0)
                {
                    double f = Hz(ROOT[bar] + bn - 24);
                    phB += f / sr; phB -= System.Math.Floor(phB);
                    float saw = (float)(phB * 2 - 1);
                    o += saw * Mathf.Exp(-ts * 7f) * 0.28f;
                }
                int mn = MEL[s64];
                if (mn >= 0)
                {
                    double f = Hz(mn + (ROOT[bar] == -3 ? 0 : 0));
                    phL += f / sr; phL -= System.Math.Floor(phL);
                    float tri = (float)(System.Math.Abs(phL * 4 - 2) - 1);
                    float sq = phL < 0.5 ? 0.3f : -0.3f;
                    o += (tri * 0.8f + sq * 0.2f) * Mathf.Exp(-ts * 5f) * 0.22f;
                }
                // acorde de fundo (pad) nos tempos fracos
                if (s16 % 4 == 2)
                {
                    phP += Hz(ROOT[bar] + 4) / sr; phP -= System.Math.Floor(phP);
                    o += (float)System.Math.Sin(phP * 2 * System.Math.PI) * Mathf.Exp(-ts * 10f) * 0.12f;
                }

                o = (float)System.Math.Tanh(o * 1.2f) * vol;
                for (int c = 0; c < ch; c++) data[i + c] = o;
                n++;
            }
        }
    }
}
