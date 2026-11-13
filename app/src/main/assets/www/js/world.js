import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { rng, pick, range } from './cities.js';
import { ROAD_HALF, SIDEWALK } from './track.js';
import * as TX from './textures.js';

const UP = new THREE.Vector3(0, 1, 0);
const _m = new THREE.Matrix4(), _q = new THREE.Quaternion(), _v = new THREE.Vector3(), _s = new THREE.Vector3(), _c = new THREE.Color();

// Acumula várias geometrias (com cor por vértice) numa só malha => poucas chamadas de desenho (bom pro celular)
class Acc {
  constructor() { this.parts = []; }
  add(geo, matrix, color) {
    const g = geo.index ? geo.toNonIndexed() : geo.clone();
    g.applyMatrix4(matrix);
    const n = g.attributes.position.count, col = new Float32Array(n * 3);
    _c.set(color);
    for (let i = 0; i < n; i++) { col[i * 3] = _c.r; col[i * 3 + 1] = _c.g; col[i * 3 + 2] = _c.b; }
    g.setAttribute('color', new THREE.BufferAttribute(col, 3));
    if (!g.attributes.uv) g.setAttribute('uv', new THREE.BufferAttribute(new Float32Array(n * 2), 2));
    this.parts.push(g);
  }
  build() { if (!this.parts.length) return null; const g = mergeGeometries(this.parts, false); this.parts.forEach(p => p.dispose()); return g; }
}

function placeMatrix(pos, heading, scale = 1, extraYaw = 0) {
  _q.setFromAxisAngle(UP, heading + extraYaw);
  _s.set(scale, scale, scale);
  return new THREE.Matrix4().compose(pos, _q, _s);
}

// Caixa de prédio com UVs proporcionais ao tamanho (janelas não esticam)
function buildingBox(w, h, d, tile = 12) {
  const g = new THREE.BoxGeometry(d, h, w); // x = lateral, z = frente
  const uv = g.attributes.uv;
  for (let face = 0; face < 6; face++) {
    for (let k = 0; k < 4; k++) {
      const i = face * 4 + k;
      let u = uv.getX(i), v = uv.getY(i);
      if (face === 0 || face === 1) { u *= w / tile; v *= h / tile; }
      else if (face === 4 || face === 5) { u *= d / tile; v *= h / tile; }
      else { u = 0.5; v = 0.995; }
      uv.setXY(i, u, v);
    }
  }
  g.translate(0, h / 2, 0);
  return g;
}

function prism(w, d, rh) {
  // telhado de duas águas: cumeeira ao longo da frente (z)
  const hw = d / 2 + 0.4, hz = w / 2 + 0.4;
  const p = [
    -hw, 0, -hz, hw, 0, -hz, 0, rh, -hz,
    hw, 0, hz, -hw, 0, hz, 0, rh, hz,
    -hw, 0, -hz, 0, rh, -hz, 0, rh, hz, -hw, 0, -hz, 0, rh, hz, -hw, 0, hz,
    hw, 0, -hz, hw, 0, hz, 0, rh, hz, hw, 0, -hz, 0, rh, hz, 0, rh, -hz,
  ];
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute(p, 3));
  g.computeVertexNormals();
  return g;
}

function textSign(text, bg = '#e91e63', fg = '#fff', w = 1024, h = 160) {
  const c = document.createElement('canvas'); c.width = w; c.height = h;
  const g = c.getContext('2d');
  const gr = g.createLinearGradient(0, 0, w, 0);
  gr.addColorStop(0, '#ff3d7f'); gr.addColorStop(0.33, '#ffc400'); gr.addColorStop(0.66, '#00c853'); gr.addColorStop(1, '#2979ff');
  g.fillStyle = bg === 'rainbow' ? gr : bg; g.fillRect(0, 0, w, h);
  g.fillStyle = 'rgba(0,0,0,0.25)'; g.fillRect(0, h - 14, w, 14);
  g.font = `bold ${Math.floor(h * 0.62)}px sans-serif`; g.textAlign = 'center'; g.textBaseline = 'middle';
  g.lineWidth = 10; g.strokeStyle = 'rgba(0,0,0,0.45)'; g.strokeText(text, w / 2, h / 2 + 4);
  g.fillStyle = fg; g.fillText(text, w / 2, h / 2 + 4);
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 4;
  return t;
}

export class World {
  constructor(scene, track, city, night, quality) {
    this.scene = scene; this.track = track; this.city = city; this.night = night; this.quality = quality;
    this.group = new THREE.Group(); scene.add(this.group);
    this.r = rng(city.seed * 7 + 3);
    this.disposables = [];
    this.blinkers = []; this.crowd = null; this.waterMat = null;
    this.buildSky();
    this.buildGround();
    this.buildBuildings();
    this.buildLamps();
    this.buildTrees();
    this.buildLandmarks();
    this.buildMountains();
    this.buildPraca();
    this.buildCrowd();
    this.buildGates();
  }

  mat(m) { this.disposables.push(m); return m; }
  mesh(geo, mat) { const m = new THREE.Mesh(geo, mat); this.group.add(m); this.disposables.push(geo); return m; }

  // ---------------- céu e luz ----------------
  buildSky() {
    const c = this.city, n = this.night;
    const [top, hor] = n ? c.ceuNoite : c.ceuDia;
    this.skyColors = { top: new THREE.Color(top), hor: new THREE.Color(hor) };
    const skyMat = this.mat(new THREE.ShaderMaterial({
      side: THREE.BackSide, depthWrite: false, fog: false,
      uniforms: { top: { value: this.skyColors.top }, hor: { value: this.skyColors.hor } },
      vertexShader: 'varying vec3 vP; void main(){ vP = normalize(position); gl_Position = projectionMatrix*modelViewMatrix*vec4(position,1.0);} ',
      fragmentShader: `uniform vec3 top; uniform vec3 hor; varying vec3 vP;
        void main(){ float h = max(vP.y,0.0); vec3 col = mix(hor, top, pow(h,0.55)); if(vP.y<0.0) col = hor*0.8;
          gl_FragColor = vec4(col,1.0);
          #include <colorspace_fragment>
        }`,
    }));
    this.sky = new THREE.Mesh(new THREE.SphereGeometry(1800, 32, 16), skyMat);
    this.sky.renderOrder = -10; this.sky.frustumCulled = false;
    this.group.add(this.sky); this.disposables.push(this.sky.geometry);

    this.scene.fog = new THREE.FogExp2(hor, c.nevoa * (n ? 1.15 : 1));
    this.scene.background = this.skyColors.hor;

    this.hemi = new THREE.HemisphereLight(n ? 0x5a6aa8 : 0xdfeeff, n ? 0x20182a : 0x6b5a45, n ? 0.65 : 1.5);
    this.group.add(this.hemi);
    this.sun = new THREE.DirectionalLight(n ? 0x9fb4ff : 0xfff1d6, n ? 0.5 : 2.4);
    this.sun.position.set(-300, 500, -200);
    this.group.add(this.sun); this.group.add(this.sun.target);

    // sol / lua
    const glow = TX.radial(n ? '220,230,255' : '255,240,200'); this.disposables.push(glow);
    const sp = new THREE.Sprite(this.mat(new THREE.SpriteMaterial({ map: glow, fog: false, depthWrite: false, transparent: true, blending: THREE.AdditiveBlending })));
    sp.scale.setScalar(n ? 140 : 320); sp.position.set(-700, 900, -500).normalize().multiplyScalar(1500);
    this.skyObj = new THREE.Group(); this.skyObj.add(sp); this.group.add(this.skyObj);
    if (n) {
      const moon = new THREE.Mesh(new THREE.CircleGeometry(30, 32), this.mat(new THREE.MeshBasicMaterial({ color: 0xf4f1e6, fog: false })));
      moon.position.copy(sp.position); moon.lookAt(0, 0, 0); this.skyObj.add(moon); this.disposables.push(moon.geometry);
      const N = 1400, p = new Float32Array(N * 3);
      for (let i = 0; i < N; i++) {
        const th = Math.random() * Math.PI * 2, ph = Math.acos(Math.random() * 0.95);
        p[i * 3] = Math.sin(ph) * Math.cos(th) * 1500; p[i * 3 + 1] = Math.cos(ph) * 1500 + 40; p[i * 3 + 2] = Math.sin(ph) * Math.sin(th) * 1500;
      }
      const sg = new THREE.BufferGeometry(); sg.setAttribute('position', new THREE.BufferAttribute(p, 3));
      const stars = new THREE.Points(sg, this.mat(new THREE.PointsMaterial({ color: 0xffffff, size: 2, sizeAttenuation: false, fog: false })));
      this.skyObj.add(stars); this.disposables.push(sg);
    }
  }

  // ---------------- chão, rua, calçadas, mar ----------------
  strip(latA, latB, yA, yB, mat, opts = {}) {
    const T = this.track, step = opts.step || 3, from = opts.from || 0, to = opts.to ?? T.length;
    const pos = [], uv = [], idx = []; let row = 0; const f = {};
    for (let s = from; s <= to + 0.01; s += step) {
      const ss = Math.min(s, to); T.frame(ss, f);
      const a = typeof yA === 'function' ? yA(ss, f.y) : f.y + yA;
      const b = typeof yB === 'function' ? yB(ss, f.y) : f.y + yB;
      pos.push(f.x + f.rx * latA, a, f.z + f.rz * latA, f.x + f.rx * latB, b, f.z + f.rz * latB);
      const v = ss * (opts.vScale || 0.05);
      uv.push(0, v, opts.uScale || 1, v);
      if (row > 0) { const p = (row - 1) * 2; idx.push(p, p + 1, p + 2, p + 1, p + 3, p + 2); }
      row++;
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    g.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2));
    g.setIndex(idx); g.computeVertexNormals();
    if (latA > latB) { g.index.array.reverse(); g.computeVertexNormals(); }
    return this.mesh(g, mat);
  }

  buildGround() {
    const T = this.track, c = this.city;
    const asph = TX.asphalt(); this.disposables.push(asph);
    const roadMat = this.mat(new THREE.MeshStandardMaterial({ map: asph, roughness: 0.92, metalness: 0 }));
    const road = T.ribbon(-ROAD_HALF, ROAD_HALF, { step: 2, uScale: 1, vScale: 1 / 18 });
    this.mesh(road, roadMat);

    const sw = TX.sidewalk(c.calcada); this.disposables.push(sw);
    const swMat = this.mat(new THREE.MeshStandardMaterial({ map: sw, roughness: 0.85 }));
    const curbMat = this.mat(new THREE.MeshStandardMaterial({ color: 0xb8b5ad, roughness: 0.8, side: THREE.DoubleSide }));
    const groundMat = this.mat(new THREE.MeshStandardMaterial({ color: c.chao, roughness: 1 }));
    const H = 0.2;
    for (const side of [-1, 1]) {
      const a = side * ROAD_HALF, b = side * SIDEWALK;
      this.strip(Math.min(a, b), Math.max(a, b), H, H, swMat, { step: 2, uScale: 1, vScale: 1 / 3 });
      // meio-fio (face vertical)
      const curb = this.strip(a, a + side * 0.001, side > 0 ? 0 : H, side > 0 ? H : 0, curbMat, { step: 2 });
      curb.material = curbMat;
      // faixa de terra/grama até longe
      const seaY = c.mar ? T.minY - 3 : null;
      const outer = (s, y) => seaY !== null ? Math.max(seaY - 0.5, y - 14) : y - 22;
      if (c.mar && side === c.mar.lado) {
        // praia: areia até a água
        const sand = this.mat(new THREE.MeshStandardMaterial({ color: 0xe8d39a, roughness: 1 }));
        this.strip(side * SIDEWALK, side * 48, H, (s, y) => Math.max(seaY + 0.6, y - 2.5), sand, { step: 4 });
        this.strip(side * 48, side * 70, (s, y) => Math.max(seaY + 0.6, y - 2.5), () => seaY - 1.2, sand, { step: 6 });
      } else {
        this.strip(side * SIDEWALK, side * 60, H, H, groundMat, { step: 4 });
        this.strip(side * 60, side * 190, H, outer, groundMat, { step: 6 });
      }
    }
    // chão distante / mar
    const cx = (Math.min(...T.x) + Math.max(...T.x)) / 2, cz = (Math.min(...T.z) + Math.max(...T.z)) / 2;
    this.center = new THREE.Vector3(cx, 0, cz);
    this.extent = Math.max(Math.max(...T.x) - Math.min(...T.x), Math.max(...T.z) - Math.min(...T.z)) / 2 + 200;
    if (c.mar) {
      this.waterMat = this.mat(new THREE.MeshStandardMaterial({ color: c.mar.cor, roughness: 0.18, metalness: 0.1, transparent: true, opacity: 0.95 }));
      const sea = this.mesh(new THREE.PlaneGeometry(6000, 6000), this.waterMat);
      sea.rotation.x = -Math.PI / 2; sea.position.set(cx, T.minY - 3, cz);
      this.seaY = T.minY - 3;
    } else {
      const far = this.mesh(new THREE.CircleGeometry(4000, 48), groundMat);
      far.rotation.x = -Math.PI / 2; far.position.set(cx, T.minY - 24, cz);
    }
  }

  // ---------------- prédios ----------------
  buildBuildings() {
    const T = this.track, c = this.city, P = c.predios, r = this.r;
    const fac = TX.facade(P.tipo); this.disposables.push(fac.map, fac.emissive);
    const mat = this.mat(new THREE.MeshStandardMaterial({
      map: fac.map, vertexColors: true, roughness: 0.85,
      emissive: 0xffffff, emissiveMap: fac.emissive, emissiveIntensity: this.night ? 0.75 : 0,
    }));
    const roofMat = this.mat(new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.9 }));
    const acc = new Acc(), roofs = new Acc();
    const f = {};
    const reserved = this.reserved = [];
    // zonas sem prédios (praça e marcos)
    reserved.push({ side: 1, a: T.praca - 50, b: T.praca + 50 });
    const isFree = (side, a, b) => !reserved.some(z => (z.side === side || z.side === 0) && a < z.b && b > z.a);
    this.isFree = isFree;
    // marcos reservam espaço
    for (const [side, s0, len] of this.landmarkSlots()) reserved.push({ side, a: s0 - len / 2, b: s0 + len / 2 });

    for (const side of [-1, 1]) {
      if (c.mar && side === c.mar.lado && c.id === 'rio') continue; // orla aberta
      for (const row of [0, 1]) {
        let s = 2 + r() * 6;
        while (s < T.length + 30) {
          const w = row ? range(r, 18, 34) : range(r, P.larg[0], P.larg[1]);
          const cs = Math.min(s + w / 2, T.length);
          if (row === 0 && !isFree(side, s, s + w)) { s += w; continue; }
          const d = row ? range(r, 16, 28) : range(r, 9, 16);
          let h = range(r, P.altMin, P.altMax);
          if (row === 1) h = h * range(r, 1.1, 1.8);
          if (row === 1 && P.tipo === 'casarao') h = range(r, 10, 22);
          const lat = side * (row ? range(r, 48, 85) : SIDEWALK + 1 + d / 2 + r() * 1.2);
          T.frame(cs, f);
          const yA = T.frame(Math.max(0, s), {}).y, yB = T.frame(Math.min(T.length, s + w), {}).y;
          const base = Math.min(yA, yB, f.y) - (row ? 10 : 2.5);
          const tall = h + (Math.max(yA, yB, f.y) - base);
          const pos = new THREE.Vector3(f.x + f.rx * lat, base, f.z + f.rz * lat);
          const m = placeMatrix(pos, f.heading);
          const col = pick(r, P.cores);
          acc.add(buildingBox(w, tall, d, P.tipo === 'casarao' ? 8 : 12), m, col);
          if (P.tipo === 'casarao' && row === 0) {
            const roof = prism(w, d, 2.2);
            roofs.add(roof, new THREE.Matrix4().multiplyMatrices(m, new THREE.Matrix4().makeTranslation(0, tall, 0)), 0xb5532d);
          } else if (P.tipo === 'torre' && r() < 0.35) {
            // antena com luz vermelha piscando
            const ant = new THREE.CylinderGeometry(0.15, 0.15, 8, 5); ant.translate(0, tall + 4, 0);
            roofs.add(ant, m, 0x888888);
            this.blinkers.push(new THREE.Vector3(0, tall + 8.2, 0).applyMatrix4(m));
          } else if (r() < 0.4) {
            const box = new THREE.BoxGeometry(d * 0.4, 2.5, w * 0.3); box.translate(0, tall + 1.25, 0);
            roofs.add(box, m, 0x9a9a9a);
          }
          s += w + (row ? range(r, 4, 14) : range(r, 0.4, 2.5));
        }
      }
    }
    const g = acc.build(); if (g) this.mesh(g, mat);
    const rg = roofs.build(); if (rg) this.mesh(rg, roofMat);
    // luzes vermelhas de antenas
    if (this.blinkers.length) {
      const bm = new THREE.InstancedMesh(new THREE.SphereGeometry(0.5, 6, 4), this.mat(new THREE.MeshBasicMaterial({ color: 0xff2020 })), this.blinkers.length);
      this.blinkers.forEach((p, i) => bm.setMatrixAt(i, new THREE.Matrix4().makeTranslation(p.x, p.y, p.z)));
      this.blinkMesh = bm; this.group.add(bm); this.disposables.push(bm.geometry);
    }
  }

  landmarkSlots() {
    // [lado, s, comprimento reservado]
    const L = this.track.length;
    const slots = {
      sp: [[1, 330, 70], [-1, 980, 40]],
      rio: [[1, 420, 40]],
      bh: [[-1, 300, 60], [1, 1150, 50]],
      ssa: [[-1, 360, 40], [1, 1080, 50]],
      cwb: [[1, 380, 30], [1, 1050, 90]],
    };
    return (slots[this.city.id] || []).filter(s => s[1] < L);
  }

  // ---------------- postes ----------------
  buildLamps() {
    const T = this.track, f = {};
    const poleGeo = mergeGeometries([
      new THREE.CylinderGeometry(0.12, 0.16, 8, 6).translate(0, 4, 0),
      new THREE.BoxGeometry(2.4, 0.12, 0.12).translate(-1.15, 7.9, 0),
    ]);
    const headGeo = new THREE.BoxGeometry(0.8, 0.18, 0.4).translate(-2.25, 7.8, 0);
    const list = [];
    for (let s = 10; s < T.length; s += 34) for (const side of [-1, 1]) list.push([s + (side > 0 ? 17 : 0), side]);
    const poleMat = this.mat(new THREE.MeshStandardMaterial({ color: 0x55585e, roughness: 0.6, metalness: 0.4 }));
    const headMat = this.mat(new THREE.MeshBasicMaterial({ color: this.night ? 0xfff0c8 : 0xcfd2d6 }));
    const poles = new THREE.InstancedMesh(poleGeo, poleMat, list.length);
    const heads = new THREE.InstancedMesh(headGeo, headMat, list.length);
    const pools = this.night ? new THREE.InstancedMesh(new THREE.PlaneGeometry(9, 9).rotateX(-Math.PI / 2),
      this.mat(new THREE.MeshBasicMaterial({ map: this.disp(TX.radial('255,214,150')), transparent: true, opacity: 0.55, depthWrite: false, blending: THREE.AdditiveBlending })), list.length) : null;
    list.forEach(([s, side], i) => {
      T.frame(s, f);
      const p = new THREE.Vector3(f.x + f.rx * side * 9.7, f.y + 0.2, f.z + f.rz * side * 9.7);
      // braço aponta pra rua: geometria aponta para -X local; lado direito (+lat) => -X local = direita?
      const m = placeMatrix(p, f.heading, 1, side > 0 ? Math.PI : 0);
      poles.setMatrixAt(i, m); heads.setMatrixAt(i, m);
      if (pools) {
        const q = new THREE.Vector3(f.x + f.rx * side * 7.5, f.y + 0.05, f.z + f.rz * side * 7.5);
        pools.setMatrixAt(i, placeMatrix(q, f.heading));
      }
    });
    this.group.add(poles, heads); this.disposables.push(poleGeo, headGeo);
    if (pools) { pools.renderOrder = 2; this.group.add(pools); }
  }
  disp(x) { this.disposables.push(x); return x; }

  // ---------------- árvores ----------------
  treeGeos(type) {
    if (type === 'coqueiro') {
      const trunk = new THREE.CylinderGeometry(0.16, 0.26, 9, 6).translate(0, 4.5, 0);
      const leaves = [];
      for (let k = 0; k < 7; k++) {
        const l = new THREE.ConeGeometry(0.45, 3.6, 4).scale(1, 1, 0.25).translate(0, 1.8, 0);
        l.rotateZ(-1.9); l.rotateY(k / 7 * Math.PI * 2); l.translate(0, 9, 0); leaves.push(l);
      }
      return [trunk, mergeGeometries(leaves), [0x3f8f3a, 0x4ea83f, 0x2f7a33]];
    }
    if (type === 'araucaria') {
      const trunk = new THREE.CylinderGeometry(0.22, 0.32, 13, 6).translate(0, 6.5, 0);
      const c1 = new THREE.ConeGeometry(4.2, 1.6, 9).rotateX(Math.PI).translate(0, 12.6, 0);
      const c2 = new THREE.ConeGeometry(3.0, 1.4, 9).rotateX(Math.PI).translate(0, 13.8, 0);
      return [trunk, mergeGeometries([c1, c2]), [0x24502c, 0x2c5e33]];
    }
    const trunk = new THREE.CylinderGeometry(0.18, 0.26, 3.2, 6).translate(0, 1.6, 0);
    const crown = new THREE.IcosahedronGeometry(2.1, 1).scale(1, 0.85, 1).translate(0, 4.4, 0);
    return [trunk, crown, type === 'ipe' ? [0xffd400, 0xff6fb5, 0xb05bd8, 0x4c9a3c] : [0x3e8a3a, 0x4f9a40, 0x2f7334]];
  }

  buildTrees() {
    const T = this.track, c = this.city, r = this.r, f = {};
    const [trunkGeo, crownGeo, colors] = this.treeGeos(c.arvores);
    const spots = [];
    for (let s = 25; s < T.length; s += 34) for (const side of [-1, 1]) {
      if (r() < 0.25) continue;
      if (c.id === 'rio' && side === -1) continue;
      spots.push([s + (side > 0 ? 17 : 0) + range(r, -4, 4), side * 11]);
    }
    if (c.mar && (c.id === 'rio')) for (let s = 10; s < T.length; s += 14) spots.push([s + range(r, -4, 4), -range(r, 18, 40)]);
    if (c.arvores === 'araucaria' || c.arvores === 'ipe') for (let k = 0; k < 120; k++) spots.push([range(r, 0, T.length), (r() < 0.5 ? -1 : 1) * range(r, 30, 140)]);
    const trunks = new THREE.InstancedMesh(trunkGeo, this.mat(new THREE.MeshStandardMaterial({ color: 0x6b4a2f, roughness: 1 })), spots.length);
    const crowns = new THREE.InstancedMesh(crownGeo, this.mat(new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.9, flatShading: true })), spots.length);
    spots.forEach(([s, lat], i) => {
      T.frame(Math.max(0, Math.min(T.length, s)), f);
      const p = new THREE.Vector3(f.x + f.rx * lat, this.groundY(f, lat), f.z + f.rz * lat);
      const m = placeMatrix(p, r() * 6.28, range(r, 0.8, 1.25));
      trunks.setMatrixAt(i, m); crowns.setMatrixAt(i, m);
      crowns.setColorAt(i, _c.set(pick(r, colors)));
    });
    this.group.add(trunks, crowns); this.disposables.push(trunkGeo, crownGeo);
  }

  // ---------------- pontos turísticos (formas simples e estilizadas) ----------------
  at(s, lat, up = 0) {
    const f = this.track.frame(Math.min(s, this.track.length), {});
    return { p: new THREE.Vector3(f.x + f.rx * lat, f.y + up, f.z + f.rz * lat), h: f.heading, f };
  }
  addStd(geo, color, m, extra = {}) {
    const mesh = this.mesh(geo, this.mat(new THREE.MeshStandardMaterial({ color, roughness: 0.7, ...extra })));
    if (m) mesh.applyMatrix4(m);
    return mesh;
  }

  buildLandmarks() {
    const c = this.city, n = this.night;
    const glowWhite = (col = 0xfff6e0) => ({ emissive: col, emissiveIntensity: n ? 0.8 : 0 });
    for (const id of c.marcos) {
      if (id === 'masp') {
        // museu vermelho suspenso por dois pórticos
        const { p, h } = this.at(330, 34, 0.2); const m = placeMatrix(p, h);
        const red = 0xd32f2f;
        for (const z of [-28, 28]) {
          this.addStd(new THREE.BoxGeometry(2.2, 16, 2.2).translate(-10, 8, z), red, m, glowWhite(0x400000));
          this.addStd(new THREE.BoxGeometry(2.2, 16, 2.2).translate(10, 8, z), red, m, glowWhite(0x400000));
        }
        this.addStd(new THREE.BoxGeometry(2.4, 2.4, 60).translate(-10, 17, 0), red, m);
        this.addStd(new THREE.BoxGeometry(2.4, 2.4, 60).translate(10, 17, 0), red, m);
        this.addStd(new THREE.BoxGeometry(18, 8, 56).translate(0, 11, 0), 0x2b3a46, m, { roughness: 0.2, metalness: 0.5, emissive: 0x3b6a9a, emissiveIntensity: n ? 0.6 : 0 });
      }
      if (id === 'antenas') {
        for (let k = 0; k < 3; k++) {
          const { p, h } = this.at(500 + k * 400, (k % 2 ? -1 : 1) * 130, -5);
          const m = placeMatrix(p, h);
          this.addStd(new THREE.CylinderGeometry(0.8, 2.6, 120, 4, 1, true).translate(0, 60, 0), 0xc62828, m, { wireframe: true });
          this.addStd(new THREE.CylinderGeometry(0.6, 0.6, 120, 6).translate(0, 60, 0), 0xdddddd, m);
          this.blinkers.push(new THREE.Vector3(0, 121, 0).applyMatrix4(m));
        }
        this.refreshBlinkers();
      }
      if (id === 'paodeacucar') {
        const { p, h } = this.at(900, -520, 0);
        p.y = this.seaY - 2;
        const mat = this.mat(new THREE.MeshStandardMaterial({ color: 0x5f7d57, roughness: 1, flatShading: true }));
        const a = this.mesh(new THREE.SphereGeometry(60, 18, 14), mat); a.scale.set(0.9, 2.3, 0.9); a.position.copy(p);
        const b = this.mesh(new THREE.SphereGeometry(50, 16, 12), mat); b.scale.set(1.1, 1.3, 1.0); b.position.copy(p).add(new THREE.Vector3(150, 0, 110));
        // cabo do bondinho
        const geo = new THREE.BufferGeometry().setFromPoints([a.position.clone().add(new THREE.Vector3(0, 135, 0)), b.position.clone().add(new THREE.Vector3(0, 62, 0))]);
        this.group.add(new THREE.Line(geo, this.mat(new THREE.LineBasicMaterial({ color: 0x222222 })))); this.disposables.push(geo);
      }
      if (id === 'cristo') {
        const { p } = this.at(1100, 380, 0);
        p.y = this.track.minY - 10;
        const hill = this.addStd(new THREE.ConeGeometry(160, 300, 16), 0x3d6a37, null, { flatShading: true, roughness: 1 });
        hill.position.copy(p).add(new THREE.Vector3(0, 150, 0));
        const top = p.clone().add(new THREE.Vector3(0, 300, 0));
        const statue = new THREE.Group(); statue.position.copy(top);
        const sm = this.mat(new THREE.MeshStandardMaterial({ color: 0xf2efe6, roughness: 0.6, emissive: 0xfff6e0, emissiveIntensity: n ? 1.1 : 0 }));
        const body = new THREE.Mesh(new THREE.CylinderGeometry(3, 5, 26, 8).translate(0, 13, 0), sm);
        const arms = new THREE.Mesh(new THREE.BoxGeometry(30, 3, 3).translate(0, 22, 0), sm);
        const head = new THREE.Mesh(new THREE.SphereGeometry(2.6, 8, 6).translate(0, 28.5, 0), sm);
        statue.add(body, arms, head); this.group.add(statue);
        this.disposables.push(body.geometry, arms.geometry, head.geometry);
        statue.lookAt(this.track.pos(this.track.length / 2, 0)); statue.rotation.x = 0; statue.rotation.z = 0;
      }
      if (id === 'serra') {
        const mat = this.mat(new THREE.MeshStandardMaterial({ color: 0x6b7f4e, roughness: 1, flatShading: true }));
        for (let k = 0; k < 9; k++) {
          const { p } = this.at(150 + k * 160, 420 + (k % 3) * 60, 0); p.y = this.track.minY - 30;
          const mnt = this.mesh(new THREE.ConeGeometry(160 + (k % 2) * 50, 220 + (k % 3) * 60, 7), mat);
          mnt.position.copy(p).add(new THREE.Vector3(0, 100, 0)); mnt.scale.set(1.6, 1, 1);
        }
      }
      if (id === 'pampulha') {
        // igrejinha de abóbadas onduladas (estilizada)
        const { p, h } = this.at(300, -32, 0.2); const m = placeMatrix(p, h);
        const white = 0xf3f3f3;
        [[0, 11, 9], [-12, 7, 6], [9, 7, 6], [17, 5, 4]].forEach(([z, rad, w]) => {
          this.addStd(new THREE.CylinderGeometry(rad, rad, w, 20, 1, true, 0, Math.PI).rotateZ(Math.PI / 2).rotateY(Math.PI / 2).translate(0, 0, z), white, m, { side: THREE.DoubleSide, ...glowWhite() });
        });
        // painel azul de azulejos
        this.addStd(new THREE.BoxGeometry(0.3, 6, 12).translate(4.5, 3, -2), 0x3f6fd8, m);
      }
      if (id === 'elevador') {
        const { p, h } = this.at(360, -26, -18); const m = placeMatrix(p, h);
        this.addStd(new THREE.BoxGeometry(9, 75, 14).translate(0, 37.5, 0), 0xeee3c6, m, glowWhite(0xffe8b0));
        this.addStd(new THREE.BoxGeometry(11, 10, 16).translate(0, 74, 0), 0xe6d7b0, m, glowWhite(0xffe8b0));
        // passarela até a rua
        this.addStd(new THREE.BoxGeometry(14, 2, 5).translate(10, 66, 0), 0xd8c9a2, m);
        // letreiro
        const sign = new THREE.Mesh(new THREE.PlaneGeometry(10, 2.4), this.mat(new THREE.MeshBasicMaterial({ map: this.disp(textSign('ELEVADOR', '#1d3557')), side: THREE.DoubleSide })));
        sign.applyMatrix4(new THREE.Matrix4().multiplyMatrices(m, new THREE.Matrix4().makeTranslation(5.6, 76, 0).multiply(new THREE.Matrix4().makeRotationY(Math.PI / 2))));
        this.group.add(sign); this.disposables.push(sign.geometry);
      }
      if (id === 'igreja') {
        const { p, h } = this.at(1080, 34, 0.2); const m = placeMatrix(p, h, 1, Math.PI / 2);
        this.addStd(new THREE.BoxGeometry(22, 16, 30).translate(0, 8, 0), 0xf6f1e3, m, glowWhite());
        for (const x of [-9, 9]) {
          this.addStd(new THREE.BoxGeometry(6, 26, 6).translate(x, 13, 14), 0xf6f1e3, m, glowWhite());
          this.addStd(new THREE.ConeGeometry(3.6, 5, 8).translate(x, 28.5, 14), 0x2f6fb5, m);
        }
        this.addStd(new THREE.BoxGeometry(5, 8, 0.4).translate(0, 4, 15.1), 0x6b3f22, m);
      }
      if (id === 'estufa') {
        // estufa de vidro (estilizada)
        const { p, h } = this.at(1050, 42, 0.2); const m = placeMatrix(p, h);
        const glass = { color: 0xbfe6ff, roughness: 0.05, metalness: 0.3, transparent: true, opacity: 0.45, emissive: 0x9fd6ff, emissiveIntensity: n ? 0.5 : 0 };
        const parts = [
          new THREE.BoxGeometry(14, 10, 36).translate(0, 5, 0),
          new THREE.CylinderGeometry(7, 7, 36, 16, 1, false, 0, Math.PI).rotateZ(Math.PI / 2).rotateY(Math.PI / 2).translate(0, 10, 0),
          new THREE.SphereGeometry(8, 16, 10, 0, Math.PI * 2, 0, Math.PI / 2).translate(0, 16, 0),
        ];
        for (const g of parts) {
          this.addStd(g, 0xbfe6ff, m, glass);
          const e = new THREE.LineSegments(new THREE.EdgesGeometry(g, 20), this.mat(new THREE.LineBasicMaterial({ color: 0xffffff })));
          e.applyMatrix4(m); this.group.add(e); this.disposables.push(e.geometry);
        }
        // jardim
        const gm = this.mat(new THREE.MeshStandardMaterial({ color: 0x3f9a46 }));
        const lawn = this.mesh(new THREE.PlaneGeometry(40, 70).rotateX(-Math.PI / 2), gm); lawn.applyMatrix4(placeMatrix(p.clone().add(new THREE.Vector3(0, -0.15, 0)), h));
      }
      if (id === 'tubo') {
        // estação-tubo de ônibus
        const { p, h } = this.at(380, 15.5, 0.2); const m = placeMatrix(p, h);
        const tube = new THREE.CylinderGeometry(1.7, 1.7, 14, 20, 1, true).rotateX(Math.PI / 2).translate(0, 2.2, 0);
        this.addStd(tube, 0x9ed8ff, m, { transparent: true, opacity: 0.4, roughness: 0.05, side: THREE.DoubleSide, emissive: 0x6ac0ff, emissiveIntensity: n ? 0.7 : 0 });
        for (let z = -7; z <= 7; z += 2) this.addStd(new THREE.TorusGeometry(1.72, 0.08, 4, 20).translate(0, 2.2, z), 0x8e9aa0, m);
        this.addStd(new THREE.BoxGeometry(3.6, 0.5, 14).translate(0, 0.25, 0), 0x777777, m);
      }
    }
  }
  refreshBlinkers() {
    if (this.blinkMesh) { this.group.remove(this.blinkMesh); }
    const bm = new THREE.InstancedMesh(new THREE.SphereGeometry(0.6, 6, 4), this.mat(new THREE.MeshBasicMaterial({ color: 0xff2020 })), this.blinkers.length);
    this.blinkers.forEach((p, i) => bm.setMatrixAt(i, new THREE.Matrix4().makeTranslation(p.x, p.y, p.z)));
    this.blinkMesh = bm; this.group.add(bm); this.disposables.push(bm.geometry);
  }

  // altura do chão em qualquer ponto ao lado da rua
  groundY(f, lat) {
    const a = Math.abs(lat), c = this.city;
    if (a <= ROAD_HALF) return f.y;
    if (a <= SIDEWALK) return f.y + 0.2;
    const lerp = (x, y, t) => x + (y - x) * Math.max(0, Math.min(1, t));
    if (c.mar && Math.sign(lat) === c.mar.lado) {
      const mid = Math.max(this.seaY + 0.6, f.y - 2.5);
      return a <= 48 ? lerp(f.y + 0.2, mid, (a - 12) / 36) : lerp(mid, this.seaY - 1.2, (a - 48) / 22);
    }
    const outer = c.mar ? Math.max(this.seaY - 0.5, f.y - 14) : f.y - 22;
    return a <= 60 ? f.y + 0.2 : lerp(f.y + 0.2, outer, (a - 60) / 130);
  }

  buildMountains() {
    const c = this.city, r = this.r;
    const mat = this.mat(new THREE.MeshStandardMaterial({ color: c.montanhas.cor, roughness: 1, flatShading: true }));
    const N = 34, geo = new THREE.ConeGeometry(1, 1, 6, 2);
    const im = new THREE.InstancedMesh(geo, mat, N);
    const baseY = (c.mar ? this.seaY : this.track.minY - 24);
    for (let i = 0; i < N; i++) {
      const a = i / N * Math.PI * 2 + r() * 0.1, R = this.extent + range(r, 250, 650);
      const H = c.montanhas.alt * range(r, 0.6, 1.8), W = H * range(r, 1.6, 2.6);
      const m = new THREE.Matrix4().compose(new THREE.Vector3(this.center.x + Math.cos(a) * R, baseY + H / 2 - 5, this.center.z + Math.sin(a) * R),
        _q.setFromAxisAngle(UP, r() * 6), _s.set(W, H, W));
      im.setMatrixAt(i, m);
    }
    this.group.add(im); this.disposables.push(geo);
  }

  // ---------------- praça da dança ----------------
  buildPraca() {
    const T = this.track, p0 = T.praca;
    const sw = this.disp(TX.sidewalk('petit'));
    const mat = this.mat(new THREE.MeshStandardMaterial({ map: sw, roughness: 0.85 }));
    this.strip(SIDEWALK, 50, 0.22, 0.22, mat, { from: p0 - 45, to: p0 + 45, step: 3, uScale: 13, vScale: 1 / 3 });
    // bandeirinhas atravessando a rua
    const flags = [], colors = [0xff3d7f, 0xffc400, 0x00c853, 0x2979ff, 0xff6d00, 0xaa00ff];
    const lines = [];
    for (let s = p0 - 40; s <= p0 + 40; s += 8) {
      const a = this.at(s, -SIDEWALK + 0.5, 7.5).p, b = this.at(s, 30, 7.5).p;
      for (let k = 0; k <= 22; k++) {
        const t = k / 22; const q = a.clone().lerp(b, t); q.y -= Math.sin(t * Math.PI) * 1.6;
        lines.push(q);
        if (k < 22) flags.push([q.clone(), colors[(k + s) % colors.length]]);
      }
      const geo = new THREE.BufferGeometry().setFromPoints(lines.splice(0));
      this.group.add(new THREE.Line(geo, this.mat(new THREE.LineBasicMaterial({ color: 0x333333 })))); this.disposables.push(geo);
    }
    const fg = new THREE.BufferGeometry(); fg.setAttribute('position', new THREE.Float32BufferAttribute([-0.35, 0, 0, 0.35, 0, 0, 0, -0.6, 0], 3)); fg.computeVertexNormals();
    const fm = new THREE.InstancedMesh(fg, this.mat(new THREE.MeshStandardMaterial({ side: THREE.DoubleSide, roughness: 0.8 })), flags.length);
    const h = T.frame(p0, {}).heading;
    flags.forEach(([q, col], i) => { fm.setMatrixAt(i, placeMatrix(q, h)); fm.setColorAt(i, _c.set(col)); });
    this.group.add(fm); this.disposables.push(fg);
    // torres de luz da praça
    this.pracaBeams = [];
    const beamTex = this.disp(TX.beamTexture());
    for (const [ds, lat] of [[-30, 22], [30, 22], [-30, 42], [30, 42]]) {
      const { p, h: hh } = this.at(p0 + ds, lat, 0.2);
      const m = placeMatrix(p, hh);
      this.addStd(new THREE.CylinderGeometry(0.25, 0.35, 12, 6).translate(0, 6, 0), 0x333333, m);
      this.addStd(new THREE.BoxGeometry(1.4, 0.8, 0.8).translate(0, 12.2, 0), 0x222222, m);
      if (this.night) {
        const cone = new THREE.Mesh(new THREE.ConeGeometry(2.6, 30, 16, 1, true).translate(0, -15, 0),
          this.mat(new THREE.MeshBasicMaterial({ map: beamTex, color: colors[this.pracaBeams.length % colors.length], transparent: true, opacity: 0.5, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide, fog: false })));
        cone.position.copy(p).add(new THREE.Vector3(0, 12.2, 0));
        this.group.add(cone); this.disposables.push(cone.geometry);
        this.pracaBeams.push(cone);
      }
    }
  }

  // ---------------- público ----------------
  buildCrowd() {
    const T = this.track, r = this.r, p0 = T.praca, people = [];
    for (let k = 0; k < 150; k++) people.push([p0 + range(r, -38, 38), range(r, 13, 46)]);
    for (let k = 0; k < 40; k++) people.push([p0 + range(r, -38, 38), -range(r, 9.6, 11.6)]);
    for (let s = 70; s < T.length - 40; s += range(r, 25, 60)) {
      if (Math.abs(s - p0) < 50) continue;
      const side = r() < 0.5 ? -1 : 1, n = 2 + Math.floor(r() * 6);
      if (this.city.id === 'rio' && side < 0) { for (let k = 0; k < n; k++) people.push([s + range(r, -3, 3), -range(r, 13, 30)]); continue; }
      for (let k = 0; k < n; k++) people.push([s + range(r, -4, 4), side * range(r, 9.8, 11.7)]);
    }
    const N = people.length;
    const body = new THREE.CylinderGeometry(0.24, 0.3, 1.05, 7).translate(0, 0.55, 0);
    const head = new THREE.SphereGeometry(0.2, 8, 6).translate(0, 1.32, 0);
    const arms = new THREE.BoxGeometry(0.9, 0.12, 0.12).translate(0, 0, 0);
    const bm = new THREE.InstancedMesh(body, this.mat(new THREE.MeshStandardMaterial({ roughness: 0.8 })), N);
    const hm = new THREE.InstancedMesh(head, this.mat(new THREE.MeshStandardMaterial({ roughness: 0.8 })), N);
    const am = new THREE.InstancedMesh(arms, this.mat(new THREE.MeshStandardMaterial({ roughness: 0.8 })), N);
    const shirts = [0xff3d7f, 0xffc400, 0x00c853, 0x2979ff, 0xffffff, 0xff6d00, 0x00bcd4, 0xe53935, 0x8e24aa, 0xfdd835];
    const skins = [0xf1c27d, 0xe0ac69, 0xc68642, 0x8d5524, 0xffdbac, 0x6b3e26];
    this.people = people.map(([s, lat], i) => {
      const { p, h } = this.at(s, lat, 0.2);
      p.y = this.groundY(this.track.frame(s, {}), lat);
      const scale = range(r, 0.7, 1.15);
      const shirt = pick(r, shirts), skin = pick(r, skins);
      bm.setColorAt(i, _c.set(shirt)); hm.setColorAt(i, _c.set(skin)); am.setColorAt(i, _c.set(skin));
      return { s, p, h: h + (lat > 0 ? -Math.PI / 2 : Math.PI / 2) + range(r, -0.5, 0.5), scale, ph: r() * 6.28, sp: range(r, 0.8, 1.2) };
    });
    this.crowd = { bm, hm, am };
    for (const m of [bm, hm, am]) { m.instanceMatrix.setUsage(THREE.DynamicDrawUsage); this.group.add(m); }
    this.disposables.push(body, head, arms);
    this.updateCrowd(0, 0, -1000, false);
  }

  updateCrowd(t, beat, truckS, partying) {
    if (!this.crowd) return;
    const { bm, hm, am } = this.crowd, m = new THREE.Matrix4(), q = new THREE.Quaternion(), sc = new THREE.Vector3(), pos = new THREE.Vector3();
    const qa = new THREE.Quaternion(), ma = new THREE.Matrix4();
    for (let i = 0; i < this.people.length; i++) {
      const P = this.people[i];
      const near = Math.abs(P.s - truckS) < 45 || partying && Math.abs(P.s - this.track.praca) < 60;
      const e = near ? 1 : 0.15;
      const jump = Math.max(0, Math.sin((beat * Math.PI) + P.ph)) * 0.35 * e;
      pos.copy(P.p); pos.y += jump;
      q.setFromAxisAngle(UP, P.h + (near ? Math.sin(t * 3 * P.sp + P.ph) * 0.3 : 0));
      sc.setScalar(P.scale);
      m.compose(pos, q, sc);
      bm.setMatrixAt(i, m); hm.setMatrixAt(i, m);
      // braços: pra cima quando a carreta passa
      const armUp = near ? 1.25 + Math.sin(t * 8 * P.sp + P.ph) * 0.12 : 0.85;
      qa.setFromAxisAngle(new THREE.Vector3(0, 0, 1), near ? Math.sin(t * 6 + P.ph) * 0.4 : 0);
      ma.compose(new THREE.Vector3(0, armUp, 0), qa, new THREE.Vector3(1, 1, 1));
      am.setMatrixAt(i, m.clone().multiply(ma));
    }
    bm.instanceMatrix.needsUpdate = hm.instanceMatrix.needsUpdate = am.instanceMatrix.needsUpdate = true;
  }

  // ---------------- portais de entrada e chegada ----------------
  buildGates() {
    const c = this.city;
    for (const [s, text] of [[45, `${c.nome.toUpperCase()} - ${c.uf}`], [this.track.length - 25, 'CHEGADA!']]) {
      const { p, h } = this.at(s, 0, 0.2); const m = placeMatrix(p, h);
      for (const lat of [-10.5, 10.5]) this.addStd(new THREE.CylinderGeometry(0.35, 0.35, 9, 8).translate(lat, 4.5, 0), 0xeeeeee, m);
      const tex = this.disp(textSign(text, 'rainbow'));
      const sign = new THREE.Mesh(new THREE.BoxGeometry(21.5, 3.2, 0.4), [
        this.mat(new THREE.MeshStandardMaterial({ color: 0xffffff })), this.mat(new THREE.MeshStandardMaterial({ color: 0xffffff })),
        this.mat(new THREE.MeshStandardMaterial({ color: 0xffffff })), this.mat(new THREE.MeshStandardMaterial({ color: 0xffffff })),
        this.mat(new THREE.MeshStandardMaterial({ map: tex, emissive: 0xffffff, emissiveMap: tex, emissiveIntensity: this.night ? 0.8 : 0.15 })),
        this.mat(new THREE.MeshStandardMaterial({ map: tex, emissive: 0xffffff, emissiveMap: tex, emissiveIntensity: this.night ? 0.8 : 0.15 })),
      ]);
      // a face -Z (index 5) fica de frente pra quem chega
      tex.wrapS = THREE.RepeatWrapping;
      sign.applyMatrix4(new THREE.Matrix4().multiplyMatrices(m, new THREE.Matrix4().makeTranslation(0, 9.5, 0)));
      this.group.add(sign); this.disposables.push(sign.geometry);
    }
  }

  update(dt, t, beat, energy, truckS, camera, partying) {
    this.sky.position.copy(camera.position);
    this.skyObj.position.copy(camera.position);
    if (this.blinkMesh) this.blinkMesh.visible = (t % 1.4) < 0.7;
    this.updateCrowd(t, beat, truckS, partying);
    if (this.pracaBeams) this.pracaBeams.forEach((b, i) => {
      b.rotation.z = Math.sin(t * 0.9 + i * 1.7) * 0.6;
      b.rotation.x = Math.cos(t * 0.7 + i) * 0.4;
      b.material.opacity = 0.18 + energy * 0.25;
    });
  }

  dispose() {
    this.scene.remove(this.group);
    this.group.traverse(o => { if (o.isInstancedMesh) o.dispose(); });
    for (const d of this.disposables) d.dispose && d.dispose();
    this.scene.fog = null;
  }
}
