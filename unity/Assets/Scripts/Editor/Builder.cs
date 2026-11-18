using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CarretaJoy.EditorTools
{
    /// <summary>
    /// Monta o projeto inteiro por código (pipeline URP, materiais, cena, ajustes do Android)
    /// e gera o APK. Usado pelo GitHub Actions: -executeMethod CarretaJoy.EditorTools.Builder.BuildAndroid
    /// No editor: menu Carreta Joy > Preparar projeto / Gerar APK.
    /// </summary>
    public static class Builder
    {
        public const string VERSION = "0.9.0";
        public const int VERSION_CODE = 10;
        const string SCENE = "Assets/Scenes/Main.unity";

        [MenuItem("Carreta Joy/Preparar projeto")]
        public static void Setup()
        {
            Log("Preparando projeto " + VERSION);
            Directory.CreateDirectory("Assets/Settings");
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Resources/Materials");

            // garante que modelos e animações passaram pelas regras de importação
            foreach (var dir in new[] { "Assets/Resources/Characters", "Assets/Resources/Animations", "Assets/Resources/Vehicles", "Assets/Resources/People", "Assets/Resources/Traffic", "Assets/Resources/Houses", "Assets/Resources/City", "Assets/Resources/Sounds", "Assets/Resources/Music" })
                if (AssetDatabase.IsValidFolder(dir))
                    foreach (var guid in AssetDatabase.FindAssets("", new[] { dir }))
                        AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);

            SetupPipeline();
            SetupInputAxes();
            SetupMaterials();
            SetupScene();
            SetupPlayer();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log("Projeto pronto");
        }

        // ---------------- URP ----------------
        static void SetupPipeline()
        {
            const string path = "Assets/Settings/URP.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create();
                AssetDatabase.CreateAsset(asset, path);
                // cria o renderer com os dados de pós-processamento padrão (bloom, tonemapping...)
                asset.LoadBuiltinRendererData(RendererType.UniversalRenderer);
            }
            asset.supportsHDR = true;
            asset.msaaSampleCount = 2;
            asset.renderScale = 1f;
            asset.shadowDistance = 60f;
            asset.shadowCascadeCount = 2;
            asset.mainLightShadowmapResolution = 2048;
            asset.maxAdditionalLightsCount = 8;
            // no URP 17.3 estes não têm "set" público: muda direto no asset serializado
            var so = new SerializedObject(asset);
            SetProp(so, "m_AdditionalLightsRenderingMode", (int)LightRenderingMode.PerPixel);
            SetProp(so, "m_AdditionalLightShadowsSupported", false);
            SetProp(so, "m_SoftShadowsSupported", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);

            GraphicsSettings.defaultRenderPipeline = asset;
            int cur = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(cur, false);

            // configurações globais do URP (cria se faltar)
            try
            {
                var gs = typeof(UniversalRenderPipelineAsset).Assembly.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings");
                var m = gs?.GetMethod("Ensure", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                m?.Invoke(null, new object[] { true });
            }
            catch (Exception e) { Debug.LogWarning("URP global settings: " + e.Message); }
        }

        static void SetProp(SerializedObject so, string name, object v)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning("URP: campo não encontrado " + name); return; }
            if (v is bool b) p.boolValue = b; else p.intValue = (int)v;
        }

        // ---------------- controle de videogame ----------------
        /// <summary>Eixos "PadAxis1".."PadAxis12" (todos os controles) no Input Manager clássico.</summary>
        static void SetupInputAxes()
        {
            var objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset");
            if (objs == null || objs.Length == 0) { Debug.LogWarning("InputManager.asset não encontrado"); return; }
            var so = new SerializedObject(objs[0]);
            var axes = so.FindProperty("m_Axes");
            for (int i = 1; i <= 12; i++)
            {
                string name = "PadAxis" + i;
                bool exists = false;
                for (int k = 0; k < axes.arraySize; k++)
                    if (axes.GetArrayElementAtIndex(k).FindPropertyRelative("m_Name").stringValue == name) { exists = true; break; }
                if (exists) continue;
                axes.arraySize++;
                var a = axes.GetArrayElementAtIndex(axes.arraySize - 1);
                a.FindPropertyRelative("m_Name").stringValue = name;
                a.FindPropertyRelative("descriptiveName").stringValue = "";
                a.FindPropertyRelative("descriptiveNegativeName").stringValue = "";
                a.FindPropertyRelative("negativeButton").stringValue = "";
                a.FindPropertyRelative("positiveButton").stringValue = "";
                a.FindPropertyRelative("altNegativeButton").stringValue = "";
                a.FindPropertyRelative("altPositiveButton").stringValue = "";
                a.FindPropertyRelative("gravity").floatValue = 0;
                a.FindPropertyRelative("dead").floatValue = 0.05f;
                a.FindPropertyRelative("sensitivity").floatValue = 1;
                a.FindPropertyRelative("snap").boolValue = false;
                a.FindPropertyRelative("invert").boolValue = false;
                a.FindPropertyRelative("type").intValue = 2;        // eixo de joystick
                a.FindPropertyRelative("axis").intValue = i - 1;
                a.FindPropertyRelative("joyNum").intValue = 0;      // qualquer controle
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            Log("eixos do controle prontos");
        }

        // ---------------- materiais ----------------
        static void SetupMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) throw new Exception("Shader URP/Lit não encontrado (URP não instalado?)");

            var m = Mat("Lit", lit);
            m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", 0.25f);

            var g = Mat("Glow", lit);
            g.SetColor("_BaseColor", Color.white);
            g.EnableKeyword("_EMISSION");
            g.SetColor("_EmissionColor", Color.white * 2f);
            g.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            // variante com textura de emissão (janelas dos prédios)
            var w = Mat("GlowMap", lit);
            w.EnableKeyword("_EMISSION");
            w.SetTexture("_EmissionMap", Texture2D.whiteTexture);
            w.SetColor("_EmissionColor", Color.white);

            foreach (var kid in new[] { "Arthur", "Afonso", "Chaves", "Fofao", "Chapolin", "Mascara", "Aranha", "Capitao", "Hulk" })
            {
                var k = Mat("Kid_" + kid, lit);
                var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Resources/Characters/{kid}/{kid}_albedo.jpg");
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Resources/Characters/{kid}/{kid}_normal.png");
                k.SetTexture("_BaseMap", albedo);
                k.SetColor("_BaseColor", Color.white);
                if (normal != null) { k.SetTexture("_BumpMap", normal); k.SetFloat("_BumpScale", 0.8f); k.EnableKeyword("_NORMALMAP"); }
                k.SetFloat("_Smoothness", 0.12f); k.SetFloat("_Metallic", 0f);
                k.SetFloat("_EnvironmentReflections", 0f);
            }

            var v = Mat("Carreta", lit);
            v.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Vehicles/Carreta_albedo.jpg"));
            var vn = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Vehicles/Carreta_normal.png");
            if (vn != null) { v.SetTexture("_BumpMap", vn); v.EnableKeyword("_NORMALMAP"); }
            v.SetFloat("_Smoothness", 0.45f); v.SetFloat("_Metallic", 0.1f);
            // carros da cidade (Kenney, CC0): uma textura de cores para todos
            var tr = Mat("Transito", lit);
            tr.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Traffic/colormap.png"));
            tr.SetColor("_BaseColor", Color.white); tr.SetFloat("_Smoothness", 0.5f); tr.SetFloat("_Metallic", 0.15f);
            var sky = Mat("Ceu", Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.04f); sky.SetFloat("_AtmosphereThickness", 0.9f); sky.SetFloat("_Exposure", 1.25f);
            AssetDatabase.SaveAssets();
        }

        static Material Mat(string name, Shader sh)
        {
            string p = "Assets/Resources/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(sh) { name = name }; AssetDatabase.CreateAsset(m, p); }
            else m.shader = sh;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------------- cena ----------------
        static void SetupScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Jogo");
            go.AddComponent<Game>();
            // a neblina precisa estar ligada na cena para o shader dela entrar no APK
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 120; RenderSettings.fogEndDistance = 420;
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/Ceu.mat");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            Lightmapping.bakedGI = false; Lightmapping.realtimeGI = false;
            EditorSceneManager.SaveScene(scene, SCENE);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SCENE, true) };
        }

        // ---------------- Android ----------------
        static void SetupPlayer()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.companyName = "Familia";
            PlayerSettings.productName = "Carreta Joy 3D";
            PlayerSettings.SetApplicationIdentifier(android, "com.familia.carretajoy.unity");
            PlayerSettings.bundleVersion = VERSION;
            PlayerSettings.Android.bundleVersionCode = VERSION_CODE;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Low);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.Android.forceInternetPermission = true;   // atualizador
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.runInBackground = false;

            // ícones: Android usa a versão quadrada (o sistema recorta); Windows a arredondada
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icon.png");
            var iconSq = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/IconSquare.png") ?? icon;
            if (icon != null)
            {
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
                foreach (var (t, tex) in new[] { (NamedBuildTarget.Android, iconSq), (NamedBuildTarget.Standalone, icon) })
                {
                    var sizes = PlayerSettings.GetIconSizes(t, IconKind.Application);
                    var arr = new Texture2D[sizes.Length];
                    for (int i = 0; i < arr.Length; i++) arr[i] = tex;
                    if (arr.Length > 0) PlayerSettings.SetIcons(t, arr, IconKind.Application);
                }
            }

            // mesma chave do APK atual (no futuro uma versão pode atualizar a outra)
            string ks = Path.GetFullPath(Path.Combine(Application.dataPath, "../../keystore/debug.keystore"));
            if (File.Exists(ks))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = ks;
                PlayerSettings.Android.keystorePass = "android";
                PlayerSettings.Android.keyaliasName = "androiddebugkey";
                PlayerSettings.Android.keyaliasPass = "android";
            }
            else Debug.LogWarning("Chave não encontrada em " + ks + " (usa a de depuração do Unity)");
            EditorUserBuildSettings.buildAppBundle = false;
        }

        // ---------------- build ----------------
        [MenuItem("Carreta Joy/Gerar APK")]
        public static void BuildAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            Setup();
            string outPath = Arg("-customBuildPath") ?? "build/Android/CarretaJoy3D.apk";
            if (!outPath.EndsWith(".apk")) outPath = Path.Combine(outPath, "CarretaJoy3D.apk");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
            var opt = new BuildPlayerOptions
            {
                scenes = new[] { SCENE },
                locationPathName = outPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opt);
            var s = report.summary;
            Log($"Resultado: {s.result}  tamanho: {s.totalSize / (1024 * 1024)} MB  erros: {s.totalErrors}  saída: {outPath}");
            if (s.result != BuildResult.Succeeded) throw new Exception("Falha ao gerar o APK: " + s.result);
        }

        [MenuItem("Carreta Joy/Gerar Windows")]
        public static void BuildWindows()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            Setup();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.familia.carretajoy3d");
            string outPath = Arg("-customBuildPath") ?? "build/StandaloneWindows64/CarretaJoy3D.exe";
            if (!outPath.EndsWith(".exe")) outPath = Path.Combine(outPath, "CarretaJoy3D.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
            var opt = new BuildPlayerOptions
            {
                scenes = new[] { SCENE }, locationPathName = outPath,
                target = BuildTarget.StandaloneWindows64, targetGroup = BuildTargetGroup.Standalone, options = BuildOptions.None
            };
            var s = BuildPipeline.BuildPlayer(opt).summary;
            Log($"Windows: {s.result}  erros: {s.totalErrors}  saída: {outPath}");
            if (s.result != BuildResult.Succeeded) throw new Exception("Falha ao gerar o Windows: " + s.result);
        }

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        static void Log(string s) => Debug.Log("[CarretaJoy] " + s);
    }
}
