using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CarretaJoy
{
    /// <summary>
    /// Ponto de partida: monta luz, céu, pós-processamento, Ponta Grossa, carreta, a turma,
    /// câmera, controles, menus e música. Mundo aberto: anda com qualquer personagem, sobe na
    /// carreta ou assume o volante dela.
    /// </summary>
    public class Game : MonoBehaviour
    {
        public static Game I;

        Camera cam; CameraRig rig; Light sun; Volume volume; Bloom bloom; ColorAdjustments color;
        Material sky;
        PgCity city; Truck truck; Music music; TouchUI ui; MenuUI menu;
        AudioSource horn;
        // turma: id do modelo, nome, altura (m)
        static readonly (string id, string nome, float h)[] ROSTER = {
            ("Arthur", "Arthur", 1.32f), ("Afonso", "Afonso", 1.18f), ("Chaves", "Chaves", 1.64f),
            ("Fofao", "Fofão", 1.70f), ("Chapolin", "Chapolin", 1.66f), ("Mascara", "Máscara", 1.80f),
            ("Aranha", "Homem-Aranha", 1.78f), ("Capitao", "Capitão América", 1.86f), ("Hulk", "Hulk", 2.20f) };
        static readonly Color[] DotCols = { new Color(0.2f, 0.6f, 1f), new Color(0.3f, 0.9f, 1f), new Color(0.9f, 0.7f, 0.4f), new Color(1f, 0.5f, 0.8f),
                                           new Color(1f, 0.2f, 0.2f), new Color(0.4f, 1f, 0.3f), new Color(0.9f, 0.1f, 0.2f), new Color(0.3f, 0.4f, 1f), new Color(0.2f, 0.75f, 0.2f) };
        readonly List<Kid> crew = new List<Kid>();
        readonly Dictionary<Kid, float> copyDelay = new Dictionary<Kid, float>();
        Kid player;
        int playerIdx;
        bool night, playing;
        public bool Driving { get; private set; }
        int score;
        float fpsAcc; int fpsN;
        string copyTrick;
        float festaAcc, menuOrbit;

        public int CharCount => ROSTER.Length;
        public string CharName(int i) => ROSTER[Mathf.Clamp(i, 0, ROSTER.Length - 1)].nome;
        public int StopCount => city != null ? city.map.Stops.Count : 0;
        public string StopName(int i) => city != null && city.map.Stops.Count > 0 ? city.map.Stops[Mathf.Clamp(i, 0, city.map.Stops.Count - 1)].name : "";
        public int PlayerIndex => playerIdx;
        public bool TruckManual => truck != null && truck.Manual;

        void Awake()
        {
            I = this;
            Settings.Load();
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            QualitySettings.vSyncCount = 0;
            if (Application.isMobilePlatform)
            {
                Screen.orientation = ScreenOrientation.AutoRotation;
                Screen.autorotateToPortrait = Screen.autorotateToPortraitUpsideDown = false;
                Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = true;
            }
        }

        void Start()
        {
            BuildEnvironment();

            city = new GameObject("PontaGrossa").AddComponent<PgCity>();
            city.Build();
            Kid.GroundHeight = city.Height;

            var tgo = new GameObject("Carreta");
            truck = tgo.AddComponent<Truck>();
            truck.Build(Resources.Load<GameObject>("Vehicles/Carreta"), city.Ring);
            truck.SetStops(city.StopDistances);
            truck.GroundHeight = city.Height;

            music = new GameObject("Musica").AddComponent<Music>();
            horn = gameObject.AddComponent<AudioSource>();
            horn.clip = MakeHorn(); horn.loop = true; horn.playOnAwake = false; horn.volume = 0.5f;

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
            for (int i = 0; i < ROSTER.Length; i++)
            {
                var r = ROSTER[i];
                var prefab = Resources.Load<GameObject>("Characters/" + r.id + "/" + r.id);
                if (prefab == null) { Debug.LogWarning("Sem modelo: " + r.id); continue; }
                var k = Kid.Create(r.nome, prefab, r.h, r.id == "Arthur" ? arthurClips : common, city.PlazaCenter + Vector3.up);
                k.truck = truck;
                SetupKidMaterials(k, r.id);
                crew.Add(k);
            }

            ui = gameObject.AddComponent<TouchUI>();
            menu = gameObject.AddComponent<MenuUI>(); menu.game = this;
            rig = cam.gameObject.AddComponent<CameraRig>();
            ui.MapTex = city.GroundTexture; ui.MapWorld = city.GroundRect;
            PlaceAtStart();
            SetPlayer(Mathf.Clamp(Settings.StartChar, 0, crew.Count - 1));
            SetNight(false);
            ApplySettings();
            menu.Open(MenuUI.Page.Main);
        }

        void SetupKidMaterials(Kid k, string name)
        {
            var mat = Resources.Load<Material>("Materials/Kid_" + name);
            if (mat == null)
            {
                var albedo = Resources.Load<Texture2D>("Characters/" + name + "/" + name + "_albedo");
                var normal = Resources.Load<Texture2D>("Characters/" + name + "/" + name + "_normal");
                mat = new Material(Resources.Load<Material>("Materials/Lit"));
                if (albedo) mat.SetTexture("_BaseMap", albedo);
                if (normal) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }
            }
            foreach (var r in k.GetComponentsInChildren<SkinnedMeshRenderer>()) r.sharedMaterial = mat;
        }

        // ---------------- menus ----------------
        public void StartGame()
        {
            if (Driving) ToggleDrive(false);
            PlaceAtStart();
            SetPlayer(Mathf.Clamp(Settings.StartChar, 0, crew.Count - 1));
            score = 0; ui.Score = "0";
            playing = true;
            menu.page = MenuUI.Page.None;
            ui.ShowToast("Ponta Grossa: " + StopName(Settings.StartStop) + "!");
        }

        public void Resume() { }
        public void GoMenu() { if (Driving) ToggleDrive(false); playing = false; menu.Open(MenuUI.Page.Main); }
        public void PreviewStart() { if (!playing) PlaceAtStart(); }
        public void SelectPlayer(int i) { if (Driving) ToggleDrive(false); SetPlayer(i); ui.ShowToast("Agora: " + player.Name); }
        public void TruckAuto() { if (Driving) ToggleDrive(false); truck.ResumeRoute(); ui.ShowToast("Carreta no piloto automático"); }

        /// <summary>Carreta no ponto de partida escolhido; a turma espalhada em volta, na calçada.</summary>
        void PlaceAtStart()
        {
            int s = Mathf.Clamp(Settings.StartStop, 0, Mathf.Max(0, city.StopDistancesByStop.Count - 1));
            float d = city.StopDistancesByStop.Count > 0 ? city.StopDistancesByStop[s] : 0;
            truck.StartAt(d);
            var tp = truck.transform;
            city.EnsureLoaded(tp.position, true);
            for (int i = 0; i < crew.Count; i++)
            {
                var k = crew[i];
                k.ResetState();
                // em leque ao lado da carreta, cada um no seu lugar
                float ang = (i - (crew.Count - 1) / 2f) * 16f;
                var off = Quaternion.Euler(0, ang, 0) * Vector3.right * (truck.HalfWidth + 4.5f + (i % 2) * 1.6f);
                var p = tp.position + tp.rotation * off;
                p.y = city.Height(p.x, p.z) + 0.3f;
                var look = tp.position - p; look.y = 0;
                k.Teleport(p, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward, Vector3.up));
            }
        }

        // ---------------- ajustes ----------------
        public void ApplySettings()
        {
            Application.targetFrameRate = Settings.Fps;
            float rs = Settings.Quality == 0 ? 0.65f : Settings.Quality == 1 ? 0.85f : 1f;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = Mathf.Clamp(rs * 1920f / Mathf.Max(Screen.width, Screen.height), 0.5f, 1f);
                urp.shadowDistance = Settings.Quality == 0 ? 25f : Settings.Quality == 1 ? 45f : 70f;
            }
            city.loadRadius = Settings.Quality == 0 ? 300f : Settings.Quality == 1 ? 400f : 520f;
            city.unloadRadius = city.loadRadius + 200f;
            bloom.active = Settings.Quality > 0;
            rig.mode = Settings.CamMode;
        }

        // ---------------- ambiente ----------------
        void BuildEnvironment()
        {
            var cgo = new GameObject("Camera");
            cam = cgo.AddComponent<Camera>();
            cgo.tag = "MainCamera";
            cam.nearClipPlane = 0.2f; cam.farClipPlane = 2000f; cam.fieldOfView = 58f;
            cgo.AddComponent<AudioListener>();
            var cd = cam.GetUniversalAdditionalCameraData();
            cd.renderPostProcessing = true;
            cd.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            cd.renderShadows = true;

            var sgo = new GameObject("Sol");
            sun = sgo.AddComponent<Light>();
            sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.85f;

            sky = Resources.Load<Material>("Materials/Ceu");
            sky = sky == null ? new Material(Shader.Find("Skybox/Procedural")) : new Material(sky);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 250f; RenderSettings.fogEndDistance = 1600f;
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

        void SetPlayer(int idx)
        {
            playerIdx = (idx + crew.Count) % crew.Count;
            var k = crew[playerIdx];
            player = k;
            rig.target = k; rig.follow = null;
            rig.yaw = k.transform.eulerAngles.y;
            rig.distance = 4.2f + k.Height * 1.0f;
            player.Running = ui.B["correr"].toggled;
            ui.Who = k.Name;
            ui.B["trocar"].label = "TROCAR: " + player.Name.ToUpper();
        }

        /// <summary>Entra ou sai do volante da carreta (force: vale de qualquer distância, pelo menu).</summary>
        public void ToggleDrive(bool force)
        {
            if (!Driving)
            {
                if (!force && truck.DistanceTo(player.transform.position) > 6f) { ui.ShowToast("Chegue perto da carreta!"); return; }
                player.ResetState();
                Driving = true;
                player.SetHidden(true);
                truck.Drive(0, 0, 0);
                rig.follow = truck.transform;
                ui.ShowToast(player.Name + " no volante!");
            }
            else
            {
                Driving = false;
                var tp = truck.transform;
                var p = tp.TransformPoint(new Vector3(truck.HalfWidth + 1.3f, 0, Truck.LENGTH / 2f - 3f));
                p.y = city.Height(p.x, p.z) + 0.3f;
                player.Teleport(p, Quaternion.Euler(0, tp.eulerAngles.y, 0));
                player.SetHidden(false);
                rig.follow = null;
                horn.Stop();
                ui.ShowToast("Carreta estacionada");
            }
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
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            fpsAcc += Time.unscaledDeltaTime; fpsN++;
            if (fpsAcc > 0.5f) { ui.Fps = fpsN / fpsAcc; fpsAcc = 0; fpsN = 0; }

            bool menuOpen = menu.page != MenuUI.Page.None;
            // jogo pausado com menu aberto; no menu inicial a cidade continua viva (carreta passeando)
            Time.timeScale = playing && menuOpen ? 0f : 1f;
            ui.Active = playing && !menuOpen;
            music.volume = Settings.Music;
            if (!playing)
            {
                truck.Tick(dt, truck.transform.position + truck.transform.forward * 1000f, true, music.Beat);
                city.EnsureLoaded(truck.transform.position, false);
                foreach (var c in crew) c.Tick(Vector3.zero, dt);
                Lights();
                return;
            }
            if (menuOpen) { horn.Stop(); return; }

            // botões gerais
            if (ui.Pressed("pausa")) { menu.Open(MenuUI.Page.Pause); return; }
            if (ui.Pressed("trocar")) { menu.Open(MenuUI.Page.Picker); return; }
            if (ui.Pressed("dia")) SetNight(!night);
            if (ui.Pressed("camera")) { Settings.CamMode = (Settings.CamMode + 1) % Settings.CamNames.Length; rig.mode = Settings.CamMode; Settings.Save(); ui.ShowToast("Câmera: " + Settings.CamNames[Settings.CamMode]); }
            if (ui.Pressed("dirigir")) ToggleDrive(false);

            rig.dragging = ui.Dragging;
            rig.Rotate(ui.CamDelta);
            rig.Zoom(ui.Zoom);

            if (Driving) TickDriving(dt);
            else TickOnFoot(dt);

            // os outros personagens
            TickCrew(dt);

            Vector3 focus = Driving ? truck.transform.position : player.transform.position;
            city.EnsureLoaded(focus, false);
            truck.Tick(dt, player.transform.position, player.OnTruck || player.Busy || Driving, music.Beat);
            if (truck.Stopped && player.OnTruck && player.Dancing) { festaAcc += dt; if (festaAcc > 1f) { festaAcc = 0; AddScore(10, null); } }

            // som: mais alto perto da carreta
            float d = Vector3.Distance(cam.transform.position, truck.transform.position);
            music.volume = Settings.Music * Mathf.Lerp(1f, 0.18f, Mathf.InverseLerp(10f, 160f, d));
            Lights();
            UpdateMap(focus);
        }

        void TickOnFoot(float dt)
        {
            bool nearTruck = truck.DistanceTo(player.transform.position) < 3.5f;
            if (ui.Pressed("correr")) { var b = ui.B["correr"]; b.toggled = !b.toggled; player.Running = b.toggled; b.label = b.toggled ? "ANDAR" : "CORRER"; }
            if (ui.Pressed("pular")) player.Jump();
            if (ui.Pressed("mortal")) { player.DoTrick("mortal"); CrewCopy("mortal"); }
            if (ui.Pressed("flip")) { player.DoTrick("flip"); CrewCopy("flip"); }
            if (ui.Pressed("pirueta")) { player.DoTrick("pirueta"); CrewCopy("pirueta"); }
            if (ui.Pressed("dancar"))
            {
                player.ToggleDance();
                if (Settings.CrewFollows) foreach (var c in crew) if (c != player && c.Dancing != player.Dancing) c.ToggleDance();
                if (player.Dancing) AddScore(30, player.OnTruck ? "FESTA NA CARRETA!" : "DANÇA!");
            }
            if (ui.Pressed("subir")) { if (!player.ClimbOrDescend() && !player.OnTruck) ui.ShowToast("Chegue perto da carreta!"); }

            player.Tick(rig.Flat(ui.Move), dt);

            // botões que aparecem conforme a situação
            SetVisible(true);
            var sb = ui.B["subir"];
            sb.label = player.OnTruck ? "DESCER" : "SUBIR";
            sb.visible = player.OnTruck || nearTruck;
            ui.B["dirigir"].visible = !player.OnTruck && truck.DistanceTo(player.transform.position) < 6f;
            ui.B["dirigir"].label = "DIRIGIR";
            ui.Info = player.OnTruck ? (truck.Stopped ? "Parada de festa! Dancem!" : "Em cima da carreta!")
                                     : nearTruck ? "Toque SUBIR ou DIRIGIR" : "Explore Ponta Grossa!";
        }

        void TickDriving(float dt)
        {
            SetVisible(false);
            ui.B["dirigir"].visible = true; ui.B["dirigir"].label = "SAIR";
            // joystick: lados viram, para frente acelera, para trás freia/ré
            truck.Drive(ui.Move.x, ui.Move.y, dt);
            if (ui.B["buzina"].held) { if (!horn.isPlaying) horn.Play(); } else if (horn.isPlaying) horn.Stop();
            // o motorista vai junto (escondido na cabine)
            player.FollowCab(truck.transform.TransformPoint(new Vector3(0, 1.2f, Truck.LENGTH / 2f - 2.5f)));
            ui.Info = Mathf.RoundToInt(Mathf.Abs(truck.ManualSpeed) * 3.6f) + " km/h";
        }

        void SetVisible(bool foot)
        {
            foreach (var id in new[] { "pular", "mortal", "flip", "pirueta", "correr", "dancar", "subir" }) ui.B[id].visible = foot;
            ui.B["buzina"].visible = !foot;
        }

        void Lights()
        {
            float beat = music.Beat;
            for (int i = 0; i < city.DanceTiles.Count; i++)
            {
                bool on = ((i * 7 + (int)beat) % 3) == 0;
                Mats.SetGlow(city.DanceTiles[i], Color.HSVToRGB(Mathf.Repeat(i * 0.13f + beat * 0.0625f, 1), 0.85f, 1), on ? (night ? 3.5f : 1.8f) : 0.15f);
            }
        }

        void UpdateMap(Vector3 focus)
        {
            ui.MapCenter = new Vector2(focus.x, focus.z);
            var fw = Driving ? truck.transform.forward : player.transform.forward;
            ui.MapHeading = Mathf.Atan2(fw.x, fw.z) * Mathf.Rad2Deg;
            ui.Dots.Clear();
            var t = truck.transform.position;
            ui.Dots.Add((new Vector2(t.x, t.z), new Color(1f, 0.2f, 0.8f), 3.2f));
            for (int i = 0; i < crew.Count; i++)
                if (crew[i] != player)
                {
                    var p = crew[i].transform.position;
                    ui.Dots.Add((new Vector2(p.x, p.z), DotCols[i % DotCols.Length], 1.8f));
                }
        }

        void LateUpdate()
        {
            if (!playing)
            {
                // menu inicial: câmera gira devagar em volta da carreta
                menuOrbit += Time.unscaledDeltaTime * 6f;
                var tp = truck.transform.position;
                var off = Quaternion.Euler(18, menuOrbit, 0) * new Vector3(0, 0, -24);
                cam.transform.position = tp + Vector3.up * 3 + off;
                cam.transform.LookAt(tp + Vector3.up * 2.5f);
                return;
            }
            rig.Tick(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
        }

        void CrewCopy(string trick)
        {
            if (!Settings.CrewFollows) return;
            copyTrick = trick;
            int n = 0;
            foreach (var c in crew) if (c != player) copyDelay[c] = 0.25f + 0.18f * n++;
        }

        void TickCrew(float dt)
        {
            var p = player;
            int slot = 0;
            foreach (var b in crew)
            {
                if (b == p) continue;
                int k = slot++;
                if (!Settings.CrewFollows || Driving)
                {
                    // cada um fica no seu lugar (parado, dançando ou em cima da carreta)
                    b.Tick(Vector3.zero, dt);
                    continue;
                }
                if (copyDelay.TryGetValue(b, out var cd) && cd >= 0)
                {
                    cd -= dt; copyDelay[b] = cd;
                    if (cd < 0 && !b.Busy) b.DoTrick(copyTrick, false);
                }
                if (p.OnTruck && !b.OnTruck && !b.Busy)
                {
                    if (truck.DistanceTo(b.transform.position) < 3.5f) b.ClimbOrDescend();
                    else if (Vector3.Distance(b.transform.position, truck.transform.position) > 40f)
                        b.PlaceOnTruck(truck, truck.transform.InverseTransformPoint(p.transform.position) + new Vector3((k % 2 == 0 ? 0.8f : -0.8f), 0, -1.2f * (k / 2)));
                }
                if (!p.OnTruck && !p.Busy && b.OnTruck && !b.Busy) b.ClimbOrDescend();

                float sp = p.OnTruck ? 0.8f : 1.0f;
                float back = (1.4f + 1.4f * (k / 2)) * sp, side = (k % 2 == 0 ? 1.3f : -1.3f) * sp;
                Vector3 target = p.transform.position - p.transform.forward * back + p.transform.right * side;
                Vector3 to = target - b.transform.position; to.y = 0;
                float dist = to.magnitude;
                Vector3 wish = Vector3.zero;
                if (!b.OnTruck && !p.OnTruck && dist > 45f && !b.Busy)
                {
                    var tp = target; tp.y = city.Height(tp.x, tp.z) + 0.3f;
                    b.Teleport(tp, p.transform.rotation);
                }
                else if (dist > 0.8f && !(b.Dancing && dist < 3f))
                {
                    wish = to / dist * Mathf.Clamp01(dist / 1.5f);
                    if (b.OnTruck && p.OnTruck) wish *= 0.8f;
                    b.Running = dist > 4f || p.HorizontalSpeed > p.WalkSpeed * 1.2f;
                }
                if (!b.OnTruck && p.Grounded && b.Grounded && p.transform.position.y - b.transform.position.y > 0.6f && dist < 3f) b.Jump();
                b.Tick(wish, dt);
            }
        }

        static AudioClip MakeHorn()
        {
            // buzina "de carreta": duas notas juntas
            int sr = 44100, n = sr / 2;
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                data[i] = 0.35f * (Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 311f * t)) * 0.5f + Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 392f * t)) * 0.5f) * 0.6f;
            }
            var c = AudioClip.Create("buzina", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
