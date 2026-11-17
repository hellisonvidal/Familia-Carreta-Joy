using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace FamiliaCarretaJoy
{
    /// <summary>
    /// Extension methods and static data access for GameConstants.
    /// </summary>
    public static class GameConstantsExtensions
    {
        private static CharacterDefinition[] characterDefinitions;
        private static CostumeDefinition[] costumeDefinitions;
        private static Dictionary<string, CostumeDefinition> costumeDict;

        public static CharacterDefinition[] GetCharacterDefinitions()
        {
            if (characterDefinitions == null)
                characterDefinitions = CreateCharacterDefinitions();
            return characterDefinitions;
        }

        public static CharacterDefinition GetCharacterDefinition(string id)
        {
            return GetCharacterDefinitions().FirstOrDefault(c => c.id == id) ?? GetCharacterDefinitions()[0];
        }

        public static CostumeDefinition GetCostume(string id)
        {
            if (costumeDict == null)
                InitializeCostumes();
            return costumeDict.ContainsKey(id) ? costumeDict[id] : costumeDict["normal"];
        }

        public static string GetCostumeName(string id)
        {
            var costume = GetCostume(id);
            return costume != null ? costume.nome : id;
        }

        public static TruckVariant GetTruckVariant(string id)
        {
            return TruckVariants.FirstOrDefault(v => v.id == id) ?? TruckVariants[0];
        }

        private static CharacterDefinition[] CreateCharacterDefinitions()
        {
            return new CharacterDefinition[]
            {
                new CharacterDefinition
                {
                    id = "arthur",
                    nome = "Arthur",
                    kid = true,
                    altura = 1.32f,
                    cabeca = "arthurHead",
                    corpo = "arthurBody",
                    rig = "arthurRig",
                    roupa = "arthur",
                    estilo = 0
                },
                new CharacterDefinition
                {
                    id = "afonso",
                    nome = "Afonso",
                    kid = true,
                    altura = 1.18f,
                    cabeca = "afonsoHead",
                    corpo = "afonsoBody",
                    rig = "afonsoRig",
                    roupa = "afonso",
                    estilo = 1,
                    poseInicial = true
                },
                new CharacterDefinition
                {
                    id = "barril",
                    nome = "Chaves",
                    altura = 1.64f,
                    rig = "chavesRig",
                    roupa = "barril",
                    estilo = 2,
                    pele = 0xf1c6a3,
                    olho = 0x6b4423,
                    rosto = new CharacterDefinition.FaceDefinition
                    {
                        pele = 0xf1c6a3,
                        sardas = true,
                        sobrancelha = 0x5a3b22
                    },
                    cabelo = 0x8a4b2a
                },
                new CharacterDefinition
                {
                    id = "bochecha",
                    nome = "Fofão",
                    altura = 1.7f,
                    rig = "fofaoRig",
                    roupa = "bochecha",
                    estilo = 3,
                    pele = 0xf3cfb3,
                    olho = 0x3b6e8f,
                    rosto = new CharacterDefinition.FaceDefinition
                    {
                        pele = 0xf3cfb3,
                        bochechas = true,
                        nariz = 0xff7a8a,
                        boca = "sorriso"
                    },
                    bochechas = true,
                    cabelo = 0x2a1a12,
                    estiloCabelo = "longo"
                },
                new CharacterDefinition
                {
                    id = "vermelho",
                    nome = "Chapolin",
                    altura = 1.66f,
                    rig = "chapolinRig",
                    roupa = "vermelho",
                    estilo = 4,
                    pele = 0xd8a27c,
                    olho = 0x4a2e1a,
                    rosto = new CharacterDefinition.FaceDefinition
                    {
                        pele = 0xd8a27c,
                        mascara = new CharacterDefinition.MaskDefinition
                        {
                            tipo = "capuz",
                            cor = 0xd81e1e
                        }
                    }
                },
                new CharacterDefinition
                {
                    id = "caraverde",
                    nome = "Máscara",
                    altura = 1.8f,
                    rig = "mascaraRig",
                    roupa = "caraverde",
                    estilo = 5,
                    pele = 0xe2b48c,
                    olho = 0x2f6b2f,
                    rosto = new CharacterDefinition.FaceDefinition
                    {
                        pele = 0x7ed321,
                        boca = "dentes",
                        sobrancelha = 0x2f5d10
                    },
                    cabecaGrande = 1.08f
                },
                new CharacterDefinition
                {
                    id = "teia",
                    nome = "Homem-Aranha",
                    altura = 1.78f,
                    rig = "aranhaRig",
                    roupa = "teia",
                    estilo = 6,
                    pele = 0xd5212b,
                    rosto = new CharacterDefinition.FaceDefinition
                    {
                        pele = 0xd5212b,
                        mascara = new CharacterDefinition.MaskDefinition
                        {
                            tipo = "total",
                            cor = 0xd5212b,
                            teia = true
                        },
                        olhos = "lente"
                    }
                },
                new CharacterDefinition
                {
                    id = "capitao",
                    nome = "Capitão América",
                    altura = 1.86f,
                    forte = 1.2f,
                    rig = "capitaoRig",
                    roupa = "capitao",
                    estilo = 7,
                    pele = 0xf0c8a4,
                    olho = 0x3d6fb3,
                    rosto = new CharacterDefinition.FaceDefinition
                    {
                        pele = 0xf0c8a4,
                        mascara = new CharacterDefinition.MaskDefinition
                        {
                            tipo = "capuz",
                            cor = 0x1f4fbf
                        },
                        bravo = true
                    }
                },
                new CharacterDefinition
                {
                    id = "gigante",
                    nome = "Hulk",
                    altura = 2.2f,
                    forte = 1.65f,
                    rig = "hulkRig",
                    roupa = "gigante",
                    estilo = 8,
                    pele = 0x5fae3a,
                    olho = 0x3a5a1a,
                    rosto = new CharacterDefinition.FaceDefinition
                    {
                        pele = 0x5fae3a,
                        boca = "bravo",
                        bravo = true,
                        sobrancelha = 0x1b3d0c
                    },
                    cabelo = 0x1b1b1b,
                    estiloCabelo = "curto"
                }
            };
        }

        private static void InitializeCostumes()
        {
            costumeDefinitions = new CostumeDefinition[]
            {
                new CostumeDefinition { id = "arthur", nome = "Roupa do Arthur", tronco = 0x1736c9, manga = "curta", bracos = 0xf3c9a5, maos = 0xf3c9a5, calca = 0x0d1748, faixa = 0x1f7a52, sapatos = 0x1a1a1a },
                new CostumeDefinition { id = "afonso", nome = "Roupa do Afonso", tronco = 0x1736c9, manga = "longa", bracos = 0xb8bcc2, maos = 0xf3c9a5, calca = 0x14161c, faixa = 0x1f7a52, sapatos = 0xd8263a, sola = 0xffffff },
                new CostumeDefinition { id = "barril", nome = "Moleque do Barril", tronco = 0xe9dcc0, manga = "curta", bracos = 0xf3c9a5, maos = 0xf3c9a5, calca = 0x8a6a3d, sapatos = 0x5a3b22, chapéu = "bone_xadrez" },
                new CostumeDefinition { id = "bochecha", nome = "Bochechudo", tronco = 0xffffff, manga = "curta", bracos = 0xf3c9a5, maos = 0xf3c9a5, calca = 0x1e3a8a, sapatos = 0x5d4037, jardineira = 0x1e3a8a, laco = 0xffc400 },
                new CostumeDefinition { id = "vermelho", nome = "Herói Vermelho", tronco = 0xd81e1e, manga = "longa", bracos = 0xd81e1e, maos = 0xd81e1e, calca = 0xd81e1e, shorts = 0xffd400, sapatos = 0xffd400, emblema = "coracao", chapeu = "antenas", capuz = "0xd81e1e" },
                new CostumeDefinition { id = "caraverde", nome = "Cara Verde", tronco = 0xffc400, manga = "longa", bracos = 0xffc400, maos = 0x7ed321, calca = 0xffc400, sapatos = 0x7a4a2a, gravata = 0xe53935, chapeu = "chapeu_amarelo" },
                new CostumeDefinition { id = "teia", nome = "Herói Teia", tronco = 0xd5212b, manga = "longa", bracos = 0xd5212b, maos = 0xd5212b, calca = 0x1f4fbf, sapatos = 0xd5212b, emblema = "aranha" },
                new CostumeDefinition { id = "capitao", nome = "Capitão Escudo", tronco = 0x1f4fbf, manga = "longa", bracos = 0x1f4fbf, maos = 0xd32f2f, calca = 0x1f4fbf, sapatos = 0xd32f2f, emblema = "estrela", cinto = 0x7a5a2a, escudo = true, chapeu = "capacete", listras = true },
                new CostumeDefinition { id = "gigante", nome = "Gigante Verde", tronco = 0x4caf2f, manga = "nenhuma", bracos = 0x4caf2f, maos = 0x4caf2f, calca = 0x6a2c91, shorts = 0x6a2c91, pernasPele = 0x4caf2f, sapatos = 0x4caf2f, rasgado = true }
            };

            costumeDict = new Dictionary<string, CostumeDefinition>();
            foreach (var c in costumeDefinitions)
            {
                costumeDict[c.id] = c;
            }
            // Add normal as fallback
            costumeDict["normal"] = costumeDefinitions[0];
        }
    }

    /// <summary>
    /// Dance pad constants.
    /// </summary>
    public static class DanceConstants
    {
        public static readonly DanceController.DancePad[] DancePads = new DanceController.DancePad[]
        {
            new DanceController.DancePad { index = 0, label = "←", nome = "ESQUERDA", cor = "#ff3d7f", direction = Vector2.left },
            new DanceController.DancePad { index = 1, label = "↑", nome = "CIMA", cor = "#ffc400", direction = Vector2.up },
            new DanceController.DancePad { index = 2, label = "↓", nome = "BAIXO", cor = "#00c853", direction = Vector2.down },
            new DanceController.DancePad { index = 3, label = "→", nome = "DIREITA", cor = "#2979ff", direction = Vector2.right }
        };
    }
}