import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { N, HALF, RW, lineC, TRAFFIC } from './city.js';

// =====================================================================
// Vida na cidade: trânsito, pedestres andando, pombos que voam quando
// alguém chega perto, bolas que dá pra chutar, chafariz, fogos de
// artifício à noite e canhão de confete na saída da carreta.
// =====================================================================
const UP = new THREE.Vector3(0, 1, 0);
const _q = new THREE.Quaternion(), _s = new THREE.Vector3(), _p = new THREE.Vector3(), _m = new THREE.Matrix4(), _c = new THREE.Color();
const rnd = (a, b) => a + Math.random() * (b - a);
const M4 = (x, y, z, ry = 0, sx = 1, sy = 1, sz = 1) => _m.compose(_p.set(x, y, z), _q.setFromAxisAngle(UP, ry), _s.set(sx, sy, sz));

function colored(geo, color) {
  const g = geo.index ? geo.toNonIndexed() : geo; const c = new THREE.Color(color), n = g.attributes.position.count, a = new Float32Array(n * 3);
  for (let i = 0; i < n; i++) a.set([c.r, c.g, c.b], i * 3);
  g.setAttribute('color', new THREE.BufferAttribute(a, 3)); g.deleteAttribute('uv'); return g;
}
function carGeo(col, night) {
  return mergeGeometries([
    colored(new THREE.BoxGeometry(1.9, 0.7, 4.3).translate(0, 0.62, 0), col),
    colored(new THREE.BoxGeometry(1.72, 0.55, 2.3).translate(0, 1.22, -0.2), 0x22313f),
    colored(new THREE.BoxGeometry(1.74, 0.08, 2.35).translate(0, 1.52, -0.2), col),
    colored(new THREE.BoxGeometry(0.5, 0.16, 0.05).translate(-0.6, 0.72, 2.16), night ? 0xffffcc : 0xfff5cc),
    colored(new THREE.BoxGeometry(0.5, 0.16, 0.05).translate(0.6, 0.72, 2.16), night ? 0xffffcc : 0xfff5cc),
    colored(new THREE.BoxGeometry(0.45, 0.14, 0.05).translate(-0.6, 0.72, -2.16), 0xd32f2f),
    colored(new THREE.BoxGeometry(0.45, 0.14, 0.05).translate(0.6, 0.72, -2.16), 0xd32f2f),
    ...[[-0.92, 1.4], [0.92, 1.4], [-0.92, -1.4], [0.92, -1.4]].map(([x, z]) => colored(new THREE.CylinderGeometry(0.34, 0.34, 0.25, 12).rotateZ(Math.PI / 2).translate(x, 0.34, z), 0x111111)),
  ]);
}

export class Life {
  constructor(scene, city, night) {
    this.scene = scene; this.city = city; this.night = night;
    this.group = new THREE.Group(); scene.add(this.group);
    this.disp = [];
    this.buildCars(); this.buildWalkers(); this.buildPigeons(); this.buildBalls(); this.buildFountain();
    this.fx = []; this.fireT = 3;
  }
  d(x) { this.disp.push(x); return x; }

  // ---------------- trânsito ----------------
  buildCars() {
    const cols = [0xe53935, 0x1e88e5, 0xfdd835, 0x43a047, 0xffffff, 0x8e24aa, 0x212121, 0xff7043, 0x90a4ae];
    const geos = cols.map(c => this.d(carGeo(c, this.night)));
    const mat = this.d(new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.35, metalness: 0.35 }));
    this.cars = [];
    const lines = [...TRAFFIC.x.map(k => ['x', k]), ...TRAFFIC.z.map(k => ['z', k])];
    for (let i = 0; i < 16; i++) {
      const [axis, k] = lines[i % lines.length];
      const mesh = new THREE.Mesh(geos[i % geos.length], mat); mesh.castShadow = true; mesh.matrixAutoUpdate = false;
      this.group.add(mesh);
      this.cars.push({ axis, k, dir: Math.random() < 0.5 ? 1 : -1, s: rnd(-HALF + 20, HALF - 20), v: 0, cruise: rnd(6.5, 9.5), mesh, honk: 0, yaw: 0, x: 0, z: 0 });
    }
    this.carLimit = [lineC(0) + RW + 3, lineC(N) - RW - 3];
  }
  carPos(c) {
    if (c.axis === 'x') { c.x = c.s; c.z = lineC(c.k) + c.dir * 1.75; c.tyaw = c.dir > 0 ? Math.PI / 2 : -Math.PI / 2; }
    else { c.z = c.s; c.x = lineC(c.k) - c.dir * 1.75; c.tyaw = c.dir > 0 ? 0 : Math.PI; }
  }
  updateCars(dt, G) {
    const pl = G.player.worldPos(G, new THREE.Vector3()), T = G.truck;
    for (const c of this.cars) {
      this.carPos(c);
      // freia se tiver alguém na frente (jogador, carreta, outro carro)
      const fx = c.axis === 'x' ? c.dir : 0, fz = c.axis === 'z' ? c.dir : 0;
      let block = 99, byPlayer = false;
      const look = (x, z, r) => { const ahead = (x - c.x) * fx + (z - c.z) * fz, lat = Math.abs((x - c.x) * fz - (z - c.z) * fx); if (ahead > 0 && ahead < 12 && lat < r) block = Math.min(block, ahead); return ahead > 0 && ahead < 12 && lat < r; };
      if (G.player.frame === 'world' && G.player.mode !== 'drive' && look(pl.x, pl.z, 1.6)) byPlayer = true;
      look(T.x, T.z, 3.5);
      for (const o of this.cars) if (o !== c) look(o.x, o.z, 1.3);
      const target = block < 99 ? Math.max(0, (block - 5) * 1.2) : c.cruise;
      c.v += Math.max(-9 * dt, Math.min(3 * dt, Math.min(target, c.cruise) - c.v));
      if (byPlayer && c.v < 0.5) { c.honk += dt; if (c.honk > 1.2) { c.honk = -3; G.sfx('carhorn', c); } } else if (c.honk > 0) c.honk = 0;
      const prev = c.s; c.s += c.dir * c.v * dt;
      // cruzamentos: às vezes vira para outra rua com trânsito
      for (let j = 1; j < N; j++) {
        const cj = lineC(j);
        if ((prev - cj) * (c.s - cj) <= 0 && prev !== cj) {
          const other = c.axis === 'x' ? 'z' : 'x';
          if (TRAFFIC[other].includes(j) && Math.random() < 0.45) { const old = lineC(c.k); c.axis = other; c.k = j; c.s = old; c.dir = Math.random() < 0.5 ? 1 : -1; }
          break;
        }
      }
      if (c.s < this.carLimit[0]) { c.s = this.carLimit[0]; c.dir = 1; }
      if (c.s > this.carLimit[1]) { c.s = this.carLimit[1]; c.dir = -1; }
      this.carPos(c);
      let dy = c.tyaw - c.yaw; while (dy > Math.PI) dy -= 2 * Math.PI; while (dy < -Math.PI) dy += 2 * Math.PI; c.yaw += dy * Math.min(1, dt * 6);
      c.mesh.matrix.copy(M4(c.x, this.city.floorY(c.x, c.z), c.z, c.yaw)); c.mesh.matrixWorldNeedsUpdate = true;
    }
  }
  // empurra um personagem para fora dos carros em movimento
  pushActor(a) {
    if (a.frame !== 'world') return;
    for (const c of this.cars) {
      const fx = Math.sin(c.yaw), fz = Math.cos(c.yaw);
      const dx = a.pos.x - c.x, dz = a.pos.z - c.z;
      const along = dx * fx + dz * fz, lat = dx * fz - dz * fx;
      if (Math.abs(along) > 2.5 || Math.abs(lat) > 1.35 || a.pos.y > this.city.floorY(c.x, c.z) + 1.55) continue;
      const pushLat = (1.35 - Math.abs(lat)) * Math.sign(lat || 1), pushAlong = (2.5 - Math.abs(along)) * Math.sign(along || 1);
      if (Math.abs(pushLat) < Math.abs(pushAlong)) { a.pos.x += fz * pushLat; a.pos.z -= fx * pushLat; } else { a.pos.x += fx * pushAlong; a.pos.z += fz * pushAlong; }
    }
  }

  // ---------------- pedestres ----------------
  buildWalkers() {
    const blocks = this.city.blocks.filter(b => b.type === 'normal' || b.type === 'parque');
    const n = 48;
    const body = this.d(new THREE.CylinderGeometry(0.22, 0.26, 0.75, 8).translate(0, 1.0, 0));
    const head = this.d(new THREE.SphereGeometry(0.17, 10, 8).translate(0, 1.55, 0));
    const leg = this.d(new THREE.BoxGeometry(0.14, 0.62, 0.16).translate(0, -0.31, 0));
    const mk = (g) => { const m = new THREE.InstancedMesh(g, this.d(new THREE.MeshStandardMaterial({ roughness: 0.8 })), n); m.instanceMatrix.setUsage(THREE.DynamicDrawUsage); m.frustumCulled = false; m.castShadow = true; this.group.add(m); return m; };
    this.wb = mk(body); this.wh = mk(head); this.wl = mk(leg); this.wr = mk(leg);
    const shirts = [0xff3d7f, 0xffc400, 0x00c853, 0x2979ff, 0xffffff, 0xff6d00, 0x00bcd4, 0xe53935, 0x8e24aa, 0x6d4c41];
    const skins = [0xf1c27d, 0xe0ac69, 0xc68642, 0x8d5524, 0xffdbac, 0x6b3e26];
    const pants = [0x263238, 0x1a237e, 0x4e342e, 0x37474f, 0x5d4037];
    this.walkers = [];
    for (let i = 0; i < n; i++) {
      const b = blocks[i % blocks.length], o = 1.9;
      const per = [[b.x0 + o, b.z0 + o], [b.x1 - o, b.z0 + o], [b.x1 - o, b.z1 - o], [b.x0 + o, b.z1 - o]];
      const L = 2 * ((b.x1 - b.x0) + (b.z1 - b.z0) - 4 * o);
      this.walkers.push({ per, L, p: Math.random() * L, dir: Math.random() < 0.5 ? 1 : -1, sp: rnd(1.0, 1.6), ph: Math.random() * 6, sc: rnd(0.85, 1.12), cheer: 0 });
      this.wb.setColorAt(i, _c.set(shirts[i % shirts.length])); this.wh.setColorAt(i, _c.set(skins[i % skins.length]));
      const pc = pants[i % pants.length]; this.wl.setColorAt(i, _c.set(pc)); this.wr.setColorAt(i, _c.set(pc));
    }
  }
  walkerAt(w) {
    let d = ((w.p % w.L) + w.L) % w.L;
    for (let e = 0; e < 4; e++) {
      const a = w.per[e], b = w.per[(e + 1) % 4], len = Math.hypot(b[0] - a[0], b[1] - a[1]);
      if (d <= len) { const t = d / len; return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, Math.atan2(b[0] - a[0], b[1] - a[1])]; }
      d -= len;
    }
    return [w.per[0][0], w.per[0][1], 0];
  }
  updateWalkers(dt, G) {
    const T = G.truck, pl = G.player.worldPos(G, new THREE.Vector3()), t = G.now;
    const tilt = new THREE.Matrix4(), off = new THREE.Matrix4();
    this.walkers.forEach((w, i) => {
      let [x, z, yaw] = this.walkerAt(w);
      const nearTruck = Math.hypot(T.x - x, T.z - z) < 28, nearPl = Math.hypot(pl.x - x, pl.z - z) < 1.3;
      let move = !nearTruck && !nearPl;
      if (move) { w.p += w.dir * w.sp * dt; w.ph += dt * w.sp * 5.5; }
      if (w.dir < 0) yaw += Math.PI;
      let hop = 0;
      if (nearTruck) { yaw = Math.atan2(T.x - x, T.z - z); hop = Math.max(0, Math.sin(G.beat * Math.PI + i)) * 0.3; }
      const y = this.city.floorY(x, z) + hop, sc = w.sc;
      const base = M4(x, y, z, yaw, sc, sc, sc).clone();
      this.wb.setMatrixAt(i, base); this.wh.setMatrixAt(i, base);
      const sw = move ? Math.sin(w.ph) * 0.5 : 0;
      for (const [mesh, side, sgn] of [[this.wl, 0.11, 1], [this.wr, -0.11, -1]]) {
        off.makeTranslation(side, 0.66, 0); tilt.makeRotationX(sw * sgn);
        mesh.setMatrixAt(i, base.clone().multiply(off).multiply(tilt));
      }
    });
    for (const m of [this.wb, this.wh, this.wl, this.wr]) m.instanceMatrix.needsUpdate = true;
  }

  // ---------------- pombos ----------------
  buildPigeons() {
    const g = this.d(mergeGeometries([
      colored(new THREE.SphereGeometry(0.12, 8, 6).scale(1, 0.8, 1.5).translate(0, 0.12, 0), 0x8a8f99),
      colored(new THREE.SphereGeometry(0.07, 8, 6).translate(0, 0.22, 0.15), 0x6f7480),
      colored(new THREE.ConeGeometry(0.025, 0.06, 4).rotateX(Math.PI / 2).translate(0, 0.21, 0.24), 0xe0a040),
      colored(new THREE.BoxGeometry(0.42, 0.015, 0.14).translate(0, 0.16, 0), 0x7a7f8a),
    ]));
    const homes = [];
    for (const b of this.city.blocks) if (b.type === 'praca' || b.type === 'inicio' || b.type === 'parque') { const a = Math.random() * 6.28; homes.push([b.cx + Math.cos(a) * 14, b.cz + Math.sin(a) * 14]); }
    this.birds = [];
    for (const [hx, hz] of homes) for (let k = 0; k < 9; k++) {
      const x = hx + rnd(-2.5, 2.5), z = hz + rnd(-2.5, 2.5);
      this.birds.push({ home: [x, z], x, z, y: this.city.floorY(x, z), vx: 0, vy: 0, vz: 0, state: 'chao', t: Math.random() * 5, yaw: Math.random() * 6 });
    }
    this.birdMesh = new THREE.InstancedMesh(g, this.d(new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.8 })), Math.max(1, this.birds.length));
    this.birdMesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage); this.birdMesh.frustumCulled = false; this.group.add(this.birdMesh);
  }
  updatePigeons(dt, G) {
    const pl = G.player.worldPos(G, new THREE.Vector3()), T = G.truck;
    let flew = false;
    this.birds.forEach((b, i) => {
      b.t += dt;
      if (b.state === 'chao') {
        const scare = Math.hypot(pl.x - b.x, pl.z - b.z) < 5 || Math.hypot(T.x - b.x, T.z - b.z) < 14;
        if (scare) { b.state = 'voo'; b.t = 0; const a = Math.atan2(b.x - pl.x, b.z - pl.z) + rnd(-0.6, 0.6); b.vx = Math.sin(a) * rnd(4, 7); b.vz = Math.cos(a) * rnd(4, 7); b.vy = rnd(4, 7); b.yaw = a; flew = true; }
        else if (Math.random() < dt * 0.5) b.yaw += rnd(-1, 1);
      } else if (b.state === 'voo') {
        b.vy -= 2 * dt; b.vy = Math.max(b.vy, 1); b.x += b.vx * dt; b.y += b.vy * dt; b.z += b.vz * dt;
        if (b.t > 5) { b.state = 'volta'; b.t = 0; }
      } else { // planando de volta pra casa
        const dx = b.home[0] - b.x, dz = b.home[1] - b.z, gy = this.city.floorY(b.home[0], b.home[1]), dy = gy - b.y, d = Math.hypot(dx, dz);
        b.yaw = Math.atan2(dx, dz); const k = Math.min(1, dt * 0.6);
        b.x += dx * k; b.z += dz * k; b.y += dy * k;
        if (d < 0.3 && Math.abs(dy) < 0.2) { b.state = 'chao'; b.y = gy; }
      }
      const peck = b.state === 'chao' ? Math.max(0, Math.sin(b.t * 6 + i)) * 0.05 : 0;
      const flap = b.state !== 'chao' ? 1 + Math.sin(b.t * 30) * 0.6 : 1;
      M4(b.x, b.y - peck, b.z, b.yaw, flap, 1, 1);
      this.birdMesh.setMatrixAt(i, _m);
    });
    this.birdMesh.instanceMatrix.needsUpdate = true;
    if (flew) G.sfx('flap');
  }

  // ---------------- bolas ----------------
  buildBalls() {
    const c = document.createElement('canvas'); c.width = 128; c.height = 64; const g = c.getContext('2d');
    g.fillStyle = '#fafafa'; g.fillRect(0, 0, 128, 64); g.fillStyle = '#111';
    for (let y = 8; y < 64; y += 24) for (let x = (y / 24 % 2) * 16 + 8; x < 128; x += 32) { g.beginPath(); for (let k = 0; k < 5; k++) { const a = k / 5 * Math.PI * 2; g.lineTo(x + Math.cos(a) * 7, y + Math.sin(a) * 7); } g.fill(); }
    const tex = this.d(new THREE.CanvasTexture(c)); tex.colorSpace = THREE.SRGBColorSpace;
    const geo = this.d(new THREE.SphereGeometry(0.22, 16, 12)), mat = this.d(new THREE.MeshStandardMaterial({ map: tex, roughness: 0.5 }));
    this.balls = [];
    for (const b of this.city.blocks) if (['parque', 'parkour', 'praca', 'inicio'].includes(b.type)) for (let k = 0; k < 2; k++) {
      const x = b.cx + rnd(-18, 18), z = b.cz + (b.type === 'praca' || b.type === 'inicio' ? rnd(-28, -14) * (k ? 1 : -1) : rnd(-18, 18));
      const m = new THREE.Mesh(geo, mat); m.castShadow = true; this.group.add(m);
      this.balls.push({ p: new THREE.Vector3(x, this.city.floorY(x, z) + 0.22, z), v: new THREE.Vector3(), m });
    }
  }
  updateBalls(dt, G) {
    for (const b of this.balls) {
      // chute: qualquer personagem que encosta
      for (const a of G.actors) {
        if (a.frame !== 'world' || a.mode === 'drive') continue;
        const dx = b.p.x - a.pos.x, dz = b.p.z - a.pos.z, d = Math.hypot(dx, dz);
        if (d < 0.55 && Math.abs(b.p.y - a.pos.y - 0.2) < 0.6) {
          const sp = Math.hypot(a.vel.x, a.vel.z), nx = dx / (d || 1), nz = dz / (d || 1);
          b.v.set(nx * (3 + sp * 1.4), 2 + sp * 0.5, nz * (3 + sp * 1.4));
          b.p.x = a.pos.x + nx * 0.56; b.p.z = a.pos.z + nz * 0.56;
          if (a === G.player) G.sfx('kick', b);
        }
      }
      b.v.y -= 18 * dt; b.p.addScaledVector(b.v, dt);
      const gy = this.city.groundAt(b.p.x, b.p.z, b.p.y).h + 0.22;
      if (b.p.y < gy) { b.p.y = gy; if (b.v.y < -1.5) b.v.y *= -0.55; else b.v.y = 0; b.v.x *= 1 - Math.min(1, dt * 1.2); b.v.z *= 1 - Math.min(1, dt * 1.2); }
      const before = b.p.clone(), hit = this.city.collide(b.p, 0.22, b.p.y - 0.22);
      if (hit) { if (hit.nx) b.v.x = Math.abs(b.v.x) * Math.sign(hit.nx) * 0.7; if (hit.nz) b.v.z = Math.abs(b.v.z) * Math.sign(hit.nz) * 0.7; }
      b.m.position.copy(b.p);
      const sp = Math.hypot(b.v.x, b.v.z); if (sp > 0.05) b.m.rotateOnWorldAxis(new THREE.Vector3(b.v.z, 0, -b.v.x).normalize(), sp * dt / 0.22);
    }
  }

  // ---------------- chafariz ----------------
  buildFountain() {
    const b = this.city.blocks.find(x => x.type === 'parque'); if (!b) return;
    const y = this.city.floorY(b.cx, b.cz), grp = new THREE.Group(); grp.position.set(b.cx, y, b.cz); this.group.add(grp);
    const stone = this.d(new THREE.MeshStandardMaterial({ color: 0xd7ccc8, roughness: 0.8 }));
    const basin = new THREE.Mesh(this.d(new THREE.CylinderGeometry(4, 4.3, 0.7, 28, 1, true).translate(0, 0.35, 0)), stone); basin.material.side = THREE.DoubleSide;
    const rim = new THREE.Mesh(this.d(new THREE.TorusGeometry(4.05, 0.18, 6, 28).rotateX(Math.PI / 2).translate(0, 0.7, 0)), stone);
    const water = new THREE.Mesh(this.d(new THREE.CircleGeometry(3.95, 28).rotateX(-Math.PI / 2).translate(0, 0.55, 0)), this.d(new THREE.MeshStandardMaterial({ color: 0x4fc3f7, roughness: 0.1, metalness: 0.2, transparent: true, opacity: 0.85 })));
    const pillar = new THREE.Mesh(this.d(new THREE.CylinderGeometry(0.35, 0.6, 1.8, 12).translate(0, 0.9, 0)), stone);
    const bowl = new THREE.Mesh(this.d(new THREE.CylinderGeometry(1.3, 0.5, 0.35, 16).translate(0, 1.9, 0)), stone);
    grp.add(basin, rim, water, pillar, bowl);
    this.city.addCollider({ x0: b.cx - 4.2, x1: b.cx + 4.2, z0: b.cz - 4.2, z1: b.cz + 4.2, top: y + 0.8, walk: true, kind: 'chafariz' });
    const n = 360, pos = new Float32Array(n * 3);
    this.drops = { n, pos, seed: Array.from({ length: n }, () => [Math.random() * 6.28, rnd(2.2, 3.4), Math.random() * 1.2]), origin: new THREE.Vector3(b.cx, y + 2.1, b.cz) };
    const g = this.d(new THREE.BufferGeometry()); g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    this.dropPts = new THREE.Points(g, this.d(new THREE.PointsMaterial({ color: 0xb3e5fc, size: 0.09, transparent: true, opacity: 0.85 })));
    this.dropPts.frustumCulled = false; this.group.add(this.dropPts);
  }
  updateFountain(t) {
    if (!this.drops) return;
    const { n, pos, seed, origin } = this.drops;
    for (let i = 0; i < n; i++) {
      const [a, v, o] = seed[i], tt = (t + o) % 1.2;
      const r = Math.cos(0.9) * v * tt * 0.9;
      pos[i * 3] = origin.x + Math.cos(a) * r; pos[i * 3 + 1] = origin.y + Math.sin(0.9) * v * tt * 1.6 - 4.9 * tt * tt; pos[i * 3 + 2] = origin.z + Math.sin(a) * r;
    }
    this.dropPts.geometry.attributes.position.needsUpdate = true;
  }

  // ---------------- fogos e confete ----------------
  particles(origin, n, colorFn, velFn, life, size, additive, gravity = 6) {
    const pos = new Float32Array(n * 3), col = new Float32Array(n * 3), vel = [];
    for (let i = 0; i < n; i++) { pos.set([origin.x, origin.y, origin.z], i * 3); const c = colorFn(i); col.set([c.r, c.g, c.b], i * 3); vel.push(velFn(i)); }
    const g = new THREE.BufferGeometry(); g.setAttribute('position', new THREE.BufferAttribute(pos, 3)); g.setAttribute('color', new THREE.BufferAttribute(col, 3));
    const m = new THREE.PointsMaterial({ size, vertexColors: true, transparent: true, depthWrite: false, blending: additive ? THREE.AdditiveBlending : THREE.NormalBlending, toneMapped: !additive, fog: false });
    const pts = new THREE.Points(g, m); pts.frustumCulled = false; this.group.add(pts);
    this.fx.push({ pts, vel, t: 0, life, gravity, drag: additive ? 0.6 : 2.2 });
  }
  firework(x, y, z, G) {
    const hue = Math.random(), ring = Math.random() < 0.4;
    this.particles(new THREE.Vector3(x, y, z), 110, () => _c.setHSL((hue + rnd(-0.06, 0.06)) % 1, 1, 0.6).clone().multiplyScalar(2),
      () => { const a = Math.random() * 6.28, e = ring ? 0 : Math.acos(rnd(-1, 1)) - Math.PI / 2, s = rnd(9, 14); return new THREE.Vector3(Math.cos(a) * Math.cos(e) * s, Math.sin(e) * s + 2, Math.sin(a) * Math.cos(e) * s); }, 1.8, 0.7, true, 5);
    G.sfx('boom');
  }
  rocket(x, z, G) {
    const y0 = this.city.floorY(x, z), h = rnd(38, 60);
    this.rockets = this.rockets || [];
    this.rockets.push({ x, z, y: y0, top: y0 + h, t: 0 });
    this.particles(new THREE.Vector3(x, y0, z), 1, () => new THREE.Color(1.5, 1.2, 0.6), () => new THREE.Vector3(0, h / 1.1, 0), 1.1, 0.9, true, 0);
  }
  confetti(pos) {
    const cols = [0xff3d7f, 0xffc400, 0x00c853, 0x2979ff, 0xaa00ff, 0xffffff];
    this.particles(pos, 220, (i) => new THREE.Color(cols[i % cols.length]), () => new THREE.Vector3(rnd(-6, 6), rnd(8, 15), rnd(-6, 6)), 3.2, 0.22, false, 5);
  }
  updateFx(dt, G) {
    for (let i = this.fx.length - 1; i >= 0; i--) {
      const f = this.fx[i]; f.t += dt; const a = f.pts.geometry.attributes.position;
      for (let k = 0; k < f.vel.length; k++) {
        const v = f.vel[k]; v.y -= f.gravity * dt; v.multiplyScalar(1 - Math.min(1, dt * f.drag));
        a.setXYZ(k, a.getX(k) + v.x * dt + (f.gravity === 5 && !f.pts.material.blending ? Math.sin(f.t * 6 + k) * 0.02 : 0), a.getY(k) + v.y * dt, a.getZ(k) + v.z * dt);
      }
      a.needsUpdate = true; f.pts.material.opacity = Math.max(0, 1 - f.t / f.life);
      if (f.t > f.life) { this.group.remove(f.pts); f.pts.geometry.dispose(); f.pts.material.dispose(); this.fx.splice(i, 1); }
    }
    if (this.rockets) for (let i = this.rockets.length - 1; i >= 0; i--) { const r = this.rockets[i]; r.t += dt; if (r.t > 1.1) { this.firework(r.x, r.top, r.z, G); this.rockets.splice(i, 1); } }
    // à noite: fogos nas festas, no fim e de vez em quando perto da carreta
    if (this.night) {
      this.fireT -= dt;
      const party = G.party || G.phase === 'fim';
      if (this.fireT <= 0) {
        const c = party ? (G.party ? { x: G.party.cx, z: G.party.cz } : { x: G.truck.x, z: G.truck.z }) : { x: G.truck.x, z: G.truck.z };
        this.rocket(c.x + rnd(-40, 40), c.z + rnd(-40, 40), G);
        this.fireT = party ? rnd(0.5, 1.2) : rnd(12, 25);
      }
    }
  }

  update(dt, G) {
    this.updateCars(dt, G); this.updateWalkers(dt, G); this.updatePigeons(dt, G); this.updateBalls(dt, G); this.updateFountain(G.now); this.updateFx(dt, G);
  }
  dispose() { this.scene.remove(this.group); this.group.traverse(o => { if (o.isInstancedMesh) o.dispose(); }); for (const x of this.disp) x.dispose && x.dispose(); }
}
