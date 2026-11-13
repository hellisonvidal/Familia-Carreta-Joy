// Motor de música e efeitos sonoros 100% sintetizados (WebAudio).
// Cada cidade tem um estilo próprio: funk, samba, forró, axé e eletrônico.
// Todas as músicas são composições originais geradas por código.

const NOTE = (n) => 440 * Math.pow(2, (n - 69) / 12);

// Padrões de 16 passos (semicolcheias) por compasso.
// x = forte, o = fraco, . = silêncio
export const STYLES = {
  funk: {
    nome: 'Funk da Carreta', bpm: 130, root: 50, scale: [0, 3, 5, 7, 10],
    kick: 'x..x..x...x..x..', snare: '....x.......x...', clap: '....x..o....x..o',
    hat: 'o.o.o.o.o.o.o.o.', bell: '..x...x...x..x..', surdo: '................', tri: '................',
    bass: [0, null, null, 0, null, null, 0, null, null, null, 3, null, null, 5, null, null],
    chords: [[0, 3, 7], [-2, 2, 5], [-4, 0, 3], [-2, 2, 5]],
    lead: [7, null, 10, null, 12, null, 10, 7, null, 5, null, 7, null, null, 3, null,
           7, null, 10, null, 12, 12, 15, null, 12, null, 10, null, 7, null, null, null],
    leadWave: 'square', bassWave: 'sawtooth', padWave: 'sawtooth',
  },
  samba: {
    nome: 'Samba da Orla', bpm: 104, root: 55, scale: [0, 2, 4, 7, 9],
    kick: 'x.......x.......', snare: '..o.o..o..o.o..o', clap: '................',
    hat: 'xoooxoooxoooxooo', bell: 'x.x..x.x.x..x.x.', surdo: '....x.......x...', tri: '................',
    bass: [0, null, null, 7, 0, null, null, 7, 5, null, null, 12, 5, null, 4, null],
    chords: [[0, 4, 7, 11], [9, 12, 16], [2, 5, 9], [7, 11, 14]],
    lead: [12, null, 14, 16, null, 14, 12, null, 9, null, 7, null, 9, 12, null, null,
           16, null, 14, null, 12, 14, 16, 19, null, 16, null, 14, 12, null, null, null],
    leadWave: 'triangle', bassWave: 'triangle', padWave: 'square',
  },
  forro: {
    nome: 'Forró das Montanhas', bpm: 122, root: 52, scale: [0, 2, 4, 6, 7, 9, 10],
    kick: 'x..x....x..x....', snare: '................', clap: '................',
    hat: '................', bell: '................', surdo: 'x..x....x..x....', tri: 'x.ox.oxox.ox.oxo',
    bass: [0, null, null, 7, null, null, 12, null, 5, null, null, 12, null, null, 7, null],
    chords: [[0, 4, 7], [5, 9, 12], [7, 10, 14], [0, 4, 7]],
    lead: [12, 14, 16, null, 18, 16, 14, null, 16, null, 12, null, 10, null, 12, null,
           19, null, 18, 16, null, 14, 16, null, 12, null, 14, null, 12, null, null, null],
    leadWave: 'square', bassWave: 'triangle', padWave: 'triangle',
  },
  axe: {
    nome: 'Axé do Pelourinho', bpm: 136, root: 53, scale: [0, 2, 4, 7, 9],
    kick: 'x...x...x...x...', snare: '..x...x...x..xx.', clap: '....x.......x...',
    hat: '.x.x.x.x.x.x.x.x', bell: 'x..x..x...x.x...', surdo: 'x.....x.x.....x.', tri: '................',
    bass: [0, null, 0, null, 7, null, 0, null, 5, null, 5, null, 7, null, 9, null],
    chords: [[0, 4, 7], [5, 9, 12], [7, 11, 14], [5, 9, 12]],
    lead: [16, null, 16, 14, 12, null, 14, null, 16, null, 19, null, 16, null, 14, null,
           12, null, 12, 14, 16, null, 14, 12, 9, null, 12, null, null, null, null, null],
    leadWave: 'sawtooth', bassWave: 'square', padWave: 'sawtooth',
  },
  eletro: {
    nome: 'Eletrô Curitibano', bpm: 124, root: 57, scale: [0, 2, 3, 5, 7, 8, 10],
    kick: 'x...x...x...x...', snare: '................', clap: '....x.......x...',
    hat: '..x...x...x...x.', bell: '................', surdo: '................', tri: '................',
    bass: [null, null, 0, null, null, null, 0, null, null, null, 0, null, null, null, 12, null],
    chords: [[0, 3, 7], [-4, 0, 3], [-9, -5, -2], [-2, 2, 5]],
    lead: [12, null, 15, null, 19, null, 15, null, 12, null, 15, null, 19, null, 22, null,
           20, null, 19, null, 15, null, 12, null, 14, null, 15, null, 14, null, 10, null],
    leadWave: 'sawtooth', bassWave: 'sawtooth', padWave: 'sawtooth',
  },
};

export class Music {
  constructor() {
    this.ctx = null;
    this.style = STYLES.funk;
    this.playing = false;
    this.musicVol = 0.8;
    this.sfxVol = 0.9;
    this.step = 0;
    this.bar = 0;
    this.t0 = 0;
    this.nextTime = 0;
    this.energy = 0; // 0..1 pulsa a cada batida (para as luzes)
    this.timer = null;
  }

  init() {
    if (this.ctx) { if (this.ctx.state === 'suspended') this.ctx.resume(); return; }
    const AC = window.AudioContext || window.webkitAudioContext;
    this.ctx = new AC();
    const c = this.ctx;
    this.comp = c.createDynamicsCompressor();
    this.comp.threshold.value = -14; this.comp.ratio.value = 4;
    this.comp.connect(c.destination);
    this.musicBus = c.createGain(); this.musicBus.gain.value = this.musicVol * 0.55;
    this.musicBus.connect(this.comp);
    this.sfxBus = c.createGain(); this.sfxBus.gain.value = this.sfxVol;
    this.sfxBus.connect(this.comp);
    // filtro "de longe" (usado quando a música abafa)
    this.musicFilter = c.createBiquadFilter(); this.musicFilter.type = 'lowpass'; this.musicFilter.frequency.value = 18000;
    this.musicFilter.connect(this.musicBus);
    // eco simples para o lead
    this.delay = c.createDelay(1); this.delayGain = c.createGain(); this.delayGain.gain.value = 0.25;
    this.delay.connect(this.delayGain); this.delayGain.connect(this.delay); this.delayGain.connect(this.musicFilter);
    // ruído reutilizável
    const len = c.sampleRate * 1;
    this.noise = c.createBuffer(1, len, c.sampleRate);
    const d = this.noise.getChannelData(0);
    for (let i = 0; i < len; i++) d[i] = Math.random() * 2 - 1;
  }

  setVolumes(music, sfx) {
    this.musicVol = music; this.sfxVol = sfx;
    if (!this.ctx) return;
    this.musicBus.gain.setTargetAtTime(music * 0.55, this.ctx.currentTime, 0.05);
    this.sfxBus.gain.setTargetAtTime(sfx, this.ctx.currentTime, 0.05);
  }

  get spb() { return 60 / this.style.bpm; } // segundos por batida
  get now() { return this.ctx ? this.ctx.currentTime : 0; }
  // posição atual em batidas desde o início da música
  beatPos(t = this.now) { return (t - this.t0) / this.spb; }
  // tempo exato de uma batida
  beatTime(b) { return this.t0 + b * this.spb; }

  play(styleKey) {
    this.init();
    this.stop();
    this.style = STYLES[styleKey] || STYLES.funk;
    this.delay.delayTime.value = this.spb * 0.75;
    this.playing = true;
    this.step = 0; this.bar = 0;
    this.t0 = this.ctx.currentTime + 0.12;
    this.nextTime = this.t0;
    this.timer = setInterval(() => this.schedule(), 25);
    this.schedule();
  }

  stop() {
    this.playing = false;
    if (this.timer) clearInterval(this.timer);
    this.timer = null;
  }

  muffle(on) {
    if (!this.ctx) return;
    this.musicFilter.frequency.setTargetAtTime(on ? 900 : 18000, this.ctx.currentTime, 0.15);
  }

  schedule() {
    if (!this.playing) return;
    const ahead = this.ctx.currentTime + 0.18;
    const sixteenth = this.spb / 4;
    while (this.nextTime < ahead) {
      this.playStep(this.step, this.nextTime);
      this.nextTime += sixteenth;
      this.step++;
      if (this.step % 16 === 0) this.bar++;
    }
  }

  playStep(stepAbs, t) {
    const s = this.style;
    const i = stepAbs % 16;
    const bar = Math.floor(stepAbs / 16);
    const section = Math.floor(bar / 4) % 4; // 0..3 -> varia arranjo
    const ch = s.chords[bar % s.chords.length];
    const hit = (pat) => pat[i] === 'x' ? 1 : pat[i] === 'o' ? 0.5 : 0;

    let k = hit(s.kick); if (k) this.kick(t, k);
    let sn = hit(s.snare); if (sn) this.snare(t, sn);
    let cl = hit(s.clap); if (cl && section !== 0) this.clap(t, cl);
    let h = hit(s.hat); if (h) this.hat(t, h);
    let b = hit(s.bell); if (b) this.bell(t, b, i);
    let su = hit(s.surdo); if (su) this.surdo(t, su);
    let tr = hit(s.tri); if (tr) this.triangle(t, tr);

    const bn = s.bass[i];
    if (bn !== null && bn !== undefined) this.bass(t, s.root - 12 + ch[0] + bn, sixteenthLen(this) * 1.6);
    if (i === 0 || (i === 8 && s.bpm < 120)) this.pad(t, ch.map(n => s.root + n), this.spb * (s.bpm < 120 ? 1.8 : 3.5));
    if (i === 6 || i === 14) { if (s === STYLES.funk || s === STYLES.axe) this.stab(t, ch.map(n => s.root + 12 + n)); }

    // melodia toca nas seções 1 e 3, e uma variação na 2
    if (section !== 0) {
      const li = (bar % 2) * 16 + i;
      let ln = s.lead[li];
      if (ln !== null && ln !== undefined) {
        if (section === 2) ln += (li % 3 === 0 ? 2 : 0);
        this.lead(t, s.root + ln, sixteenthLen(this) * 1.8);
      }
    }
    // virada de bateria no fim de cada 4 compassos
    if (bar % 4 === 3 && i >= 12) this.snare(t, 0.6);
  }

  // ---------- instrumentos ----------
  env(g, t, a, peak, dec) {
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(peak, t + a);
    g.gain.exponentialRampToValueAtTime(0.0001, t + a + dec);
  }
  osc(type, freq, t, dur, peak, dest, a = 0.005) {
    const c = this.ctx, o = c.createOscillator(), g = c.createGain();
    o.type = type; o.frequency.setValueAtTime(freq, t);
    this.env(g, t, a, peak, dur);
    o.connect(g); g.connect(dest);
    o.start(t); o.stop(t + a + dur + 0.05);
    return o;
  }
  noiseHit(t, dur, peak, type, freq, q, dest) {
    const c = this.ctx, src = c.createBufferSource(), f = c.createBiquadFilter(), g = c.createGain();
    src.buffer = this.noise; f.type = type; f.frequency.value = freq; f.Q.value = q || 1;
    this.env(g, t, 0.002, peak, dur);
    src.connect(f); f.connect(g); g.connect(dest);
    src.start(t, Math.random() * 0.5); src.stop(t + dur + 0.05);
  }
  kick(t, v) {
    const c = this.ctx, o = c.createOscillator(), g = c.createGain();
    o.frequency.setValueAtTime(150, t); o.frequency.exponentialRampToValueAtTime(42, t + 0.12);
    this.env(g, t, 0.003, 1.0 * v, 0.32);
    o.connect(g); g.connect(this.musicFilter); o.start(t); o.stop(t + 0.4);
    this._pulse(t, 1);
  }
  snare(t, v) { this.noiseHit(t, 0.14, 0.45 * v, 'bandpass', 1800, 0.8, this.musicFilter); this.osc('triangle', 190, t, 0.08, 0.25 * v, this.musicFilter); this._pulse(t, 0.6); }
  clap(t, v) { for (let k = 0; k < 3; k++) this.noiseHit(t + k * 0.011, 0.06 + k * 0.03, 0.35 * v, 'bandpass', 1200, 1.2, this.musicFilter); }
  hat(t, v) { this.noiseHit(t, 0.035, 0.18 * v, 'highpass', 8000, 0.7, this.musicFilter); }
  bell(t, v, i) { const f = (i % 4 === 0) ? 560 : 830; this.osc('square', f, t, 0.12, 0.08 * v, this.musicFilter); this.osc('square', f * 1.5, t, 0.08, 0.04 * v, this.musicFilter); }
  surdo(t, v) {
    const c = this.ctx, o = c.createOscillator(), g = c.createGain();
    o.frequency.setValueAtTime(95, t); o.frequency.exponentialRampToValueAtTime(60, t + 0.2);
    this.env(g, t, 0.004, 0.7 * v, 0.4); o.connect(g); g.connect(this.musicFilter); o.start(t); o.stop(t + 0.5);
    this._pulse(t, 0.8);
  }
  triangle(t, v) { this.osc('sine', 3200, t, v > 0.7 ? 0.35 : 0.06, 0.06 * v, this.musicFilter); this.osc('sine', 4700, t, 0.05, 0.03 * v, this.musicFilter); }
  bass(t, n, dur) {
    const c = this.ctx, o = c.createOscillator(), f = c.createBiquadFilter(), g = c.createGain();
    o.type = this.style.bassWave; o.frequency.setValueAtTime(NOTE(n), t);
    f.type = 'lowpass'; f.frequency.setValueAtTime(900, t); f.frequency.exponentialRampToValueAtTime(200, t + dur);
    this.env(g, t, 0.006, 0.45, dur);
    o.connect(f); f.connect(g); g.connect(this.musicFilter); o.start(t); o.stop(t + dur + 0.1);
  }
  pad(t, notes, dur) {
    for (const n of notes) {
      for (const det of [-6, 6]) {
        const c = this.ctx, o = c.createOscillator(), f = c.createBiquadFilter(), g = c.createGain();
        o.type = this.style.padWave; o.frequency.value = NOTE(n); o.detune.value = det;
        f.type = 'lowpass'; f.frequency.value = 1400;
        g.gain.setValueAtTime(0.0001, t); g.gain.linearRampToValueAtTime(0.035, t + 0.08);
        g.gain.setValueAtTime(0.035, t + dur * 0.7); g.gain.linearRampToValueAtTime(0.0001, t + dur);
        o.connect(f); f.connect(g); g.connect(this.musicFilter); o.start(t); o.stop(t + dur + 0.05);
      }
    }
  }
  stab(t, notes) { for (const n of notes) this.osc('square', NOTE(n), t, 0.09, 0.04, this.musicFilter); }
  lead(t, n, dur) {
    const c = this.ctx, o = c.createOscillator(), o2 = c.createOscillator(), f = c.createBiquadFilter(), g = c.createGain();
    o.type = this.style.leadWave; o2.type = 'sine';
    o.frequency.value = NOTE(n); o2.frequency.value = NOTE(n + 12);
    // vibrato
    const lfo = c.createOscillator(), lg = c.createGain(); lfo.frequency.value = 5.5; lg.gain.value = 4;
    lfo.connect(lg); lg.connect(o.frequency); lfo.start(t); lfo.stop(t + dur + 0.1);
    f.type = 'lowpass'; f.frequency.value = 3200;
    this.env(g, t, 0.01, 0.11, dur);
    o.connect(f); o2.connect(f); f.connect(g); g.connect(this.musicFilter); g.connect(this.delay);
    o.start(t); o2.start(t); o.stop(t + dur + 0.1); o2.stop(t + dur + 0.1);
  }
  _pulse(t, amt) {
    const ms = Math.max(0, (t - this.ctx.currentTime) * 1000);
    setTimeout(() => { this.energy = Math.max(this.energy, amt); }, ms);
  }

  // ---------- efeitos ----------
  sfx(name) {
    if (!this.ctx) return;
    const t = this.ctx.currentTime + 0.01, B = this.sfxBus;
    switch (name) {
      case 'jump': {
        const o = this.osc('square', 300, t, 0.15, 0.12, B); o.frequency.exponentialRampToValueAtTime(700, t + 0.12); break;
      }
      case 'land': this.noiseHit(t, 0.08, 0.3, 'lowpass', 400, 1, B); break;
      case 'trick': {
        const c = this.ctx, src = c.createBufferSource(), f = c.createBiquadFilter(), g = c.createGain();
        src.buffer = this.noise; f.type = 'bandpass'; f.Q.value = 3;
        f.frequency.setValueAtTime(400, t); f.frequency.exponentialRampToValueAtTime(3000, t + 0.3);
        this.env(g, t, 0.05, 0.35, 0.3); src.connect(f); f.connect(g); g.connect(B); src.start(t); src.stop(t + 0.45); break;
      }
      case 'coin': this.osc('sine', 1318, t, 0.08, 0.18, B); this.osc('sine', 1976, t + 0.07, 0.18, 0.18, B); break;
      case 'star': [1046, 1318, 1568, 2093].forEach((f, k) => this.osc('triangle', f, t + k * 0.06, 0.2, 0.15, B)); break;
      case 'combo': [784, 988, 1175].forEach((f, k) => this.osc('square', f, t + k * 0.05, 0.12, 0.08, B)); break;
      case 'crash': this.noiseHit(t, 0.35, 0.6, 'lowpass', 600, 1, B); this.osc('sawtooth', 120, t, 0.25, 0.2, B); break;
      case 'climb': for (let k = 0; k < 4; k++) this.osc('triangle', 500 + k * 120, t + k * 0.12, 0.06, 0.1, B); break;
      case 'horn': {
        for (const [f, d] of [[233, 0], [294, 0], [233, 0.45], [294, 0.45]]) {
          const o = this.ctx.createOscillator(), g = this.ctx.createGain(), fl = this.ctx.createBiquadFilter();
          o.type = 'sawtooth'; o.frequency.value = f; fl.type = 'lowpass'; fl.frequency.value = 1800;
          g.gain.setValueAtTime(0.0001, t + d); g.gain.linearRampToValueAtTime(0.18, t + d + 0.03);
          g.gain.setValueAtTime(0.18, t + d + 0.32); g.gain.linearRampToValueAtTime(0.0001, t + d + 0.38);
          o.connect(fl); fl.connect(g); g.connect(B); o.start(t + d); o.stop(t + d + 0.4);
        }
        break;
      }
      case 'cheer': {
        const c = this.ctx, src = c.createBufferSource(), f = c.createBiquadFilter(), g = c.createGain();
        src.buffer = this.noise; src.loop = true; f.type = 'bandpass'; f.frequency.value = 1500; f.Q.value = 0.6;
        g.gain.setValueAtTime(0.0001, t); g.gain.linearRampToValueAtTime(0.3, t + 0.3); g.gain.linearRampToValueAtTime(0.0001, t + 2.2);
        src.connect(f); f.connect(g); g.connect(B); src.start(t); src.stop(t + 2.3);
        for (let k = 0; k < 6; k++) this.osc('triangle', 900 + Math.random() * 900, t + 0.1 + Math.random() * 1.2, 0.25, 0.04, B);
        break;
      }
      case 'perfect': this.osc('sine', 1568, t, 0.12, 0.15, B); this.osc('sine', 2349, t, 0.12, 0.08, B); break;
      case 'good': this.osc('sine', 1175, t, 0.1, 0.12, B); break;
      case 'miss': this.osc('square', 160, t, 0.12, 0.08, B); break;
      case 'click': this.osc('sine', 880, t, 0.05, 0.12, B); break;
    }
  }

  update(dt) { this.energy = Math.max(0, this.energy - dt * 4); }
}

function sixteenthLen(m) { return m.spb / 4; }
