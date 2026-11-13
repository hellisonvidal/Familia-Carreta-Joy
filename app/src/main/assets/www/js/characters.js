import * as THREE from 'three';
import { SkinnedBody } from './skinned.js';

// =====================================================================
// Corpos articulados (braços, pernas, tronco e cabeça se mexem) feitos
// com formas simples. Os meninos usam a CABEÇA REAL (rosto dos modelos
// do Meshy) em cima desse corpo, por isso podem correr, dançar e trocar
// de roupa por fantasias. Os adultos são fantasias genéricas.
// =====================================================================

const SKIN = 0xf3c9a5;

// ---------- texturas desenhadas em canvas ----------
const texCache = new Map();
function canvasTex(key, w, h, draw, repeat = false) {
  if (texCache.has(key)) return texCache.get(key);
  const c = document.createElement('canvas'); c.width = w; c.height = h;
  draw(c.getContext('2d'), w, h);
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 4;
  if (repeat) t.wrapS = t.wrapT = THREE.RepeatWrapping;
  texCache.set(key, t); return t;
}
const hex = (n) => '#' + n.toString(16).padStart(6, '0');

function patternTex(kind, a, b) {
  return canvasTex(`p:${kind}:${a}:${b}`, 128, 128, (g, w, h) => {
    g.fillStyle = hex(a); g.fillRect(0, 0, w, h);
    g.strokeStyle = hex(b); g.fillStyle = hex(b);
    if (kind === 'listras') { for (let y = 0; y < h; y += 32) g.fillRect(0, y, w, 14); }
    else if (kind === 'xadrez') { for (let y = 0; y < h; y += 32) for (let x = 0; x < w; x += 32) if (((x + y) / 32) % 2 === 0) g.fillRect(x, y, 32, 32); }
    else if (kind === 'teia') {
      g.lineWidth = 2;
      for (let x = 0; x <= w; x += 21) { g.beginPath(); g.moveTo(x, 0); g.lineTo(x, h); g.stroke(); }
      for (let y = 10; y <= h; y += 24) { g.beginPath(); for (let x = 0; x <= w; x += 21) { g.quadraticCurveTo(x - 10, y + 6, x, y); } g.stroke(); }
    } else if (kind === 'remendo') {
      g.fillStyle = hex(b); g.fillRect(20, 60, 34, 30); g.fillRect(80, 15, 26, 24);
      g.strokeStyle = '#2a1a0a'; g.setLineDash([4, 3]); g.strokeRect(20, 60, 34, 30); g.strokeRect(80, 15, 26, 24);
    }
  }, true);
}

function emblemTex(kind) {
  return canvasTex('e:' + kind, 128, 128, (g, w, h) => {
    g.clearRect(0, 0, w, h);
    const star = (cx, cy, R, r, col) => { g.fillStyle = col; g.beginPath(); for (let i = 0; i < 10; i++) { const a = -Math.PI / 2 + i * Math.PI / 5, rr = i % 2 ? r : R; g.lineTo(cx + Math.cos(a) * rr, cy + Math.sin(a) * rr); } g.closePath(); g.fill(); };
    if (kind === 'coracao') {
      g.fillStyle = '#ffd400'; g.beginPath(); g.arc(64, 64, 60, 0, 7); g.fill();
      g.fillStyle = '#e53935'; g.beginPath(); g.moveTo(64, 100); g.bezierCurveTo(10, 60, 30, 18, 64, 44); g.bezierCurveTo(98, 18, 118, 60, 64, 100); g.fill();
    } else if (kind === 'estrela') { star(64, 66, 58, 23, '#ffffff'); }
    else if (kind === 'aranha') {
      g.fillStyle = '#111'; g.beginPath(); g.ellipse(64, 50, 12, 16, 0, 0, 7); g.ellipse(64, 80, 16, 22, 0, 0, 7); g.fill();
      g.strokeStyle = '#111'; g.lineWidth = 5;
      for (const s of [-1, 1]) for (let k = 0; k < 4; k++) { g.beginPath(); g.moveTo(64, 62 + k * 6); g.lineTo(64 + s * 40, 40 + k * 22); g.lineTo(64 + s * 52, 30 + k * 30); g.stroke(); }
    } else if (kind === 'escudo') {
      for (const [r, c] of [[62, '#d32f2f'], [48, '#f5f5f5'], [35, '#d32f2f'], [24, '#1e4fb8']]) { g.fillStyle = c; g.beginPath(); g.arc(64, 64, r, 0, 7); g.fill(); }
      star(64, 66, 22, 9, '#ffffff');
    } else if (kind === 'raio') {
      g.fillStyle = '#ffd400'; g.beginPath(); g.moveTo(70, 8); g.lineTo(30, 72); g.lineTo(60, 72); g.lineTo(48, 120); g.lineTo(98, 50); g.lineTo(66, 50); g.closePath(); g.fill();
    } else if (kind === 'joy') {
      g.fillStyle = '#ff3d7f'; g.beginPath(); g.arc(64, 64, 60, 0, 7); g.fill();
      g.fillStyle = '#fff'; g.font = 'bold 46px sans-serif'; g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillText('JOY', 64, 68);
    }
  });
}

// Rosto desenhado (para os adultos). A frente da esfera fica em u = 0.25.
function faceTex(f) {
  return canvasTex('f:' + JSON.stringify(f), 512, 256, (g, w, h) => {
    g.fillStyle = hex(f.pele); g.fillRect(0, 0, w, h);
    const cx = w * 0.25, ey = h * 0.44;
    if (f.mascara) { // máscara (cobre o rosto todo ou só os olhos)
      g.fillStyle = hex(f.mascara.cor);
      if (f.mascara.tipo === 'total') g.fillRect(0, 0, w, h);
      else if (f.mascara.tipo === 'olhos') g.fillRect(0, h * 0.28, w, h * 0.24);
      else if (f.mascara.tipo === 'capuz') { g.fillRect(0, 0, w, h * 0.42); g.fillRect(0, 0, w * 0.1, h); g.fillRect(w * 0.4, 0, w * 0.6, h); }
      else if (f.mascara.tipo === 'garras') { g.fillRect(0, 0, w, h * 0.55); g.fillStyle = hex(f.mascara.cor2 || 0x111111); g.beginPath(); g.moveTo(cx - 60, h * 0.5); g.lineTo(cx, h * 0.3); g.lineTo(cx + 60, h * 0.5); g.lineTo(cx + 55, h * 0.38); g.lineTo(cx, h * 0.2); g.lineTo(cx - 55, h * 0.38); g.fill(); }
      if (f.mascara.teia) { g.strokeStyle = '#111'; g.lineWidth = 1.5; for (let a = 0; a < 16; a++) { g.beginPath(); g.moveTo(cx, ey + 10); g.lineTo(cx + Math.cos(a / 16 * 6.28) * 300, ey + 10 + Math.sin(a / 16 * 6.28) * 200); g.stroke(); } for (let r = 20; r < 300; r += 22) { g.beginPath(); g.ellipse(cx, ey + 10, r, r * 0.7, 0, 0, 7); g.stroke(); } }
    }
    // olhos
    if (f.olhos === 'lente') { // olhos grandes brancos de máscara
      for (const s of [-1, 1]) { g.fillStyle = '#111'; g.beginPath(); g.ellipse(cx + s * 26, ey, 24, 17, s * -0.35, 0, 7); g.fill(); g.fillStyle = '#fff'; g.beginPath(); g.ellipse(cx + s * 26, ey, 20, 13, s * -0.35, 0, 7); g.fill(); }
    } else {
      for (const s of [-1, 1]) {
        g.fillStyle = '#fff'; g.beginPath(); g.ellipse(cx + s * 20, ey, f.olhosGrandes ? 15 : 10, f.olhosGrandes ? 13 : 8, 0, 0, 7); g.fill();
        g.fillStyle = '#3b2a1a'; g.beginPath(); g.arc(cx + s * 20, ey + 1, f.olhosGrandes ? 7 : 5, 0, 7); g.fill();
        g.fillStyle = '#111'; g.beginPath(); g.arc(cx + s * 20, ey + 1, 2.5, 0, 7); g.fill();
        if (!f.semSobrancelha) { g.strokeStyle = hex(f.sobrancelha || 0x3b2a1a); g.lineWidth = 5; g.beginPath(); g.moveTo(cx + s * 10, ey - 16); g.lineTo(cx + s * 32, ey - 18 + (f.bravo ? s * 0 + 6 : 0)); g.stroke(); }
      }
    }
    // boca
    if (!(f.mascara && f.mascara.tipo === 'total')) {
      const my = h * 0.66;
      if (f.boca === 'dentes') { // sorrisão
        g.fillStyle = '#fff'; g.beginPath(); g.ellipse(cx, my, 46, 22, 0, 0, Math.PI); g.fill(); g.fillRect(cx - 46, my - 4, 92, 6);
        g.strokeStyle = '#999'; g.lineWidth = 1.5; for (let x = -40; x <= 40; x += 10) { g.beginPath(); g.moveTo(cx + x, my - 4); g.lineTo(cx + x, my + 16); g.stroke(); }
        g.strokeStyle = '#2b1b10'; g.lineWidth = 3; g.beginPath(); g.ellipse(cx, my, 46, 22, 0, 0, Math.PI); g.stroke();
      } else if (f.boca === 'bravo') { g.fillStyle = '#5a2020'; g.fillRect(cx - 18, my, 36, 8); }
      else { g.strokeStyle = '#8a2e2e'; g.lineWidth = 5; g.beginPath(); g.arc(cx, my - 12, 20, 0.35, Math.PI - 0.35); g.stroke(); }
    }
    if (f.bochechas) { g.fillStyle = 'rgba(255,90,110,0.55)'; for (const s of [-1, 1]) { g.beginPath(); g.arc(cx + s * 50, h * 0.58, 22, 0, 7); g.fill(); } }
    if (f.nariz) { g.fillStyle = hex(f.nariz); g.beginPath(); g.arc(cx, h * 0.54, 8, 0, 7); g.fill(); }
    if (f.sardas) { g.fillStyle = 'rgba(150,80,40,0.5)'; for (let k = 0; k < 14; k++) g.fillRect(cx - 40 + Math.random() * 80, h * 0.52 + Math.random() * 16, 3, 3); }
  });
}

// ---------- materiais ----------
const matCache = new Map();
function mat(spec) {
  if (spec && spec.isMaterial) return spec;
  const key = typeof spec === 'object' ? JSON.stringify(spec) : String(spec);
  if (matCache.has(key)) return matCache.get(key);
  let m;
  if (typeof spec === 'number') m = new THREE.MeshStandardMaterial({ color: spec, roughness: 0.72 });
  else if (spec.padrao) m = new THREE.MeshStandardMaterial({ map: patternTex(spec.padrao, spec.a, spec.b), roughness: 0.72 });
  else m = new THREE.MeshStandardMaterial({ color: spec.cor ?? 0xffffff, roughness: spec.rough ?? 0.6, metalness: spec.metal ?? 0, emissive: spec.emissive ?? 0, emissiveIntensity: spec.ei ?? 1 });
  matCache.set(key, m); return m;
}

// ---------- fantasias / roupas ----------
// tronco, manga (curta|longa), bracos, maos, calca (longa|curta), pernas, sapatos, acessorios
export const COSTUMES = {
  arthur:   { nome: 'Roupa do Arthur', tronco: 0x1736c9, manga: 'curta', bracos: SKIN, maos: SKIN, calca: 0x0d1748, faixa: 0x1f7a52, sapatos: 0x1a1a1a, emblema: null },
  afonso:   { nome: 'Roupa do Afonso', tronco: 0x1736c9, manga: 'longa', bracos: 0xb8bcc2, maos: SKIN, calca: 0x14161c, faixa: 0x1f7a52, sapatos: 0xd8263a, sola: 0xffffff },
  barril:   { nome: 'Moleque do Barril', tronco: { padrao: 'listras', a: 0xe9dcc0, b: 0xb48a5a }, manga: 'curta', bracos: SKIN, maos: SKIN, calca: { padrao: 'remendo', a: 0x8a6a3d, b: 0x6d8a3d }, sapatos: 0x5a3b22, suspensorio: 0x6b4a2a, chapeu: 'bone_xadrez' },
  bochecha: { nome: 'Bochechudo', tronco: 0x1e88e5, manga: 'longa', bracos: 0xe53935, maos: 0xffffff, calca: 0xe53935, sapatos: 0x222222, gola: 0xffffff, emblema: 'joy' },
  vermelho: { nome: 'Herói Vermelho', tronco: 0xd81e1e, manga: 'longa', bracos: 0xd81e1e, maos: 0xd81e1e, calca: 0xd81e1e, shorts: 0xffd400, sapatos: 0xffd400, emblema: 'coracao', chapeu: 'antenas', capuz: 0xd81e1e },
  caraverde:{ nome: 'Cara Verde', tronco: 0xffc400, manga: 'longa', bracos: 0xffc400, maos: 0x7ed321, calca: 0xffc400, sapatos: 0x7a4a2a, gravata: 0xe53935, chapeu: 'chapeu_amarelo' },
  teia:     { nome: 'Herói Teia', tronco: { padrao: 'teia', a: 0xd5212b, b: 0x1a1a1a }, manga: 'longa', bracos: { padrao: 'teia', a: 0xd5212b, b: 0x1a1a1a }, maos: 0xd5212b, calca: 0x1f4fbf, sapatos: 0xd5212b, emblema: 'aranha' },
  capitao:  { nome: 'Capitão Escudo', tronco: 0x1f4fbf, manga: 'longa', bracos: 0x1f4fbf, maos: 0xd32f2f, calca: 0x1f4fbf, sapatos: 0xd32f2f, emblema: 'estrela', cinto: 0x7a5a2a, escudo: true, chapeu: 'capacete', listras: true },
  gigante:  { nome: 'Gigante Verde', tronco: 0x4caf2f, manga: 'nenhuma', bracos: 0x4caf2f, maos: 0x4caf2f, calca: 0x6a2c91, shorts: 0x6a2c91, pernasPele: 0x4caf2f, sapatos: 0x4caf2f, rasgado: true },
  garras:   { nome: 'Garras', tronco: 0xffc400, manga: 'longa', bracos: 0x1a3fa8, maos: 0x1a3fa8, calca: 0x1a3fa8, sapatos: 0x1a3fa8, cinto: 0xd32f2f, garras: true, ombreiras: 0x1a3fa8, chapeu: 'mascara_garras' },
};
export const KID_COSTUMES = ['normal', 'barril', 'bochecha', 'vermelho', 'caraverde', 'teia', 'capitao', 'gigante', 'garras'];

// ---------- personagens ----------
export const CHARACTERS = [
  { id: 'arthur', nome: 'Arthur', kid: true, altura: 1.32, cabeca: 'arthurHead', corpo: 'arthurBody', roupa: 'arthur', estilo: 0 },
  { id: 'afonso', nome: 'Afonso', kid: true, altura: 1.18, cabeca: 'afonsoHead', corpo: 'afonsoBody', roupa: 'afonso', estilo: 1, poseInicial: true },
  { id: 'barril', nome: 'Moleque do Barril', altura: 1.62, roupa: 'barril', estilo: 2, rosto: { pele: SKIN, sardas: true, olhosGrandes: true, sobrancelha: 0x5a3b22 }, cabelo: 0x8a4b2a },
  { id: 'bochecha', nome: 'Bochechudo', altura: 1.7, roupa: 'bochecha', estilo: 3, rosto: { pele: 0xffe3cf, bochechas: true, nariz: 0xff5a7a, olhosGrandes: true, boca: 'sorriso' }, bochechas: true, cabelo: 0x2a1a12, cabeloLongo: true },
  { id: 'vermelho', nome: 'Herói Vermelho', altura: 1.66, roupa: 'vermelho', estilo: 4, rosto: { pele: SKIN, mascara: { tipo: 'capuz', cor: 0xd81e1e }, olhosGrandes: true } },
  { id: 'caraverde', nome: 'Cara Verde', altura: 1.8, roupa: 'caraverde', estilo: 5, rosto: { pele: 0x7ed321, boca: 'dentes', olhosGrandes: true, sobrancelha: 0x2f5d10 }, cabecaGrande: 1.12 },
  { id: 'teia', nome: 'Herói Teia', altura: 1.78, roupa: 'teia', estilo: 6, rosto: { pele: 0xd5212b, mascara: { tipo: 'total', cor: 0xd5212b, teia: true }, olhos: 'lente' } },
  { id: 'capitao', nome: 'Capitão Escudo', altura: 1.86, forte: 1.25, roupa: 'capitao', estilo: 7, rosto: { pele: SKIN, mascara: { tipo: 'capuz', cor: 0x1f4fbf }, bravo: true } },
  { id: 'gigante', nome: 'Gigante Verde', altura: 2.35, forte: 1.9, cabecaGrande: 1.45, roupa: 'gigante', estilo: 8, rosto: { pele: 0x4caf2f, boca: 'bravo', bravo: true, sobrancelha: 0x1b3d0c }, cabelo: 0x1b1b1b },
  { id: 'garras', nome: 'Garras', altura: 1.7, forte: 1.2, roupa: 'garras', estilo: 9, rosto: { pele: SKIN, mascara: { tipo: 'garras', cor: 0xffc400, cor2: 0x111111 }, bravo: true, boca: 'bravo' } },
];

// Ajuste das cabeças reais (unidades do modelo original)
const REAL_HEADS = {
  arthurHead: { base: [0, 0.55, -0.02], altura: 0.41, alvo: 0.33, rot: [0, 0, 0] },
  afonsoHead: { base: [0, 0.535, -0.07], altura: 0.416, alvo: 0.32, rot: [0, 0, 0] },
};

// membro anatômico: mais grosso em cima, mais fino embaixo
function limb(r0, r1, len, m) {
  const g = new THREE.Group();
  const cyl = new THREE.Mesh(new THREE.CylinderGeometry(r0, r1, len, 10, 1, true).translate(0, -len / 2, 0), mat(m));
  const top = new THREE.Mesh(new THREE.SphereGeometry(r0, 10, 8), mat(m));
  const bot = new THREE.Mesh(new THREE.SphereGeometry(r1, 10, 8), mat(m)); bot.position.y = -len;
  g.add(cyl, top, bot); return g;
}
const TORSO_PROFILE = [[0.8, 0], [0.76, 0.12], [0.72, 0.3], [0.8, 0.52], [0.95, 0.72], [1.0, 0.84], [0.86, 0.95], [0.5, 1.0], [0.2, 1.01]];
function torsoGeo(forte) {
  const pts = TORSO_PROFILE.map(([r, y]) => new THREE.Vector2(r * (y > 0.5 ? 1 + (forte - 1) * 0.15 : 1), y));
  return new THREE.LatheGeometry(pts, 16);
}
function capsule(r, len, m, seg = 8) {
  const g = new THREE.CapsuleGeometry(r, Math.max(0.001, len), 4, seg);
  g.translate(0, -len / 2 - r * 0.2, 0);
  return new THREE.Mesh(g, mat(m));
}

// =====================================================================
export class Body {
  constructor(def, costumeKey, assets) {
    this.def = def;
    this.root = new THREE.Group();     // pés no chão
    this.pivot = new THREE.Group();    // giro das manobras (centro do corpo)
    this.root.add(this.pivot);
    this.assets = assets;
    this.build(costumeKey);
  }

  build(costumeKey) {
    if (this.rig) this.pivot.remove(this.rig);
    const def = this.def, H = def.altura, F = def.forte || 1;
    const kid = !!def.kid;
    const C = COSTUMES[costumeKey === 'normal' || !costumeKey ? def.roupa : costumeKey];
    this.costumeKey = costumeKey;
    // proporções
    const P = kid
      ? { hip: 0.44 * H, torso: 0.27 * H, neck: 0.05 * H, sh: 0.11 * H, uarm: 0.165 * H, farm: 0.15 * H, rl: 0.05 * H, ra: 0.034 * H, tw: 0.112 * H, td: 0.072 * H }
      : { hip: 0.5 * H, torso: 0.3 * H, neck: 0.035 * H, sh: 0.125 * H * Math.sqrt(F), uarm: 0.175 * H, farm: 0.16 * H, rl: 0.045 * H * F, ra: 0.03 * H * F, tw: 0.12 * H * F, td: 0.075 * H * F };
    P.thigh = P.hip * 0.5; P.shin = P.hip * 0.46;
    this.P = P;
    const rig = new THREE.Group(); this.rig = rig;
    this.pivot.position.y = P.hip;
    this.pivot.add(rig);
    const J = this.j = {};
    const part = (parent, name, x, y, z) => { const g = new THREE.Group(); g.position.set(x, y, z); parent.add(g); J[name] = g; return g; };

    // quadril e tronco
    const hips = part(rig, 'hips', 0, 0, 0);
    const pelvis = new THREE.Mesh(new THREE.SphereGeometry(1, 12, 8), mat(C.shorts ?? C.calca)); pelvis.scale.set(P.tw * 0.95, P.td * 1.3, P.td * 1.05); hips.add(pelvis);
    const spine = part(hips, 'spine', 0, 0.02, 0);
    const torso = new THREE.Mesh(torsoGeo(kid ? 1 : F), mat(C.tronco));
    torso.scale.set(P.tw * 1.05, P.torso * 0.97, P.td * 1.1); torso.position.y = -P.torso * 0.04; spine.add(torso);
    if (C.listras) for (let k = 0; k < 3; k++) { const ring = new THREE.Mesh(new THREE.CylinderGeometry(1, 1, 1, 14, 1, true), mat(k % 2 ? 0xffffff : 0xd32f2f)); ring.scale.set(P.tw * 0.84, P.torso * 0.07, P.td * 0.9); ring.position.y = P.torso * (0.12 + k * 0.07); spine.add(ring); }
    if (C.cinto) { const b = new THREE.Mesh(new THREE.CylinderGeometry(1, 1, 1, 14, 1, true), mat(C.cinto)); b.scale.set(P.tw * 0.87, P.torso * 0.08, P.td * 0.93); b.position.y = P.torso * 0.04; spine.add(b); }
    if (C.suspensorio) for (const s of [-1, 1]) { const st = new THREE.Mesh(new THREE.BoxGeometry(0.035 * H, P.torso * 0.85, P.td * 2.15), mat(C.suspensorio)); st.position.set(s * P.tw * 0.45, P.torso * 0.5, 0); spine.add(st); }
    if (C.gravata) { const t = new THREE.Mesh(new THREE.ConeGeometry(P.tw * 0.18, P.torso * 0.55, 4), mat(C.gravata)); t.rotation.x = Math.PI; t.position.set(0, P.torso * 0.62, P.td * 0.98); spine.add(t); }
    if (C.gola) { const t = new THREE.Mesh(new THREE.TorusGeometry(P.tw * 0.62, P.tw * 0.18, 6, 16), mat(C.gola)); t.rotation.x = Math.PI / 2; t.position.y = P.torso * 0.92; spine.add(t); }
    if (C.emblema) {
      const e = new THREE.Mesh(new THREE.CircleGeometry(P.tw * 0.5, 24), new THREE.MeshStandardMaterial({ map: emblemTex(C.emblema), transparent: true, roughness: 0.6 }));
      e.position.set(0, P.torso * 0.6, P.td * 1.12); spine.add(e);
    }
    if (C.ombreiras) for (const s of [-1, 1]) { const o = new THREE.Mesh(new THREE.SphereGeometry(P.ra * 2, 8, 6, 0, Math.PI * 2, 0, Math.PI / 2), mat(C.ombreiras)); o.position.set(s * P.sh, P.torso * 0.93, 0); o.scale.set(1.2, 0.9, 1.1); spine.add(o); }
    if (C.rasgado) { const r = new THREE.Mesh(new THREE.CylinderGeometry(1, 1.1, 1, 10, 1, true), mat(C.shorts)); r.scale.set(P.tw * 0.9, P.torso * 0.1, P.td * 0.95); r.position.y = -P.torso * 0.02; spine.add(r); }

    // pescoço e cabeça
    const neck = part(spine, 'neck', 0, P.torso * (kid ? 0.98 : 0.93) + (def.forte > 1.5 ? P.torso * 0.08 : 0), 0);
    const nm = new THREE.Mesh(new THREE.CylinderGeometry(P.tw * 0.28, P.tw * 0.32, P.neck * 2, 10), mat(def.rosto && def.rosto.mascara && def.rosto.mascara.tipo !== 'olhos' ? def.rosto.mascara.cor : (def.rosto ? def.rosto.pele : SKIN)));
    nm.position.y = P.neck * 0.6; neck.add(nm);
    if (!kid && (C.manga === 'curta' || C.manga === 'longa')) { const col = new THREE.Mesh(new THREE.TorusGeometry(P.tw * 0.32, P.tw * 0.07, 6, 14), mat(typeof C.tronco === 'number' ? C.tronco : 0xffffff)); col.rotation.x = Math.PI / 2; neck.add(col); }
    const head = part(neck, 'head', 0, P.neck * 1.2, 0);
    if (kid) this.buildRealHead(head, def, C);
    else this.buildFakeHead(head, def, C, P);

    // braços
    for (const [s, n] of [[1, 'L'], [-1, 'R']]) {
      const sh = part(spine, 'sh' + n, s * P.sh, P.torso * 0.86, 0);
      const mus = C.manga === 'nenhuma' ? 1.3 : 1;
      sh.add(limb(P.ra * 1.15 * mus, P.ra * 0.85 * mus, P.uarm, C.bracos));
      if (C.manga === 'curta') { const sl = capsule(P.ra * 1.35, P.uarm * 0.35, C.tronco); sh.add(sl); }
      const el = part(sh, 'el' + n, 0, -P.uarm, 0);
      let faMat = C.manga === 'longa' || C.manga === 'nenhuma' ? C.bracos : (def.rosto && def.rosto.pele && !kid ? def.rosto.pele : SKIN);
      el.add(limb(P.ra * 0.95 * mus, P.ra * 0.7 * mus, P.farm - P.ra * 1.6, faMat));
      // mão: palma + polegar
      const hand = new THREE.Group(); hand.position.y = -P.farm + P.ra * 0.9; el.add(hand);
      const palm = new THREE.Mesh(new THREE.SphereGeometry(P.ra * 1.05, 10, 8), mat(C.maos)); palm.scale.set(0.75, 1.3, 1.05); hand.add(palm);
      const th2 = new THREE.Mesh(new THREE.CapsuleGeometry(P.ra * 0.32, P.ra * 0.6, 3, 6), mat(C.maos)); th2.position.set(-s * P.ra * 0.2, P.ra * 0.1, P.ra * 0.75); th2.rotation.x = 0.7; hand.add(th2);
      J['hand' + n] = hand;
      if (C.garras) for (let k = -1; k <= 1; k++) { const cl = new THREE.Mesh(new THREE.BoxGeometry(0.008 * H, 0.16 * H * 0.6, 0.008 * H), mat({ cor: 0xdfe6ee, metal: 0.9, rough: 0.2 })); cl.position.set(k * P.ra * 0.6, -P.farm - 0.05 * H, P.ra * 0.6); el.add(cl); }
      if (C.escudo && n === 'L') { const shd = new THREE.Mesh(new THREE.CylinderGeometry(0.2 * H * 0.75, 0.2 * H * 0.75, 0.03, 28), [mat(0xd32f2f), new THREE.MeshStandardMaterial({ map: emblemTex('escudo'), metalness: 0.4, roughness: 0.35 }), mat(0xd32f2f)]); shd.rotation.z = Math.PI / 2; shd.position.set(P.ra * 2.2, -P.farm * 0.5, 0); el.add(shd); }
    }
    // pernas
    for (const [s, n] of [[1, 'L'], [-1, 'R']]) {
      const th = part(hips, 'th' + n, s * P.tw * 0.5, -P.td * 0.3, 0);
      const thighMat = C.shorts && !C.calcaLonga ? C.shorts : C.calca;
      th.add(limb(P.rl * 1.15, P.rl * 0.82, P.thigh, thighMat));
      if (C.faixa) { const f = new THREE.Mesh(new THREE.BoxGeometry(0.012 * H, P.thigh * 0.9, P.rl * 0.6), mat(C.faixa)); f.position.set(s * P.rl * 1.0, -P.thigh * 0.5, 0); th.add(f); }
      const kn = part(th, 'kn' + n, 0, -P.thigh, 0);
      const shinMat = C.pernasPele ?? (C.shorts && !C.calcaLonga && C.shorts !== C.calca ? C.calca : C.calca);
      kn.add(limb(P.rl * 0.85, P.rl * 0.6, P.shin - P.rl * 0.5, shinMat));
      if (C.faixa) { const f = new THREE.Mesh(new THREE.BoxGeometry(0.012 * H, P.shin * 0.85, P.rl * 0.5), mat(C.faixa)); f.position.set(s * P.rl * 0.85, -P.shin * 0.5, 0); kn.add(f); }
      const foot = new THREE.Mesh(new THREE.CapsuleGeometry(P.rl * 0.85, P.rl * 1.6, 4, 8), mat(C.sapatos));
      foot.rotation.x = Math.PI / 2; foot.scale.set(1, 1, 0.75); foot.position.set(0, -P.shin - P.rl * 0.35, P.rl * 0.8); kn.add(foot);
      if (C.sola) { const so = new THREE.Mesh(new THREE.BoxGeometry(P.rl * 1.7, P.rl * 0.35, P.rl * 3.6), mat(C.sola)); so.position.set(0, -P.shin - P.rl * 1.05, P.rl * 0.8); kn.add(so); }
    }
    rig.traverse(o => { if (o.isMesh) { o.castShadow = false; o.frustumCulled = false; } });
    this.feetY = P.hip; // altura do quadril
  }

  buildRealHead(head, def, C) {
    const cfg = REAL_HEADS[def.cabeca];
    const src = this.assets[def.cabeca];
    const s = cfg.alvo / cfg.altura;
    const holder = new THREE.Group(); holder.scale.setScalar(s); holder.rotation.set(...cfg.rot);
    const m = src.clone(true);
    m.position.set(-cfg.base[0], -cfg.base[1], -cfg.base[2]);
    holder.add(m); head.add(holder);
    // acessórios de cabeça que deixam o rosto à mostra
    this.addHeadwear(head, C.chapeu, cfg.alvo * 0.55, true);
  }

  buildFakeHead(head, def, C, P) {
    const R = 0.068 * def.altura * (def.cabecaGrande || 1);
    const face = new THREE.Mesh(new THREE.SphereGeometry(R, 24, 16), new THREE.MeshStandardMaterial({ map: faceTex(def.rosto), roughness: 0.65 }));
    face.scale.set(0.95, 1.08, 1); face.position.y = R * 1.05; head.add(face);
    if (!(def.rosto.mascara && def.rosto.mascara.tipo === 'total')) {
      const nose = new THREE.Mesh(new THREE.SphereGeometry(R * 0.16, 8, 6), mat(def.rosto.nariz ?? def.rosto.pele)); nose.scale.set(0.8, 1, 1.1); nose.position.set(0, R * 1.0, R * 0.97); head.add(nose);
    }
    for (const s2 of [-1, 1]) { const ear = new THREE.Mesh(new THREE.SphereGeometry(R * 0.22, 8, 6), mat(def.rosto.mascara && def.rosto.mascara.tipo !== 'olhos' ? def.rosto.mascara.cor : def.rosto.pele)); ear.scale.set(0.5, 1, 0.8); ear.position.set(s2 * R * 0.93, R * 1.05, 0); head.add(ear); }
    if (def.bochechas) for (const s of [-1, 1]) { const b = new THREE.Mesh(new THREE.SphereGeometry(R * 0.55, 14, 10), mat(def.rosto.pele)); b.position.set(s * R * 0.62, R * 0.72, R * 0.55); head.add(b); }
    if (def.cabelo) {
      const hair = new THREE.Mesh(new THREE.SphereGeometry(R * 1.04, 16, 10, 0, Math.PI * 2, 0, Math.PI * 0.55), mat(def.cabelo));
      hair.position.y = R * 1.08; hair.rotation.x = -0.25; head.add(hair);
      if (def.cabeloLongo) { const back = new THREE.Mesh(new THREE.CapsuleGeometry(R * 0.9, R * 0.9, 4, 10), mat(def.cabelo)); back.position.set(0, R * 0.5, -R * 0.45); back.scale.set(1.15, 1, 0.6); head.add(back); }
    }
    const m = def.rosto.mascara;
    if (m && (m.tipo === 'capuz' || m.tipo === 'garras')) {
      const hood = new THREE.Mesh(new THREE.SphereGeometry(R * 1.06, 16, 10, Math.PI * 0.9, Math.PI * 1.2, 0, Math.PI * 0.62), mat(m.cor));
      hood.position.y = R * 1.08; head.add(hood);
    }
    this.addHeadwear(head, C.chapeu, R * 2.1, false);
  }

  addHeadwear(head, kind, top, kid) {
    if (!kind) return;
    const H = top; // altura aproximada do topo da cabeça
    if (kind === 'antenas') {
      for (const s of [-1, 1]) {
        const a = new THREE.Mesh(new THREE.CylinderGeometry(0.008, 0.008, H * 0.55, 5), mat(0xffd400)); a.position.set(s * H * 0.18, H * 1.1, 0); a.rotation.z = -s * 0.35; head.add(a);
        const b = new THREE.Mesh(new THREE.SphereGeometry(H * 0.07, 8, 6), mat(0xd81e1e)); b.position.set(s * H * 0.28, H * 1.36, 0); head.add(b);
      }
      if (kid) { const band = new THREE.Mesh(new THREE.TorusGeometry(H * 0.42, H * 0.04, 6, 18), mat(0xd81e1e)); band.rotation.x = Math.PI / 2 - 0.3; band.position.y = H * 0.85; head.add(band); }
    } else if (kind === 'bone_xadrez') {
      const cap = new THREE.Mesh(new THREE.SphereGeometry(H * 0.5, 16, 8, 0, Math.PI * 2, 0, Math.PI / 2), new THREE.MeshStandardMaterial({ map: patternTex('xadrez', 0x6b8e4e, 0x8a5a2a), roughness: 0.8 }));
      cap.position.y = H * (kid ? 0.82 : 0.78); cap.scale.set(1.08, 0.75, 1.1); head.add(cap);
      for (const s of [-1, 1]) { const f = new THREE.Mesh(new THREE.BoxGeometry(H * 0.08, H * 0.28, H * 0.32), cap.material); f.position.set(s * H * 0.5, H * 0.66, -H * 0.02); head.add(f); }
    } else if (kind === 'chapeu_amarelo') {
      const brim = new THREE.Mesh(new THREE.CylinderGeometry(H * 0.75, H * 0.75, H * 0.04, 20), mat(0xffc400)); brim.position.y = H * 0.92; head.add(brim);
      const crown = new THREE.Mesh(new THREE.CylinderGeometry(H * 0.42, H * 0.47, H * 0.42, 20), mat(0xffc400)); crown.position.y = H * 1.12; head.add(crown);
      const band = new THREE.Mesh(new THREE.CylinderGeometry(H * 0.475, H * 0.475, H * 0.1, 20), mat(0x3b2a1a)); band.position.y = H * 0.98; head.add(band);
    } else if (kind === 'capacete') {
      const helm = new THREE.Mesh(new THREE.SphereGeometry(H * 0.53, 18, 10, Math.PI * 0.82, Math.PI * 1.36, 0, Math.PI * 0.55), mat(0x1f4fbf)); helm.position.y = H * 0.58; head.add(helm);
      const star = new THREE.Mesh(new THREE.CircleGeometry(H * 0.13, 5), mat(0xffffff)); star.position.set(0, H * 0.98, H * 0.27); star.rotation.x = -0.6; head.add(star);
      for (const s of [-1, 1]) { const w = new THREE.Mesh(new THREE.ConeGeometry(H * 0.06, H * 0.22, 4), mat(0xffffff)); w.position.set(s * H * 0.5, H * 0.8, 0); w.rotation.z = -s * 1.1; head.add(w); }
    } else if (kind === 'mascara_garras') {
      if (kid) { // tiara com "orelhas" pontudas (rosto à mostra)
        for (const s of [-1, 1]) { const e = new THREE.Mesh(new THREE.ConeGeometry(H * 0.1, H * 0.45, 4), mat(0x111111)); e.position.set(s * H * 0.32, H * 1.1, -H * 0.05); e.rotation.z = -s * 0.5; head.add(e); }
        const band = new THREE.Mesh(new THREE.TorusGeometry(H * 0.42, H * 0.05, 6, 18), mat(0xffc400)); band.rotation.x = Math.PI / 2 - 0.25; band.position.y = H * 0.82; head.add(band);
      } else for (const s of [-1, 1]) { const e = new THREE.Mesh(new THREE.ConeGeometry(H * 0.08, H * 0.4, 4), mat(0x111111)); e.position.set(s * H * 0.3, H * 0.98, -H * 0.02); e.rotation.z = -s * 0.5; head.add(e); }
    }
  }

  // ---------------- animação ----------------
  // pose = { lean, twist, sway, bob, sh: [Lx,Lz,Rx,Rz], el: [L,R], th: [Lx,Lz,Rx,Rz], kn: [L,R], head: [x,y] }
  apply(p) {
    const J = this.j;
    J.hips.position.y = p.bob || 0;
    J.hips.rotation.set(0, p.twist || 0, p.sway || 0);
    J.spine.rotation.set(p.lean || 0, (p.spineTwist || 0), p.spineSway || 0);
    J.head.rotation.set(p.head ? p.head[0] : 0, p.head ? p.head[1] : 0, p.head ? p.head[2] || 0 : 0);
    J.shL.rotation.set(p.sh[0], 0, p.sh[1]); J.shR.rotation.set(p.sh[2], 0, p.sh[3]);
    J.elL.rotation.set(p.el[0], 0, 0); J.elR.rotation.set(p.el[1], 0, 0);
    J.thL.rotation.set(p.th[0], 0, p.th[1]); J.thR.rotation.set(p.th[2], 0, p.th[3]);
    J.knL.rotation.set(p.kn[0], 0, 0); J.knR.rotation.set(p.kn[1], 0, 0);
  }
  setCostume(key) { this.build(key); }
}

// ---------------- poses ----------------
export const lerpPose = (a, b, t) => {
  const o = {};
  for (const k of ['lean', 'twist', 'sway', 'bob', 'spineTwist', 'spineSway']) o[k] = (a[k] || 0) + ((b[k] || 0) - (a[k] || 0)) * t;
  for (const k of ['sh', 'el', 'th', 'kn', 'head']) {
    const A = a[k] || [0, 0, 0, 0], B = b[k] || [0, 0, 0, 0];
    o[k] = A.map((v, i) => v + ((B[i] || 0) - v) * t);
  }
  return o;
};

export const POSES = {
  idle(t) {
    const b = Math.sin(t * 2) * 0.03;
    return { lean: 0.02, sh: [b, 0.12, -b, -0.12], el: [-0.2, -0.2], th: [0, 0.03, 0, -0.03], kn: [0.02, 0.02], head: [0, 0] };
  },
  run(ph, a, H) {
    const s = Math.sin(ph), c = Math.cos(ph);
    return {
      lean: 0.18 * a, bob: Math.abs(s) * 0.04 * a * H, twist: s * 0.15 * a,
      sh: [s * 1.0 * a, 0.12, -s * 1.0 * a, -0.12], el: [-0.9 - 0.4 * a, -0.9 - 0.4 * a],
      th: [-s * 0.95 * a, 0, s * 0.95 * a, 0], kn: [Math.max(0, c) * 1.5 * a + 0.1, Math.max(0, -c) * 1.5 * a + 0.1], head: [-0.1 * a, 0],
    };
  },
  air(vy, tuck) {
    const up = vy > 0 ? 1 : 0;
    const p = { lean: 0.1, sh: [-0.4 - up * 0.6, 0.7, -0.4 - up * 0.6, -0.7], el: [-0.6, -0.6], th: [-0.7, 0.05, -0.2, -0.05], kn: [1.2, 0.6], head: [0, 0] };
    if (tuck > 0) return lerpPose(p, { lean: 0.5, sh: [-1.2, 0.3, -1.2, -0.3], el: [-2.0, -2.0], th: [-2.0, 0.1, -2.0, -0.1], kn: [2.4, 2.4], head: [0.3, 0] }, tuck);
    return p;
  },
  star() { // estrela: braços e pernas abertos
    return { sh: [0, 2.6, 0, -2.6], el: [0, 0], th: [0, 0.6, 0, -0.6], kn: [0, 0], head: [0, 0] };
  },
  climb(t) {
    const s = Math.sin(t * 9);
    return { lean: -0.1, sh: [-2.6 + s * 0.4, 0.2, -2.6 - s * 0.4, -0.2], el: [-0.4, -0.4], th: [-0.9 - s * 0.5, 0.1, -0.9 + s * 0.5, -0.1], kn: [1.4, 1.4], head: [-0.2, 0] };
  },
  crouchStart() { // pose característica do Afonso (agachado, mão no chão, braço pra cima)
    return {
      lean: 0.55, bob: -0.36, twist: 0.25, sway: 0.08,
      sh: [-0.9, 0.35, 0.6, -2.5], el: [0, -0.4],
      th: [-1.9, 0.25, -0.2, -0.95], kn: [2.3, 1.7], head: [-0.45, -0.2, 0.15],
    };
  },
  sit() { return { lean: -0.1, sh: [-0.5, 0.1, -0.5, -0.1], el: [-0.8, -0.8], th: [-1.5, 0.1, -1.5, -0.1], kn: [1.5, 1.5], head: [0, 0] }; },
  // estilos de dança (cada personagem tem um jeito)
  dance(beat, style, move, mk) {
    const ph = beat * Math.PI, s = Math.sin(ph), s2 = Math.sin(ph * 2), b = Math.abs(Math.sin(ph));
    let p;
    switch (style % 6) {
      case 0: p = { bob: -b * 0.06, sway: s * 0.12, sh: [-0.4 + s * 0.5, 0.4, -0.4 - s * 0.5, -0.4], el: [-1.4, -1.4], th: [0, 0.1, 0, -0.1], kn: [b * 0.4, b * 0.4], head: [0, s * 0.3] }; break;
      case 1: p = { bob: -b * 0.08, twist: s * 0.3, sh: [-2.6 * (s > 0 ? 1 : 0.5), 0.5, -2.6 * (s > 0 ? 0.5 : 1), -0.5], el: [-0.3, -0.3], th: [-0.1, 0.15, -0.1, -0.15], kn: [b * 0.5, b * 0.5], head: [-0.2, 0] }; break;
      case 2: p = { bob: -b * 0.05, sway: s2 * 0.18, spineSway: -s2 * 0.15, sh: [0, 1.2 + s2 * 0.6, 0, -1.2 + s2 * 0.6], el: [-0.5, -0.5], th: [0, 0.1, 0, -0.1], kn: [0.3, 0.3], head: [0, 0, s2 * 0.2] }; break;
      case 3: p = { bob: -b * 0.1, lean: 0.2, sh: [-1.0 + s * 0.8, 0.3, -1.0 - s * 0.8, -0.3], el: [-1.6, -1.6], th: [-0.3 * b, 0.2, -0.3 * (1 - b), -0.2], kn: [0.6 * b, 0.6 * (1 - b)], head: [0.1, 0] }; break;
      case 4: p = { bob: -b * 0.07, twist: s2 * 0.4, sh: [-1.6, 1.0, -0.3, -0.3], el: [-0.6 + s * 0.6, -1.2], th: [0, 0.1, 0, -0.1], kn: [0.3, 0.3], head: [0, -s2 * 0.3] }; break;
      default: p = { bob: -b * 0.12, sh: [-3.0, 0.3 + s * 0.3, -3.0, -0.3 + s * 0.3], el: [-0.2, -0.2], th: [0, 0.2, 0, -0.2], kn: [b * 0.6, b * 0.6], head: [-0.3, 0] };
    }
    if (move) {
      const k = Math.sin(mk * Math.PI);
      let m;
      if (move === 'esq') m = { sway: 0.3, sh: [0, 2.0, 0, -0.3], el: [-0.3, -1.0], th: [0, 0.5, 0, -0.1], kn: [0.2, 0.4] };
      else if (move === 'cima') m = { sh: [-3.0, 0.2, -3.0, -0.2], el: [0, 0], th: [-0.5, 0, -0.5, 0], kn: [1.0, 1.0] };
      else if (move === 'baixo') m = { bob: -0.25, lean: 0.25, sh: [-0.8, 0.6, -0.8, -0.6], el: [-1.2, -1.2], th: [-1.2, 0.4, -1.2, -0.4], kn: [1.9, 1.9] };
      else m = POSES.star();
      if (!m.head) m.head = [0, 0];
      p = lerpPose(p, { ...p, ...m }, k);
    }
    return p;
  },
};

// Corpo real (modelo do Meshy com esqueleto automático) quando é a roupa normal dos meninos;
// corpo articulado com a cabeça real quando é fantasia.
export function makeBody(def, costume, assets) {
  if (def.kid && (!costume || costume === 'normal') && def.corpo && assets[def.corpo]) return new SkinnedBody(assets[def.corpo], def.corpo, def.altura);
  return new Body(def, costume, assets);
}
