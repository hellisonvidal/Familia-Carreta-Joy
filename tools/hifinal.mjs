import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS, EXTMeshoptCompression } from '@gltf-transform/extensions';
import { prune, dedup, weld, simplify, textureCompress, reorder, quantize } from '@gltf-transform/functions';
import { MeshoptSimplifier, MeshoptEncoder } from 'meshoptimizer';
import sharp from 'sharp';
await MeshoptSimplifier.ready; await MeshoptEncoder.ready;
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS).registerDependencies({ 'meshopt.encoder': MeshoptEncoder });
const W = '/home/user/familia-carreta-joy/app/src/main/assets/www/models';
async function body(src, out, ratio) {
  const d = await io.read(src);
  for (const a of d.getRoot().listAnimations()) a.dispose();
  for (const m of d.getRoot().listMaterials()) { m.setMetallicRoughnessTexture(null); m.setMetallicFactor(0); m.setRoughnessFactor(0.88); m.setNormalScale(0.85); }
  const steps = [weld()];
  if (ratio < 1) steps.push(simplify({ simplifier: MeshoptSimplifier, ratio, error: 0.0004 }));
  steps.push(prune(), dedup(),
    textureCompress({ encoder: sharp, targetFormat: 'webp', resize: [2048, 2048], quality: 96, slots: /baseColor/ }),
    textureCompress({ encoder: sharp, targetFormat: 'webp', resize: [2048, 2048], quality: 90, slots: /normal/ }),
    reorder({ encoder: MeshoptEncoder }),
    // precisão alta: posição 16 bits, normal 12 bits, UV 14 bits, pesos 16 bits
    quantize({ quantizePosition: 16, quantizeNormal: 12, quantizeTexcoord: 14, quantizeWeight: 16, quantizeGeneric: 12 }));
  await d.transform(...steps);
  d.createExtension(EXTMeshoptCompression).setRequired(true).setEncoderOptions({ method: EXTMeshoptCompression.EncoderMethod.QUANTIZE });
  await io.write(out, d);
  const p = d.getRoot().listMeshes()[0].listPrimitives()[0];
  console.log(out.split('/').pop(), 'tris', p.getIndices().getCount() / 3);
}
await body('/home/user/rig_in/out/arthur_rig.glb', `${W}/ArthurRig.glb`, 1);
await body('/home/user/rig_in/out/afonso_hi.glb', `${W}/AfonsoRig.glb`, 1);
