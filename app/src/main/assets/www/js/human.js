import * as THREE from 'three';
import { makeBones, applyPose } from './skinned.js';

// =====================================================================
// Corpo humano "de gente de verdade" para os personagens adultos:
// uma única malha contínua (tronco, pescoço, cabeça, braços com mãos,
// pernas) com esqueleto, proporções reais e um "atlas" pintado com o
// rosto (olhos, íris, sobrancelhas, nariz, lábios) e a roupa/fantasia.
// =====================================================================
const SEG = 16;
export const AT = { // regiões do atlas (frações do canvas 512x1024)
  head: [0, 0, 1, 0.25], torso: [0, 0.25, 1, 0.5625], arm: [0, 0.5625, 0.5, 1], leg: [0.5, 0.5625, 1, 1],
};
const W = 512, Hc = 1024;
const lerp = (a, b, t) => a + (b - a) * t;
const ss = (a, b, x) => { const t = Math.max(0, Math.min(1, (x - a) / (b - a))); return t * t * (3 - 2 * t); };
const hex = (n) => '#' + (n >>> 0).toString(16).padStart(6, '0');
function interp(prof, d) { // perfil [[d, a, b...]] -> valores interpolados
  if (d <= prof[0][0]) return prof[0].slice(1);
  for (let i = 1; i < prof.length; i++) if (d <= prof[i][0]) { const k = (d - prof[i - 1][0]) / (prof[i][0] - prof[i - 1][0]); return prof[i].slice(1).map((v, j) => lerp(prof[i - 1][j + 1], v, k)); }
  return prof[prof.length - 1].slice(1);
}

class Builder {
  constructor() { this.pos = []; this.uv = []; this.si = []; this.sw = []; this.idx = []; this.col = []; }
  vert(p, u, v, w) {
    this.pos.push(p[0], p[1], p[2]); this.uv.push(u, 1 - v);
    const e = Object.entries(w).sort((a, b) => b[1] - a[1]).slice(0, 4), tot = e.reduce((a, x) => a + x[1], 0) || 1;
    for (let k = 0; k < 4; k++) { this.si.push(e[k] ? this.bi[e[k][0]] : 0); this.sw.push(e[k] ? e[k][1] / tot : 0); }
    return this.pos.length / 3 - 1;
  }
  // tubo por anéis: ring(i) => {c:[x,y,z], rx, rz, t, w, v}
  tube(n, ring, region, capStart, capEnd) {
    const [u0, v0, u1, v1] = region, base = this.pos.length / 3;
    for (let i = 0; i < n; i++) {
      const R = ring(i);
      for (let k = 0; k <= SEG; k++) {
        const th = -Math.PI + 2 * Math.PI * k / SEG;
        const rx = R.rx * (R.shape ? R.shape(th) : 1), rz = R.rz * (R.shape ? R.shape(th) : 1);
        this.vert([R.c[0] + Math.sin(th) * rx, R.c[1], R.c[2] + Math.cos(th) * rz], lerp(u0, u1, k / SEG), lerp(v0, v1, Math.min(0.995, Math.max(0.005, R.v))), R.w);
      }
    }
    for (let i = 0; i < n - 1; i++) for (let k = 0; k < SEG; k++) {
      const a = base + i * (SEG + 1) + k, b = a + 1, c = a + SEG + 1, d = c + 1;
      if (ring(0).c[1] > ring(n - 1).c[1]) this.idx.push(a, c, b, b, c, d); else this.idx.push(a, b, c, b, d, c);
    }
    const cap = (i, flip) => {
      const R = ring(i), ci = this.vert(R.c, lerp(u0, u1, 0.5), lerp(v0, v1, Math.min(0.995, Math.max(0.005, R.v))), R.w);
      for (let k = 0; k < SEG; k++) { const a = base + i * (SEG + 1) + k; flip ? this.idx.push(ci, a + 1, a) : this.idx.push(ci, a, a + 1); }
    };
    const down = ring(0).c[1] > ring(n - 1).c[1];
    if (capStart) cap(0, !down);
    if (capEnd) cap(n - 1, down);
  }
  geometry() {
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(this.pos, 3));
    g.setAttribute('uv', new THREE.Float32BufferAttribute(this.uv, 2));
    g.setAttribute('skinIndex', new THREE.Uint16BufferAttribute(this.si, 4));
    g.setAttribute('skinWeight', new THREE.Float32BufferAttribute(this.sw, 4));
    g.setIndex(this.idx); g.computeVertexNormals();
    return g;
  }
}

// ---------------- pintura do atlas (rosto + roupa) ----------------
function fillPattern(g, spec, x, y, w, h, tools) {
  if (typeof spec === 'number') { g.fillStyle = hex(spec); g.fillRect(x, y, w, h); return; }
  if (spec && spec.padrao) {
    const t = tools.patternTex(spec.padrao, spec.a, spec.b);
    const pat = g.createPattern(t.image, 'repeat'); g.save(); g.translate(x, y); g.fillStyle = pat; g.fillRect(0, 0, w, h); g.restore(); return;
  }
  g.fillStyle = '#888'; g.fillRect(x, y, w, h);
}
function noise(g, x, y, w, h, n, a) { for (let i = 0; i < n; i++) { g.fillStyle = Math.random() < 0.5 ? `rgba(0,0,0,${a})` : `rgba(255,255,255,${a})`; g.fillRect(x + Math.random() * w, y + Math.random() * h, 2, 2); } }

export function paintAtlas(def, C, tools) {
  const c = document.createElement('canvas'); c.width = W; c.height = Hc;
  const g = c.getContext('2d');
  const F = def.rosto || {}, skin = def.pele ?? F.pele ?? 0xe8b896;
  const rg = (r) => [r[0] * W, r[1] * Hc, (r[2] - r[0]) * W, (r[3] - r[1]) * Hc];
  // ---- cabeça ----
  {
    const [x, y, w, h] = rg(AT.head);
    const grd = g.createLinearGradient(0, y, 0, y + h); const sk = new THREE.Color(skin);
    grd.addColorStop(0, hex(sk.clone().multiplyScalar(0.95).getHex())); grd.addColorStop(0.7, hex(skin)); grd.addColorStop(1, hex(sk.clone().multiplyScalar(0.85).getHex()));
    g.fillStyle = grd; g.fillRect(x, y, w, h);
    noise(g, x, y, w, h, 1500, 0.025);
    const cx = x + w / 2, Y = (t) => y + h * (1 - t), U = (du) => cx + du * w;
    if (def.barba) { g.fillStyle = 'rgba(60,40,30,0.35)'; g.beginPath(); g.ellipse(cx, Y(0.12), w * 0.11, h * 0.16, 0, 0, 7); g.fill(); }
    // olhos
    const eyeY = Y(0.52), dx = w * 0.062, iris = hex(def.olho ?? 0x5b3a1e);
    const m = F.mascara;
    const maskAll = m && m.tipo === 'total';
    if (m) {
      g.fillStyle = hex(m.cor);
      if (m.tipo === 'total') g.fillRect(x, y, w, h);
      else if (m.tipo === 'capuz') { g.fillRect(x, y, w, h); g.fillStyle = hex(skin); g.beginPath(); g.ellipse(cx, Y(0.36), w * 0.12, h * 0.32, 0, 0, 7); g.fill(); }
      else if (m.tipo === 'garras') { g.fillRect(x, y, w, h * 0.55); g.fillStyle = hex(m.cor2 ?? 0x111111); g.beginPath(); g.moveTo(cx - w * 0.12, Y(0.48)); g.lineTo(cx, Y(0.62)); g.lineTo(cx + w * 0.12, Y(0.48)); g.lineTo(cx + w * 0.1, Y(0.56)); g.lineTo(cx, Y(0.72)); g.lineTo(cx - w * 0.1, Y(0.56)); g.fill(); }
      else if (m.tipo === 'olhos') g.fillRect(x, Y(0.6), w, h * 0.18);
      if (m.teia) { g.strokeStyle = '#111'; g.lineWidth = 1.3; for (let a = 0; a < 18; a++) { g.beginPath(); g.moveTo(cx, eyeY); g.lineTo(cx + Math.cos(a / 18 * 6.28) * w, eyeY + Math.sin(a / 18 * 6.28) * h); g.stroke(); } for (let r = 12; r < w; r += 14) { g.beginPath(); g.ellipse(cx, eyeY, r, r * 0.6, 0, 0, 7); g.stroke(); } }
    }
    if (!tools.noFace) {
    if (F.olhos === 'lente') {
      for (const s of [-1, 1]) { g.fillStyle = '#111'; g.beginPath(); g.ellipse(U(s * 0.05), eyeY, 15, 11, s * -0.4, 0, 7); g.fill(); g.fillStyle = '#fff'; g.beginPath(); g.ellipse(U(s * 0.05), eyeY, 12, 8, s * -0.4, 0, 7); g.fill(); }
    } else {
      for (const s of [-1, 1]) {
        const ex = cx + s * dx;
        g.fillStyle = 'rgba(120,70,50,0.22)'; g.beginPath(); g.ellipse(ex, eyeY + 3, 19, 11, 0, 0, 7); g.fill(); // sombra do olho
        g.fillStyle = '#fbfbf6'; g.beginPath(); g.ellipse(ex, eyeY, 15, 8, 0, 0, 7); g.fill();
        g.fillStyle = iris; g.beginPath(); g.arc(ex, eyeY + 0.5, 7, 0, 7); g.fill();
        g.fillStyle = '#0d0d0d'; g.beginPath(); g.arc(ex, eyeY + 0.5, 3.4, 0, 7); g.fill();
        g.fillStyle = '#fff'; g.beginPath(); g.arc(ex + 2.2, eyeY - 2.2, 1.7, 0, 7); g.fill();
        g.strokeStyle = '#24160f'; g.lineWidth = 3; g.beginPath(); g.ellipse(ex, eyeY + 1.5, 15.5, 9, 0, Math.PI * 1.05, Math.PI * 1.95); g.stroke(); // pálpebra/cílios
        g.strokeStyle = 'rgba(80,40,30,0.35)'; g.lineWidth = 1.5; g.beginPath(); g.ellipse(ex, eyeY + 1, 15, 8.5, 0, Math.PI * 0.15, Math.PI * 0.85); g.stroke();
        if (!maskAll) { g.strokeStyle = hex(F.sobrancelha ?? def.cabelo ?? 0x3b2a1a); g.lineWidth = 5; g.lineCap = 'round'; g.beginPath(); g.moveTo(ex - 14 * s, eyeY - 16 + (F.bravo ? -4 : 0)); g.quadraticCurveTo(ex, eyeY - 23, ex + 15 * s, eyeY - 16 + (F.bravo ? 4 : 1)); g.stroke(); }
      }
    }
    if (!maskAll) {
      // nariz (sombra) e boca
      g.fillStyle = 'rgba(110,60,40,0.2)'; g.beginPath(); g.ellipse(cx, Y(0.34), 10, 6, 0, 0, 7); g.fill();
      g.fillStyle = 'rgba(80,40,30,0.5)'; g.beginPath(); g.ellipse(cx - 5, Y(0.32), 2.6, 1.8, 0, 0, 7); g.ellipse(cx + 5, Y(0.32), 2.6, 1.8, 0, 0, 7); g.fill();
      const my = Y(0.2);
      if (F.boca === 'dentes') {
        g.fillStyle = '#7a1f2a'; g.beginPath(); g.ellipse(cx, my, 32, 14, 0, 0, Math.PI); g.fill();
        g.fillStyle = '#fff'; g.fillRect(cx - 29, my - 1, 58, 8);
        g.strokeStyle = '#bbb'; g.lineWidth = 1; for (let k = -27; k <= 27; k += 7) { g.beginPath(); g.moveTo(cx + k, my - 1); g.lineTo(cx + k, my + 7); g.stroke(); }
      } else if (F.boca === 'bravo') { g.fillStyle = '#8a3b3b'; g.fillRect(cx - 16, my - 2, 32, 5); }
      else {
        g.fillStyle = '#c0605e'; g.beginPath(); g.moveTo(cx - 18, my); g.quadraticCurveTo(cx, my + 12, cx + 18, my); g.quadraticCurveTo(cx, my + 3, cx - 18, my); g.fill();
        g.fillStyle = '#b05452'; g.beginPath(); g.moveTo(cx - 17, my); g.quadraticCurveTo(cx - 7, my - 6, cx, my - 3); g.quadraticCurveTo(cx + 7, my - 6, cx + 17, my); g.fill();
        g.strokeStyle = 'rgba(90,30,30,0.6)'; g.lineWidth = 1.5; g.beginPath(); g.moveTo(cx - 17, my); g.quadraticCurveTo(cx, my + 3, cx + 17, my); g.stroke();
      }
      g.fillStyle = F.bochechas ? 'rgba(255,90,110,0.45)' : 'rgba(220,110,100,0.14)';
      for (const s of [-1, 1]) { g.beginPath(); g.arc(cx + s * w * 0.075, Y(0.3), F.bochechas ? 16 : 11, 0, 7); g.fill(); }
      if (F.sardas) { g.fillStyle = 'rgba(150,80,40,0.45)'; for (let k = 0; k < 24; k++) g.fillRect(cx - 30 + Math.random() * 60, Y(0.36) + Math.random() * 12, 2, 2); }
    }
    }
    // cabelo pintado (linha do cabelo)
    if (def.cabelo !== undefined && !(m && m.tipo !== 'olhos')) {
      g.fillStyle = hex(def.cabelo);
      g.fillRect(x, y, w, h * 0.2); // topo
      g.fillRect(x, y, w * 0.3, h * 0.62); g.fillRect(x + w * 0.7, y, w * 0.3, h * 0.62); // nuca e laterais de trás
      noise(g, x, y, w, h * 0.6, 900, 0.08);
    }
  }
  // ---- tronco ----
  {
    const [x, y, w, h] = rg(AT.torso), Y = (t) => y + h * (1 - t), cx = x + w / 2;
    fillPattern(g, C.tronco, x, y, w, h, tools);
    noise(g, x, y, w, h, 2500, 0.03);
    // calça na parte de baixo
    fillPattern(g, C.shorts ?? C.calca, x, Y(0.22), w, h * 0.22 + 1, tools);
    if (C.cinto !== undefined) { g.fillStyle = hex(C.cinto); g.fillRect(x, Y(0.27), w, h * 0.05); }
    if (C.listras) for (let k = 0; k < 3; k++) { g.fillStyle = k % 2 ? '#ffffff' : '#d32f2f'; g.fillRect(x, Y(0.32 + k * 0.05), w, h * 0.05); }
    if (C.manga === 'nenhuma') { g.fillStyle = hex(C.bracos); g.fillRect(x, Y(1), w, h * 0.78); }
    if (C.gola !== undefined) { g.fillStyle = hex(C.gola); g.fillRect(x, Y(1), w, h * 0.07); }
    else if (C.manga !== 'nenhuma' && !(def.rosto && def.rosto.mascara && def.rosto.mascara.tipo !== 'olhos')) { g.fillStyle = hex(skin); g.beginPath(); g.moveTo(cx - 30, Y(1)); g.lineTo(cx, Y(0.88)); g.lineTo(cx + 30, Y(1)); g.fill(); }
    if (C.suspensorio !== undefined) { g.fillStyle = hex(C.suspensorio); for (const u of [0.42, 0.58, 0.08, -0.08 + 1]) g.fillRect(x + w * u - 6, Y(0.98), 12, h * 0.75); }
    if (C.gravata !== undefined) { g.fillStyle = hex(C.gravata); g.beginPath(); g.moveTo(cx - 8, Y(0.97)); g.lineTo(cx + 8, Y(0.97)); g.lineTo(cx + 13, Y(0.55)); g.lineTo(cx, Y(0.48)); g.lineTo(cx - 13, Y(0.55)); g.fill(); g.strokeStyle = 'rgba(0,0,0,0.3)'; g.lineWidth = 3; g.beginPath(); g.moveTo(cx - 40, Y(1)); g.lineTo(cx - 10, Y(0.6)); g.moveTo(cx + 40, Y(1)); g.lineTo(cx + 10, Y(0.6)); g.stroke(); }
    if (C.emblema) { const e = tools.emblemTex(C.emblema).image; g.drawImage(e, cx - 42, Y(0.82), 84, 84); }
    if (C.jardineira !== undefined) { // macacão: peitilho na frente e alças
      g.fillStyle = hex(C.jardineira); g.fillRect(x, Y(0.3), w, h * 0.3); g.fillRect(cx - 55, Y(0.72), 110, h * 0.42);
      for (const u of [-45, 45]) g.fillRect(cx + u - 8, Y(1), 16, h * 0.3);
      g.fillRect(x + w * 0.02, Y(1), 16, h * 0.7); g.fillRect(x + w * 0.98 - 16, Y(1), 16, h * 0.7);
      g.fillStyle = '#ffc400'; for (const u of [-38, 38]) { g.beginPath(); g.arc(cx + u, Y(0.7), 7, 0, 7); g.fill(); }
    }
    if (C.golaMarinheiro !== undefined) { g.fillStyle = hex(C.golaMarinheiro); g.beginPath(); g.moveTo(cx - 70, Y(1)); g.lineTo(cx, Y(0.72)); g.lineTo(cx + 70, Y(1)); g.fill(); g.fillRect(x, Y(1), w * 0.22, h * 0.22); g.fillRect(x + w * 0.78, Y(1), w * 0.22, h * 0.22); g.strokeStyle = '#fff'; g.lineWidth = 4; g.beginPath(); g.moveTo(cx - 58, Y(0.99)); g.lineTo(cx, Y(0.76)); g.lineTo(cx + 58, Y(0.99)); g.stroke(); }
    if (C.laco !== undefined) { g.fillStyle = hex(C.laco); const ly = C.golaMarinheiro !== undefined ? Y(0.75) : Y(0.94); g.beginPath(); g.moveTo(cx, ly); g.lineTo(cx - 34, ly - 16); g.lineTo(cx - 34, ly + 16); g.closePath(); g.moveTo(cx, ly); g.lineTo(cx + 34, ly - 16); g.lineTo(cx + 34, ly + 16); g.closePath(); g.fill(); g.beginPath(); g.arc(cx, ly, 8, 0, 7); g.fill(); }
    if (C.rasgado) { g.fillStyle = hex(C.bracos); for (let k = 0; k < w; k += 20) { g.beginPath(); g.moveTo(x + k, Y(0.22)); g.lineTo(x + k + 10, Y(0.15)); g.lineTo(x + k + 20, Y(0.22)); g.fill(); } }
    // costura lateral e sombra
    g.fillStyle = 'rgba(0,0,0,0.08)'; g.fillRect(x + w * 0.24, y, 3, h); g.fillRect(x + w * 0.75, y, 3, h);
  }
  // ---- braços ----
  {
    const [x, y, w, h] = rg(AT.arm), Y = (t) => y + h * t;
    const sleeve = C.manga === 'curta' ? 0.25 : C.manga === 'longa' ? 0.68 : 0;
    g.fillStyle = hex(skin); g.fillRect(x, y, w, h);
    if (C.manga === 'nenhuma') { g.fillStyle = hex(C.bracos); g.fillRect(x, y, w, h); }
    if (sleeve) fillPattern(g, C.bracos ?? C.tronco, x, y, w, h * sleeve, tools);
    if (C.manga === 'curta') fillPattern(g, C.tronco, x, y, w, h * sleeve, tools);
    if (C.maos !== undefined && C.maos !== skin) { g.fillStyle = hex(C.maos); g.fillRect(x, Y(0.69), w, h * 0.31); }
    if (C.maos === undefined || C.maos === skin) { g.fillStyle = hex(skin); g.fillRect(x, Y(0.8), w, h * 0.2); }
    noise(g, x, y, w, h, 1200, 0.03);
  }
  // ---- pernas ----
  {
    const [x, y, w, h] = rg(AT.leg), Y = (t) => y + h * t;
    fillPattern(g, C.calca, x, y, w, h, tools);
    if (C.shorts !== undefined) { fillPattern(g, C.shorts, x, y, w, h * 0.42, tools); g.fillStyle = hex(C.pernasPele ?? (C.shorts === C.calca ? C.calca : C.calca)); if (C.pernasPele !== undefined) g.fillRect(x, Y(0.42), w, h * 0.58); }
    if (C.meias !== undefined) { g.fillStyle = hex(C.meias); g.fillRect(x, Y(0.78), w, h * 0.2); }
    g.fillStyle = hex(C.sapatos ?? 0x222222); g.fillRect(x, Y(0.96), w, h * 0.04 + 1); // sapato (pés)
    if (C.faixa !== undefined) { g.fillStyle = hex(C.faixa); g.fillRect(x + w * 0.25 - 4, y, 8, h); g.fillRect(x + w * 0.75 - 4, y, 8, h); }
    g.fillStyle = 'rgba(0,0,0,0.12)'; g.fillRect(x, Y(0.5), w, 3);
    noise(g, x, y, w, h, 1500, 0.03);
  }
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 4;
  return t;
}

// ---------------- o corpo ----------------
export class HumanBody {
  constructor(def, C, tools) {
    const H = def.altura, F = def.forte || 1, Fw = Math.pow(F, 0.8), Fa = Math.pow(F, 0.75);
    this.root = new THREE.Group(); this.pivot = new THREE.Group(); this.root.add(this.pivot);
    this.rig = new THREE.Group(); this.pivot.add(this.rig);
    // articulações (metros)
    const hipY = 0.52 * H, waistY = 0.61 * H, neckY = 0.84 * H, headY = 0.866 * H, shY = 0.815 * H;
    const shX = 0.112 * H * Math.sqrt(F), hipX = 0.054 * H * Math.sqrt(F);
    const aOut = 0.16, dir = [Math.sin(aOut), -Math.cos(aOut), 0];
    const L1 = 0.172 * H, L2 = 0.15 * H, L3 = 0.1 * H;
    const sh = [shX, shY, -0.004 * H], el = sh.map((v, i) => v + dir[i] * L1), wr = el.map((v, i) => v + dir[i] * L2);
    const th = [hipX, hipY, 0], kn = [hipX * 1.02, hipY - 0.245 * H, 0.004 * H];
    const at = { hips: [0, hipY, 0], spine: [0, waistY, 0], neck: [0, neckY, -0.004 * H], head: [0, headY, 0], shL: sh, elL: el, shR: [-sh[0], sh[1], sh[2]], elR: [-el[0], el[1], el[2]], thL: th, knL: kn, thR: [-th[0], th[1], th[2]], knR: [-kn[0], kn[1], kn[2]] };
    const { bones, list, index, rest } = makeBones(at);
    this.rest = rest; this.armRest = aOut; this.legRest = 0;
    const B = new Builder(); B.bi = index;
    const hh = (y) => y * H;

    // tronco
    const TP = [ // [y/H, meia largura, meia prof., z]
      [0.455, 0.045, 0.045, 0], [0.48, 0.08, 0.058, -0.004], [0.52, 0.093, 0.063, -0.006], [0.56, 0.093, 0.062, -0.004], [0.61, 0.083, 0.057, 0],
      [0.66, 0.09 * Fw, 0.062 * Math.pow(F, 0.6), 0.004], [0.71, 0.1 * Fw, 0.068 * Math.pow(F, 0.6), 0.008], [0.76, 0.106 * Fw, 0.066 * Math.pow(F, 0.6), 0.004],
      [0.79, 0.106 * Fw, 0.062 * Math.pow(F, 0.6), 0], [0.81, 0.094 * Fw, 0.054, -0.003], [0.826, 0.07 * Fw, 0.045, -0.004], [0.84, 0.042, 0.035, -0.004], [0.85, 0.03, 0.028, -0.002],
    ];
    B.tube(TP.length, (i) => {
      const [y, w, d, z] = TP[i], t = (y - 0.455) / (0.848 - 0.455);
      const sy = ss(waistY - 0.06 * H, waistY + 0.06 * H, hh(y)), ny = ss(0.83 * H, 0.85 * H, hh(y));
      return { c: [0, hh(y), z * H], rx: w * H, rz: d * H, v: 1 - t, w: { hips: (1 - sy) * (1 - ny), spine: sy * (1 - ny), head: ny } };
    }, AT.torso, true, false);
    // pescoço
    B.tube(5, (i) => { const t = i / 4, y = lerp(0.83, 0.875, t); const k = ss(0.845, 0.87, y); return { c: [0, hh(y), -0.004 * H], rx: 0.027 * H * Math.sqrt(F), rz: 0.029 * H * Math.sqrt(F), v: 0.995 - t * 0.04, w: { spine: 1 - k, head: k } }; }, AT.head, false, false);
    // cabeça
    const HR = 0.047 * H * (def.cabecaGrande || 1) * (F > 1.5 ? 0.9 : 1), HD = HR * 1.13, HH = 0.135 * H * (def.cabecaGrande || 1) * (F > 1.5 ? 0.9 : 1);
    const HP = [[0, 0.32, 0.18], [0.07, 0.58, 0.14], [0.17, 0.76, 0.1], [0.3, 0.86, 0.06], [0.45, 0.93, 0.03], [0.58, 0.97, 0], [0.7, 0.96, -0.02], [0.8, 0.9, -0.03], [0.89, 0.76, -0.03], [0.95, 0.56, -0.02], [0.99, 0.25, 0], [1, 0.02, 0]];
    const headBase = headY - 0.004 * H;
    B.tube(HP.length, (i) => { const [t, r, z] = HP[i]; return { c: [0, headBase + t * HH, z * HR + 0.006 * H], rx: r * HR, rz: r * HD, v: 1 - t, w: { head: 1 }, shape: (th) => 1 - 0.07 * Math.pow(Math.cos(th), 8) }; }, AT.head, false, true);
    this.headTop = HH; this.HR = HR;
    // braços (até a ponta dos dedos)
    const AP = [[-0.03, 0.012, 0.012], [-0.018, 0.028, 0.029], [0, 0.035, 0.036], [0.045, 0.033, 0.034], [0.09, 0.031, 0.032], [0.15, 0.025, 0.026], [0.172, 0.022, 0.024], [0.2, 0.025, 0.026], [0.25, 0.023, 0.024], [0.318, 0.017, 0.019], [0.335, 0.014, 0.026], [0.36, 0.013, 0.026], [0.385, 0.011, 0.02], [0.4, 0.008, 0.012]];
    for (const s of [1, -1]) {
      const n = s > 0 ? 'L' : 'R';
      B.tube(AP.length, (i) => {
        const [dd, rx, rz] = AP[i], d = dd * H, total = 0.4 * H;
        const p = [s * (sh[0] + dir[0] * d), sh[1] + dir[1] * d, sh[2]];
        const k = ss(L1 - 0.022 * H, L1 + 0.022 * H, d);
        return { c: p, rx: rx * H * (dd < 0.32 ? Fa : 1), rz: rz * H * (dd < 0.32 ? Fa : 1), v: Math.max(0, d) / total, w: { ['sh' + n]: 1 - k, ['el' + n]: k } };
      }, s > 0 ? [0.5, 0.5625, 0, 1] : AT.arm, true, true);
    }
    // pernas
    const LP = [[0, 0.064, 0.062], [0.05, 0.062, 0.062], [0.12, 0.054, 0.056], [0.2, 0.043, 0.046], [0.245, 0.037, 0.04], [0.29, 0.039, 0.044], [0.34, 0.038, 0.042], [0.41, 0.027, 0.03], [0.468, 0.023, 0.026]];
    for (const s of [1, -1]) {
      const n = s > 0 ? 'L' : 'R';
      B.tube(LP.length, (i) => {
        const [dd, rx, rz] = LP[i], d = dd * H;
        const k = ss(0.245 * H - 0.025 * H, 0.245 * H + 0.025 * H, d);
        const ft = dd / 0.468;
        return { c: [s * lerp(th[0], kn[0] * 1.05, ft), hipY + 0.02 * H - d, lerp(0, kn[2], ft)], rx: rx * H * Math.sqrt(F), rz: rz * H * Math.sqrt(F), v: ft, w: { ['th' + n]: 1 - k, ['kn' + n]: k } };
      }, s > 0 ? [1, 0.5625, 0.5, 1] : AT.leg, false, true);
    }
    const geo = B.geometry();
    const atlas = paintAtlas(def, C, tools);
    const skinMat = new THREE.MeshStandardMaterial({ map: atlas, roughness: 0.68, metalness: 0 });
    const mesh = new THREE.SkinnedMesh(geo, skinMat); mesh.frustumCulled = false; mesh.castShadow = true;
    mesh.add(bones.hips);
    mesh.position.y = -hipY; this.rig.add(mesh);
    this.pivot.position.y = hipY;
    this.root.updateMatrixWorld(true);
    mesh.bind(new THREE.Skeleton(list));
    this.mesh = mesh; this.j = bones; this.H = H;
    const skin = def.pele ?? (def.rosto && def.rosto.pele) ?? 0xe8b896;
    const m = def.rosto && def.rosto.mascara;
    const faceCol = m && m.tipo !== 'olhos' ? m.cor : skin;
    // nariz e orelhas (presos ao osso da cabeça)
    const hb = bones.head, std = (c) => new THREE.MeshStandardMaterial({ color: c, roughness: 0.65 });
    const yOf = (t) => (headBase + t * HH) - headY;
    if (!(m && m.tipo === 'total')) {
      const nose = new THREE.Mesh(new THREE.SphereGeometry(HR * 0.17, 10, 8), std(def.rosto && def.rosto.nariz || faceCol));
      nose.scale.set(0.75, 1.15, 1.05); nose.position.set(0, yOf(0.36), HD * 0.93 + 0.006 * H); nose.rotation.x = -0.25; hb.add(nose);
    }
    for (const s of [-1, 1]) { const ear = new THREE.Mesh(new THREE.SphereGeometry(HR * 0.2, 8, 6), std(faceCol)); ear.scale.set(0.45, 1.05, 0.75); ear.position.set(s * HR * 0.93, yOf(0.5), -0.004 * H); hb.add(ear); }
    if (def.bochechas) for (const s of [-1, 1]) { const b = new THREE.Mesh(new THREE.SphereGeometry(HR * 0.42, 12, 10), std(faceCol)); b.position.set(s * HR * 0.55, yOf(0.3), HD * 0.55); b.scale.set(1, 0.9, 0.8); hb.add(b); }
    // cabelo
    if (def.cabelo !== undefined && !(m && m.tipo !== 'olhos')) this.addHair(hb, def, HR, HD, HH, yOf);
    // sapatos
    for (const s of [1, -1]) {
      const n = s > 0 ? 'L' : 'R', kb = bones['kn' + n];
      const shoe = new THREE.Mesh(new THREE.CapsuleGeometry(0.03 * H * Math.sqrt(F), 0.085 * H, 4, 10), std(C.sapatos));
      shoe.rotation.x = Math.PI / 2; shoe.scale.set(1.05, 1, 0.75);
      shoe.position.set(s * (kn[0] * 1.05 - kn[0]), (0.03 * H) - kn[1], 0.035 * H); kb.add(shoe);
      shoe.castShadow = true;
      if (C.sola !== undefined) { const so = new THREE.Mesh(new THREE.BoxGeometry(0.06 * H, 0.012 * H, 0.15 * H), std(C.sola)); so.position.set(shoe.position.x, shoe.position.y - 0.022 * H, shoe.position.z); kb.add(so); }
    }
    this.J = bones;
  }

  addHair(hb, def, HR, HD, HH, yOf) {
    const style = def.estiloCabelo || 'curto';
    const mat = new THREE.MeshStandardMaterial({ color: def.cabelo, roughness: 0.85 });
    const cap = new THREE.Mesh(new THREE.SphereGeometry(1, 20, 12, 0, Math.PI * 2, 0, Math.PI * 0.58), mat);
    cap.scale.set(HR * 1.1, HH * 0.5, HD * 1.1); cap.position.set(0, yOf(0.56), -HR * 0.12); cap.rotation.x = -0.55; hb.add(cap);
    if (style === 'longo') { const back = new THREE.Mesh(new THREE.CapsuleGeometry(HR * 0.85, HH * 0.5, 4, 12), mat); back.scale.set(1.15, 1, 0.55); back.position.set(0, yOf(0.35), -HD * 0.55); hb.add(back); }
    if (style === 'topete') { const t = new THREE.Mesh(new THREE.SphereGeometry(HR * 0.55, 12, 8), mat); t.scale.set(1.2, 0.6, 1); t.position.set(0, yOf(0.95), HD * 0.35); hb.add(t); }
    if (style === 'arrepiado') for (let k = 0; k < 7; k++) { const sp = new THREE.Mesh(new THREE.ConeGeometry(HR * 0.22, HR * 0.6, 5), mat); const a = k / 7 * Math.PI * 2; sp.position.set(Math.sin(a) * HR * 0.5, yOf(0.95), Math.cos(a) * HD * 0.45); sp.rotation.set(Math.cos(a) * 0.6, 0, -Math.sin(a) * 0.6); hb.add(sp); }
  }

  apply(p) { applyPose(this.j, this.rest, p, this.armRest, 0, false); }
}
