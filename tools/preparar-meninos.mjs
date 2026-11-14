import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { prune, dedup, weld, simplify, textureCompress, meshopt, resample } from '@gltf-transform/functions';
import { MeshoptSimplifier, MeshoptEncoder } from 'meshoptimizer';
import sharp from 'sharp';
await MeshoptSimplifier.ready; await MeshoptEncoder.ready;
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS).registerDependencies({ 'meshopt.encoder': MeshoptEncoder });
const W = '/home/user/familia-carreta-joy/app/src/main/assets/www';
async function body(src, out, ratio) {
  const d = await io.read(src);
  for (const a of d.getRoot().listAnimations()) a.dispose();
  // tecido fosco: sem mapa de metal/rugosidade (era o que fazia manchas brancas de brilho)
  for (const m of d.getRoot().listMaterials()) { m.setMetallicRoughnessTexture(null); m.setMetallicFactor(0); m.setRoughnessFactor(0.95); m.setNormalScale(0.5); }
  await d.transform(weld(), simplify({ simplifier: MeshoptSimplifier, ratio, error: 0.002 }), prune(), dedup(),
    textureCompress({ encoder: sharp, targetFormat: 'webp', resize: [2048, 2048], quality: 90, slots: /baseColor/ }),
    textureCompress({ encoder: sharp, targetFormat: 'webp', resize: [1024, 1024], quality: 85, slots: /normal/ }),
    meshopt({ encoder: MeshoptEncoder, level: 'medium' }));
  await io.write(out, d);
}
async function anims(src, map) {
  for (const [from, to] of Object.entries(map)) {
    const d = await io.read(src);
    for (const a of d.getRoot().listAnimations()) { if (a.getName() !== from) { a.dispose(); continue; } a.setName(to);
      for (const ch of a.listChannels()) if (/Hand(Thumb|Index|Middle|Ring|Pinky)/.test(ch.getTargetNode().getName())) { ch.getSampler().dispose(); ch.dispose(); } }
    for (const m of d.getRoot().listMeshes()) m.dispose(); for (const s of d.getRoot().listSkins()) s.dispose();
    await d.transform(resample({ tolerance: 0.0005 }), prune(), dedup());
    await io.write(`${W}/anims/${to}.glb`, d);
  }
}
await body('/home/user/rig_in/out/arthur_rig.glb', `${W}/models/ArthurRig.glb`, 0.4);
await body('/home/user/rig_in/out/afonso_rig.glb', `${W}/models/AfonsoRig.glb`, 1.0);
// (animações já preparadas)
if (process.env.ANIMS) await anims('/home/user/rig_in/out/afonso_rig.glb', { Gangnam_Groove: 'danca' });
if (process.env.ANIMS) await anims('/home/user/rig_in/out/arthur_anim.glb', { Running: 'correrK', Walking: 'andarK' });
