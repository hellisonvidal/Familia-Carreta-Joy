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
        // animações em pé/sentadas no chão: heightFromFeet garante pés exatamente em Y = 0 (nunca afundam nem flutuam)
        static readonly string[] GroundedClips = {
            "Idle", "Walk", "Run", "Sprint_Loop", "Passeio_Loop", "Danca_Gangnam", "Danca2_Loop",
            "Charleston_Loop", "Russa_Loop", "Salsa_Loop", "Acenar_Loop", "Chamar_Loop",
            "Conversa_Loop", "Agachado_Loop", "Sentado_Loop", "Moonwalk"
        };

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
                // Usa o Avatar T-pose limpo de Idle.fbx (Mixamo) ou Passeio_Loop.fbx (Quaternius) para evitar que
                // o Unity gere um Avatar com joelho/pé torcido em clipes que começam com a perna dobrada (Run.fbx, Sprint_Loop.fbx)!
                string fn = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                Avatar refAv = null;
                if (fn == "Run" || fn == "Walk" || fn == "Jump" || fn == "Mortal" || fn == "Flip" || fn == "RunToFlip")
                    refAv = AssetDatabase.LoadAssetAtPath<Avatar>("Assets/Resources/Animations/Idle.fbx");
                else if (fn == "Sprint_Loop")
                    refAv = AssetDatabase.LoadAssetAtPath<Avatar>("Assets/Resources/Animations/Livres/Passeio_Loop.fbx");

                if (refAv != null && refAv.isValid && refAv.isHuman)
                {
                    mi.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    mi.sourceAvatar = refAv;
                }
                else
                {
                    mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                }
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
            bool grounded = GroundedClips.Contains(file);
            var clips = mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.loopTime = loop;
                c.loopPose = loop;
                c.lockRootRotation = true;           // não gira o corpo sozinho
                c.keepOriginalOrientation = !livre;
                if (file == "Escalar")
                {
                    // escalar: quem sobe o menino até o topo da mureta é o jogo; a pose não pode subir junto
                    c.lockRootHeightY = false; c.keepOriginalPositionY = false; c.heightFromFeet = false;
                }
                else if (grounded)
                {
                    // andar/correr/parado/dançar/sentado: mede altura direto pelos pés -> nunca afunda nem flutua no asfalto!
                    c.lockRootHeightY = true; c.keepOriginalPositionY = false; c.heightFromFeet = true;
                }
                else if (file == "PuloAr_Loop" || file == "Ninja_Loop")
                {
                    // pose no ar: a gravidade/física do CharacterController já controla a altura do corpo
                    c.lockRootHeightY = true; c.keepOriginalPositionY = false; c.heightFromFeet = true;
                }
                else
                {
                    // acrobacias com salto próprio (mortal, flip, estrela, rolamento...): mantém o arco vertical da captura
                    c.lockRootHeightY = true; c.keepOriginalPositionY = true; c.heightFromFeet = false;
                }
                // deslocamento para a frente/lados vira root motion (pelo centro de massa):
                // nos ciclos (andar/correr/dançar) o jogo ignora; nas acrobacias o jogo aplica com gravidade
                c.lockRootPositionXZ = false; c.keepOriginalPositionXZ = false;
            }
            mi.clipAnimations = clips;
        }

        void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            if (!IsAnim(assetPath) || clip == null) return;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (file != "Run" && file != "Walk" && file != "Sprint_Loop" && file != "Passeio_Loop") return;

            // Remove torção lateral nos joelhos/pés e garante leve abertura simétrica (+0.02) no quadril:
            // pernas 100% retas e paralelas ao andar/correr, sem curvar para dentro nem encostar uma na outra
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                string n = b.propertyName;
                if (n == "Left Lower Leg Twist In-Out" || n == "Right Lower Leg Twist In-Out"
                 || n == "Left Foot Twist In-Out" || n == "Right Foot Twist In-Out"
                 || n == "Left Upper Leg Twist In-Out" || n == "Right Upper Leg Twist In-Out")
                {
                    AnimationUtility.SetEditorCurve(clip, b, AnimationCurve.Constant(0f, clip.length, 0f));
                }
                else if (n == "Left Upper Leg In-Out" || n == "Right Upper Leg In-Out")
                {
                    AnimationUtility.SetEditorCurve(clip, b, AnimationCurve.Constant(0f, clip.length, 0.02f));
                }
            }
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
