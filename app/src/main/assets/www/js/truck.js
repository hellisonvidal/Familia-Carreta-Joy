import * as THREE from 'three';
import * as TX from './textures.js';
import { RING } from './city.js';

// Comprimento da carreta (metros) depois de escalar o modelo do Meshy
export const TRUCK_LEN = 17;
const _c = new THREE.Color();
const SKIP_PLAYER = new Set(['pessoa']);
const SKIP_AUTO = new Set(['pessoa', 'banco', 'arvore', 'poste', 'carro']);

// Variações: o mesmo modelo com a pintura recolorida + enfeites próprios
export const VARIANTS = [
  { id: 'arcoiris', nome: 'Arco-Íris', desc: 'A original, todas as cores', filtro: null, hue: [0, 1], extra: null, cor: '#ff3d7f' },
  { id: 'dourada', nome: 'Furacão Dourada', desc: 'Brilha igual troféu', filtro: 'sepia(1) saturate(2.6) hue-rotate(-12deg) brightness(1.1)', hue: [0.1, 0.06], extra: 'estrela', cor: '#ffc400' },
  { id: 'neon', nome: 'Neon Azul', desc: 'Tubos de neon nas laterais', filtro: 'hue-rotate(170deg) saturate(1.5)', hue: [0.5, 0.25], extra: 'neon', cor: '#00e5ff' },
  { id: 'rosa', nome: 'Rosa Choque', desc: 'Com balões de coração', filtro: 'sepia(1) saturate(4) hue-rotate(285deg) brightness(1.05)', hue: [0.9, 0.12], extra: 'coracoes', cor: '#ff4fb8' },
  { id: 'brasil', nome: 'Verde-Amarela', desc: 'Bandeiras do Brasil', filtro: 'sepia(1) saturate(3.5) hue-rotate(40deg)', hue: [0.2, 0.15], extra: 'bandeiras', cor: '#00c853' },
  { id: 'trio', nome: 'Trio Elétrico', desc: 'Torre de som gigante', filtro: 'grayscale(0.5) contrast(1.3) brightness(0.85)', hue: [0.75, 0.35], extra: 'torre', cor: '#7c4dff' },
];

function recolor(model, filtro) {
  if (!filtro) return;
  const done = new Map();
  model.traverse(o => {
    if (!o.isMesh || !o.material) return;
    const m = o.material.clone(); o.material = m;
    const map = m.map; if (!map || !map.image) return;
    if (!done.has(map)) {
      const img = map.image, c = document.createElement('canvas');
      c.width = img.width; c.height = img.height;
      const g = c.getContext('2d'); g.filter = filtro; g.drawImage(img, 0, 0);
      const t = new THREE.CanvasTexture(c);
      t.flipY = map.flipY; t.colorSpace = THREE.SRGBColorSpace; t.wrapS = map.wrapS; t.wrapT = map.wrapT; t.channel = map.channel; t.anisotropy = 4;
      done.set(map, t);
    }
    m.map = done.get(map);
  });
}

export class Truck {
  constructor(gltfScene, variant = VARIANTS[0]) {
    this.variant = variant;
    this.root = new THREE.Group();
    this.root.matrixAutoUpdate = false;
    const model = gltfScene.clone(true);
    recolor(model, variant.filtro);
    const box = new THREE.Box3().setFromObject(model);
    const size = box.getSize(new THREE.Vector3());
    const S = TRUCK_LEN / size.x;
    this.S = S;
    model.scale.setScalar(S);
    model.rotation.y = Math.PI / 2; // cabine para +Z (frente)
    model.position.y = -box.min.y * S;
    model.traverse(o => { if (o.isMesh) o.frustumCulled = false; });
    this.root.add(model);
    this.height = size.y * S;
    this.roofY = this.height - 0.12;
    this.halfW = size.z * S / 2;
    this.roofZ = [-0.86 * S, 0.58 * S];
    // estado de direção
    this.x = 0; this.z = 0; this.y = 0; this.yaw = 0; this.v = 0; this.steer = 0; this.pitch = 0; this.roll = 0;
    this.autoS = 0; this.autoDist = 0; this.route = null;
    this.vel = new THREE.Vector3();
    this.night = false;
    this.buildLights();
    this.buildExtras();
  }

  buildLights() {
    const R = this.root, H = this.roofY, W = this.halfW;
    const bulbs = [];
    for (let z = this.roofZ[0] + 0.3; z <= this.roofZ[1] - 0.2; z += 0.45) bulbs.push([W + 0.05, H + 0.08, z], [-W - 0.05, H + 0.08, z], [W + 0.08, 1.05, z], [-W - 0.08, 1.05, z]);
    for (let x = -W; x <= W; x += 0.45) bulbs.push([x, H + 0.08, this.roofZ[0]], [x, H + 0.08, this.roofZ[1]]);
    this.bulbData = bulbs;
    this.bulbs = new THREE.InstancedMesh(new THREE.SphereGeometry(0.09, 6, 4), new THREE.MeshBasicMaterial({ color: 0xffffff, toneMapped: false }), bulbs.length);
    bulbs.forEach((b, i) => { this.bulbs.setMatrixAt(i, new THREE.Matrix4().makeTranslation(b[0], b[1], b[2])); this.bulbs.setColorAt(i, _c.setHSL(i / bulbs.length, 1, 0.6)); });
    this.bulbs.frustumCulled = false; R.add(this.bulbs);
    const beamTex = TX.beamTexture();
    this.beams = [];
    [[W - 0.2, this.roofZ[0] + 0.4], [-W + 0.2, this.roofZ[0] + 0.4], [W - 0.2, this.roofZ[1] - 0.3], [-W + 0.2, this.roofZ[1] - 0.3]].forEach(([x, z], i) => {
      const holder = new THREE.Group(); holder.position.set(x, H + 0.35, z);
      holder.add(new THREE.Mesh(new THREE.CylinderGeometry(0.22, 0.28, 0.4, 8), new THREE.MeshStandardMaterial({ color: 0x222222, metalness: 0.6, roughness: 0.4 })));
      const cone = new THREE.Mesh(new THREE.ConeGeometry(2.6, 26, 18, 1, true).translate(0, -13, 0),
        new THREE.MeshBasicMaterial({ map: beamTex, transparent: true, opacity: 0.5, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide, fog: false, toneMapped: false }));
      cone.rotation.x = Math.PI; cone.frustumCulled = false;
      const pivot = new THREE.Group(); pivot.add(cone); holder.add(pivot); R.add(holder);
      this.beams.push({ pivot, cone, i });
    });
    this.lasers = new THREE.Group();
    const lg = new THREE.BoxGeometry(0.05, 0.05, 60).translate(0, 0, -30);
    for (let k = 0; k < 7; k++) {
      const m = new THREE.Mesh(lg, new THREE.MeshBasicMaterial({ color: k % 2 ? 0x00ff88 : 0xff00cc, transparent: true, opacity: 0.8, blending: THREE.AdditiveBlending, depthWrite: false, fog: false, toneMapped: false }));
      m.userData.k = k; m.frustumCulled = false; this.lasers.add(m);
    }
    this.lasers.position.set(0, H + 0.5, this.roofZ[0] + 0.2); R.add(this.lasers);
    this.disco = new THREE.Mesh(new THREE.IcosahedronGeometry(0.45, 1), new THREE.MeshStandardMaterial({ color: 0xdddddd, metalness: 1, roughness: 0.15, flatShading: true, emissive: 0x333333 }));
    this.disco.position.set(0, H + 2.2, this.roofZ[0] + 2.5);
    const pole = new THREE.Mesh(new THREE.CylinderGeometry(0.04, 0.04, 2, 4), new THREE.MeshStandardMaterial({ color: 0x888888 })); pole.position.set(0, H + 1.1, this.roofZ[0] + 2.5);
    R.add(this.disco, pole);
    const head = new THREE.MeshBasicMaterial({ color: 0xfffbe8, toneMapped: false });
    for (const x of [-1.0, 1.0]) { const h = new THREE.Mesh(new THREE.CircleGeometry(0.22, 12), head); h.position.set(x, 0.95, TRUCK_LEN / 2 + 0.05); R.add(h); }
    this.headSpot = new THREE.SpotLight(0xfff1d0, 0, 60, 0.55, 0.6, 1.2);
    this.headSpot.position.set(0, 1.2, TRUCK_LEN / 2); this.headSpot.target.position.set(0, 0, TRUCK_LEN / 2 + 20);
    R.add(this.headSpot, this.headSpot.target);
    this.party = [0xff2d95, 0x00e5ff, 0xffd400].map((c, i) => { const l = new THREE.PointLight(c, 0, 22, 1.6); l.position.set(i === 1 ? -W - 2.5 : W + 2.5, H + 1.5, -3 + i * 4); R.add(l); return l; });
    const sh = new THREE.Mesh(new THREE.PlaneGeometry(W * 2 + 1.2, TRUCK_LEN + 1).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ map: TX.blobShadow(), transparent: true, depthWrite: false, opacity: 0.9 }));
    sh.position.y = 0.05; sh.renderOrder = 1; R.add(sh);
  }

  buildExtras() {
    const R = this.root, H = this.roofY, W = this.halfW, e = this.variant.extra;
    if (e === 'estrela') {
      const s = new THREE.Shape(); for (let i = 0; i < 10; i++) { const a = i / 10 * Math.PI * 2 + Math.PI / 2, r = i % 2 ? 0.55 : 1.3; i ? s.lineTo(Math.cos(a) * r, Math.sin(a) * r) : s.moveTo(Math.cos(a) * r, Math.sin(a) * r); }
      const star = new THREE.Mesh(new THREE.ExtrudeGeometry(s, { depth: 0.25, bevelEnabled: false }), new THREE.MeshStandardMaterial({ color: 0xffd54f, metalness: 0.8, roughness: 0.25, emissive: 0xffb300, emissiveIntensity: 0.5 }));
      star.position.set(0, H + 2.2, TRUCK_LEN / 2 - 3.2); R.add(star); this.spin = star;
    }
    if (e === 'neon') for (const y of [0.6, H - 0.15]) for (const s of [-1, 1]) {
      const t = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.06, TRUCK_LEN - 2.5, 6).rotateX(Math.PI / 2), new THREE.MeshBasicMaterial({ color: y < 1 ? 0x00e5ff : 0xff00e5, toneMapped: false }));
      t.position.set(s * (W + 0.1), y, -1); R.add(t);
    }
    if (e === 'coracoes') for (const [x, z] of [[W, this.roofZ[0]], [-W, this.roofZ[0]], [W, this.roofZ[1]], [-W, this.roofZ[1]]]) for (let k = 0; k < 3; k++) {
      const b = new THREE.Mesh(new THREE.SphereGeometry(0.45, 12, 10), new THREE.MeshStandardMaterial({ color: [0xff4fb8, 0xff1744, 0xffffff][k], roughness: 0.3, emissive: 0x330011 }));
      b.scale.set(1, 1.15, 1); b.position.set(x + (k - 1) * 0.5, H + 3 + k * 0.4, z); R.add(b);
      const str = new THREE.Mesh(new THREE.CylinderGeometry(0.01, 0.01, 3 + k * 0.4, 3), new THREE.MeshBasicMaterial({ color: 0xffffff })); str.position.set(x + (k - 1) * 0.25, H + 1.5 + k * 0.2, z); R.add(str);
    }
    if (e === 'bandeiras') for (const s of [-1, 1]) {
      const pole = new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.05, 4, 6), new THREE.MeshStandardMaterial({ color: 0xdddddd })); pole.position.set(s * (W - 0.2), H + 2, this.roofZ[0] + 0.3); R.add(pole);
      const c = document.createElement('canvas'); c.width = 96; c.height = 64; const g = c.getContext('2d');
      g.fillStyle = '#009c3b'; g.fillRect(0, 0, 96, 64); g.fillStyle = '#ffdf00'; g.beginPath(); g.moveTo(48, 6); g.lineTo(90, 32); g.lineTo(48, 58); g.lineTo(6, 32); g.fill(); g.fillStyle = '#002776'; g.beginPath(); g.arc(48, 32, 14, 0, 7); g.fill();
      const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace;
      const f = new THREE.Mesh(new THREE.PlaneGeometry(1.8, 1.2), new THREE.MeshStandardMaterial({ map: t, side: THREE.DoubleSide })); f.position.set(s * (W - 0.2), H + 3.4, this.roofZ[0] - 0.6); f.rotation.y = Math.PI / 2; R.add(f);
      (this.flags = this.flags || []).push(f);
    }
    if (e === 'torre') {
      const mat = new THREE.MeshStandardMaterial({ color: 0x151515, roughness: 0.5 });
      const cone = new THREE.MeshStandardMaterial({ color: 0x333333, metalness: 0.5, roughness: 0.4, emissive: 0x7c4dff, emissiveIntensity: 0.4 });
      for (let k = 0; k < 3; k++) {
        const b = new THREE.Mesh(new THREE.BoxGeometry(W * 2 - 0.2, 1.1, 1.4), mat); b.position.set(0, H + 0.55 + k * 1.1, this.roofZ[0] + 0.8); R.add(b);
        for (const x of [-0.9, 0, 0.9]) { const sp = new THREE.Mesh(new THREE.CircleGeometry(0.4, 16), cone); sp.position.set(x, H + 0.55 + k * 1.1, this.roofZ[0] + 0.1); sp.rotation.y = Math.PI; R.add(sp); }
      }
      this.roofZ = [this.roofZ[0] + 1.6, this.roofZ[1]];
    }
  }

  setNight(n) { this.night = n; this.lasers.visible = n; this.headSpot.intensity = n ? 60 : 0; }

  // ---------------- posição no mundo ----------------
  place(city) {
    const f = Math.sin(this.yaw), c = Math.cos(this.yaw), half = TRUCK_LEN * 0.42;
    const yf = city.floorY(this.x + f * half, this.z + c * half), yb = city.floorY(this.x - f * half, this.z - c * half);
    const yl = city.floorY(this.x + c * 1.4, this.z - f * 1.4), yr = city.floorY(this.x - c * 1.4, this.z + f * 1.4);
    const ty = (yf + yb) / 2;
    this.y += (ty - this.y) * (this.inited ? 0.35 : 1); this.inited = true;
    this.pitch = Math.atan2(yf - yb, half * 2); this.roll = Math.atan2(yl - yr, 2.8) * 0.6;
    const q = new THREE.Quaternion().setFromEuler(new THREE.Euler(-this.pitch, this.yaw, -this.roll, 'YXZ'));
    this.root.matrix.compose(new THREE.Vector3(this.x, this.y, this.z), q, new THREE.Vector3(1, 1, 1));
    this.root.matrixWorldNeedsUpdate = true; this.root.updateMatrixWorld(true);
  }
  toWorld(v, out = new THREE.Vector3()) { return out.copy(v).applyMatrix4(this.root.matrixWorld); }
  toLocal(v, out = new THREE.Vector3()) { this._inv = this._inv || new THREE.Matrix4(); this._inv.copy(this.root.matrixWorld).invert(); return out.copy(v).applyMatrix4(this._inv); }
  get roofWorldY() { return this.y + this.roofY; }

  // ---------------- direção ----------------
  // ctrl = { player: true, throttle, steer } ou { auto: true, speed }
  drive(dt, city, ctrl) {
    const px = this.x, pz = this.z;
    if (ctrl.player) {
      const th = ctrl.throttle;
      if (th > 0.05) this.v += (this.v < 0 ? 9 : 3.2) * th * dt;
      else if (th < -0.05) this.v += (this.v > 0 ? 9 : 2.5) * th * dt;
      else this.v *= Math.max(0, 1 - dt * 0.8);
      this.v = Math.max(-5, Math.min(14, this.v));
      this.steer += (ctrl.steer * 0.55 - this.steer) * Math.min(1, dt * 4);
      this.yaw -= this.v * Math.tan(this.steer) / 9 * dt;
      this.route = null; this.lostRing = true;
    } else {
      // piloto automático: segue o anel (ou volta pra ele pelas ruas)
      let tx, tz;
      if (this.lostRing && !this.route) {
        const d = Math.hypot(this.x - city.ringPoint(city.ringNearestS(this.x, this.z)).x, this.z - city.ringPoint(city.ringNearestS(this.x, this.z)).z);
        if (d > 10) this.route = city.routeToRing(this.x, this.z);
        else { this.autoS = city.ringNearestS(this.x, this.z); }
        this.lostRing = false;
      }
      if (this.route) {
        const wp = this.route[0]; tx = wp[0]; tz = wp[1];
        if (Math.hypot(tx - this.x, tz - this.z) < 6) { this.route.shift(); if (!this.route.length) { this.route = null; this.autoS = city.ringNearestS(this.x, this.z); } }
      } else {
        // avança o progresso no anel
        for (let k = 0; k < 4; k++) { const p = city.ringPoint(this.autoS + 2); if ((p.x - this.x) * Math.sin(this.yaw) + (p.z - this.z) * Math.cos(this.yaw) < 6) { this.autoS += 2; this.autoDist += 2; } }
        const p = city.ringPoint(this.autoS + 9); tx = p.x; tz = p.z;
      }
      const want = Math.atan2(tx - this.x, tz - this.z);
      let d = want - this.yaw; while (d > Math.PI) d -= 2 * Math.PI; while (d < -Math.PI) d += 2 * Math.PI;
      this.yaw += Math.max(-1, Math.min(1, d * 2)) * Math.min(0.8, 0.12 + Math.abs(this.v) * 0.12) * dt;
      const target = Math.abs(d) > 1 ? Math.min(ctrl.speed, 3) : ctrl.speed;
      this.v += Math.max(-6 * dt, Math.min(2.5 * dt, target - this.v));
    }
    this.x += Math.sin(this.yaw) * this.v * dt;
    this.z += Math.cos(this.yaw) * this.v * dt;
    // colisões (4 círculos ao longo da carreta)
    let bump = false;
    const f = Math.sin(this.yaw), c = Math.cos(this.yaw), tmp = new THREE.Vector3();
    for (const off of [-6.5, -2.2, 2.2, 6.5]) {
      tmp.set(this.x + f * off, 0, this.z + c * off);
      const ox = tmp.x, oz = tmp.z;
      const hit = city.collide(tmp, 1.75, this.y + 0.3, ctrl.player ? SKIP_PLAYER : SKIP_AUTO);
      if (hit) { this.x += tmp.x - ox; this.z += tmp.z - oz; bump = true; }
    }
    if (bump && ctrl.player && Math.abs(this.v) > 3) { this.v *= 0.4; this.bumped = true; } else this.bumped = false;
    const lim = 600; this.x = Math.max(-lim, Math.min(lim, this.x)); this.z = Math.max(-lim, Math.min(lim, this.z));
    this.vel.set((this.x - px) / Math.max(dt, 1e-4), 0, (this.z - pz) / Math.max(dt, 1e-4));
    this.place(city);
  }

  update(dt, t, beat, energy, party) {
    const n = this.night, e = energy, [h0, hr] = this.variant.hue;
    for (let i = 0; i < this.bulbData.length; i++) {
      const hue = (h0 + ((i / 24 + beat * 0.25) % 1) * hr) % 1;
      const on = ((i + Math.floor(beat * 2)) % 4) !== 0 || !party;
      _c.setHSL(hue, 1, on ? (n ? 0.55 + e * 0.3 : 0.5) : 0.08);
      if (n) _c.multiplyScalar(1.6 + e);
      this.bulbs.setColorAt(i, _c);
    }
    this.bulbs.instanceColor.needsUpdate = true;
    this.beams.forEach(b => {
      b.pivot.rotation.z = Math.sin(t * 0.8 + b.i * 1.6) * 0.55; b.pivot.rotation.x = Math.cos(t * 0.6 + b.i) * 0.45;
      b.cone.material.color.setHSL((h0 + ((t * 0.05 + b.i * 0.25) % 1) * hr) % 1, 1, 0.55);
      b.cone.material.opacity = (n ? 0.32 : 0.07) + e * (n ? 0.4 : 0.08);
    });
    if (n) this.lasers.children.forEach(l => { const k = l.userData.k; l.rotation.y = (k - 3) * 0.18 + Math.sin(t * 1.3) * 0.25; l.rotation.x = 0.25 + Math.sin(t * 0.9 + k) * 0.12; l.material.opacity = 0.35 + e * 0.6; });
    this.disco.rotation.y += dt * 1.5;
    if (this.spin) this.spin.rotation.y += dt * 1.2;
    if (this.flags) this.flags.forEach((f, i) => f.rotation.x = Math.sin(t * 6 + i) * 0.15);
    this.party.forEach((l, i) => { l.intensity = (n ? 26 : 4) * (0.45 + 0.55 * (i === Math.floor(beat) % 3 ? 1 : e)); });
  }
}
