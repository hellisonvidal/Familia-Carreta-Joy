import * as THREE from 'three';
import { makeBody, POSES, lerpPose } from './characters.js';
const SEED = () => Math.random() * 10;
import { TRUCK_LEN } from './truck.js';

export const TRICKS = {
  mortal:  { nome: 'MORTAL',  dur: 0.62, pts: 100 },
  flip:    { nome: 'FLIP',    dur: 0.72, pts: 150 },
  estrela: { nome: 'ESTRELA', dur: 0.6,  pts: 80 },
  pirueta: { nome: 'PIRUETA', dur: 0.5,  pts: 60 },
};
const GRAV = 24;
const ease = (t) => t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2;
const clamp01 = (t) => Math.max(0, Math.min(1, t));
const UP = new THREE.Vector3(0, 1, 0);
const _v = new THREE.Vector3(), _q = new THREE.Quaternion();

let shadowTex = null;
function blob() {
  if (shadowTex) return shadowTex;
  const c = document.createElement('canvas'); c.width = c.height = 64; const g = c.getContext('2d');
  const gr = g.createRadialGradient(32, 32, 0, 32, 32, 32); gr.addColorStop(0, 'rgba(0,0,0,0.5)'); gr.addColorStop(1, 'rgba(0,0,0,0)');
  g.fillStyle = gr; g.fillRect(0, 0, 64, 64); shadowTex = new THREE.CanvasTexture(c); return shadowTex;
}

export class Actor {
  constructor(def, assets, costume) {
    this.def = def; this.name = def.nome; this.kid = !!def.kid;
    this.assets = assets;
    this.root = new THREE.Group();
    this.body = makeBody(def, costume, assets); this.root.add(this.body.root);
    this.shadow = new THREE.Mesh(new THREE.PlaneGeometry(1, 1).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ map: blob(), transparent: true, depthWrite: false }));
    this.shadow.renderOrder = 1;
    this.speedMax = def.forte > 1.5 ? 6.2 : this.kid ? 6.6 : 7.2;
    this.jumpV = def.forte > 1.5 ? 10.5 : this.kid ? 9.4 : 9.8;
    this.H = def.altura;
    if (def.poseInicial && assets.afonsoFull) {
      // modelo original agachado (pose característica)
      const m = assets.afonsoFull.clone(true);
      const box = new THREE.Box3().setFromObject(m), s = 0.53;
      m.scale.setScalar(s); m.position.y = -box.min.y * s;
      this.fullModel = m; this.root.add(m); m.visible = false;
    }
    this.reset();
  }

  reset() {
    this.frame = 'world'; this.pos = new THREE.Vector3(); this.vel = new THREE.Vector3(); this.yaw = 0;
    this.grounded = true; this.ground = 0; this.mode = 'normal'; this.trick = null; this.queue = null; this.airTricks = [];
    this.stumble = 0; this.invul = 0; this.airTime = 0; this.runPhase = Math.random() * 6; this.dance = null; this.standT = 0;
    this.controlled = false; this.ai = { t: 0 }; this.onTop = null; this.lastPushT = 0;
    this.cur = null; this.landT = 0; this.seed = this.seed ?? SEED(); this.prevYaw = 0; this.turn = 0;
    if (this.fullModel) this.fullModel.visible = false;
    this.body.rig.visible = true; this.root.visible = true;
  }

  setCostume(key) {
    this.root.remove(this.body.root);
    this.body = makeBody(this.def, key, this.assets); this.root.add(this.body.root);
    if (this.mode === 'pose') this.body.rig.visible = false;
  }

  // posição no mundo (para câmera, distância etc.)
  worldPos(G, out = new THREE.Vector3()) {
    if (this.frame === 'truck') return G.truck.toWorld(this.pos, out);
    return out.copy(this.pos);
  }

  // ---------------- comandos ----------------
  jump(boost = 1) {
    if (this.mode !== 'normal' || !this.grounded || this.stumble > 0) return false;
    this.vel.y = this.jumpV * boost; this.grounded = false; this.airTricks = []; this.airTime = 0;
    return true;
  }
  startTrick(type) {
    if (this.mode !== 'normal' || this.stumble > 0) return false;
    if (this.trick) { if (this.trick.t / this.trick.dur > 0.6) { this.queue = type; return true; } return false; }
    if (this.grounded) {
      if (type === 'estrela') { this.trick = { type, t: 0, dur: 0.65, ground: true }; return true; }
      this.jump();
    }
    this.trick = { type, t: 0, dur: TRICKS[type].dur, ground: false };
    return true;
  }
  danceMove(type) { this.dance = { type, t: 0, dur: type === 'mortal' ? 0.7 : 0.5 }; }

  // Afonso: começa agachado e levanta quando se mexe
  startPose() { if (!this.fullModel) return; this.mode = 'pose'; this.fullModel.visible = true; this.body.rig.visible = false; }
  standUp(G) {
    if (this.mode !== 'pose') return;
    this.fullModel.visible = false; this.body.rig.visible = true; this.mode = 'stand'; this.standT = 0;
    G.onEvent('levantou', this);
  }

  // subir na carreta pela lateral
  canClimbTruck(G) {
    if (this.frame !== 'world' || this.mode !== 'normal' || !this.grounded) return false;
    const l = G.truck.toLocal(this.pos, _v);
    return Math.abs(l.x) < G.truck.halfW + 2.2 && l.z > G.truck.roofZ[0] - 1 && l.z < G.truck.roofZ[1] + 1 && l.y < 2;
  }
  beginClimbTruck(G, spot) {
    if (!this.canClimbTruck(G) && !this.forceClimb) return false;
    this.forceClimb = false;
    const T = G.truck, l = T.toLocal(this.pos, new THREE.Vector3());
    const side = l.x >= 0 ? 1 : -1;
    const z = Math.max(T.roofZ[0] + 0.6, Math.min(T.roofZ[1] - 0.6, l.z));
    this.cFrom = l.clone(); this.cSide = new THREE.Vector3(side * (T.halfW + 0.35), Math.max(0, l.y), z);
    this.cTop = new THREE.Vector3(side * (T.halfW + 0.35), T.roofY, z);
    this.cTo = spot ? spot.clone() : new THREE.Vector3(side * (T.halfW - 0.5), T.roofY, z);
    this.cSideSign = side;
    this.frame = 'truck'; this.pos.copy(l); this.mode = 'truckclimb'; this.ct = 0; this.trick = null; this.vel.set(0, 0, 0);
    return true;
  }
  beginDescend(G) {
    if (this.frame !== 'truck' || this.mode !== 'normal') return false;
    const T = G.truck, side = this.pos.x >= 0 ? 1 : -1;
    this.dFrom = this.pos.clone(); this.dTo = new THREE.Vector3(side * (T.halfW + 2.2), 0, this.pos.z + 1.5); this.dSide = side;
    this.mode = 'descend'; this.ct = 0; this.trick = null;
    return true;
  }
  enterDrive(G) { this.mode = 'drive'; this.root.visible = false; this.shadow.visible = false; this.trick = null; }
  exitDrive(G) {
    const T = G.truck;
    const p = T.toWorld(new THREE.Vector3(T.halfW + 1.3, 0, TRUCK_LEN / 2 - 3), new THREE.Vector3());
    this.frame = 'world'; this.pos.copy(p); this.pos.y = G.city.groundAt(p.x, p.z, p.y + 2).h;
    this.vel.set(0, 0, 0); this.mode = 'normal'; this.root.visible = true; this.grounded = true; this.yaw = T.yaw;
  }

  // ---------------- atualização ----------------
  // inp = { mx, mz (direção desejada no mundo, 0..1), want (0..1) }
  update(dt, G, inp) {
    this.invul = Math.max(0, this.invul - dt);
    this.stumble = Math.max(0, this.stumble - dt);
    const T = G.truck;
    if (this.mode === 'drive') { this.pos.copy(T.toWorld(_v.set(0, 2, TRUCK_LEN / 2 - 1.5))); return; }
    if (this.mode === 'pose') {
      if (inp && inp.want > 0.2) this.standUp(G);
      this.place(G); this.animate(dt, G, 0); return;
    }
    if (this.mode === 'truckclimb') return this.updateClimbTruck(dt, G);
    if (this.mode === 'descend') return this.updateDescend(dt, G);
    if (this.mode === 'ledge') return this.updateLedge(dt, G);
    if (this.mode === 'stand') { this.standT += dt; if (this.standT > 0.75) this.mode = 'normal'; }

    // direção desejada (no referencial atual)
    let mx = inp ? inp.mx : 0, mz = inp ? inp.mz : 0, want = inp ? inp.want : 0;
    if (this.mode === 'stand' || this.stumble > 0.4 || G.freeze) want = 0;
    if (this.frame === 'truck') { // converte direção do mundo para o referencial da carreta
      const a = -T.yaw, c = Math.cos(a), s = Math.sin(a);
      const lx = mx * c + mz * s, lz = -mx * s + mz * c; mx = lx; mz = lz;
    }
    const sp = this.speedMax * (this.boost || 1) * want * (this.frame === 'truck' ? 0.55 : 1);
    const acc = this.grounded ? 30 : 9;
    const tvx = mx * sp, tvz = mz * sp;
    this.vel.x += Math.max(-acc * dt, Math.min(acc * dt, tvx - this.vel.x));
    this.vel.z += Math.max(-acc * dt, Math.min(acc * dt, tvz - this.vel.z));
    if (want > 0.1) { let d = Math.atan2(mx, mz) - this.yaw; while (d > Math.PI) d -= 2 * Math.PI; while (d < -Math.PI) d += 2 * Math.PI; this.yaw += d * Math.min(1, dt * 12); }

    // estrela no chão: rodinha para o lado
    if (this.trick && this.trick.ground) { const s2 = Math.sin(this.yaw + Math.PI / 2), c2 = Math.cos(this.yaw + Math.PI / 2); this.vel.x = s2 * 4.5 * (this.trick.dir || 1); this.vel.z = c2 * 4.5 * (this.trick.dir || 1); }

    this.pos.x += this.vel.x * dt; this.pos.z += this.vel.z * dt;
    if (!this.grounded) { this.vel.y -= GRAV * dt; this.airTime += dt; }
    this.pos.y += this.vel.y * dt;

    if (this.frame === 'world') this.physicsWorld(dt, G); else this.physicsTruck(dt, G);
    this.updateTricks(dt, G);
    this.place(G);
    const hs = Math.hypot(this.vel.x, this.vel.z);
    this.animate(dt, G, hs);
    // notas
    if (this.frame === 'world' || true) {
      const wp = this.worldPos(G, _v);
      G.city.collect(wp, wp.y + this.H * 0.5, (n) => G.onEvent(n.gold ? 'estrelaDourada' : 'nota', this, n));
    }
  }

  physicsWorld(dt, G) {
    const C = G.city, T = G.truck;
    // colisões com prédios, carros, quiosques...
    const hit = C.collide(this.pos, 0.32, this.pos.y);
    if (G.life) G.life.pushActor(this);
    if (hit && hit.c.walk && this.mode === 'normal') {
      const ledge = C.topAt(hit.c, this.pos.x - hit.nx * 0.6, this.pos.z - hit.nz * 0.6) - this.pos.y;
      const pushing = (this.vel.x * -hit.nx + this.vel.z * -hit.nz) > 0.5 || G.now - this.lastPushT < 0.15;
      this.lastPushT = G.now;
      if (pushing && ledge > 0 && (ledge < 1.15 || (!this.grounded && ledge < 2.6) || (this.grounded && ledge < 2.6 && this.climbIntent))) {
        this.mode = 'ledge'; this.ct = 0; this.lFrom = this.pos.clone();
        this.lTo = new THREE.Vector3(this.pos.x - hit.nx * 0.7, this.pos.y + ledge, this.pos.z - hit.nz * 0.7);
        this.lDur = 0.2 + ledge * 0.15; this.trick = null; this.vel.set(0, 0, 0);
        G.onEvent('escalou', this, ledge); return;
      }
    }
    // carreta: lateral é parede, teto é chão
    const l = T.toLocal(this.pos, _v);
    const inX = Math.abs(l.x) < T.halfW + 0.3, inZ = l.z > -TRUCK_LEN / 2 && l.z < TRUCK_LEN / 2;
    if (inX && inZ) {
      const onRoofZ = l.z > T.roofZ[0] && l.z < T.roofZ[1];
      if (onRoofZ && l.y >= T.roofY - 0.7 && this.vel.y <= 0.5) { // pousou no teto
        this.frame = 'truck'; this.pos.copy(l); this.pos.y = T.roofY; this.grounded = true; this.vel.set(0, 0, 0);
        this.land(G, null); return;
      } else if (l.y < T.roofY - 0.7) { // empurra pra fora pela lateral
        const pushX = (T.halfW + 0.31) * Math.sign(l.x || 1) - l.x;
        const pushZ = l.z > 0 ? TRUCK_LEN / 2 - l.z : -TRUCK_LEN / 2 - l.z;
        if (Math.abs(pushX) < Math.abs(pushZ)) l.x += pushX; else l.z += pushZ;
        T.toWorld(l, this.pos); this.pos.y = this.pos.y; // altura é refeita abaixo
      }
    }
    // chão
    const g = C.groundAt(this.pos.x, this.pos.z, this.pos.y);
    this.ground = g.h; this.onTop = g.top;
    if (this.grounded) {
      if (this.pos.y > g.h + 0.35) { this.grounded = false; this.vel.y = 0; this.airTricks = []; this.airTime = 0; }
      else { this.pos.y = g.h; this.vel.y = 0; }
      // rampa lança pra cima
      if (g.top && g.top.ramp) this.onRamp = g.top;
      else if (this.onRamp) { const r = this.onRamp; this.onRamp = null; if (Math.hypot(this.vel.x, this.vel.z) > 2) { this.vel.y = 12.5; this.grounded = false; this.airTricks = []; this.airTime = 0; G.onEvent('rampa', this); } }
    } else if (this.pos.y <= g.h && this.vel.y <= 0) {
      this.pos.y = g.h; this.grounded = true; this.vel.y = 0; this.land(G, g.top);
    }
    if (this.pos.y < g.h - 3) this.pos.y = g.h; // nunca cair pelo chão
  }

  physicsTruck(dt, G) {
    const T = G.truck;
    const inside = Math.abs(this.pos.x) < T.halfW - 0.05 && this.pos.z > T.roofZ[0] && this.pos.z < T.roofZ[1];
    if (inside) {
      if (this.grounded) { this.pos.y = T.roofY; this.vel.y = 0; }
      else if (this.pos.y <= T.roofY && this.vel.y <= 0) { this.pos.y = T.roofY; this.grounded = true; this.vel.y = 0; this.land(G, null); }
    } else {
      // saiu do teto: passa a cair no mundo (herda a velocidade da carreta)
      const w = T.toWorld(this.pos, new THREE.Vector3());
      const a = T.yaw, c = Math.cos(a), s = Math.sin(a);
      const vx = this.vel.x * c + this.vel.z * s, vz = -this.vel.x * s + this.vel.z * c;
      this.frame = 'world'; this.pos.copy(w); this.vel.x = vx + T.vel.x; this.vel.z = vz + T.vel.z; this.yaw += T.yaw;
      if (this.grounded) { this.grounded = false; this.vel.y = 1.5; this.airTricks = []; this.airTime = 0; }
    }
  }

  updateTricks(dt, G) {
    if (!this.trick) return;
    const tr = this.trick; tr.t += dt;
    if (tr.t >= tr.dur) {
      if (tr.ground) G.onEvent('manobra', this, tr.type);
      else this.airTricks.push(tr.type);
      this.trick = null;
      if (this.queue && !this.grounded) { const q = this.queue; this.trick = { type: q, t: 0, dur: TRICKS[q].dur, ground: false }; }
      this.queue = null;
    }
  }

  land(G, top) {
    if (this.trick && !this.trick.ground) {
      if (this.trick.t / this.trick.dur < 0.72) { this.trick = null; this.queue = null; this.crash(G, true); return; }
      this.airTricks.push(this.trick.type); this.trick = null;
    }
    const list = this.airTricks; this.airTricks = [];
    if (this.airTime > 0.25) this.landT = Math.min(0.3, 0.12 + this.airTime * 0.12);
    G.onEvent('pousou', this, list, top, this.airTime);
  }
  crash(G, fromTrick) {
    if (this.invul > 0) return;
    this.stumble = 1.0; this.invul = 1.5; this.trick = null; this.queue = null; this.vel.x *= 0.2; this.vel.z *= 0.2;
    G.onEvent('tombo', this, fromTrick);
  }

  updateLedge(dt, G) {
    this.ct += dt; const k = clamp01(this.ct / this.lDur);
    this.pos.lerpVectors(this.lFrom, this.lTo, k); this.pos.y = this.lFrom.y + (this.lTo.y - this.lFrom.y) * Math.min(1, k * 1.4) + Math.sin(k * Math.PI) * 0.3;
    if (k >= 1) { this.mode = 'normal'; this.grounded = true; this.vel.set(0, 0, 0); this.pos.copy(this.lTo); }
    this.place(G); this.cur = POSES.climb(this.ct); this.body.apply(this.cur); this.body.pivot.rotation.set(0, 0, 0);
  }
  updateClimbTruck(dt, G) {
    this.ct += dt; const t = this.ct, p = this.pos, T1 = 0.25, T2 = 1.0, T3 = 1.3;
    let face = this.cSideSign > 0 ? -Math.PI / 2 : Math.PI / 2;
    if (t < T1) p.lerpVectors(this.cFrom, this.cSide, ease(t / T1));
    else if (t < T2) { const k = (t - T1) / (T2 - T1); p.lerpVectors(this.cSide, this.cTop, k); }
    else if (t < T3) { const k = (t - T2) / (T3 - T2); p.lerpVectors(this.cTop, this.cTo, ease(k)); p.y += Math.sin(k * Math.PI) * 0.5; }
    else { this.mode = 'normal'; p.copy(this.cTo); this.grounded = true; this.vel.set(0, 0, 0); G.onEvent('subiu', this); }
    this.yaw = face; this.place(G);
    this.cur = t > T1 && t < T2 ? POSES.climb(t) : POSES.idle(t, this.seed); this.body.apply(this.cur); this.body.pivot.rotation.set(0, 0, 0);
  }
  updateDescend(dt, G) {
    this.ct += dt; const D = 0.9, k = clamp01(this.ct / D), p = this.pos;
    p.lerpVectors(this.dFrom, this.dTo, k); p.y = this.dFrom.y * (1 - k * k) + Math.sin(k * Math.PI) * 1.6;
    this.yaw = this.dSide > 0 ? Math.PI / 2 : -Math.PI / 2;
    this.place(G);
    this.cur = POSES.air(1, 1); this.body.apply(this.cur); this.body.pivot.rotation.set(-ease(k) * Math.PI * 2, 0, 0);
    if (k >= 1) {
      const w = G.truck.toWorld(this.dTo, new THREE.Vector3());
      this.frame = 'world'; this.pos.copy(w); this.pos.y = G.city.groundAt(w.x, w.z, w.y + 1.5).h; this.yaw += G.truck.yaw;
      this.vel.set(G.truck.vel.x, 0, G.truck.vel.z); this.mode = 'normal'; this.grounded = true;
      this.body.pivot.rotation.set(0, 0, 0); G.onEvent('desceu', this);
    }
  }

  place(G) {
    if (this.frame === 'truck') {
      G.truck.toWorld(this.pos, this.root.position);
      _q.setFromRotationMatrix(G.truck.root.matrixWorld);
      this.root.quaternion.copy(_q).multiply(new THREE.Quaternion().setFromAxisAngle(UP, this.yaw));
      this.shadow.visible = false;
    } else {
      this.root.position.copy(this.pos);
      this.root.quaternion.setFromAxisAngle(UP, this.yaw);
      this.shadow.visible = this.mode !== 'drive';
      this.shadow.position.set(this.pos.x, this.ground + 0.07, this.pos.z);
      this.shadow.scale.setScalar(Math.max(0.35, (this.def.forte || 1) * 0.9 - (this.pos.y - this.ground) * 0.1));
    }
  }

  animate(dt, G, hs) {
    const B = this.body;
    let rx = 0, ry = 0, rz = 0;
    if (this.mode === 'pose') { this.cur = POSES.crouchStart(); B.apply(this.cur); B.pivot.rotation.set(0, 0, 0); return; }
    if (this.mode === 'stand') { this.cur = lerpPose(POSES.crouchStart(), POSES.idle(0), ease(clamp01(this.standT / 0.7))); B.apply(this.cur); B.pivot.rotation.set(0, 0, 0); return; }
    let pose;
    const tr = this.trick;
    if (tr) {
      const k = clamp01(tr.t / tr.dur), e = ease(k);
      if (tr.type === 'mortal') { rx = -e * Math.PI * 2; pose = POSES.air(this.vel.y, Math.sin(k * Math.PI)); }
      else if (tr.type === 'flip') { rx = e * Math.PI * 2; ry = e * Math.PI * 2; pose = POSES.air(this.vel.y, Math.sin(k * Math.PI)); }
      else if (tr.type === 'pirueta') { ry = e * Math.PI * 2; pose = POSES.air(this.vel.y, 0.3); }
      else { rz = -e * Math.PI * 2 * (tr.dir || 1); pose = POSES.star(); }
      if (tr.ground) this.pos.y = this.ground + Math.sin(k * Math.PI) * 0.5;
    } else if (this.stumble > 0) {
      const k = 1 - this.stumble; rx = Math.sin(k * Math.PI) * 1.1; pose = POSES.air(-1, 0.5);
    } else if (!this.grounded) pose = POSES.air(this.vel.y, 0);
    else if (hs > 0.5) {
      const L = this.H / 1.75; // passo proporcional à altura
      if (hs < 3.2) { this.runPhase += dt * hs * 3.2 / L; pose = POSES.walk(this.runPhase, Math.min(1, hs / 2.2)); }
      else { this.runPhase += dt * (5 + hs * 1.5) / Math.sqrt(L); pose = POSES.run(this.runPhase, Math.min(1, hs / 6), this.H); }
    }
    else if (this.frame === 'truck' || (G.dancing && (!G.comboDance || this.controlled || this.comboJoin)) || G.party || this.dance) {
      let mk = 0, mv = null;
      if (this.dance) { this.dance.t += dt; mk = clamp01(this.dance.t / this.dance.dur); mv = this.dance.type; if (this.dance.type === 'mortal') rx = -ease(mk) * Math.PI * 2; if (mv === 'cima') ry = ease(mk) * Math.PI * 2; if (this.dance.t > this.dance.dur) this.dance = null; }
      pose = POSES.dance(G.beat, this.def.estilo, mv === 'mortal' ? 'cima' : mv, mk);
      if (mv === 'cima' || mv === 'mortal') pose.bob = (pose.bob || 0) + Math.sin(mk * Math.PI) * 0.9;
    } else pose = POSES.idle(G.now, this.seed);
    // aterrissagem: dobra os joelhos
    if (this.landT > 0 && this.grounded && !tr) { this.landT -= dt; pose = lerpPose(pose, POSES.land(), Math.min(1, this.landT / 0.12)); }
    // inclina nas curvas
    let dy = this.yaw - this.prevYaw; while (dy > Math.PI) dy -= 2 * Math.PI; while (dy < -Math.PI) dy += 2 * Math.PI; this.prevYaw = this.yaw;
    this.turn += ((dt > 0 ? dy / dt : 0) - this.turn) * Math.min(1, dt * 6);
    if (hs > 2 && this.grounded) pose.sway = (pose.sway || 0) - Math.max(-0.25, Math.min(0.25, this.turn * 0.05 * hs / 6));
    // olha para a câmera quando está parado perto dela
    if (hs < 0.5 && !tr && G.camPos && this.mode === 'normal') {
      const wp = this.worldPos(G, new THREE.Vector3());
      const d = G.camPos.distanceTo(wp);
      if (d < 9) {
        const yawW = this.frame === 'truck' ? this.yaw + G.truck.yaw : this.yaw;
        let a = Math.atan2(G.camPos.x - wp.x, G.camPos.z - wp.z) - yawW; while (a > Math.PI) a -= 2 * Math.PI; while (a < -Math.PI) a += 2 * Math.PI;
        if (Math.abs(a) < 1.8) { pose.head = [(pose.head ? pose.head[0] : 0) - 0.05, Math.max(-0.8, Math.min(0.8, a)) * 0.8, pose.head ? pose.head[2] || 0 : 0]; }
      }
    }
    // transição suave entre as poses (rápida nas manobras)
    const k = 1 - Math.exp(-dt * (tr || !this.grounded ? 22 : 13));
    this.cur = this.cur ? lerpPose(this.cur, pose, k) : pose;
    B.apply(this.cur);
    B.pivot.rotation.set(rx, ry, rz, 'YXZ');
    if (tr && tr.type === 'estrela') B.pivot.rotation.set(0, 0, rz);
  }
}
