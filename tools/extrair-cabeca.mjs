import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { prune, dedup, simplify, weld, textureCompress, quantize, meshopt, resample } from '@gltf-transform/functions';
import { MeshoptSimplifier, MeshoptEncoder } from 'meshoptimizer';
import sharp from 'sharp';
const [,, inp, out, kind] = process.argv;
await MeshoptSimplifier.ready; await MeshoptEncoder.ready;
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS).registerDependencies({ 'meshopt.encoder': MeshoptEncoder });
const doc = await io.read(inp);
const inside = (kind === 'arthur' || kind === 'afonso2')
  ? (x, y, z) => y > (kind === 'afonso2' ? 0.535 : 0.548)
  : (x, y, z) => { const dx = (x + 0.52) / 0.3, dy = (y - 0.5) / 0.34, dz = (z - 0.36) / 0.34; return dx*dx + dy*dy + dz*dz < 1 && y > 0.195 && !(x > -0.3 && y < 0.47); };
let uvmin=[1,1], uvmax=[0,0];
for (const mesh of doc.getRoot().listMeshes()) for (const prim of mesh.listPrimitives()) {
  const pos = prim.getAttribute('POSITION'), idx = prim.getIndices(), uv = prim.getAttribute('TEXCOORD_0');
  const I = idx.getArray(), keep = [];
  const p = [0,0,0];
  const ok = (i) => { pos.getElement(i, p); return inside(100*p[0], 100*p[2], -100*p[1]); };
  for (let t = 0; t < I.length; t += 3) if (ok(I[t]) && ok(I[t+1]) && ok(I[t+2])) {
    keep.push(I[t], I[t+1], I[t+2]);
    for (const k of [I[t],I[t+1],I[t+2]]) { const u=uv.getElement(k,[0,0]); uvmin=[Math.min(uvmin[0],u[0]),Math.min(uvmin[1],u[1])]; uvmax=[Math.max(uvmax[0],u[0]),Math.max(uvmax[1],u[1])]; }
  }
  // remap compacto
  const map = new Map(), order = [];
  const ni = keep.map(i => { if (!map.has(i)) { map.set(i, order.length); order.push(i); } return map.get(i); });
  for (const sem of prim.listSemantics()) {
    const a = prim.getAttribute(sem), n = a.getElementSize(), src = a.getArray();
    const dst = new src.constructor(order.length * n);
    order.forEach((oi, j) => { for (let c = 0; c < n; c++) dst[j*n+c] = src[oi*n+c]; });
    a.setArray(dst);
  }
  idx.setArray(new Uint32Array(ni));
  console.log('tris', ni.length/3, 'verts', order.length);
}
console.log('uv bbox', uvmin, uvmax);
await doc.transform(weld(), simplify({ simplifier: MeshoptSimplifier, ratio: Number(process.env.R||0.04), error: 0.001 }), prune(), dedup(),
  textureCompress({ encoder: sharp, targetFormat: 'webp', resize: [2048, 2048], slots: /baseColor/ }),
  textureCompress({ encoder: sharp, targetFormat: 'webp', resize: [1024, 1024], slots: /normal|metallic/ }),
  quantize(), meshopt({ encoder: MeshoptEncoder }));
await io.write(out, doc);
