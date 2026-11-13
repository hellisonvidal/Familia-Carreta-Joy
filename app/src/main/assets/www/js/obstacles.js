import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { rng, pick, range } from './cities.js';
import { LANES } from './track.js';

// Tipos: len = comprimento na rua, w = largura, h = altura
const TYPES = {
  cone:    { len: 0.55, w: 0.6, h: 0.75, walk: false, knock: true },
  barreira:{ len: 0.45, w: 2.2, h: 1.0, walk: false, knock: true },
  banco:   { len: 2.4, w: 0.9, h: 0.42, walk: true },
  carro:   { len: 4.2, w: 1.9, h: 1.45, walk: true },
  rampa:   { len: 4.6, w: 2.3, h: 1.1, walk: true, ramp: true },
  caixas:  { len: 1.6, w: 2.2, h: 2.6, walk: false },
};

function colored(geo, color) {
  const g = geo.index ? geo.toNonIndexed() : geo;
  const c = new THREE.Color(color), n = g.attributes.position.count, a = new Float32Array(n * 3);
  for (let i = 0; i < n; i++) { a[i * 3] = c.r; a[i * 3 + 1] = c.g; a[i * 3 + 2] = c.b; }
  g.setAttribute('color', new THREE.BufferAttribute(a, 3));
  g.deleteAttribute('uv');
  return g;
}

function protos() {
  const P = {};
  P.cone = mergeGeometries([
    colored(new THREE.BoxGeometry(0.6, 0.06, 0.6).translate(0, 0.03, 0), 0x222222),
    colored(new THREE.ConeGeometry(0.26, 0.7, 12).translate(0, 0.41, 0), 0xff6d00),
    colored(new THREE.CylinderGeometry(0.16, 0.19, 0.12, 12).translate(0, 0.45, 0), 0xffffff),
  ]);
  const bar = [];
  for (let k = 0; k < 6; k++) bar.push(colored(new THREE.BoxGeometry(0.37, 0.32, 0.1).translate(-0.925 + k * 0.37, 0.8, 0), k % 2 ? 0xffffff : 0xe53935));
  bar.push(colored(new THREE.BoxGeometry(0.1, 1.0, 0.1).translate(-1.0, 0.5, 0), 0x555555), colored(new THREE.BoxGeometry(0.1, 1.0, 0.1).translate(1.0, 0.5, 0), 0x555555));
  bar.push(colored(new THREE.BoxGeometry(0.5, 0.06, 0.4).translate(-1.0, 0.03, 0), 0x333333), colored(new THREE.BoxGeometry(0.5, 0.06, 0.4).translate(1.0, 0.03, 0), 0x333333));
  P.barreira = mergeGeometries(bar);
  P.banco = mergeGeometries([
    colored(new THREE.BoxGeometry(0.8, 0.08, 2.4).translate(0, 0.38, 0), 0x8d5a2b),
    colored(new THREE.BoxGeometry(0.7, 0.36, 0.12).translate(0, 0.18, -1.0), 0x444444),
    colored(new THREE.BoxGeometry(0.7, 0.36, 0.12).translate(0, 0.18, 1.0), 0x444444),
    colored(new THREE.BoxGeometry(0.08, 0.5, 2.4).translate(-0.4, 0.65, 0), 0x8d5a2b),
  ]);
  P.carros = [0xe53935, 0x1e88e5, 0xfdd835, 0x43a047, 0xffffff, 0x8e24aa, 0x212121].map(col => {
    const parts = [
      colored(new THREE.BoxGeometry(1.9, 0.75, 4.2).translate(0, 0.65, 0), col),
      colored(new THREE.BoxGeometry(1.7, 0.45, 2.3).translate(0, 1.22, -0.2), col),
      colored(new THREE.BoxGeometry(1.72, 0.36, 2.0).translate(0, 1.2, -0.2), 0x1c2833),
      colored(new THREE.BoxGeometry(1.6, 0.06, 2.2).translate(0, 1.45, -0.2), col),
      colored(new THREE.BoxGeometry(1.5, 0.12, 0.05).translate(0, 0.75, 2.11), 0xfff5cc),
      colored(new THREE.BoxGeometry(1.5, 0.12, 0.05).translate(0, 0.75, -2.11), 0xc62828),
    ];
    for (const [x, z] of [[-0.9, 1.35], [0.9, 1.35], [-0.9, -1.35], [0.9, -1.35]])
      parts.push(colored(new THREE.CylinderGeometry(0.34, 0.34, 0.26, 12).rotateZ(Math.PI / 2).translate(x, 0.34, z), 0x111111));
    return mergeGeometries(parts);
  });
  // rampa (cunha)
  const w = 1.15, L = 2.3, H = 1.1;
  const pos = [
    -w, 0, -L, w, 0, -L, w, H, L, -w, 0, -L, w, H, L, -w, H, L, // rampa
    -w, 0, L, w, 0, L, w, H, L, -w, 0, L, w, H, L, -w, H, L,     // fundo (vertical)
    -w, 0, -L, -w, H, L, -w, 0, L, w, 0, -L, w, 0, L, w, H, L,   // laterais
  ];
  const rg = new THREE.BufferGeometry(); rg.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3)); rg.computeVertexNormals();
  const rc = new Float32Array(pos.length); const c1 = new THREE.Color(0xffc400), c2 = new THREE.Color(0x222222);
  for (let i = 0; i < pos.length / 3; i++) { const c = i < 6 ? (Math.floor((pos[i * 3 + 2] + L) * 2) % 2 ? c1 : c1) : c2; rc.set([c.r, c.g, c.b], i * 3); }
  rg.setAttribute('color', new THREE.BufferAttribute(rc, 3));
  const stripes = [];
  for (let k = 0; k < 4; k++) {
    const z0 = -L + 0.4 + k * 1.1, t = (z0 + L) / (2 * L);
    stripes.push(colored(new THREE.BoxGeometry(2.32, 0.03, 0.35).rotateX(-Math.atan2(H, 2 * L)).translate(0, H * t + 0.03, z0), 0x111111));
  }
  P.rampa = mergeGeometries([rg, ...stripes]);
  const boxes = [];
  for (const [x, y, z, s] of [[-0.55, 0.5, 0, 1], [0.55, 0.5, 0.05, 1], [0, 1.5, 0, 1], [0.1, 2.25, -0.1, 0.55]]) {
    boxes.push(colored(new THREE.BoxGeometry(s, s, s).translate(x, y - (1 - s) / 2, z), 0xa1662f));
    boxes.push(colored(new THREE.BoxGeometry(s * 1.02, s * 0.12, s * 1.02).translate(x, y - (1 - s) / 2, z), 0xd7a35a));
  }
  P.caixas = mergeGeometries(boxes);
  return P;
}

let PROTOS = null;

export class Obstacles {
  constructor(scene, track, city) {
    this.track = track; this.scene = scene;
    this.group = new THREE.Group(); scene.add(this.group);
    PROTOS = PROTOS || protos();
    this.mat = new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 0.6, metalness: 0.1 });
    this.list = []; this.notes = []; this.flying = [];
    this.generate(rng(city.seed * 13 + 5));
    this.buildNotes();
  }

  add(type, s, laneIdx, variant) {
    const T = TYPES[type];
    const o = { type, s, lat: LANES[laneIdx], lane: laneIdx, ...T, hitBy: null, knocked: false };
    const geo = type === 'carro' ? pick(this.r, PROTOS.carros) : PROTOS[type];
    const mesh = new THREE.Mesh(geo, this.mat);
    const f = this.track.frame(s, {});
    mesh.position.set(f.x + f.rx * o.lat, f.y, f.z + f.rz * o.lat);
    mesh.rotation.y = f.heading;
    // inclina com a ladeira
    mesh.rotateX(-Math.atan(f.grade));
    this.group.add(mesh);
    o.mesh = mesh;
    this.list.push(o);
    return o;
  }

  note(s, laneIdx, y, gold = false) { this.notes.push({ s, lat: LANES[laneIdx], y, gold, taken: false }); }

  generate(r) {
    this.r = r;
    const T = this.track, L = T.length, P = T.praca;
    const lanes = [0, 1, 2];
    let s = 110;
    const shuffle = (a) => a.map(v => [r(), v]).sort((x, y) => x[0] - y[0]).map(v => v[1]);
    while (s < L - 90) {
      if (Math.abs(s - P) < 75) { s += 10; continue; }
      const pat = Math.floor(r() * 9);
      const [a, b, c] = shuffle(lanes);
      const notesLine = (lane, s0, n = 5, y = 0.9) => { for (let k = 0; k < n; k++) this.note(s0 + k * 2.6, lane, y); };
      let span = 6;
      switch (pat) {
        case 0: this.add('cone', s, a); notesLine(b, s - 5); break;
        case 1: this.add('cone', s, a); this.add('cone', s, b); notesLine(c, s - 5); for (let k = -2; k <= 2; k++) this.note(s + k * 1.6, a, 1.9 - Math.abs(k) * 0.3); break;
        case 2: this.add('barreira', s, a); for (let k = -2; k <= 2; k++) this.note(s + k * 1.8, a, 2.0 - Math.abs(k) * 0.35); break;
        case 3: this.add('carro', s, a); for (let k = -2; k <= 3; k++) this.note(s + k * 1.6, a, 2.6); notesLine(b, s - 6, 4); span = 10; break;
        case 4: this.add('banco', s, a); this.add('banco', s + 2.6, a); notesLine(a, s - 1, 4, 1.4); break;
        case 5: { // rampa voadora com estrela dourada
          this.add('rampa', s, a);
          const end = s + 2.3;
          for (let k = 1; k <= 9; k++) { const t = k * 1.3 / 11; const y = 1.1 + 13.5 * t - 12 * t * t + 0.6; this.note(end + k * 1.3, a, y, k === 5); }
          if (r() < 0.6) this.add('caixas', end + 7, a);
          span = 18; break;
        }
        case 6: this.add('caixas', s, a); if (r() < 0.5) this.add('cone', s, b); notesLine(c, s - 4, 4); break;
        case 7: { // parkour: carro, carro
          this.add('carro', s, a); this.add('carro', s + 6.5, a);
          for (let k = -2; k <= 7; k++) this.note(s + k * 1.5, a, 2.6 + (k > 1 && k < 4 ? 0.8 : 0));
          span = 14; break;
        }
        case 8: this.add('barreira', s, a); this.add('banco', s + 9, b); this.add('cone', s + 9, c); notesLine(b, s + 7.5, 3, 1.4); span = 12; break;
      }
      s += span + range(r, 20, 34);
    }
    this.list.sort((x, y) => x.s - y.s);
  }

  buildNotes() {
    // nota musical estilizada
    const g = mergeGeometries([
      new THREE.SphereGeometry(0.2, 10, 8).scale(1.25, 0.9, 0.7).translate(0, 0, 0),
      new THREE.CylinderGeometry(0.035, 0.035, 0.6, 6).translate(0.22, 0.3, 0),
      new THREE.BoxGeometry(0.22, 0.08, 0.05).rotateZ(-0.5).translate(0.32, 0.53, 0),
    ]);
    const star = new THREE.Shape();
    for (let i = 0; i < 10; i++) { const a = i / 10 * Math.PI * 2 + Math.PI / 2, R = i % 2 ? 0.25 : 0.6; i ? star.lineTo(Math.cos(a) * R, Math.sin(a) * R) : star.moveTo(Math.cos(a) * R, Math.sin(a) * R); }
    const sg = new THREE.ExtrudeGeometry(star, { depth: 0.15, bevelEnabled: false }).translate(0, 0, -0.075);
    const nn = this.notes.filter(n => !n.gold), gg = this.notes.filter(n => n.gold);
    this.noteMesh = new THREE.InstancedMesh(g, new THREE.MeshStandardMaterial({ color: 0xffd54f, emissive: 0xffa000, emissiveIntensity: 0.9, metalness: 0.3, roughness: 0.3 }), Math.max(1, nn.length));
    this.starMesh = new THREE.InstancedMesh(sg, new THREE.MeshStandardMaterial({ color: 0xfff176, emissive: 0xffeb3b, emissiveIntensity: 1.4, metalness: 0.4, roughness: 0.2 }), Math.max(1, gg.length));
    nn.forEach((n, i) => { n.mesh = this.noteMesh; n.i = i; });
    gg.forEach((n, i) => { n.mesh = this.starMesh; n.i = i; });
    for (const n of this.notes) { const p = this.track.pos(n.s, n.lat, n.y); n.p = p; n.h = this.track.frame(n.s, {}).heading; }
    this.group.add(this.noteMesh, this.starMesh);
    this.noteMesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage); this.starMesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    this.updateNotes(0, 0);
  }

  // ---------- física ----------
  overlap(o, s, lat, pad = 0.25) {
    return Math.abs(lat - o.lat) < o.w / 2 + pad && s > o.s - o.len / 2 - 0.2 && s < o.s + o.len / 2 + 0.2;
  }
  topAt(o, s) {
    if (o.ramp) return o.h * Math.max(0, Math.min(1, (s - (o.s - o.len / 2)) / o.len));
    return o.h;
  }
  groundAt(s, lat, y) {
    let h = 0, top = null;
    for (const o of this.list) {
      if (o.s - s > 6) break;
      if (s - o.s > 6 || !o.walk || o.knocked) continue;
      if (!this.overlap(o, s, lat, 0.1)) continue;
      const t = this.topAt(o, s);
      if (y >= t - 0.45 && t > h) { h = t; top = o; }
    }
    return { h, top };
  }
  rampLaunch(s, lat, kid) {
    const last = kid._lastS ?? s; kid._lastS = s;
    for (const o of this.list) {
      if (!o.ramp) continue;
      const end = o.s + o.len / 2;
      if (last < end && s >= end && Math.abs(lat - o.lat) < o.w / 2 + 0.1 && kid.y > o.h * 0.6) return true;
    }
    return false;
  }
  collide(s, lat, y, kid) {
    for (const o of this.list) {
      if (o.s - s > 4) break;
      if (s - o.s > 4 || o.knocked) continue;
      if (!this.overlap(o, s, lat, 0.22)) continue;
      const t = this.topAt(o, s);
      if (o.ramp) continue;
      if (o.walk && y >= t - 0.45) continue;
      if (y >= t - 0.05) continue;
      if (o.knock) this.knock(o, kid);
      return o;
    }
    return null;
  }
  knock(o, kid) {
    o.knocked = true;
    o.vel = new THREE.Vector3((Math.random() - 0.5) * 6, 6 + Math.random() * 3, 0);
    const f = this.track.frame(o.s, {});
    o.vel.x += f.fx * 9; o.vel.z += f.fz * 9;
    o.spin = new THREE.Vector3(Math.random() * 8, Math.random() * 8, Math.random() * 8);
    this.flying.push(o);
  }
  collect(s, lat, yc, kid, G) {
    for (const n of this.notes) {
      if (n.taken) continue;
      if (Math.abs(n.s - s) > 1.1 || Math.abs(n.lat - lat) > 1.15 || Math.abs(n.y - yc) > 1.35) continue;
      n.taken = true; n.takenT = 0;
      G.onEvent(n.gold ? 'estrelaDourada' : 'nota', kid, n);
    }
  }
  // para a IA: próximo obstáculo à frente numa faixa
  ahead(s, lane, maxDist) {
    let best = null;
    for (const o of this.list) {
      if (o.s + o.len / 2 < s - 0.5) continue;
      if (o.s - s > maxDist) break;
      if (o.lane !== lane || o.knocked) continue;
      best = o; break;
    }
    return best;
  }

  updateNotes(t, dt) {
    const m = new THREE.Matrix4(), q = new THREE.Quaternion(), sc = new THREE.Vector3(), up = new THREE.Vector3(0, 1, 0);
    for (const n of this.notes) {
      let k = 1, y = n.p.y + Math.sin(t * 3 + n.s) * 0.08;
      if (n.taken) { n.takenT += dt; k = Math.max(0, 1 - n.takenT * 4); y += n.takenT * 4; }
      q.setFromAxisAngle(up, t * 3 + n.s * 0.3);
      sc.setScalar(k * (n.gold ? 1.1 : 1));
      m.compose(new THREE.Vector3(n.p.x, y, n.p.z), q, sc);
      n.mesh.setMatrixAt(n.i, m);
    }
    this.noteMesh.instanceMatrix.needsUpdate = true; this.starMesh.instanceMatrix.needsUpdate = true;
  }

  update(dt, t) {
    this.updateNotes(t, dt);
    for (let i = this.flying.length - 1; i >= 0; i--) {
      const o = this.flying[i];
      o.vel.y -= 20 * dt;
      o.mesh.position.addScaledVector(o.vel, dt);
      o.mesh.rotation.x += o.spin.x * dt; o.mesh.rotation.z += o.spin.z * dt;
      if (o.mesh.position.y < -30) { o.mesh.visible = false; this.flying.splice(i, 1); }
    }
  }

  dispose() {
    this.scene.remove(this.group);
    this.mat.dispose(); this.noteMesh.dispose(); this.starMesh.dispose();
  }
}
