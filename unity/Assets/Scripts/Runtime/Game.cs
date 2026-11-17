using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CarretaJoy
{
    /// <summary>
    /// Ponto de partida: monta luz, céu, pós-processamento, cidade, carreta, Arthur e Afonso,
    /// câmera, controles e música. Arthur começa no controle; Afonso acompanha (e vice-versa).
    /// </summary>
    public class Game : MonoBehaviour
    {
        public static Game I;

        Camera cam; CameraRig rig; Light sun; Volume volume; Bloom bloom; ColorAdjustments color;
        Material sky;
        City city; Truck truck; Music music; TouchUI ui;
        Kid arthur, afonso, player, buddy;
        bool night;
        int score;
        float fpsAcc; int fpsN;
        float buddyTrickDelay = -1; string buddyTrick;

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            QualitySettings.vSyncCount = 0;
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.autorotateToPortrait = Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = true;
            // telas muito grandes: renderiza um pouco abaixo da resolução nativa (economiza bateria e calor)
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                urp.renderScale = Mathf.Clamp(1920f / Mathf.Max(Screen.width, Screen.height), 0.6f, 1f);
        }

        void Start()
        {
            BuildEnvironment();

            city = new GameObject("Cidade").AddComponent<City>();
            city.Build(7);

            var tgo = new GameObject("Carreta");
            truck = tgo.AddComponent<Truck>();
            truck.Build(Resources.Load<GameObject>("Vehicles/Carreta"), city.Ring);

            music = new GameObject("Musica").AddComponent<Music>();

            var common = new KidClips
            {
                idle = KidClips.Load("Animations/Idle"),
                walk = KidClips.Load("Animations/Walk"),
                run = KidClips.Load("Animations/Run"),
                dance = KidClips.Load("Animations/Danca_Gangnam"),
                jump = KidClips.Load("Animations/Jump"),
                mortal = KidClips.Load("Animations/Mortal"),
                flip = KidClips.Load("Animations/Flip"),
                pirueta = KidClips.Load("Animations/RunToFlip"),
            };
            var arthurClips = new KidClips
            {
                idle = common.idle, dance = common.dance, jump = common.jump, mortal = common.mortal, flip = common.flip, pirueta = common.pirueta,
                walk = KidClips.Load("Animations/Arthur_AndarCorrer", "Walking") ?? common.walk,
                run = KidClips.Load("Animations/Arthur_AndarCorrer", "Running") ?? common.run,
            };

            var start = city.PlazaCenter + new Vector3(0, 0.1f, -10f);
            arthur = Kid.Create("Arthur", Resources.Load<GameObject>("Characters/Arthur/Arthur"), 1.32f, arthurClips, start);
            afonso = Kid.Create("Afonso", Resources.Load<GameObject>("Characters/Afonso/Afonso"), 1.18f, common, start + new Vector3(1.4f, 0, -0.6f));
            arthur.truck = afonso.truck = truck;
            SetupKidMaterials(arthur, "Arthur");
            SetupKidMaterials(afonso, "Afonso");

            ui = gameObject.AddComponent<TouchUI>();
            rig = cam.gameObject.AddComponent<CameraRig>();
            SetPlayer(arthur);
            rig.yaw = 0;
            SetNight(false);
            ui.ShowToast("CARRETA JOY!");
        }

        void SetupKidMaterials(Kid k, string name)
        {
            var albedo = Resources.Load<Texture2D>("Characters/" + name + "/" + name + "_albedo");
            var normal = Resources.Load<Texture2D>("Characters/" + name + "/" + name + "_normal");
            var mat = Resources.Load<Material>("Materials/Kid_" + name);
            if (mat == null)
            {
                mat = new Material(Resources.Load<Material>("Materials/Lit"));
                if (albedo) mat.SetTexture("_BaseMap", albedo);
                if (normal) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }
            }
            foreach (var r in k.GetComponentsInChildren<SkinnedMeshRenderer>()) r.sharedMaterial = mat;
        }

        // ---------------- ambiente ----------------
        void BuildEnvironment()
        {
            var cgo = new GameObject("Camera");
            cam = cgo.AddComponent<Camera>();
            cgo.tag = "MainCamera";
            cam.nearClipPlane = 0.15f; cam.farClipPlane = 600f; cam.fieldOfView = 58f;
            cgo.AddComponent<AudioListener>();
            var cd = cam.GetUniversalAdditionalCameraData();
            cd.renderPostProcessing = true;
            cd.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            cd.renderShadows = true;

            var sgo = new GameObject("Sol");
            sun = sgo.AddComponent<Light>();
            sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.85f;

            sky = Resources.Load<Material>("Materials/Ceu");
            if (sky == null) sky = new Material(Shader.Find("Skybox/Procedural"));
            else sky = new Material(sky);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 120f; RenderSettings.fogEndDistance = 420f;
            RenderSettings.sun = sun;

            var vgo = new GameObject("PosProcessamento");
            volume = vgo.AddComponent<Volume>();
            volume.isGlobal = true;
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            bloom = prof.Add<Bloom>(true);
            bloom.threshold.Override(1.0f); bloom.intensity.Override(0.8f); bloom.scatter.Override(0.65f);
            var tm = prof.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
            color = prof.Add<ColorAdjustments>(true);
            color.saturation.Override(12f); color.contrast.Override(8f); color.postExposure.Override(0.25f);
            var vig = prof.Add<Vignette>(true); vig.intensity.Override(0.22f); vig.smoothness.Override(0.4f);
            volume.sharedProfile = prof;
        }

        void SetNight(bool n)
        {
            night = n;
            if (n)
            {
                sun.color = new Color(0.55f, 0.65f, 1f); sun.intensity = 0.25f;
                sun.transform.rotation = Quaternion.Euler(55, -40, 0);
                RenderSettings.ambientSkyColor = new Color(0.16f, 0.18f, 0.32f);
                RenderSettings.ambientEquatorColor = new Color(0.12f, 0.10f, 0.20f);
                RenderSettings.ambientGroundColor = new Color(0.06f, 0.05f, 0.08f);
                RenderSettings.fogColor = new Color(0.06f, 0.07f, 0.14f);
                sky.SetFloat("_Exposure", 0.12f); sky.SetFloat("_AtmosphereThickness", 0.55f); sky.SetColor("_SkyTint", new Color(0.25f, 0.25f, 0.6f));
                bloom.intensity.Override(1.6f); bloom.threshold.Override(0.85f);
                color.postExposure.Override(0.45f);
            }
            else
            {
                sun.color = new Color(1f, 0.95f, 0.86f); sun.intensity = 1.35f;
                sun.transform.rotation = Quaternion.Euler(48, -35, 0);
                RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.92f);
                RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.52f);
                RenderSettings.ambientGroundColor = new Color(0.32f, 0.30f, 0.26f);
                RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.95f);
                sky.SetFloat("_Exposure", 1.25f); sky.SetFloat("_AtmosphereThickness", 0.9f); sky.SetColor("_SkyTint", new Color(0.5f, 0.6f, 0.9f));
                bloom.intensity.Override(0.6f); bloom.threshold.Override(1.05f);
                color.postExposure.Override(0.2f);
            }
            Mats.ApplyNight(n);
            truck.SetNight(n);
            ui.B["dia"].label = n ? "DIA" : "NOITE";
            DynamicGI.UpdateEnvironment();
        }

        void SetPlayer(Kid k)
        {
            player = k; buddy = k == arthur ? afonso : arthur;
            rig.target = k;
            rig.yaw = k.transform.eulerAngles.y;
            player.Running = ui.B["correr"].toggled;
            ui.Who = k.Name;
            ui.B["trocar"].label = "TROCAR: " + buddy.Name.ToUpper();
        }

        public void AddScore(int pts, string msg)
        {
            score += pts;
            ui.Score = score.ToString();
            if (msg != null) ui.ShowToast(msg + " +" + pts);
        }

        public void OnLand(Kid k) { }

        // ---------------- laço principal ----------------
        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            fpsAcc += Time.unscaledDeltaTime; fpsN++;
            if (fpsAcc > 0.5f) { ui.Fps = fpsN / fpsAcc; fpsAcc = 0; fpsN = 0; }

            // botões
            if (ui.Pressed("dia")) SetNight(!night);
            if (ui.Pressed("som")) { music.on = !music.on; ui.B["som"].toggled = !music.on; ui.B["som"].label = music.on ? "SOM" : "MUDO"; }
            if (ui.Pressed("trocar")) { SetPlayer(buddy); ui.ShowToast("Agora: " + player.Name); }
            if (ui.Pressed("correr")) { var b = ui.B["correr"]; b.toggled = !b.toggled; player.Running = b.toggled; b.label = b.toggled ? "ANDAR" : "CORRER"; }
            if (ui.Pressed("pular")) player.Jump();
            if (ui.Pressed("mortal")) { player.DoTrick("mortal"); BuddyCopy("mortal"); }
            if (ui.Pressed("flip")) { player.DoTrick("flip"); BuddyCopy("flip"); }
            if (ui.Pressed("pirueta")) { player.DoTrick("pirueta"); BuddyCopy("pirueta"); }
            if (ui.Pressed("dancar")) { player.ToggleDance(); if (player.Dancing != buddy.Dancing) buddy.ToggleDance(); if (player.Dancing) AddScore(30, player.OnTruck ? "FESTA NA CARRETA!" : "DANÇA!"); }
            if (ui.Pressed("subir"))
            {
                bool ok = player.ClimbOrDescend();
                if (!ok && !player.OnTruck) ui.ShowToast("Chegue perto da carreta!");
            }

            // jogador
            rig.dragging = ui.Dragging;
            rig.Rotate(ui.CamDelta);
            player.Tick(rig.Flat(ui.Move), dt);

            // amigo segue o jogador (e sobe/desce da carreta junto)
            TickBuddy(dt);

            truck.Tick(dt, player.transform.position, player.OnTruck || player.Busy, music.Beat);

            // contexto do botão SUBIR/DESCER
            var sb = ui.B["subir"];
            sb.label = player.OnTruck ? "DESCER" : "SUBIR";
            sb.visible = player.OnTruck || truck.DistanceTo(player.transform.position) < 3.5f;
            ui.Info = player.OnTruck ? (truck.Stopped ? "Parada de festa! Dancem!" : "Em cima da carreta!")
                                     : (truck.DistanceTo(player.transform.position) < 3.5f ? "Toque SUBIR" : "Ache a carreta e suba!");
            if (truck.Stopped && player.OnTruck && player.Dancing) { festaAcc += dt; if (festaAcc > 1f) { festaAcc = 0; AddScore(10, null); } }

            // som: mais alto perto da carreta
            float d = Vector3.Distance(cam.transform.position, truck.transform.position);
            music.volume = Mathf.Lerp(0.65f, 0.12f, Mathf.InverseLerp(10f, 160f, d));

            // pista de dança da praça pisca no ritmo
            float beat = music.Beat;
            for (int i = 0; i < city.DanceTiles.Count; i++)
            {
                bool on = ((i * 7 + (int)beat) % 3) == 0;
                Mats.SetGlow(city.DanceTiles[i], Color.HSVToRGB(Mathf.Repeat(i * 0.13f + beat * 0.0625f, 1), 0.85f, 1), on ? (night ? 3.5f : 1.8f) : 0.15f);
            }
        }
        float festaAcc;

        void LateUpdate() { rig.Tick(Mathf.Min(Time.deltaTime, 0.05f)); }

        void BuddyCopy(string trick) { buddyTrick = trick; buddyTrickDelay = 0.35f; }

        void TickBuddy(float dt)
        {
            var b = buddy; var p = player;
            if (buddyTrickDelay >= 0) { buddyTrickDelay -= dt; if (buddyTrickDelay < 0 && !b.Busy) b.DoTrick(buddyTrick); }

            if (p.OnTruck && !b.OnTruck && !b.Busy)
            {
                if (truck.DistanceTo(b.transform.position) < 3.5f) b.ClimbOrDescend();
                else if (Vector3.Distance(b.transform.position, truck.transform.position) > 40f) b.PlaceOnTruck(truck, truck.transform.InverseTransformPoint(p.transform.position) + Vector3.right * 0.8f);
            }
            if (!p.OnTruck && !p.Busy && b.OnTruck && !b.Busy) b.ClimbOrDescend();

            Vector3 target = p.transform.position - p.transform.forward * 1.2f + p.transform.right * 1.1f;
            Vector3 to = target - b.transform.position; to.y = 0;
            float dist = to.magnitude;
            Vector3 wish = Vector3.zero;
            if (!b.OnTruck && !p.OnTruck && dist > 45f && !b.Busy)
            {
                b.cc.enabled = false; b.transform.position = target + Vector3.up * 0.5f; b.cc.enabled = true; b.vel = Vector3.zero;
            }
            else if (dist > 0.6f && !(b.Dancing && dist < 3f))
            {
                wish = to / dist * Mathf.Clamp01(dist / 1.5f);
                if (b.OnTruck && p.OnTruck) wish *= 0.8f;
                b.Running = dist > 4f || p.HorizontalSpeed > p.WalkSpeed * 1.2f;
            }
            if (!b.OnTruck && p.Grounded && b.Grounded && p.transform.position.y - b.transform.position.y > 0.6f && dist < 3f) b.Jump();
            b.Tick(wish, dt);
        }
    }
}
