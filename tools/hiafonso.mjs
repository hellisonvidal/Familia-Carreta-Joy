// Afonso de alta qualidade: malha/textura do Afonso2 + esqueleto e pesos do modelo com esqueleto do Meshy
import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { weld, simplify, prune, dedup } from '@gltf-transform/functions';
import { MeshoptSimplifier } from 'meshoptimizer';
await MeshoptSimplifier.ready;
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS);
const TARGET = +(process.env.TRIS || 90000);
// 1) malha boa, simplificada
const hi = await io.read('/home/user/conv/out/Afonso2.glb');
const tri0 = hi.getRoot().listMeshes()[0].listPrimitives()[0].getIndices().getCount() / 3;
await hi.transform(weld(), simplify({ simplifier: MeshoptSimplifier, ratio: TARGET / tri0, error: 0.0004, lockBorder: false }));
const hp = hi.getRoot().listMeshes()[0].listPrimitives()[0];
const HP = hp.getAttribute('POSITION').getArray(), HN = hp.getAttribute('NORMAL').getArray(), HUV = hp.getAttribute('TEXCOORD_0').getArray(), HI = hp.getIndices().getArray();
const nh = HP.length / 3;
console.log('alta: tris', HI.length / 3, 'verts', nh);
// 2) modelo com esqueleto
const lo = await io.read('/home/user/rig_in/out/afonso_rig.glb');
const lp = lo.getRoot().listMeshes()[0].listPrimitives()[0];
const LP = lp.getAttribute('POSITION').getArray(), LN = lp.getAttribute('NORMAL').getArray(), LI = lp.getIndices().getArray();
const LJ = lp.getAttribute('JOINTS_0').getArray(), LW = lp.getAttribute('WEIGHTS_0').getArray();
// caixas para alinhar (Afonso2 está girado -90° em X: (x,y,z) -> (x, z, -y))
const bb = (P, f) => { const mn = [1e9, 1e9, 1e9], mx = [-1e9, -1e9, -1e9]; for (let i = 0; i < P.length; i += 3) { const v = f(P[i], P[i + 1], P[i + 2]); for (let k = 0; k < 3; k++) { mn[k] = Math.min(mn[k], v[k]); mx[k] = Math.max(mx[k], v[k]); } } return [mn, mx]; };
const rot = (x, y, z) => [x, z, -y];
const [hmn, hmx] = bb(HP, rot), [lmn, lmx] = bb(LP, (x, y, z) => [x, y, z]);
const sc = (lmx[1] - lmn[1]) / (hmx[1] - hmn[1]);
const hc = [(hmn[0] + hmx[0]) / 2, hmn[1], (hmn[2] + hmx[2]) / 2], lc = [(lmn[0] + lmx[0]) / 2, lmn[1], (lmn[2] + lmx[2]) / 2];
console.log('escala', sc, 'larguras', (hmx[0] - hmn[0]) * sc, lmx[0] - lmn[0], 'prof', (hmx[2] - hmn[2]) * sc, lmx[2] - lmn[2]);
const toLo = (x, y, z) => { const r = rot(x, y, z); return [(r[0] - hc[0]) * sc + lc[0], (r[1] - hc[1]) * sc + lc[1], (r[2] - hc[2]) * sc + lc[2]]; };
// 3) grade espacial dos triângulos da malha com esqueleto
const nt = LI.length / 3, cell = (lmx[1] - lmn[1]) / 60, grid = new Map();
const key = (x, y, z) => `${Math.floor(x / cell)},${Math.floor(y / cell)},${Math.floor(z / cell)}`;
for (let t = 0; t < nt; t++) {
  const a = LI[t * 3], b = LI[t * 3 + 1], c = LI[t * 3 + 2];
  const xs = [LP[a * 3], LP[b * 3], LP[c * 3]], ys = [LP[a * 3 + 1], LP[b * 3 + 1], LP[c * 3 + 1]], zs = [LP[a * 3 + 2], LP[b * 3 + 2], LP[c * 3 + 2]];
  for (let i = Math.floor(Math.min(...xs) / cell); i <= Math.floor(Math.max(...xs) / cell); i++)
    for (let j = Math.floor(Math.min(...ys) / cell); j <= Math.floor(Math.max(...ys) / cell); j++)
      for (let k = Math.floor(Math.min(...zs) / cell); k <= Math.floor(Math.max(...zs) / cell); k++) { const K = `${i},${j},${k}`; if (!grid.has(K)) grid.set(K, []); grid.get(K).push(t); }
}
// ponto mais próximo num triângulo (Ericson)
function closest(p, a, b, c) {
  const sub = (u, v) => [u[0] - v[0], u[1] - v[1], u[2] - v[2]], dot = (u, v) => u[0] * v[0] + u[1] * v[1] + u[2] * v[2];
  const ab = sub(b, a), ac = sub(c, a), ap = sub(p, a), d1 = dot(ab, ap), d2 = dot(ac, ap);
  if (d1 <= 0 && d2 <= 0) return [1, 0, 0];
  const bp = sub(p, b), d3 = dot(ab, bp), d4 = dot(ac, bp); if (d3 >= 0 && d4 <= d3) return [0, 1, 0];
  const vc = d1 * d4 - d3 * d2; if (vc <= 0 && d1 >= 0 && d3 <= 0) { const v = d1 / (d1 - d3); return [1 - v, v, 0]; }
  const cp = sub(p, c), d5 = dot(ab, cp), d6 = dot(ac, cp); if (d6 >= 0 && d5 <= d6) return [0, 0, 1];
  const vb = d5 * d2 - d1 * d6; if (vb <= 0 && d2 >= 0 && d6 <= 0) { const w = d2 / (d2 - d6); return [1 - w, 0, w]; }
  const va = d3 * d6 - d5 * d4; if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) { const w = (d4 - d3) / ((d4 - d3) + (d5 - d6)); return [0, 1 - w, w]; }
  const den = 1 / (va + vb + vc), v = vb * den, w = vc * den; return [1 - v - w, v, w];
}
const V = (A, i) => [A[i * 3], A[i * 3 + 1], A[i * 3 + 2]];
const outP = new Float32Array(nh * 3), outN = new Float32Array(nh * 3), outJ = new Uint16Array(nh * 4), outW = new Float32Array(nh * 4);
let far = 0;
for (let i = 0; i < nh; i++) {
  const p = toLo(HP[i * 3], HP[i * 3 + 1], HP[i * 3 + 2]); outP.set(p, i * 3);
  const n = rot(HN[i * 3], HN[i * 3 + 1], HN[i * 3 + 2]); outN.set(n, i * 3);
  let best = null, bd = 1e18;
  for (let r = 1; r <= 4 && (!best || r <= 2); r++) {
    const cx = Math.floor(p[0] / cell), cy = Math.floor(p[1] / cell), cz = Math.floor(p[2] / cell);
    for (let dx = -r; dx <= r; dx++) for (let dy = -r; dy <= r; dy++) for (let dz = -r; dz <= r; dz++) {
      const L = grid.get(`${cx + dx},${cy + dy},${cz + dz}`); if (!L) continue;
      for (const t of L) {
        const a = LI[t * 3], b = LI[t * 3 + 1], c = LI[t * 3 + 2];
        const bc = closest(p, V(LP, a), V(LP, b), V(LP, c));
        const q = [0, 1, 2].map(k => bc[0] * LP[a * 3 + k] + bc[1] * LP[b * 3 + k] + bc[2] * LP[c * 3 + k]);
        let d = (q[0] - p[0]) ** 2 + (q[1] - p[1]) ** 2 + (q[2] - p[2]) ** 2;
        // prefere superfície virada para o mesmo lado (evita pegar a outra perna/braço)
        const tn = [0, 1, 2].map(k => LN[a * 3 + k] + LN[b * 3 + k] + LN[c * 3 + k]);
        if (tn[0] * n[0] + tn[1] * n[1] + tn[2] * n[2] < 0) d *= 4;
        if (d < bd) { bd = d; best = { a, b, c, bc }; }
      }
    }
  }
  if (!best) { far++; continue; }
  const w = new Map();
  for (const [vi, bw] of [[best.a, best.bc[0]], [best.b, best.bc[1]], [best.c, best.bc[2]]]) for (let k = 0; k < 4; k++) { const j = LJ[vi * 4 + k], ww = LW[vi * 4 + k] * bw; if (ww > 0) w.set(j, (w.get(j) || 0) + ww); }
  const top = [...w.entries()].sort((x, y) => y[1] - x[1]).slice(0, 4), tot = top.reduce((s, e) => s + e[1], 0) || 1;
  top.forEach(([j, ww], k) => { outJ[i * 4 + k] = j; outW[i * 4 + k] = ww / tot; });
}
console.log('sem par:', far);
// 4) grava no documento do modelo com esqueleto, trocando a malha e a textura
const acc = (arr, type) => lo.createAccessor().setType(type).setArray(arr).setBuffer(lo.getRoot().listBuffers()[0]);
lp.setAttribute('POSITION', acc(outP, 'VEC3')).setAttribute('NORMAL', acc(outN, 'VEC3')).setAttribute('TEXCOORD_0', acc(new Float32Array(HUV), 'VEC2'))
  .setAttribute('JOINTS_0', acc(outJ, 'VEC4')).setAttribute('WEIGHTS_0', acc(outW, 'VEC4')).setIndices(acc(new Uint32Array(HI), 'SCALAR'));
const hm = hp.getMaterial(), lm = lp.getMaterial();
const copyTex = (t) => { if (!t) return null; return lo.createTexture(t.getName()).setImage(t.getImage()).setMimeType(t.getMimeType()); };
lm.setBaseColorTexture(copyTex(hm.getBaseColorTexture())).setNormalTexture(copyTex(hm.getNormalTexture())).setMetallicRoughnessTexture(null);
for (const a of lo.getRoot().listAnimations()) a.dispose();
await lo.transform(prune(), dedup());
await io.write('/home/user/rig_in/out/afonso_hi.glb', lo);
