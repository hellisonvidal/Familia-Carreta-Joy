using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace FamiliaCarretaJoy
{
    /// <summary>
    /// UI Manager handling all game UI including menus, HUD, and dialogs.
    /// Ported from the HTML/CSS UI in index.html and main.js.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("Canvas")]
        public Canvas mainCanvas;

        [Header("Screens")]
        public GameObject loadingScreen;
        public GameObject homeScreen;
        public GameObject menuScreen;
        public GameObject configScreen;
        public GameObject pauseScreen;
        public GameObject pickerScreen;
        public GameObject resultsScreen;
        public GameObject danceScreen;
        public GameObject rotateScreen;

        [Header("HUD")]
        public GameObject hud;
        public TextMeshProUGUI scoreText;
        public TextMeshProUGUI multiplierText;
        public GameObject multiplierObj;
        public Slider comboMeter;
        public TextMeshProUGUI characterNameText;
        public TextMeshProUGUI timerText;
        public TextMeshProUGUI cityNameText;
        public RawImage minimapImage;
        public Button pauseButton;

        [Header("Controls")]
        public GameObject controlsPanel;
        public RectTransform joystickZone;
        public RectTransform joystickBase;
        public RectTransform joystickKnob;
        public Button runButton;
        public Button ctxButton;
        public Button driveButton;
        public Button hornButton;
        public Button partyButton;
        public Button comboButton;
        public Button charactersButton;
        public GameObject trickPad;
        public GameObject drivePad;
        public Button mortalButton;
        public Button flipButton;
        public Button estrelaButton;
        public Button piruetaButton;
        public Button dashButton;
        public Button slideButton;
        public Button jumpButton;
        public Button gasPedal;
        public Button brakePedal;

        [Header("Toasts & Hints")]
        public Transform toastsContainer;
        public GameObject toastPrefab;
        public TextMeshProUGUI hintText;

        [Header("Dance")]
        public GameObject dancePanel;
        public TextMeshProUGUI danceTitle;
        public TextMeshProUGUI danceInfo;
        public Transform danceLanes;
        public Transform dancePads;

        [Header("Menus")]
        public Transform cityList;
        public Transform characterList;
        public Transform costumeAChips;
        public Transform costumeBChips;
        public Transform truckList;
        public Transform timeList;
        public Button dayButton;
        public Button nightButton;
        public TextMeshProUGUI summaryText;
        public Button playButton;
        public Button menuBackButton;

        [Header("Config")]
        public Slider musicVolume;
        public Slider sfxVolume;
        public Slider brightness;
        public Slider contrast;
        public Slider saturation;
        public TMP_Dropdown qualityDropdown;
        public TMP_Dropdown fpsDropdown;
        public Toggle vibrateToggle;
        public TextMeshProUGUI versionText;
        public TextMeshProUGUI tempInfoText;
        public Button updateButton;
        public Button updateGoButton;
        public Button updateWebButton;
        public TextMeshProUGUI updateStatusText;
        public GameObject updateLinks;

        [Header("Picker")]
        public Transform pickerList;
        public Transform pickerCostumeA;
        public Transform pickerCostumeB;
        public Button pickerOkButton;

        [Header("Pause")]
        public Button resumeButton;
        public Button pauseConfigButton;
        public Button restartButton;
        public Button pauseMenuButton;
        public Button pauseExitButton;

        [Header("Results")]
        public TextMeshProUGUI resultTitle;
        public TextMeshProUGUI resultStars;
        public TextMeshProUGUI resultScore;
        public TextMeshProUGUI resultInfo;
        public Button nextCityButton;
        public Button againButton;
        public Button resultMenuButton;

        // Prefabs
        public GameObject cityCardPrefab;
        public GameObject characterCardPrefab;
        public GameObject truckCardPrefab;
        public GameObject chipPrefab;
        public GameObject tabButtonPrefab;
        public GameObject dancePadPrefab;

        private Coroutine hintCoroutine;
        private List<GameObject> activeToasts = new List<GameObject>();

        private void Awake()
        {
            // Setup button listeners
            SetupButtonListeners();

            // Hide all screens initially
            HideAllScreens();
            ShowLoadingScreen();
        }

        private void SetupButtonListeners()
        {
            // Home screen
            if (playButton != null) playButton.onClick.AddListener(OnHomePlay);
            // Add more button listeners as needed
        }

        public void ShowLoadingScreen()
        {
            HideAllScreens();
            if (loadingScreen != null) loadingScreen.SetActive(true);
        }

        public void ShowHomeScreen()
        {
            HideAllScreens();
            if (homeScreen != null) homeScreen.SetActive(true);
            BuildHomeScreen();
        }

        public void ShowMenuScreen()
        {
            HideAllScreens();
            if (menuScreen != null) menuScreen.SetActive(true);
            BuildMenuScreen();
        }

        public void ShowConfigScreen()
        {
            if (configScreen != null) configScreen.SetActive(true);
            BuildConfigScreen();
        }

        public void ShowPauseScreen()
        {
            if (pauseScreen != null) pauseScreen.SetActive(true);
        }

        public void ShowPickerScreen()
        {
            if (pickerScreen != null) pickerScreen.SetActive(true);
            BuildPickerScreen();
        }

        public void ShowResultsScreen()
        {
            HideAllScreens();
            if (resultsScreen != null) resultsScreen.SetActive(true);
            BuildResultsScreen();
        }

        public void ShowDanceScreen()
        {
            if (danceScreen != null) danceScreen.SetActive(true);
        }

        public void HideDanceScreen()
        {
            if (danceScreen != null) danceScreen.SetActive(false);
        }

        public void ShowHUD()
        {
            if (hud != null) hud.SetActive(true);
        }

        public void HideHUD()
        {
            if (hud != null) hud.SetActive(false);
        }

        public void ShowRotateScreen(bool show)
        {
            if (rotateScreen != null) rotateScreen.SetActive(show);
        }

        private void HideAllScreens()
        {
            var screens = new[] { loadingScreen, homeScreen, menuScreen, configScreen, pauseScreen, pickerScreen, resultsScreen, danceScreen, rotateScreen };
            foreach (var screen in screens)
            {
                if (screen != null) screen.SetActive(false);
            }
        }

        #region Screen Building

        private void BuildHomeScreen()
        {
            if (summaryText != null && GameManager.Instance != null)
            {
                var gm = GameManager.Instance;
                var city = GameConstants.Cities[gm.currentCityIndex];
                var character = GameConstants.GetCharacterDefinition(gm.currentCharacter);
                var truck = GameConstants.GetTruckVariant(gm.currentTruckVariant);
                var timeStr = $"{gm.gameDurationMinutes} min";

                summaryText.text = $"🏙️ <b>{city.nome}</b> {(gm.nightMode ? "🌙" : "☀️")} · 🧒 <b>{character.nome}</b> · 🚚 <b>{truck.nome}</b> · ⏱ <b>{timeStr}</b>";
            }
        }

        private void BuildMenuScreen()
        {
            // Cities tab
            BuildCityList();

            // Characters tab
            BuildCharacterList();

            // Costumes
            BuildCostumeChips(costumeAChips, "arthur");
            BuildCostumeChips(costumeBChips, "afonso");

            // Trucks
            BuildTruckList();

            // Time
            BuildTimeList();

            // Day/Night buttons
            if (dayButton != null) dayButton.interactable = !GameManager.Instance.nightMode;
            if (nightButton != null) nightButton.interactable = GameManager.Instance.nightMode;

            // Summary
            UpdateMenuSummary();
        }

        private void BuildCityList()
        {
            if (cityList == null || cityCardPrefab == null) return;

            foreach (Transform child in cityList) Destroy(child.gameObject);

            for (int i = 0; i < GameConstants.Cities.Length; i++)
            {
                var city = GameConstants.Cities[i];
                var card = Instantiate(cityCardPrefab, cityList);
                var selected = i == GameManager.Instance.currentCityIndex;

                // Setup card UI
                var button = card.GetComponent<Button>();
                if (button != null)
                {
                    int idx = i;
                    button.onClick.AddListener(() => OnCitySelected(idx));
                }

                // Update card visuals (name, description, stars, best score)
                // This would be implemented with actual UI components
            }
        }

        private void BuildCharacterList()
        {
            if (characterList == null || characterCardPrefab == null) return;

            foreach (Transform child in characterList) Destroy(child.gameObject);

            var characters = GameConstants.GetCharacterDefinitions();
            foreach (var charDef in characters)
            {
                var card = Instantiate(characterCardPrefab, characterList);
                var selected = charDef.id == GameManager.Instance.currentCharacter;

                var button = card.GetComponent<Button>();
                if (button != null)
                {
                    string id = charDef.id;
                    button.onClick.AddListener(() => OnCharacterSelected(id));
                }
            }
        }

        private void BuildCostumeChips(Transform container, string characterId)
        {
            if (container == null || chipPrefab == null) return;

            foreach (Transform child in container) Destroy(child.gameObject);

            foreach (var costumeId in GameConstants.KidCostumes)
            {
                var chip = Instantiate(chipPrefab, container);
                var button = chip.GetComponent<Button>();
                var label = chip.GetComponentInChildren<TextMeshProUGUI>();

                string displayName = costumeId == "normal" ? "Roupa dele" : GameConstants.GetCostumeName(costumeId);
                if (label != null) label.text = displayName;

                bool selected = GameManager.Instance.saveData.costumes[characterId] == costumeId;
                // Apply selected visual style

                if (button != null)
                {
                    string cid = characterId;
                    string cosid = costumeId;
                    button.onClick.AddListener(() => OnCostumeSelected(cid, cosid));
                }
            }
        }

        private void BuildTruckList()
        {
            if (truckList == null || truckCardPrefab == null) return;

            foreach (Transform child in truckList) Destroy(child.gameObject);

            foreach (var variant in GameConstants.TruckVariants)
            {
                var card = Instantiate(truckCardPrefab, truckList);
                bool selected = variant.id == GameManager.Instance.currentTruckVariant;

                var button = card.GetComponent<Button>();
                if (button != null)
                {
                    string id = variant.id;
                    button.onClick.AddListener(() => OnTruckSelected(id));
                }
            }
        }

        private void BuildTimeList()
        {
            if (timeList == null || chipPrefab == null) return;

            foreach (Transform child in timeList) Destroy(child.gameObject);

            foreach (int minutes in GameConstants.Durations)
            {
                var chip = Instantiate(chipPrefab, timeList);
                var button = chip.GetComponent<Button>();
                var label = chip.GetComponentInChildren<TextMeshProUGUI>();

                if (label != null) label.text = $"{minutes} min";
                bool selected = minutes == GameManager.Instance.gameDurationMinutes;

                if (button != null)
                {
                    int mins = minutes;
                    button.onClick.AddListener(() => OnTimeSelected(mins));
                }
            }
        }

        private void UpdateMenuSummary()
        {
            if (summaryText != null)
            {
                var gm = GameManager.Instance;
                var city = GameConstants.Cities[gm.currentCityIndex];
                var character = GameConstants.GetCharacterDefinition(gm.currentCharacter);
                var truck = GameConstants.GetTruckVariant(gm.currentTruckVariant);

                summaryText.text = $"🏙️ <b>{city.nome}</b> {(gm.nightMode ? "🌙" : "☀️")} · 🧒 <b>{character.nome}</b> · 🚚 <b>{truck.nome}</b> · ⏱ <b>{gm.gameDurationMinutes} min</b>";
            }
        }

        private void BuildConfigScreen()
        {
            if (musicVolume != null) musicVolume.value = GameManager.Instance.saveData.settings.music;
            if (sfxVolume != null) sfxVolume.value = GameManager.Instance.saveData.settings.sfx;
            if (brightness != null) brightness.value = GameManager.Instance.saveData.settings.bright;
            if (contrast != null) contrast.value = GameManager.Instance.saveData.settings.contrast;
            if (saturation != null) saturation.value = GameManager.Instance.saveData.settings.satur;
            if (qualityDropdown != null) qualityDropdown.value = GetQualityIndex(GameManager.Instance.saveData.settings.quality);
            if (fpsDropdown != null) fpsDropdown.value = GameManager.Instance.saveData.settings.fps == 60 ? 1 : 0;
            if (vibrateToggle != null) vibrateToggle.isOn = GameManager.Instance.saveData.settings.vibrar;
            if (versionText != null) versionText.text = "Versão instalada: " + Application.version;
        }

        private void BuildPickerScreen()
        {
            if (pickerList == null || characterCardPrefab == null) return;

            foreach (Transform child in pickerList) Destroy(child.gameObject);

            var gm = GameManager.Instance;
            foreach (var character in gm.allCharacters)
            {
                var card = Instantiate(characterCardPrefab, pickerList);
                bool selected = character == gm.player;

                var button = card.GetComponent<Button>();
                if (button != null)
                {
                    var c = character;
                    button.onClick.AddListener(() => OnPickerCharacterSelected(c));
                }
            }

            BuildCostumeChips(pickerCostumeA, "arthur");
            BuildCostumeChips(pickerCostumeB, "afonso");
        }

        private void BuildResultsScreen()
        {
            var gm = GameManager.Instance;
            if (resultTitle != null) resultTitle.text = gm.gameState.finished ? "Chegamos!" : "Tempo esgotado!";
            if (resultScore != null) resultScore.text = $"Pontuação: {gm.gameState.score:N0}";

            // Calculate stars based on score
            int stars = CalculateStars(gm.gameState.score, gm.cityDef);
            if (resultStars != null) resultStars.text = new string('★', stars) + new string('☆', 3 - stars);

            if (resultInfo != null)
            {
                var stats = gm.gameState.stats;
                resultInfo.text = $"Manobras: {stats["tricks"]} · Notas: {stats["notes"]} · Estrelas: {stats["gold"]} · Tombos: {stats["tombos"]} · Melhor combo: {stats["best"]}";
            }
        }

        #endregion

        #region Event Handlers

        public void OnHomePlay()
        {
            GameManager.Instance.StartNewGame();
        }

        public void OnCitySelected(int index)
        {
            GameManager.Instance.currentCityIndex = index;
            GameManager.Instance.nightMode = false;
            BuildMenuScreen();
        }

        public void OnCharacterSelected(string id)
        {
            GameManager.Instance.currentCharacter = id;
            BuildMenuScreen();
        }

        public void OnCostumeSelected(string characterId, string costumeId)
        {
            GameManager.Instance.saveData.costumes[characterId] = costumeId;
            BuildCostumeChips(characterId == "arthur" ? costumeAChips : costumeBChips, characterId);
        }

        public void OnTruckSelected(string id)
        {
            GameManager.Instance.currentTruckVariant = id;
            BuildMenuScreen();
        }

        public void OnTimeSelected(int minutes)
        {
            GameManager.Instance.gameDurationMinutes = minutes;
            BuildMenuScreen();
        }

        public void OnDayNightSelected(bool night)
        {
            GameManager.Instance.nightMode = night;
            BuildMenuScreen();
        }

        public void OnPickerCharacterSelected(CharacterController character)
        {
            GameManager.Instance.SwitchCharacter(character);
        }

        #endregion

        #region HUD Updates

        public void UpdateHUD()
        {
            if (GameManager.Instance == null) return;

            var gs = GameManager.Instance.gameState;
            var player = gs.player;

            if (scoreText != null) scoreText.text = gs.score.ToString("N0");

            int mult = gs.GetMultiplier();
            if (multiplierText != null)
            {
                multiplierText.text = $"x{mult}";
                if (multiplierObj != null) multiplierObj.SetActive(mult >= 2);
            }

            if (comboMeter != null)
            {
                comboMeter.value = Mathf.Clamp01(gs.comboMeter / GameConstants.ComboFullThreshold);
            }

            if (characterNameText != null && player != null)
                characterNameText.text = player.def.nome;

            if (timerText != null)
            {
                int seconds = Mathf.Max(0, Mathf.CeilToInt(gs.timeLeft));
                timerText.text = $"⏱ {seconds / 60}:{seconds % 60:D2}";
                // Add low time warning style
            }

            if (cityNameText != null)
            {
                var city = GameConstants.Cities[GameManager.Instance.currentCityIndex];
                var music = GameConstants.MusicStyles[city.musica];
                cityNameText.text = $"{city.nome} · 🎵 {music.nome}";
            }
        }

        public void UpdateMinimap(Texture2D texture)
        {
            if (minimapImage != null) minimapImage.texture = texture;
        }

        #endregion

        #region Toasts & Hints

        public void ShowToast(string text, ToastType type = ToastType.Normal)
        {
            if (toastPrefab == null || toastsContainer == null) return;

            // Limit toasts
            while (activeToasts.Count > 2)
            {
                var old = activeToasts[0];
                activeToasts.RemoveAt(0);
                Destroy(old);
            }

            var toast = Instantiate(toastPrefab, toastsContainer);
            var label = toast.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = text;

            // Apply type styling
            var image = toast.GetComponent<Image>();
            if (image != null)
            {
                switch (type)
                {
                    case ToastType.Small: image.color = new Color(1f, 1f, 1f, 0.7f); break;
                    case ToastType.Gold: image.color = new Color(1f, 0.8f, 0f, 0.9f); break;
                    case ToastType.Bad: image.color = new Color(1f, 0.3f, 0.3f, 0.9f); break;
                    default: image.color = new Color(0f, 0f, 0f, 0.8f); break;
                }
            }

            activeToasts.Add(toast);
            StartCoroutine(RemoveToastAfterDelay(toast, 1.3f));
        }

        private IEnumerator RemoveToastAfterDelay(GameObject toast, float delay)
        {
            yield return new WaitForSeconds(delay);
            activeToasts.Remove(toast);
            Destroy(toast);
        }

        public void ShowHint(string text, float duration = 2.5f)
        {
            if (hintText == null) return;

            if (hintCoroutine != null) StopCoroutine(hintCoroutine);
            hintCoroutine = StartCoroutine(ShowHintRoutine(text, duration));
        }

        private IEnumerator ShowHintRoutine(string text, float duration)
        {
            hintText.text = text;
            hintText.gameObject.SetActive(true);
            yield return new WaitForSeconds(duration);
            hintText.gameObject.SetActive(false);
        }

        #endregion

        #region Dance UI

        public void UpdateDanceUI(int beat, float energy, DanceJudgeQuality lastJudge, int streak)
        {
            if (danceInfo != null)
            {
                danceInfo.text = $"Beat: {beat} · Energy: {energy:P0}";
            }
            // Update dance lanes/pads
        }

        public void CreateDancePads()
        {
            if (dancePads == null || dancePadPrefab == null) return;

            foreach (Transform child in dancePads) Destroy(child.gameObject);

            foreach (var pad in GameConstants.DancePads)
            {
                var padObj = Instantiate(dancePadPrefab, dancePads);
                var button = padObj.GetComponent<Button>();
                var image = padObj.GetComponent<Image>();
                var label = padObj.GetComponentInChildren<TextMeshProUGUI>();

                if (image != null) image.color = HexToColor(pad.cor);
                if (label != null) label.text = $"{pad.label}\n<small>{pad.nome}</small>";

                if (button != null)
                {
                    int index = pad.index;
                    button.onClick.AddListener(() => OnDancePadPressed(index));
                }
            }
        }

        public void OnDancePadPressed(int index)
        {
            GameManager.Instance?.danceController?.OnPadPress(index);
        }

        #endregion

        #region Helpers

        private int GetQualityIndex(string quality)
        {
            switch (quality)
            {
                case "leve": return 0;
                case "media": return 1;
                case "alta": return 2;
                default: return 1;
            }
        }

        private int CalculateStars(int score, CityDefinition city)
        {
            // Simple star calculation based on score thresholds
            if (score > 5000) return 3;
            if (score > 2000) return 2;
            if (score > 500) return 1;
            return 0;
        }

        private Color HexToColor(string hex)
        {
            hex = hex.Replace("#", "");
            return new Color(
                int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
                int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
                int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f
            );
        }

        #endregion
    }
}