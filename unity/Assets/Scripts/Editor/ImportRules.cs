using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CarretaJoy.EditorTools
{
    /// <summary>
    /// Regras de importação: meninos e animações como Humanoid (o Unity adapta qualquer animação
    /// Mixamo a qualquer corpo, sem deformar), carreta como modelo comum, texturas com qualidade.
    /// </summary>
    public class ImportRules : AssetPostprocessor
    {
        static bool IsChar(string p) => p.Contains("/Resources/Characters/") || p.Contains("/Resources/People/");
        static bool IsAnim(string p) => p.Contains("/Resources/Animations/");
        static bool IsVehicle(string p) => p.Contains("/Resources/Vehicles/") || p.Contains("/Resources/Traffic/");

        static readonly string[] Loops = { "Idle", "Walk", "Run", "Danca_Gangnam", "Arthur_AndarCorrer" };

        void OnPreprocessModel()
        {
            var mi = (ModelImporter)assetImporter;
            if (IsChar(assetPath))
            {
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = false;
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.importBlendShapes = false;
                mi.meshCompression = ModelImporterMeshCompression.Off;
                mi.optimizeGameObjects = false;
                mi.importNormals = ModelImporterNormals.Import;
                mi.importTangents = ModelImporterTangents.CalculateMikk;
                mi.isReadable = false;
                mi.skinWeights = ModelImporterSkinWeights.Standard;
            }
            else if (IsAnim(assetPath))
            {
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = true;
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.animationCompression = ModelImporterAnimationCompression.Optimal;
                mi.resampleCurves = true;
            }
            else if (assetPath.Contains("/Resources/Houses/"))
            {
                // casas: o jogo lê os vértices para juntar com o resto do quarteirão
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.isReadable = true;
                mi.meshCompression = ModelImporterMeshCompression.Off;
            }
            else if (IsVehicle(assetPath))
            {
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.meshCompression = ModelImporterMeshCompression.Low;
                mi.importTangents = ModelImporterTangents.CalculateMikk;
                mi.isReadable = false;
            }
        }

        void OnPreprocessAnimation()
        {
            if (!IsAnim(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool loop = Loops.Contains(file) || file.EndsWith("_Loop");
            bool livre = assetPath.Contains("/Animations/Livres/") || assetPath.Contains("/Animations/CMU/");   // Quaternius (CC0) e CMU: corpo virado pela orientação do esqueleto
            var clips = mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.loopTime = loop;
                c.loopPose = loop;
                c.lockRootRotation = true;           // não gira o corpo sozinho
                c.keepOriginalOrientation = !livre;
                // altura sempre dentro da pose, medida do chão original do arquivo: o corpo sobe no mortal,
                // agacha no rolamento e nunca afunda no chão (o jogo mantém os pés do menino no piso)
                c.lockRootHeightY = true; c.keepOriginalPositionY = true; c.heightFromFeet = false;
                // deslocamento para a frente/lados vira root motion (pelo centro de massa):
                // nos ciclos (andar/correr/dançar) o jogo ignora, então o corpo fica no lugar sem "pular para trás"
                // a cada passada; nas acrobacias o jogo aplica (o flip avança, a estrela vai para o lado)
                c.lockRootPositionXZ = false; c.keepOriginalPositionXZ = false;
                if (file == "Escalar")
                {
                    // escalar: quem sobe o menino até o topo da mureta é o jogo; a pose não pode subir junto
                    c.lockRootHeightY = false; c.keepOriginalPositionY = false;
                }
            }
            mi.clipAnimations = clips;
        }

        void OnPreprocessAudio()
        {
            var ai = (AudioImporter)assetImporter;
            var st = ai.defaultSampleSettings;
            if (assetPath.Contains("/Resources/Music/"))
            {
                // músicas longas: tocam direto do arquivo (sem ocupar dezenas de MB de memória)
                st.loadType = AudioClipLoadType.Streaming; st.compressionFormat = AudioCompressionFormat.Vorbis; st.quality = 0.55f;
                ai.loadInBackground = true;
            }
            else if (assetPath.Contains("/Resources/Sounds/"))
            {
                st.loadType = AudioClipLoadType.DecompressOnLoad; st.compressionFormat = AudioCompressionFormat.Vorbis; st.quality = 0.6f;
                ai.forceToMono = true;
            }
            else return;
            ai.defaultSampleSettings = st;
        }

        void OnPreprocessTexture()
        {
            if (assetPath.Contains("/Resources/Map/"))
            {
                // mapa de cores do chão de Ponta Grossa (3528 x 3730, 4 m por pixel)
                var tm = (TextureImporter)assetImporter;
                tm.maxTextureSize = 4096; tm.mipmapEnabled = true; tm.wrapMode = TextureWrapMode.Clamp;
                tm.anisoLevel = 4; tm.npotScale = TextureImporterNPOTScale.None;
                tm.textureCompression = TextureImporterCompression.Compressed;
                var a = tm.GetPlatformTextureSettings("Android");
                a.overridden = true; a.maxTextureSize = 4096; a.format = TextureImporterFormat.ASTC_6x6;
                tm.SetPlatformTextureSettings(a);
                return;
            }
            if (assetPath.Contains("/Resources/Traffic/") || assetPath.Contains("/Resources/Houses/"))
            {
                // paleta de cores dos carros: pixels nítidos, sem compressão que borre as cores
                var tt = (TextureImporter)assetImporter;
                tt.filterMode = FilterMode.Point; tt.mipmapEnabled = false; tt.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }
            if (!(IsChar(assetPath) || IsVehicle(assetPath))) return;
            var ti = (TextureImporter)assetImporter;
            ti.maxTextureSize = 2048;
            ti.anisoLevel = 8;
            ti.mipmapEnabled = true;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            if (assetPath.EndsWith("_normal.png")) ti.textureType = TextureImporterType.NormalMap;
            var android = ti.GetPlatformTextureSettings("Android");
            android.overridden = true; android.maxTextureSize = 2048;
            android.format = assetPath.EndsWith("_normal.png") ? TextureImporterFormat.ASTC_5x5 : TextureImporterFormat.ASTC_4x4;
            ti.SetPlatformTextureSettings(android);
        }
    }
}
