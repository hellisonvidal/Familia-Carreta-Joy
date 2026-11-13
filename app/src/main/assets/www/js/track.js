import * as THREE from 'three';
import { rng, pick, range } from './cities.js';

// Geometria da rua: tudo no jogo é posicionado por (s = distância ao longo da rua, lat = deslocamento lateral).
// lat > 0 = lado direito.
export const ROAD_HALF = 9;       // asfalto de -9 a +9
export const SIDEWALK = 12;        // calçada até ±12
export const LANES = [0, 3, 6];    // faixas onde os meninos correm
export const TRUCK_LAT = -3.5;     // faixa da carreta

export class Track {
  constructor(city) {
    this.city = city;
    const r = rng(city.seed);
    const L = city.comprimento;
    this.length = L;
    this.praca = Math.round(L * 0.52);   // praça da dança
    const N = L + 1;
    this.x = new Float32Array(N); this.z = new Float32Array(N); this.y = new Float32Array(N);
    this.hd = new Float32Array(N); this.gr = new Float32Array(N);

    // segmentos de inclinação e curvatura (retas, subidas, descidas, curvas)
    const seg = (list, flatZones) => {
      const out = new Float32Array(N);
      let s = 0, prev = 0;
      while (s < N) {
        const len = Math.round(range(r, 70, 190));
        let val = pick(r, list);
        if (flatZones.some(([a, b]) => s < b && s + len > a)) val = 0;
        for (let i = 0; i < len && s + i < N; i++) {
          const k = Math.min(1, i / 35);
          const e = k * k * (3 - 2 * k);
          out[s + i] = prev + (val - prev) * e;
        }
        prev = val; s += len;
      }
      return out;
    };
    const flat = [[0, 80], [this.praca - 70, this.praca + 70], [L - 60, L + 10]];
    const grade = seg(city.rampas, flat);
    const curv = seg(city.curvas, flat);
    let h = 0, hx = 0, x = 0, z = 0, y = 0;
    for (let i = 0; i < N; i++) {
      this.x[i] = x; this.z[i] = z; this.y[i] = y; this.hd[i] = h; this.gr[i] = grade[i];
      h += curv[i]; x += Math.sin(h); z += Math.cos(h); y += grade[i];
    }
    this.minY = Math.min(...this.y); this.maxY = Math.max(...this.y);
  }

  clampS(s) { return Math.max(0, Math.min(this.length - 0.001, s)); }

  // Retorna posição no mundo + vetores (frente, direita) e inclinação.
  frame(s, out = {}) {
    s = this.clampS(s);
    const i = Math.floor(s), f = s - i, j = Math.min(i + 1, this.length);
    const x = this.x[i] + (this.x[j] - this.x[i]) * f;
    const z = this.z[i] + (this.z[j] - this.z[i]) * f;
    const y = this.y[i] + (this.y[j] - this.y[i]) * f;
    const h = this.hd[i] + (this.hd[j] - this.hd[i]) * f;
    const g = this.gr[i] + (this.gr[j] - this.gr[i]) * f;
    out.x = x; out.y = y; out.z = z; out.heading = h; out.grade = g;
    out.fx = Math.sin(h); out.fz = Math.cos(h);
    out.rx = -out.fz; out.rz = out.fx; // direita
    return out;
  }

  pos(s, lat, up = 0, v = new THREE.Vector3()) {
    const f = this.frame(s, _f);
    return v.set(f.x + f.rx * lat, f.y + up, f.z + f.rz * lat);
  }

  // Matriz de orientação (frente = +Z local, para cima = +Y) com inclinação da ladeira
  basis(s, pitch = true, m = new THREE.Matrix4()) {
    const f = this.frame(s, _f);
    const fwd = new THREE.Vector3(f.fx, pitch ? f.grade : 0, f.fz).normalize();
    const left = new THREE.Vector3().crossVectors(_up, fwd).normalize();
    const up = new THREE.Vector3().crossVectors(fwd, left).normalize();
    return m.makeBasis(left, up, fwd);
  }

  // Constrói uma faixa (ribbon) seguindo a rua entre duas posições laterais.
  ribbon(latA, latB, opts = {}) {
    const step = opts.step || 2;
    const yA = opts.yA || 0, yB = opts.yB !== undefined ? opts.yB : yA;
    const uScale = opts.uScale || 1, vScale = opts.vScale || 1;
    const from = opts.from || 0, to = opts.to !== undefined ? opts.to : this.length;
    const pos = [], uv = [], idx = [];
    let row = 0;
    for (let s = from; s <= to + 0.01; s += step) {
      const ss = Math.min(s, to);
      const f = this.frame(ss, _f);
      const yb = typeof opts.yFn === 'function' ? opts.yFn(ss) : 0;
      pos.push(f.x + f.rx * latA, f.y + yA + yb, f.z + f.rz * latA);
      pos.push(f.x + f.rx * latB, f.y + yB + yb, f.z + f.rz * latB);
      const v = ss * vScale;
      uv.push(0, v, uScale, v);
      if (row > 0) {
        const a = (row - 1) * 2, b = a + 1, c = row * 2, d = c + 1;
        // ordem para normal para cima quando latA < latB
        idx.push(a, b, c, b, d, c);
      }
      row++;
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
    g.setAttribute('uv', new THREE.Float32BufferAttribute(uv, 2));
    g.setIndex(idx);
    g.computeVertexNormals();
    if (opts.flip) { const n = g.attributes.normal.array; for (let i = 0; i < n.length; i++) n[i] *= -1; }
    return g;
  }
}

const _f = {};
const _up = new THREE.Vector3(0, 1, 0);
