using UnityEngine;
using System.Collections.Generic;

namespace FamiliaCarretaJoy
{
    /// <summary>
    /// Music controller for procedural music synthesis.
    /// Ported from the Music class in music.js.
    /// Generates music in real-time using audio synthesis (no audio files).
    /// </summary>
    public class MusicController : MonoBehaviour
    {
        [Header("Settings")]
        public float musicVolume = 0.8f;
        public float sfxVolume = 0.9f;

        [Header("Audio")]
        public AudioSource musicSource;
        public AudioSource sfxSource;

        // Synthesis
        private AudioClip musicClip;
        private int sampleRate = 44100;
        private float[] musicBuffer;
        private int bufferPosition = 0;
        private int bufferSize = 44100 * 2; // 2 seconds buffer

        // Music state
        private int currentStyle = 0;
        private float bpm = 130f;
        private float beatTime = 0f;
        private int beat = 0;
        private float energy = 0f;
        private bool playing = false;
        private bool muffled = false;

        // Synthesis parameters
        private float phase = 0f;
        private float[] oscillatorPhases = new float[8];
        private float[] oscillatorFreqs = new float[8];
        private float[] oscillatorAmps = new float[8];

        // Drum synthesis
        private float kickPhase = 0f;
        private float snarePhase = 0f;
        private float hihatPhase = 0f;

        // SFX
        private Dictionary<string, AudioClip> sfxClips = new Dictionary<string, AudioClip>();

        public int CurrentBeat => beat;
        public float CurrentEnergy => energy;

        private void Awake()
        {
            // Setup audio sources
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            musicSource.volume = musicVolume;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
            sfxSource.volume = sfxVolume;

            // Initialize synthesis
            InitializeSynthesis();
        }

        private void InitializeSynthesis()
        {
            // Create audio clip for procedural music
            musicClip = AudioClip.Create("ProceduralMusic", bufferSize, 2, sampleRate, true, OnAudioRead, OnAudioSetPosition);
            musicBuffer = new float[bufferSize * 2]; // Stereo

            // Generate SFX clips procedurally
            GenerateSFX();
        }

        private void GenerateSFX()
        {
            // Generate common sound effects procedurally
            sfxClips["jump"] = GenerateTone(440f, 0.1f, 0.5f, AnimationCurve.EaseInOut(0, 1, 0.1f, 0));
            sfxClips["land"] = GenerateTone(220f, 0.15f, 0.3f, AnimationCurve.EaseInOut(0, 1, 0.15f, 0));
            sfxClips["trick"] = GenerateTone(880f, 0.1f, 0.4f, AnimationCurve.EaseInOut(0, 1, 0.1f, 0));
            sfxClips["combo"] = GenerateChord(new float[] { 523f, 659f, 784f }, 0.3f, 0.5f);
            sfxClips["cheer"] = GenerateNoise(0.5f, 0.4f);
            sfxClips["horn"] = GenerateTone(200f, 0.5f, 0.6f, AnimationCurve.Linear(0, 1, 0.5f, 0.3f));
            sfxClips["coin"] = GenerateTone(1000f, 0.05f, 0.3f, AnimationCurve.EaseInOut(0, 1, 0.05f, 0));
            sfxClips["perfect"] = GenerateChord(new float[] { 659f, 880f, 1318f }, 0.2f, 0.6f);
            sfxClips["good"] = GenerateTone(523f, 0.1f, 0.4f, AnimationCurve.EaseInOut(0, 1, 0.1f, 0));
            sfxClips["miss"] = GenerateTone(150f, 0.2f, 0.3f, AnimationCurve.EaseInOut(0, 1, 0.2f, 0));
            sfxClips["star"] = GenerateChord(new float[] { 880f, 1318f, 1760f }, 0.4f, 0.7f);
            sfxClips["click"] = GenerateTone(1000f, 0.03f, 0.2f, AnimationCurve.EaseInOut(0, 1, 0.03f, 0));
            sfxClips["climb"] = GenerateTone(300f, 0.2f, 0.3f, AnimationCurve.EaseInOut(0, 1, 0.2f, 0));
            sfxClips["crash"] = GenerateNoise(0.3f, 0.5f);
        }

        private AudioClip GenerateTone(float frequency, float duration, float volume, AnimationCurve envelope)
        {
            int samples = Mathf.RoundToInt(sampleRate * duration);
            var clip = AudioClip.Create("SFX_Tone", samples, 1, sampleRate, false);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = envelope.Evaluate(t / duration);
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * env * volume;
            }

            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateChord(float[] frequencies, float duration, float volume)
        {
            int samples = Mathf.RoundToInt(sampleRate * duration);
            var clip = AudioClip.Create("SFX_Chord", samples, 1, sampleRate, false);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 5f);
                float sample = 0f;
                foreach (float freq in frequencies)
                {
                    sample += Mathf.Sin(2f * Mathf.PI * freq * t);
                }
                data[i] = (sample / frequencies.Length) * env * volume;
            }

            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateNoise(float duration, float volume)
        {
            int samples = Mathf.RoundToInt(sampleRate * duration);
            var clip = AudioClip.Create("SFX_Noise", samples, 1, sampleRate, false);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 10f);
                data[i] = (Random.value * 2f - 1f) * env * volume;
            }

            clip.SetData(data, 0);
            return clip;
        }

        private void OnAudioRead(float[] data)
        {
            if (!playing) return;

            GenerateMusicBuffer(data);
        }

        private void OnAudioSetPosition(int newPosition)
        {
            bufferPosition = newPosition;
        }

        private void GenerateMusicBuffer(float[] data)
        {
            int channels = 2;
            int frames = data.Length / channels;
            float beatDuration = 60f / bpm;

            for (int i = 0; i < frames; i++)
            {
                float t = (float)i / sampleRate;

                // Update beat
                beatTime += 1f / sampleRate;
                if (beatTime >= beatDuration)
                {
                    beatTime -= beatDuration;
                    beat++;
                    if (beat >= 16) beat = 0; // 4 bars of 4 beats
                }

                // Generate music based on current style
                float sample = 0f;

                // Drums
                sample += GenerateKick(t);
                sample += GenerateSnare(t);
                sample += GenerateHihat(t);

                // Bass
                sample += GenerateBass(t);

                // Melody/Lead
                sample += GenerateLead(t);

                // Pad/Chords
                sample += GeneratePad(t);

                // Apply master volume and muffling
                float masterVol = musicVolume * (muffled ? 0.3f : 1f);
                sample *= masterVol;

                // Stereo
                data[i * 2] = sample;     // Left
                data[i * 2 + 1] = sample; // Right
            }
        }

        private float GenerateKick(float t)
        {
            // Kick on beats 1 and 3 (and sometimes 2&4 for funk)
            float beatPos = beatTime / (60f / bpm);
            float kick = 0f;

            if (Mathf.Abs(beatPos % 1f) < 0.02f || (currentStyle == 0 && Mathf.Abs((beatPos + 0.5f) % 1f) < 0.02f))
            {
                kickPhase = 0f;
            }

            if (kickPhase < 0.1f)
            {
                float env = Mathf.Exp(-kickPhase * 30f);
                kick = Mathf.Sin(2f * Mathf.PI * (60f - kickPhase * 40f) * kickPhase) * env * 0.8f;
            }
            kickPhase += 1f / sampleRate;

            return kick;
        }

        private float GenerateSnare(float t)
        {
            // Snare on beats 2 and 4
            float beatPos = beatTime / (60f / bpm);
            float snare = 0f;

            if (Mathf.Abs((beatPos + 0.5f) % 1f) < 0.02f)
            {
                snarePhase = 0f;
            }

            if (snarePhase < 0.15f)
            {
                float env = Mathf.Exp(-snarePhase * 15f);
                float tone = Mathf.Sin(2f * Mathf.PI * 200f * snarePhase) * 0.3f;
                float noise = (Random.value * 2f - 1f) * 0.7f;
                snare = (tone + noise) * env * 0.6f;
            }
            snarePhase += 1f / sampleRate;

            return snare;
        }

        private float GenerateHihat(float t)
        {
            // Hihat on every 8th note
            float beatPos = beatTime / (60f / bpm);
            float hihat = 0f;

            if (Mathf.Abs((beatPos * 2f) % 0.5f) < 0.01f)
            {
                hihatPhase = 0f;
            }

            if (hihatPhase < 0.05f)
            {
                float env = Mathf.Exp(-hihatPhase * 50f);
                hihat = (Random.value * 2f - 1f) * env * 0.3f;
            }
            hihatPhase += 1f / sampleRate;

            return hihat;
        }

        private float GenerateBass(float t)
        {
            // Bass follows root notes of the scale
            int[] scale = GameConstants.MusicStyles[currentStyle].scale;
            float rootFreq = NoteToFrequency(GameConstants.MusicStyles[currentStyle].key);

            // Simple bass pattern
            float noteIndex = (beat % scale.Length);
            float freq = rootFreq * Mathf.Pow(2f, scale[noteIndex] / 12f) * 0.5f; // One octave down

            oscillatorPhases[0] += freq / sampleRate * 2f * Mathf.PI;
            if (oscillatorPhases[0] > 2f * Mathf.PI) oscillatorPhases[0] -= 2f * Mathf.PI;

            float env = 0.5f + 0.5f * Mathf.Sin(beatTime * 4f); // Pulsing
            return Mathf.Sin(oscillatorPhases[0]) * env * 0.3f;
        }

        private float GenerateLead(float t)
        {
            // Lead melody - simplified
            int[] scale = GameConstants.MusicStyles[currentStyle].scale;
            float rootFreq = NoteToFrequency(GameConstants.MusicStyles[currentStyle].key);

            // Play notes on beats
            float lead = 0f;
            if (beatTime < 0.05f || (beatTime > 0.45f && beatTime < 0.5f))
            {
                int noteIdx = Random.Range(0, scale.Length);
                float freq = rootFreq * Mathf.Pow(2f, scale[noteIdx] / 12f);

                oscillatorPhases[1] += freq / sampleRate * 2f * Mathf.PI;
                if (oscillatorPhases[1] > 2f * Mathf.PI) oscillatorPhases[1] -= 2f * Mathf.PI;

                float env = Mathf.Exp(-beatTime * 8f);
                lead = Mathf.Sin(oscillatorPhases[1]) * env * 0.2f;
            }

            return lead;
        }

        private float GeneratePad(float t)
        {
            // Atmospheric pad chord
            int[] scale = GameConstants.MusicStyles[currentStyle].scale;
            float rootFreq = NoteToFrequency(GameConstants.MusicStyles[currentStyle].key);

            float pad = 0f;
            // Major/minor triad
            int[] chord = { 0, 2, 4 }; // Root, third, fifth (simplified)
            for (int i = 0; i < chord.Length; i++)
            {
                float freq = rootFreq * Mathf.Pow(2f, scale[chord[i] % scale.Length] / 12f) * 0.25f;
                oscillatorPhases[2 + i] += freq / sampleRate * 2f * Mathf.PI;
                if (oscillatorPhases[2 + i] > 2f * Mathf.PI) oscillatorPhases[2 + i] -= 2f * Mathf.PI;

                pad += Mathf.Sin(oscillatorPhases[2 + i]) * 0.05f;
            }

            return pad * 0.3f;
        }

        private float NoteToFrequency(string note)
        {
            // Simplified: A4 = 440Hz
            switch (note.ToUpper())
            {
                case "C": return 261.63f;
                case "D": return 293.66f;
                case "E": return 329.63f;
                case "F": return 349.23f;
                case "G": return 392.00f;
                case "A": return 440.00f;
                case "B": return 493.88f;
                default: return 440f;
            }
        }

        public void Play(int styleIndex)
        {
            currentStyle = Mathf.Clamp(styleIndex, 0, GameConstants.MusicStyles.Length - 1);
            bpm = GameConstants.MusicStyles[currentStyle].bpm;
            beat = 0;
            beatTime = 0f;
            playing = true;
            muffled = false;

            if (musicSource != null && musicClip != null)
            {
                musicSource.clip = musicClip;
                musicSource.Play();
            }
        }

        public void Stop()
        {
            playing = false;
            if (musicSource != null) musicSource.Stop();
        }

        public void Muffle(bool muffle)
        {
            muffled = muffle;
        }

        public void SetVolumes(float music, float sfx)
        {
            musicVolume = Mathf.Clamp01(music);
            sfxVolume = Mathf.Clamp01(sfx);
            if (musicSource != null) musicSource.volume = musicVolume;
            if (sfxSource != null) sfxSource.volume = sfxVolume;
        }

        public void PlaySFX(string name)
        {
            if (sfxClips.ContainsKey(name) && sfxSource != null)
            {
                sfxSource.PlayOneShot(sfxClips[name], sfxVolume);
            }
        }

        public float GetEnergy()
        {
            // Return energy based on current beat position
            float beatPos = beatTime / (60f / bpm);
            return Mathf.Sin(beatPos * Mathf.PI * 2f) * 0.5f + 0.5f;
        }

        public void Init()
        {
            // Re-initialize if needed
            if (!playing)
            {
                Play(currentStyle);
            }
        }
    }
}