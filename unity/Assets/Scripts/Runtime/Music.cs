using UnityEngine;

namespace CarretaJoy
{
    /// <summary>
    /// Música da carreta gerada na hora (sintetizador): batida de funk/pop animada,
    /// baixo, palmas, chimbal e uma melodia alegre. Não precisa de arquivo de áudio.
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

        void Awake()
        {
            sr = AudioSettings.outputSampleRate;
            var src = GetComponent<AudioSource>();
            src.playOnAwake = true; src.loop = true; src.spatialBlend = 0f; src.volume = 1f;
            src.Play();
        }

        void Update() { Beat = (float)(n / sr * bpm / 60.0); }

        float Noise() { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return (seed / 4294967295f) * 2f - 1f; }
        static double Hz(int semi) => 261.63 * System.Math.Pow(2, semi / 12.0);

        void OnAudioFilterRead(float[] data, int ch)
        {
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
