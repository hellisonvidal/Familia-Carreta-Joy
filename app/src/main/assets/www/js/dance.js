// Momento de dança na praça: jogo de ritmo sincronizado com a música.
// Cada acerto faz os dois meninos dançarem juntos no teto da carreta.
export const PADS = [
  { key: 'esq', label: '⬅', cor: '#00c853', nome: 'Passinho' },
  { key: 'cima', label: '⬆', cor: '#ffc400', nome: 'Giro' },
  { key: 'baixo', label: '⬇', cor: '#2979ff', nome: 'Rebolado' },
  { key: 'dir', label: '➡', cor: '#ff3d7f', nome: 'Estrela' },
];

const TRAVEL = 1.7; // segundos que a nota leva para descer

export class Dance {
  constructor(music, root, onJudge) {
    this.music = music; this.root = root; this.onJudge = onJudge;
    this.lanesEl = root.querySelector('.dlanes');
    this.judgeEl = root.querySelector('.djudge');
    this.infoEl = root.querySelector('.dinfo');
    this.active = false;
  }

  start(beats = 32) {
    const m = this.music;
    const startBeat = Math.ceil(m.beatPos() / 4) * 4 + 4; // começa no próximo compasso + 1
    this.notes = [];
    let last = -1, rep = 0;
    for (let b = 0; b < beats; b++) {
      const harder = b >= beats / 2;
      const addNote = (beat) => {
        let lane = Math.floor(Math.random() * 4);
        if (lane === last) { rep++; if (rep > 1) lane = (lane + 1 + Math.floor(Math.random() * 3)) % 4; } else rep = 0;
        last = lane;
        this.notes.push({ beat, t: m.beatTime(beat), lane, judged: false, el: null });
      };
      if (b % 8 === 7 && !harder) continue; // respiro
      if (Math.random() < 0.88) addNote(startBeat + b);
      if (harder && Math.random() < 0.3) addNote(startBeat + b + 0.5);
    }
    this.endT = this.notes[this.notes.length - 1].t + 1.2;
    this.startT = m.beatTime(startBeat);
    this.hits = 0; this.perfect = 0; this.streak = 0; this.bestStreak = 0; this.score = 0;
    this.active = true;
    this.root.classList.add('show');
    this.lanesEl.querySelectorAll('.dnote').forEach(e => e.remove());
    this.judge('Prepare-se!', 'prep');
  }

  judge(text, cls) {
    this.judgeEl.textContent = text;
    this.judgeEl.className = 'djudge ' + cls;
    void this.judgeEl.offsetWidth; this.judgeEl.classList.add('pop');
  }

  press(lane) {
    if (!this.active) return;
    const now = this.music.now;
    let best = null, bd = 1e9;
    for (const n of this.notes) {
      if (n.judged || n.lane !== lane) continue;
      const d = Math.abs(n.t - now);
      if (d < bd) { bd = d; best = n; }
    }
    const padEl = this.root.querySelectorAll('.dpad')[lane];
    padEl.classList.remove('hit'); void padEl.offsetWidth; padEl.classList.add('hit');
    if (!best || bd > 0.22) return; // toque fora do tempo não pune
    best.judged = true;
    if (best.el) { best.el.classList.add('done'); setTimeout(() => best.el && best.el.remove(), 200); }
    let q;
    if (bd < 0.08) { q = 'perfeito'; this.perfect++; this.score += 100; }
    else if (bd < 0.15) { q = 'bom'; this.score += 60; }
    else { q = 'ok'; this.score += 30; }
    this.hits++; this.streak++; this.bestStreak = Math.max(this.bestStreak, this.streak);
    this.judge(q === 'perfeito' ? 'PERFEITO!' : q === 'bom' ? 'BOM!' : 'OK', q);
    this.onJudge(q, PADS[lane].key, this.streak);
  }

  update() {
    if (!this.active) return null;
    const now = this.music.now;
    const H = this.lanesEl.clientHeight, hitY = H - 70;
    for (const n of this.notes) {
      if (n.judged) continue;
      const dt = n.t - now;
      if (dt > TRAVEL) continue;
      if (!n.el) {
        n.el = document.createElement('div');
        n.el.className = 'dnote';
        n.el.style.left = `calc(${n.lane * 25 + 12.5}% - 32px)`;
        n.el.style.background = PADS[n.lane].cor;
        n.el.textContent = PADS[n.lane].label;
        this.lanesEl.appendChild(n.el);
      }
      const y = hitY - (dt / TRAVEL) * hitY;
      n.el.style.transform = `translateY(${y - 32}px)`;
      if (dt < -0.22) {
        n.judged = true; n.miss = true; this.streak = 0;
        n.el.classList.add('miss'); const el = n.el; setTimeout(() => el.remove(), 250);
        this.judge('Ops!', 'miss');
        this.onJudge('erro', null, 0);
      }
    }
    const total = this.notes.length;
    const done = this.notes.filter(n => n.judged).length;
    this.infoEl.textContent = now < this.startT ? `Começa em ${Math.max(1, Math.ceil(this.startT - now))}...` : `${done}/${total}  ·  sequência ${this.streak}`;
    if (now > this.endT) {
      this.active = false;
      this.root.classList.remove('show');
      const pct = Math.round(100 * (this.hits + this.perfect * 0.5) / (total * 1.5));
      return { pct, score: this.score, hits: this.hits, total, bestStreak: this.bestStreak };
    }
    return null;
  }

  cancel() { this.active = false; this.root.classList.remove('show'); }
}
