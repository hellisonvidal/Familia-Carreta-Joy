import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import { prune, resample, dedup, quantize, meshopt } from '@gltf-transform/functions';
import { MeshoptEncoder } from 'meshoptimizer';
await MeshoptEncoder.ready;
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS).registerDependencies({ 'meshopt.encoder': MeshoptEncoder });
const OUT = '/home/user/familia-carreta-joy/app/src/main/assets/www/anims';
const keep = { idle: 'idle', walking: 'andar', running: 'correr', jump: 'pulo', Backflip_1: 'mortal', Running_Forward_Flip: 'flip', Run_To_Flip: 'mortal2', Hit_On_The_Back: 'tombo', Fall_Flat: 'queda', left_strafe_walking: 'ladoE', right_strafe_walking: 'ladoD' };
for (const [src, name] of Object.entries(keep)) {
  const doc = await io.read(`/home/user/anim_in/glb/${src}.glb`);
  for (const an of doc.getRoot().listAnimations()) {
    an.setName(name);
    for (const ch of an.listChannels()) {
      const n = ch.getTargetNode().getName();
      if (/Hand(Thumb|Index|Middle|Ring|Pinky)|_End/.test(n)) { ch.getSampler().dispose(); ch.dispose(); }
    }
  }
  // tira a malha (só o esqueleto e a animação)
  for (const m of doc.getRoot().listMeshes()) m.dispose();
  for (const s of doc.getRoot().listSkins()) s.dispose();
  await doc.transform(resample({ tolerance: 0.0005 }), prune(), dedup());
  await io.write(`${OUT}/${name}.glb`, doc);
}
// boneco X Bot
const xb = await io.read('/home/user/anim_in/glb/X_Bot.glb');
for (const an of xb.getRoot().listAnimations()) an.dispose();
await xb.transform(prune(), dedup(), meshopt({ encoder: MeshoptEncoder, level: 'medium' }));
await io.write('/home/user/familia-carreta-joy/app/src/main/assets/www/models/xbot.glb', xb);
