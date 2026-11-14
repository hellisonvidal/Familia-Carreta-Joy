import * as THREE from 'three';
import { RoundedBoxGeometry } from 'three/addons/geometries/RoundedBoxGeometry.js';

// Outros modelos de carreta, montados peça a peça (frente = +Z).
const std = (color, extra = {}) => new THREE.MeshStandardMaterial({ color, roughness: 0.5, metalness: 0.2, ...extra });
const rb = (w, h, d, r) => new RoundedBoxGeometry(w, h, d, 3, r);

function canvasTex(w, h, draw) { const c = document.createElement('canvas'); c.width = w; c.height = h; draw(c.getContext('2d'), w, h); const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 4; return t; }
const speakerTex = () => canvasTex(256, 256, (g, w, h) => {
  g.fillStyle = '#111'; g.fillRect(0, 0, w, h);
  for (const [x, y, r] of [[64, 64, 52], [192, 64, 52], [64, 192, 52], [192, 192, 52]]) {
    const gr = g.createRadialGradient(x, y, 4, x, y, r); gr.addColorStop(0, '#666'); gr.addColorStop(0.25, '#222'); gr.addColorStop(0.8, '#333'); gr.addColorStop(1, '#555');
    g.fillStyle = gr; g.beginPath(); g.arc(x, y, r, 0, 7); g.fill(); g.strokeStyle = '#888'; g.lineWidth = 4; g.stroke();
  }
});
const ledTex = (text) => canvasTex(512, 256, (g, w, h) => {
  const gr = g.createLinearGradient(0, 0, w, h); gr.addColorStop(0, '#ff3d7f'); gr.addColorStop(0.35, '#7c4dff'); gr.addColorStop(0.7, '#00e5ff'); gr.addColorStop(1, '#ffc400');
  g.fillStyle = gr; g.fillRect(0, 0, w, h);
  g.fillStyle = 'rgba(0,0,0,0.25)'; for (let x = 0; x < w; x += 8) g.fillRect(x, 0, 2, h); for (let y = 0; y < h; y += 8) g.fillRect(0, y, w, 2);
  g.font = 'bold 96px sans-serif'; g.textAlign = 'center'; g.textBaseline = 'middle'; g.lineWidth = 10; g.strokeStyle = '#000'; g.strokeText(text, w / 2, h / 2); g.fillStyle = '#fff'; g.fillText(text, w / 2, h / 2);
});
const stripeTex = (a, b) => canvasTex(256, 64, (g, w, h) => { for (let x = 0; x < w; x += 32) { g.fillStyle = (x / 32) % 2 ? a : b; g.beginPath(); g.moveTo(x, 0); g.lineTo(x + 32, 0); g.lineTo(x + 16, h); g.lineTo(x - 16, h); g.fill(); } });

function wheels(G, zs, W, r = 0.55) {
  const tire = std(0x111111, { roughness: 0.9, metalness: 0 }), hub = std(0xcfd8dc, { metalness: 0.8, roughness: 0.3 });
  for (const z of zs) for (const s of [-1, 1]) {
    const t = new THREE.Mesh(new THREE.CylinderGeometry(r, r, 0.45, 20).rotateZ(Math.PI / 2), tire); t.position.set(s * (W - 0.2), r, z); G.add(t);
    const h = new THREE.Mesh(new THREE.CylinderGeometry(r * 0.55, r * 0.55, 0.47, 12).rotateZ(Math.PI / 2), hub); h.position.copy(t.position); G.add(h);
  }
}
function cab(G, z, W, color, H = 3.2) {
  const c = new THREE.Mesh(rb(W * 2, H, 2.6, 0.35), std(color)); c.position.set(0, 0.6 + H / 2, z); G.add(c);
  const win = new THREE.Mesh(rb(W * 2 - 0.3, 1.1, 0.12, 0.05), std(0x1c2a38, { metalness: 0.6, roughness: 0.1 })); win.position.set(0, 0.6 + H * 0.68, z + 1.28); G.add(win);
  for (const s of [-1, 1]) { const sw = new THREE.Mesh(rb(0.1, 0.9, 1.4, 0.04), win.material); sw.position.set(s * W, 0.6 + H * 0.68, z + 0.3); G.add(sw); }
  const grill = new THREE.Mesh(rb(W * 1.6, 0.8, 0.12, 0.05), std(0xb0bec5, { metalness: 0.9, roughness: 0.25 })); grill.position.set(0, 1.3, z + 1.31); G.add(grill);
  const bumper = new THREE.Mesh(rb(W * 2 + 0.1, 0.35, 0.4, 0.1), std(0x263238)); bumper.position.set(0, 0.7, z + 1.35); G.add(bumper);
}
function railing(G, W, y, z0, z1, color = 0xeeeeee) {
  const m = std(color, { metalness: 0.7, roughness: 0.3 });
  for (const s of [-1, 1]) { const bar = new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.05, z1 - z0, 6).rotateX(Math.PI / 2), m); bar.position.set(s * (W - 0.08), y + 1.0, (z0 + z1) / 2); G.add(bar);
    for (let z = z0; z <= z1; z += 1.6) { const p = new THREE.Mesh(new THREE.CylinderGeometry(0.04, 0.04, 1.0, 6), m); p.position.set(s * (W - 0.08), y + 0.5, z); G.add(p); } }
  const back = new THREE.Mesh(new THREE.CylinderGeometry(0.05, 0.05, W * 2, 6).rotateZ(Math.PI / 2), m); back.position.set(0, y + 1.0, z0); G.add(back);
}

// cada design devolve { group, height, roofY, halfW, roofZ }
export function buildDesign(id) {
  const G = new THREE.Group();
  if (id === 'trioGigante') {
    const W = 1.7, L = 17, deckY = 4.6;
    cab(G, L / 2 - 1.4, W, 0x7c4dff, 3.0);
    const body = new THREE.Mesh(rb(W * 2, deckY - 0.8, L - 3.2, 0.25), std(0x1a1a2e)); body.position.set(0, 0.8 + (deckY - 0.8) / 2, -1.5); G.add(body);
    const sp = speakerTex(), spm = new THREE.MeshStandardMaterial({ map: sp, roughness: 0.6 });
    for (const s of [-1, 1]) for (let z = -9; z < 5.5; z += 1.9) { const p = new THREE.Mesh(new THREE.PlaneGeometry(1.8, 1.8), spm); p.position.set(s * (W + 0.01), 2.8, z); p.rotation.y = s * Math.PI / 2; G.add(p); const p2 = p.clone(); p2.position.y = 1.2 + 0.35; p2.scale.set(1, 0.7, 1); G.add(p2); }
    const back = new THREE.Mesh(new THREE.PlaneGeometry(W * 2, deckY - 1), spm); back.position.set(0, 0.8 + (deckY - 1) / 2, -1.5 - (L - 3.2) / 2 - 0.01); back.rotation.y = Math.PI; G.add(back);
    const deck = new THREE.Mesh(rb(W * 2 + 0.2, 0.25, L - 3.0, 0.08), std(0x444444, { metalness: 0.5 })); deck.position.set(0, deckY, -1.5); G.add(deck);
    railing(G, W + 0.1, deckY, -1.5 - (L - 3.2) / 2, 5.6);
    const truss = std(0xb0bec5, { metalness: 0.9, roughness: 0.25 });
    for (const z of [-8.5, 5.2]) for (const s of [-1, 1]) { const p = new THREE.Mesh(new THREE.CylinderGeometry(0.08, 0.08, 3, 6), truss); p.position.set(s * (W - 0.05), deckY + 1.5, z); G.add(p); }
    for (const s of [-1, 1]) { const t = new THREE.Mesh(new THREE.CylinderGeometry(0.08, 0.08, 13.7, 6).rotateX(Math.PI / 2), truss); t.position.set(s * (W - 0.05), deckY + 3, -1.65); G.add(t); }
    const led = new THREE.Mesh(new THREE.PlaneGeometry(W * 2, 1.4), new THREE.MeshBasicMaterial({ map: ledTex('TRIO JOY'), toneMapped: false })); led.position.set(0, deckY + 2.3, 5.25); G.add(led);
    const led2 = led.clone(); led2.position.z = -8.55; led2.rotation.y = Math.PI; G.add(led2);
    wheels(G, [6.4, 4.6, -3, -4.7, -6.4], W);
    return { group: G, height: deckY + 0.13, roofY: deckY + 0.12, halfW: W, roofZ: [-9.4, 5.4] };
  }
  if (id === 'onibus2') {
    const W = 1.3, L = 13, H1 = 2.6;
    const paint = std(0xff6d00);
    const lower = new THREE.Mesh(rb(W * 2, H1, L, 0.35), paint); lower.position.set(0, 0.55 + H1 / 2, 0); G.add(lower);
    const upper = new THREE.Mesh(rb(W * 2, 1.0, L - 0.4, 0.2), std(0xffc400)); upper.position.set(0, 0.55 + H1 + 0.5, -0.2); G.add(upper);
    const glass = std(0x1c2a38, { metalness: 0.6, roughness: 0.1 });
    for (const s of [-1, 1]) { const w = new THREE.Mesh(rb(0.06, 1.0, L - 1.6, 0.05), glass); w.position.set(s * (W + 0.01), 0.55 + H1 * 0.62, 0); G.add(w); }
    const fw = new THREE.Mesh(rb(W * 2 - 0.3, 1.3, 0.08, 0.05), glass); fw.position.set(0, 0.55 + H1 * 0.6, L / 2 + 0.01); G.add(fw);
    const stripe = new THREE.Mesh(new THREE.PlaneGeometry(L - 0.8, 0.5), new THREE.MeshStandardMaterial({ map: stripeTex('#00c853', '#2979ff'), roughness: 0.5 }));
    for (const s of [-1, 1]) { const st = stripe.clone(); st.position.set(s * (W + 0.02), 0.95, 0); st.rotation.y = s * Math.PI / 2; G.add(st); }
    const roofY = 0.55 + H1 + 1.0;
    railing(G, W, roofY, -L / 2 + 0.4, L / 2 - 0.6, 0xffffff);
    const led = new THREE.Mesh(new THREE.PlaneGeometry(W * 2 - 0.4, 0.45), new THREE.MeshBasicMaterial({ map: ledTex('FESTA!'), toneMapped: false })); led.position.set(0, 0.55 + H1 - 0.2, L / 2 + 0.02); G.add(led);
    wheels(G, [4.3, -3.6, -4.9], W, 0.55);
    return { group: G, height: roofY, roofY: roofY + 0.01, halfW: W, roofZ: [-L / 2 + 0.7, L / 2 - 0.8] };
  }
  // caminhão-palco
  const W = 1.6, L = 15, deckY = 1.7;
  cab(G, L / 2 - 1.4, W, 0xe53935, 3.0);
  const bed = new THREE.Mesh(rb(W * 2 + 0.3, 0.5, L - 3, 0.1), std(0x263238)); bed.position.set(0, deckY - 0.25, -1.5); G.add(bed);
  const floor = new THREE.Mesh(new THREE.PlaneGeometry(W * 2 + 0.2, L - 3.2).rotateX(-Math.PI / 2), new THREE.MeshStandardMaterial({ map: canvasTex(256, 512, (g) => { for (let y = 0; y < 512; y += 64) for (let x = 0; x < 256; x += 64) { g.fillStyle = ['#ff3d7f', '#ffc400', '#00c853', '#2979ff'][((x + y) / 64) % 4]; g.fillRect(x + 2, y + 2, 60, 60); } }), emissive: 0xffffff, emissiveIntensity: 0.15, roughness: 0.4 }));
  floor.position.set(0, deckY + 0.01, -1.5); G.add(floor); G.userData.floor = floor;
  const truss = std(0xb0bec5, { metalness: 0.9, roughness: 0.25 });
  for (const z of [-8.4, 4.8]) { for (const s of [-1, 1]) { const p = new THREE.Mesh(new THREE.BoxGeometry(0.25, 4.2, 0.25), truss); p.position.set(s * (W + 0.05), deckY + 2.1, z); G.add(p); } const top = new THREE.Mesh(new THREE.BoxGeometry(W * 2 + 0.35, 0.25, 0.25), truss); top.position.set(0, deckY + 4.2, z); G.add(top); }
  for (const s of [-1, 1]) { const t = new THREE.Mesh(new THREE.BoxGeometry(0.25, 0.25, 13.2), truss); t.position.set(s * (W + 0.05), deckY + 4.2, -1.8); G.add(t); }
  const wall = new THREE.Mesh(new THREE.PlaneGeometry(W * 2 + 0.3, 3.4), new THREE.MeshBasicMaterial({ map: ledTex('CARRETA JOY'), toneMapped: false })); wall.position.set(0, deckY + 2.1, -8.45); G.add(wall);
  for (const s of [-1, 1]) for (let k = 0; k < 2; k++) { const sp = new THREE.Mesh(new THREE.BoxGeometry(0.9, 1.2, 0.8), new THREE.MeshStandardMaterial({ map: speakerTex(), roughness: 0.6 })); sp.position.set(s * (W - 0.5), deckY + 0.6 + k * 1.2, -7.8); G.add(sp); }
  wheels(G, [6.0, 4.2, -4.6, -6.3], W);
  return { group: G, height: deckY, roofY: deckY + 0.02, halfW: W, roofZ: [-7.2, 5.0] };
}
