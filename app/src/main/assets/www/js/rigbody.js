import * as THREE from 'three';
import { Driver } from './anim.js';

// =====================================================================
// Meninos com o corpo REAL já com esqueleto Mixamo (modelos do rascunho).
// Cada esqueleto tem uma pose de repouso diferente (Arthur em T, Afonso
// com os braços abaixados), então as animações do X Bot NÃO podem ser
// copiadas direto. Fazemos "retarget": a animação toca num X Bot
// invisível e cada osso do menino recebe só o quanto o osso do X Bot
// girou em relação à pose T (com a direção dos ossos alinhada antes).
// Animações feitas para o próprio menino tocam direto nele.
// =====================================================================
const CHAIN = [ // [osso, filho usado para a direção]
  ['Hips', 'Spine'], ['Spine', 'Spine1'], ['Spine1', 'Spine2'], ['Spine2', 'Neck'], ['Neck', 'Head'], ['Head', null],
  ['LeftShoulder', 'LeftArm'], ['LeftArm', 'LeftForeArm'], ['LeftForeArm', 'LeftHand'], ['LeftHand', null],
  ['RightShoulder', 'RightArm'], ['RightArm', 'RightForeArm'], ['RightForeArm', 'RightHand'], ['RightHand', null],
  ['LeftUpLeg', 'LeftLeg'], ['LeftLeg', 'LeftFoot'], ['LeftFoot', 'LeftToeBase'], ['LeftToeBase', null],
  ['RightUpLeg', 'RightLeg'], ['RightLeg', 'RightFoot'], ['RightFoot', 'RightToeBase'], ['RightToeBase', null],
];
const _q = new THREE.Quaternion(), _q2 = new THREE.Quaternion();

export class RigBody {
  constructor(template, clips, def, opt = {}) {
    // own: corpo visível; src: X Bot invisível que toca as animações
    this.own = new Driver(template, clips, true);
    this.src = new Driver(opt.xbot, clips, false);
    this.native = new Set(opt.native || []); // clipes que tocam direto no menino
    this.alias = opt.alias || {};
    this.drv = this.src; // (o resto do jogo consulta duração/progresso por aqui)
    const root = this.own.root;
    root.traverse(o => {
      if (!o.isMesh) return;
      o.frustumCulled = false; o.castShadow = true;
      const m = o.material; if (m) { if (m.map) m.map.anisotropy = 8; if (m.normalMap) m.normalMap.anisotropy = 4; m.metalness = 0; m.metalnessMap = null; m.roughnessMap = null; m.roughness = 0.95; m.envMapIntensity = 0.35; m.normalScale && m.normalScale.set(0.5, 0.5); m.needsUpdate = true; }
    });
    // volta o menino para a pose de fábrica
    this.own.bones.forEach((b, i) => b.quaternion.copy(this.own.bindLocal[i]));
    root.updateMatrixWorld(true);
    this.fixWaist();
    // alinhamento: rotação que leva a direção de cada osso do menino para a do X Bot (pose T)
    this.map = [];
    for (const [n, child] of CHAIN) {
      const kb = this.own.b[n], sb = this.src.b[n]; if (!kb || !sb) continue;
      let align = new THREE.Quaternion();
      if (child && this.own.b[child] && this.src.b[child]) {
        const dk = this.own.Pbind.get(this.own.b[child]).clone().sub(this.own.Pbind.get(kb)).normalize();
        const ds = this.src.Pbind.get(this.src.b[child]).clone().sub(this.src.Pbind.get(sb)).normalize();
        align.setFromUnitVectors(dk, ds);
      } else { const prev = this.map[this.map.length - 1]; if (prev) align.copy(prev.align); }
      const tgtT = align.clone().multiply(this.own.Wbind.get(kb));      // menino "em pose T"
      const srcTinv = this.src.Wbind.get(sb).clone().invert();
      this.map.push({ kb, sb, align, tgtT, srcTinv, n });
    }
    this.prev = this.own.bones.map(b => b.quaternion.clone()); this.first = true;
    // escala e pivô
    const box = new THREE.Box3().setFromObject(root), h0 = box.max.y - box.min.y;
    const s = def.altura / h0;
    this.root = new THREE.Group(); this.pivot = new THREE.Group(); this.rig = new THREE.Group();
    this.root.add(this.pivot); this.pivot.add(this.rig); this.rig.add(root);
    root.scale.multiplyScalar(s);
    const hip = this.own.b.Hips.getWorldPosition(new THREE.Vector3());
    const hipY = (hip.y - box.min.y) * s;
    this.pivot.position.y = hipY; this.rig.position.y = -hipY - box.min.y * s;
    this.hipRest = this.own.b.Hips.position.clone();
    this.hipScale = this.own.hipRest.y / Math.max(1e-6, this.src.hipRest.y);
    this.H = def.altura;
  }

  // A camiseta acima da virilha estava presa aos ossos das coxas (pesos do Meshy):
  // ao levantar a perna, a barra da camiseta afinava a cintura. Passa esses pesos para o quadril.
  fixWaist() {
    const own = this.own, root = own.root, inv = new THREE.Matrix4().copy(root.matrixWorld).invert();
    const yL = own.Pbind.get(own.b.LeftUpLeg).y, yR = own.Pbind.get(own.b.RightUpLeg).y;
    const hipY = own.Pbind.get(own.b.Hips).y, legY = Math.min(yL, yR);
    const H = own.Pbind.get(own.b.Head).y - legY;
    const lo = legY - H * 0.02, hi = legY + H * 0.12; // abaixo de "lo": perna; acima de "hi": tronco
    const ss = (a, b, x) => { const t = Math.max(0, Math.min(1, (x - a) / (b - a))); return t * t * (3 - 2 * t); };
    const v = new THREE.Vector3();
    root.traverse(o => {
      if (!o.isSkinnedMesh) return;
      const bones = o.skeleton.bones, hipsI = bones.findIndex(b => /Hips$/.test(b.name));
      if (hipsI < 0) return;
      const legI = new Set(bones.map((b, i) => /(Left|Right)(UpLeg|Leg)$/.test(b.name) ? i : -1).filter(i => i >= 0));
      const si = o.geometry.attributes.skinIndex, sw = o.geometry.attributes.skinWeight, pos = o.geometry.attributes.position;
      const m = new THREE.Matrix4().multiplyMatrices(inv, o.matrixWorld);
      let changed = 0;
      for (let i = 0; i < pos.count; i++) {
        v.fromBufferAttribute(pos, i); o.applyBoneTransform(i, v); v.applyMatrix4(m);
        const f = ss(lo, hi, v.y); if (f <= 0) continue;
        let moved = 0, hipSlot = -1;
        for (let k = 0; k < 4; k++) {
          const j = si.getComponent(i, k), w = sw.getComponent(i, k);
          if (j === hipsI) hipSlot = k;
          if (legI.has(j) && w > 0) { moved += w * f; sw.setComponent(i, k, w * (1 - f)); }
        }
        if (moved <= 0) continue;
        if (hipSlot < 0) { // usa o espaço de menor peso
          let mk = 0; for (let k = 1; k < 4; k++) if (sw.getComponent(i, k) < sw.getComponent(i, mk)) mk = k;
          moved += sw.getComponent(i, mk); si.setComponent(i, mk, hipsI); sw.setComponent(i, mk, 0); hipSlot = mk;
        }
        sw.setComponent(i, hipSlot, sw.getComponent(i, hipSlot) + moved);
        changed++;
      }
      si.needsUpdate = sw.needsUpdate = true;
      this.waistFixed = (this.waistFixed || 0) + changed;
    });
  }

  update(dt, st) {
    if (st.clip && this.alias[st.clip]) st = { ...st, clip: this.alias[st.clip] };
    const own = this.own;
    if (st.clip && this.native.has(st.clip) && own.clips[st.clip]) {
      // animação feita para este menino: toca direto
      own.update(dt, st, 1e9);
      this.src.cur = st.clip;
    } else {
      this.src.update(dt, st);
      // retarget: mundo_menino = (mundo_xbot * pose_T_xbot^-1) * menino_em_T
      const D = new Map(), Delta = new Map();
      for (const m of this.map) { const d = this.src.worldQ(m.sb, new THREE.Quaternion()).multiply(m.srcTinv); Delta.set(m.n, d); }
      // coluna: espalha a torção/curva da cintura igualmente por Spine, Spine1 e Spine2
      // (concentrada numa junta só, a cintura do menino "estrangula")
      const dh = Delta.get('Hips'), d2 = Delta.get('Spine2');
      if (dh && d2 && Delta.get('Spine') && Delta.get('Spine1')) {
        const rel = dh.clone().invert().multiply(d2);
        // limita a torção total (em torno do eixo vertical do corpo)
        const ax = new THREE.Vector3(0, 1, 0), proj = ax.clone().multiplyScalar(ax.dot(new THREE.Vector3(rel.x, rel.y, rel.z)));
        const twist = new THREE.Quaternion(proj.x, proj.y, proj.z, rel.w).normalize();
        const swing = rel.clone().multiply(twist.clone().invert());
        const tAng = 2 * Math.acos(Math.min(1, Math.abs(twist.w))), maxT = 0.55;
        if (tAng > maxT) twist.slerp(new THREE.Quaternion(), 1 - maxT / tAng);
        const relC = swing.multiply(twist);
        const I = new THREE.Quaternion();
        Delta.set('Spine', dh.clone().multiply(I.clone().slerp(relC, 1 / 3)));
        Delta.set('Spine1', dh.clone().multiply(I.clone().slerp(relC, 2 / 3)));
        Delta.set('Spine2', dh.clone().multiply(relC));
      }
      const W = new Map();
      for (const m of this.map) {
        const desired = Delta.get(m.n).clone().multiply(m.tgtT);
        const parent = m.kb.parent;
        const pw = parent && W.has(parent) ? W.get(parent) : own.worldQ(parent, _q2);
        m.kb.quaternion.copy(pw.clone().invert().multiply(desired));
        W.set(m.kb, desired);
      }
      own.b.Hips.position.copy(this.hipRest); own.b.Hips.position.y += (this.src.bob || 0) * this.hipScale; // pulinho do quadril, na escala do menino
    }
    // suavização entre movimentos (vale para os dois caminhos)
    const k = this.first ? 1 : 1 - Math.exp(-dt * 16); this.first = false;
    own.bones.forEach((b, i) => { this.prev[i].slerp(b.quaternion, k); b.quaternion.copy(this.prev[i]); });
    own.root.updateMatrixWorld(true);
  }
  clipDur(n) { return this.src.clipDur(n); }
}
