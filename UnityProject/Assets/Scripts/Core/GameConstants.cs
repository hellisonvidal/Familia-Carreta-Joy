using UnityEngine;

namespace FamiliaCarretaJoy
{
    /// <summary>
    /// Constants and configuration values ported from the original JavaScript game.
    /// </summary>
    public static class GameConstants
    {
        // City configuration
        public const int CityBlocksPerSide = 6;           // N
        public const float BlockSize = 84f;               // P - distance between streets
        public const float RoadHalfWidth = 7f;            // RW
        public const float SidewalkWidth = 3.5f;          // SW
        public const float HalfCitySize = CityBlocksPerSide * BlockSize / 2f; // HALF
        public const float RingRadius = HalfCitySize - 3.5f; // RING - truck lane radius

        // Truck configuration
        public const float TruckLength = 17f;             // TRUCK_LEN

        // Physics
        public const float Gravity = 24f;                 // GRAV

        // Game timing
        public const float ComboFullThreshold = 1200f;    // COMBO_FULL

        // Character base stats
        public const float BaseWalkSpeed = 3.2f;
        public const float BaseRunSpeed = 6.6f;
        public const float BaseJumpVelocity = 9.8f;

        // Trick definitions (ported from TRICKS in actor.js)
        public static readonly TrickDefinition[] Tricks = new TrickDefinition[]
        {
            new TrickDefinition("mortal", "MORTAL", 0.58f, 100),
            new TrickDefinition("flip", "FLIP", 0.68f, 150),
            new TrickDefinition("estrela", "ESTRELA", 0.76f, 90),
            new TrickDefinition("pirueta", "PIRUETA", 0.44f, 70),
        };

        // Dance names (ported from DANCE_NAMES in actor.js)
        public static readonly string[] DanceNames = new string[]
        {
            "Hip Hop", "Swing", "Break", "Gangnam"
        };

        // Dance clips (ported from DANCE_CLIPS in actor.js)
        public static readonly string[] DanceClips = new string[]
        {
            "danca", "danca2", "danca3", "danca4"
        };

        // Cities configuration (ported from cities.js)
        public static readonly CityDefinition[] Cities = new CityDefinition[]
        {
            new CityDefinition
            {
                id = "sp",
                nome = "São Paulo",
                uf = "SP",
                descricao = "A maior cidade do Brasil, com a Paulista, MASP e muito trânsito!",
                musica = 0, // Funk
                relevo = 12f,
                ceuDia = new string[] { "#87CEEB", "#E0F0FF" },
                ceuNoite = new string[] { "#1a1a2e", "#16213e" },
                nevoa = 0.002f,
                chao = "#b8b5ad",
                calcada = "petit",
                predios = new BuildingStyle { tipo = "torre", altMin = 18, altMax = 50, cores = new int[] { 0x8a8d94, 0x7a7d84, 0x9a9da4, 0x6a6d74 } },
                mar = null,
                arvores = "ipê",
                marcos = new string[] { "masp" }
            },
            new CityDefinition
            {
                id = "rio",
                nome = "Rio de Janeiro",
                uf = "RJ",
                descricao = "Cidade maravilhosa com praia, Pão de Açúcar e Cristo Redentor!",
                musica = 1, // Samba
                relevo = 35f,
                ceuDia = new string[] { "#87CEEB", "#E0F0FF" },
                ceuNoite = new string[] { "#1a1a2e", "#16213e" },
                nevoa = 0.003f,
                chao = "#c8c5bd",
                calcada = "petit",
                predios = new BuildingStyle { tipo = "casarao", altMin = 12, altMax = 35, cores = new int[] { 0xfff8e7, 0xffe0b2, 0xf8bbd0, 0xb3e5fc, 0xc8e6c9 } },
                mar = new SeaDefinition { lado = "sul", cor = "#1f6f9a" },
                arvores = "coqueiro",
                marcos = new string[] { "paodeacucar", "cristo" }
            },
            new CityDefinition
            {
                id = "bh",
                nome = "Belo Horizonte",
                uf = "MG",
                descricao = "Capital mineira com a Pampulha, ladeiras e muito queijo!",
                musica = 2, // Sertanejo
                relevo = 40f,
                ceuDia = new string[] { "#87CEEB", "#E0F0FF" },
                ceuNoite = new string[] { "#1a1a2e", "#16213e" },
                nevoa = 0.0025f,
                chao = "#c8c5bd",
                calcada = "petit",
                predios = new BuildingStyle { tipo = "torre", altMin = 15, altMax = 40, cores = new int[] { 0x8a8d94, 0x7a7d84, 0x9a9da4 } },
                mar = null,
                arvores = "ipê",
                marcos = new string[] { "pampulha" }
            },
            new CityDefinition
            {
                id = "ssa",
                nome = "Salvador",
                uf = "BA",
                descricao = "Terra do axé, pelourinho, acarajé e farol da Barra!",
                musica = 3, // Axé
                relevo = 25f,
                ceuDia = new string[] { "#87CEEB", "#E0F0FF" },
                ceuNoite = new string[] { "#1a1a2e", "#16213e" },
                nevoa = 0.002f,
                chao = "#e6d29a",
                calcada = "petit",
                predios = new BuildingStyle { tipo = "casarao", altMin = 10, altMax = 30, cores = new int[] { 0xfff8e7, 0xffe0b2, 0xf8bbd0, 0xb3e5fc } },
                mar = new SeaDefinition { lado = "leste", cor = "#1f6f9a" },
                arvores = "coqueiro",
                marcos = new string[] { "igreja" }
            },
            new CityDefinition
            {
                id = "cwb",
                nome = "Curitiba",
                uf = "PR",
                descricao = "Cidade ecológica com jardim botânico, óperas de arame e muito pinheiro!",
                musica = 4, // Rock
                relevo = 45f,
                ceuDia = new string[] { "#87CEEB", "#E0F0FF" },
                ceuNoite = new string[] { "#1a1a2e", "#16213e" },
                nevoa = 0.004f,
                chao = "#b8b5ad",
                calcada = "petit",
                predios = new BuildingStyle { tipo = "torre", altMin = 15, altMax = 40, cores = new int[] { 0x8a8d94, 0x7a7d84, 0x9a9da4, 0x6a6d74 } },
                mar = null,
                arvores = "araucaria",
                marcos = new string[] { "estufa" }
            }
        };

        // Truck variants (ported from VARIANTS in truck.js)
        public static readonly TruckVariant[] TruckVariants = new TruckVariant[]
        {
            new TruckVariant { id = "arcoiris", nome = "Arco-Íris", desc = "A original, todas as cores", filtro = null, hue = new float[] { 0f, 1f }, extra = null, cor = "#ff3d7f" },
            new TruckVariant { id = "dourada", nome = "Furacão Dourada", desc = "Brilha igual troféu", filtro = "sepia(1) saturate(2.6) hue-rotate(-12deg) brightness(1.1)", hue = new float[] { 0.1f, 0.06f }, extra = "estrela", cor = "#ffc400" },
            new TruckVariant { id = "neon", nome = "Neon Azul", desc = "Tubos de neon nas laterais", filtro = "hue-rotate(170deg) saturate(1.5)", hue = new float[] { 0.5f, 0.25f }, extra = "neon", cor = "#00e5ff" },
            new TruckVariant { id = "rosa", nome = "Rosa Choque", desc = "Com balões de coração", filtro = "sepia(1) saturate(4) hue-rotate(285deg) brightness(1.05)", hue = new float[] { 0.9f, 0.12f }, extra = "coracoes", cor = "#ff4fb8" },
            new TruckVariant { id = "brasil", nome = "Verde-Amarela", desc = "Bandeiras do Brasil", filtro = "sepia(1) saturate(3.5) hue-rotate(40deg)", hue = new float[] { 0.2f, 0.15f }, extra = "bandeiras", cor = "#00c853" },
            new TruckVariant { id = "trio", nome = "Carreta Paredão", desc = "Torre de som no teto", filtro = "grayscale(0.5) contrast(1.3) brightness(0.85)", hue = new float[] { 0.75f, 0.35f }, extra = "torre", cor = "#455a64" },
            new TruckVariant { id = "trioGigante", design = "trioGigante", nome = "Trio Elétrico Gigante", desc = "Paredões de som e palco no alto", hue = new float[] { 0.75f, 0.35f }, extra = null, cor = "#7c4dff" },
            new TruckVariant { id = "onibus2", design = "onibus2", nome = "Ônibus da Alegria", desc = "Dois andares, andar de cima aberto", hue = new float[] { 0.08f, 0.2f }, extra = null, cor = "#ff6d00" },
            new TruckVariant { id = "palco", design = "palco", nome = "Caminhão-Palco", desc = "Palco com treliça e painel de LED", hue = new float[] { 0.95f, 0.3f }, extra = null, cor = "#e53935" }
        };

        // Duration options (ported from DURATIONS in cities.js)
        public static readonly int[] Durations = new int[] { 5, 10, 15, 20 };

        // Music styles (ported from STYLES in music.js)
        public static readonly MusicStyle[] MusicStyles = new MusicStyle[]
        {
            new MusicStyle { id = 0, nome = "Funk", bpm = 130, key = "C", scale = new int[] { 0, 2, 3, 5, 7, 8, 10 } },
            new MusicStyle { id = 1, nome = "Samba", bpm = 140, key = "D", scale = new int[] { 0, 2, 4, 5, 7, 9, 11 } },
            new MusicStyle { id = 2, nome = "Sertanejo", bpm = 120, key = "G", scale = new int[] { 0, 2, 4, 5, 7, 9, 11 } },
            new MusicStyle { id = 3, nome = "Axé", bpm = 135, key = "A", scale = new int[] { 0, 2, 4, 5, 7, 9, 11 } },
            new MusicStyle { id = 4, nome = "Rock", bpm = 150, key = "E", scale = new int[] { 0, 2, 3, 5, 7, 8, 10 } }
        };
    }

    /// <summary>
    /// Trick definition matching the original JavaScript TRICKS object.
    /// </summary>
    [System.Serializable]
    public struct TrickDefinition
    {
        public string id;
        public string nome;
        public float dur;
        public int pts;

        public TrickDefinition(string id, string nome, float dur, int pts)
        {
            this.id = id;
            this.nome = nome;
            this.dur = dur;
            this.pts = pts;
        }

        public static TrickDefinition GetById(string id)
        {
            foreach (var t in GameConstants.Tricks)
            {
                if (t.id == id) return t;
            }
            return GameConstants.Tricks[0];
        }
    }

    /// <summary>
    /// City definition matching the original cities.js structure.
    /// </summary>
    [System.Serializable]
    public class CityDefinition
    {
        public string id;
        public string nome;
        public string uf;
        public string descricao;
        public int musica;
        public float relevo;
        public string[] ceuDia;
        public string[] ceuNoite;
        public float nevoa;
        public string chao;
        public string calcada;
        public BuildingStyle predios;
        public SeaDefinition mar;
        public string arvores;
        public string[] marcos;
    }

    [System.Serializable]
    public class BuildingStyle
    {
        public string tipo;
        public int altMin;
        public int altMax;
        public int[] cores;
    }

    [System.Serializable]
    public class SeaDefinition
    {
        public string lado;
        public string cor;
    }

    /// <summary>
    /// Truck variant definition matching the original truck.js VARIANTS.
    /// </summary>
    [System.Serializable]
    public class TruckVariant
    {
        public string id;
        public string nome;
        public string desc;
        public string filtro;
        public float[] hue;
        public string extra;
        public string cor;
        public string design; // For custom designs like trioGigante, onibus2, palco
    }

    /// <summary>
    /// Music style definition matching the original music.js STYLES.
    /// </summary>
    [System.Serializable]
    public class MusicStyle
    {
        public int id;
        public string nome;
        public int bpm;
        public string key;
        public int[] scale;
    }
}