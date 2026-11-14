import * as THREE from 'three';
import { Driver } from './anim.js';
import { AT, paintAtlas } from './human.js';

// =====================================================================
// Personagens adultos sobre um corpo humanoide de verdade (X Bot da
// Mixamo, já com esqueleto). O rosto e a fantasia são pintados num
// "atlas" e mapeados pelas regiões do corpo (cabeça, tronco, braços,
// pernas). As animações capturadas tocam direto neste esqueleto.
// =====================================================================
let UVCACHE = null; // as coordenadas são iguais para todos (mesmo corpo)

function regionOf(name) {
  if (/Head|Neck/.test(name)) return 'head';
  if (/Arm|Hand/.test(name)) return /Left/.test(name) ? 'armL' : 'armR';
  if (/UpLeg|Leg|Foot|Toe/.test(name)) return /Left/.test(name) ? 'legL' : 'legR';
  return 'torso';
}

function buildUVs(drv) {
  const root = drv.root; root.updateMatrixWorld(true);
  const inv = new THREE.Matrix4().copy(root.matrixWorld).invert();
  const P = (n) => drv.b[n].getWorldPosition(new THREE.Vector3()).applyMatrix4(inv);
  const head = P('Head'), top = P('HeadTop_End'), neck = P('Neck');
  const hipY = P('Hips').y, crotch = (P('LeftUpLeg').y + hipY) / 2 - 0.05, neckTop = P('Neck').y;
  const arm = { L: [P('LeftArm'), P('LeftHand')], R: [P('RightArm'), P('RightHand')] };
  const leg = { L: [P('LeftUpLeg'), P('LeftFoot')], R: [P('RightUpLeg'), P('RightFoot')] };
  const lerp = (a, b, t) => a + (b - a) * t, clamp = (t) => Math.max(0.004, Math.min(0.996, t));
  const reg = (r, u, v) => [lerp(r[0], r[2], u), 1 - lerp(r[1], r[3], clamp(v))];
  const out = new Map();
  const hb = new THREE.Box3();
  root.traverse(o => {
    if (!o.isSkinnedMesh) return;
    const g = o.geometry, pos = g.attributes.position, si = g.attributes.skinIndex, sw = g.attributes.skinWeight;
    const m = new THREE.Matrix4().multiplyMatrices(inv, o.matrixWorld);
    const uv = new Float32Array(pos.count * 2), v = new THREE.Vector3();
    for (let i = 0; i < pos.count; i++) {
      v.fromBufferAttribute(pos, i); o.applyBoneTransform(i, v); v.applyMatrix4(m);
      let best = 0, bw = -1; for (let k = 0; k < 4; k++) { const w = sw.getComponent(i, k); if (w > bw) { bw = w; best = si.getComponent(i, k); } }
      const bname = o.skeleton.bones[best].name.replace(/^mixamorig:?/, '');
      const r = regionOf(bname);
      let u, t, R;
      if (r === 'head' && /Head/.test(bname) && o.name === 'Beta_Surface') hb.expandByPoint(v);
      if (r === 'head') {
        u = 0.5 + Math.atan2(v.x - head.x, v.z - head.z) / (2 * Math.PI); t = (v.y - neck.y) / (top.y - neck.y); R = AT.head; [u, t] = [u, 1 - t];
      } else if (r === 'torso') {
        u = 0.5 + Math.atan2(v.x, v.z - head.z) / (2 * Math.PI); t = (v.y - crotch) / (neckTop - crotch); R = AT.torso; [u, t] = [u, 1 - t];
      } else if (r === 'armL' || r === 'armR') {
        const [a, b] = arm[r[3]]; const ax = b.clone().sub(a), L = ax.length(); ax.normalize();
        const d = v.clone().sub(a); t = d.dot(ax) / (L * 1.25);
        if (/Hand/.test(bname)) t = Math.max(t, 0.8);
        u = 0.5 + Math.atan2(d.z, d.y) / (2 * Math.PI); R = r === 'armL' ? [AT.arm[2], AT.arm[1], AT.arm[0], AT.arm[3]] : AT.arm;
      } else {
        const [a, b] = leg[r[3]]; t = (a.y - v.y) / (a.y - b.y);
        if (/Foot|Toe/.test(bname)) t = 0.985;
        u = 0.5 + Math.atan2(v.x - a.x, v.z - a.z) / (2 * Math.PI); R = r === 'legL' ? [AT.leg[2], AT.leg[1], AT.leg[0], AT.leg[3]] : AT.leg;
        t = Math.min(t, 0.985);
      }
      const [U, V] = reg(R, u, t); uv[i * 2] = U; uv[i * 2 + 1] = V;
    }
    out.set(o.name, new THREE.BufferAttribute(uv, 2));
  });
  return { out, head, top, neck, headBox: hb };
}

export class XBotBody {
  constructor(template, clips, def, C, tools) {
    const H = def.altura, F = def.forte || 1;
    this.drv = new Driver(template, clips, true);
    const root = this.drv.root;
    if (!UVCACHE) UVCACHE = buildUVs(this.drv);
    const atlas = paintAtlas(def, C, { ...tools, noFace: true });
    // cada personagem tem seu material; a geometria é compartilhada
    root.traverse(o => {
      if (!o.isSkinnedMesh) return;
      o.geometry = o.geometry.clone(); o.geometry.setAttribute('uv', UVCACHE.out.get(o.name));
      o.material = new THREE.MeshStandardMaterial({ map: atlas, roughness: /Joint/.test(o.name) ? 0.55 : 0.7, metalness: 0 });
      o.castShadow = true; o.frustumCulled = false;
    });
    // altura e porte
    const box = new THREE.Box3().setFromObject(root), h0 = box.max.y - box.min.y;
    const s = H / h0;
    this.root = new THREE.Group(); this.pivot = new THREE.Group(); this.rig = new THREE.Group();
    this.root.add(this.pivot); this.pivot.add(this.rig); this.rig.add(root);
    const hipY = this.drv.hipRest.y * s;
    this.pivot.position.y = hipY; this.rig.position.y = -hipY - box.min.y * s;
    root.scale.set(s * Math.sqrt(F), s, s * Math.sqrt(F));
    if (def.cabecaGrande) this.drv.b.Head.scale.setScalar(def.cabecaGrande);
    if (F > 1.3) { this.drv.b.Spine2.scale.set(1.15, 1, 1.2); this.drv.b.Head.scale.setScalar(0.8); this.drv.b.LeftArm.scale.setScalar(1.15); this.drv.b.RightArm.scale.setScalar(1.15); }
    this.H = H; this.s = s;
    // grupo preso à cabeça com eixos iguais aos do personagem (para nariz, orelhas, cabelo, chapéu)
    const hb = this.drv.b.Head;
    const hg = new THREE.Group(); hg.quaternion.copy(this.drv.W0.get(hb)).invert(); hb.add(hg);
    this.headGroup = hg;
    const hu = UVCACHE.top.y - UVCACHE.neck.y; // altura da cabeça (unidades do boneco)
    this.headH = hu; this.headR = hu * 0.42; this.headBase = UVCACHE.neck.y - this.drv.b.Head.getWorldPosition(new THREE.Vector3()).y * 0 ;
    // mão: grupo alinhado ao personagem
    this.hand = {};
    for (const side of ['Left', 'Right']) { const b = this.drv.b[side + 'Hand']; const g = new THREE.Group(); g.quaternion.copy(this.drv.W0.get(b)).invert(); b.add(g); this.hand[side] = g; }
    this.fore = {};
    for (const side of ['Left', 'Right']) { const b = this.drv.b[side + 'ForeArm']; const g = new THREE.Group(); g.quaternion.copy(this.drv.W0.get(b)).invert(); b.add(g); this.fore[side] = g; }
    this.root.updateMatrixWorld(true);
    this.headTopLocal = this.drv.b.HeadTop_End.position.length();
    // centro e raios da cabeça no espaço do grupo da cabeça
    const hbx = UVCACHE.headBox, cW = hbx.getCenter(new THREE.Vector3()), sz = hbx.getSize(new THREE.Vector3());
    root.updateMatrixWorld(true);
    const inv = new THREE.Matrix4().copy(this.rig.matrixWorld).invert();
    this.headC = hg.worldToLocal(cW.clone().applyMatrix4(root.matrixWorld));
    const k = 1 / s; // o grupo da cabeça já está dentro da escala do boneco
    this.headR = new THREE.Vector3(sz.x / 2, sz.y / 2, sz.z / 2);
    this.addFace(def);
  }
  addFace(def) {
    const F = def.rosto || {}, m = F.mascara, skin = def.pele ?? F.pele ?? 0xe8b896;
    const hex = (n) => '#' + (n >>> 0).toString(16).padStart(6, '0');
    const c = document.createElement('canvas'); c.width = 256; c.height = 256; const g = c.getContext('2d');
    g.clearRect(0, 0, 256, 256);
    const cx = 128, ey = 112;
    // pele do rosto (oval) — ou a máscara
    const face = m && m.tipo !== 'olhos' && m.tipo !== 'capuz' ? m.cor : skin;
    const grd = g.createRadialGradient(cx, 130, 30, cx, 130, 120); grd.addColorStop(0, hex(face)); grd.addColorStop(0.85, hex(face)); grd.addColorStop(1, 'rgba(0,0,0,0)');
    g.fillStyle = grd; g.beginPath(); g.ellipse(cx, 130, 104, 122, 0, 0, 7); g.fill();
    if (m && m.tipo === 'garras') { g.fillStyle = hex(m.cor); g.fillRect(0, 0, 256, 128); g.fillStyle = hex(m.cor2 ?? 0x111111); g.beginPath(); g.moveTo(40, 120); g.lineTo(cx, 60); g.lineTo(216, 120); g.lineTo(200, 92); g.lineTo(cx, 30); g.lineTo(56, 92); g.fill(); }
    if (m && m.tipo === 'olhos') { g.fillStyle = hex(m.cor); g.fillRect(0, ey - 22, 256, 44); }
    if (m && m.teia) { g.strokeStyle = 'rgba(0,0,0,0.85)'; g.lineWidth = 1.5; for (let a = 0; a < 16; a++) { g.beginPath(); g.moveTo(cx, ey + 10); g.lineTo(cx + Math.cos(a / 16 * 6.28) * 200, ey + 10 + Math.sin(a / 16 * 6.28) * 200); g.stroke(); } for (let r = 14; r < 180; r += 18) { g.beginPath(); g.ellipse(cx, ey + 10, r, r * 0.8, 0, 0, 7); g.stroke(); } }
    // olhos
    if (F.olhos === 'lente') {
      for (const s2 of [-1, 1]) { g.fillStyle = '#111'; g.beginPath(); g.ellipse(cx + s2 * 40, ey, 30, 20, s2 * -0.4, 0, 7); g.fill(); g.fillStyle = '#fff'; g.beginPath(); g.ellipse(cx + s2 * 40, ey, 24, 15, s2 * -0.4, 0, 7); g.fill(); }
    } else {
      for (const s2 of [-1, 1]) {
        const ex = cx + s2 * 38;
        g.fillStyle = '#fbfbf6'; g.beginPath(); g.ellipse(ex, ey, 19, 11, 0, 0, 7); g.fill();
        g.fillStyle = hex(def.olho ?? 0x5b3a1e); g.beginPath(); g.arc(ex, ey + 1, 9, 0, 7); g.fill();
        g.fillStyle = '#0d0d0d'; g.beginPath(); g.arc(ex, ey + 1, 4.5, 0, 7); g.fill();
        g.fillStyle = '#fff'; g.beginPath(); g.arc(ex + 3, ey - 3, 2.2, 0, 7); g.fill();
        g.strokeStyle = '#24160f'; g.lineWidth = 3.5; g.beginPath(); g.ellipse(ex, ey + 2, 20, 12, 0, Math.PI * 1.05, Math.PI * 1.95); g.stroke();
        g.strokeStyle = hex(F.sobrancelha ?? def.cabelo ?? 0x3b2a1a); g.lineWidth = 7; g.lineCap = 'round';
        g.beginPath(); g.moveTo(ex - 18 * s2, ey - 22 + (F.bravo ? -4 : 0)); g.quadraticCurveTo(ex, ey - 32, ex + 20 * s2, ey - 22 + (F.bravo ? 6 : 2)); g.stroke();
      }
    }
    if (!(m && m.tipo === 'total')) {
      g.fillStyle = 'rgba(120,60,40,0.18)'; g.beginPath(); g.ellipse(cx, 142, 13, 9, 0, 0, 7); g.fill();
      const my = 168;
      if (F.boca === 'dentes') { g.fillStyle = '#7a1f2a'; g.beginPath(); g.ellipse(cx, my, 46, 20, 0, 0, Math.PI); g.fill(); g.fillStyle = '#fff'; g.fillRect(cx - 42, my - 1, 84, 11); }
      else if (F.boca === 'bravo') { g.fillStyle = '#7a3333'; g.fillRect(cx - 22, my - 2, 44, 6); }
      else { g.fillStyle = '#c0605e'; g.beginPath(); g.moveTo(cx - 25, my); g.quadraticCurveTo(cx, my + 17, cx + 25, my); g.quadraticCurveTo(cx, my + 5, cx - 25, my); g.fill(); g.strokeStyle = 'rgba(90,30,30,0.7)'; g.lineWidth = 2; g.beginPath(); g.moveTo(cx - 24, my); g.quadraticCurveTo(cx, my + 4, cx + 24, my); g.stroke(); }
      g.fillStyle = F.bochechas ? 'rgba(255,90,110,0.45)' : 'rgba(220,110,100,0.16)'; for (const s2 of [-1, 1]) { g.beginPath(); g.arc(cx + s2 * 60, 160, F.bochechas ? 22 : 16, 0, 7); g.fill(); }
      if (def.barba) { g.fillStyle = 'rgba(60,40,30,0.4)'; g.beginPath(); g.ellipse(cx, 205, 70, 40, 0, 0, Math.PI); g.fill(); }
      if (F.sardas) { g.fillStyle = 'rgba(150,80,40,0.5)'; for (let k = 0; k < 26; k++) g.fillRect(cx - 50 + Math.random() * 100, 140 + Math.random() * 20, 3, 3); }
    }
    const tex = new THREE.CanvasTexture(c); tex.colorSpace = THREE.SRGBColorSpace;
    const R = this.headR, C = this.headC;
    const geo = new THREE.SphereGeometry(1, 24, 18, Math.PI * 0.5 - 1.05, 2.1, 0.78, 1.75);
    const mesh = new THREE.Mesh(geo, new THREE.MeshStandardMaterial({ map: tex, transparent: true, roughness: 0.6, polygonOffset: true, polygonOffsetFactor: -2 }));
    // SphereGeometry: phi começa em -X; a frente (+Z) fica em phi = PI/2... ajusta para olhar para +Z
    mesh.rotation.y = -Math.PI / 2 + Math.PI / 2;
    mesh.scale.set(R.x * 1.04, R.y * 1.03, R.z * 1.12); mesh.position.copy(C);
    this.headGroup.add(mesh); this.face = mesh;
  }
  // posição y (no grupo da cabeça) de uma fração t da altura da cabeça (0 = queixo, 1 = topo)
  yOf(t) { return (t - 0.12) * this.headTopLocal; }
  update(dt, st) { this.drv.update(dt, st); }
}
