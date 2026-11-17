using UnityEngine;

namespace FamiliaCarretaJoy
{
    /// <summary>
    /// Character definition matching the original CHARACTERS array in characters.js.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterDefinition", menuName = "Familia Carreta Joy/Character Definition")]
    public class CharacterDefinition : ScriptableObject
    {
        public string id;
        public string nome;
        public bool kid = false;
        public float altura = 1.7f;
        public float forte = 1f;
        public string cabeca;      // Head model name (for kids)
        public string corpo;       // Body model name (for kids)
        public string rig;         // Rig model name (Mixamo)
        public string roupa;       // Costume key
        public int estilo = 0;     // Dance style
        public bool poseInicial = false; // Afonso's initial crouched pose

        // Visual properties (for adults)
        public int pele = 0xf3c9a5;
        public int olho = 0x3b2a1a;
        public FaceDefinition rosto;
        public int cabelo = 0x1b1b1b;
        public string estiloCabelo = "curto";
        public float cabecaGrande = 1f;
        public bool bochechas = false;
    }

    [System.Serializable]
    public class FaceDefinition
    {
        public int pele = 0xf3c9a5;
        public bool sardas = false;
        public int sobrancelha = 0x3b2a1a;
        public string olhos = "normal"; // "normal", "lente"
        public bool olhosGrandes = false;
        public bool semSobrancelha = false;
        public string boca = "normal"; // "normal", "dentes", "bravo", "sorriso"
        public bool bravo = false;
        public bool bochechas = false;
        public int nariz = 0;
        public MaskDefinition mascara;
    }

    [System.Serializable]
    public class MaskDefinition
    {
        public string tipo = "none"; // "none", "total", "olhos", "capuz"
        public int cor = 0;
        public bool teia = false;
    }

    /// <summary>
    /// Costume definition matching the original COSTUMES object in characters.js.
    /// </summary>
    [CreateAssetMenu(fileName = "CostumeDefinition", menuName = "Familia Carreta Joy/Costume Definition")]
    public class CostumeDefinition : ScriptableObject
    {
        public string id;
        public string nome;

        // Colors as hex integers
        public int tronco = 0x1736c9;
        public string manga = "curta"; // "curta", "longa", "nenhuma"
        public int bracos = 0xf3c9a5;
        public int maos = 0xf3c9a5;
        public int calca = 0x0d1748;
        public int faixa = 0x1f7a52;
        public int sapatos = 0x1a1a1a;
        public int sola = 0;

        // Pattern textures
        public string troncoPattern = "";
        public int patternColorA = 0;
        public int patternColorB = 0;

        // Extras
        public int shorts = 0;
        public bool calcaLonga = false;
        public int pernasPele = 0;
        public string emblema = "";
        public bool escudo = false;
        public int cinto = 0;
        public bool suspensorio = false;
        public int suspensorioColor = 0;
        public bool gravata = false;
        public int gravataColor = 0;
        public bool gola = false;
        public int golaColor = 0;
        public bool ombreiras = false;
        public int ombreirasColor = 0;
        public bool rasgado = false;
        public string chapeu = "";
        public bool listras = false;
        public string capuz = "";
        public int capuzColor = 0;
    }

    /// <summary>
    /// Static database of all character definitions.
    /// </summary>
    public static class CharacterDatabase
    {
        public static readonly string[] KidCostumes = new string[]
        {
            "normal", "barril", "bochecha", "vermelho", "caraverde", "teia", "capitao", "gigante"
        };

        public static readonly CharacterDefinition[] Characters = new CharacterDefinition[]
        {
            // Note: These would be created as ScriptableObjects in Unity
            // This is just the data structure definition
        };
    }
}