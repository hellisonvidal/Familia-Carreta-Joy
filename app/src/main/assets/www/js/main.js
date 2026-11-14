import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { MeshoptDecoder } from 'three/addons/libs/meshopt_decoder.module.js';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
import { CITIES, DURATIONS } from './cities.js';
import { City, HALF } from './city.js';
import { Truck, VARIANTS, TRUCK_LEN } from './truck.js';
import { Actor, TRICKS } from './actor.js';
import { CHARACTERS, COSTUMES, KID_COSTUMES, makeBody, POSES } from './characters.js';
import { Music, STYLES } from './music.js';
import { Dance, PADS } from './dance.js';
import { Life } from './life.js';

const $ = (id) => document.getElementById(id);
const clamp = (v, a, b) => Math.max(a, Math.min(b, v));

// ---------------- salvamento ----------------
const SAVE_KEY = 'carretaJoy.v2';
let save = {
  best: {}, stars: {}, city: 0, night: null, char: 'arthur', truck: 'arcoiris', minutes: 10,
  costumes: { arthur: 'normal', afonso: 'normal' },
  settings: { music: 0.8, sfx: 0.9, quality: 'media', bright: 1.2, contrast: 1.05, satur: 1.1 },
};
try { const s = JSON.parse(localStorage.getItem(SAVE_KEY)); if (s) save = { ...save, ...s, settings: { ...save.settings, ...(s.settings || {}) }, costumes: { ...save.costumes, ...(s.costumes || {}) } }; } catch (e) { }
const persist = () => { try { localStorage.setItem(SAVE_KEY, JSON.stringify(save)); } catch (e) { } };

// ---------------- renderizador ----------------
const canvas = $('c');
const renderer = new THREE.WebGLRenderer({ canvas, antialias: save.settings.quality !== 'leve', powerPreference: 'high-performance' });
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.outputColorSpace = THREE.SRGBColorSpace;
renderer.shadowMap.enabled = save.settings.quality !== 'leve';
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
const scene = new THREE.Scene();
const pmrem = new THREE.PMREMGenerator(renderer);
scene.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
scene.environmentIntensity = 0.55;
const camera = new THREE.PerspectiveCamera(62, 1, 0.3, 5000);
// luz que acompanha a câmera: ilumina o rosto dos personagens (nada de personagem escuro)
const camLight = new THREE.DirectionalLight(0xffffff, 0.9);
scene.add(camLight, camLight.target);
const composer = new EffectComposer(renderer);
composer.addPass(new RenderPass(scene, camera));
const bloom = new UnrealBloomPass(new THREE.Vector2(256, 256), 0.75, 0.45, 0.85);
composer.addPass(bloom); composer.addPass(new OutputPass());
let useBloom = false;

// ---------------- estado ----------------
const music = new Music();
music.setVolumes(save.settings.music, save.settings.sfx);
const G = {
  phase: 'loading', city: null, cityIdx: save.city, night: null, truck: null, actors: [], player: null,
  score: 0, combo: 0, comboT: 0, beat: 0, now: 0, paused: false, freeze: false, dancing: false,
  stats: {}, shake: 0, timeLeft: 600, total: 600, visited: new Set(), stopState: null,
  onEvent: (...a) => onEvent(...a),
  // som com distância: longe da câmera não toca
  sfx: (name, src) => { if (src && G.camPos) { const p = src.p || src; if (Math.hypot(p.x - G.camPos.x, p.z - G.camPos.z) > 45) return; } music.sfx(name); },
};
window.__G = G;
const assets = {};
const dance = new Dance(music, $('dance'), onDanceJudge);

function applyLook() {
  const s = save.settings;
  renderer.toneMappingExposure = s.bright * (G.night ? 1.05 : 1);
  canvas.style.filter = `contrast(${s.contrast}) saturate(${s.satur})`;
}
function applyQuality() {
  const q = save.settings.quality;
  const pr = Math.min(window.devicePixelRatio || 1, q === 'alta' ? 2 : q === 'media' ? 1.5 : 1);
  renderer.setPixelRatio(pr); composer.setPixelRatio(pr); resize();
}
function resize() {
  const w = innerWidth, h = innerHeight;
  renderer.setSize(w, h, false); composer.setSize(w, h); bloom.resolution.set(w / 2, h / 2);
  camera.aspect = w / h; camera.fov = w / h < 1.5 ? 72 : 62;
  camera.userData.off = undefined; camera.clearViewOffset(); camera.updateProjectionMatrix();
}
addEventListener('resize', resize);

// ---------------- carregamento ----------------
async function loadModels() {
  const loader = new GLTFLoader().setMeshoptDecoder(MeshoptDecoder);
  const files = { carreta: 'models/carreta.glb', afonsoFull: 'models/Afonso.glb', arthurHead: 'models/ArthurHead.glb', afonsoHead: 'models/AfonsoHead.glb', arthurBody: 'models/ArthurBody.glb', afonsoBody: 'models/AfonsoBody.glb' };
  const prog = {};
  const upd = () => { const v = Object.values(prog); const l = v.reduce((a, p) => a + p.l, 0), t = v.reduce((a, p) => a + p.t, 0) || 1; $('loadbar').style.width = `${Math.round(100 * l / t)}%`; };
  await Promise.all(Object.entries(files).map(([k, url]) => new Promise((res, rej) => {
    prog[k] = { l: 0, t: 1 };
    loader.load(url, (g) => { assets[k] = g.scene; prog[k].l = prog[k].t; upd(); res(); }, (e) => { prog[k] = { l: e.loaded, t: e.total || e.loaded || 1 }; upd(); }, rej);
  })));
  // pele/cabelo dos meninos sem "metal" (era isso que deixava eles escuros)
  for (const k of ['afonsoFull', 'arthurHead', 'afonsoHead']) assets[k].traverse(o => {
    if (o.isMesh) { o.material.metalness = 0; o.material.metalnessMap = null; o.material.roughness = 0.78; o.material.envMapIntensity = 0.8; o.material.needsUpdate = true; o.frustumCulled = false; }
  });
}

// retrato para os menus
const thumbs = {};
function makeThumb(obj, key) {
  const W = 192, H = 256, rt = new THREE.WebGLRenderTarget(W, H);
  const sc = new THREE.Scene(); sc.environment = scene.environment;
  sc.add(new THREE.HemisphereLight(0xffffff, 0x553377, 2.2)); const dl = new THREE.DirectionalLight(0xffffff, 2); dl.position.set(1, 2, 3); sc.add(dl);
  sc.add(obj); obj.updateMatrixWorld(true);
  const box = new THREE.Box3().setFromObject(obj), c = box.getCenter(new THREE.Vector3()), s = box.getSize(new THREE.Vector3());
  const cam = new THREE.PerspectiveCamera(28, W / H, 0.01, 100);
  const look = new THREE.Vector3(c.x, box.min.y + s.y * 0.58, c.z);
  cam.position.set(c.x + s.y * 0.15, look.y + s.y * 0.05, c.z + s.y * 2.3); cam.lookAt(look);
  renderer.setRenderTarget(rt); renderer.setClearColor(0x000000, 0); renderer.clear(); renderer.render(sc, cam);
  const px = new Uint8Array(W * H * 4);
  renderer.readRenderTargetPixels(rt, 0, 0, W, H, px); renderer.setRenderTarget(null);
  const cv = document.createElement('canvas'); cv.width = W; cv.height = H;
  const ctx = cv.getContext('2d'), img = ctx.createImageData(W, H);
  for (let y = 0; y < H; y++) img.data.set(px.subarray((H - 1 - y) * W * 4, (H - y) * W * 4), y * W * 4);
  ctx.putImageData(img, 0, 0); rt.dispose(); sc.remove(obj);
  thumbs[key] = cv.toDataURL(); return thumbs[key];
}
function renderThumbs() {
  for (const def of CHARACTERS) { const b = makeBody(def, def.kid ? save.costumes[def.id] : null, assets); b.apply(POSES.dance(0.5, def.estilo, null, 0)); makeThumb(b.root, def.id); }
}

// ---------------- menu ----------------
function stars(n) { return [0, 1, 2].map(i => i < n ? '★' : '<span class="off">★</span>').join(''); }
function costumeChips(el, kidId, onPick) {
  el.innerHTML = '';
  KID_COSTUMES.forEach(k => {
    const b = document.createElement('button'); b.className = 'chip' + (save.costumes[kidId] === k ? ' sel' : '');
    b.textContent = k === 'normal' ? 'Roupa dele' : COSTUMES[k].nome;
    b.onclick = () => { save.costumes[kidId] = k; persist(); music.init(); music.sfx('click'); onPick(); };
    el.appendChild(b);
  });
}
function buildMenu() {
  const night = G.night ?? CITIES[G.cityIdx].noite;
  const cl = $('citylist'); cl.innerHTML = '';
  CITIES.forEach((c, i) => {
    const b = document.createElement('button'); b.className = 'card' + (i === G.cityIdx ? ' sel' : '');
    b.innerHTML = `<b>${c.nome} <small style="display:inline">${c.uf}</small></b><small>${c.descricao}</small><div class="st">${stars(save.stars[c.id] || 0)}</div><div class="mus">🎵 ${STYLES[c.musica].nome}${save.best[c.id] ? ' · recorde ' + save.best[c.id] : ''}</div>`;
    b.onclick = () => { G.cityIdx = i; G.night = null; music.init(); music.sfx('click'); buildMenu(); menuScene(); };
    cl.appendChild(b);
  });
  $('btnDia').classList.toggle('sel', !night); $('btnNoite').classList.toggle('sel', night);
  const ch = $('charlist'); ch.innerHTML = '';
  CHARACTERS.forEach(def => {
    const b = document.createElement('button'); b.className = 'ccard' + (save.char === def.id ? ' sel' : '');
    b.innerHTML = `<img src="${thumbs[def.id] || ''}"><span>${def.nome}</span>`;
    b.onclick = () => { save.char = def.id; persist(); music.init(); music.sfx('click'); buildMenu(); };
    ch.appendChild(b);
  });
  costumeChips($('costA'), 'arthur', buildMenu); costumeChips($('costB'), 'afonso', buildMenu);
  const tl = $('trucklist'); tl.innerHTML = '';
  VARIANTS.forEach(v => {
    const b = document.createElement('button'); b.className = 'card' + (save.truck === v.id ? ' sel' : '');
    b.innerHTML = `<div class="sw" style="background:${v.id === 'arcoiris' ? 'linear-gradient(90deg,#ff3d7f,#ffc400,#00c853,#2979ff)' : v.cor}"></div><b>${v.nome}</b><small>${v.desc}</small>`;
    b.onclick = () => { save.truck = v.id; persist(); music.init(); music.sfx('click'); buildMenu(); menuScene(true); };
    tl.appendChild(b);
  });
  const tm = $('timelist'); tm.innerHTML = '';
  DURATIONS.forEach(m => { const b = document.createElement('button'); b.className = 'chip' + (save.minutes === m ? ' sel' : ''); b.textContent = `${m} min`; b.onclick = () => { save.minutes = m; persist(); music.init(); music.sfx('click'); buildMenu(); }; tm.appendChild(b); });
  const c = CITIES[G.cityIdx], def = CHARACTERS.find(d => d.id === save.char) || CHARACTERS[0], v = VARIANTS.find(v => v.id === save.truck) || VARIANTS[0];
  $('summary').innerHTML = `🏙️ <b>${c.nome}</b> ${night ? '🌙' : '☀️'} · 🧒 <b>${def.nome}</b> · 🚚 <b>${v.nome}</b> · ⏱ <b>${save.minutes} min</b>`;
}
document.querySelectorAll('.tab').forEach(t => t.onclick = () => {
  document.querySelectorAll('.tab').forEach(x => x.classList.toggle('sel', x === t));
  document.querySelectorAll('.tabpage').forEach(p => p.classList.toggle('show', p.id === t.dataset.tab));
  music.init(); music.sfx('click');
});
$('btnDia').onclick = () => { G.night = false; buildMenu(); menuScene(); };
$('btnNoite').onclick = () => { G.night = true; buildMenu(); menuScene(); };
$('btnPlay').onclick = () => { music.init(); save.city = G.cityIdx; persist(); startCity(G.cityIdx, G.night ?? CITIES[G.cityIdx].noite); };
function openConfig() {
  const s = save.settings;
  $('volMusic').value = s.music; $('volSfx').value = s.sfx; $('quality').value = s.quality; $('bright').value = s.bright; $('contrast').value = s.contrast; $('satur').value = s.satur;
  show('config', true);
}
$('btnConfig').onclick = openConfig;
$('btnPauseCfg').onclick = openConfig;
$('volMusic').oninput = (e) => { save.settings.music = +e.target.value; music.setVolumes(save.settings.music, save.settings.sfx); };
$('volSfx').oninput = (e) => { save.settings.sfx = +e.target.value; music.setVolumes(save.settings.music, save.settings.sfx); music.init(); music.sfx('coin'); };
$('bright').oninput = (e) => { save.settings.bright = +e.target.value; applyLook(); };
$('contrast').oninput = (e) => { save.settings.contrast = +e.target.value; applyLook(); };
$('satur').oninput = (e) => { save.settings.satur = +e.target.value; applyLook(); };
$('quality').onchange = (e) => { save.settings.quality = e.target.value; applyQuality(); if (G.city) setupShadows(); };
$('btnConfigOk').onclick = () => { persist(); show('config', false); };
function show(id, on) { $(id).classList.toggle('show', on); }

// ---------------- montar o mundo ----------------
function clearWorld() {
  if (G.life) { G.life.dispose(); G.life = null; }
  if (G.city) G.city.dispose();
  if (G.truck) scene.remove(G.truck.root);
  for (const a of G.actors) scene.remove(a.root, a.shadow);
  G.city = null; G.actors = [];
}
function roofSpots(T) {
  const spots = [], zs = [T.roofZ[1] - 1.2, T.roofZ[1] - 3.6, T.roofZ[1] - 6, T.roofZ[1] - 8.4, T.roofZ[0] + 1.2];
  for (const z of zs) for (const x of [-0.75, 0.75]) spots.push(new THREE.Vector3(x, T.roofY, z));
  return spots;
}
function buildWorld(idx, night, variantId) {
  clearWorld();
  const def = CITIES[idx];
  G.cityDef = def; G.night = night; G.builtNight = night;
  G.city = new City(scene, def, night);
  G.life = new Life(scene, G.city, night);
  const variant = VARIANTS.find(v => v.id === variantId) || VARIANTS[0];
  G.truck = new Truck(assets.carreta, variant); scene.add(G.truck.root);
  G.truck.setNight(night);
  const st = G.city.start;
  G.truck.x = st.x; G.truck.z = st.z; G.truck.yaw = st.heading; G.truck.v = 0; G.truck.autoS = 0; G.truck.autoDist = 0; G.truck.inited = false;
  G.truck.place(G.city);
  const spots = roofSpots(G.truck);
  G.actors = CHARACTERS.map((d, i) => {
    const a = new Actor(d, assets, d.kid ? save.costumes[d.id] : null);
    a.spot = spots[i % spots.length];
    a.frame = 'truck'; a.pos.copy(a.spot); a.yaw = (a.spot.x > 0 ? 1 : -1) * Math.PI / 2;
    scene.add(a.root, a.shadow);
    return a;
  });
  G.player = G.actors[0];
  useBloom = save.settings.quality !== 'leve' && (night || save.settings.quality === 'alta');
  bloom.strength = night ? 0.7 : 0.3; bloom.threshold = night ? 0.93 : 0.97;
  camLight.intensity = night ? 1.1 : 0.7;
  setupShadows();
  applyLook();
}
function setupShadows() {
  const sun = G.city.sun, q = save.settings.quality;
  renderer.shadowMap.enabled = q !== 'leve';
  sun.castShadow = q !== 'leve';
  sun.shadow.mapSize.set(q === 'alta' ? 2048 : 1024, q === 'alta' ? 2048 : 1024);
  const c = sun.shadow.camera; c.left = -32; c.right = 32; c.top = 32; c.bottom = -32; c.near = 10; c.far = 600; c.updateProjectionMatrix();
  sun.shadow.bias = -0.0008; sun.shadow.normalBias = 0.04;
  G.truck.root.traverse(o => { if (o.isMesh && !o.material.transparent) o.castShadow = true; });
  for (const a of G.actors) a.root.traverse(o => { if (o.isMesh) o.castShadow = true; });
}
// o sol acompanha a câmera para a sombra ficar sempre nítida perto do jogador
const SUN_DIR = new THREE.Vector3(-0.45, 0.8, -0.35).normalize();
function updateSun() {
  if (!G.city) return;
  const sun = G.city.sun, t = camLook;
  sun.position.copy(t).addScaledVector(SUN_DIR, 300); sun.target.position.copy(t); sun.target.updateMatrixWorld();
}

// cena de fundo do menu
function menuScene(force) {
  const idx = G.cityIdx, night = G.night ?? CITIES[idx].noite;
  if (force || !G.city || G.cityDef !== CITIES[idx] || G.builtNight !== night) buildWorld(idx, night, save.truck);
  G.phase = 'menu'; G.freeze = true;
  if (music.ctx) { if (!music.playing || music.style !== STYLES[CITIES[idx].musica]) music.play(CITIES[idx].musica); music.muffle(true); }
  camInit = true;
}

function startCity(idx, night) {
  buildWorld(idx, night, save.truck);
  dance.cancel();
  const C = G.city;
  G.player = G.actors.find(a => a.def.id === save.char) || G.actors[0];
  // o jogador começa na calçada da praça inicial, do lado da carreta
  const sp = C.spawn;
  const placeWorld = (a, x, z, yaw) => { a.frame = 'world'; a.pos.set(x, C.groundAt(x, z, 50).h, z); a.yaw = yaw; a.grounded = true; };
  placeWorld(G.player, sp.x, sp.z, 0);
  G.actors.filter(a => a.kid && a !== G.player).forEach(k => placeWorld(k, sp.x + 2.2, sp.z - 0.5, 0));
  if (G.player.def.poseInicial) G.player.startPose();
  for (const a of G.actors) a.controlled = a === G.player;
  G.score = 0; G.combo = 0; G.comboT = 0; G.finished = false; G.visited = new Set([C.stops.find(s => s.inicio)]); G.stopState = null;
  C.stops.forEach(s => s.announced = false);
  G.stats = { tricks: 0, notes: 0, gold: 0, tombos: 0, best: 0, dance: [], parkour: 0, km: 0 };
  G.total = save.minutes * 60; G.timeLeft = G.total;
  G.phase = 'intro'; G.phaseT = 0; G.paused = false; G.freeze = false; G.dancing = false;
  G.party = null; G.escort = null; G.startedEscort = false; escortRound = 0; G.meter = 0; G.comboDance = false;
  ['menu', 'pause', 'results', 'picker'].forEach(s => show(s, false));
  $('hud').classList.remove('hidden');
  $('cityname').textContent = `${G.cityDef.nome} · 🎵 ${STYLES[G.cityDef.musica].nome}`;
  drawMinimapBase();
  music.play(G.cityDef.musica); music.muffle(false);
  setTimeout(() => music.sfx('horn'), 300);
  hint(G.player.def.poseInicial ? 'Mexa o joystick para o Afonso levantar e sair correndo!' : 'A cidade é toda sua! A carreta vai dar a volta: siga, suba e dance!', 4);
  camInit = true; camYaw = 0;
  updateHUD();
}

// ---------------- pontuação e eventos ----------------
function mult() { return Math.min(8, 1 + Math.floor(G.combo / 2)); }
function addScore(pts, a) { const v = Math.round(pts * mult() * (a && a !== G.player ? 0.4 : 1)); G.score += v; return v; }
function bump() { G.combo++; G.comboT = 4.5; G.stats.best = Math.max(G.stats.best, G.combo); }
// medidor de combo: acrobacias enchem; cheio => DANÇA COMBO (pontos multiplicados)
const COMBO_FULL = 1200;
function fillCombo(pts) {
  if (G.comboDance) return;
  const was = G.meter >= COMBO_FULL; G.meter = Math.min(COMBO_FULL, (G.meter || 0) + pts);
  if (!was && G.meter >= COMBO_FULL) { toast('💃 COMBO CHEIO! Toque DANÇA COMBO!', 'gold'); music.sfx('combo'); }
}
function startComboDance() {
  const p = G.player;
  if (G.meter < COMBO_FULL || G.dancing || G.phase !== 'play' || p.mode !== 'normal' || !p.grounded) return;
  G.dancing = true; G.comboDance = true; G.meter = 0; G.comboPts = 0; camInit = true;
  // quem está perto entra na dança também
  for (const a of G.actors) if (a !== p && a.worldPos(G, _w).distanceTo(p.worldPos(G, _v2)) < 14) a.comboJoin = true;
  dance.start(16); music.sfx('cheer'); hint('💃 DANÇA COMBO! Cada acerto vale o triplo!', 2.5);
}
let lastCoin = 0;
function onEvent(type, a, x, y, airTime) {
  if (G.phase === 'menu') return;
  const me = a === G.player;
  switch (type) {
    case 'levantou': music.sfx('jump'); burst(a.worldPos(G), 0xffc400); toast('Bora, Afonso! 🏃', 'small'); break;
    case 'desceu': music.sfx('land'); break;
    case 'subiu': if (me) { music.sfx('climb'); toast('No teto da carreta! +20', 'small'); addScore(20, a); } break;
    case 'escalou': if (me) { music.sfx('climb'); G.stats.parkour++; const p = addScore(x > 1.2 ? 40 : 15, a); bump(); if (x > 1.2) toast(`ESCALOU! +${p}`, 'small'); } break;
    case 'manobra': { const pts = addScore(TRICKS[x].pts, a); bump(); if (me) fillCombo(pts); G.stats.tricks++; if (me) { toast(`${TRICKS[x].nome}! +${pts}`); music.sfx('trick'); } break; }
    case 'pousou': {
      const list = x, top = y;
      if (list.length) {
        let base = list.reduce((s, t) => s + TRICKS[t].pts, 0); if (list.length > 1) base *= 1 + (list.length - 1) * 0.5;
        const pts = addScore(base, a); bump(); if (me) fillCombo(pts); G.stats.tricks += list.length;
        if (me) { toast(`${list.map(t => TRICKS[t].nome).join(' + ')}! +${pts}`, list.length > 1 ? 'gold' : ''); music.sfx(list.length > 1 ? 'combo' : 'trick'); }
      }
      if (top && ['carro', 'banco', 'quiosque', 'container', 'loja', 'caixa', 'palco', 'tubo', 'degrau'].includes(top.kind) && me && airTime > 0.25) {
        const pts = addScore(top.kind === 'banco' ? 15 : 40, a); bump(); fillCombo(pts); G.stats.parkour++; toast(`PARKOUR! +${pts}`, 'small');
      }
      if (me) music.sfx('land');
      break;
    }
    case 'rampa': if (me) { toast('VOOOU! 🚀', 'small'); music.sfx('jump'); } addScore(30, a); break;
    case 'tombo': G.combo = 0; G.stats.tombos++; if (me) { toast(x ? 'Caiu da manobra!' : 'Opa! Tombo!', 'bad'); music.sfx('crash'); G.shake = 0.4; } break;
    case 'nota': addScore(10, a); G.comboT = Math.max(G.comboT, 2.5); G.stats.notes++; if (G.now - lastCoin > 0.06) { music.sfx('coin'); lastCoin = G.now; } break;
    case 'estrelaDourada': addScore(250, a); bump(); G.stats.gold++; music.sfx('star'); toast('⭐ ESTRELA DOURADA! ⭐', 'gold'); break;
  }
  updateHUD();
}
function onDanceJudge(q, move, streak) {
  if (q === 'erro') { music.sfx('miss'); return; }
  music.sfx(q === 'perfeito' ? 'perfect' : 'good');
  const special = streak > 0 && streak % 8 === 0;
  for (const a of G.actors) if (a.mode === 'normal' && (G.comboDance ? (a === G.player || a.comboJoin) : (a.frame === 'truck' || a === G.player || a.kid))) a.danceMove(special ? 'mortal' : move);
  if (special) { toast(`SEQUÊNCIA ${streak}! MORTAL JUNTOS!`, 'gold'); music.sfx('cheer'); }
  const base = q === 'perfeito' ? 50 : q === 'bom' ? 30 : 15;
  if (G.comboDance) { const v = base * 3 * mult(); G.score += v; G.comboPts += v; } else G.score += base;
  updateHUD();
}

// ---------------- HUD ----------------
function toast(text, cls = '') {
  const box = $('toasts'); while (box.children.length > 2) box.firstChild.remove();
  const d = document.createElement('div'); d.className = 'toast ' + cls; d.textContent = text; box.appendChild(d); setTimeout(() => d.remove(), 1300);
}
let hintTimer = null;
function hint(text, secs = 2.5) { const h = $('hint'); h.textContent = text; h.classList.add('show'); clearTimeout(hintTimer); hintTimer = setTimeout(() => h.classList.remove('show'), secs * 1000); }
function updateHUD() {
  $('score').textContent = G.score.toLocaleString('pt-BR');
  const m = mult(); $('mult').textContent = `x${m}`; $('mult').classList.toggle('hidden', m < 2);
  if (G.player) $('who').textContent = G.player.name;
}
function fmtTime(s) { s = Math.max(0, Math.ceil(s)); return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`; }
const _w = new THREE.Vector3(), _v2 = new THREE.Vector3();
function updateControls() {
  const p = G.player, driving = p.mode === 'drive', busy = G.phase !== 'play' || G.dancing;
  $('trickPad').classList.toggle('hidden', driving); $('drivePad').classList.toggle('hidden', !driving);
  const ctx = $('btnCtx');
  if (driving) ctx.classList.add('hidden');
  else {
    ctx.classList.remove('hidden');
    if (p.frame === 'truck') { ctx.textContent = '⬇ DESCER'; ctx.disabled = busy || p.mode !== 'normal'; }
    else { ctx.textContent = '⬆ SUBIR'; ctx.disabled = busy || !p.canClimbTruck(G); }
    ctx.classList.toggle('pulse', !ctx.disabled && p.frame === 'world');
  }
  const drv = $('btnDrive');
  const nearCab = p.mode === 'normal' && (p.frame === 'truck' || p.worldPos(G, _w).distanceTo(G.truck.toWorld(_v2.set(0, 1, TRUCK_LEN / 2 - 2))) < 7);
  drv.textContent = driving ? '🚶 SAIR' : '🚚 DIRIGIR';
  drv.disabled = busy || !(driving || nearCab);
  $('btnHorn').classList.toggle('hidden', !driving);
  $('btnParty').classList.toggle('hidden', !partyStopAvailable());
  const full = G.meter >= COMBO_FULL;
  $('meterFill').style.width = `${Math.min(100, 100 * (G.meter || 0) / COMBO_FULL)}%`; $('meter').classList.toggle('full', full);
  $('btnCombo').classList.toggle('hidden', !(full && !driving && !G.dancing));
  $('btnRun').classList.toggle('hidden', driving);
  $('controls').style.visibility = G.dancing ? 'hidden' : 'visible';
  $('timer').textContent = '⏱ ' + fmtTime(G.timeLeft); $('timer').classList.toggle('low', G.timeLeft < 30);
}

// ---------------- minimapa ----------------
let mmBase = null;
const mmS = (v) => (v + HALF + 30) / (2 * HALF + 60) * 150;
function drawMinimapBase() {
  const c = document.createElement('canvas'); c.width = c.height = 150; const g = c.getContext('2d');
  g.fillStyle = 'rgba(40,60,40,0.9)'; g.fillRect(0, 0, 150, 150);
  if (G.cityDef.mar) { g.fillStyle = '#1f6f9a'; if (G.cityDef.mar.lado === 'sul') g.fillRect(0, mmS(HALF + 14), 150, 40); else g.fillRect(0, 0, mmS(-HALF - 14), 150); }
  for (const b of G.city.blocks) {
    g.fillStyle = b.type === 'praca' || b.type === 'inicio' ? '#ff3d7f' : b.type === 'parque' || b.type === 'parkour' || b.type === 'marco' ? '#4caf50' : '#9e9e9e';
    g.fillRect(mmS(b.x0), mmS(-b.z1), mmS(b.x1) - mmS(b.x0), mmS(-b.z0) - mmS(-b.z1));
  }
  g.font = 'bold 12px sans-serif'; g.textAlign = 'center'; g.textBaseline = 'middle';
  for (const s of G.city.stops) g.fillText(s.inicio ? '🏁' : '💃', mmS(s.block.cx), mmS(-s.block.cz));
  mmBase = c;
}
function drawMinimap() {
  const g = $('minimap').getContext('2d'); if (!mmBase) return;
  g.clearRect(0, 0, 150, 150); g.drawImage(mmBase, 0, 0);
  const T = G.truck;
  g.save(); g.translate(mmS(T.x), mmS(-T.z)); g.rotate(T.yaw); g.fillStyle = '#ffeb3b'; g.strokeStyle = '#000'; g.lineWidth = 1;
  g.beginPath(); g.moveTo(0, -7); g.lineTo(4.5, 6); g.lineTo(-4.5, 6); g.closePath(); g.fill(); g.stroke(); g.restore();
  for (const a of G.actors) { if (!a.kid || a === G.player) continue; const p = a.worldPos(G, _w); g.fillStyle = '#ffffff'; g.beginPath(); g.arc(mmS(p.x), mmS(-p.z), 2.5, 0, 7); g.fill(); }
  const p = G.player.worldPos(G, _w);
  g.fillStyle = '#00e5ff'; g.strokeStyle = '#000'; g.beginPath(); g.arc(mmS(p.x), mmS(-p.z), 4, 0, 7); g.fill(); g.stroke();
}

// ---------------- controles ----------------
const joy = { x: 0, y: 0, id: null };
const holds = { gas: false, brake: false };
let running = false;
const keys = new Set();
const joyEl = $('joy'), knob = $('knob');
function joyMove(e) {
  const r = joyEl.getBoundingClientRect(), cx = r.left + r.width / 2, cy = r.top + r.height / 2, R = r.width / 2;
  let dx = (e.clientX - cx) / R, dy = (e.clientY - cy) / R; const m = Math.hypot(dx, dy); if (m > 1) { dx /= m; dy /= m; }
  joy.x = dx; joy.y = -dy; knob.style.transform = `translate(${dx * R * 0.6}px, ${dy * R * 0.6}px)`;
}
joyEl.addEventListener('pointerdown', (e) => { e.preventDefault(); joy.id = e.pointerId; joyEl.setPointerCapture(e.pointerId); joyMove(e); });
joyEl.addEventListener('pointermove', (e) => { if (e.pointerId === joy.id) joyMove(e); });
const joyEnd = (e) => { if (e.pointerId !== joy.id) return; joy.id = null; joy.x = joy.y = 0; knob.style.transform = ''; };
joyEl.addEventListener('pointerup', joyEnd); joyEl.addEventListener('pointercancel', joyEnd);
// arrastar a tela gira a câmera
let camYaw = 0, camPitch = 0.32, camDragT = -10, drag = null;
canvas.addEventListener('pointerdown', (e) => { drag = { id: e.pointerId, x: e.clientX, y: e.clientY }; });
addEventListener('pointermove', (e) => {
  if (!drag || e.pointerId !== drag.id) return;
  const dx = e.clientX - drag.x, dy = e.clientY - drag.y; drag.x = e.clientX; drag.y = e.clientY;
  camYaw -= dx * 0.008; camPitch = clamp(camPitch + dy * 0.005, 0.08, 1.1); camDragT = G.now;
});
addEventListener('pointerup', (e) => { if (drag && e.pointerId === drag.id) drag = null; });

function act(a) {
  if (G.paused || G.phase === 'menu' || G.phase === 'loading' || G.phase === 'fim') return;
  const p = G.player;
  if (a === 'chars') { openPicker(); return; }
  if (G.dancing) return;
  if (a === 'horn') { music.sfx('horn'); return; }
  if (a === 'party') { const st = partyStopAvailable(); if (st) beginStopParty(st); return; }
  if (a === 'drive') { toggleDrive(); return; }
  if (a === 'run') { running = !running; $('btnRun').classList.toggle('on2', running); $('btnRun').innerHTML = running ? '🏃 CORRENDO' : '🚶 ANDAR'; music.sfx('click'); return; }
  if (a === 'combo') { startComboDance(); return; }
  if (p.mode === 'drive') return;
  if (p.mode === 'pose') { p.standUp(G); return; }
  switch (a) {
    case 'jump': if (p.jump()) music.sfx('jump'); p.climbIntent = true; setTimeout(() => p.climbIntent = false, 400); break;
    case 'mortal': case 'flip': case 'estrela': {
      const wasGround = p.grounded;
      if (a === 'estrela' && p.grounded) { if (p.startTrick('estrela')) { p.trick.dir = joy.x > 0.2 ? -1 : 1; music.sfx('trick'); } break; }
      if (p.startTrick(a)) music.sfx(wasGround ? 'jump' : 'trick');
      break;
    }
    case 'ctx':
      if (p.frame === 'truck') { if (p.beginDescend(G)) music.sfx('jump'); }
      else if (p.beginClimbTruck(G)) music.sfx('climb');
      break;
  }
}
function toggleDrive() {
  const p = G.player;
  if (p.mode === 'drive') { p.exitDrive(G); toast('Saiu da carreta', 'small'); music.sfx('land'); return; }
  if (p.mode !== 'normal') return;
  p.enterDrive(G); toast('🚚 Você está dirigindo!', 'gold'); music.sfx('horn');
  hint('▲ acelera, ▼ ré/freio, vire com o joystick. Pare numa praça 💃 para a FESTA!', 4);
}
document.querySelectorAll('#controls .ctl[data-act]').forEach(b => {
  b.addEventListener('pointerdown', (e) => { e.preventDefault(); e.stopPropagation(); b.classList.add('on'); act(b.dataset.act); });
  const off = () => b.classList.remove('on'); b.addEventListener('pointerup', off); b.addEventListener('pointerleave', off); b.addEventListener('pointercancel', off);
});
document.querySelectorAll('#controls .ctl[data-hold]').forEach(b => {
  const k = b.dataset.hold;
  b.addEventListener('pointerdown', (e) => { e.preventDefault(); holds[k] = true; b.classList.add('on'); });
  const off = () => { holds[k] = false; b.classList.remove('on'); }; b.addEventListener('pointerup', off); b.addEventListener('pointerleave', off); b.addEventListener('pointercancel', off);
});
$('btnChars').onclick = () => act('chars');
const KEYS = { ' ': 'jump', z: 'mortal', x: 'estrela', c: 'flip', e: 'ctx', f: 'drive', h: 'horn', g: 'party', q: 'chars', r: 'run', v: 'combo' };
const DKEYS = { ArrowLeft: 0, ArrowUp: 1, ArrowDown: 2, ArrowRight: 3 };
addEventListener('keydown', (e) => {
  if (e.repeat) return;
  if (e.key === 'Escape' || e.key === 'p') { togglePause(); return; }
  if (G.dancing && DKEYS[e.key] !== undefined) { dance.press(DKEYS[e.key]); e.preventDefault(); return; }
  keys.add(e.key.toLowerCase());
  const a = KEYS[e.key.toLowerCase()]; if (a) { act(a); e.preventDefault(); }
});
addEventListener('keyup', (e) => keys.delete(e.key.toLowerCase()));
function stickInput() {
  let x = joy.x, y = joy.y;
  if (keys.has('a') || keys.has('arrowleft')) x -= 1; if (keys.has('d') || keys.has('arrowright')) x += 1;
  if (keys.has('w') || keys.has('arrowup')) y += 1; if (keys.has('s') || keys.has('arrowdown')) y -= 1;
  const m = Math.hypot(x, y); if (m > 1) { x /= m; y /= m; }
  return { x, y, m: Math.min(1, m) };
}
PADS.forEach((p, i) => {
  const b = document.createElement('button'); b.className = 'dpad'; b.style.background = p.cor; b.innerHTML = `${p.label}<small>${p.nome}</small>`;
  b.addEventListener('pointerdown', (e) => { e.preventDefault(); dance.press(i); }); document.querySelector('.dpads').appendChild(b);
});

// ---------------- trocar personagem / fantasia ----------------
function openPicker() {
  if (G.phase !== 'play' && G.phase !== 'intro') return;
  G.paused = true; show('picker', true); buildPicker();
}
function buildPicker() {
  const el = $('pickList'); el.innerHTML = '';
  for (const a of G.actors) {
    const b = document.createElement('button'); b.className = 'ccard' + (a === G.player ? ' sel' : '');
    b.innerHTML = `<img src="${thumbs[a.def.id] || ''}"><span>${a.name}</span>`;
    b.onclick = () => { switchTo(a); buildPicker(); };
    el.appendChild(b);
  }
  const apply = () => { for (const a of G.actors) if (a.kid) a.setCostume(save.costumes[a.def.id]); setupShadows(); buildPicker(); toast('Fantasia trocada! ✨', 'small'); };
  costumeChips($('pCostA'), 'arthur', apply); costumeChips($('pCostB'), 'afonso', apply);
}
function switchTo(a) {
  if (a === G.player) return;
  const old = G.player;
  if (old.mode === 'drive') old.exitDrive(G);
  if (old.mode === 'pose') old.standUp(G);
  old.controlled = false; a.controlled = true; G.player = a; save.char = a.def.id; persist();
  if (a.mode === 'pose') a.standUp(G);
  music.sfx('click'); updateHUD(); camInit = true;
}
$('btnPickOk').onclick = () => { show('picker', false); G.paused = false; };

// ---------------- IA (os outros personagens) ----------------
function worldDirFromLocal(lx, lz) { const a = G.truck.yaw, c = Math.cos(a), s = Math.sin(a); return [lx * c + lz * s, -lx * s + lz * c]; }
function aiInput(a, dt) {
  const p = G.player, T = G.truck;
  a.ai.t += dt;
  if (a.mode !== 'normal' && a.mode !== 'stand') return null;
  const pw = p.worldPos(G, new THREE.Vector3()), aw = a.worldPos(G, new THREE.Vector3());
  if (a.comboJoin && G.comboDance) return { mx: 0, mz: 0, want: 0 };
  // 1) festa na praça: vai para o seu lugar na pista e dança
  if (G.party && a.partySpot) {
    if (a.frame === 'truck') { if (a.mode === 'normal') a.beginDescend(G); return null; }
    const sp = a.partySpot, dx = sp.x - aw.x, dz = sp.z - aw.z, d = Math.hypot(dx, dz);
    if (d < 0.4) { let dy = sp.yaw - a.yaw; while (dy > Math.PI) dy -= 2 * Math.PI; while (dy < -Math.PI) dy += 2 * Math.PI; a.yaw += dy * Math.min(1, dt * 4); return { mx: 0, mz: 0, want: 0 }; }
    if (Math.hypot(a.vel.x, a.vel.z) < 0.4 && a.grounded && a.ai.t > 0.8) { a.jump(); a.climbIntent = true; a.ai.t = 0; }
    return { mx: dx / d, mz: dz / d, want: Math.min(1, d / 1.5) };
  }
  // 2) saída: acompanha a carreta pela rua fazendo acrobacias
  if (a.escort && G.escort) {
    if (a.frame === 'truck') { if (a.mode === 'normal') a.beginDescend(G); return null; }
    const tgt = T.toWorld(a.escort, new THREE.Vector3());
    const dx = tgt.x - aw.x, dz = tgt.z - aw.z, d = Math.hypot(dx, dz);
    if (d > 35) { a.pos.set(tgt.x, G.city.groundAt(tgt.x, tgt.z, tgt.y + 3).h, tgt.z); a.vel.set(0, 0, 0); a.grounded = true; burst(a.pos, 0xffffff); return null; }
    a.ai.trickT -= dt;
    if (a.ai.trickT <= 0 && a.grounded && !a.trick && d < 4) {
      a.ai.trickT = 1.2 + Math.random() * 1.8;
      const r = Math.random();
      if (r < 0.25) { a.startTrick('estrela'); if (a.trick) a.trick.dir = Math.random() < 0.5 ? 1 : -1; }
      else { a.jump(1.15); const t1 = ['mortal', 'flip', 'pirueta', 'mortal'][Math.floor(Math.random() * 4)]; a.trick = { type: t1, t: 0, dur: TRICKS[t1].dur, ground: false }; if (r > 0.8) a.queue = 'pirueta'; }
    }
    // corre junto com a carreta (velocidade dela + correção até o lugar)
    const vx = T.vel.x + dx * 1.6, vz = T.vel.z + dz * 1.6, v = Math.hypot(vx, vz);
    if (v < 0.3) { a.yaw += (T.yaw - a.yaw) * Math.min(1, dt * 3); return { mx: 0, mz: 0, want: 0 }; }
    return { mx: vx / v, mz: vz / v, want: Math.min(1, v / (a.speedMax * a.boost)) };
  }
  const followPlayer = a.kid && p.frame === 'world' && p.mode !== 'drive' && p.mode !== 'pose' && !G.dancing && G.phase === 'play';
  if (followPlayer) {
    if (a.frame === 'truck') { if (a.ai.t > 1.2) { a.beginDescend(G); a.ai.t = 0; } return null; }
    const side = a.def.id === 'afonso' ? 1 : -1;
    const tx = pw.x - Math.sin(p.yaw) * 2.2 + Math.cos(p.yaw) * 1.6 * side, tz = pw.z - Math.cos(p.yaw) * 2.2 - Math.sin(p.yaw) * 1.6 * side;
    const dx = tx - aw.x, dz = tz - aw.z, d = Math.hypot(dx, dz);
    if (d > 45) { a.frame = 'world'; a.pos.set(tx, G.city.groundAt(tx, tz, pw.y + 2).h, tz); a.vel.set(0, 0, 0); burst(a.pos, 0xffffff); return null; }
    if (p.vel.y > 6 && a.grounded && a.ai.t > 0.3) { a.jump(); a.ai.t = 0; a.climbIntent = true; }
    if (!a.grounded && !a.trick && a.vel.y > 3 && Math.random() < 0.04) a.startTrick(['mortal', 'flip', 'pirueta'][Math.floor(Math.random() * 3)]);
    if (Math.hypot(a.vel.x, a.vel.z) < 0.5 && d > 3 && a.grounded) { a.ai.stuck = (a.ai.stuck || 0) + dt; if (a.ai.stuck > 0.6) { a.jump(); a.climbIntent = true; a.ai.stuck = 0; } } else a.ai.stuck = 0;
    return d > 1.2 ? { mx: dx / d, mz: dz / d, want: Math.min(1, d / 3) } : { mx: 0, mz: 0, want: 0 };
  }
  if (a.mode === 'pose') return null;
  // volta para a carreta / fica no seu lugar no teto
  if (a.frame === 'truck') {
    a.ai.back = 0;
    const dx = a.spot.x - a.pos.x, dz = a.spot.z - a.pos.z, d = Math.hypot(dx, dz);
    if (d < 0.35) { const want = (a.spot.x > 0 ? 1 : -1) * Math.PI / 2; a.yaw += (want - a.yaw) * Math.min(1, dt * 3); return { mx: 0, mz: 0, want: 0 }; }
    const [wx, wz] = worldDirFromLocal(dx / d, dz / d); return { mx: wx, mz: wz, want: Math.min(0.6, d) };
  }
  if (a.canClimbTruck(G)) { a.beginClimbTruck(G, a.spot); return null; }
  const l = T.toLocal(aw, new THREE.Vector3());
  const side = l.x >= 0 ? 1 : -1;
  const target = T.toWorld(new THREE.Vector3(side * (T.halfW + 1), 0, clamp(l.z, T.roofZ[0] + 1, T.roofZ[1] - 1)), new THREE.Vector3());
  const dx = target.x - aw.x, dz = target.z - aw.z, d = Math.hypot(dx, dz) || 1;
  a.ai.back = (a.ai.back || 0) + dt;
  // carreta andando: em vez de correr atrás e ficar empurrando a lateral, reaparece no teto
  if ((Math.abs(T.v) > 0.8 && a.ai.back > 1.2) || a.ai.back > 6 || d > 40) { a.ai.back = 0; a.frame = 'truck'; a.pos.copy(a.spot); a.vel.set(0, 0, 0); a.grounded = true; burst(T.toWorld(a.spot, new THREE.Vector3()), 0xff3d7f); return null; }
  if (Math.hypot(a.vel.x, a.vel.z) < 0.5 && a.grounded && a.ai.t > 0.8) { a.jump(); a.ai.t = 0; }
  return { mx: dx / d, mz: dz / d, want: 1 };
}

// ---------------- paradas nas praças ----------------
function partyStopAvailable() {
  if (G.phase !== 'play' || G.dancing || G.player.mode !== 'drive') return null;
  const T = G.truck; if (Math.abs(T.v) > 0.8) return null;
  return G.city.stops.find(s => !s.inicio && !G.visited.has(s) && Math.hypot(s.x - T.x, s.z - T.z) < 25) || null;
}
function beginStopParty(st) {
  G.visited.add(st); G.dancing = true; G.stopState = { st, t: 0, mode: 'dance' };
  gatherAt(st);
  dance.start(32); music.sfx('cheer'); hint(`💃 Todo mundo na pista da ${st.nome}!`, 2.5);
}
// todos os integrantes se reúnem no espaço de dança da praça
function gatherAt(st) {
  stopEscort(); G.party = st; camInit = true;
  if (G.life) G.life.confetti(new THREE.Vector3(st.cx, G.city.floorY(st.cx, st.cz) + 1, st.cz));
  const p = G.player;
  if (p.mode === 'drive') p.exitDrive(G);
  if (p.mode === 'pose') p.standUp(G);
  const others = G.actors.filter(a => a !== p);
  [p, ...others].forEach((a, i) => {
    const sp = st.spots[i % st.spots.length];
    a.partySpot = sp; a.boost = 1.3;
    const w = a.worldPos(G, new THREE.Vector3());
    const far = Math.hypot(w.x - sp.x, w.z - sp.z) > 28;
    if (a === p || far || a.mode !== 'normal') {
      // vai direto pro lugar (com confete)
      a.mode = 'normal'; a.trick = null; a.frame = 'world'; a.root.visible = true; a.body.rig.visible = true; if (a.fullModel) a.fullModel.visible = false;
      a.pos.set(sp.x, G.city.groundAt(sp.x, sp.z, 60).h, sp.z); a.vel.set(0, 0, 0); a.grounded = true; a.yaw = sp.yaw;
      burst(a.pos, [0xff3d7f, 0xffc400, 0x00c853, 0x2979ff][i % 4]);
    } else if (a.frame === 'truck') { a.beginDescend(G); }
  });
}
function endParty() {
  const st = G.party; G.party = null;
  for (const a of G.actors) { a.partySpot = null; a.boost = 1; }
  if (st) startEscort();
}
// SAÍDA: 5 integrantes acompanham a carreta pela rua fazendo acrobacias
let escortRound = 0;
function startEscort() {
  const pool = G.actors.filter(a => a !== G.player && !(a.kid && G.player.kid));
  const pick = [];
  for (let k = 0; k < pool.length && pick.length < 5; k++) pick.push(pool[(escortRound * 5 + k) % pool.length]);
  escortRound++;
  const T = G.truck, W = T.halfW;
  const offs = [[W + 2.3, 6], [W + 2.3, -1], [W + 2.3, -8], [W + 5.4, 2.5], [W + 5.4, -4.5]];
  pick.forEach((a, i) => { a.escort = new THREE.Vector3(offs[i][0], 0, offs[i][1]); a.boost = 1.55; a.ai.trickT = 1 + Math.random() * 2; });
  G.escort = { list: pick, until: G.now + 55 };
  if (G.life) G.life.confetti(G.truck.toWorld(new THREE.Vector3(0, G.truck.roofY + 1, 0)));
  hint('🤸 Saída da carreta! Olha a turma fazendo acrobacias na rua!', 3.5);
}
function stopEscort() {
  if (!G.escort) return;
  for (const a of G.escort.list) { a.escort = null; a.boost = 1; }
  G.escort = null;
}
function playerNearTruck() { const p = G.player; if (p.frame === 'truck' || p.mode === 'drive') return true; return p.worldPos(G, _w).distanceTo(_v2.set(G.truck.x, G.truck.y, G.truck.z)) < 32; }
function remainingStops() { return G.city.stops.filter(s => !s.inicio && !G.visited.has(s)).length; }
function autopilotSpeed() {
  const T = G.truck, C = G.city;
  const remDist = Math.max(0, C.ringLen - T.autoDist) + (T.route ? 60 : 0);
  const remT = Math.max(4, G.timeLeft - remainingStops() * 24);
  return clamp(remDist / remT, remDist > 5 ? 1.6 : 0, 10);
}
function updateTruck(dt) {
  const T = G.truck, C = G.city, p = G.player;
  if (G.phase === 'menu' || G.phase === 'fim' || G.phase === 'menuload') { T.place(C); return; }
  if (p.mode === 'drive') {
    const st = stickInput();
    let th = (holds.gas ? 1 : 0) - (holds.brake ? 1 : 0); if (!holds.gas && !holds.brake) th = st.y;
    T.drive(dt, C, { player: true, throttle: G.dancing ? 0 : th, steer: st.x });
    if (T.bumped) { music.sfx('crash'); G.shake = 0.3; }
    G.stats.km += Math.abs(T.v) * dt;
    return;
  }
  let speed = G.phase === 'play' ? autopilotSpeed() : 0;
  if (G.stopState) {
    speed = 0; G.stopState.t += dt;
    if (G.stopState.mode === 'wait' && playerNearTruck() && G.stopState.t > 1) beginStopParty(G.stopState.st);
    if (G.stopState.mode === 'wait' && G.stopState.t > 18) { G.visited.add(G.stopState.st); G.stopState = null; hint('A carreta seguiu viagem! 🚚', 2); }
  } else if (G.phase === 'play' && !T.route) {
    for (const s of C.stops) {
      if (s.inicio || G.visited.has(s)) continue;
      let ds = s.s - (T.autoS % C.ringLen); if (ds < -5) ds += C.ringLen;
      if (ds > 80) continue;
      const dist = Math.hypot(s.x - T.x, s.z - T.z);
      if (!s.announced) { s.announced = true; hint(`🎉 A carreta está chegando na ${s.nome}! Venha dançar!`, 3); music.sfx('horn'); }
      if (dist < 45) speed = Math.min(speed, Math.max(0.6, Math.sqrt(2 * 1.8 * dist)));
      if (dist < 2.2) {
        T.v = 0;
        beginStopParty(s); // todos os integrantes se reúnem na pista de dança
      }
    }
  }
  T.drive(dt, C, { auto: true, speed });
}

// ---------------- câmera ----------------
const camPos = new THREE.Vector3(), camLook = new THREE.Vector3(), tmpV = new THREE.Vector3(), tmpL = new THREE.Vector3();
let camInit = true;
function camBlocked(from, to) {
  const C = G.city, d = to.clone().sub(from), L = d.length(); d.normalize();
  for (let s = 1.5; s < L; s += 1) {
    const x = from.x + d.x * s, y = from.y + d.y * s, z = from.z + d.z * s;
    for (const c of C.near(x, z)) if (c.kind !== 'pessoa' && c.kind !== 'arvore' && c.kind !== 'poste' && x > c.x0 && x < c.x1 && z > c.z0 && z < c.z1 && y < C.topAt(c, x, z) && y > c.base) return Math.max(1.5, s - 0.8);
  }
  return L;
}
function updateCamera(dt) {
  const T = G.truck, p = G.player;
  let desired, look, k = 1 - Math.exp(-dt * 6);
  if (G.phase === 'menu' || G.phase === 'menuload') {
    const a = G.now * 0.12;
    desired = T.toWorld(tmpV.set(Math.sin(a) * 16 - 4, 6 + Math.sin(G.now * 0.3), Math.cos(a) * 16)).clone();
    look = T.toWorld(tmpL.set(0, 3.5, 0)).clone(); k = 1 - Math.exp(-dt * 3);
  } else if (G.comboDance) {
    const w = p.worldPos(G, new THREE.Vector3()), a = G.now * 0.4;
    desired = new THREE.Vector3(w.x + Math.sin(a) * 6, w.y + 2.6, w.z + Math.cos(a) * 6);
    look = new THREE.Vector3(w.x, w.y + p.H * 0.6, w.z); k = 1 - Math.exp(-dt * 3);
  } else if (G.party) {
    const st = G.party, sw = Math.sin(G.now * 0.3) * 7, y0 = G.city.floorY(st.cx, st.cz);
    const cx = st.cx + st.n[0] * 1.7, cz = st.cz + st.n[1] * 1.7;
    desired = new THREE.Vector3(cx - st.n[0] * 12 + st.t[0] * sw, y0 + 4.2, cz - st.n[1] * 12 + st.t[1] * sw);
    look = new THREE.Vector3(cx, y0 + 1.1, cz); k = 1 - Math.exp(-dt * 2.5);
  } else if (G.dancing || G.phase === 'fim') {
    const a = -Math.PI / 2 + Math.sin(G.now * 0.35) * 0.75;
    desired = T.toWorld(tmpV.set(Math.sin(a) * 11, T.roofY + 3 + Math.sin(G.now * 0.5) * 0.8, Math.cos(a) * 11 - 1)).clone();
    look = T.toWorld(tmpL.set(0, T.roofY + 1, -1.5)).clone(); k = 1 - Math.exp(-dt * 2.5);
  } else if (p.mode === 'drive') {
    if (G.now - camDragT > 1.5) { let d = T.yaw - camYaw; while (d > Math.PI) d -= 2 * Math.PI; while (d < -Math.PI) d += 2 * Math.PI; camYaw += d * Math.min(1, dt * 2.2); }
    desired = new THREE.Vector3(T.x - Math.sin(camYaw) * 22, T.y + 8 + camPitch * 6, T.z - Math.cos(camYaw) * 22);
    look = new THREE.Vector3(T.x + Math.sin(T.yaw) * 6, T.y + 3, T.z + Math.cos(T.yaw) * 6);
  } else {
    look = p.worldPos(G, new THREE.Vector3()); look.y += p.H * 0.75;
    const moving = Math.hypot(p.vel.x, p.vel.z) > 1.5;
    const pyaw = p.frame === 'truck' ? p.yaw + T.yaw : p.yaw;
    if (moving && G.now - camDragT > 1.2) { let d = pyaw - camYaw; while (d > Math.PI) d -= 2 * Math.PI; while (d < -Math.PI) d += 2 * Math.PI; camYaw += d * Math.min(1, dt * 1.2); }
    const dist = (p.kid ? 5.2 : 6.2) * (p.def.forte > 1.5 ? 1.35 : 1);
    const dir = new THREE.Vector3(-Math.sin(camYaw) * Math.cos(camPitch), Math.sin(camPitch), -Math.cos(camYaw) * Math.cos(camPitch));
    const L = camBlocked(look, look.clone().addScaledVector(dir, dist));
    desired = look.clone().addScaledVector(dir, L);
    const gy = G.city.floorY(desired.x, desired.z) + 0.6; if (desired.y < gy) desired.y = gy;
    k = 1 - Math.exp(-dt * 10);
  }
  if (camInit || !isFinite(camPos.x)) { camPos.copy(desired); camLook.copy(look); camInit = false; }
  camPos.lerp(desired, k); camLook.lerp(look, Math.min(1, k * 1.5));
  camera.position.copy(camPos);
  if (G.shake > 0) { G.shake -= dt; camera.position.x += (Math.random() - 0.5) * G.shake; camera.position.y += (Math.random() - 0.5) * G.shake; }
  camera.lookAt(camLook);
  G.camPos = camera.position;
  const off = G.dancing;
  if (off !== camera.userData.off) { camera.userData.off = off; if (off) camera.setViewOffset(innerWidth, innerHeight, innerWidth * 0.2, 0, innerWidth, innerHeight); else camera.clearViewOffset(); }
  camLight.position.copy(camera.position).add(tmpV.set(0, 4, 0)); camLight.target.position.copy(camLook);
}

// ---------------- confete ----------------
const bursts = [];
function burst(pos, color) {
  const n = 40, geo = new THREE.BufferGeometry(), p = new Float32Array(n * 3), v = [];
  for (let i = 0; i < n; i++) { p.set([pos.x, pos.y + 0.8, pos.z], i * 3); v.push(new THREE.Vector3((Math.random() - 0.5) * 6, Math.random() * 6 + 2, (Math.random() - 0.5) * 6)); }
  geo.setAttribute('position', new THREE.BufferAttribute(p, 3));
  const pts = new THREE.Points(geo, new THREE.PointsMaterial({ color, size: 0.25, transparent: true }));
  scene.add(pts); bursts.push({ pts, v, t: 0 });
}
function updateBursts(dt) {
  for (let i = bursts.length - 1; i >= 0; i--) {
    const b = bursts[i]; b.t += dt; const a = b.pts.geometry.attributes.position;
    for (let k = 0; k < b.v.length; k++) { b.v[k].y -= 12 * dt; a.setXYZ(k, a.getX(k) + b.v[k].x * dt, a.getY(k) + b.v[k].y * dt, a.getZ(k) + b.v[k].z * dt); }
    a.needsUpdate = true; b.pts.material.opacity = 1 - b.t;
    if (b.t > 1) { scene.remove(b.pts); b.pts.geometry.dispose(); b.pts.material.dispose(); bursts.splice(i, 1); }
  }
}

// ---------------- fim de jogo ----------------
function endGame() {
  if (G.phase === 'fim') return;
  const driving = G.player.mode === 'drive';
  G.phase = 'fim'; G.dancing = false; dance.cancel(); G.stopState = null; G.party = null;
  const atStart = !driving;
  const go = () => {
    const C = G.city, T = G.truck;
    if (atStart) {
      // personagem: todo mundo volta para a praça inicial
      T.x = C.start.x; T.z = C.start.z; T.yaw = C.start.heading; T.v = 0; T.inited = false; T.place(C);
      stopEscort();
      for (const a of G.actors) { a.reset(); a.controlled = a === G.player; }
      gatherAt(C.stops.find(s => s.inicio));
    } else G.player.exitDrive(G); // carreta: acaba onde está
    T.v = 0; camInit = true;
    music.sfx('cheer'); music.sfx('horn');
    setTimeout(() => showResults(atStart), 2600);
  };
  if (atStart) { $('fade').classList.add('on'); setTimeout(() => { go(); $('fade').classList.remove('on'); }, 700); }
  else go();
  toast('⏱ ACABOU O TEMPO!', 'big gold');
}
function showResults(atStart) {
  const c = G.cityDef, th = [2000, 5500, 10000], n = th.filter(t => G.score >= t).length;
  const newBest = G.score > (save.best[c.id] || 0);
  if (newBest) save.best[c.id] = G.score;
  save.stars[c.id] = Math.max(save.stars[c.id] || 0, n); persist();
  $('resTitle').textContent = atStart ? `De volta à Praça Inicial de ${c.nome}!` : `A festa acabou aqui em ${c.nome}!`;
  $('resStars').innerHTML = stars(n);
  $('resScore').textContent = G.score.toLocaleString('pt-BR') + ' pts';
  const d = G.stats.dance, lap = Math.min(100, Math.round(100 * G.truck.autoDist / G.city.ringLen));
  $('resInfo').innerHTML = `${newBest ? '🏆 <b>Novo recorde!</b><br>' : ''}Manobras: ${G.stats.tricks} · Parkour: ${G.stats.parkour} · Notas: ${G.stats.notes}/${G.city.notesTotal} · Estrelas: ${G.stats.gold}<br>
    Maior combo: ${G.stats.best} · Tombos: ${G.stats.tombos}${d.length ? ` · Danças: ${d.map(x => x.pct + '%').join(', ')}` : ''}<br>Volta da carreta: ${lap}%${G.stats.km > 10 ? ` · Você dirigiu ${Math.round(G.stats.km)} m` : ''}`;
  $('btnNext').textContent = `Próxima: ${CITIES[(G.cityIdx + 1) % CITIES.length].nome} ▶`;
  show('results', true);
}
function togglePause(force) {
  if (G.phase !== 'play' && G.phase !== 'intro') return false;
  if ($('picker').classList.contains('show')) { show('picker', false); G.paused = false; return true; }
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
$('btnNext').onclick = () => { G.cityIdx = (G.cityIdx + 1) % CITIES.length; G.night = null; startCity(G.cityIdx, CITIES[G.cityIdx].noite); };
$('btnResMenu').onclick = () => goMenu();
window.onAndroidBack = () => {
  if ($('config').classList.contains('show')) { show('config', false); return true; }
  if ($('picker').classList.contains('show')) { show('picker', false); G.paused = false; return true; }
  if (G.phase === 'menu' || G.phase === 'loading') return false;
  if (G.phase === 'fim') { goMenu(); return true; }
  togglePause(); return true;
};
window.onAppPause = () => { if (G.phase === 'play' || G.phase === 'intro') togglePause(true); else if (music.ctx) music.ctx.suspend(); };
window.onAppResume = () => { if (!G.paused && music.ctx) music.ctx.resume(); };
document.addEventListener('visibilitychange', () => { if (document.hidden) window.onAppPause(); });
function goMenu() {
  dance.cancel(); G.dancing = false;
  ['results', 'pause', 'picker'].forEach(s => show(s, false));
  $('hud').classList.add('hidden');
  menuScene(true); buildMenu(); show('menu', true);
}

// ---------------- loop ----------------
let last = performance.now(), fpsAcc = 0, fpsN = 0, lowFps = 0;
function frame(nowMs) {
  requestAnimationFrame(frame);
  const dt = Math.min(0.05, (nowMs - last) / 1000); last = nowMs;
  if (!G.city) return;
  if (G.paused) { render(); return; }
  G.now += dt;
  music.update(dt);
  G.beat = music.playing ? music.beatPos() : G.now * 2;
  if (G.phase === 'intro') { G.phaseT += dt; if (G.phaseT > 1.5) { G.phase = 'play'; toast('VAI! 🎉', 'big gold'); } }
  if (G.phase === 'play' && !G.dancing) { G.timeLeft -= dt; if (G.timeLeft <= 0) { G.timeLeft = 0; endGame(); } }
  if (G.dancing) {
    const r = dance.update();
    if (r) {
      if (G.comboDance) {
        G.dancing = false; G.comboDance = false; for (const a of G.actors) a.comboJoin = false;
        toast(`DANÇA COMBO! +${G.comboPts}`, 'big gold'); music.sfx('cheer'); updateHUD();
        return render();
      }
      G.stats.dance.push(r); G.dancing = false; G.stopState = null; endParty();
      toast(r.pct >= 85 ? `ARRASARAM! ${r.pct}%` : r.pct >= 60 ? `Muito bem! ${r.pct}%` : `Boa! ${r.pct}%`, 'gold');
      music.sfx('cheer'); music.sfx('horn'); hint('A carreta vai seguir! Bora pra rua! 🚚', 3);
    }
  }
  if (G.phase === 'play' && !G.escort && !G.party && !G.startedEscort && G.truck.v > 0.5) { G.startedEscort = true; startEscort(); }
  if (G.escort && G.now > G.escort.until) { stopEscort(); hint('A turma voltou para o teto da carreta! 🚚', 2); }
  updateTruck(dt);
  G.truck.update(dt, G.now, G.beat, music.energy, true);
  const st = stickInput();
  for (const a of G.actors) {
    let inp = null;
    if (G.phase === 'menu' || G.phase === 'menuload' || G.phase === 'fim') inp = { mx: 0, mz: 0, want: 0 };
    else if (a === G.player) {
      if (G.dancing) inp = { mx: 0, mz: 0, want: 0 };
      else {
        const fwdX = Math.sin(camYaw), fwdZ = Math.cos(camYaw), rx = -Math.cos(camYaw), rz = Math.sin(camYaw);
        const mx = fwdX * st.y + rx * st.x, mz = fwdZ * st.y + rz * st.x, m = Math.hypot(mx, mz) || 1;
        const run = running || keys.has('shift');
        a.boost = a.partySpot || a.escort ? a.boost : (run ? 1.12 : 1);
        inp = { mx: mx / m, mz: mz / m, want: st.m * (run ? 1 : 0.42) };
      }
    } else inp = aiInput(a, dt);
    a.update(dt, G, inp);
  }
  if (G.comboT > 0) { G.comboT -= dt; if (G.comboT <= 0) { G.combo = 0; updateHUD(); } }
  G.city.update(dt, G.now, G.beat, music.energy, G.truck, camera, G.stopState ? G.stopState.st : null);
  if (G.life) G.life.update(dt, G);
  updateBursts(dt);
  updateCamera(dt);
  updateSun();
  if (G.phase === 'play' || G.phase === 'intro') { updateControls(); drawMinimap(); }
  render();
  fpsAcc += dt; fpsN++;
  if (fpsAcc > 2) { const fps = fpsN / fpsAcc; fpsAcc = 0; fpsN = 0; if (fps < 24 && useBloom) { if (++lowFps >= 2) useBloom = false; } else lowFps = 0; }
}
function render() { if (useBloom) composer.render(); else renderer.render(scene, camera); }

// ---------------- início ----------------
async function boot() {
  applyQuality(); applyLook();
  try { await loadModels(); } catch (e) { $('loadtxt').textContent = 'Erro ao carregar os modelos 3D: ' + e.message; return; }
  $('loadtxt').textContent = 'Preparando a festa...';
  await new Promise(r => setTimeout(r, 30));
  try { renderThumbs(); } catch (e) { console.warn(e); }
  menuScene(true);
  show('loading', false); buildMenu(); show('menu', true);
  const first = () => { music.init(); if (!music.playing) { music.play(CITIES[G.cityIdx].musica); music.muffle(true); } removeEventListener('pointerdown', first); };
  addEventListener('pointerdown', first);
  requestAnimationFrame(frame);
}
boot();
