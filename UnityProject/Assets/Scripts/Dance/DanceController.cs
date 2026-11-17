using UnityEngine;
using System.Collections.Generic;

namespace FamiliaCarretaJoy
{
    /// <summary>
    /// Dance rhythm game controller.
    /// Ported from the Dance class in dance.js.
    /// </summary>
    public class DanceController : MonoBehaviour
    {
        [Header("Settings")]
        public float noteSpeed = 10f;
        public float judgeWindowPerfect = 0.08f;
        public float judgeWindowGood = 0.15f;
        public float judgeWindowOk = 0.25f;

        [Header("References")]
        public MusicController musicController;
        public UIManager uiManager;

        // Dance state
        private bool active = false;
        private int totalBeats = 0;
        private int currentBeat = 0;
        private float beatTime = 0f;
        private List<DanceNote> notes = new List<DanceNote>();
        private int streak = 0;
        private int score = 0;

        // Dance pads (4 directions)
        public static readonly DancePad[] DancePads = new DancePad[]
        {
            new DancePad { index = 0, label = "←", nome = "ESQUERDA", cor = "#ff3d7f", direction = Vector2.left },
            new DancePad { index = 1, label = "↑", nome = "CIMA", cor = "#ffc400", direction = Vector2.up },
            new DancePad { index = 2, label = "↓", nome = "BAIXO", cor = "#00c853", direction = Vector2.down },
            new DancePad { index = 3, label = "→", nome = "DIREITA", cor = "#2979ff", direction = Vector2.right }
        };

        public void Initialize(MusicController music)
        {
            musicController = music;
        }

        public void StartDance(int beats)
        {
            active = true;
            totalBeats = beats;
            currentBeat = 0;
            beatTime = 0f;
            streak = 0;
            score = 0;
            notes.Clear();

            // Generate dance pattern
            GenerateDancePattern();

            // Show dance UI
            uiManager?.ShowDanceScreen();
            uiManager?.CreateDancePads();
        }

        public void Cancel()
        {
            active = false;
            notes.Clear();
            uiManager?.HideDanceScreen();
        }

        private void GenerateDancePattern()
        {
            // Generate a sequence of notes for the dance
            // Each beat has a 50% chance of having a note
            for (int i = 0; i < totalBeats; i++)
            {
                if (Random.value < 0.5f)
                {
                    int pad = Random.Range(0, 4);
                    notes.Add(new DanceNote
                    {
                        beat = i,
                        pad = pad,
                        spawned = false,
                        judged = false
                    });
                }
            }
        }

        public void Update(float dt, GameState gameState)
        {
            if (!active) return;

            beatTime += dt;
            float beatDuration = 60f / musicController.bpm;

            // Spawn notes
            foreach (var note in notes)
            {
                if (!note.spawned && currentBeat >= note.beat - 2)
                {
                    note.spawned = true;
                    // Show note on UI lane
                }
            }

            // Update note positions
            foreach (var note in notes)
            {
                if (note.spawned && !note.judged)
                {
                    float noteTime = (note.beat - currentBeat) * beatDuration - beatTime;
                    // Update visual position based on noteTime
                }
            }

            // Auto-judge missed notes
            foreach (var note in notes)
            {
                if (!note.judged && currentBeat > note.beat + 1)
                {
                    JudgeNote(note, DanceJudgeQuality.Miss);
                }
            }

            // Check if dance is complete
            if (currentBeat >= totalBeats)
            {
                EndDance();
            }
        }

        public void OnPadPress(int padIndex)
        {
            if (!active) return;

            // Find the closest unjudged note for this pad
            DanceNote closestNote = null;
            float closestTime = float.MaxValue;

            foreach (var note in notes)
            {
                if (note.pad == padIndex && !note.judged)
                {
                    float noteTime = (note.beat - currentBeat) * (60f / musicController.bpm) - beatTime;
                    float absTime = Mathf.Abs(noteTime);
                    if (absTime < closestTime)
                    {
                        closestTime = absTime;
                        closestNote = note;
                    }
                }
            }

            if (closestNote != null)
            {
                DanceJudgeQuality quality;
                if (closestTime < judgeWindowPerfect) quality = DanceJudgeQuality.Perfect;
                else if (closestTime < judgeWindowGood) quality = DanceJudgeQuality.Good;
                else if (closestTime < judgeWindowOk) quality = DanceJudgeQuality.Ok;
                else quality = DanceJudgeQuality.Miss;

                JudgeNote(closestNote, quality);
            }
            else
            {
                // Pressed with no note - miss
                musicController.PlaySFX("miss");
            }
        }

        private void JudgeNote(DanceNote note, DanceJudgeQuality quality)
        {
            note.judged = true;

            switch (quality)
            {
                case DanceJudgeQuality.Perfect:
                    streak++;
                    score += 50;
                    musicController.PlaySFX("perfect");
                    break;
                case DanceJudgeQuality.Good:
                    streak++;
                    score += 30;
                    musicController.PlaySFX("good");
                    break;
                case DanceJudgeQuality.Ok:
                    streak = 0;
                    score += 15;
                    musicController.PlaySFX("click");
                    break;
                case DanceJudgeQuality.Miss:
                    streak = 0;
                    musicController.PlaySFX("miss");
                    break;
            }

            // Special: every 8 notes, trigger mortal together
            if (streak > 0 && streak % 8 == 0)
            {
                GameManager.Instance?.gameState?.OnDanceJudge(quality, "mortal", streak);
            }
            else
            {
                GameManager.Instance?.gameState?.OnDanceJudge(quality, GetMoveForPad(note.pad), streak);
            }

            // Update UI
            uiManager?.UpdateDanceUI(currentBeat, musicController.GetEnergy(), quality, streak);
        }

        private string GetMoveForPad(int pad)
        {
            switch (pad)
            {
                case 0: return "esq";
                case 1: return "cima";
                case 2: return "baixo";
                case 3: return "dir";
                default: return "esq";
            }
        }

        private void EndDance()
        {
            active = false;
            uiManager?.HideDanceScreen();

            // Award bonus for combo dance
            if (GameManager.Instance?.gameState?.comboDanceActive == true)
            {
                GameManager.Instance.gameState.comboDanceActive = false;
                GameManager.Instance.gameState.dancing = false;

                foreach (var character in GameManager.Instance.allCharacters)
                {
                    character.comboJoin = false;
                }
            }
        }

        public bool IsActive => active;
        public int CurrentScore => score;
        public int CurrentStreak => streak;
    }

    [System.Serializable]
    public class DanceNote
    {
        public int beat;
        public int pad;
        public bool spawned;
        public bool judged;
    }

    [System.Serializable]
    public class DancePad
    {
        public int index;
        public string label;
        public string nome;
        public string cor;
        public Vector2 direction;
    }
}