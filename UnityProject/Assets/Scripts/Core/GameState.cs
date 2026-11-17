using UnityEngine;
using System.Collections.Generic;

namespace FamiliaCarretaJoy
{
    /// <summary>
    /// Main game state class, ported from the G object in main.js.
    /// </summary>
    public class GameState : MonoBehaviour
    {
        public static GameState Instance { get; private set; }

        [Header("Phase")]
        public GamePhase phase = GamePhase.Loading;
        public float phaseTime = 0f;

        [Header("City")]
        public CityDefinition cityDef;
        public bool night;
        public int cityIndex = 0;

        [Header("Truck")]
        public TruckVariant truckVariant;
        public TruckController truck;

        [Header("Characters")]
        public List<CharacterController> actors = new List<CharacterController>();
        public CharacterController player;

        [Header("Scoring")]
        public int score = 0;
        public int combo = 0;
        public float comboTimer = 0f;
        public float comboMeter = 0f;
        public bool comboDanceActive = false;
        public int comboDancePoints = 0;
        public int beat = 0;

        [Header("Time")]
        public float timeLeft = 600f;
        public float totalTime = 600f;
        public int minutes = 10;

        [Header("Stops")]
        public HashSet<CityStop> visitedStops = new HashSet<CityStop>();
        public CityStop currentStopState = null;
        public float stopStateTimer = 0f;
        public string stopStateMode = "";

        [Header("Party/Escort")]
        public CityStop partyStop = null;
        public EscortData escort = null;
        public bool startedEscort = false;
        public int escortRound = 0;

        [Header("State")]
        public bool paused = false;
        public bool freeze = false;
        public bool dancing = false;
        public bool finished = false;
        public Dictionary<string, object> stats = new Dictionary<string, object>();

        [Header("Visual")]
        public float screenShake = 0f;
        public Vector3 camPos;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void ResetForNewGame()
        {
            phase = GamePhase.Intro;
            phaseTime = 0f;
            score = 0;
            combo = 0;
            comboTimer = 0f;
            comboMeter = 0f;
            comboDanceActive = false;
            comboDancePoints = 0;
            beat = 0;
            timeLeft = minutes * 60f;
            totalTime = timeLeft;
            visitedStops.Clear();
            currentStopState = null;
            stopStateTimer = 0f;
            stopStateMode = "";
            partyStop = null;
            escort = null;
            startedEscort = false;
            escortRound = 0;
            paused = false;
            freeze = false;
            dancing = false;
            finished = false;
            stats.Clear();
            screenShake = 0f;

            stats["tricks"] = 0;
            stats["notes"] = 0;
            stats["gold"] = 0;
            stats["tombos"] = 0;
            stats["best"] = 0;
            stats["dance"] = new List<string>();
            stats["parkour"] = 0;
            stats["km"] = 0f;
        }

        public int GetMultiplier()
        {
            return Mathf.Min(8, 1 + Mathf.FloorToInt(combo / 2f));
        }

        public void AddScore(int points, CharacterController actor = null)
        {
            bool isPlayer = actor == player;
            float mult = GetMultiplier();
            float actorMult = isPlayer ? 1f : 0.4f;
            int finalPoints = Mathf.RoundToInt(points * mult * actorMult);
            score += finalPoints;
        }

        public void BumpCombo()
        {
            combo++;
            comboTimer = 4.5f;
            stats["best"] = Mathf.Max((int)stats["best"], combo);
        }

        public void FillCombo(int points)
        {
            if (comboDanceActive) return;

            bool wasFull = comboMeter >= GameConstants.ComboFullThreshold;
            comboMeter = Mathf.Min(GameConstants.ComboFullThreshold, comboMeter + points);

            if (!wasFull && comboMeter >= GameConstants.ComboFullThreshold)
            {
                // Trigger combo full event
                EventManager.TriggerComboFull();
            }
        }

        public void StartComboDance()
        {
            if (comboMeter < GameConstants.ComboFullThreshold || dancing || phase != GamePhase.Play ||
                player.mode != CharacterMode.Normal || !player.grounded) return;

            dancing = true;
            comboDanceActive = true;
            comboMeter = 0f;
            comboDancePoints = 0f;

            // Find nearby actors to join
            foreach (var actor in actors)
            {
                if (actor != player && actor.mode == CharacterMode.Normal &&
                    Vector3.Distance(actor.transform.position, player.transform.position) < 14f)
                {
                    actor.comboJoin = true;
                }
            }

            EventManager.TriggerComboDanceStart();
        }

        public void OnDanceJudge(DanceJudgeQuality quality, string move, int streak)
        {
            if (quality == DanceJudgeQuality.Miss)
            {
                EventManager.TriggerSFX("miss");
                return;
            }

            EventManager.TriggerSFX(quality == DanceJudgeQuality.Perfect ? "perfect" : "good");

            bool special = streak > 0 && streak % 8 == 0;
            foreach (var actor in actors)
            {
                if (actor.mode == CharacterMode.Normal &&
                    (comboDanceActive ? (actor == player || actor.comboJoin) :
                     (actor.frame == CharacterFrame.Truck || actor == player || actor.def.kid)))
                {
                    actor.DoDanceMove(special ? "mortal" : move);
                }
            }

            if (special)
            {
                EventManager.TriggerToast($"SEQUÊNCIA {streak}! MORTAL JUNTOS!", ToastType.Gold);
                EventManager.TriggerSFX("cheer");
            }

            int basePoints = quality == DanceJudgeQuality.Perfect ? 50 : (quality == DanceJudgeQuality.Good ? 30 : 15);

            if (comboDanceActive)
            {
                int mult = GetMultiplier();
                int points = basePoints * 3 * mult;
                score += points;
                comboDancePoints += points;
            }
            else
            {
                score += basePoints;
            }

            EventManager.TriggerHUDUpdate();
        }

        private void Update()
        {
            if (phase == GamePhase.Play && !paused && !freeze)
            {
                timeLeft -= Time.deltaTime;
                if (timeLeft <= 0f)
                {
                    timeLeft = 0f;
                    EndGame();
                }

                if (comboTimer > 0f)
                    comboTimer -= Time.deltaTime;
                else
                    combo = 0;

                phaseTime += Time.deltaTime;
            }
        }

        public void EndGame()
        {
            phase = GamePhase.End;
            finished = true;
            EventManager.TriggerGameEnd();
        }
    }

    public enum GamePhase
    {
        Loading,
        Menu,
        Intro,
        Play,
        Pause,
        End
    }

    public enum CharacterMode
    {
        Normal,
        Drive,
        Pose,
        Stand,
        TruckClimb,
        Descend,
        Ledge
    }

    public enum CharacterFrame
    {
        World,
        Truck
    }

    public enum DanceJudgeQuality
    {
        Perfect,
        Good,
        Ok,
        Miss
    }

    public enum ToastType
    {
        Normal,
        Small,
        Gold,
        Bad
    }

    [System.Serializable]
    public class CityStop
    {
        public Vector3 position;
        public string nome;
        public bool inicio;
        public Vector3[] spots;
        public float cx, cz;
        public Vector2 n, t;
        public bool announced = false;
    }

    [System.Serializable]
    public class EscortData
    {
        public List<CharacterController> list = new List<CharacterController>();
        public float until;
    }

    /// <summary>
    /// Event manager for decoupled communication between systems.
    /// </summary>
    public static class EventManager
    {
        public delegate void GameEvent();
        public delegate void GameEventWithData<T>(T data);

        public static event GameEvent OnComboFull;
        public static event GameEvent OnComboDanceStart;
        public static event GameEvent OnGameEnd;
        public static event GameEvent OnHUDUpdate;
        public static event GameEventWithData<string> OnSFX;
        public static event GameEventWithData<ToastData> OnToast;

        public static void TriggerComboFull() => OnComboFull?.Invoke();
        public static void TriggerComboDanceStart() => OnComboDanceStart?.Invoke();
        public static void TriggerGameEnd() => OnGameEnd?.Invoke();
        public static void TriggerHUDUpdate() => OnHUDUpdate?.Invoke();
        public static void TriggerSFX(string name) => OnSFX?.Invoke(name);
        public static void TriggerToast(string text, ToastType type = ToastType.Normal)
            => OnToast?.Invoke(new ToastData { text = text, type = type });

        [System.Serializable]
        public class ToastData
        {
            public string text;
            public ToastType type;
        }
    }
}