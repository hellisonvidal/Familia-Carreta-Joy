import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { rng, pick, range } from './cities.js';
import * as TX from './textures.js';

// =====================================================================
// Cidade em MUNDO ABERTO: grade de quarteirões com ladeiras.
// A carreta dá a volta pelo anel (as ruas da borda); os personagens
// andam por onde quiserem, sobem em carros, quiosques, telhados baixos...
// =====================================================================
export const N = 6;          // quarteirões por lado
export const P = 84;         // distância entre ruas
export const RW = 7;         // meia largura do asfalto
export const SW = 3.5;       // calçada
export const HALF = N * P / 2;
export const lineC = (k) => -HALF + k * P;
export const RING = HALF - 3.5; // faixa da carreta no anel
// ruas internas com trânsito de carros (as outras são calçadões com rampas e caixas)
export const TRAFFIC = { x: [1, 3, 5], z: [2, 4] };
export const isTraffic = (axis, k) => TRAFFIC[axis].includes(k);

const UP = new THREE.Vector3(0, 1, 0);
const _c = new THREE.Color(), _q = new THREE.Quaternion(), _s = new THREE.Vector3();

class Acc {
  constructor() { this.parts = []; }
  add(geo, matrix, color) {
    const g = geo.index ? geo.toNonIndexed() : geo.clone();
    if (matrix) g.applyMatrix4(matrix);
    const n = g.attributes.position.count, col = new Float32Array(n * 3);
    _c.set(color);
    for (let i = 0; i < n; i++) { col[i * 3] = _c.r; col[i * 3 + 1] = _c.g; col[i * 3 + 2] = _c.b; }
    g.setAttribute('color', new THREE.BufferAttribute(col, 3));
    if (!g.attributes.uv) g.setAttribute('uv', new THREE.BufferAttribute(new Float32Array(n * 2), 2));
    if (!g.attributes.normal) g.computeVertexNormals();
    this.parts.push(g);
  }
  build() { if (!this.parts.length) return null; const g = mergeGeometries(this.parts, false); this.parts.forEach(p => p.dispose()); this.parts = []; return g; }
}
const M4 = (x, y, z, ry = 0, sx = 1, sy = 1, sz = 1) => new THREE.Matrix4().compose(new THREE.Vector3(x, y, z), _q.setFromAxisAngle(UP, ry), _s.set(sx, sy, sz));

function buildingBox(w, h, d, tile = 12) {
  // w = largura em x, d = profundidade em z
  const g = new THREE.BoxGeometry(w, h, d);
  const uv = g.attributes.uv;
  for (let face = 0; face < 6; face++) for (let k = 0; k < 4; k++) {
    const i = face * 4 + k; let u = uv.getX(i), v = uv.getY(i);
    if (face === 0 || face === 1) { u *= d / tile; v *= h / tile; }
    else if (face === 4 || face === 5) { u *= w / tile; v *= h / tile; }
    else { u = 0.5; v = 0.995; }
    uv.setXY(i, u, v);
  }
  g.translate(0, h / 2, 0);
  return g;
}
function prism(w, d, rh) {
  const hw = w / 2 + 0.4, hz = d / 2 + 0.4;
  const p = [-hw, 0, -hz, -hw, 0, hz, -hw, rh, 0, hw, 0, hz, hw, 0, -hz, hw, rh, 0,
    -hw, 0, -hz, -hw, rh, 0, hw, rh, 0, -hw, 0, -hz, hw, rh, 0, hw, 0, -hz,
    -hw, 0, hz, hw, 0, hz, hw, rh, 0, -hw, 0, hz, hw, rh, 0, -hw, rh, 0];
  const g = new THREE.BufferGeometry(); g.setAttribute('position', new THREE.Float32BufferAttribute(p, 3)); g.computeVertexNormals();
  return g;
}
function textSign(text) {
  const c = document.createElement('canvas'); c.width = 1024; c.height = 160;
  const g = c.getContext('2d');
  const gr = g.createLinearGradient(0, 0, 1024, 0);
  gr.addColorStop(0, '#ff3d7f'); gr.addColorStop(0.33, '#ffc400'); gr.addColorStop(0.66, '#00c853'); gr.addColorStop(1, '#2979ff');
  g.fillStyle = gr; g.fillRect(0, 0, 1024, 160);
  g.font = 'bold 96px sans-serif'; g.textAlign = 'center'; g.textBaseline = 'middle';
  g.lineWidth = 10; g.strokeStyle = 'rgba(0,0,0,0.45)'; g.strokeText(text, 512, 84); g.fillStyle = '#fff'; g.fillText(text, 512, 84);
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t;
}

// ---------------------------------------------------------------------
export class City {
  constructor(scene, def, night) {
    this.scene = scene; this.def = def; this.night = night;
    this.group = new THREE.Group(); scene.add(this.group);
    this.r = rng(def.seed * 7 + 3);
    this.disp = [];
    this.colliders = [];
    this.grid = new Map();
    this.blinkers = [];
    const r = rng(def.seed);
    this.ph = [r() * 6, r() * 6, r() * 6];
    this.A = def.relevo;
    this.seaY = def.mar ? -this.A - 2.5 : null;
    this.planBlocks();
    this.buildSky();
    this.buildTerrain();
    this.buildStreets();
    this.buildBlocks();
    this.buildLamps();
    this.buildTrees();
    this.buildProps();
    this.buildPlazas();
    this.buildLandmarks();
    this.buildMountains();
    this.buildCrowd();
    this.buildRing();
  }

  // ---------------- relevo ----------------
  height(x, z) {
    const A = this.A, [a, b, c] = this.ph;
    let h = A * (0.55 * Math.sin(x / 95 + a) * Math.cos(z / 110 + b) + 0.45 * Math.sin((x * 0.6 + z * 0.8) / 130 + c));
    // perto do mar o terreno desce
    if (this.def.mar) {
      const d = this.def.mar.lado === 'sul' ? -z - (HALF + RW + 2) : -x - (HALF + RW + 2);
      if (d > 0) { const t = Math.min(1, d / 60); h = h + (this.seaY - 1.5 - h) * t * t * (3 - 2 * t); }
    }
    return h;
  }
  inCity(x, z) { return Math.abs(x) <= HALF + RW && Math.abs(z) <= HALF + RW; }
  onRoad(x, z) {
    if (!this.inCity(x, z)) return false;
    const fx = ((x + HALF) % P + P) % P, fz = ((z + HALF) % P + P) % P;
    return fx < RW || fx > P - RW || fz < RW || fz > P - RW;
  }
  // altura do piso (asfalto, calçada ou terra)
  floorY(x, z) {
    const h = this.height(x, z);
    if (!this.inCity(x, z)) return h - 0.12;
    return this.onRoad(x, z) ? h : h + 0.2;
  }

  // ---------------- colisões ----------------
  addCollider(c) {
    c.base = c.base ?? Math.min(this.floorY(c.x0, c.z0), this.floorY(c.x1, c.z1), this.floorY(c.x0, c.z1), this.floorY(c.x1, c.z0));
    this.colliders.push(c);
    const m = 2;
    for (let gx = Math.floor((c.x0 - m) / 16); gx <= Math.floor((c.x1 + m) / 16); gx++)
      for (let gz = Math.floor((c.z0 - m) / 16); gz <= Math.floor((c.z1 + m) / 16); gz++) {
        const k = gx * 1000 + gz; if (!this.grid.has(k)) this.grid.set(k, []); this.grid.get(k).push(c);
      }
    return c;
  }
  near(x, z) { return this.grid.get(Math.floor(x / 16) * 1000 + Math.floor(z / 16)) || []; }
  topAt(c, x, z) {
    if (!c.ramp) return c.top;
    const t = c.ramp.axis === 'x' ? (x - c.x0) / (c.x1 - c.x0) : (z - c.z0) / (c.z1 - c.z0);
    const k = Math.max(0, Math.min(1, c.ramp.dir > 0 ? t : 1 - t));
    return c.base + (c.top - c.base) * k;
  }
  groundAt(x, z, y, r = 0.25) {
    let h = this.floorY(x, z), top = null;
    for (const c of this.near(x, z)) {
      if (!c.walk) continue;
      if (x < c.x0 - r || x > c.x1 + r || z < c.z0 - r || z > c.z1 + r) continue;
      const t = this.topAt(c, x, z);
      if (y >= t - 0.5 && t > h) { h = t; top = c; }
    }
    return { h, top };
  }
  // empurra um círculo pra fora dos obstáculos; devolve o obstáculo batido
  collide(pos, r, feetY, skip) {
    let hit = null;
    for (const c of this.near(pos.x, pos.z)) {
      if (c.ramp || (skip && skip.has(c.kind))) continue;
      const t = this.topAt(c, pos.x, pos.z);
      if (feetY >= t - 0.45) continue;
      if (feetY + 1.6 < c.base) continue;
      const cx = Math.max(c.x0, Math.min(c.x1, pos.x)), cz = Math.max(c.z0, Math.min(c.z1, pos.z));
      const dx = pos.x - cx, dz = pos.z - cz, d2 = dx * dx + dz * dz;
      if (d2 >= r * r) continue;
      if (d2 > 1e-6) { const d = Math.sqrt(d2), k = (r - d) / d; pos.x += dx * k; pos.z += dz * k; hit = { c, nx: dx / d, nz: dz / d }; }
      else { // dentro: sai pelo lado mais perto
        const opts = [[pos.x - c.x0, -1, 0], [c.x1 - pos.x, 1, 0], [pos.z - c.z0, 0, -1], [c.z1 - pos.z, 0, 1]].sort((a, b) => a[0] - b[0]);
        const [d, nx, nz] = opts[0]; pos.x += nx * (d + r); pos.z += nz * (d + r); hit = { c, nx, nz };
      }
    }
    return hit;
  }

  // ---------------- planta da cidade ----------------
  planBlocks() {
    const T = {};
    const set = (i, j, type, extra = {}) => { T[`${i},${j}`] = { type, i, j, ...extra }; };
    set(2, 0, 'inicio'); set(5, 3, 'praca', { nome: 'Praça Leste' }); set(2, 5, 'praca', { nome: 'Praça Norte' });
    set(1, 2, 'parque'); set(3, 3, 'parkour');
    const marcos = { sp: [[3, 2, 'masp']], rio: [], bh: [[4, 1, 'pampulha']], ssa: [[3, 1, 'igreja']], cwb: [[4, 2, 'estufa']] }[this.def.id] || [];
    for (const [i, j, m] of marcos) set(i, j, 'marco', { marco: m });
    this.blocks = [];
    for (let i = 0; i < N; i++) for (let j = 0; j < N; j++) this.blocks.push(T[`${i},${j}`] || { type: 'normal', i, j });
    for (const b of this.blocks) {
      b.x0 = lineC(b.i) + RW; b.x1 = lineC(b.i + 1) - RW; b.z0 = lineC(b.j) + RW; b.z1 = lineC(b.j + 1) - RW;
      b.cx = (b.x0 + b.x1) / 2; b.cz = (b.z0 + b.z1) / 2;
    }
    this.blockAt = (i, j) => this.blocks.find(b => b.i === i && b.j === j);
    const start = this.blockAt(2, 0);
    this.start = { x: start.cx, z: -RING, heading: Math.PI / 2 }; // carreta virada para +x
    this.spawn = { x: start.cx - 6, z: start.z0 + 6 }; // calçada da praça inicial
    // paradas da carreta + ESPAÇO RESERVADO PARA DANÇA (pista entre a rua e o coreto)
    this.stops = []; this.zones = [];
    for (const b of this.blocks) {
      if (b.type !== 'praca' && b.type !== 'inicio') continue;
      let sx, sz, nx = 0, nz = 0;
      if (b.j === 0) { sx = b.cx; sz = -RING; nz = 1; } else if (b.j === N - 1) { sx = b.cx; sz = RING; nz = -1; }
      else if (b.i === N - 1) { sx = RING; sz = b.cz; nx = -1; } else { sx = -RING; sz = b.cz; nx = 1; }
      const tx = -nz, tz = nx; // direção ao longo da rua
      const cx = sx + nx * 13, cz = sz + nz * 13; // centro da pista
      const spots = [];
      for (const [row, n] of [[0, 5], [1, 5]]) for (let k = 0; k < n; k++) {
        const off = (k - (n - 1) / 2) * 3.2 + (row ? 1.6 : 0) * 0;
        const x = cx + tx * off + nx * row * 3.4, z = cz + tz * off + nz * row * 3.4;
        spots.push({ x, z, yaw: Math.atan2(-nx, -nz) });
      }
      // a ordem: frente-centro primeiro (para o jogador)
      const order = [2, 1, 3, 0, 4, 7, 6, 8, 5, 9];
      const st = { x: sx, z: sz, block: b, nome: b.nome || 'Praça Inicial', inicio: b.type === 'inicio', n: [nx, nz], t: [tx, tz], cx, cz, spots: order.map(i => spots[i]) };
      this.stops.push(st);
      const hw = 10, d0 = 6.5, d1 = 21; // retângulo reservado
      const pts = [[sx + nx * d0 + tx * hw, sz + nz * d0 + tz * hw], [sx + nx * d1 - tx * hw, sz + nz * d1 - tz * hw]];
      this.zones.push({ x0: Math.min(pts[0][0], pts[1][0]), x1: Math.max(pts[0][0], pts[1][0]), z0: Math.min(pts[0][1], pts[1][1]), z1: Math.max(pts[0][1], pts[1][1]), st });
    }
  }
  inZone(x, z, m = 0) { return this.zones.some(q => x > q.x0 - m && x < q.x1 + m && z > q.z0 - m && z < q.z1 + m); }

  // ---------------- céu e luz ----------------
  buildSky() {
    const d = this.def, n = this.night;
    const [top, hor] = n ? d.ceuNoite : d.ceuDia;
    const skyMat = this.m(new THREE.ShaderMaterial({
      side: THREE.BackSide, depthWrite: false, fog: false,
      uniforms: { top: { value: new THREE.Color(top) }, hor: { value: new THREE.Color(hor) } },
      vertexShader: 'varying vec3 vP; void main(){ vP = normalize(position); gl_Position = projectionMatrix*modelViewMatrix*vec4(position,1.0);} ',
      fragmentShader: `uniform vec3 top; uniform vec3 hor; varying vec3 vP;
        void main(){ float h = max(vP.y,0.0); vec3 col = mix(hor, top, pow(h,0.55)); if(vP.y<0.0) col = hor*0.8;
          gl_FragColor = vec4(col,1.0);
          #include <colorspace_fragment>
        }`,
    }));
    this.sky = new THREE.Mesh(this.g(new THREE.SphereGeometry(2200, 32, 16)), skyMat);
    this.sky.renderOrder = -10; this.sky.frustumCulled = false; this.group.add(this.sky);
    this.scene.fog = new THREE.FogExp2(hor, d.nevoa * (n ? 1.1 : 1));
    this.scene.background = new THREE.Color(hor);
    this.hemi = new THREE.HemisphereLight(n ? 0x7080c0 : 0xe8f2ff, n ? 0x30283a : 0x8a7a65, n ? 1.0 : 1.9);
    this.sun = new THREE.DirectionalLight(n ? 0xa8b8ff : 0xfff3dc, n ? 0.7 : 2.6);
    this.sun.position.set(-300, 500, -200);
    this.group.add(this.hemi, this.sun, this.sun.target);
    const glow = this.t(TX.radial(n ? '220,230,255' : '255,240,200'));
    const sp = new THREE.Sprite(this.m(new THREE.SpriteMaterial({ map: glow, fog: false, depthWrite: false, transparent: true, blending: THREE.AdditiveBlending })));
    sp.scale.setScalar(n ? 160 : 360); sp.position.set(-700, 900, -500).normalize().multiplyScalar(1800);
    this.skyObj = new THREE.Group(); this.skyObj.add(sp); this.group.add(this.skyObj);
    if (!n) {
      // nuvens
      const cc = document.createElement('canvas'); cc.width = cc.height = 128; const cg = cc.getContext('2d');
      for (let k = 0; k < 14; k++) { const gx = 30 + Math.random() * 68, gy = 50 + Math.random() * 30, gr2 = 18 + Math.random() * 22; const gd = cg.createRadialGradient(gx, gy, 0, gx, gy, gr2); gd.addColorStop(0, 'rgba(255,255,255,0.9)'); gd.addColorStop(1, 'rgba(255,255,255,0)'); cg.fillStyle = gd; cg.fillRect(0, 0, 128, 128); }
      const ct = this.t(new THREE.CanvasTexture(cc)); ct.colorSpace = THREE.SRGBColorSpace;
      const cm = this.m(new THREE.SpriteMaterial({ map: ct, fog: false, depthWrite: false, transparent: true, opacity: 0.9 }));
      this.clouds = [];
      for (let k = 0; k < 36; k++) {
        const sp2 = new THREE.Sprite(cm); const a = Math.random() * 6.28, R = 500 + Math.random() * 1000;
        sp2.position.set(Math.cos(a) * R, 280 + Math.random() * 260, Math.sin(a) * R); sp2.scale.set(260 + Math.random() * 260, 110 + Math.random() * 80, 1);
        this.group.add(sp2); this.clouds.push(sp2);
      }
    }
    if (n) {
      const moon = new THREE.Mesh(this.g(new THREE.CircleGeometry(34, 32)), this.m(new THREE.MeshBasicMaterial({ color: 0xf4f1e6, fog: false })));
      moon.position.copy(sp.position); moon.lookAt(0, 0, 0); this.skyObj.add(moon);
      const NS = 1500, p = new Float32Array(NS * 3);
      for (let i = 0; i < NS; i++) { const th = Math.random() * 6.28, ph = Math.acos(Math.random() * 0.95); p[i * 3] = Math.sin(ph) * Math.cos(th) * 1800; p[i * 3 + 1] = Math.cos(ph) * 1800 + 40; p[i * 3 + 2] = Math.sin(ph) * Math.sin(th) * 1800; }
      const sg = this.g(new THREE.BufferGeometry()); sg.setAttribute('position', new THREE.BufferAttribute(p, 3));
      this.skyObj.add(new THREE.Points(sg, this.m(new THREE.PointsMaterial({ color: 0xffffff, size: 2, sizeAttenuation: false, fog: false }))));
    }
  }
  m(x) { this.disp.push(x); return x; }
  g(x) { this.disp.push(x); return x; }
  t(x) { this.disp.push(x); return x; }
  add(geo, mat) { const me = new THREE.Mesh(geo, mat); this.disp.push(geo); this.group.add(me); me.receiveShadow = true; return me; }

  // ---------------- terreno ----------------
  buildTerrain() {
    const S = HALF + 420, step = 8, n = Math.round(2 * S / step) + 1;
    const g = this.g(new THREE.PlaneGeometry(2 * S, 2 * S, n - 1, n - 1));
    g.rotateX(-Math.PI / 2);
    const pos = g.attributes.position, col = new Float32Array(pos.count * 3);
    const base = new THREE.Color(this.def.chao), sand = new THREE.Color(0xe6d29a);
    for (let i = 0; i < pos.count; i++) {
      const x = pos.getX(i), z = pos.getZ(i);
      const y = this.height(x, z) - (this.inCity(x, z) ? 0.25 : 0.12);
      pos.setY(i, y);
      let c = base.clone().offsetHSL(0, 0, (Math.random() - 0.5) * 0.04);
      if (this.def.mar && y < this.seaY + 3) c = sand.clone();
      col.set([c.r, c.g, c.b], i * 3);
    }
    g.setAttribute('color', new THREE.BufferAttribute(col, 3));
    g.computeVertexNormals();
    this.add(g, this.m(new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 1 })));
    if (this.def.mar) {
      const wc = document.createElement('canvas'); wc.width = wc.height = 128; const wg = wc.getContext('2d'); const id = wg.createImageData(128, 128);
      for (let y = 0; y < 128; y++) for (let x = 0; x < 128; x++) { const v = Math.sin(x / 128 * 6.28 * 3 + Math.sin(y / 128 * 6.28 * 2) * 1.5), w2 = Math.cos(y / 128 * 6.28 * 4 + Math.sin(x / 128 * 6.28) * 2); const i = (y * 128 + x) * 4; id.data[i] = 128 + v * 40; id.data[i + 1] = 128 + w2 * 40; id.data[i + 2] = 255; id.data[i + 3] = 255; }
      wg.putImageData(id, 0, 0);
      this.waterNormal = this.t(new THREE.CanvasTexture(wc)); this.waterNormal.wrapS = this.waterNormal.wrapT = THREE.RepeatWrapping; this.waterNormal.repeat.set(220, 220);
      this.water = this.add(this.g(new THREE.PlaneGeometry(8000, 8000)), this.m(new THREE.MeshStandardMaterial({ color: this.def.mar.cor, roughness: 0.12, metalness: 0.15, normalMap: this.waterNormal, normalScale: new THREE.Vector2(0.6, 0.6) })));
      this.water.rotation.x = -Math.PI / 2; this.water.position.y = this.seaY;
    }
  }

  // ---------------- ruas ----------------
  streetGeo(axis, c) {
    const pos = [], uv = [], idx = []; let row = 0;
    const from = -HALF - RW, to = HALF + RW;
    for (let s = from; s <= to + 0.01; s += 3) {
      const ss = Math.min(s, to);
      for (const side of [-1, 1]) {
        const x = axis === 'x' ? ss : c + side * RW, z = axis === 'x' ? c + side * RW : ss;
        pos.push(x, this.height(x, z), z); uv.push(side < 0 ? 0 : 1, ss / 14);
      }
      if (row > 0) { const p = (row - 1) * 2; if (axis === 'x') idx.push(p, p + 1, p + 2, p + 1, p + 3, p + 2); else idx.push(p, p + 2, p + 1, p + 1, p + 2, p + 3); }
      row++;
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3)); g.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2));
    g.setIndex(idx); g.computeVertexNormals();
    return g;
  }
  buildStreets() {
    const tex = this.t(TX.asphaltStreet());
    const mx = this.m(new THREE.MeshStandardMaterial({ map: tex, roughness: 0.9 }));
    const mz = this.m(new THREE.MeshStandardMaterial({ map: tex, roughness: 0.9, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 }));
    const gx = [], gz = [];
    for (let k = 0; k <= N; k++) { gx.push(this.streetGeo('x', lineC(k))); gz.push(this.streetGeo('z', lineC(k))); }
    this.add(mergeGeometries(gx), mx); this.add(mergeGeometries(gz), mz);
    gx.concat(gz).forEach(g => g.dispose());
  }

  // ---------------- quarteirões e prédios ----------------
  slabGeo(b, yOff = 0.2, inset = 0, uvs = 3) {
    const x0 = b.x0 + inset, x1 = b.x1 - inset, z0 = b.z0 + inset, z1 = b.z1 - inset;
    const nx = Math.ceil((x1 - x0) / 3), nz = Math.ceil((z1 - z0) / 3);
    const g = new THREE.PlaneGeometry(x1 - x0, z1 - z0, nx, nz); g.rotateX(-Math.PI / 2); g.translate((x0 + x1) / 2, 0, (z0 + z1) / 2);
    const p = g.attributes.position, uv = g.attributes.uv;
    for (let i = 0; i < p.count; i++) { const x = p.getX(i), z = p.getZ(i); p.setY(i, this.height(x, z) + yOff); uv.setXY(i, x / uvs, z / uvs); }
    g.computeVertexNormals();
    return g;
  }
  curbGeo(b) {
    const pts = [[b.x0, b.z0], [b.x1, b.z0], [b.x1, b.z1], [b.x0, b.z1], [b.x0, b.z0]], pos = [], idx = [];
    let v = 0;
    for (let e = 0; e < 4; e++) {
      const [ax, az] = pts[e], [bx, bz] = pts[e + 1], L = Math.hypot(bx - ax, bz - az), n = Math.ceil(L / 3);
      for (let k = 0; k <= n; k++) { const x = ax + (bx - ax) * k / n, z = az + (bz - az) * k / n, h = this.height(x, z); pos.push(x, h - 0.05, z, x, h + 0.2, z); if (k) idx.push(v - 2, v, v - 1, v - 1, v, v + 1); v += 2; }
    }
    const g = new THREE.BufferGeometry(); g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3)); g.setIndex(idx); g.computeVertexNormals();
    return g;
  }
  buildBlocks() {
    const d = this.def, Pd = d.predios, r = this.r;
    const swTex = this.t(TX.sidewalk(d.calcada)), petit = this.t(TX.sidewalk('petit')), grassT = this.t(TX.grass());
    const slabs = [], plazas = [], curbs = [], grass = [];
    for (const b of this.blocks) {
      (b.type === 'inicio' || b.type === 'praca' ? plazas : slabs).push(this.slabGeo(b));
      curbs.push(this.curbGeo(b));
      if (b.type === 'parque' || b.type === 'parkour' || b.type === 'marco') grass.push(this.slabGeo(b, 0.23, SW, 6));
    }
    this.add(mergeGeometries(slabs), this.m(new THREE.MeshStandardMaterial({ map: swTex, color: 0xd8d4cc, roughness: 0.9 })));
    this.add(mergeGeometries(plazas), this.m(new THREE.MeshStandardMaterial({ map: petit, color: 0xb8b2a8, roughness: 0.9 })));
    this.add(mergeGeometries(curbs), this.m(new THREE.MeshStandardMaterial({ color: 0xb8b5ad, roughness: 0.8, side: THREE.DoubleSide })));
    if (grass.length) this.add(mergeGeometries(grass), this.m(new THREE.MeshStandardMaterial({ map: grassT, roughness: 1 })));
    [...slabs, ...plazas, ...curbs, ...grass].forEach(g => g.dispose());

    const fac = TX.facade(Pd.tipo); this.t(fac.map); this.t(fac.emissive);
    const mat = this.m(new THREE.MeshStandardMaterial({ map: fac.map, vertexColors: true, roughness: 0.85, emissive: 0xffffff, emissiveMap: fac.emissive, emissiveIntensity: this.night ? 0.75 : 0 }));
    const roofMat = this.m(new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.9, side: THREE.DoubleSide }));
    const acc = new Acc(), roofs = new Acc(), shops = new Acc();
    const shopFac = TX.facade('casarao'); this.t(shopFac.map); this.t(shopFac.emissive);
    for (const b of this.blocks) {
      if (b.type !== 'normal') continue;
      const G = Pd.grade, x0 = b.x0 + SW + 0.6, x1 = b.x1 - SW - 0.6, z0 = b.z0 + SW + 0.6, z1 = b.z1 - SW - 0.6;
      const cuts = (a, bb) => { const out = [a]; for (let k = 1; k < G; k++) out.push(a + (bb - a) * (k / G + (r() - 0.5) * 0.12)); out.push(bb); return out; };
      const xs = cuts(x0, x1), zs = cuts(z0, z1);
      const lowLot = Math.floor(r() * 4); let lotN = 0;
      for (let a = 0; a < G; a++) for (let c = 0; c < G; c++) {
        const edge = a === 0 || c === 0 || a === G - 1 || c === G - 1;
        if (!edge) continue; // miolo do quarteirão (não aparece)
        const lx0 = xs[a] + 0.3, lx1 = xs[a + 1] - 0.3, lz0 = zs[c] + 0.3, lz1 = zs[c + 1] - 0.3;
        const w = lx1 - lx0, dd = lz1 - lz0, cx = (lx0 + lx1) / 2, cz = (lz0 + lz1) / 2;
        const hs = [this.height(lx0, lz0), this.height(lx1, lz0), this.height(lx0, lz1), this.height(lx1, lz1)];
        const base = Math.min(...hs) - 1.5, topG = Math.max(...hs) + 0.2;
        const isLow = Pd.tipo !== 'casarao' && (lotN++ % 4 === lowLot) && edge;
        let h = isLow ? range(r, 3.2, 4.4) : range(r, Pd.altMin, Pd.altMax);
        if (isLow) {
          // lojinha baixa: dá pra escalar e correr no telhado (parkour)
          const top = topG + h;
          shops.add(buildingBox(w, top - base, dd, 8), M4(cx, base, cz), pick(r, [0xffe0b2, 0xb3e5fc, 0xf8bbd0, 0xc8e6c9, 0xfff9c4]));
          roofs.add(new THREE.BoxGeometry(w + 0.2, 0.3, dd + 0.2).translate(0, 0.15, 0), M4(cx, top, cz), 0x8d8d8d);
          this.addCollider({ x0: lx0, x1: lx1, z0: lz0, z1: lz1, top: top + 0.3, walk: true, base, kind: 'loja' });
          // toldo listrado virado para a rua
          const awn = pick(r, [0xe53935, 0x1e88e5, 0x43a047, 0xfb8c00, 0x8e24aa]);
          const faces = [];
          if (a === 0) faces.push([lx0, cz, -1, 0, dd]); if (a === G - 1) faces.push([lx1, cz, 1, 0, dd]);
          if (c === 0) faces.push([cx, lz0, 0, -1, w]); if (c === G - 1) faces.push([cx, lz1, 0, 1, w]);
          for (const [fx, fz, nx, nz, len] of faces) {
            const yA = this.height(fx, fz) + 2.9;
            for (let k = 0; k < 6; k++) {
              const off = (k - 2.5) * len / 6 * 0.9;
              const px = fx + nx * 0.9 + (nz !== 0 ? off : 0), pz = fz + nz * 0.9 + (nx !== 0 ? off : 0);
              const g2 = new THREE.BoxGeometry(nx !== 0 ? 1.9 : len * 0.9 / 6, 0.08, nz !== 0 ? 1.9 : len * 0.9 / 6);
              g2.rotateX(nz * 0.35); g2.rotateZ(-nx * 0.35);
              roofs.add(g2, M4(px, yA, pz), k % 2 ? 0xffffff : awn);
            }
          }
          continue;
        }
        const top = topG + h;
        acc.add(buildingBox(w, top - base, dd, Pd.tipo === 'casarao' ? 8 : 12), M4(cx, base, cz), pick(r, Pd.cores));
        if (Pd.tipo === 'casarao') roofs.add(prism(w, dd, 2.2), M4(cx, top, cz), 0xb5532d);
        else if (Pd.tipo === 'torre' && r() < 0.3) { roofs.add(new THREE.CylinderGeometry(0.15, 0.15, 8, 5).translate(0, 4, 0), M4(cx, top, cz), 0x888888); this.blinkers.push(new THREE.Vector3(cx, top + 8.2, cz)); }
        else if (r() < 0.4) roofs.add(new THREE.BoxGeometry(w * 0.3, 2.5, dd * 0.3).translate(0, 1.25, 0), M4(cx, top, cz), 0x9a9a9a);
        this.addCollider({ x0: lx0, x1: lx1, z0: lz0, z1: lz1, top, walk: true, base, kind: 'predio' });
      }
    }
    const g1 = acc.build(); if (g1) this.add(g1, mat).castShadow = true;
    const g2 = roofs.build(); if (g2) this.add(g2, roofMat);
    const g3 = shops.build(); if (g3) this.add(g3, this.m(new THREE.MeshStandardMaterial({ map: shopFac.map, vertexColors: true, roughness: 0.85, emissive: 0xffffff, emissiveMap: shopFac.emissive, emissiveIntensity: this.night ? 0.7 : 0 })));
    if (this.blinkers.length) this.refreshBlinkers();
  }
  refreshBlinkers() {
    if (this.blinkMesh) this.group.remove(this.blinkMesh);
    const bm = new THREE.InstancedMesh(this.g(new THREE.SphereGeometry(0.6, 6, 4)), this.m(new THREE.MeshBasicMaterial({ color: 0xff2020 })), this.blinkers.length);
    this.blinkers.forEach((p, i) => bm.setMatrixAt(i, new THREE.Matrix4().makeTranslation(p.x, p.y, p.z)));
    this.blinkMesh = bm; this.group.add(bm);
  }

  // ---------------- postes ----------------
  buildLamps() {
    const list = [];
    for (const b of this.blocks) {
      const edges = [[b.x0, b.z0, b.x1, b.z0, 0, -1], [b.x1, b.z0, b.x1, b.z1, 1, 0], [b.x1, b.z1, b.x0, b.z1, 0, 1], [b.x0, b.z1, b.x0, b.z0, -1, 0]];
      for (const [ax, az, bx, bz, nx, nz] of edges) for (const t of [0.2, 0.5, 0.8]) {
        const x = ax + (bx - ax) * t - nx * 1.0, z = az + (bz - az) * t - nz * 1.0;
        list.push([x, z, Math.atan2(nx, nz)]);
      }
    }
    for (let i = list.length - 1; i >= 0; i--) if (this.inZone(list[i][0], list[i][1], 0.5)) list.splice(i, 1);
    this.lamps = list;
    const poleGeo = this.g(mergeGeometries([new THREE.CylinderGeometry(0.12, 0.16, 8, 6).translate(0, 4, 0), new THREE.BoxGeometry(0.12, 0.12, 2.4).translate(0, 7.9, 1.15)]));
    const headGeo = this.g(new THREE.BoxGeometry(0.4, 0.18, 0.8).translate(0, 7.8, 2.25));
    const poles = new THREE.InstancedMesh(poleGeo, this.m(new THREE.MeshStandardMaterial({ color: 0x55585e, roughness: 0.6, metalness: 0.4 })), list.length);
    const heads = new THREE.InstancedMesh(headGeo, this.m(new THREE.MeshBasicMaterial({ color: this.night ? 0xfff0c8 : 0xcfd2d6 })), list.length);
    const pools = this.night ? new THREE.InstancedMesh(this.g(new THREE.PlaneGeometry(10, 10).rotateX(-Math.PI / 2)),
      this.m(new THREE.MeshBasicMaterial({ map: this.t(TX.radial('255,214,150')), transparent: true, opacity: 0.22, depthWrite: false, blending: THREE.AdditiveBlending })), list.length) : null;
    list.forEach(([x, z, a], i) => {
      const y = this.floorY(x, z);
      const m = M4(x, y, z, a); poles.setMatrixAt(i, m); heads.setMatrixAt(i, m);
      this.addCollider({ x0: x - 0.18, x1: x + 0.18, z0: z - 0.18, z1: z + 0.18, top: y + 8, walk: false, kind: 'poste' });
      if (pools) { const px = x + Math.sin(a) * 2.3, pz = z + Math.cos(a) * 2.3; pools.setMatrixAt(i, M4(px, this.floorY(px, pz) + 0.06, pz)); }
    });
    this.group.add(poles, heads); if (pools) { pools.renderOrder = 2; this.group.add(pools); }
  }

  // ---------------- árvores ----------------
  treeGeos(type) {
    if (type === 'coqueiro') {
      const trunk = new THREE.CylinderGeometry(0.16, 0.26, 9, 6).translate(0, 4.5, 0);
      const leaves = []; for (let k = 0; k < 7; k++) { const l = new THREE.ConeGeometry(0.45, 3.6, 4).scale(1, 1, 0.25).translate(0, 1.8, 0); l.rotateZ(-1.9); l.rotateY(k / 7 * Math.PI * 2); l.translate(0, 9, 0); leaves.push(l); }
      return [trunk, mergeGeometries(leaves), [0x3f8f3a, 0x4ea83f, 0x2f7a33]];
    }
    if (type === 'araucaria') {
      const trunk = new THREE.CylinderGeometry(0.22, 0.32, 13, 6).translate(0, 6.5, 0);
      return [trunk, mergeGeometries([new THREE.ConeGeometry(4.2, 1.6, 9).rotateX(Math.PI).translate(0, 12.6, 0), new THREE.ConeGeometry(3.0, 1.4, 9).rotateX(Math.PI).translate(0, 13.8, 0)]), [0x24502c, 0x2c5e33]];
    }
    return [new THREE.CylinderGeometry(0.18, 0.26, 3.2, 6).translate(0, 1.6, 0), new THREE.IcosahedronGeometry(2.1, 1).scale(1, 0.85, 1).translate(0, 4.4, 0),
      type === 'ipe' ? [0xffd400, 0xff6fb5, 0xb05bd8, 0x4c9a3c] : [0x3e8a3a, 0x4f9a40, 0x2f7334]];
  }
  buildTrees() {
    const r = this.r, spots = [];
    for (const b of this.blocks) {
      const parky = b.type === 'parque' || b.type === 'praca' || b.type === 'marco';
      if (b.type === 'normal' || b.type === 'parkour') {
        const edges = [[b.x0, b.z0, b.x1, b.z0, 0, -1], [b.x1, b.z0, b.x1, b.z1, 1, 0], [b.x1, b.z1, b.x0, b.z1, 0, 1], [b.x0, b.z1, b.x0, b.z0, -1, 0]];
        for (const [ax, az, bx, bz, nx, nz] of edges) for (const t of [0.35, 0.65]) if (r() < 0.7) spots.push([ax + (bx - ax) * t - nx * 1.2, az + (bz - az) * t - nz * 1.2]);
      }
      if (parky) for (let k = 0; k < (b.type === 'parque' ? 22 : 10); k++) {
        const x = range(r, b.x0 + 6, b.x1 - 6), z = range(r, b.z0 + 6, b.z1 - 6);
        if (b.type === 'praca' && Math.hypot(x - b.cx, z - b.cz) < 22) continue;
        spots.push([x, z]);
      }
    }
    // árvores fora da cidade
    for (let k = 0; k < 160; k++) {
      const a = r() * 6.28, R = HALF + range(r, 30, 300), x = Math.cos(a) * R, z = Math.sin(a) * R;
      if (this.def.mar && this.height(x, z) < this.seaY + 2) continue;
      spots.push([x, z]);
    }
    if (this.def.mar && this.def.arvores === 'coqueiro') for (let k = 0; k < 40; k++) {
      const t = range(r, -HALF, HALF), off = range(r, 14, 40);
      const [x, z] = this.def.mar.lado === 'sul' ? [t, -HALF - off] : [-HALF - off, t];
      spots.push([x, z]);
    }
    for (let i = spots.length - 1; i >= 0; i--) if (this.inZone(spots[i][0], spots[i][1], 2)) spots.splice(i, 1);
    const [trunkGeo, crownGeo, colors] = this.treeGeos(this.def.arvores);
    this.g(trunkGeo); this.g(crownGeo);
    const trunks = new THREE.InstancedMesh(trunkGeo, this.m(new THREE.MeshStandardMaterial({ color: 0x6b4a2f, roughness: 1 })), spots.length);
    const crowns = new THREE.InstancedMesh(crownGeo, this.m(new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.9, flatShading: true })), spots.length);
    spots.forEach(([x, z], i) => {
      const y = this.inCity(x, z) ? this.floorY(x, z) + (this.onRoad(x, z) ? 0 : 0.03) : this.height(x, z) - 0.12;
      const m = M4(x, y, z, r() * 6.28, 1, 1, 1).multiply(new THREE.Matrix4().makeScale(...Array(3).fill(range(r, 0.8, 1.2))));
      trunks.setMatrixAt(i, m); crowns.setMatrixAt(i, m); crowns.setColorAt(i, _c.set(pick(r, colors)));
      if (this.inCity(x, z)) this.addCollider({ x0: x - 0.3, x1: x + 0.3, z0: z - 0.3, z1: z + 0.3, top: y + 4, walk: false, kind: 'arvore' });
    });
    trunks.castShadow = crowns.castShadow = true;
    this.group.add(trunks, crowns);
  }

  // ---------------- obstáculos de parkour + notas ----------------
  buildProps() {
    const r = this.r, acc = new Acc();
    this.notes = [];
    const note = (x, z, y, gold = false) => this.notes.push({ p: new THREE.Vector3(x, y, z), gold, taken: false });
    const carColors = [0xe53935, 0x1e88e5, 0xfdd835, 0x43a047, 0xffffff, 0x8e24aa, 0x212121, 0xff7043];
    const car = (x, z, alongX) => {
      const y = this.floorY(x, z), ry = alongX ? Math.PI / 2 : 0, col = pick(r, carColors);
      const m = M4(x, y, z, ry);
      acc.add(new THREE.BoxGeometry(1.9, 0.75, 4.2).translate(0, 0.65, 0), m, col);
      acc.add(new THREE.BoxGeometry(1.72, 0.5, 2.3).translate(0, 1.25, -0.2), m, 0x1c2833);
      acc.add(new THREE.BoxGeometry(1.74, 0.08, 2.35).translate(0, 1.52, -0.2), m, col);
      for (const [wx, wz] of [[-0.9, 1.35], [0.9, 1.35], [-0.9, -1.35], [0.9, -1.35]]) acc.add(new THREE.CylinderGeometry(0.34, 0.34, 0.26, 10).rotateZ(Math.PI / 2).translate(wx, 0.34, wz), m, 0x111111);
      const [hw, hl] = alongX ? [2.1, 0.95] : [0.95, 2.1];
      this.addCollider({ x0: x - hw, x1: x + hw, z0: z - hl, z1: z + hl, top: y + 1.5, walk: true, kind: 'carro', truck: true });
      for (let k = -1; k <= 1; k++) note(x + (alongX ? k * 1.5 : 0), z + (alongX ? 0 : k * 1.5), y + 2.6);
    };
    const bench = (x, z, alongX) => {
      const y = this.floorY(x, z), m = M4(x, y, z, alongX ? Math.PI / 2 : 0);
      acc.add(new THREE.BoxGeometry(0.8, 0.08, 2.4).translate(0, 0.42, 0), m, 0x8d5a2b);
      acc.add(new THREE.BoxGeometry(0.7, 0.4, 0.12).translate(0, 0.2, -1), m, 0x444444); acc.add(new THREE.BoxGeometry(0.7, 0.4, 0.12).translate(0, 0.2, 1), m, 0x444444);
      const [hw, hl] = alongX ? [1.2, 0.4] : [0.4, 1.2];
      this.addCollider({ x0: x - hw, x1: x + hw, z0: z - hl, z1: z + hl, top: y + 0.46, walk: true, kind: 'banco' });
    };
    const ramp = (x, z, axis, dir) => {
      const y = this.floorY(x, z), L = 4.6, W = 2.4, Hh = 1.2;
      const g = new THREE.BufferGeometry();
      const p = [-W / 2, 0, -L / 2, W / 2, 0, -L / 2, W / 2, Hh, L / 2, -W / 2, 0, -L / 2, W / 2, Hh, L / 2, -W / 2, Hh, L / 2,
        -W / 2, 0, L / 2, W / 2, Hh, L / 2, W / 2, 0, L / 2, -W / 2, 0, L / 2, -W / 2, Hh, L / 2, W / 2, Hh, L / 2,
        -W / 2, 0, -L / 2, -W / 2, Hh, L / 2, -W / 2, 0, L / 2, W / 2, 0, -L / 2, W / 2, 0, L / 2, W / 2, Hh, L / 2];
      g.setAttribute('position', new THREE.Float32BufferAttribute(p, 3)); g.computeVertexNormals();
      const ry = axis === 'x' ? (dir > 0 ? Math.PI / 2 : -Math.PI / 2) : (dir > 0 ? 0 : Math.PI);
      acc.add(g, M4(x, y, z, ry), 0xffc400);
      const [hw, hl] = axis === 'x' ? [L / 2, W / 2] : [W / 2, L / 2];
      this.addCollider({ x0: x - hw, x1: x + hw, z0: z - hl, z1: z + hl, top: y + Hh, base: y, walk: true, ramp: { axis, dir }, kind: 'rampa' });
      for (let k = 1; k <= 8; k++) { const t = k * 0.13, d = L / 2 + t * 8; note(x + (axis === 'x' ? dir * d : 0), z + (axis === 'z' ? dir * d : 0), y + Hh + 12 * t - 12 * t * t + 0.6, k === 4); }
    };
    const kiosk = (x, z) => {
      const y = this.floorY(x, z), m = M4(x, y, z);
      acc.add(new THREE.BoxGeometry(2.6, 2.4, 2.6).translate(0, 1.2, 0), m, pick(r, [0x2e7d32, 0x1565c0, 0xc62828, 0xef6c00]));
      acc.add(new THREE.BoxGeometry(3.2, 0.25, 3.2).translate(0, 2.55, 0), m, 0xeeeeee);
      this.addCollider({ x0: x - 1.6, x1: x + 1.6, z0: z - 1.6, z1: z + 1.6, top: y + 2.68, walk: true, kind: 'quiosque' });
      note(x, z, y + 3.6, true);
    };
    const boxes = (x, z, n) => {
      let y = this.floorY(x, z);
      for (let k = 0; k < n; k++) {
        acc.add(new THREE.BoxGeometry(1.4, 1.4, 1.4).translate(0, 0.7, 0), M4(x + k * 1.0, y, z, 0), 0xa1662f);
        this.addCollider({ x0: x + k * 1.0 - 0.7, x1: x + k * 1.0 + 0.7, z0: z - 0.7, z1: z + 0.7, top: y + 1.4 * (1 + Math.min(k, 1)), walk: true, kind: 'caixa' });
        if (k === 1) acc.add(new THREE.BoxGeometry(1.4, 1.4, 1.4).translate(0, 0.7, 0), M4(x + 1.0, y + 1.4, z, 0.2), 0xb87a3f);
      }
    };
    const container = (x, z, alongX, levels = 1, col = 0xc62828) => {
      for (let k = 0; k < levels; k++) {
        const y = this.floorY(x, z) + k * 2.6;
        acc.add(new THREE.BoxGeometry(2.4, 2.6, 6).translate(0, 1.3, 0), M4(x, y, z, alongX ? Math.PI / 2 : 0), k % 2 ? 0x1565c0 : col);
      }
      const [hw, hl] = alongX ? [3, 1.2] : [1.2, 3];
      this.addCollider({ x0: x - hw, x1: x + hw, z0: z - hl, z1: z + hl, top: this.floorY(x, z) + 2.6 * levels, walk: true, kind: 'container' });
      note(x, z, this.floorY(x, z) + 2.6 * levels + 1, levels > 1);
    };
    // ruas internas (o anel fica livre para a carreta)
    for (const axis of ['x', 'z']) for (let k = 1; k < N; k++) for (let seg = 0; seg < N; seg++) {
      const c = lineC(k), a = lineC(seg) + RW + 6, b = lineC(seg + 1) - RW - 6;
      const P2 = (t, lat) => axis === 'x' ? [a + (b - a) * t, c + lat] : [c + lat, a + (b - a) * t];
      let pat = Math.floor(r() * 6);
      if (isTraffic(axis, k) && (pat === 1 || pat === 3 || pat === 2 || pat === 4)) pat = 0; // rua de trânsito: só carros estacionados
      if (pat === 0 || pat === 3) { const [x, z] = P2(0.3, 5.2); car(x, z, axis === 'x'); const [x2, z2] = P2(0.62, 5.2); car(x2, z2, axis === 'x'); }
      if (pat === 1 || pat === 3) { const [x, z] = P2(0.5, -1.75); ramp(x, z, axis, 1); }
      if (pat === 2) { const [x, z] = P2(0.45, -5.2); car(x, z, axis === 'x'); const [x2, z2] = P2(0.55, 1.75); boxes(x2, z2, 2); }
      if (pat === 4) { const [x, z] = P2(0.5, 1.75); ramp(x, z, axis, -1); const [x2, z2] = P2(0.25, -5.2); car(x2, z2, axis === 'x'); }
      // notas em linha
      const lat = pick(r, [-3.5, 0, 3.5]);
      for (let t = 0.08; t < 0.95; t += 0.09) { const [x, z] = P2(t, lat); note(x, z, this.floorY(x, z) + 0.9); }
      // bancos na calçada
      if (r() < 0.6) { const [x, z] = P2(0.5, RW + 1.6); bench(x, z, axis === 'x'); }
      if (r() < 0.35) { const [x, z] = P2(0.15, -(RW + 1.8)); kiosk(x, z); }
    }
    // notas no anel (calçada interna)
    for (let t = -HALF + 20; t < HALF - 20; t += 9) {
      for (const [x, z] of [[t, -HALF + RW + 1.6], [t, HALF - RW - 1.6], [-HALF + RW + 1.6, t], [HALF - RW - 1.6, t]]) note(x, z, this.floorY(x, z) + 0.9);
    }
    // parque de parkour
    const pk = this.blocks.find(b => b.type === 'parkour');
    if (pk) {
      const { cx, cz } = pk;
      container(cx - 16, cz - 12, true, 1); container(cx - 8, cz - 12, true, 2, 0x2e7d32); container(cx + 2, cz - 12, true, 3, 0xef6c00);
      ramp(cx - 22, cz + 4, 'x', 1); boxes(cx - 12, cz + 4, 3); kiosk(cx + 4, cz + 4);
      ramp(cx + 12, cz + 14, 'z', -1); container(cx + 18, cz - 2, false, 2, 0x6a1b9a);
      bench(cx - 4, cz + 18, true); bench(cx + 2, cz + 18, true);
      for (let k = 0; k < 4; k++) { const y = this.floorY(cx + 14, cz + 18) ; acc.add(new THREE.BoxGeometry(2, 0.5 + k * 0.6, 2).translate(0, (0.5 + k * 0.6) / 2, 0), M4(cx + 10 + k * 2.2, y, cz + 22), 0x9e9e9e); this.addCollider({ x0: cx + 9 + k * 2.2, x1: cx + 11 + k * 2.2, z0: cz + 21, z1: cz + 23, top: y + 0.5 + k * 0.6, walk: true, kind: 'degrau' }); }
    }
    const g = acc.build(); if (g) this.add(g, this.m(new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.6, metalness: 0.1 }))).castShadow = true;
    this.buildNotes();
    this.buildStreetDetails();
  }
  buildStreetDetails() {
    const r = this.r;
    // faixa de pedestre
    const zc = document.createElement('canvas'); zc.width = 128; zc.height = 64; const zg = zc.getContext('2d');
    zg.clearRect(0, 0, 128, 64); zg.fillStyle = 'rgba(240,240,235,0.95)'; for (let x = 4; x < 128; x += 16) zg.fillRect(x, 0, 9, 64);
    const zt = this.t(new THREE.CanvasTexture(zc)); zt.colorSpace = THREE.SRGBColorSpace;
    const zgeo = this.g(new THREE.PlaneGeometry(12.5, 3).rotateX(-Math.PI / 2));
    const spots = [];
    for (let i = 0; i <= N; i++) for (let j = 0; j <= N; j++) {
      const x = lineC(i), z = lineC(j);
      for (const [dx, dz, ry] of [[RW + 2, 0, Math.PI / 2], [-RW - 2, 0, Math.PI / 2], [0, RW + 2, 0], [0, -RW - 2, 0]]) {
        const px = x + dx, pz = z + dz; if (Math.abs(px) > HALF + 1 || Math.abs(pz) > HALF + 1) continue;
        spots.push([px, pz, ry]);
      }
    }
    const zm = new THREE.InstancedMesh(zgeo, this.m(new THREE.MeshStandardMaterial({ map: zt, transparent: true, roughness: 0.8, polygonOffset: true, polygonOffsetFactor: -4, polygonOffsetUnits: -4 })), spots.length);
    spots.forEach(([x, z, ry], i) => zm.setMatrixAt(i, M4(x, this.height(x, z) + 0.03, z, ry)));
    zm.receiveShadow = true; this.group.add(zm);
    // lixeiras e hidrantes perto dos postes
    const bins = [], hyd = [];
    for (const [x, z, a] of this.lamps) { if (r() < 0.35) bins.push([x + Math.cos(a) * 1.4, z - Math.sin(a) * 1.4]); else if (r() < 0.15) hyd.push([x - Math.cos(a) * 1.6, z + Math.sin(a) * 1.6]); }
    const binGeo = this.g(mergeGeometries([new THREE.CylinderGeometry(0.32, 0.28, 0.9, 10).translate(0, 0.45, 0), new THREE.CylinderGeometry(0.35, 0.35, 0.08, 10).translate(0, 0.94, 0)]));
    const bm = new THREE.InstancedMesh(binGeo, this.m(new THREE.MeshStandardMaterial({ color: 0x2e7d32, roughness: 0.5, metalness: 0.2 })), Math.max(1, bins.length));
    bins.forEach(([x, z], i) => { const y = this.floorY(x, z); bm.setMatrixAt(i, M4(x, y, z)); this.addCollider({ x0: x - 0.33, x1: x + 0.33, z0: z - 0.33, z1: z + 0.33, top: y + 0.98, walk: true, kind: 'lixeira' }); });
    const hydGeo = this.g(mergeGeometries([new THREE.CylinderGeometry(0.14, 0.17, 0.6, 8).translate(0, 0.3, 0), new THREE.SphereGeometry(0.15, 8, 6).translate(0, 0.62, 0), new THREE.CylinderGeometry(0.06, 0.06, 0.36, 6).rotateZ(Math.PI / 2).translate(0, 0.4, 0)]));
    const hm = new THREE.InstancedMesh(hydGeo, this.m(new THREE.MeshStandardMaterial({ color: 0xd32f2f, roughness: 0.4, metalness: 0.3 })), Math.max(1, hyd.length));
    hyd.forEach(([x, z], i) => { const y = this.floorY(x, z); hm.setMatrixAt(i, M4(x, y, z)); });
    bm.castShadow = hm.castShadow = true; this.group.add(bm, hm);
  }
  buildNotes() {
    const g = this.g(mergeGeometries([new THREE.SphereGeometry(0.2, 10, 8).scale(1.25, 0.9, 0.7), new THREE.CylinderGeometry(0.035, 0.035, 0.6, 6).translate(0.22, 0.3, 0), new THREE.BoxGeometry(0.22, 0.08, 0.05).rotateZ(-0.5).translate(0.32, 0.53, 0)]));
    const star = new THREE.Shape();
    for (let i = 0; i < 10; i++) { const a = i / 10 * Math.PI * 2 + Math.PI / 2, R = i % 2 ? 0.25 : 0.6; i ? star.lineTo(Math.cos(a) * R, Math.sin(a) * R) : star.moveTo(Math.cos(a) * R, Math.sin(a) * R); }
    const sg = this.g(new THREE.ExtrudeGeometry(star, { depth: 0.15, bevelEnabled: false }).translate(0, 0, -0.075));
    const nn = this.notes.filter(n => !n.gold), gg = this.notes.filter(n => n.gold);
    this.noteMesh = new THREE.InstancedMesh(g, this.m(new THREE.MeshStandardMaterial({ color: 0xffd54f, emissive: 0xffa000, emissiveIntensity: 0.9, metalness: 0.3, roughness: 0.3 })), Math.max(1, nn.length));
    this.starMesh = new THREE.InstancedMesh(sg, this.m(new THREE.MeshStandardMaterial({ color: 0xfff176, emissive: 0xffeb3b, emissiveIntensity: 1.4, metalness: 0.4, roughness: 0.2 })), Math.max(1, gg.length));
    nn.forEach((n, i) => { n.mesh = this.noteMesh; n.i = i; }); gg.forEach((n, i) => { n.mesh = this.starMesh; n.i = i; });
    for (const m of [this.noteMesh, this.starMesh]) { m.instanceMatrix.setUsage(THREE.DynamicDrawUsage); m.frustumCulled = false; this.group.add(m); }
    this.notesTotal = this.notes.length;
  }
  collect(pos, centerY, onTake) {
    for (const n of this.notes) {
      if (n.taken) continue;
      const dx = n.p.x - pos.x, dz = n.p.z - pos.z;
      if (dx * dx + dz * dz > 1.3 || Math.abs(n.p.y - centerY) > 1.35) continue;
      n.taken = true; n.takenT = 0; onTake(n);
    }
  }
  updateNotes(t, dt) {
    const m = new THREE.Matrix4(), q = new THREE.Quaternion(), sc = new THREE.Vector3(), p = new THREE.Vector3();
    for (const n of this.notes) {
      let k = 1, y = n.p.y + Math.sin(t * 3 + n.p.x) * 0.08;
      if (n.taken) { if (n.takenT > 0.5) { if (!n.hidden) { n.hidden = true; m.makeScale(0, 0, 0); n.mesh.setMatrixAt(n.i, m); } continue; } n.takenT += dt; k = Math.max(0, 1 - n.takenT * 4); y += n.takenT * 4; }
      q.setFromAxisAngle(UP, t * 3 + n.p.x * 0.3); sc.setScalar(k * (n.gold ? 1.1 : 1));
      m.compose(p.set(n.p.x, y, n.p.z), q, sc); n.mesh.setMatrixAt(n.i, m);
    }
    this.noteMesh.instanceMatrix.needsUpdate = true; this.starMesh.instanceMatrix.needsUpdate = true;
  }

  // ---------------- praças ----------------
  buildPlazas() {
    const acc = new Acc(), r = this.r, colors = [0xff3d7f, 0xffc400, 0x00c853, 0x2979ff, 0xff6d00, 0xaa00ff];
    this.beams = [];
    const beamTex = this.t(TX.beamTexture());
    const flagsPos = [];
    for (const b of this.blocks) {
      if (b.type !== 'praca' && b.type !== 'inicio') continue;
      const y = this.floorY(b.cx, b.cz);
      // coreto / palco no centro (dá pra subir)
      acc.add(new THREE.CylinderGeometry(7, 7.4, 1.0, 20).translate(0, 0.5, 0), M4(b.cx, y, b.cz), 0xe0d6c2);
      acc.add(new THREE.CylinderGeometry(7.6, 7.6, 0.2, 20).translate(0, 1.1, 0), M4(b.cx, y, b.cz), 0xff3d7f);
      this.addCollider({ x0: b.cx - 6.5, x1: b.cx + 6.5, z0: b.cz - 6.5, z1: b.cz + 6.5, top: y + 1.2, walk: true, kind: 'palco' });
      for (const [dx, dz] of [[-6, -6], [6, -6], [-6, 6], [6, 6]]) acc.add(new THREE.CylinderGeometry(0.2, 0.2, 5, 6).translate(0, 3.7, 0), M4(b.cx + dx, y, b.cz + dz), 0xffffff);
      acc.add(new THREE.ConeGeometry(9.5, 3.2, 8).translate(0, 7.8, 0), M4(b.cx, y, b.cz, Math.PI / 8), b.type === 'inicio' ? 0x00c853 : 0x2979ff);
      // bandeirinhas
      for (let k = 0; k < 8; k++) {
        const a = k / 8 * Math.PI * 2, R = 28;
        const p0 = new THREE.Vector3(b.cx, y + 8.6, b.cz), p1 = new THREE.Vector3(b.cx + Math.cos(a) * R, y + 5.5, b.cz + Math.sin(a) * R);
        for (let f = 1; f < 14; f++) { const t = f / 14, q = p0.clone().lerp(p1, t); q.y -= Math.sin(t * Math.PI) * 1.2; flagsPos.push([q, Math.atan2(Math.cos(a), -Math.sin(a)), colors[(f + k) % 6]]); }
        if (!this.inZone(p1.x, p1.z, 1)) acc.add(new THREE.CylinderGeometry(0.1, 0.1, 5.5, 5).translate(0, 2.75, 0), M4(p1.x, this.floorY(p1.x, p1.z), p1.z), 0x777777);
      }
      // torres de luz
      for (const [dx, dz] of [[-24, -24], [24, -24], [-24, 24], [24, 24]]) {
        const x = b.cx + dx, z = b.cz + dz, yy = this.floorY(x, z);
        acc.add(new THREE.CylinderGeometry(0.25, 0.35, 12, 6).translate(0, 6, 0), M4(x, yy, z), 0x333333);
        acc.add(new THREE.BoxGeometry(1.4, 0.8, 0.8).translate(0, 12.2, 0), M4(x, yy, z), 0x222222);
        if (this.night) {
          const cone = new THREE.Mesh(this.g(new THREE.ConeGeometry(2.6, 30, 16, 1, true).translate(0, -15, 0)),
            this.m(new THREE.MeshBasicMaterial({ map: beamTex, color: colors[this.beams.length % 6], transparent: true, opacity: 0.3, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide, fog: false })));
          cone.position.set(x, yy + 12.2, z); cone.rotation.x = Math.PI; this.group.add(cone); this.beams.push(cone);
        }
      }
    }
    // pistas de dança (quadradinhos coloridos que acendem à noite)
    const fc = document.createElement('canvas'); fc.width = 256; fc.height = 128; const fg2 = fc.getContext('2d');
    const cols = ['#ff3d7f', '#ffc400', '#00c853', '#2979ff', '#aa00ff', '#ff6d00'];
    for (let y = 0; y < 4; y++) for (let x = 0; x < 8; x++) { fg2.fillStyle = '#111'; fg2.fillRect(x * 32, y * 32, 32, 32); fg2.fillStyle = cols[(x + y * 3) % 6]; fg2.fillRect(x * 32 + 3, y * 32 + 3, 26, 26); }
    const ft = this.t(new THREE.CanvasTexture(fc)); ft.colorSpace = THREE.SRGBColorSpace;
    this.floorMat = this.m(new THREE.MeshStandardMaterial({ map: ft, emissive: 0xffffff, emissiveMap: ft, emissiveIntensity: this.night ? 0.2 : 0.05, roughness: 0.5, color: 0xbbbbbb, side: THREE.DoubleSide }));
    for (const st of this.stops) {
      const L = 20, W = 9, g = new THREE.PlaneGeometry(L, W, 10, 5); g.rotateX(-Math.PI / 2);
      const p2 = g.attributes.position, ang = Math.atan2(st.t[0], st.t[1]);
      for (let i = 0; i < p2.count; i++) {
        const lx = p2.getX(i), lz = p2.getZ(i);
        const x = st.cx + st.t[0] * lx + st.n[0] * (lz + 1.7), z = st.cz + st.t[1] * lx + st.n[1] * (lz + 1.7);
        p2.setXYZ(i, x, this.floorY(x, z) + 0.04, z);
      }
      g.computeVertexNormals(); const me = this.add(g, this.floorMat); me.renderOrder = 1;
    }
    const fg = new THREE.BufferGeometry(); fg.setAttribute('position', new THREE.Float32BufferAttribute([-0.35, 0, 0, 0.35, 0, 0, 0, -0.6, 0], 3)); fg.computeVertexNormals();
    const fm = new THREE.InstancedMesh(this.g(fg), this.m(new THREE.MeshStandardMaterial({ side: THREE.DoubleSide, roughness: 0.8 })), flagsPos.length);
    flagsPos.forEach(([q, a, col], i) => { fm.setMatrixAt(i, M4(q.x, q.y, q.z, a)); fm.setColorAt(i, _c.set(col)); });
    this.group.add(fm);
    // portal de largada/chegada sobre o anel
    const st = this.start, y0 = this.height(st.x, st.z);
    for (const dz of [-RW - 1, RW + 1]) acc.add(new THREE.CylinderGeometry(0.4, 0.4, 9, 8).translate(0, 4.5, 0), M4(st.x, this.height(st.x, -HALF + dz), -HALF + dz), 0xeeeeee);
    const tex = this.t(textSign(`${this.def.nome.toUpperCase()} · LARGADA`));
    const sign = new THREE.Mesh(this.g(new THREE.BoxGeometry(0.4, 3.2, 16)), [
      this.m(new THREE.MeshStandardMaterial({ map: tex, emissive: 0xffffff, emissiveMap: tex, emissiveIntensity: this.night ? 0.8 : 0.15 })),
      this.m(new THREE.MeshStandardMaterial({ map: tex, emissive: 0xffffff, emissiveMap: tex, emissiveIntensity: this.night ? 0.8 : 0.15 })),
      ...Array(4).fill(this.m(new THREE.MeshStandardMaterial({ color: 0xffffff })))]);
    sign.position.set(st.x, y0 + 9.5, -HALF); this.group.add(sign);
    const g = acc.build(); if (g) this.add(g, this.m(new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.7 })));
  }

  // ---------------- marcos (estilizados) ----------------
  std(geo, color, x, y, z, ry = 0, extra = {}) { const me = this.add(this.g(geo), this.m(new THREE.MeshStandardMaterial({ color, roughness: 0.7, ...extra }))); me.position.set(x, y, z); me.rotation.y = ry; return me; }
  buildLandmarks() {
    const n = this.night, d = this.def;
    const glow = (col = 0xfff6e0) => ({ emissive: col, emissiveIntensity: n ? 0.8 : 0 });
    const blk = (id) => this.blocks.find(b => b.marco === id);
    for (const id of d.marcos) {
      if (id === 'masp') {
        const b = blk('masp'); if (!b) continue; const y = this.floorY(b.cx, b.cz);
        const g = new THREE.Group(); g.position.set(b.cx, y, b.cz); this.group.add(g);
        const red = this.m(new THREE.MeshStandardMaterial({ color: 0xd32f2f, roughness: 0.6, ...glow(0x400000) }));
        const add = (geo, m, x, yy, z) => { const me = new THREE.Mesh(this.g(geo), m); me.position.set(x, yy, z); g.add(me); };
        for (const x of [-24, 24]) { add(new THREE.BoxGeometry(2.2, 16, 2.2), red, x, 8, -9); add(new THREE.BoxGeometry(2.2, 16, 2.2), red, x, 8, 9); add(new THREE.BoxGeometry(2.4, 2.4, 20), red, x, 17, 0); }
        add(new THREE.BoxGeometry(52, 8, 16), this.m(new THREE.MeshStandardMaterial({ color: 0x2b3a46, roughness: 0.2, metalness: 0.5, emissive: 0x3b6a9a, emissiveIntensity: n ? 0.6 : 0 })), 0, 11, 0);
        this.addCollider({ x0: b.cx - 26, x1: b.cx + 26, z0: b.cz - 8, z1: b.cz + 8, top: y + 15, base: y + 7, walk: true, kind: 'museu' });
      }
      if (id === 'antenas') for (let k = 0; k < 3; k++) {
        const x = [-420, 380, 60][k], z = [300, -260, 470][k], y = this.height(x, z);
        this.std(new THREE.CylinderGeometry(0.8, 2.6, 120, 4, 1, true).translate(0, 60, 0), 0xc62828, x, y, z, 0, { wireframe: true });
        this.std(new THREE.CylinderGeometry(0.6, 0.6, 120, 6).translate(0, 60, 0), 0xdddddd, x, y, z);
        this.blinkers.push(new THREE.Vector3(x, y + 121, z)); this.refreshBlinkers();
      }
      if (id === 'paodeacucar') {
        const mat = this.m(new THREE.MeshStandardMaterial({ color: 0x5f7d57, roughness: 1, flatShading: true }));
        const a = this.add(this.g(new THREE.SphereGeometry(60, 18, 14)), mat); a.scale.set(0.9, 2.3, 0.9); a.position.set(260, this.seaY - 2, -HALF - 420);
        const b = this.add(this.g(new THREE.SphereGeometry(50, 16, 12)), mat); b.scale.set(1.1, 1.3, 1); b.position.set(420, this.seaY - 2, -HALF - 330);
        const geo = this.g(new THREE.BufferGeometry().setFromPoints([a.position.clone().add(new THREE.Vector3(0, 135, 0)), b.position.clone().add(new THREE.Vector3(0, 62, 0))]));
        this.group.add(new THREE.Line(geo, this.m(new THREE.LineBasicMaterial({ color: 0x222222 }))));
      }
      if (id === 'cristo') {
        const x = -480, z = 520, y = this.height(x, z) - 10;
        this.std(new THREE.ConeGeometry(170, 320, 16), 0x3d6a37, x, y + 160, z, 0, { flatShading: true, roughness: 1 });
        const sm = this.m(new THREE.MeshStandardMaterial({ color: 0xf2efe6, roughness: 0.6, emissive: 0xfff6e0, emissiveIntensity: n ? 1.1 : 0 }));
        const st = new THREE.Group(); st.position.set(x, y + 320, z);
        for (const [geo, yy] of [[new THREE.CylinderGeometry(3, 5, 26, 8), 13], [new THREE.BoxGeometry(30, 3, 3), 22], [new THREE.SphereGeometry(2.6, 8, 6), 28.5]]) { const me = new THREE.Mesh(this.g(geo), sm); me.position.y = yy; st.add(me); }
        st.lookAt(0, y + 320, 0); this.group.add(st);
      }
      if (id === 'serra') {
        const mat = this.m(new THREE.MeshStandardMaterial({ color: 0x6b7f4e, roughness: 1, flatShading: true }));
        for (let k = 0; k < 9; k++) { const me = this.add(this.g(new THREE.ConeGeometry(170 + (k % 2) * 50, 240 + (k % 3) * 60, 7)), mat); me.position.set(-600 + k * 150, this.height(0, 700) + 80, 700 + (k % 3) * 60); me.scale.set(1.6, 1, 1); }
      }
      if (id === 'pampulha') {
        const b = blk('pampulha'); if (!b) continue; const y = this.floorY(b.cx, b.cz);
        [[0, 11, 9], [-12, 7, 6], [9, 7, 6], [17, 5, 4]].forEach(([z, rad, w]) => {
          const me = this.std(new THREE.CylinderGeometry(rad, rad, w, 20, 1, true, 0, Math.PI).rotateZ(Math.PI / 2).rotateY(Math.PI / 2).translate(0, 0, z), 0xf3f3f3, b.cx, y, b.cz, Math.PI / 2, { side: THREE.DoubleSide, ...glow() });
        });
        this.addCollider({ x0: b.cx - 18, x1: b.cx + 22, z0: b.cz - 10, z1: b.cz + 10, top: y + 10, walk: false, kind: 'igreja' });
      }
      if (id === 'elevador') {
        const x = -HALF - 26, z = 40, y = this.height(x, z) - 6;
        this.std(new THREE.BoxGeometry(9, 70, 14).translate(0, 35, 0), 0xeee3c6, x, y, z, 0, glow(0xffe8b0));
        this.std(new THREE.BoxGeometry(11, 10, 16).translate(0, 69, 0), 0xe6d7b0, x, y, z, 0, glow(0xffe8b0));
      }
      if (id === 'igreja') {
        const b = blk('igreja'); if (!b) continue; const y = this.floorY(b.cx, b.cz);
        this.std(new THREE.BoxGeometry(22, 16, 30).translate(0, 8, 0), 0xf6f1e3, b.cx, y, b.cz, 0, glow());
        for (const x of [-9, 9]) { this.std(new THREE.BoxGeometry(6, 26, 6).translate(x, 13, -14), 0xf6f1e3, b.cx, y, b.cz, 0, glow()); this.std(new THREE.ConeGeometry(3.6, 5, 8).translate(x, 28.5, -14), 0x2f6fb5, b.cx, y, b.cz); }
        this.addCollider({ x0: b.cx - 12, x1: b.cx + 12, z0: b.cz - 18, z1: b.cz + 15, top: y + 16, walk: true, kind: 'igreja' });
      }
      if (id === 'estufa') {
        const b = blk('estufa'); if (!b) continue; const y = this.floorY(b.cx, b.cz);
        const glass = { roughness: 0.05, metalness: 0.3, transparent: true, opacity: 0.45, emissive: 0x9fd6ff, emissiveIntensity: n ? 0.5 : 0 };
        for (const geo of [new THREE.BoxGeometry(14, 10, 36).translate(0, 5, 0), new THREE.CylinderGeometry(7, 7, 36, 16, 1, false, 0, Math.PI).rotateZ(Math.PI / 2).rotateY(Math.PI / 2).translate(0, 10, 0), new THREE.SphereGeometry(8, 16, 10, 0, Math.PI * 2, 0, Math.PI / 2).translate(0, 16, 0)]) {
          this.std(geo, 0xbfe6ff, b.cx, y, b.cz, 0, glass);
          const e = new THREE.LineSegments(this.g(new THREE.EdgesGeometry(geo, 20)), this.m(new THREE.LineBasicMaterial({ color: 0xffffff }))); e.position.set(b.cx, y, b.cz); this.group.add(e);
        }
        this.addCollider({ x0: b.cx - 7, x1: b.cx + 7, z0: b.cz - 18, z1: b.cz + 18, top: y + 10, walk: true, kind: 'estufa' });
      }
      if (id === 'tubo') {
        const x = lineC(3) + RW + 1.8, z = lineC(1) + 30, y = this.floorY(x, z);
        this.std(new THREE.CylinderGeometry(1.5, 1.5, 12, 20, 1, true).rotateX(Math.PI / 2).translate(0, 2.2, 0), 0x9ed8ff, x, y, z, 0, { transparent: true, opacity: 0.4, roughness: 0.05, side: THREE.DoubleSide, emissive: 0x6ac0ff, emissiveIntensity: n ? 0.7 : 0 });
        this.addCollider({ x0: x - 1.5, x1: x + 1.5, z0: z - 6, z1: z + 6, top: y + 3.7, walk: true, kind: 'tubo' });
      }
    }
  }

  buildMountains() {
    const r = this.r, c = this.def.montanhas, N2 = 36, geo = this.g(new THREE.ConeGeometry(1, 1, 6, 2));
    const im = new THREE.InstancedMesh(geo, this.m(new THREE.MeshStandardMaterial({ color: c.cor, roughness: 1, flatShading: true })), N2);
    for (let i = 0; i < N2; i++) {
      const a = i / N2 * Math.PI * 2 + r() * 0.1, R = HALF + range(r, 650, 1100), H = c.alt * range(r, 0.6, 1.8), W = H * range(r, 1.6, 2.6);
      const x = Math.cos(a) * R, z = Math.sin(a) * R;
      im.setMatrixAt(i, new THREE.Matrix4().compose(new THREE.Vector3(x, (this.def.mar ? this.seaY : -this.A) + H / 2 - 8, z), _q.setFromAxisAngle(UP, r() * 6), _s.set(W, H, W)));
    }
    this.group.add(im);
  }

  // ---------------- público ----------------
  buildCrowd() {
    const r = this.r, people = [];
    for (const b of this.blocks) {
      if (b.type === 'praca' || b.type === 'inicio') for (let k = 0; k < 70; k++) { const a = r() * 6.28, R = range(r, 10, 30), x = b.cx + Math.cos(a) * R, z = b.cz + Math.sin(a) * R; if (this.inZone(x, z, 1.5)) continue; people.push([x, z, Math.atan2(-Math.cos(a), -Math.sin(a))]); }
    }
    // gente na calçada do anel (vendo a carreta passar)
    for (let t = -HALF + 15; t < HALF - 15; t += range(r, 10, 22)) {
      const side = Math.floor(r() * 4), n = 1 + Math.floor(r() * 4);
      for (let k = 0; k < n; k++) {
        const o = t + range(r, -2, 2), off = range(r, RW + 0.8, RW + 3);
        const [x, z, f] = [[o, -HALF + off, Math.PI], [o, HALF - off, 0], [-HALF + off, o, -Math.PI / 2], [HALF - off, o, Math.PI / 2]][side];
        people.push([x, z, f]);
      }
    }
    for (let i = people.length - 1; i >= 0; i--) if (this.inZone(people[i][0], people[i][1], 1)) people.splice(i, 1);
    const N2 = people.length;
    const body = this.g(new THREE.CylinderGeometry(0.24, 0.3, 1.05, 7).translate(0, 0.55, 0));
    const head = this.g(new THREE.SphereGeometry(0.2, 8, 6).translate(0, 1.32, 0));
    const arms = this.g(new THREE.BoxGeometry(0.9, 0.12, 0.12));
    const bm = new THREE.InstancedMesh(body, this.m(new THREE.MeshStandardMaterial({ roughness: 0.8 })), N2);
    const hm = new THREE.InstancedMesh(head, this.m(new THREE.MeshStandardMaterial({ roughness: 0.8 })), N2);
    const am = new THREE.InstancedMesh(arms, this.m(new THREE.MeshStandardMaterial({ roughness: 0.8 })), N2);
    const shirts = [0xff3d7f, 0xffc400, 0x00c853, 0x2979ff, 0xffffff, 0xff6d00, 0x00bcd4, 0xe53935, 0x8e24aa, 0xfdd835];
    const skins = [0xf1c27d, 0xe0ac69, 0xc68642, 0x8d5524, 0xffdbac, 0x6b3e26];
    this.people = people.map(([x, z, f], i) => {
      bm.setColorAt(i, _c.set(pick(r, shirts))); hm.setColorAt(i, _c.set(pick(r, skins))); am.setColorAt(i, _c.set(pick(r, skins)));
      return { p: new THREE.Vector3(x, this.floorY(x, z), z), h: f + range(r, -0.4, 0.4), scale: range(r, 0.7, 1.15), ph: r() * 6.28, sp: range(r, 0.8, 1.2) };
    });
    for (const p of this.people) this.addCollider({ x0: p.p.x - 0.3, x1: p.p.x + 0.3, z0: p.p.z - 0.3, z1: p.p.z + 0.3, top: p.p.y + 1.5, walk: false, kind: 'pessoa' });
    this.crowd = { bm, hm, am };
    for (const m of [bm, hm, am]) { m.instanceMatrix.setUsage(THREE.DynamicDrawUsage); m.frustumCulled = false; this.group.add(m); }
  }
  updateCrowd(t, beat, truckPos, partyAt) {
    const { bm, hm, am } = this.crowd, m = new THREE.Matrix4(), q = new THREE.Quaternion(), sc = new THREE.Vector3(), pos = new THREE.Vector3();
    const qa = new THREE.Quaternion(), ma = new THREE.Matrix4(), Z = new THREE.Vector3(0, 0, 1), one = new THREE.Vector3(1, 1, 1), ap = new THREE.Vector3();
    for (let i = 0; i < this.people.length; i++) {
      const P2 = this.people[i];
      const dT = Math.hypot(P2.p.x - truckPos.x, P2.p.z - truckPos.z);
      const near = dT < 40 || (partyAt && Math.hypot(P2.p.x - partyAt.x, P2.p.z - partyAt.z) < 45);
      const e = near ? 1 : 0.15;
      pos.copy(P2.p); pos.y += Math.max(0, Math.sin(beat * Math.PI + P2.ph)) * 0.35 * e;
      q.setFromAxisAngle(UP, P2.h + (near ? Math.sin(t * 3 * P2.sp + P2.ph) * 0.3 : 0)); sc.setScalar(P2.scale);
      m.compose(pos, q, sc); bm.setMatrixAt(i, m); hm.setMatrixAt(i, m);
      qa.setFromAxisAngle(Z, near ? Math.sin(t * 6 + P2.ph) * 0.4 : 0);
      ma.compose(ap.set(0, near ? 1.25 + Math.sin(t * 8 * P2.sp + P2.ph) * 0.12 : 0.85, 0), qa, one);
      am.setMatrixAt(i, m.multiply(ma));
    }
    bm.instanceMatrix.needsUpdate = hm.instanceMatrix.needsUpdate = am.instanceMatrix.needsUpdate = true;
  }

  // ---------------- volta da carreta ----------------
  buildRing() {
    const R = RING, c = 12, pts = [];
    const corner = (cx, cz, a0) => { for (let k = 0; k <= 6; k++) { const a = a0 + k / 6 * Math.PI / 2; pts.push([cx + Math.cos(a) * c, cz + Math.sin(a) * c]); } };
    pts.push([this.start.x, -R]);
    pts.push([R - c, -R]); corner(R - c, -R + c, -Math.PI / 2);
    pts.push([R, R - c]); corner(R - c, R - c, 0);
    pts.push([-R + c, R]); corner(-R + c, R - c, Math.PI / 2);
    pts.push([-R, -R + c]); corner(-R + c, -R + c, Math.PI);
    pts.push([this.start.x, -R]);
    // densifica
    const dense = [];
    for (let i = 0; i < pts.length - 1; i++) { const [ax, az] = pts[i], [bx, bz] = pts[i + 1], L = Math.hypot(bx - ax, bz - az), n = Math.max(1, Math.ceil(L / 2)); for (let k = 0; k < n; k++) dense.push([ax + (bx - ax) * k / n, az + (bz - az) * k / n]); }
    this.ring = dense; this.ringS = [0];
    for (let i = 1; i < dense.length; i++) this.ringS.push(this.ringS[i - 1] + Math.hypot(dense[i][0] - dense[i - 1][0], dense[i][1] - dense[i - 1][1]));
    this.ringLen = this.ringS[this.ringS.length - 1] + Math.hypot(dense[0][0] - dense[dense.length - 1][0], dense[0][1] - dense[dense.length - 1][1]);
    for (const st of this.stops) st.s = this.ringNearestS(st.x, st.z);
  }
  ringPoint(s) {
    s = ((s % this.ringLen) + this.ringLen) % this.ringLen;
    let lo = 0, hi = this.ringS.length - 1;
    while (lo < hi) { const mid = (lo + hi + 1) >> 1; if (this.ringS[mid] <= s) lo = mid; else hi = mid - 1; }
    const a = this.ring[lo], b = this.ring[(lo + 1) % this.ring.length], seg = (this.ringS[lo + 1] ?? this.ringLen) - this.ringS[lo];
    const t = seg > 0 ? (s - this.ringS[lo]) / seg : 0;
    return { x: a[0] + (b[0] - a[0]) * t, z: a[1] + (b[1] - a[1]) * t };
  }
  ringNearestS(x, z) {
    let best = 0, bd = 1e18;
    for (let i = 0; i < this.ring.length; i++) { const d = (this.ring[i][0] - x) ** 2 + (this.ring[i][1] - z) ** 2; if (d < bd) { bd = d; best = i; } }
    return this.ringS[best];
  }
  // caminho pelas ruas (grade) de um ponto até o anel
  routeToRing(x, z) {
    const ki = Math.round((x + HALF) / P), kj = Math.round((z + HALF) / P);
    const cki = Math.max(0, Math.min(N, ki)), ckj = Math.max(0, Math.min(N, kj));
    const nx = lineC(cki), nz = lineC(ckj);
    // vai até o cruzamento mais perto e depois reto até a borda mais próxima
    const opts = [[cki, 0], [cki, N], [0, ckj], [N, ckj]].map(([i, j]) => ({ i, j, d: Math.abs(i - cki) + Math.abs(j - ckj) })).sort((a, b) => a.d - b.d);
    const tgt = opts[0];
    const ex = lineC(tgt.i), ez = lineC(tgt.j);
    const clampR = (v) => Math.max(-RING, Math.min(RING, v));
    return [[nx, nz], [clampR(ex), clampR(ez)]];
  }

  update(dt, t, beat, energy, truckPos, camera, partyAt) {
    this.sky.position.copy(camera.position); this.skyObj.position.copy(camera.position);
    if (this.blinkMesh) this.blinkMesh.visible = (t % 1.4) < 0.7;
    this.updateCrowd(t, beat, truckPos, partyAt);
    this.updateNotes(t, dt);
    if (this.waterNormal) { this.waterNormal.offset.x = t * 0.012; this.waterNormal.offset.y = t * 0.007; }
    if (this.clouds) for (const c of this.clouds) { c.position.x += dt * 4; if (c.position.x > 1600) c.position.x = -1600; }
    if (this.floorMat) this.floorMat.emissiveIntensity = (this.night ? 0.16 : 0.03) + energy * (this.night ? 0.22 : 0.06);
    this.beams.forEach((b, i) => { b.rotation.z = Math.sin(t * 0.9 + i * 1.7) * 0.6 + Math.PI; b.rotation.x = Math.PI + Math.cos(t * 0.7 + i) * 0.4; b.material.opacity = 0.16 + energy * 0.25; });
  }

  dispose() {
    this.scene.remove(this.group);
    this.group.traverse(o => { if (o.isInstancedMesh) o.dispose(); });
    for (const d of this.disp) d && d.dispose && d.dispose();
    this.scene.fog = null;
  }
}
