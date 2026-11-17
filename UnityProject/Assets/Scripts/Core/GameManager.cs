using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

namespace FamiliaCarretaJoy
{
    /// <summary>
    /// Main game manager that coordinates all game systems.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("References")]
        public GameState gameState;
        public CityController city;
        public TruckController truck;
        public CharacterController player;
        public List<CharacterController> allCharacters = new List<CharacterController>();
        public DanceController danceController;
        public MusicController musicController;
        public UIManager uiManager;
        public InputManager inputManager;

        [Header("Settings")]
        public int currentCityIndex = 0;
        public bool nightMode = false;
        public string currentTruckVariant = "arcoiris";
        public string currentCharacter = "arthur";
        public int gameDurationMinutes = 10;

        [Header("Save Data")]
        public SaveData saveData;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadSaveData();
            InitializeSystems();
        }

        private void InitializeSystems()
        {
            // Ensure GameState exists
            if (gameState == null)
                gameState = GetComponent<GameState>() ?? gameObject.AddComponent<GameState>();

            // Initialize managers
            if (musicController == null)
                musicController = GetComponent<MusicController>() ?? gameObject.AddComponent<MusicController>();

            if (danceController == null)
                danceController = GetComponent<DanceController>() ?? gameObject.AddComponent<DanceController>();

            if (uiManager == null)
                uiManager = FindObjectOfType<UIManager>();

            if (inputManager == null)
                inputManager = GetComponent<InputManager>() ?? gameObject.AddComponent<InputManager>();
        }

        public void StartNewGame()
        {
            gameState.ResetForNewGame();
            gameState.minutes = gameDurationMinutes;
            gameState.cityIndex = currentCityIndex;
            gameState.night = nightMode;
            gameState.truckVariant = GameConstants.TruckVariants.FirstOrDefault(v => v.id == currentTruckVariant) ?? GameConstants.TruckVariants[0];

            // Load city
            StartCoroutine(LoadCityRoutine());
        }

        private IEnumerator LoadCityRoutine()
        {
            gameState.phase = GamePhase.Loading;

            // Load city
            var cityDef = GameConstants.Cities[currentCityIndex];
            if (city == null)
            {
                var cityObj = new GameObject("City");
                city = cityObj.AddComponent<CityController>();
            }
            city.Initialize(cityDef, nightMode);
            gameState.cityDef = cityDef;

            // Load truck
            if (truck == null)
            {
                var truckObj = new GameObject("Truck");
                truck = truckObj.AddComponent<TruckController>();
            }
            truck.Initialize(gameState.truckVariant, CreateTruckModel(gameState.truckVariant));
            gameState.truck = truck;

            // Position truck at start
            truck.x = city.startBlock.cx;
            truck.z = -CityController.RingRadius;
            truck.yaw = Mathf.PI / 2f;
            truck.velocity = 0f;
            truck.autoS = 0f;
            truck.autoDist = 0f;
            truck.route = null;
            truck.inited = false;
            truck.Place(city);

            // Create characters
            CreateCharacters();

            // Set player
            player = allCharacters.FirstOrDefault(c => c.def.id == currentCharacter) ?? allCharacters[0];
            gameState.player = player;
            player.controlled = true;

            // Initialize dance and music
            danceController.Initialize(musicController);
            musicController.Play(cityDef.musica);

            // Setup camera
            SetupCamera();

            // Start intro phase
            gameState.phase = GamePhase.Intro;
            gameState.phaseTime = 0f;

            // Show intro hint
            if (player.def.poseInicial)
                uiManager.ShowHint("Mexa o joystick para o Afonso levantar e sair correndo!", 4f);
            else
                uiManager.ShowHint("A cidade é toda sua! A carreta vai dar a volta: siga, suba e dance!", 4f);

            uiManager.ShowHUD();
            yield return null;
        }

        private GameObject CreateTruckModel(TruckVariant variant)
        {
            // Create a procedural truck model or load from resources
            var truckModel = new GameObject("TruckModel");
            // In a full implementation, this would load the actual GLB model
            // For now, create a simple placeholder
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.transform.SetParent(truckModel.transform);
            body.transform.localScale = new Vector3(2.5f, 4f, GameConstants.TruckLength);
            body.transform.localPosition = new Vector3(0f, 2f, 0f);
            var mr = body.GetComponent<MeshRenderer>();
            mr.material = new Material(Shader.Find("Standard"));
            mr.material.color = Color.HSVToRGB(variant.hue[0], variant.hue[1], 1f);
            DestroyImmediate(body.GetComponent<Collider>());
            return truckModel;
        }

        private void CreateCharacters()
        {
            allCharacters.Clear();

            foreach (var charDef in GameConstants.GetCharacterDefinitions())
            {
                var charObj = new GameObject($"Character_{charDef.id}");
                var controller = charObj.AddComponent<CharacterController>();

                // Create body
                var bodyObj = new GameObject("Body");
                var body = bodyObj.AddComponent<CharacterBody>();
                bodyObj.transform.SetParent(charObj.transform);

                // Initialize with definition and costume
                string costumeKey = charDef.kid ? saveData.costumes[charDef.id] : charDef.roupa;
                body.Initialize(charDef, GameConstants.GetCostume(costumeKey), null);

                controller.Initialize(charDef, body);
                allCharacters.Add(controller);
            }

            // Position characters on truck roof
            var spots = GetRoofSpots(truck);
            for (int i = 0; i < allCharacters.Count; i++)
            {
                var character = allCharacters[i];
                character.frame = CharacterFrame.Truck;
                character.pos = spots[i % spots.Length];
                character.yaw = (character.pos.x > 0 ? 1 : -1) * Mathf.PI / 2f;
                character.mode = CharacterMode.Normal;
                character.grounded = true;
            }

            gameState.actors = allCharacters;
        }

        private Vector3[] GetRoofSpots(TruckController truck)
        {
            var spots = new List<Vector3>();
            float[] zs = {
                truck.roofZMax - 1.2f,
                truck.roofZMax - 3.6f,
                truck.roofZMax - 6f,
                truck.roofZMax - 8.4f,
                truck.roofZMin + 1.2f
            };
            foreach (float z in zs)
            {
                foreach (float x in new float[] { -0.95f, 0.95f, 0f })
                {
                    spots.Add(new Vector3(x, truck.roofY, z));
                }
            }
            return spots.ToArray();
        }

        private void SetupCamera()
        {
            Camera.main.transform.position = new Vector3(0f, 10f, -15f);
            Camera.main.transform.LookAt(player.transform);
        }

        private void Update()
        {
            if (gameState.phase == GamePhase.Play && !gameState.paused && !gameState.freeze)
            {
                float dt = Time.deltaTime;

                // Update truck
                var driveInput = new TruckController.DriveInput
                {
                    playerControlled = gameState.player.mode == CharacterMode.Drive,
                    throttle = inputManager.GetThrottle(),
                    steer = inputManager.GetSteer(),
                    autoSpeed = CalculateAutopilotSpeed()
                };
                truck.Drive(dt, city, driveInput);

                // Update characters
                foreach (var character in allCharacters)
                {
                    var input = character.controlled ? inputManager.GetCharacterInput() : GetAIInput(character, dt);
                    character.UpdateCharacter(dt, gameState, input);
                }

                // Update city
                city.UpdateNotes(Time.time, dt);

                // Update truck effects
                truck.UpdateEffects(dt, Time.time, gameState.beat, musicController.GetEnergy(), gameState.dancing);

                // Update camera
                UpdateCamera(dt);

                // Update dance
                if (gameState.dancing)
                {
                    danceController.Update(dt, gameState);
                }

                // Check for party stops
                CheckPartyStops();

                // Update HUD
                uiManager.UpdateHUD();
            }
        }

        private float CalculateAutopilotSpeed()
        {
            float remDist = Mathf.Max(0f, city.GetRingLength() - truck.autoDist) + (truck.route != null ? 60f : 0f);
            float remTime = Mathf.Max(4f, gameState.timeLeft - gameState.visitedStops.Count * 24f);
            return Mathf.Clamp(remDist / remTime, remDist > 5f ? 1.6f : 0f, 10f);
        }

        private InputManager.InputData GetAIInput(CharacterController character, float dt)
        {
            // Simplified AI - in full implementation, port the complete AI from actor.js
            character.ai.timer += dt;

            if (character.mode != CharacterMode.Normal && character.mode != CharacterMode.Stand)
                return new InputManager.InputData { mx = 0f, mz = 0f, want = 0f };

            // Party mode
            if (gameState.partyStop != null && character.partySpot != null)
            {
                if (character.frame == CharacterFrame.Truck)
                {
                    if (character.mode == CharacterMode.Normal) character.BeginDescend(gameState);
                    return new InputManager.InputData { mx = 0f, mz = 0f, want = 0f };
                }

                var target = character.partySpot.position;
                var current = character.WorldPosition(gameState);
                float dx = target.x - current.x, dz = target.z - current.z;
                float d = Mathf.Sqrt(dx * dx + dz * dz);

                if (d < 0.4f)
                {
                    float dyaw = Mathf.DeltaAngle(character.yaw, character.partySpot.yaw);
                    character.yaw += dyaw * Mathf.Min(1f, dt * 4f);
                    return new InputManager.InputData { mx = 0f, mz = 0f, want = 0f };
                }

                return new InputManager.InputData { mx = dx / d, mz = dz / d, want = Mathf.Min(1f, d / 1.5f) };
            }

            // Escort mode
            if (character.escortOffset != Vector3.zero && gameState.escort != null)
            {
                // Similar to party but follows truck
                return new InputManager.InputData { mx = 0f, mz = 0f, want = 0f };
            }

            // Follow player
            bool followPlayer = character.def.kid && gameState.player.frame == CharacterFrame.World &&
                               gameState.player.mode != CharacterMode.Drive && gameState.player.mode != CharacterMode.Pose &&
                               !gameState.dancing && gameState.phase == GamePhase.Play;

            if (followPlayer)
            {
                // Follow player logic
                return new InputManager.InputData { mx = 0f, mz = 0f, want = 0f };
            }

            // Return to truck
            if (character.frame == CharacterFrame.Truck)
            {
                // Move to spot
                return new InputManager.InputData { mx = 0f, mz = 0f, want = 0f };
            }

            if (character.CanClimbTruck(gameState))
            {
                character.BeginClimbTruck(gameState, character.pos);
                return new InputManager.InputData { mx = 0f, mz = 0f, want = 0f };
            }

            // Wander near truck
            return new InputManager.InputData { mx = 0f, mz = 0f, want = 0f };
        }

        private void UpdateCamera(float dt)
        {
            // Camera logic ported from main.js
        }

        private void CheckPartyStops()
        {
            if (gameState.phase != GamePhase.Play || gameState.dancing || gameState.player.mode != CharacterMode.Drive)
                return;

            if (Mathf.Abs(truck.velocity) > 0.8f) return;

            var stop = city.stops.FirstOrDefault(s => !s.inicio && !gameState.visitedStops.Contains(s) &&
                Vector2.Distance(new Vector2(s.position.x, s.position.z), new Vector2(truck.x, truck.z)) < 25f);

            if (stop != null)
            {
                gameState.visitedStops.Add(stop);
                gameState.dancing = true;
                gameState.currentStopState = stop;
                gameState.stopStateTimer = 0f;
                gameState.stopStateMode = "dance";

                GatherAtStop(stop);
                danceController.StartDance(32);
                musicController.PlaySFX("cheer");
                uiManager.ShowHint($"💃 Todo mundo na pista da {stop.nome}!", 2.5f);
            }
        }

        private void GatherAtStop(CityStop stop)
        {
            gameState.partyStop = stop;

            if (gameState.player.mode == CharacterMode.Drive)
                gameState.player.ExitDrive(gameState);
            if (gameState.player.mode == CharacterMode.Pose)
                gameState.player.StandUp(gameState);

            var player = gameState.player;
            var others = allCharacters.Where(c => c != player).ToList();

            var allChars = new List<CharacterController> { player };
            allChars.AddRange(others);

            for (int i = 0; i < allChars.Count; i++)
            {
                var character = allChars[i];
                var spot = stop.spots[i % stop.spots.Length];
                character.partySpot = new CharacterController.CityStop
                {
                    position = spot.position,
                    yaw = spot.yaw
                };
                character.boost = 1.3f;

                var current = character.WorldPosition(gameState);
                float far = Vector2.Distance(new Vector2(current.x, current.z), new Vector2(spot.position.x, spot.position.z)) > 28f;

                if (character == player || far || character.mode != CharacterMode.Normal)
                {
                    character.mode = CharacterMode.Normal;
                    character.trick = null;
                    character.frame = CharacterFrame.World;
                    character.pos = new Vector3(spot.position.x, city.FloorY(spot.position.x, spot.position.z, 60f).height, spot.position.z);
                    character.vel = Vector3.zero;
                    character.grounded = true;
                    character.yaw = spot.yaw;
                }
                else if (character.frame == CharacterFrame.Truck)
                {
                    character.BeginDescend(gameState);
                }
            }
        }

        public void EndParty()
        {
            gameState.partyStop = null;
            foreach (var character in allCharacters)
            {
                character.partySpot = null;
                character.boost = 1f;
            }
            StartEscort();
        }

        private void StartEscort()
        {
            var pool = allCharacters.Where(c => c != gameState.player && !(c.def.kid && gameState.player.def.kid)).ToList();
            var pick = new List<CharacterController>();

            for (int k = 0; k < pool.Count && pick.Count < 5; k++)
            {
                pick.Add(pool[(gameState.escortRound * 5 + k) % pool.Count]);
            }
            gameState.escortRound++;

            float W = truck.halfWidth;
            var offsets = new Vector2[]
            {
                new Vector2(W + 2.3f, 6f),
                new Vector2(W + 2.3f, -1f),
                new Vector2(W + 2.3f, -8f),
                new Vector2(W + 5.4f, 2.5f),
                new Vector2(W + 5.4f, -4.5f)
            };

            for (int i = 0; i < pick.Count; i++)
            {
                pick[i].escortOffset = new Vector3(offsets[i].x, 0f, offsets[i].y);
                pick[i].boost = 1.55f;
                pick[i].ai.trickTimer = 1f + Random.value * 2f;
            }

            gameState.escort = new GameState.EscortData { list = pick, until = Time.time + 55f };
            uiManager.ShowHint("🤸 Saída da carreta! Olha a turma fazendo acrobacias na rua!", 3.5f);
        }

        private void LoadSaveData()
        {
            string json = PlayerPrefs.GetString("CarretaJoySave", "{}");
            saveData = JsonUtility.FromJson<SaveData>(json);
            if (saveData == null) saveData = new SaveData();

            // Apply defaults
            if (saveData.best == null) saveData.best = new Dictionary<string, int>();
            if (saveData.stars == null) saveData.stars = new Dictionary<string, int>();
            if (saveData.costumes == null) saveData.costumes = new Dictionary<string, string>
            {
                { "arthur", "normal" }, { "afonso", "normal" }
            };
            if (saveData.settings == null) saveData.settings = new SaveSettings
            {
                music = 0.8f, sfx = 0.9f, quality = "media",
                bright = 1.2f, contrast = 1.05f, satur = 1.1f,
                fps = 30, vibrar = true
            };
        }

        public void SaveGame()
        {
            saveData.city = currentCityIndex;
            saveData.night = nightMode;
            saveData.char = currentCharacter;
            saveData.truck = currentTruckVariant;
            saveData.minutes = gameDurationMinutes;

            string json = JsonUtility.ToJson(saveData);
            PlayerPrefs.SetString("CarretaJoySave", json);
            PlayerPrefs.Save();
        }

        public void QuitGame()
        {
            SaveGame();
            Application.Quit();
        }
    }

    [System.Serializable]
    public class SaveData
    {
        public Dictionary<string, int> best;
        public Dictionary<string, int> stars;
        public int city = 0;
        public bool night = false;
        public string char = "arthur";
        public string truck = "arcoiris";
        public int minutes = 10;
        public Dictionary<string, string> costumes;
        public SaveSettings settings;
    }

    [System.Serializable]
    public class SaveSettings
    {
        public float music = 0.8f;
        public float sfx = 0.9f;
        public string quality = "media";
        public float bright = 1.2f;
        public float contrast = 1.05f;
        public float satur = 1.1f;
        public int fps = 30;
        public bool vibrar = true;
    }
}