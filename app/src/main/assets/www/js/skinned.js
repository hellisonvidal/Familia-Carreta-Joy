import * as THREE from 'three';

// =====================================================================
// Esqueleto automático para os modelos REAIS dos meninos (Meshy, em pé).
// Os modelos não vêm com ossos, então aqui criamos os ossos nas
// articulações e calculamos o "peso" de cada vértice (pele) pela
// posição: cabeça, tronco, braços e pernas. Assim o corpo de verdade,
// com a roupa de verdade, corre, pula e dança.
// Coordenadas abaixo em unidades do modelo original (altura ~1.9).
// =====================================================================
export const RIGS = {
  arthurBody: {
    neck: [0, 0.53, -0.02], top: 0.955, floor: -0.952,
    sh: [0.215, 0.405, -0.02], el: [0.255, 0.07, -0.02], wr: [0.27, -0.1, 0.0], handEnd: -0.2,
    waist: [0, -0.02, -0.02], hips: [0, -0.13, 0], th: [0.09, -0.16, 0], kn: [0.105, -0.52, 0], an: [0.16, -0.86, 0],
    crotch: -0.2, torsoX: 0.213, armBlend: [0.0, 0.012],
  },
  afonsoBody: {
    neck: [0, 0.53, -0.07], top: 0.951, floor: -0.952,
    sh: [0.235, 0.42, -0.06], el: [0.385, 0.06, -0.07], wr: [0.43, -0.12, -0.06], handEnd: -0.255,
    waist: [0, -0.02, -0.05], hips: [0, -0.14, -0.04], th: [0.11, -0.17, -0.03], kn: [0.15, -0.52, -0.02], an: [0.21, -0.86, -0.02],
    crotch: -0.19, torsoX: 0.205, armBlend: [0.012, 0.035],
  },
};

const ss = (a, b, x) => { const t = Math.max(0, Math.min(1, (x - a) / (b - a))); return t * t * (3 - 2 * t); };
function segT(p, a, b) { // posição relativa ao longo de um segmento (0..1, sem limitar)
  const abx = b[0] - a[0], aby = b[1] - a[1], abz = b[2] - a[2];
  return ((p[0] - a[0]) * abx + (p[1] - a[1]) * aby + (p[2] - a[2]) * abz) / (abx * abx + aby * aby + abz * abz);
}

function segDist(p, a, b) {
  const t = Math.max(0, Math.min(1, segT(p, a, b)));
  const x = a[0] + (b[0] - a[0]) * t - p[0], y = a[1] + (b[1] - a[1]) * t - p[1], z = a[2] + (b[2] - a[2]) * t - p[2];
  return Math.sqrt(x * x + y * y + z * z);
}
function floatAttr(attr) {
  const n = attr.count, k = attr.itemSize, out = new Float32Array(n * k);
  for (let i = 0; i < n; i++) for (let c = 0; c < k; c++) out[i * k + c] = attr.getComponent(i, c);
  return new THREE.BufferAttribute(out, k);
}

export const BONE_NAMES = ['hips', 'spine', 'neck', 'head', 'shL', 'elL', 'shR', 'elR', 'thL', 'knL', 'thR', 'knR'];
export const BONE_PARENT = { spine: 'hips', neck: 'spine', head: 'neck', shL: 'spine', elL: 'shL', shR: 'spine', elR: 'shR', thL: 'hips', knL: 'thL', thR: 'hips', knR: 'thR' };
export function makeBones(at) {
  const bones = {}, list = [];
  for (const n of BONE_NAMES) {
    const b = new THREE.Bone(); b.name = n; bones[n] = b; list.push(b);
    const p = at[n], pp = BONE_PARENT[n] ? at[BONE_PARENT[n]] : [0, 0, 0];
    b.position.set(p[0] - pp[0], p[1] - pp[1], p[2] - pp[2]);
    if (BONE_PARENT[n]) bones[BONE_PARENT[n]].add(b);
  }
  return { bones, list, index: Object.fromEntries(BONE_NAMES.map((n, i) => [n, i])), rest: Object.fromEntries(BONE_NAMES.map(n => [n, bones[n].position.clone()])) };
}
// aplica uma pose (ângulos das articulações) nos ossos
export function applyPose(J, R, p, armRest, legRest, limitArms) {
  J.hips.position.set(R.hips.x, R.hips.y + (p.bob || 0), R.hips.z);
  J.hips.rotation.set(0, p.twist || 0, p.sway || 0);
  J.spine.rotation.set(p.lean || 0, p.spineTwist || 0, p.spineSway || 0);
  J.head.rotation.set(p.head ? p.head[0] : 0, p.head ? p.head[1] : 0, p.head ? p.head[2] || 0 : 0);
  const cx = limitArms ? (v) => Math.max(-1.75, Math.min(1.3, v)) : (v) => v;
  const cz = limitArms ? (v) => Math.max(-0.25, Math.min(1.35, v)) : (v) => v;
  J.shL.rotation.set(cx(p.sh[0]), 0, cz(p.sh[1]) - armRest); J.shR.rotation.set(cx(p.sh[2]), 0, -cz(-p.sh[3]) + armRest);
  J.elL.rotation.set(p.el[0], 0, 0); J.elR.rotation.set(p.el[1], 0, 0);
  J.thL.rotation.set(p.th[0], 0, p.th[1] - legRest); J.thR.rotation.set(p.th[2], 0, p.th[3] + legRest);
  J.knL.rotation.set(p.kn[0], 0, 0); J.knR.rotation.set(p.kn[1], 0, 0);
}

export class SkinnedBody {
  constructor(src, rigKey, altura) {
    const R = RIGS[rigKey];
    this.root = new THREE.Group();
    this.pivot = new THREE.Group(); this.root.add(this.pivot);
    this.rig = new THREE.Group(); this.pivot.add(this.rig);
    // pega a malha do GLB e "assa" as transformações em Float32
    let srcMesh = null; src.updateMatrixWorld(true); src.traverse(o => { if (o.isMesh && !srcMesh) srcMesh = o; });
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', floatAttr(srcMesh.geometry.attributes.position));
    g.setAttribute('normal', floatAttr(srcMesh.geometry.attributes.normal));
    g.setAttribute('uv', floatAttr(srcMesh.geometry.attributes.uv));
    g.setIndex(srcMesh.geometry.index.clone());
    g.applyMatrix4(srcMesh.matrixWorld);
    // unidades do modelo -> metros, pés no chão
    const S = altura / (R.top - R.floor);
    const toM = (v) => [v[0] * S, (v[1] - R.floor) * S, v[2] * S];
    const pos = g.attributes.position;
    for (let i = 0; i < pos.count; i++) pos.setXYZ(i, pos.getX(i) * S, (pos.getY(i) - R.floor) * S, pos.getZ(i) * S);
    g.computeBoundingSphere();
    const J = {
      hips: toM(R.hips), waist: toM(R.waist), neck: toM(R.neck),
      shL: toM(R.sh), elL: toM(R.el), wrL: toM(R.wr), thL: toM(R.th), knL: toM(R.kn), anL: toM(R.an),
    };
    const mir = (v) => [-v[0], v[1], v[2]];
    Object.assign(J, { shR: mir(J.shL), elR: mir(J.elL), wrR: mir(J.wrL), thR: mir(J.thL), knR: mir(J.knL), anR: mir(J.anL) });
    const handEnd = (R.handEnd - R.floor) * S, crotch = (R.crotch - R.floor) * S, torsoX = R.torsoX * S;
    this.hipY = J.hips[1];

    // ossos (mesmos nomes do corpo articulado => as mesmas poses servem)
    const names = ['hips', 'spine', 'neck', 'head', 'shL', 'elL', 'shR', 'elR', 'thL', 'knL', 'thR', 'knR'];
    const at = { hips: J.hips, spine: J.waist, neck: J.neck, head: [0, J.neck[1] + 0.03 * altura, J.neck[2]], shL: J.shL, elL: J.elL, shR: J.shR, elR: J.elR, thL: J.thL, knL: J.knL, thR: J.thR, knR: J.knR };
    const parent = { spine: 'hips', neck: 'spine', head: 'neck', shL: 'spine', elL: 'shL', shR: 'spine', elR: 'shR', thL: 'hips', knL: 'thL', thR: 'hips', knR: 'thR' };
    const bones = {}, list = [];
    for (const n of names) {
      const b = new THREE.Bone(); b.name = n; bones[n] = b; list.push(b);
      const p = at[n], pp = parent[n] ? at[parent[n]] : [0, 0, 0];
      b.position.set(p[0] - pp[0], p[1] - pp[1], p[2] - pp[2]);
      if (parent[n]) bones[parent[n]].add(b);
    }
    const BI = Object.fromEntries(names.map((n, i) => [n, i]));
    this.rest = Object.fromEntries(names.map(n => [n, bones[n].position.clone()]));
    // ângulo de repouso dos braços/pernas (o modelo não está com os braços retos pra baixo)
    this.armRest = Math.atan2(J.elL[0] - J.shL[0], J.shL[1] - J.elL[1]);
    this.legRest = Math.atan2(J.knL[0] - J.thL[0], J.thL[1] - J.knL[1]);

    // pesos da pele (até 3 ossos por vértice, com transições suaves)
    const n = pos.count, si = new Uint16Array(n * 4), sw = new Float32Array(n * 4);
    const A = altura, p = [0, 0, 0];
    for (let i = 0; i < n; i++) {
      p[0] = pos.getX(i); p[1] = pos.getY(i); p[2] = pos.getZ(i);
      const side = p[0] >= 0 ? 'L' : 'R', ax = Math.abs(p[0]);
      const w = {};
      const add = (b, v) => { if (v > 0.001) w[b] = (w[b] || 0) + v; };
      const sh = J['sh' + side], el = J['el' + side];
      // tronco (quadril -> peito -> cabeça)
      const torso = (k) => {
        const hy = ss(J.neck[1] - 0.015 * A, J.neck[1] + 0.035 * A, p[1]);
        const sy = ss(J.waist[1] - 0.07 * A, J.waist[1] + 0.07 * A, p[1]);
        add('head', k * hy); add('spine', k * (1 - hy) * sy); add('hips', k * (1 - hy) * (1 - sy));
      };
      // braço
      let armK = 0;
      if (p[1] > handEnd - 0.02 * A && p[1] < sh[1] + 0.07 * A) {
        armK = ss(torsoX - R.armBlend[0] * A, torsoX + R.armBlend[1] * A, ax);
        const tU = segT(p, sh, el);
        if (tU < 0.25) armK *= ss(-0.15, 0.25, tU); // ombro/axila: mistura com o peito
        // só segue o braço quem está perto do braço (mão encostada na calça não puxa a calça)
        const wr = J['wr' + side], hx = wr[0] + (wr[0] - el[0]) * 0.5, hy = handEnd, hz = wr[2];
        const dArm = Math.min(segDist(p, sh, el), segDist(p, el, wr), segDist(p, wr, [hx, hy, hz]));
        if (R.gate && p[1] < sh[1] - 0.1 * A && p[1] > handEnd + 0.02 * A) armK *= ss(R.gate * A, R.gate * 0.7 * A, dArm);
      }
      // abaixo do cotovelo o braço é "tudo ou nada" (mãos encostadas no corpo não esticam a calça)
      if (p[1] < J.elL[1]) armK = armK >= 0.5 ? 1 : 0;
      // perna
      let legK = 0;
      if (p[1] < crotch + 0.06 * A) legK = ss(crotch + 0.06 * A, crotch - 0.07 * A, p[1]) * (1 - armK);
      const torsoK = Math.max(0, 1 - armK - legK);
      torso(torsoK);
      if (armK > 0) { const tF = segT(p, el, J['wr' + side]); const f = ss(-0.18, 0.18, tF); add('sh' + side, armK * (1 - f)); add('el' + side, armK * f); }
      if (legK > 0) { const tS = segT(p, J['kn' + side], J['an' + side]); const f = ss(-0.14, 0.14, tS); add('th' + side, legK * (1 - f)); add('kn' + side, legK * f); }
      const ent = Object.entries(w).sort((a, b) => b[1] - a[1]).slice(0, 4);
      const tot = ent.reduce((a, e) => a + e[1], 0) || 1;
      ent.forEach(([b, v], k) => { si[i * 4 + k] = BI[b]; sw[i * 4 + k] = v / tot; });
      if (!ent.length) { si[i * 4] = BI.spine; sw[i * 4] = 1; }
    }
    // corta triângulos que "grudam" a mão/braço no corpo (abaixo do cotovelo)
    const cat = (v) => { const b = names[si[v * 4]]; return b.startsWith('sh') || b.startsWith('el') ? b.slice(2) : b.startsWith('th') || b.startsWith('kn') ? 'leg' : 'torso'; };
    const idx = g.index.array, keep = [], elY = J.elL[1];
    for (let t = 0; t < idx.length; t += 3) {
      const a = idx[t], b = idx[t + 1], c = idx[t + 2];
      const ca = cat(a), cb = cat(b), cc = cat(c);
      const mixed = (ca !== cb || cb !== cc) && (ca === 'L' || ca === 'R' || cb === 'L' || cb === 'R' || cc === 'L' || cc === 'R');
      if (mixed && (pos.getY(a) + pos.getY(b) + pos.getY(c)) / 3 < elY) continue;
      keep.push(a, b, c);
    }
    g.setIndex(keep);
    g.setAttribute('skinIndex', new THREE.Uint16BufferAttribute(si, 4));
    g.setAttribute('skinWeight', new THREE.Float32BufferAttribute(sw, 4));

    const mat = srcMesh.material.clone();
    mat.metalness = 0; mat.metalnessMap = null; mat.roughness = 0.8; mat.envMapIntensity = 0.8;
    const mesh = new THREE.SkinnedMesh(g, mat);
    mesh.frustumCulled = false;
    mesh.add(bones.hips);
    mesh.position.y = -this.hipY;
    this.rig.add(mesh);
    this.pivot.position.y = this.hipY;
    this.root.updateMatrixWorld(true);
    mesh.bind(new THREE.Skeleton(list));
    this.mesh = mesh;
    this.j = bones;
  }

  apply(p) { applyPose(this.j, this.rest, p, this.armRest, this.legRest * 0.6, true); }
}
