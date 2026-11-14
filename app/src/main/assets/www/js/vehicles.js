import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';

// Carros com cantos arredondados, vários modelos. Frente = +Z.
const _c = new THREE.Color();
function col(geo, color) {
  const g = geo.index ? geo.toNonIndexed() : geo.clone();
  _c.set(color); const n = g.attributes.position.count, a = new Float32Array(n * 3);
  for (let i = 0; i < n; i++) a.set([_c.r, _c.g, _c.b], i * 3);
  g.setAttribute('color', new THREE.BufferAttribute(a, 3)); g.deleteAttribute('uv');
  return g;
}
const rb = (w, h, d, r, seg = 3) => new RoundedBoxGeometry(w, h, d, seg, r);
function wheel(x, y, z, r, w) {
  return [
    col(new THREE.CylinderGeometry(r, r, w, 16).rotateZ(Math.PI / 2).translate(x, y, z), 0x151515),
    col(new THREE.CylinderGeometry(r * 0.55, r * 0.55, w + 0.02, 12).rotateZ(Math.PI / 2).translate(x, y, z), 0xb0b6bc),
  ];
}
const GLASS = 0x223344, LIGHT = 0xfff3c4, TAIL = 0xd32f2f, BLACK = 0x1b1b1b;

export const VEHICLE_TYPES = ['sedan', 'hatch', 'suv', 'taxi', 'van', 'picape', 'fusca', 'sedan', 'hatch'];

// devolve { geo, len, w, h } (geometria com cores por vértice)
export function vehicleGeometry(type, color) {
  const P = [];
  let len = 4.3, w = 1.85, h = 1.45;
  const lights = (zf, zb, y, x) => {
    for (const s of [-1, 1]) {
      P.push(col(new THREE.SphereGeometry(0.13, 10, 8).scale(1.4, 0.8, 0.4).translate(s * x, y, zf), LIGHT));
      P.push(col(new THREE.BoxGeometry(0.34, 0.12, 0.06).translate(s * x, y + 0.05, zb), TAIL));
    }
  };
  if (type === 'fusca') {
    len = 4.0; w = 1.6; h = 1.5;
    P.push(col(new THREE.SphereGeometry(1, 24, 16).scale(0.82, 0.62, 2.0).translate(0, 0.62, 0), color));
    P.push(col(new THREE.SphereGeometry(1, 20, 14, 0, Math.PI * 2, 0, Math.PI / 2).scale(0.7, 0.62, 1.05).translate(0, 0.9, -0.15), color));
    P.push(col(new THREE.SphereGeometry(1, 20, 14, 0, Math.PI * 2, 0, Math.PI / 2).scale(0.66, 0.56, 0.98).translate(0, 0.92, -0.15), GLASS));
    for (const [x, z] of [[-0.72, 1.2], [0.72, 1.2], [-0.72, -1.25], [0.72, -1.25]]) { P.push(col(new THREE.SphereGeometry(0.42, 12, 8).scale(0.6, 0.85, 1).translate(x, 0.45, z), color)); P.push(...wheel(x, 0.32, z, 0.32, 0.22)); }
    lights(1.92, -1.95, 0.72, 0.5);
  } else if (type === 'onibus') {
    len = 11; w = 2.5; h = 3.1;
    P.push(col(rb(2.5, 2.6, 11, 0.3).translate(0, 1.75, 0), color));
    P.push(col(rb(2.54, 0.9, 10.2, 0.2).translate(0, 2.25, -0.2), GLASS));
    P.push(col(rb(2.3, 1.6, 0.1, 0.05).translate(0, 2.0, 5.5), GLASS));
    P.push(col(rb(2.52, 0.25, 11.02, 0.1).translate(0, 1.05, 0), 0xffffff));
    for (const [x, z] of [[-1.15, 3.6], [1.15, 3.6], [-1.15, -3.4], [1.15, -3.4]]) P.push(...wheel(x, 0.5, z, 0.5, 0.32));
    lights(5.5, -5.5, 0.9, 0.9);
  } else {
    const spec = {
      sedan: { L: 4.4, W: 1.85, bodyH: 0.62, cabL: 2.2, cabH: 0.55, cabZ: -0.2, r: 0.22 },
      hatch: { L: 3.9, W: 1.75, bodyH: 0.62, cabL: 2.2, cabH: 0.6, cabZ: -0.45, r: 0.24 },
      suv: { L: 4.6, W: 1.95, bodyH: 0.85, cabL: 2.8, cabH: 0.65, cabZ: -0.35, r: 0.26 },
      taxi: { L: 4.4, W: 1.85, bodyH: 0.62, cabL: 2.2, cabH: 0.55, cabZ: -0.2, r: 0.22 },
      van: { L: 4.8, W: 1.95, bodyH: 1.05, cabL: 3.9, cabH: 0.8, cabZ: -0.35, r: 0.28 },
      picape: { L: 5.0, W: 1.95, bodyH: 0.8, cabL: 1.7, cabH: 0.65, cabZ: 0.55, r: 0.22 },
    }[type] || {};
    const { L, W, bodyH, cabL, cabH, cabZ, r } = spec;
    len = L; w = W; h = 0.35 + bodyH + cabH;
    const by = 0.35 + bodyH / 2;
    P.push(col(rb(W, bodyH, L, r).translate(0, by, 0), color));
    P.push(col(rb(W * 0.92, cabH + 0.08, cabL, r * 0.9).translate(0, 0.35 + bodyH + cabH / 2 - 0.04, cabZ), color));
    P.push(col(rb(W * 0.935, cabH * 0.72, cabL * 0.96, r * 0.6).translate(0, 0.35 + bodyH + cabH * 0.42, cabZ), GLASS));
    P.push(col(rb(W + 0.04, 0.12, L + 0.04, 0.05).translate(0, 0.42, 0), BLACK)); // para-choque/saia
    const wz = L / 2 - 0.85, wr = type === 'suv' || type === 'picape' ? 0.4 : 0.34;
    for (const [x, z] of [[-W / 2 + 0.05, wz], [W / 2 - 0.05, wz], [-W / 2 + 0.05, -wz], [W / 2 - 0.05, -wz]]) P.push(...wheel(x, wr, z, wr, 0.26));
    lights(L / 2 + 0.01, -L / 2 - 0.01, by + 0.05, W / 2 - 0.35);
    if (type === 'taxi') { P.push(col(rb(0.6, 0.18, 0.3, 0.05).translate(0, h + 0.09, cabZ), 0xffffff)); }
    if (type === 'picape') { P.push(col(new THREE.BoxGeometry(W * 0.9, 0.08, 2.0).translate(0, 0.35 + bodyH + 0.04, -1.3), 0x333333)); }
  }
  return { geo: mergeGeometries(P), len, w, h };
}

export const CAR_COLORS = [0xe53935, 0x1e88e5, 0xfdd835, 0x43a047, 0xf5f5f5, 0x8e24aa, 0x212121, 0xff7043, 0x90a4ae, 0x00897b, 0x6d4c41];
export function pickVehicle(r) {
  const type = VEHICLE_TYPES[Math.floor(r() * VEHICLE_TYPES.length)];
  const color = type === 'taxi' ? 0xffc400 : CAR_COLORS[Math.floor(r() * CAR_COLORS.length)];
  return { type, color };
}
