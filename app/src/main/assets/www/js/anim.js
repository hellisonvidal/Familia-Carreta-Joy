import * as THREE from 'three';
import * as SkeletonUtils from 'three/addons/utils/SkeletonUtils.js';

// =====================================================================
// Motor de animação: esqueleto padrão Mixamo tocando animações
// capturadas (parado, andar, correr, pulo, mortal, flip, tombo) e
// também as poses do jogo (dança, estrela, escalar...). Transição
// suave entre tudo. Os meninos recebem os movimentos por "retarget".
// =====================================================================
export const CLIP_INFO = {
  idle: { loop: true }, andar: { loop: true, speed: 1.45 }, correr: { loop: true, speed: 5.2 },
  pulo: { from: 0.55, to: 1.08 }, mortal: { from: 0.5, to: 1.55 }, flip: { from: 0.29, to: 0.95 },
  mortal2: { from: 0.17, to: 1.35 }, tombo: { from: 0.0, to: 1.6 }, queda: { from: 0.6, to: 2.4 },
  ladoE: { loop: true }, ladoD: { loop: true },
};
const MAP = { hips: 'Hips', spine: 'Spine', chest: 'Spine2', neck: 'Neck', head: 'Head', shL: 'LeftArm', elL: 'LeftForeArm', shR: 'RightArm', elR: 'RightForeArm', thL: 'LeftUpLeg', knL: 'LeftLeg', thR: 'RightUpLeg', knR: 'RightLeg' };
const _q = new THREE.Quaternion(), _q2 = new THREE.Quaternion(), _e = new THREE.Euler(), _v = new THREE.Vector3(), _v2 = new THREE.Vector3();

// prepara os clipes: tira a posição do quadril (o jogo controla o deslocamento) e recorta os trechos úteis
export function prepareClips(gltfs) {
  const out = {};
  for (const [name, g] of Object.entries(gltfs)) {
    let clip = g.animations[0]; if (!clip) continue;
    clip = clip.clone(); clip.name = name;
    clip.tracks = clip.tracks.filter(t => !/Hips\.position/.test(t.name));
    const info = CLIP_INFO[name] || {};
    if (info.from !== undefined) clip = THREE.AnimationUtils.subclip(clip, name, Math.round(info.from * 30), Math.round(info.to * 30), 30);
    clip.resetDuration();
    out[name] = clip;
  }
  return out;
}

export class Driver {
  // template = gltf.scene do X Bot; render = true quando este esqueleto é o próprio corpo visível
  constructor(template, clips, render) {
    this.root = SkeletonUtils.clone(template);
    if (!render) this.root.traverse(o => { if (o.isMesh) o.visible = false; });
    this.b = {};
    this.root.traverse(o => { if (o.isBone) { const short = o.name.replace(/^mixamorig:?/, ''); this.b[short] = o; } });
    this.bones = Object.values(this.b);
    this.clips = clips;
    // leitores das trilhas de cada animação (sem AnimationMixer: aplica sempre, quadro a quadro)
    this.tracks = {};
    for (const [n, c] of Object.entries(clips)) {
      this.tracks[n] = [];
      for (const tr of c.tracks) {
        const [node, prop] = tr.name.split('.'); const bone = this.b[node.replace(/^mixamorig:?/, '')];
        if (!bone || prop !== 'quaternion') continue;
        this.tracks[n].push({ bone, it: tr.createInterpolant() });
      }
    }
    this.ct = 0;
    this.cur = null;
    this.hipRest = this.b.Hips.position.clone();
    this.buildNeutral();
    this.prev = this.bones.map(b => b.quaternion.clone());
    this.first = true;
  }

  worldQ(bone, out = new THREE.Quaternion()) {
    out.identity(); const chain = []; let o = bone; while (o && o !== this.root) { chain.push(o); o = o.parent; }
    for (let i = chain.length - 1; i >= 0; i--) out.multiply(chain[i].quaternion);
    return out;
  }
  // pose neutra: braços para baixo (como os modelos dos meninos), a partir da pose T
  buildNeutral() {
    this.root.updateMatrixWorld(true);
    for (const [arm, fore, s] of [['LeftArm', 'LeftForeArm', 1], ['RightArm', 'RightForeArm', -1]]) {
      const A = this.b[arm], F = this.b[fore];
      const pa = A.getWorldPosition(new THREE.Vector3()), pf = F.getWorldPosition(new THREE.Vector3());
      const inv = new THREE.Matrix4().copy(this.root.matrixWorld).invert(); pa.applyMatrix4(inv); pf.applyMatrix4(inv);
      const dir = pf.sub(pa).normalize(), want = new THREE.Vector3(s * Math.sin(0.2), -Math.cos(0.2), 0);
      const swing = new THREE.Quaternion().setFromUnitVectors(dir, want);
      const W = this.worldQ(A), P = this.worldQ(A.parent);
      const newW = swing.multiply(W);
      A.quaternion.copy(P.invert().multiply(newW));
      this.root.updateMatrixWorld(true);
    }
    this.neutralLocal = this.bones.map(b => b.quaternion.clone());
    this.W0 = new Map(); for (const b of this.bones) this.W0.set(b, this.worldQ(b).clone());
    // local = W0_pai^-1 * E * W0_osso
    this.A = new Map(); for (const b of this.bones) this.A.set(b, (b.parent && b.parent.isBone ? this.W0.get(b.parent).clone() : this.worldQ(b.parent || this.root)).invert());
  }

  // aplica uma pose do jogo (ângulos das articulações, no espaço do personagem)
  setPose(p) {
    this.bones.forEach((b, i) => b.quaternion.copy(this.neutralLocal[i]));
    const put = (name, x, y, z) => { const b = this.b[name]; if (!b) return; _e.set(x, y, z, 'XYZ'); _q.setFromEuler(_e); b.quaternion.copy(this.A.get(b)).multiply(_q).multiply(this.W0.get(b)); };
    put('Hips', 0, p.twist || 0, p.sway || 0);
    put('Spine', p.lean || 0, p.spineTwist || 0, p.spineSway || 0);
    if (p.head) put('Head', p.head[0], p.head[1], p.head[2] || 0);
    put('LeftArm', p.sh[0], 0, p.sh[1] - 0.2); put('RightArm', p.sh[2], 0, p.sh[3] + 0.2);
    put('LeftForeArm', p.el[0], 0, 0); put('RightForeArm', p.el[1], 0, 0);
    put('LeftUpLeg', p.th[0], 0, p.th[1]); put('RightUpLeg', p.th[2], 0, p.th[3]);
    put('LeftLeg', p.kn[0], 0, 0); put('RightLeg', p.kn[1], 0, 0);
    this.bob = p.bob || 0;
  }

  // st = { clip, ts, restart } ou { pose }
  update(dt, st, smooth = 14) {
    if (st.clip && this.tracks[st.clip]) {
      const c = this.clips[st.clip], info = CLIP_INFO[st.clip] || {};
      if (this.cur !== st.clip || st.restart) { this.cur = st.clip; this.ct = 0; }
      this.ct += dt * (st.ts ?? 1);
      const t = info.loop ? this.ct % c.duration : Math.min(this.ct, c.duration);
      for (const { bone, it } of this.tracks[st.clip]) bone.quaternion.fromArray(it.evaluate(t));
      this.bob = 0;
    } else {
      this.cur = null;
      this.setPose(st.pose);
    }
    // suavização (transição entre movimentos)
    const k = this.first ? 1 : 1 - Math.exp(-dt * smooth); this.first = false;
    this.bones.forEach((b, i) => { this.prev[i].slerp(b.quaternion, k); b.quaternion.copy(this.prev[i]); });
    this.b.Hips.position.set(this.hipRest.x, this.hipRest.y + this.bob * (this.hipRest.y > 50 ? 100 : 1), this.hipRest.z);
    this.root.updateMatrixWorld(true);
  }
  clipDur(name) { return this.clips[name] ? this.clips[name].duration : 1; }
  progress() { return this.cur ? Math.min(1, this.ct / this.clips[this.cur].duration) : 0; }

  // copia os movimentos para um corpo com articulações "em repouso = identidade" (meninos e fantasias)
  retarget(J, setBob) {
    const D = (name) => this.worldQ(this.b[MAP[name]], new THREE.Quaternion()).multiply(_q2.copy(this.W0.get(this.b[MAP[name]])).invert());
    const Dh = D('hips'), Dc = D('chest'), Dhd = D('head');
    const loc = (parentW, W) => parentW.clone().invert().multiply(W);
    J.hips.quaternion.copy(Dh);
    J.spine.quaternion.copy(loc(Dh, Dc));
    J.head.quaternion.copy(loc(Dc, Dhd));
    if (J.neck) J.neck.quaternion.identity();
    for (const s of ['L', 'R']) {
      const Ds = D('sh' + s), De = D('el' + s), Dt = D('th' + s), Dk = D('kn' + s);
      J['sh' + s].quaternion.copy(loc(Dc, Ds)); J['el' + s].quaternion.copy(loc(Ds, De));
      J['th' + s].quaternion.copy(loc(Dh, Dt)); J['kn' + s].quaternion.copy(loc(Dt, Dk));
    }
    if (setBob) setBob(this.bob);
  }
}
