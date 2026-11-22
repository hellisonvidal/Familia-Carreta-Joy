using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CarretaJoy
{
    /// <summary>
    /// Ponto de partida: monta luz, céu, pós-processamento, Ponta Grossa, carreta, a turma,
    /// câmera, controles, menus e música. Mundo aberto: a turma corre e dança na rua acompanhando
    /// a carreta (enquanto as pessoas comuns passeiam sentadas dentro dela), ou assume o volante.
    /// </summary>
    public class Game : MonoBehaviour
    {
        public static Game I;

        Camera cam; CameraRig rig; Light sun; Volume volume; Bloom bloom; ColorAdjustments color;
        Material sky;
        PgCity city; Truck truck; Music music; TouchUI ui; MenuUI menu;
        People people; Traffic traffic;
        public Updater updater;
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
        bool tricksOpen = true;

        public int CharCount => ROSTER.Length;
        public string CharName(int i) => ROSTER[Mathf.Clamp(i, 0, ROSTER.Length - 1)].nome;
        public string CharId(int i) => ROSTER[Mathf.Clamp(i, 0, ROSTER.Length - 1)].id;
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

            truck = MakeTruck(Settings.Truck);

            music = new GameObject("Musica").AddComponent<Music>();
            music.OnTrack = t => { if (ui != null && playing) ui.ShowToast("♪ " + t); };
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
                // animações livres (Quaternius, CC0)
                air = KidClips.Load("Animations/Livres/PuloAr_Loop"),
                roll = KidClips.Load("Animations/Livres/Rolamento"),
                slideIn = KidClips.Load("Animations/Livres/Deslize_Inicio"),
                slideLoop = KidClips.Load("Animations/Livres/Deslize_Loop"),
                slideOut = KidClips.Load("Animations/Livres/Deslize_Fim"),
                climb = KidClips.Load("Animations/Livres/Escalar"),
                ninjaIn = KidClips.Load("Animations/Livres/Ninja_Inicio"),
                ninjaLoop = KidClips.Load("Animations/Livres/Ninja_Loop"),
                ninjaOut = KidClips.Load("Animations/Livres/Ninja_Fim"),
                crouch = KidClips.Load("Animations/Livres/Agachado_Loop"),
                dance2 = KidClips.Load("Animations/Livres/Danca2_Loop"),
                // capturas da CMU (uso livre): acrobacias dos combos e danças
                estrela = KidClips.Load("Animations/CMU/Estrela"),
                mortalLateral = KidClips.Load("Animations/CMU/MortalLateral"),
                reversao = KidClips.Load("Animations/CMU/Reversao"),
                chute = KidClips.Load("Animations/CMU/ChuteVoador"),
                giro = KidClips.Load("Animations/CMU/GiroNoAr"),
                breakdance = KidClips.Load("Animations/CMU/Break"),
                moonwalk = KidClips.Load("Animations/CMU/Moonwalk"),
            };
            var danceList = new List<AnimationClip>();
            foreach (var dc in new[] { common.dance, common.dance2, KidClips.Load("Animations/CMU/Charleston_Loop"), KidClips.Load("Animations/CMU/Russa_Loop"), KidClips.Load("Animations/CMU/Salsa_Loop") })
                if (dc != null) danceList.Add(dc);
            common.dances = danceList.ToArray();
            // o Arthur usa as mesmas passadas da turma (as dele tinham os pés virados para fora)
            var arthurClips = common;
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

            // gente na rua e na carreta, e carros pelas ruas
            people = new GameObject("Pessoas").AddComponent<People>();
            people.Init(city, truck, common.dances);
            traffic = new GameObject("Transito").AddComponent<Traffic>();
            traffic.Init(city, truck);
            traffic.Blocked = (p, r) => { foreach (var c in crew) if (!c.OnTruck && !(Driving && c == player) && (c.transform.position - p).sqrMagnitude < r * r) return true; return false; };

            ui = gameObject.AddComponent<TouchUI>();
            menu = gameObject.AddComponent<MenuUI>(); menu.game = this;
            updater = gameObject.AddComponent<Updater>();
            Invoke(nameof(CheckUpdates), 2f);
            rig = cam.gameObject.AddComponent<CameraRig>();
            ui.MapTex = city.GroundTexture; ui.MapWorld = city.GroundRect;
            PlaceAtStart();
            SetPlayer(Mathf.Clamp(Settings.StartChar, 0, crew.Count - 1));
            SetNight(false);
            ApplySettings();
            menu.Open(MenuUI.Page.Main);
        }

        Truck MakeTruck(int variant)
        {
            variant = Mathf.Clamp(variant, 0, Truck.Variants.Length - 1);
            var prefab = Resources.Load<GameObject>("Vehicles/" + Truck.Variants[variant].file);
            if (prefab == null) { variant = 0; prefab = Resources.Load<GameObject>("Vehicles/Carreta"); }
            var t = new GameObject("Carreta").AddComponent<Truck>();
            t.Build(prefab, city.Ring, variant);
            t.SetStops(city.StopDistances);
            t.GroundHeight = city.Height;
            t.cruise = Settings.TruckSpeedValue;
            return t;
        }

        /// <summary>Trocou a carreta no menu: monta a nova no lugar da antiga.</summary>
        void RebuildTruck()
        {
            if (Driving) ToggleDrive(false);
            foreach (var k in crew) k.ResetState();
            var old = truck;
            truck = MakeTruck(Settings.Truck);
            foreach (var k in crew) k.truck = truck;
            people.SetTruck(truck); traffic.SetTruck(truck);
            truck.SetNight(night);
            if (old != null) Destroy(old.gameObject);
        }

        public string TruckName(int i) => Truck.Variants[Mathf.Clamp(i, 0, Truck.Variants.Length - 1)].nome;
        public int TruckCount => Truck.Variants.Length;
        public string TruckFile(int i) => Truck.Variants[Mathf.Clamp(i, 0, Truck.Variants.Length - 1)].file;

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
            score = 0; ui.Score = "0"; festa = 0;
            if (group) { group = false; rig.distance = savedDist; }
            playing = true;
            menu.page = MenuUI.Page.None;
            ui.ShowToast("Ponta Grossa: " + StopName(Settings.StartStop) + "!");
        }

        public void Resume() { }
        public void NextMusic() => music.Next();
        public string MusicTitle => music != null ? music.NowPlaying : "";
        void CheckUpdates() { if (Application.internetReachability != NetworkReachability.NotReachable) updater.Check(); }
        public void GoMenu() { if (Driving) ToggleDrive(false); if (group) { group = false; rig.distance = savedDist; } playing = false; menu.Open(MenuUI.Page.Main); }
        public void PreviewStart() { if (!playing) PlaceAtStart(); }
        public void SelectPlayer(int i) { if (Driving) ToggleDrive(false); SetPlayer(i); ui.ShowToast("Agora: " + player.Name); }
        public void TruckAuto() { if (Driving) ToggleDrive(false); else truck.ReturnToRoute(); ui.ShowToast("Carreta no piloto automático"); }

        /// <summary>Carreta no ponto de partida escolhido; a turma espalhada livremente dos dois lados na rua.</summary>
        void PlaceAtStart()
        {
            if (truck.Variant != Mathf.Clamp(Settings.Truck, 0, Truck.Variants.Length - 1)) RebuildTruck();
            int s = Mathf.Clamp(Settings.StartStop, 0, Mathf.Max(0, city.StopDistancesByStop.Count - 1));
            float d = city.StopDistancesByStop.Count > 0 ? city.StopDistancesByStop[s] : 0;
            truck.StartAt(d);
            var tp = truck.transform;
            city.EnsureLoaded(tp.position, true);
            crewAI.Clear();
            for (int i = 0; i < crew.Count; i++)
            {
                var k = crew[i];
                k.ResetState();
                // espalha dos dois lados da rua acompanhando o sentido da carreta (sempre dentro da pista livre, longe de paredes)
                int side = (i % 2 == 0) ? 1 : -1;
                int row = i / 2;
                float lat = 1.8f + (row % 2) * 0.9f;
                float zOff = 3.8f - row * 2.5f;
                var p = truck.FreeStreetPoint(side, lat, zOff);
                p.y = city.Height(p.x, p.z) + 0.15f;
                Vector3 fwd = tp.forward; fwd.y = 0f;
                k.Teleport(p, Quaternion.LookRotation(fwd.sqrMagnitude > 0.01f ? fwd : Vector3.forward, Vector3.up));
                crewAI[k] = new CrewAI
                {
                    side = side,
                    lat = lat,
                    zOff = zOff,
                    retargetT = 2.0f + i * 0.7f,
                    climbT = 16f + i * 5.5f,
                    trickT = 7f + i * 2.3f,
                    lastPos = p,
                };
                if (ROSTER[i].id == "Afonso") k.StartCrouched();   // a pose dele: começa agachado e levanta ao andar
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
            city.detailRadius = Settings.Quality == 0 ? 0f : Settings.Quality == 1 ? 180f : 260f;
            bloom.active = Settings.Quality > 0;
            rig.mode = Settings.CamMode;
            if (truck != null) truck.cruise = Settings.TruckSpeedValue;
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
                // a carreta volta sozinha para o percurso e segue a volta pela cidade
                truck.ReturnToRoute();
                ui.ShowToast("A carreta segue o percurso sozinha");
            }
        }

        public void AddScore(int pts, string msg)
        {
            score += pts;
            if (!group) festa = Mathf.Min(FESTA_MAX, festa + pts);
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
                people.Tick(truck.transform.position, dt, truck.Stopped);
                traffic.Tick(truck.transform.position, dt);
                AccompanyTruckOnStreet(player, crew.Count - 1, dt);
                TickCrew(dt);
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

            ui.Festa = group ? groupT / GROUP_TIME : festa / FESTA_MAX;
            ui.FestaOn = group;
            if (group) TickGroup(dt);
            else
            {
                if (Driving) TickDriving(dt);
                else TickOnFoot(dt);
                // os outros personagens (livres ao lado da carreta, subindo algumas vezes)
                TickCrew(dt);
                if (festa >= FESTA_MAX && !Driving && !player.Busy && player.Trick == null && (player.Grounded || player.OnTruck)) StartGroup();
            }

            Vector3 focus = Driving ? truck.transform.position : player.transform.position;
            city.EnsureLoaded(focus, false);
            truck.Tick(dt, player.transform.position, player.OnTruck || player.Busy || Driving, music.Beat);
            if (truck.Stopped && !Driving && (player.OnTruck || truck.DistanceTo(player.transform.position) < 12f) && player.Dancing) { festaAcc += dt; if (festaAcc > 1f) { festaAcc = 0; AddScore(10, null); } }
            people.Tick(focus, dt, truck.Stopped);
            traffic.Tick(focus, dt);

            // som: mais alto perto da carreta
            float d = Vector3.Distance(cam.transform.position, truck.transform.position);
            music.volume = Settings.Music * Mathf.Lerp(1f, 0.18f, Mathf.InverseLerp(10f, 160f, d));
            Lights();
            UpdateMap(focus);
        }

        void TickOnFoot(float dt)
        {
            bool nearClimb = truck.DistanceTo(player.transform.position) < 3.8f;
            bool nearDrive = truck.DistanceTo(player.transform.position) < 6f;
            if (ui.Pressed("correr")) { player.Running = true; ui.ShowToast("Correndo"); }
            if (ui.Pressed("andar")) { player.Running = false; ui.ShowToast("Andando"); }
            ui.B["correr"].toggled = player.Running; ui.B["andar"].toggled = !player.Running;
            if (ui.Pressed("manobras")) { tricksOpen = !tricksOpen; ui.B["manobras"].toggled = tricksOpen; }
            if (ui.Pressed("pular")) player.Jump();
            foreach (var b in TrickBtns) if (ui.Pressed(b)) OnTrickButton(b);
            TickCombos();
            if (ui.Pressed("dancar"))
            {
                player.ToggleDance();
                SyncCrewDance();
                if (player.Dancing) AddScore(30, (player.OnTruck ? "FESTA NA CARRETA! " : truck.Stopped && nearDrive ? "FESTA NA RUA! " : "DANÇA: ") + Kid.DanceNames[Mathf.Clamp(player.DanceStyle, 0, Kid.DanceNames.Length - 1)].ToUpper());
            }
            if (ui.Pressed("subir")) { if (!player.ClimbOrDescend(true) && !player.OnTruck) ui.ShowToast("Chegue perto da carreta!"); }

            player.Tick(rig.Flat(ui.Move), dt);

            SetVisible(true);
            var sb = ui.B["subir"];
            sb.label = player.OnTruck ? "DESCER" : "SUBIR";
            sb.visible = player.OnTruck || nearClimb;
            ui.B["dirigir"].visible = !player.OnTruck && nearDrive;
            ui.B["dirigir"].label = "DIRIGIR";
            ui.Info = player.OnTruck ? (truck.Stopped ? "Parada de festa! Dancem!" : "Na carreta! Toque DESCER para voltar à rua")
                                     : (truck.Stopped && nearDrive) ? "Parada de festa! Dancem na rua!"
                                     : nearClimb ? "Toque SUBIR ou DIRIGIR"
                                     : nearDrive ? "Toque DIRIGIR para assumir o volante"
                                     : "Acompanhe a carreta por Ponta Grossa!";
        }

        // ---------------- combos ----------------
        static readonly string[] TrickBtns = { "mortal", "flip", "pirueta", "rolar", "deslizar", "ninja" };
        /// <summary>Dois botões seguidos (menos de 1 s entre eles) viram uma acrobacia especial.</summary>
        public static readonly (string a, string b, string move)[] Combos = {
            ("mortal", "flip", "estrela"), ("flip", "mortal", "reversao"), ("pirueta", "pirueta", "giro"),
            ("rolar", "mortal", "mortal_lateral"), ("ninja", "flip", "chute"), ("deslizar", "pirueta", "break"), ("rolar", "deslizar", "moonwalk") };
        /// <summary>Dançando, os botões de manobra trocam o estilo (ou puxam um passo especial).</summary>
        public static readonly (string btn, int style, string move)[] DanceCombos = {
            ("ninja", 1, null), ("mortal", 2, null), ("flip", 3, null), ("pirueta", 4, null), ("rolar", -1, "break"), ("deslizar", -1, "moonwalk") };
        public static readonly string[] BtnNames = { "mortal", "MORTAL", "flip", "FLIP", "pirueta", "PIRUETA", "rolar", "ROLAR", "deslizar", "DESLIZAR", "ninja", "NINJA" };
        public static string BtnName(string id) { for (int i = 0; i < BtnNames.Length; i += 2) if (BtnNames[i] == id) return BtnNames[i + 1]; return id.ToUpper(); }

        string lastBtn; float lastBtnT = -10f;
        string pending; float pendingT; bool pendingCombo;
        int chain; float lastTrickEnd = -10f; bool wasTrick;

        void OnTrickButton(string b)
        {
            float now = Time.time;
            if (player.Dancing && player.Trick == null && !player.Busy)
            {
                foreach (var dc in DanceCombos)
                {
                    if (dc.btn != b) continue;
                    if (dc.move != null) { StartMove(dc.move, true); return; }
                    player.SetDanceStyle(dc.style);
                    SyncCrewDance();
                    AddScore(80, "COMBO DE DANÇA: " + Kid.DanceNames[Mathf.Clamp(dc.style, 0, Kid.DanceNames.Length - 1)].ToUpper());
                    return;
                }
            }
            string move = b; bool combo = false;
            if (lastBtn != null && now - lastBtnT < 1.0f)
                foreach (var c in Combos) if (c.a == lastBtn && c.b == b) { move = c.move; combo = true; break; }
            lastBtn = combo ? null : b; lastBtnT = now;
            StartMove(move, combo);
        }

        void StartMove(string move, bool combo)
        {
            if (player.Trick != null || player.Busy)
            {
                // ainda na manobra anterior: guarda para emendar logo em seguida
                if (pending == null || combo) { pending = move; pendingT = Time.time; pendingCombo = combo; }
                return;
            }
            DoMove(move, combo);
        }

        void DoMove(string move, bool combo)
        {
            bool linked = Time.time - lastTrickEnd < 1.2f;
            player.DoTrick(move);
            if (player.Trick == null) return;
            chain = linked ? chain + 1 : 1;
            if (combo) { AddScore(150 + 50 * chain, "COMBO: " + Kid.TrickTitle(move) + "!"); people.Cheer(false); }
            else if (chain >= 2) AddScore(40 * chain, "SEQUÊNCIA x" + chain + "!");
            if (chain == 5) AddScore(500, "SUPER COMBO x5!");
            CrewCopy(move);
        }

        void TickCombos()
        {
            bool tr = player.Trick != null;
            if (wasTrick && !tr) lastTrickEnd = Time.time;
            wasTrick = tr;
            if (pending != null)
            {
                if (Time.time - pendingT > 1.6f) pending = null;
                else if (!tr && !player.Busy) { var m = pending; pending = null; DoMove(m, pendingCombo); }
            }
        }

        // ---------------- festa da turma ----------------
        const float FESTA_MAX = 2500f, GROUP_TIME = 30f;
        float festa, groupT, savedDist;
        bool group; int groupStyle;
        Vector3 groupFace;
        readonly Dictionary<Kid, Vector3> groupSlot = new Dictionary<Kid, Vector3>();
        readonly Dictionary<Kid, float> olaDelay = new Dictionary<Kid, float>();

        /// <summary>Medidor cheio: a turma toda se junta em fileiras, de frente para a câmera, e dança junto.</summary>
        void StartGroup()
        {
            group = true; groupT = GROUP_TIME; groupStyle = 0;
            Vector3 c = player.transform.position;
            if (player.OnTruck)
            {
                var tp = truck.transform;
                c = tp.TransformPoint(new Vector3(truck.HalfWidth + 6f, 0, 0));
            }
            c.y = city.Height(c.x, c.z);
            // de frente para a câmera
            var f = cam.transform.position - c; f.y = 0;
            if (f.sqrMagnitude < 0.01f) f = -player.transform.forward;
            f.Normalize(); groupFace = f;
            var right = Vector3.Cross(Vector3.up, f).normalized;
            // fileiras: o jogador no meio da primeira, os outros atrás em fileiras de 4
            var order = new List<Kid> { player };
            foreach (var k in crew) if (k != player) order.Add(k);
            groupSlot.Clear();
            for (int i = 0; i < order.Count; i++)
            {
                int row, col; float cols;
                if (i == 0) { row = 0; col = 0; cols = 1; }
                else { int j = i - 1; row = 1 + j / 4; col = j % 4; cols = 4; }
                var pos = c - f * (row * 1.9f) + right * ((col - (cols - 1) / 2f) * 1.6f);
                pos.y = city.Height(pos.x, pos.z) + 0.1f;
                var k = order[i];
                groupSlot[k] = pos;
                k.ResetState();
                k.Teleport(pos, Quaternion.LookRotation(f));
            }
            foreach (var k in crew) k.SetDanceStyle(groupStyle);
            savedDist = rig.distance; rig.distance = Mathf.Max(rig.distance, 11f);
            people.Cheer(true);
            ui.ShowToast("FESTA DA TURMA! Todos juntos!");
        }

        void GroupStyle(int st)
        {
            groupStyle = Mathf.Clamp(st, 0, Kid.DanceNames.Length - 1);
            foreach (var k in crew) { if (k.Trick == null && !k.Busy) k.SetDanceStyle(groupStyle); }
            AddScore(60, "TODOS: " + Kid.DanceNames[groupStyle].ToUpper());
            people.Cheer(false);
        }

        void TickGroup(float dt)
        {
            SetVisible(true);
            ui.B["subir"].visible = false; ui.B["dirigir"].visible = false;
            ui.Info = "Festa da turma! Os botões mudam o passo de todos";
            // passos de dança enfileirados: os botões mudam o passo da turma inteira ao mesmo tempo
            if (ui.Pressed("dancar")) GroupStyle((groupStyle + 1) % Kid.DanceNames.Length);
            if (ui.Pressed("ninja")) GroupStyle(1);
            if (ui.Pressed("mortal")) GroupStyle(2);
            if (ui.Pressed("flip")) GroupStyle(3);
            if (ui.Pressed("pirueta")) GroupStyle(4);
            if (ui.Pressed("pular"))
            {
                // "ola": cada um pula um pouquinho depois do vizinho
                int i = 0; foreach (var k in crew) olaDelay[k] = 0.12f * i++;
                AddScore(80, "OLA!");
            }
            if (ui.Pressed("rolar")) { foreach (var k in crew) k.DoTrick("giro", false); AddScore(120, "GIRO DA TURMA!"); }
            if (ui.Pressed("deslizar")) { foreach (var k in crew) k.DoTrick("moonwalk", false); AddScore(120, "MOONWALK DA TURMA!"); }
            foreach (var k in crew)
            {
                if (olaDelay.TryGetValue(k, out var od) && od >= 0) { od -= dt; olaDelay[k] = od; if (od < 0) k.Jump(); }
                bool free = k.Trick == null && !k.Busy && k.Grounded;
                if (free && groupSlot.TryGetValue(k, out var slot))
                {
                    // volta para o lugar na fileira e retoma o passo da turma
                    var to = slot - k.transform.position; to.y = 0;
                    if (to.magnitude > 0.25f) { var np = k.transform.position + to * Mathf.Min(1f, dt * 3f); np.y = slot.y; k.Teleport(np, Quaternion.LookRotation(groupFace)); }
                    if (!k.Dancing) k.SetDanceStyle(groupStyle);
                    k.transform.rotation = Quaternion.Slerp(k.transform.rotation, Quaternion.LookRotation(groupFace), 1 - Mathf.Exp(-dt * 6f));
                }
                k.Tick(Vector3.zero, dt);
            }
            festaAcc += dt; if (festaAcc > 1f) { festaAcc = 0; AddScore(15, null); }
            groupT -= dt;
            if (groupT <= 0) EndGroup();
        }

        void EndGroup()
        {
            group = false; festa = 0;
            rig.distance = savedDist;
            foreach (var k in crew) k.StopDance();
            AddScore(500, "FESTA COMPLETA!");
            people.Cheer(true);
        }

        void SyncCrewDance()
        {
            if (!Settings.CrewFollows) return;
            foreach (var c in crew)
            {
                if (c == player) continue;
                if (player.Dancing) c.SetDanceStyle(player.DanceStyle);
                else c.StopDance();
            }
        }

        void TickDriving(float dt)
        {
            SetVisible(false);
            ui.B["dirigir"].visible = true; ui.B["dirigir"].label = "SAIR";
            if (ui.Pressed("seguro"))
            {
                Settings.SafeDrive = !Settings.SafeDrive; Settings.Save();
                ui.ShowToast(Settings.SafeDrive ? "Modo seguro: devagar (até 15 km/h)" : "Modo livre: mais rápido");
            }
            truck.SafeMode = Settings.SafeDrive;
            ui.B["seguro"].toggled = Settings.SafeDrive; ui.B["seguro"].label = Settings.SafeDrive ? "SEGURO" : "LIVRE";
            // joystick: lados viram, para frente acelera, para trás freia/ré
            truck.Drive(ui.Move.x, ui.Throttle, dt);
            if (ui.B["buzina"].held) { if (!horn.isPlaying) horn.Play(); } else if (horn.isPlaying) horn.Stop();
            // o motorista vai junto (escondido na cabine)
            player.FollowCab(truck.transform.TransformPoint(new Vector3(0, 1.2f, Truck.LENGTH / 2f - 2.5f)));
            ui.Info = Mathf.RoundToInt(Mathf.Abs(truck.ManualSpeed) * 3.6f) + " km/h";
        }

        void SetVisible(bool foot)
        {
            foreach (var id in new[] { "pular", "correr", "andar", "manobras", "dancar", "subir" }) ui.B[id].visible = foot;
            // manobras: abertas pelo botão MANOBRAS (o controle de videogame alcança sempre)
            foreach (var id in new[] { "mortal", "flip", "pirueta", "rolar", "deslizar", "ninja" }) ui.B[id].visible = foot && (tricksOpen || Pad.Connected);
            ui.B["buzina"].visible = !foot;
            ui.B["seguro"].visible = !foot;
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
            traffic.Dots(ui.Dots);
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

        // estado livre de cada membro da turma acompanhando a carreta na rua (com subidas ocasionais)
        class CrewAI
        {
            public int side = 1;           // -1 esquerda / +1 direita da carreta
            public float lat = 2.2f;       // distância livre além da lateral da carreta (na rua, sem entrar em prédios)
            public float zOff = 0f;        // posição livre à frente/atrás ao longo da carreta (m)
            public float retargetT = 3f;   // tempo para escolher novo ponto livre na rua
            public float climbT = 22f;     // contagem até subir brevemente na carreta
            public float rideLeft = 0f;    // quanto tempo fica em cima antes de voltar à rua
            public bool wantClimb;         // indo até a lateral para subir
            public float trickT = 9f;      // contagem para pulo/acrobacia espontânea na rua
            public Vector3 lastPos;        // detecção de travamento em paredes/esquinas
            public float stuckT;
        }
        readonly Dictionary<Kid, CrewAI> crewAI = new Dictionary<Kid, CrewAI>();
        // danças em pé para a turma na rua (exclui Russa_Loop = 3 para ninguém começar se revirando no chão)
        static readonly int[] StreetDances = { 0, 1, 2, 4 };
        static readonly string[] StreetTricks = { "flip", "pirueta", "mortal" };

        int CountCrewOnTruck()
        {
            int n = 0;
            foreach (var c in crew) if (c.OnTruck) n++;
            return n;
        }

        void TickCrew(float dt)
        {
            var p = player;
            int slot = 0;
            foreach (var b in crew)
            {
                if (b == p) continue;
                int k = slot++;
                if (Settings.CrewMode == 0 || !playing || (Driving && Settings.CrewMode != 2))
                {
                    AccompanyTruckOnStreet(b, k, dt);
                    continue;
                }
                if (Settings.CrewMode == 2)
                {
                    b.Tick(Vector3.zero, dt);
                    continue;
                }
                if (copyDelay.TryGetValue(b, out var cd) && cd >= 0)
                {
                    cd -= dt; copyDelay[b] = cd;
                    if (cd < 0 && !b.Busy) b.DoTrick(copyTrick, false);
                }
                if (!p.OnTruck && !p.Busy && b.OnTruck && !b.Busy) b.ClimbOrDescend(false);
                float back = 1.6f + 1.5f * (k / 2), side = k % 2 == 0 ? 1.4f : -1.4f;
                Vector3 target = p.transform.position - p.transform.forward * back + p.transform.right * side;
                Vector3 to = target - b.transform.position; to.y = 0;
                float dist = to.magnitude;
                Vector3 wish = Vector3.zero;
                if (!b.OnTruck && dist > 45f && !b.Busy)
                {
                    var tp = target; tp.y = city.Height(tp.x, tp.z) + 0.3f;
                    b.Teleport(tp, p.transform.rotation);
                }
                else if (dist > 0.85f && !(b.Dancing && dist < 3f))
                {
                    wish = b.OnTruck ? (to / dist) * 0.7f : truck.SteerAround(b.transform.position, target) * Mathf.Clamp01(dist / 1.5f);
                    b.Running = dist > 3.5f || p.HorizontalSpeed > p.WalkSpeed * 1.15f;
                }
                if (!b.OnTruck && p.Grounded && b.Grounded && p.transform.position.y - b.transform.position.y > 0.6f && dist < 3f) b.Jump();
                b.Tick(wish, dt);
            }
            SeparateCrew(dt);
        }

        /// <summary>Separação horizontal suave entre personagens na rua para ninguém atravessar o colega.</summary>
        void SeparateCrew(float dt)
        {
            const float minDist = 0.90f;
            float maxPush = 2.5f * dt;
            for (int i = 0; i < crew.Count; i++)
            {
                var a = crew[i];
                if (a.Busy || a.OnTruck || (Driving && a == player)) continue;
                for (int j = i + 1; j < crew.Count; j++)
                {
                    var b = crew[j];
                    if (b.Busy || b.OnTruck || (Driving && b == player)) continue;
                    Vector3 d = a.transform.position - b.transform.position;
                    d.y = 0f;
                    float dist = d.magnitude;
                    if (dist >= minDist || dist < 1e-4f) continue;
                    float pen = Mathf.Min((minDist - dist) * 0.5f, maxPush);
                    Vector3 dir = d / dist;
                    if (a != player) a.PushHorizontal(dir * pen);
                    if (b != player) b.PushHorizontal(-dir * pen);
                }
            }
        }

        /// <summary>
        /// Movimentação livre na rua ao lado da carreta (desviando da carroceria), com danças em pé
        /// nas paradas e subidas ocasionais na carreta (1-2 personagens por vez por alguns segundos).
        /// </summary>
        void AccompanyTruckOnStreet(Kid b, int k, float dt)
        {
            if (!crewAI.TryGetValue(b, out var ai))
            {
                ai = new CrewAI
                {
                    side = (k % 2 == 0) ? 1 : -1,
                    lat = 3.2f + (k % 3) * 1.1f,
                    zOff = 3.5f - (k / 2) * 2.6f,
                    retargetT = 2f + k * 0.6f,
                    climbT = 18f + k * 5f,
                    trickT = 8f + k * 2.5f
                };
                crewAI[b] = ai;
            }

            // se está em cima da carreta: dança um pouco e depois volta para a rua!
            if (b.OnTruck)
            {
                ai.wantClimb = false;
                ai.rideLeft -= dt;
                if ((truck.Stopped || ai.rideLeft <= 0f) && !b.Busy && b.Trick == null)
                {
                    b.StopDance();
                    b.ClimbOrDescend(false);
                    ai.climbT = Random.Range(24f, 44f);
                }
                else if (!b.Dancing && !b.Busy && b.Trick == null)
                {
                    b.SetDanceStyle(StreetDances[k % StreetDances.Length]);
                }
                b.Tick(Vector3.zero, dt);
                return;
            }

            // atualiza ponto livre na rua ao lado da carreta periodicamente (faixa segura na pista, longe de paredes)
            ai.retargetT -= dt;
            if (ai.retargetT <= 0f)
            {
                ai.retargetT = Random.Range(2.8f, 5.5f);
                ai.lat = Random.Range(1.55f, 3.2f);
                ai.zOff = Random.Range(-7.5f, 4.8f);
            }

            // se ficou muito longe (ex: teletransporte/curva fechada), reaparece limpo na rua do seu lado
            Vector3 goal = truck.FreeStreetPoint(ai.side, ai.lat, ai.zOff);
            Vector3 toGoal = goal - b.transform.position; toGoal.y = 0f;
            float dist = toGoal.magnitude;
            if (dist > 45f && !b.Busy)
            {
                ai.wantClimb = false; ai.stuckT = 0f;
                var tp = goal; tp.y = city.Height(tp.x, tp.z) + 0.2f;
                b.ResetState();
                b.Teleport(tp, truck.transform.rotation);
                ai.lastPos = tp;
                b.Tick(Vector3.zero, dt);
                return;
            }

            // parada de festa: dança em pé na rua (nunca Russa_Loop no chão!)
            if (truck.Stopped)
            {
                ai.wantClimb = false;
                if (dist > 1.8f && !b.Dancing && !b.Busy && b.Trick == null)
                {
                    Vector3 dir = truck.SteerAround(b.transform.position, goal);
                    b.Running = dist > 4.5f;
                    b.Tick(dir * Mathf.Clamp01(dist / 1.5f), dt);
                    CheckCrewStuck(b, ai, dt, true);
                }
                else
                {
                    if (!b.Dancing && !b.Busy && b.Trick == null)
                        b.SetDanceStyle(StreetDances[(k + (int)(Time.time * 0.05f)) % StreetDances.Length]);
                    ai.stuckT = 0f; ai.lastPos = b.transform.position;
                    b.Tick(Vector3.zero, dt);
                }
                return;
            }

            // ocasionalmente um ou outro personagem decide subir na carreta por alguns segundos
            if (!ai.wantClimb)
            {
                ai.climbT -= dt;
                if (ai.climbT <= 0f)
                {
                    if (playing && CountCrewOnTruck() < 2)
                    {
                        ai.wantClimb = true;
                        ai.rideLeft = Random.Range(8f, 14f);
                    }
                    else ai.climbT = Random.Range(14f, 26f);
                }
            }

            if (ai.wantClimb && !b.Busy && b.Trick == null)
            {
                if (b.Dancing) b.StopDance();
                float td = truck.DistanceTo(b.transform.position);
                if (td < 3.4f)
                {
                    if (b.ClimbOrDescend(false)) { ai.wantClimb = false; ai.stuckT = 0f; b.Tick(Vector3.zero, dt); return; }
                }
                // aproxima pela lateral do próprio lado da carreta (nunca atravessando)
                Vector3 sidePt = truck.FreeStreetPoint(ai.side, 1.5f, Mathf.Clamp(ai.zOff, -4f, 3f));
                Vector3 dir = truck.SteerAround(b.transform.position, sidePt);
                b.Running = true;
                b.Tick(dir, dt);
                CheckCrewStuck(b, ai, dt, true);
                return;
            }

            // movimentação livre acompanhando a carreta na rua ao lado dela
            if (b.Dancing && dist > 1.5f) b.StopDance();
            Vector3 leadGoal = truck.FreeStreetPoint(ai.side, ai.lat, ai.zOff + 1.4f);
            Vector3 toLead = leadGoal - b.transform.position; toLead.y = 0f;
            float leadDist = toLead.magnitude;
            float truckSpd = new Vector3(truck.Velocity.x, 0f, truck.Velocity.z).magnitude;
            Vector3 wish = Vector3.zero;
            if (leadDist > 1.2f)
            {
                Vector3 dir = truck.SteerAround(b.transform.position, leadGoal);
                wish = dir * Mathf.Clamp01(leadDist / 1.8f);
                b.Running = leadDist > 3.2f || truckSpd > b.WalkSpeed * 1.15f;
            }
            else if (truckSpd > 0.35f)
            {
                Vector3 fwd = truck.transform.forward; fwd.y = 0f;
                if (fwd.sqrMagnitude > 0.01f) wish = fwd.normalized * Mathf.Clamp01(truckSpd / Mathf.Max(0.5f, b.WalkSpeed));
                b.Running = truckSpd > b.WalkSpeed * 1.15f;
            }

            // de vez em quando faz um pulo ou acrobacia alegre na rua enquanto acompanha
            ai.trickT -= dt;
            if (playing && ai.trickT <= 0f && b.Grounded && !b.Busy && b.Trick == null && leadDist < 5f)
            {
                ai.trickT = Random.Range(10f, 20f);
                if (Random.value < 0.45f) b.Jump();
                else b.DoTrick(StreetTricks[Random.Range(0, StreetTricks.Length)], false);
            }
            b.Tick(wish, dt);
            CheckCrewStuck(b, ai, dt, wish.sqrMagnitude > 0.16f);
        }

        /// <summary>Se um personagem encostar/travar na parede de um prédio, puxa-o de volta para a pista livre.</summary>
        void CheckCrewStuck(Kid b, CrewAI ai, float dt, bool tryingToMove)
        {
            Vector3 moved = b.transform.position - ai.lastPos; moved.y = 0f;
            ai.lastPos = b.transform.position;
            bool blocked = tryingToMove && !b.Busy && b.Trick == null && (b.HitWall || moved.magnitude < 0.35f * dt);
            if (!blocked) { ai.stuckT = Mathf.Max(0f, ai.stuckT - dt * 2f); return; }
            ai.stuckT += dt;
            if (ai.stuckT > 0.45f)
            {
                // puxa o alvo para mais perto da pista livre e ajusta a posição longitudinal
                ai.lat = 1.45f;
                ai.zOff = Mathf.Clamp(ai.zOff - 1.5f, -7.5f, 3.5f);
                ai.wantClimb = false;
            }
            if (ai.stuckT > 2.0f)
            {
                // preso em quina/muro fechado: recoloca limpo na pista livre atrás/ao lado da carreta
                ai.stuckT = 0f;
                ai.side = -ai.side;
                ai.lat = 1.6f;
                Vector3 safe = truck.FreeStreetPoint(ai.side, ai.lat, -3.5f);
                safe.y = city.Height(safe.x, safe.z) + 0.15f;
                b.Teleport(safe, truck.transform.rotation);
                ai.lastPos = safe;
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
