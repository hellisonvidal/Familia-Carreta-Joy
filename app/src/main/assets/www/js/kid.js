import * as THREE from 'three';
import { LANES, TRUCK_LAT } from './track.js';
import * as TX from './textures.js';

// Os modelos do Meshy são estáticos (sem esqueleto), então todas as animações
// são procedurais: o corpo inteiro gira, pula, balança e estica.

export const TRICKS = {
  mortal:  { nome: 'MORTAL',  dur: 0.62, pts: 100 },
  flip:    { nome: 'FLIP',    dur: 0.72, pts: 150 },
  estrela: { nome: 'ESTRELA', dur: 0.6,  pts: 80 },
  pirueta: { nome: 'PIRUETA', dur: 0.5,  pts: 60 },
};

const GRAV = 24, JUMP_V = 9.2, LANE_SPEED = 13;
const easeInOut = (t) => t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2;
const clamp01 = (t) => Math.max(0, Math.min(1, t));

let shadowTex = null;

export class Kid {
  constructor(name, gltfScene, height, roofSpot) {
    this.name = name;
    this.root = new THREE.Group();
    this.pivot = new THREE.Group();     // giros de manobras acontecem aqui (no centro do corpo)
    this.squash = new THREE.Group();    // achatar/esticar
    const model = gltfScene.clone(true);
    const box = new THREE.Box3().setFromObject(model);
    const size = box.getSize(new THREE.Vector3());
    const s = height / size.y;
    model.scale.setScalar(s);
    const center = box.getCenter(new THREE.Vector3()).multiplyScalar(s);
    this.h = height;
    model.position.set(-center.x, -center.y, -center.z);
    model.traverse(o => { if (o.isMesh) o.frustumCulled = false; });
    this.pivot.position.y = height / 2;
    this.squash.add(model); this.pivot.add(this.squash); this.root.add(this.pivot);
    // sombrinha
    shadowTex = shadowTex || TX.blobShadow();
    this.shadow = new THREE.Mesh(new THREE.PlaneGeometry(1.1, 1.1).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ map: shadowTex, transparent: true, depthWrite: false }));
    this.shadow.renderOrder = 1;

    this.roofSpot = roofSpot.clone();
    this.reset();
  }

  reset() {
    this.mode = 'roof';      // roof | up | down | street
    this.ds = 0; this.lat = LANES[0]; this.laneIdx = 0; this.y = 0; this.vy = 0;
    this.ground = 0; this.onGround = true;
    this.trick = null; this.queue = null; this.airTricks = [];
    this.stumble = 0; this.invul = 0; this.trans = 0;
    this.dance = null; this.danceT = 0;
    this.runPhase = Math.random() * 6;
    this.facing = 0; this.lastDir = 1;
    this.local = this.roofSpot.clone();
    this.airTime = 0;
    this.onCar = false;
    this.s = 0;
  }

  get airborne() { return this.mode === 'street' && !this.onGround; }

  // ---------------- comandos ----------------
  jump(boost = 1) {
    if (this.mode !== 'street' || !this.onGround || this.stumble > 0) return false;
    this.vy = JUMP_V * boost; this.onGround = false; this.airTricks = []; this.airTime = 0;
    return true;
  }
  move(dir) {
    if (this.mode !== 'street' || this.stumble > 0) return;
    this.lastDir = dir;
    this.laneIdx = Math.max(0, Math.min(LANES.length - 1, this.laneIdx + dir));
  }
  startTrick(type) {
    if (this.mode !== 'street' || this.stumble > 0) return false;
    if (this.trick) {
      if (this.trick.t / this.trick.dur > 0.6) { this.queue = type; return true; }
      return false;
    }
    if (this.onGround) {
      if (type === 'estrela') {
        // estrela no chão: rodinha de lado mudando de faixa
        let dir = this.lastDir;
        if (this.laneIdx + dir < 0 || this.laneIdx + dir >= LANES.length) dir = -dir;
        this.laneIdx += dir; this.cartDir = dir;
        this.trick = { type, t: 0, dur: 0.6, ground: true, dir };
        return true;
      }
      // as outras manobras precisam de impulso: pula automaticamente
      this.jump();
    }
    this.trick = { type, t: 0, dur: TRICKS[type].dur, ground: false, dir: this.lastDir };
    return true;
  }

  // ---------------- dança (no teto da carreta) ----------------
  danceMove(type) { this.dance = { type, t: 0, dur: type === 'mortal' ? 0.7 : 0.45 }; }

  // ---------------- atualização ----------------
  // world = { track, truck, obstacles, onEvent(fn) }
  update(dt, G) {
    this.invul = Math.max(0, this.invul - dt);
    if (this.mode === 'street') this.updateStreet(dt, G);
    else if (this.mode === 'up' || this.mode === 'down') this.updateTransition(dt, G);
    else this.updateRoof(dt, G);
  }

  updateRoof(dt, G) {
    const truck = G.truck;
    this.local.lerp(this.roofSpot, clamp01(dt * 5));
    truck.toWorld(this.local, this.root.position);
    this.root.quaternion.setFromRotationMatrix(truck.root.matrixWorld);
    // dança no ritmo
    const beat = G.beat;
    const ph = beat % 1;
    const bounce = Math.abs(Math.sin(ph * Math.PI));
    let px = 0, py = 0, pz = 0, ry = -Math.PI / 2 + Math.sin(beat * Math.PI * 0.5) * 0.35, rz = Math.sin(beat * Math.PI) * 0.1, rx = 0;
    let lift = bounce * 0.12, sq = 1 - bounce * 0.05, side = 0;
    if (this.dance) {
      const d = this.dance; d.t += dt; const k = clamp01(d.t / d.dur);
      const arc = Math.sin(k * Math.PI);
      switch (d.type) {
        case 'esq': side = Math.sin(k * Math.PI * 2) * 0.6; rz += arc * 0.35; lift += arc * 0.25; break;
        case 'dir': px = k * Math.PI * 2; lift += arc * 0.5; break; // estrela no lugar
        case 'cima': lift += arc * 1.3; ry += easeInOut(k) * Math.PI * 2; break; // pulo com giro
        case 'baixo': sq = 1 - arc * 0.32; rz += Math.sin(k * Math.PI * 4) * 0.35; break; // rebolado
        case 'mortal': lift += arc * 1.8; rx = -easeInOut(k) * Math.PI * 2; break;
      }
      if (d.t >= d.dur) this.dance = null;
    }
    this.root.translateY(lift);
    this.root.translateX(side);
    this.pivot.rotation.set(rx, ry, rz + px, 'YXZ');
    if (this.dance && this.dance.type === 'dir') { this.pivot.rotation.set(0, ry, px); }
    this.squash.scale.set(1 + (1 - sq) * 0.5, sq, 1 + (1 - sq) * 0.5);
    this.shadow.visible = false;
  }

  // sobe/desce a lateral da carreta
  beginClimb(G) {
    if (this.mode !== 'street' || !this.onGround) return false;
    this.mode = 'up'; this.trans = 0;
    // posição atual em coordenadas da carreta
    const z = Math.max(G.truck.roofZ[0] + 1, Math.min(G.truck.roofZ[1] - 1, this.ds));
    this.tFrom = new THREE.Vector3(-(this.lat - TRUCK_LAT), 0, this.ds);
    this.tSide = new THREE.Vector3(-(G.truck.halfW + 0.35), 0, z);
    this.trick = null; this.squash.scale.set(1, 1, 1);
    return true;
  }
  beginDescend(G) {
    if (this.mode !== 'roof') return false;
    this.mode = 'down'; this.trans = 0; this.dance = null;
    this.laneIdx = 0;
    this.tFrom = this.local.clone();
    this.tTo = new THREE.Vector3(-(LANES[0] - TRUCK_LAT), 0, this.local.z + 1);
    return true;
  }

  updateTransition(dt, G) {
    const truck = G.truck;
    this.trans += dt;
    const p = this.local;
    let rx = 0, ry = 0, sc = 1;
    if (this.mode === 'up') {
      const T1 = 0.25, T2 = 1.0, T3 = 1.3, t = this.trans;
      const topSide = this.tSide.clone(); topSide.y = truck.roofY;
      if (t < T1) { p.lerpVectors(this.tFrom, this.tSide, easeInOut(t / T1)); }
      else if (t < T2) {
        const k = (t - T1) / (T2 - T1);
        p.lerpVectors(this.tSide, topSide, k);
        p.x += Math.sin(k * Math.PI * 8) * 0.08; // escalando (balança de um lado pro outro)
        ry = Math.PI / 2; // de frente pra carreta
        sc = 1 + Math.sin(k * Math.PI * 8) * 0.05;
      } else if (t < T3) {
        const k = (t - T2) / (T3 - T2);
        p.lerpVectors(topSide, this.roofSpot, easeInOut(k)); p.y += Math.sin(k * Math.PI) * 0.6;
        ry = Math.PI / 2 * (1 - k) - Math.PI / 2 * k;
      } else {
        this.mode = 'roof'; p.copy(this.roofSpot);
        G.onEvent('subiu', this);
      }
    } else {
      const D = 0.85, k = clamp01(this.trans / D);
      p.lerpVectors(this.tFrom, this.tTo, k);
      p.y = this.tFrom.y * (1 - k) + Math.sin(k * Math.PI) * 1.6 + (1 - k) * 0;
      p.y = Math.max(p.y, this.tFrom.y * (1 - k * k));
      rx = -easeInOut(k) * Math.PI * 2; // mortal pra descer
      if (k >= 1) {
        this.mode = 'street'; this.lat = LANES[0]; this.laneIdx = 0; this.ds = this.tTo.z; this.y = 0; this.vy = 0; this.onGround = true;
        this.s = truck.s + this.ds; this._lastS = this.s;
        G.onEvent('desceu', this);
      }
    }
    truck.toWorld(p, this.root.position);
    this.root.quaternion.setFromRotationMatrix(truck.root.matrixWorld);
    this.pivot.rotation.set(rx, ry, 0);
    this.squash.scale.set(1, sc, 1);
    this.shadow.visible = false;
  }

  updateStreet(dt, G) {
    const { track, truck } = G;
    // fica ao lado da carreta; se tropeçar, fica pra trás e depois alcança
    const targetDs = this.stumble > 0 ? this.ds : 0;
    this.ds += (targetDs - this.ds) * clamp01(dt * 0.9);
    const s = truck.s + this.ds;
    const tl = LANES[this.laneIdx];
    const dl = tl - this.lat;
    this.lat += Math.sign(dl) * Math.min(Math.abs(dl), LANE_SPEED * dt);

    // chão (inclui teto de carro, banco, rampa)
    const prevGround = this.ground;
    const g = G.obstacles.groundAt(s, this.lat, this.y);
    this.ground = g.h;
    this.onCar = g.top;
    if (this.onGround) {
      if (this.y > this.ground + 0.05) {
        // saiu de cima de algo: cai (rampa lança pra cima)
        if (g.launch || prevGround > 0.5 && G.obstacles.lastRamp) { }
        this.onGround = false; this.vy = 0; this.airTricks = []; this.airTime = 0;
      } else this.y = this.ground;
      if (G.obstacles.rampLaunch(s, this.lat, this)) {
        this.vy = 13.5; this.onGround = false; this.airTricks = []; this.airTime = 0;
        G.onEvent('rampa', this);
      }
    }
    if (!this.onGround) {
      this.vy -= GRAV * dt; this.y += this.vy * dt; this.airTime += dt;
      if (this.y <= this.ground && this.vy <= 0) {
        this.y = this.ground; this.onGround = true; this.vy = 0;
        this.land(G, g);
      }
    }

    // manobras
    let rx = 0, ry = 0, rz = 0, sq = 1;
    if (this.trick) {
      const tr = this.trick; tr.t += dt; const k = clamp01(tr.t / tr.dur), e = easeInOut(k);
      switch (tr.type) {
        case 'mortal': rx = -e * Math.PI * 2; break;
        case 'flip': rx = e * Math.PI * 2; ry = e * Math.PI * 2; break;
        case 'estrela': rz = -tr.dir * e * Math.PI * 2; break;
        case 'pirueta': ry = e * Math.PI * 2; break;
      }
      if (tr.ground) { this.y = this.ground + Math.sin(k * Math.PI) * 0.6; }
      if (k >= 1) {
        if (tr.ground) { G.onEvent('manobra', this, tr.type); }
        else this.airTricks.push(tr.type);
        this.trick = null;
        if (this.queue && !this.onGround) { const q = this.queue; this.queue = null; this.trick = { type: q, t: 0, dur: TRICKS[q].dur, ground: false, dir: this.lastDir }; }
        this.queue = null;
      }
    }

    // corrida (balanço) e tropeço
    if (this.stumble > 0) {
      this.stumble -= dt;
      const k = 1 - this.stumble / 1.0;
      rx = Math.sin(k * Math.PI) * 1.2; rz = Math.sin(k * Math.PI * 3) * 0.3;
    }
    this.runPhase += dt * 13;
    const run = this.onGround && !this.trick && this.stumble <= 0;
    const bob = run ? Math.abs(Math.sin(this.runPhase)) * 0.12 : 0;
    if (run) { rz += Math.sin(this.runPhase) * 0.08; rx += 0.12; }
    if (!this.onGround && !this.trick) { rx += -0.25 * Math.sign(this.vy); sq = 1 + Math.min(0.12, Math.abs(this.vy) * 0.01); }
    // inclinação ao trocar de faixa
    rz += -Math.sign(dl) * Math.min(0.35, Math.abs(dl) * 0.25);

    const f = track.frame(s, {});
    this.root.position.set(f.x + f.rx * this.lat, f.y + this.y + bob, f.z + f.rz * this.lat);
    this.root.quaternion.setFromAxisAngle(_up, f.heading);
    this.pivot.rotation.set(rx, ry, rz, 'YXZ');
    if (this.trick && this.trick.type === 'estrela') this.pivot.rotation.set(0, 0, rz);
    this.squash.scale.set(1 / Math.sqrt(sq), sq, 1 / Math.sqrt(sq));
    this.squash.position.y = 0;
    // sombra no chão
    this.shadow.visible = true;
    this.shadow.position.set(f.x + f.rx * this.lat, f.y + this.ground + 0.06, f.z + f.rz * this.lat);
    const hgt = this.y - this.ground;
    this.shadow.scale.setScalar(Math.max(0.4, 1 - hgt * 0.12));
    this.s = s;

    // colisões
    if (this.invul <= 0) {
      const hit = G.obstacles.collide(s, this.lat, this.y, this);
      if (hit) this.crash(G, hit);
    }
    G.obstacles.collect(s, this.lat, this.y + this.h * 0.5, this, G);
  }

  land(G, g) {
    if (this.trick && !this.trick.ground) {
      const k = this.trick.t / this.trick.dur;
      if (k < 0.72) { this.trick = null; this.queue = null; this.crash(G, null, true); return; }
      this.airTricks.push(this.trick.type); this.trick = null;
    }
    const list = this.airTricks; this.airTricks = [];
    G.onEvent('pousou', this, list, g.top);
  }

  crash(G, obstacle, fromTrick = false) {
    if (this.invul > 0) return;
    this.stumble = 1.0; this.invul = 1.6; this.trick = null; this.queue = null;
    this.ds -= 4;
    if (!this.onGround) { this.vy = Math.min(this.vy, 0); }
    G.onEvent('tombo', this, obstacle, fromTrick);
  }
}

const _up = new THREE.Vector3(0, 1, 0);
