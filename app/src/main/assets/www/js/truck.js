import * as THREE from 'three';
import { TRUCK_LAT } from './track.js';
import * as TX from './textures.js';

// Dimensões da carreta (em metros) depois de escalar o modelo do Meshy
export const TRUCK_LEN = 17;
const _c = new THREE.Color();

export class Truck {
  constructor(gltfScene) {
    this.root = new THREE.Group();        // segue a rua (frente = +Z local, esquerda = +X local)
    this.root.matrixAutoUpdate = false;
    const model = gltfScene.clone(true);
    const box = new THREE.Box3().setFromObject(model);
    const size = box.getSize(new THREE.Vector3());
    const S = TRUCK_LEN / size.x;
    this.S = S;
    model.scale.setScalar(S);
    // modelo original: cabine em -X. Giramos para a cabine ficar em +Z (frente).
    model.rotation.y = Math.PI / 2;
    model.position.y = -box.min.y * S;
    model.traverse(o => { if (o.isMesh) { o.frustumCulled = false; if (o.material) { o.material.envMapIntensity = 1; } } });
    this.root.add(model);
    this.model = model;
    this.height = size.y * S;
    this.roofY = this.height - 0.12;            // teto plano (palco)
    this.halfW = size.z * S / 2;
    this.roofZ = [-0.86 * S, 0.58 * S];         // trecho do teto do baú (traseira -> frente)
    this.s = 20; this.v = 0;
    this.lights = [];
    this.night = false;
    this.buildLights();
  }

  buildLights() {
    const R = this.root, H = this.roofY, W = this.halfW;
    // lâmpadas ao longo das bordas do teto e das colunas
    const bulbs = [];
    for (let z = this.roofZ[0] + 0.3; z <= this.roofZ[1] - 0.2; z += 0.45) {
      bulbs.push([W + 0.05, H + 0.08, z], [-W - 0.05, H + 0.08, z]);
      bulbs.push([W + 0.08, 1.05, z], [-W - 0.08, 1.05, z]);
    }
    for (let x = -W; x <= W; x += 0.45) bulbs.push([x, H + 0.08, this.roofZ[0]], [x, H + 0.08, this.roofZ[1]]);
    this.bulbData = bulbs;
    this.bulbs = new THREE.InstancedMesh(new THREE.SphereGeometry(0.09, 6, 4), new THREE.MeshBasicMaterial({ color: 0xffffff, toneMapped: false }), bulbs.length);
    bulbs.forEach((b, i) => { this.bulbs.setMatrixAt(i, new THREE.Matrix4().makeTranslation(b[0], b[1], b[2])); this.bulbs.setColorAt(i, _c.setHSL(i / bulbs.length, 1, 0.6)); });
    R.add(this.bulbs);

    // canhões de luz (cones aditivos) nos cantos do teto
    const beamTex = TX.beamTexture();
    this.beams = [];
    const corners = [[W - 0.2, this.roofZ[0] + 0.4], [-W + 0.2, this.roofZ[0] + 0.4], [W - 0.2, this.roofZ[1] - 0.3], [-W + 0.2, this.roofZ[1] - 0.3]];
    corners.forEach(([x, z], i) => {
      const holder = new THREE.Group(); holder.position.set(x, H + 0.35, z);
      const base = new THREE.Mesh(new THREE.CylinderGeometry(0.22, 0.28, 0.4, 8), new THREE.MeshStandardMaterial({ color: 0x222222, metalness: 0.6, roughness: 0.4 }));
      holder.add(base);
      const cone = new THREE.Mesh(new THREE.ConeGeometry(2.6, 26, 18, 1, true).translate(0, -13, 0),
        new THREE.MeshBasicMaterial({ map: beamTex, transparent: true, opacity: 0.55, blending: THREE.AdditiveBlending, depthWrite: false, side: THREE.DoubleSide, fog: false, toneMapped: false }));
      cone.rotation.x = Math.PI; // aponta pra cima
      const pivot = new THREE.Group(); pivot.add(cone); holder.add(pivot);
      R.add(holder);
      this.beams.push({ pivot, cone, i });
    });

    // lasers saindo das caixas de som traseiras
    this.lasers = new THREE.Group();
    const lg = new THREE.BoxGeometry(0.05, 0.05, 60).translate(0, 0, -30);
    for (let k = 0; k < 7; k++) {
      const m = new THREE.Mesh(lg, new THREE.MeshBasicMaterial({ color: k % 2 ? 0x00ff88 : 0xff00cc, transparent: true, opacity: 0.8, blending: THREE.AdditiveBlending, depthWrite: false, fog: false, toneMapped: false }));
      m.userData.k = k; this.lasers.add(m);
    }
    this.lasers.position.set(0, H + 0.5, this.roofZ[0] + 0.2);
    R.add(this.lasers);

    // globo espelhado no teto
    this.disco = new THREE.Mesh(new THREE.IcosahedronGeometry(0.45, 1), new THREE.MeshStandardMaterial({ color: 0xdddddd, metalness: 1, roughness: 0.15, flatShading: true, emissive: 0x333333 }));
    this.disco.position.set(0, H + 2.2, this.roofZ[0] + 2.5);
    const pole = new THREE.Mesh(new THREE.CylinderGeometry(0.04, 0.04, 2, 4), new THREE.MeshStandardMaterial({ color: 0x888888 }));
    pole.position.set(0, H + 1.1, this.roofZ[0] + 2.5);
    R.add(this.disco, pole);

    // faróis
    const head = new THREE.MeshBasicMaterial({ color: 0xfffbe8, toneMapped: false });
    for (const x of [-1.0, 1.0]) {
      const h = new THREE.Mesh(new THREE.CircleGeometry(0.22, 12), head);
      h.position.set(x, 0.95, TRUCK_LEN / 2 + 0.05); R.add(h);
    }
    this.headSpot = new THREE.SpotLight(0xfff1d0, 0, 60, 0.55, 0.6, 1.2);
    this.headSpot.position.set(0, 1.2, TRUCK_LEN / 2); this.headSpot.target.position.set(0, 0, TRUCK_LEN / 2 + 20);
    R.add(this.headSpot, this.headSpot.target);

    // luzes coloridas reais (iluminam os meninos e a rua)
    const cols = [0xff2d95, 0x00e5ff, 0xffd400];
    this.party = cols.map((c, i) => {
      const l = new THREE.PointLight(c, 0, 22, 1.6);
      l.position.set(i === 1 ? -W - 2.5 : W + 2.5, H + 1.5, -3 + i * 4);
      R.add(l); return l;
    });

    // sombra suave
    const sh = new THREE.Mesh(new THREE.PlaneGeometry(this.halfW * 2 + 1.2, TRUCK_LEN + 1).rotateX(-Math.PI / 2),
      new THREE.MeshBasicMaterial({ map: TX.blobShadow(), transparent: true, depthWrite: false, opacity: 0.9 }));
    sh.position.y = 0.04; sh.renderOrder = 1; R.add(sh);
  }

  setNight(n) {
    this.night = n;
    this.lasers.visible = n;
    this.beams.forEach(b => b.cone.visible = true);
    this.headSpot.intensity = n ? 60 : 0;
  }

  place(track) {
    const m = track.basis(this.s, true, this.root.matrix);
    const p = track.pos(this.s, TRUCK_LAT, 0);
    m.setPosition(p);
    this.root.matrixWorldNeedsUpdate = true;
    this.root.updateMatrixWorld(true);
  }

  // converte um ponto em coordenadas da carreta para o mundo
  toWorld(v, out = new THREE.Vector3()) { return out.copy(v).applyMatrix4(this.root.matrixWorld); }

  update(dt, t, beat, energy, party) {
    const n = this.night;
    const e = energy;
    // lâmpadas: perseguição de arco-íris no ritmo
    const N = this.bulbData.length;
    for (let i = 0; i < N; i++) {
      const hue = (i / 24 + beat * 0.25) % 1;
      const on = ((i + Math.floor(beat * 2)) % 4) !== 0 || !party;
      _c.setHSL(hue, 1, on ? (n ? 0.55 + e * 0.3 : 0.5) : 0.08);
      if (n) _c.multiplyScalar(1.6 + e);
      this.bulbs.setColorAt(i, _c);
    }
    this.bulbs.instanceColor.needsUpdate = true;
    // canhões varrendo o céu
    this.beams.forEach(b => {
      b.pivot.rotation.z = Math.sin(t * 0.8 + b.i * 1.6) * 0.55;
      b.pivot.rotation.x = Math.cos(t * 0.6 + b.i) * 0.45;
      _c.setHSL((t * 0.05 + b.i * 0.25) % 1, 1, 0.55);
      b.cone.material.color.copy(_c);
      b.cone.material.opacity = (n ? 0.32 : 0.08) + e * (n ? 0.4 : 0.1);
    });
    // lasers em leque
    if (n) this.lasers.children.forEach(l => {
      const k = l.userData.k;
      l.rotation.y = (k - 3) * 0.18 + Math.sin(t * 1.3) * 0.25;
      l.rotation.x = 0.25 + Math.sin(t * 0.9 + k) * 0.12;
      l.material.opacity = 0.35 + e * 0.6;
    });
    this.disco.rotation.y += dt * 1.5;
    this.party.forEach((l, i) => {
      const base = n ? 26 : 4;
      l.intensity = base * (0.45 + 0.55 * (i === Math.floor(beat) % 3 ? 1 : e));
    });
  }
}
