import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { MeshoptDecoder } from 'three/addons/libs/meshopt_decoder.module.js';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';
import { CITIES } from './cities.js';
import { Track, LANES, TRUCK_LAT } from './track.js';
import { World } from './world.js';
import { Truck } from './truck.js';
import { Kid, TRICKS } from './kid.js';
import { Obstacles } from './obstacles.js';
import { Music, STYLES } from './music.js';
import { Dance, PADS } from './dance.js';

const $ = (id) => document.getElementById(id);
const clamp = (v, a, b) => Math.max(a, Math.min(b, v));

// ---------------- salvamento ----------------
const SAVE_KEY = 'carretaJoy.v1';
let save = { best: {}, stars: {}, settings: { music: 0.8, sfx: 0.9, quality: 'media' }, kid: 0, city: 0 };
try { const s = JSON.parse(localStorage.getItem(SAVE_KEY)); if (s) save = { ...save, ...s, settings: { ...save.settings, ...(s.settings || {}) } }; } catch (e) { }
const persist = () => { try { localStorage.setItem(SAVE_KEY, JSON.stringify(save)); } catch (e) { } };

// ---------------- renderizador ----------------
const canvas = $('c');
const renderer = new THREE.WebGLRenderer({ canvas, antialias: save.settings.quality !== 'leve', powerPreference: 'high-performance' });
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.toneMappingExposure = 1.0;
renderer.outputColorSpace = THREE.SRGBColorSpace;
const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(62, 1, 0.3, 4000);
const composer = new EffectComposer(renderer);
composer.addPass(new RenderPass(scene, camera));
const bloom = new UnrealBloomPass(new THREE.Vector2(256, 256), 0.85, 0.45, 0.8);
composer.addPass(bloom);
composer.addPass(new OutputPass());
let useBloom = false;

function applyQuality() {
  const q = save.settings.quality;
  const pr = Math.min(window.devicePixelRatio || 1, q === 'alta' ? 2 : q === 'media' ? 1.5 : 1);
  renderer.setPixelRatio(pr);
  composer.setPixelRatio(pr);
  resize();
}
function resize() {
  const w = window.innerWidth, h = window.innerHeight;
  renderer.setSize(w, h, false);
  composer.setSize(w, h);
  bloom.resolution.set(w / 2, h / 2);
  camera.aspect = w / h;
  camera.fov = w / h < 1.5 ? 70 : 62;
  camera.userData.off = undefined; camera.clearViewOffset();
  camera.updateProjectionMatrix();
}
window.addEventListener('resize', resize);

// ---------------- estado ----------------
const music = new Music();
music.setVolumes(save.settings.music, save.settings.sfx);
const G = {
  phase: 'loading', city: null, cityIdx: save.city || 0, night: null,
  track: null, world: null, truck: null, obstacles: null, kids: [], active: save.kid || 0,
  score: 0, combo: 0, comboT: 0, beat: 0, t: 0, danced: false, paused: false,
  stats: { tricks: 0, notes: 0, gold: 0, tombos: 0, best: 0, dance: null },
  onEvent: (...a) => onEvent(...a),
  shake: 0,
};
let models = {};
const dance = new Dance(music, $('dance'), onDanceJudge);

// ---------------- carregamento ----------------
async function loadModels() {
  const loader = new GLTFLoader().setMeshoptDecoder(MeshoptDecoder);
  const files = { carreta: 'models/carreta.glb', arthur: 'models/Arthur.glb', afonso: 'models/Afonso.glb' };
  const prog = {};
  const upd = () => {
    const vals = Object.values(prog); const l = vals.reduce((a, p) => a + p.l, 0), t = vals.reduce((a, p) => a + p.t, 0) || 1;
    $('loadbar').style.width = `${Math.round(100 * l / t)}%`;
  };
  await Promise.all(Object.entries(files).map(([k, url]) => new Promise((res, rej) => {
    prog[k] = { l: 0, t: 1 };
    loader.load(url, (g) => { models[k] = g.scene; prog[k].l = prog[k].t; upd(); res(); },
      (e) => { prog[k] = { l: e.loaded, t: e.total || e.loaded || 1 }; upd(); }, rej);
  })));
}

function makeThumb(obj) {
  // retrato para o menu
  const size = 256;
  const rt = new THREE.WebGLRenderTarget(size, size);
  const sc = new THREE.Scene();
  sc.add(new THREE.HemisphereLight(0xffffff, 0x553377, 2.2));
  const dl = new THREE.DirectionalLight(0xffffff, 2); dl.position.set(1, 2, 3); sc.add(dl);
  const o = obj.clone(true); sc.add(o);
  const box = new THREE.Box3().setFromObject(o), c = box.getCenter(new THREE.Vector3()), s = box.getSize(new THREE.Vector3());
  const cam = new THREE.PerspectiveCamera(30, 1, 0.01, 100);
  const top = new THREE.Vector3(c.x, box.max.y - s.y * 0.28, c.z);
  cam.position.set(c.x + s.y * 0.1, top.y + s.y * 0.05, c.z + s.y * 1.35); cam.lookAt(top);
  renderer.setRenderTarget(rt); renderer.setClearColor(0x000000, 0); renderer.clear(); renderer.render(sc, cam);
  const px = new Uint8Array(size * size * 4);
  renderer.readRenderTargetPixels(rt, 0, 0, size, size, px);
  renderer.setRenderTarget(null);
  const cv = document.createElement('canvas'); cv.width = cv.height = size;
  const ctx = cv.getContext('2d'), img = ctx.createImageData(size, size);
  for (let y = 0; y < size; y++) img.data.set(px.subarray((size - 1 - y) * size * 4, (size - y) * size * 4), y * size * 4);
  ctx.putImageData(img, 0, 0);
  rt.dispose();
  return cv.toDataURL();
}

// ---------------- menu ----------------
function stars(n) { return [0, 1, 2].map(i => i < n ? '★' : '<span class="off">★</span>').join(''); }
function buildMenu() {
  const list = $('citylist'); list.innerHTML = '';
  CITIES.forEach((c, i) => {
    const b = document.createElement('button');
    b.className = 'citycard' + (i === G.cityIdx ? ' sel' : '');
    b.innerHTML = `<b>${c.nome} <small style="display:inline">${c.uf}</small></b><small>${c.descricao}</small>
      <div class="st">${stars(save.stars[c.id] || 0)}</div><div class="mus">🎵 ${STYLES[c.musica].nome}${save.best[c.id] ? ' · recorde ' + save.best[c.id] : ''}</div>`;
    b.onclick = () => { G.cityIdx = i; G.night = null; music.init(); music.sfx('click'); buildMenu(); };
    list.appendChild(b);
  });
  const night = G.night ?? CITIES[G.cityIdx].noite;
  $('btnDia').classList.toggle('sel', !night); $('btnNoite').classList.toggle('sel', night);
  document.querySelectorAll('.kidcard').forEach(k => k.classList.toggle('sel', +k.dataset.kid === G.active));
}
document.querySelectorAll('.kidcard').forEach(k => k.onclick = () => { G.active = +k.dataset.kid; music.init(); music.sfx('click'); buildMenu(); });
$('btnDia').onclick = () => { G.night = false; buildMenu(); };
$('btnNoite').onclick = () => { G.night = true; buildMenu(); };
$('btnPlay').onclick = () => { music.init(); save.kid = G.active; save.city = G.cityIdx; persist(); startCity(G.cityIdx, G.night ?? CITIES[G.cityIdx].noite); };
$('btnConfig').onclick = () => { $('volMusic').value = save.settings.music; $('volSfx').value = save.settings.sfx; $('quality').value = save.settings.quality; show('config', true); };
$('volMusic').oninput = (e) => { save.settings.music = +e.target.value; music.setVolumes(save.settings.music, save.settings.sfx); };
$('volSfx').oninput = (e) => { save.settings.sfx = +e.target.value; music.setVolumes(save.settings.music, save.settings.sfx); music.init(); music.sfx('coin'); };
$('quality').onchange = (e) => { save.settings.quality = e.target.value; applyQuality(); };
$('btnConfigOk').onclick = () => { persist(); show('config', false); };

function show(id, on) { $(id).classList.toggle('show', on); }

// ---------------- iniciar cidade ----------------
function clearCity() {
  if (G.world) G.world.dispose();
  if (G.obstacles) G.obstacles.dispose();
  G.world = G.obstacles = null;
}

function startCity(idx, night) {
  clearCity();
  dance.cancel();
  const city = CITIES[idx];
  G.city = city; G.cityIdx = idx; G.night = night;
  G.track = new Track(city);
  G.world = new World(scene, G.track, city, night, save.settings.quality);
  G.obstacles = new Obstacles(scene, G.track, city);
  G.truck.s = 12; G.truck.v = 0; G.truck.setNight(night);
  G.truck.place(G.track);
  for (const k of G.kids) { k.reset(); }
  G.score = 0; G.combo = 0; G.comboT = 0; G.danced = false; G.finished = false;
  G.stats = { tricks: 0, notes: 0, gold: 0, tombos: 0, best: 0, dance: null };
  G.phase = 'intro'; G.phaseT = 0; G.paused = false;
  useBloom = save.settings.quality !== 'leve' && (night || save.settings.quality === 'alta');
  bloom.strength = night ? 0.75 : 0.3;
  bloom.threshold = night ? 0.85 : 0.95;
  renderer.toneMappingExposure = night ? 1.1 : 1.0;
  ['menu', 'pause', 'results'].forEach(s => show(s, false));
  $('hud').classList.remove('hidden');
  $('cityname').textContent = `${city.nome} - ${city.uf}  ·  🎵 ${STYLES[city.musica].nome}`;
  $('progpraca').style.left = `${100 * G.track.praca / G.track.length}%`;
  music.play(city.musica);
  music.muffle(false);
  setTimeout(() => music.sfx('horn'), 300);
  hint('Prontos? A carreta vai sair!', 2.5);
  camInit = true;
  updateHUD();
}

// ---------------- pontuação e eventos ----------------
function mult() { return Math.min(8, 1 + Math.floor(G.combo / 2)); }
function addScore(pts, kid) {
  const v = Math.round(pts * mult() * (kid && kid !== player() ? 0.5 : 1));
  G.score += v; return v;
}
function bump() { G.combo++; G.comboT = 4.5; G.stats.best = Math.max(G.stats.best, G.combo); }
function player() { return G.kids[G.active]; }
function buddy() { return G.kids[1 - G.active]; }

let lastCoin = 0;
function onEvent(type, kid, a, b) {
  const me = kid === player();
  switch (type) {
    case 'desceu':
      music.sfx('land');
      if (me && !G.toldStreet) { G.toldStreet = true; hint('Pule os obstáculos e faça manobras no ar! ⬆', 3); }
      break;
    case 'subiu':
      music.sfx('climb'); if (me) toast('Subiu! +20', 'small'); addScore(20, kid);
      break;
    case 'manobra': { // estrela no chão
      const pts = addScore(TRICKS[a].pts, kid); bump(); G.stats.tricks++;
      if (me) { toast(`${TRICKS[a].nome}! +${pts}`); music.sfx('trick'); }
      break;
    }
    case 'pousou': {
      const list = a, top = b;
      if (list.length) {
        let base = list.reduce((s, t) => s + TRICKS[t].pts, 0);
        if (list.length > 1) base *= 1 + (list.length - 1) * 0.5;
        const pts = addScore(base, kid); bump(); G.stats.tricks += list.length;
        if (me) {
          const names = list.map(t => TRICKS[t].nome).join(' + ');
          toast(`${names}! +${pts}`, list.length > 1 ? 'gold' : ''); music.sfx(list.length > 1 ? 'combo' : 'trick');
        }
      }
      if (top && (top.type === 'carro' || top.type === 'banco')) {
        const pts = addScore(top.type === 'carro' ? 50 : 20, kid); bump();
        if (me) toast(`PARKOUR! +${pts}`, 'small');
      }
      if (me) music.sfx('land');
      break;
    }
    case 'rampa':
      if (me) { toast('VOOOU! 🚀', 'small'); music.sfx('jump'); } addScore(30, kid);
      break;
    case 'tombo':
      G.combo = 0; G.stats.tombos++;
      if (me) { toast(b ? 'Caiu da manobra!' : 'Opa! Tombo!', 'bad'); music.sfx('crash'); G.shake = 0.4; }
      break;
    case 'nota': {
      addScore(10, kid); G.comboT = Math.max(G.comboT, 2.5); G.stats.notes++;
      if (G.t - lastCoin > 0.06) { music.sfx('coin'); lastCoin = G.t; }
      break;
    }
    case 'estrelaDourada':
      addScore(250, kid); bump(); G.stats.gold++; music.sfx('star');
      toast('⭐ ESTRELA DOURADA! ⭐', 'gold');
      break;
  }
  updateHUD();
}

function onDanceJudge(q, move, streak) {
  if (q === 'erro') { music.sfx('miss'); return; }
  music.sfx(q === 'perfeito' ? 'perfect' : 'good');
  const special = streak > 0 && streak % 8 === 0;
  for (const k of G.kids) k.danceMove(special ? 'mortal' : move);
  if (special) { toast(`SEQUÊNCIA ${streak}! MORTAL JUNTOS!`, 'gold'); music.sfx('cheer'); }
  G.score += q === 'perfeito' ? 50 : q === 'bom' ? 30 : 15;
  updateHUD();
}

// ---------------- HUD ----------------
let toastCount = 0;
function toast(text, cls = '') {
  const box = $('toasts');
  while (box.children.length > 2) box.firstChild.remove();
  const d = document.createElement('div'); d.className = 'toast ' + cls; d.textContent = text;
  box.appendChild(d); setTimeout(() => d.remove(), 1300); toastCount++;
}
let hintTimer = null;
function hint(text, secs = 2.5) {
  const h = $('hint'); h.textContent = text; h.classList.add('show');
  clearTimeout(hintTimer); hintTimer = setTimeout(() => h.classList.remove('show'), secs * 1000);
}
function updateHUD() {
  $('score').textContent = G.score.toLocaleString('pt-BR');
  const m = mult(); $('mult').textContent = `x${m}`; $('mult').classList.toggle('hidden', m < 2);
  const p = player();
  $('who').textContent = `🎮 ${p ? p.name : ''}`;
}
function updateControls() {
  const p = player(), ctx = $('btnCtx');
  const busy = G.phase !== 'drive' && G.phase !== 'chegando';
  if (p.mode === 'roof') { ctx.textContent = '⬇ DESCER'; ctx.disabled = busy; }
  else if (p.mode === 'street') { ctx.textContent = '⬆ SUBIR'; ctx.disabled = busy || !p.onGround; }
  else { ctx.disabled = true; }
  ctx.classList.toggle('pulse', !busy && p.mode === 'roof' && !G.toldStreet);
  const street = p.mode === 'street';
  document.querySelectorAll('#controls .dir').forEach(b => b.disabled = !street || busy);
  $('controls').style.visibility = G.phase === 'dance' ? 'hidden' : 'visible';
  document.querySelector('.topcenter').style.visibility = G.phase === 'dance' ? 'hidden' : 'visible';
  const pct = clamp(G.truck.s / G.track.length, 0, 1) * 100;
  $('progfill').style.width = pct + '%'; $('progtruck').style.left = pct + '%';
}

// ---------------- controles ----------------
function act(a) {
  if (G.paused || !G.track) return;
  const p = player();
  if (G.phase === 'dance') return;
  if (a === 'swap') { G.active = 1 - G.active; music.sfx('click'); toast(`Agora é o ${player().name}!`, 'small'); updateHUD(); return; }
  if (G.phase !== 'drive' && G.phase !== 'intro' && G.phase !== 'chegando') return;
  if (p.mode === 'roof') {
    // no teto: botões fazem passinhos de dança
    const map = { jump: 'cima', mortal: 'mortal', flip: 'cima', estrela: 'dir', left: 'esq', right: 'esq' };
    if (a === 'ctx' && G.phase === 'drive') { if (p.beginDescend(G)) music.sfx('jump'); return; }
    if (map[a]) { p.danceMove(map[a]); G.score += 5; updateHUD(); }
    return;
  }
  if (p.mode !== 'street') return;
  switch (a) {
    case 'left': p.move(-1); break;
    case 'right': p.move(1); break;
    case 'jump': if (p.jump()) music.sfx('jump'); break;
    case 'mortal': case 'flip': case 'estrela': {
      const wasGround = p.onGround;
      if (p.startTrick(a)) { music.sfx(wasGround && a !== 'estrela' ? 'jump' : 'trick'); }
      break;
    }
    case 'ctx': if (p.beginClimb(G)) music.sfx('climb'); break;
  }
}
document.querySelectorAll('#controls .ctl').forEach(b => {
  b.addEventListener('pointerdown', (e) => { e.preventDefault(); b.classList.add('on'); act(b.dataset.act); });
  const off = () => b.classList.remove('on');
  b.addEventListener('pointerup', off); b.addEventListener('pointerleave', off); b.addEventListener('pointercancel', off);
});
// gestos no canvas
let sw = null;
canvas.addEventListener('pointerdown', (e) => { sw = { x: e.clientX, y: e.clientY, t: performance.now() }; });
canvas.addEventListener('pointerup', (e) => {
  if (!sw) return; const dx = e.clientX - sw.x, dy = e.clientY - sw.y; sw = null;
  if (Math.max(Math.abs(dx), Math.abs(dy)) < 40) return;
  if (Math.abs(dx) > Math.abs(dy)) act(dx > 0 ? 'right' : 'left'); else if (dy < 0) act('jump');
});
// teclado (para testar no computador)
const KEYS = { ArrowLeft: 'left', ArrowRight: 'right', ArrowUp: 'jump', ' ': 'jump', z: 'mortal', x: 'estrela', c: 'flip', e: 'ctx', q: 'swap', Tab: 'swap' };
const DKEYS = { ArrowLeft: 0, ArrowUp: 1, ArrowDown: 2, ArrowRight: 3, d: 0, f: 1, j: 2, k: 3 };
window.addEventListener('keydown', (e) => {
  if (e.repeat) return;
  if (e.key === 'Escape' || e.key === 'p') { togglePause(); return; }
  if (G.phase === 'dance' && DKEYS[e.key] !== undefined) { dance.press(DKEYS[e.key]); e.preventDefault(); return; }
  const a = KEYS[e.key]; if (a) { act(a); e.preventDefault(); }
});
// botões da dança
PADS.forEach((p, i) => {
  const b = document.createElement('button'); b.className = 'dpad'; b.style.background = p.cor;
  b.innerHTML = `${p.label}<small>${p.nome}</small>`;
  b.addEventListener('pointerdown', (e) => { e.preventDefault(); dance.press(i); });
  document.querySelector('.dpads').appendChild(b);
});

// ---------------- pausa / resultado ----------------
function togglePause(force) {
  if (!G.track || G.phase === 'menu' || G.phase === 'fim') return false;
  const on = force !== undefined ? force : !G.paused;
  G.paused = on; show('pause', on);
  if (music.ctx) on ? music.ctx.suspend() : music.ctx.resume();
  return true;
}
$('btnPause').onclick = () => togglePause();
$('btnResume').onclick = () => togglePause(false);
$('btnRestart').onclick = () => { togglePause(false); startCity(G.cityIdx, G.night); };
$('btnMenu').onclick = () => { togglePause(false); goMenu(); };
$('btnAgain').onclick = () => startCity(G.cityIdx, G.night);
$('btnNext').onclick = () => { const n = (G.cityIdx + 1) % CITIES.length; startCity(n, CITIES[n].noite); };
$('btnResMenu').onclick = () => goMenu();
window.onAndroidBack = () => {
  if ($('config').classList.contains('show')) { show('config', false); return true; }
  if (G.phase === 'menu' || G.phase === 'loading') return false;
  if (G.phase === 'fim') { goMenu(); return true; }
  togglePause(); return true;
};
window.onAppPause = () => { if (G.phase !== 'menu' && G.phase !== 'loading' && G.phase !== 'fim') togglePause(true); else if (music.ctx) music.ctx.suspend(); };
window.onAppResume = () => { if (!G.paused && music.ctx) music.ctx.resume(); };
document.addEventListener('visibilitychange', () => { if (document.hidden) window.onAppPause(); });

function goMenu() {
  G.phase = 'menu'; dance.cancel();
  show('results', false); show('pause', false);
  $('hud').classList.add('hidden');
  buildMenu(); show('menu', true);
  // fundo do menu: a carreta parada com a cidade escolhida
  menuScene();
}

function finish() {
  G.phase = 'fim'; G.phaseT = 0;
  const c = G.city;
  const th = [2500, 6500, 12000];
  const n = th.filter(t => G.score >= t).length;
  const newBest = G.score > (save.best[c.id] || 0);
  if (newBest) save.best[c.id] = G.score;
  save.stars[c.id] = Math.max(save.stars[c.id] || 0, n);
  persist();
  setTimeout(() => {
    if (G.phase !== 'fim') return;
    $('resTitle').textContent = `Chegamos em ${c.nome}!`;
    $('resStars').innerHTML = stars(n);
    $('resScore').textContent = G.score.toLocaleString('pt-BR') + ' pts';
    const d = G.stats.dance;
    $('resInfo').innerHTML = `${newBest ? '🏆 <b>Novo recorde!</b><br>' : ''}Manobras: ${G.stats.tricks} · Notas: ${G.stats.notes} · Estrelas douradas: ${G.stats.gold}<br>
      Maior combo: ${G.stats.best} · Tombos: ${G.stats.tombos}${d ? `<br>Dança: ${d.pct}% (${d.hits}/${d.total} acertos)` : ''}`;
    $('btnNext').textContent = `Próxima: ${CITIES[(G.cityIdx + 1) % CITIES.length].nome} ▶`;
    show('results', true);
    music.sfx('cheer');
  }, 2600);
}

// ---------------- IA do irmão ----------------
function aiUpdate(k, dt) {
  const p = player();
  k.aiT = (k.aiT || 0) - dt;
  if (G.phase !== 'drive') return;
  if (k.mode === 'roof' && p.mode === 'street' && k.aiT <= 0) {
    if (k.aiWait === undefined) k.aiWait = 1.2;
    k.aiWait -= dt;
    if (k.aiWait <= 0) { k.beginDescend(G); k.aiWait = undefined; k.aiLane = 2; }
    return;
  }
  if (k.mode === 'street' && (p.mode === 'roof' || p.mode === 'up') && k.onGround && !k.trick) { k.beginClimb(G); return; }
  if (k.mode !== 'street') return;
  // escolhe faixa diferente do jogador e livre de caixas
  const O = G.obstacles, s = k.s ?? G.truck.s;
  let want = k.aiLane ?? 2;
  if (want === p.laneIdx && p.mode === 'street') want = p.laneIdx === 2 ? 1 : 2;
  const blocked = (lane) => { const o = O.ahead(s, lane, 16); return o && o.type === 'caixas'; };
  if (blocked(want)) { for (const l of [2, 1, 0]) if (!blocked(l) && (p.mode !== 'street' || l !== p.laneIdx)) { want = l; break; } }
  k.aiLane = want;
  if (k.laneIdx !== want && k.onGround && !k.trick) k.move(Math.sign(want - k.laneIdx));
  // pula obstáculos
  const o = O.ahead(s, k.laneIdx, 10);
  if (o && k.onGround && !k.trick && o.type !== 'rampa' && o.type !== 'banco') {
    const d = (o.s - o.len / 2) - s;
    if (d > 0 && d < G.truck.v * 0.33 + 0.4) { k.jump(); }
  }
  // manobras no ar
  if (!k.onGround && !k.trick && k.vy > 2) {
    const rem = (k.vy + Math.sqrt(k.vy * k.vy + 2 * 24 * Math.max(0, k.y - k.ground))) / 24;
    const opts = ['mortal', 'flip', 'pirueta', 'estrela'].filter(t => TRICKS[t].dur < rem * 0.92);
    if (opts.length && Math.random() < 0.06) k.startTrick(opts[Math.floor(Math.random() * opts.length)]);
  }
}

// ---------------- câmera ----------------
const camPos = new THREE.Vector3(), camLook = new THREE.Vector3(), tmpV = new THREE.Vector3(), tmpL = new THREE.Vector3();
let camInit = true;
function updateCamera(dt) {
  const T = G.track, tr = G.truck, p = player();
  let desired, look;
  if (G.phase === 'menu') {
    const a = G.t * 0.12;
    desired = tr.toWorld(tmpV.set(Math.sin(a) * 15 - 4, 5 + Math.sin(G.t * 0.3), Math.cos(a) * 15));
    look = tr.toWorld(tmpL.set(0, 3, 0));
  } else if (G.phase === 'intro') {
    const k = clamp(G.phaseT / 3.2, 0, 1), e = k * k * (3 - 2 * k);
    desired = tr.toWorld(tmpV.set(-8 - 4 * e, 3 + 5 * e, 20 - 34 * e));
    look = tr.toWorld(tmpL.set(0, 3.5, 0));
  } else if (G.phase === 'dance' || G.phase === 'toDance') {
    const a = -Math.PI / 2 + Math.sin(G.t * 0.35) * 0.75;
    desired = tr.toWorld(tmpV.set(Math.sin(a) * 9.5, tr.roofY + 2.2 + Math.sin(G.t * 0.5) * 0.8, Math.cos(a) * 9.5 - 0.5));
    look = tr.toWorld(tmpL.set(0, tr.roofY + 0.9, -0.5));
  } else if (G.phase === 'fim') {
    desired = tr.toWorld(tmpV.set(-9, 6, 16));
    look = tr.toWorld(tmpL.set(0, tr.roofY, -2));
  } else if (p.mode === 'street') {
    const s = p.s;
    desired = T.pos(s - 6, p.lat * 0.7 + 1.3, 2.9 + p.y * 0.6, tmpV);
    look = T.pos(s + 6, p.lat * 0.85 - 0.6, 1.1 + p.y * 0.65, tmpL);
  } else {
    desired = tr.toWorld(tmpV.set(-7.5, tr.roofY + 3.2, -12));
    look = tr.toWorld(tmpL.set(-1, tr.roofY + 0.8, 2));
  }
  if (!isFinite(desired.x + desired.y + desired.z + look.x + look.y + look.z)) return;
  if (camInit || !isFinite(camPos.x + camLook.x)) { camPos.copy(desired); camLook.copy(look); camInit = false; }
  const k = 1 - Math.exp(-dt * (p.mode === 'street' && G.phase === 'drive' ? 6 : 3));
  camPos.lerp(desired, k); camLook.lerp(look, k);
  // não deixar a câmera entrar no chão
  camera.position.copy(camPos);
  if (G.shake > 0) { G.shake -= dt; camera.position.x += (Math.random() - 0.5) * G.shake; camera.position.y += (Math.random() - 0.5) * G.shake; }
  camera.lookAt(camLook);
  // na dança, desloca a imagem pra esquerda (as notas ficam à direita)
  const off = G.phase === 'dance';
  if (off !== camera.userData.off) {
    camera.userData.off = off;
    if (off) camera.setViewOffset(innerWidth, innerHeight, innerWidth * 0.2, 0, innerWidth, innerHeight); else camera.clearViewOffset();
  }
}

// ---------------- fases ----------------
function updatePhase(dt) {
  const T = G.track, tr = G.truck;
  G.phaseT += dt;
  const base = 11.5;
  let target = 0;
  if (G.phase === 'intro') {
    target = 0;
    if (G.phaseT > 1.0 && G.phaseT - dt <= 1.0) toast('3', 'big');
    if (G.phaseT > 1.8 && G.phaseT - dt <= 1.8) toast('2', 'big');
    if (G.phaseT > 2.6 && G.phaseT - dt <= 2.6) toast('1', 'big');
    if (G.phaseT > 3.4) { G.phase = 'drive'; G.phaseT = 0; toast('VAI! 🎉', 'big gold'); music.sfx('horn'); hint('Toque em DESCER para pular na rua!', 3.5); }
  } else if (G.phase === 'drive') {
    const f = T.frame(tr.s, {});
    target = clamp(base * (1 - f.grade * 2.2), 8, 14);
    if (!G.danced && tr.s > T.praca - 70) {
      G.phase = 'toDance'; G.phaseT = 0;
      hint('Praça à vista! Todo mundo pro teto da carreta! 💃', 3.5);
      music.sfx('horn');
    }
    if (tr.s > T.length - 70 && !G.finished) { G.phase = 'chegando'; G.phaseT = 0; hint('Chegando! 🏁', 2); }
  }
  if (G.phase === 'toDance') {
    const left = T.praca - tr.s;
    target = Math.min(base, Math.sqrt(Math.max(0, 2 * 2.6 * left)));
    for (const k of G.kids) if (k.mode === 'street' && k.onGround && !k.trick) k.beginClimb(G);
    if (left < 0.6 && tr.v < 0.6 && G.kids.every(k => k.mode === 'roof')) {
      G.phase = 'dance'; G.phaseT = 0; dance.start(32); music.sfx('cheer');
    }
  } else if (G.phase === 'dance') {
    target = 0;
    const r = dance.update();
    if (r) {
      G.stats.dance = r; G.danced = true;
      toast(r.pct >= 85 ? `ARRASARAM! ${r.pct}%` : r.pct >= 60 ? `Muito bem! ${r.pct}%` : `Boa! ${r.pct}%`, 'gold');
      music.sfx('cheer'); music.sfx('horn');
      G.phase = 'drive'; G.phaseT = 0;
      hint('Bora! Desce da carreta e continua a festa!', 3);
    }
  } else if (G.phase === 'chegando') {
    const left = (T.length - 22) - tr.s;
    target = Math.min(base, Math.sqrt(Math.max(0, 2 * 2.4 * left)));
    // continua jogável até parar
    if (left < 0.6 && tr.v < 0.5) {
      for (const k of G.kids) if (k.mode === 'street' && k.onGround) k.beginClimb(G);
      if (!G.finished) { G.finished = true; finish(); }
    }
  }
  const acc = target > tr.v ? 3.2 : 6;
  tr.v += clamp(target - tr.v, -acc * dt, acc * dt);
  tr.s = Math.min(T.length - 1, tr.s + tr.v * dt);
}

// ---------------- loop ----------------
let last = performance.now();
let fpsAcc = 0, fpsN = 0, lowFps = 0;
function frame(now) {
  requestAnimationFrame(frame);
  let dt = Math.min(0.05, (now - last) / 1000); last = now;
  if (!G.track) { return; }
  if (G.paused) { render(); return; }
  G.t += dt;
  music.update(dt);
  G.beat = music.playing ? music.beatPos() : G.t * 2;
  const party = G.phase === 'dance' || G.phase === 'fim' || G.phase === 'menu';

  if (G.phase !== 'menu') updatePhase(dt);
  G.truck.place(G.track);
  G.truck.update(dt, G.t, G.beat, music.energy, true);
  if (G.phase !== 'menu') {
    for (const k of G.kids) { if (k !== player()) aiUpdate(k, dt); k.update(dt, G); }
    G.obstacles.update(dt, G.t);
    if (G.comboT > 0) { G.comboT -= dt; if (G.comboT <= 0) { G.combo = 0; updateHUD(); } }
    updateControls();
  } else {
    for (const k of G.kids) k.update(dt, G);
  }
  G.world.update(dt, G.t, G.beat, music.energy, G.truck.s, camera, party);
  updateCamera(dt);
  render();

  // qualidade automática: se ficar muito lento, desliga o brilho
  fpsAcc += dt; fpsN++;
  if (fpsAcc > 2) { const fps = fpsN / fpsAcc; fpsAcc = 0; fpsN = 0; if (fps < 24 && useBloom) { lowFps++; if (lowFps >= 2) { useBloom = false; } } else lowFps = 0; }
}
function render() { if (useBloom) composer.render(); else renderer.render(scene, camera); }

// cena de fundo do menu
function menuScene() {
  const idx = G.cityIdx;
  if (!G.world || G.city !== CITIES[idx] || G.phase !== 'menu') {
    clearCity();
    G.city = CITIES[idx]; G.track = new Track(G.city);
    G.world = new World(scene, G.track, G.city, true, 'leve');
    G.obstacles = new Obstacles(scene, G.track, G.city);
  }
  G.truck.s = 60; G.truck.v = 0; G.truck.setNight(true); G.truck.place(G.track);
  for (const k of G.kids) k.reset();
  useBloom = save.settings.quality !== 'leve'; bloom.strength = 0.75; bloom.threshold = 0.85;
  G.phase = 'menu';
  if (!music.playing && music.ctx) music.play('funk');
  music.muffle(true);
  camInit = true;
}

// ---------------- início ----------------
async function boot() {
  applyQuality();
  try {
    await loadModels();
  } catch (e) {
    $('loadtxt').textContent = 'Erro ao carregar os modelos 3D: ' + e.message;
    return;
  }
  $('loadtxt').textContent = 'Preparando a festa...';
  G.truck = new Truck(models.carreta);
  scene.add(G.truck.root);
  const roof = G.truck.roofY;
  G.kids = [
    new Kid('Arthur', models.arthur, 1.45, new THREE.Vector3(-0.7, roof, -2.0)),
    new Kid('Afonso', models.afonso, 1.15, new THREE.Vector3(-0.7, roof, 1.0)),
  ];
  for (const k of G.kids) scene.add(k.root, k.shadow);
  try {
    $('thumb0').src = makeThumb(models.arthur);
    $('thumb1').src = makeThumb(models.afonso);
  } catch (e) { console.warn(e); }
  menuScene();
  show('loading', false);
  buildMenu(); show('menu', true);
  // a música do menu começa no primeiro toque (regra dos navegadores)
  const first = () => { music.init(); if (!music.playing) { music.play('funk'); music.muffle(true); } window.removeEventListener('pointerdown', first); };
  window.addEventListener('pointerdown', first);
  requestAnimationFrame(frame);
}
boot();
window.__G = G; // depuração
