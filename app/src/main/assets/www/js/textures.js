import * as THREE from 'three';

// Texturas desenhadas em canvas (sem arquivos externos)
function canvas(w, h) { const c = document.createElement('canvas'); c.width = w; c.height = h; return [c, c.getContext('2d')]; }
function tex(c, repeat = true, srgb = true) {
  const t = new THREE.CanvasTexture(c);
  if (repeat) { t.wrapS = t.wrapT = THREE.RepeatWrapping; }
  if (srgb) t.colorSpace = THREE.SRGBColorSpace;
  t.anisotropy = 4;
  return t;
}
function speckle(g, w, h, n, colors, size = 2) {
  for (let i = 0; i < n; i++) {
    g.fillStyle = colors[(Math.random() * colors.length) | 0];
    g.fillRect(Math.random() * w, Math.random() * h, size, size);
  }
}

// Asfalto: 512px = 18 m de largura (lat -9..9) por 18 m de comprimento
export function asphalt() {
  const [c, g] = canvas(512, 512);
  g.fillStyle = '#3a3b3e'; g.fillRect(0, 0, 512, 512);
  speckle(g, 512, 512, 9000, ['#2f3033', '#45464a', '#353639', '#4c4d50'], 2);
  // remendos
  for (let i = 0; i < 6; i++) { g.fillStyle = 'rgba(20,20,22,0.25)'; g.fillRect(Math.random() * 512, Math.random() * 512, 30 + Math.random() * 60, 20 + Math.random() * 40); }
  const X = (lat) => (lat + 9) / 18 * 512;
  // bordas contínuas
  g.fillStyle = '#e8e8e2';
  g.fillRect(X(-8.7), 0, 6, 512); g.fillRect(X(8.7) - 6, 0, 6, 512);
  // faixas tracejadas
  for (const lat of [-5.5, -1.6, 1.5, 4.5]) {
    for (let v = 0; v < 512; v += 128) g.fillRect(X(lat) - 3, v, 6, 64);
  }
  // faixa amarela dupla à esquerda
  g.fillStyle = '#f2c230'; g.fillRect(X(-8.2), 0, 4, 512); g.fillRect(X(-8.2) + 8, 0, 4, 512);
  return tex(c);
}

// Calçadas (256px = 3 m x 3 m)
export function sidewalk(kind) {
  const [c, g] = canvas(256, 256);
  if (kind === 'ondas' || kind === 'petit') {
    g.fillStyle = '#efeae0'; g.fillRect(0, 0, 256, 256);
    g.fillStyle = '#1d1d1d';
    if (kind === 'ondas') {
      // ondas pretas e brancas
      g.beginPath();
      for (let band = -1; band < 3; band++) {
        const y0 = band * 128;
        g.moveTo(0, y0 + 64);
        for (let x = 0; x <= 256; x += 4) g.lineTo(x, y0 + 64 + Math.sin(x / 256 * Math.PI * 2) * 36);
        for (let x = 256; x >= 0; x -= 4) g.lineTo(x, y0 + 64 + 64 + Math.sin(x / 256 * Math.PI * 2) * 36);
        g.closePath();
      }
      g.fill();
    } else {
      // padrão geométrico de petit-pavé
      for (let y = 0; y < 256; y += 64) for (let x = 0; x < 256; x += 64) {
        if (((x + y) / 64) % 2 === 0) { g.beginPath(); g.moveTo(x + 32, y); g.lineTo(x + 64, y + 32); g.lineTo(x + 32, y + 64); g.lineTo(x, y + 32); g.fill(); }
      }
    }
    speckle(g, 256, 256, 2500, ['rgba(0,0,0,0.15)', 'rgba(255,255,255,0.15)'], 3);
  } else if (kind === 'pedra') {
    g.fillStyle = '#5d5248'; g.fillRect(0, 0, 256, 256);
    for (let y = 0; y < 256; y += 24) for (let x = (y / 24) % 2 * 12; x < 256; x += 24) {
      const v = 120 + Math.random() * 60;
      g.fillStyle = `rgb(${v},${v * 0.9},${v * 0.8})`;
      g.beginPath(); g.ellipse(x + 12, y + 12, 10, 10, 0, 0, Math.PI * 2); g.fill();
    }
  } else {
    g.fillStyle = '#a9a7a2'; g.fillRect(0, 0, 256, 256);
    speckle(g, 256, 256, 3000, ['#9b9994', '#b6b4af', '#8f8d88'], 2);
    g.strokeStyle = '#7f7d79'; g.lineWidth = 3;
    for (let i = 0; i <= 256; i += 64) { g.beginPath(); g.moveTo(i, 0); g.lineTo(i, 256); g.stroke(); g.beginPath(); g.moveTo(0, i); g.lineTo(256, i); g.stroke(); }
  }
  return tex(c);
}

// Fachada de prédio: 4 colunas x 4 andares de janelas por "bloco" de 12 m x 12 m.
// Retorna {map, emissive}
export function facade(kind) {
  const [c, g] = canvas(256, 256);
  const [e, ge] = canvas(256, 256);
  g.fillStyle = '#ffffff'; g.fillRect(0, 0, 256, 256);
  ge.fillStyle = '#000'; ge.fillRect(0, 0, 256, 256);
  speckle(g, 256, 256, 1500, ['#eeeeee', '#f6f6f6', '#e4e4e4'], 3);
  const lit = ['#ffd27a', '#ffe9b0', '#fff3d6', '#ffc46b', '#bfe3ff'];
  if (kind === 'casarao') {
    // 2 andares, 3 janelas com moldura (sobrados coloniais)
    for (let row = 0; row < 2; row++) for (let col = 0; col < 3; col++) {
      const x = 22 + col * 80, y = 30 + row * 118, w = 50, h = 80;
      g.fillStyle = '#fbfbf5'; g.fillRect(x - 6, y - 6, w + 12, h + 12);
      g.fillStyle = '#2b2b33'; g.fillRect(x, y, w, h);
      g.fillStyle = '#3c6e71'; g.fillRect(x, y, w / 2 - 2, h); g.fillRect(x + w / 2 + 2, y, w / 2 - 2, h);
      g.fillStyle = '#fbfbf5'; g.fillRect(x - 10, y + h + 4, w + 20, 6);
      if (Math.random() < 0.55) { ge.fillStyle = lit[(Math.random() * 3) | 0]; ge.fillRect(x + 4, y + 4, w - 8, h - 8); }
    }
    g.fillStyle = '#fbfbf5'; g.fillRect(0, 122, 256, 8);
  } else {
    for (let row = 0; row < 4; row++) for (let col = 0; col < 4; col++) {
      const x = 10 + col * 64, y = 12 + row * 64, w = 44, h = 40;
      g.fillStyle = kind === 'medio' ? '#3d4a57' : '#2f4252';
      g.fillRect(x, y, w, h);
      g.fillStyle = 'rgba(255,255,255,0.25)'; g.fillRect(x + 3, y + 3, w * 0.35, h - 6);
      if (kind === 'medio') { g.fillStyle = '#d9d9d9'; g.fillRect(x - 4, y + h, w + 8, 5); }
      if (Math.random() < 0.42) { ge.fillStyle = lit[(Math.random() * lit.length) | 0]; ge.fillRect(x + 2, y + 2, w - 4, h - 4); }
    }
  }
  // faixa lisa no topo (usada pelo telhado)
  g.fillStyle = '#ffffff'; g.fillRect(0, 0, 256, 4);
  ge.fillStyle = '#000'; ge.fillRect(0, 0, 256, 4);
  return { map: tex(c), emissive: tex(e) };
}

export function radial(color = '255,220,150') {
  const [c, g] = canvas(128, 128);
  const gr = g.createRadialGradient(64, 64, 0, 64, 64, 64);
  gr.addColorStop(0, `rgba(${color},1)`); gr.addColorStop(0.4, `rgba(${color},0.45)`); gr.addColorStop(1, `rgba(${color},0)`);
  g.fillStyle = gr; g.fillRect(0, 0, 128, 128);
  return tex(c, false);
}

export function blobShadow() {
  const [c, g] = canvas(128, 128);
  const gr = g.createRadialGradient(64, 64, 0, 64, 64, 64);
  gr.addColorStop(0, 'rgba(0,0,0,0.55)'); gr.addColorStop(0.6, 'rgba(0,0,0,0.3)'); gr.addColorStop(1, 'rgba(0,0,0,0)');
  g.fillStyle = gr; g.fillRect(0, 0, 128, 128);
  return tex(c, false);
}

export function beamTexture() {
  const [c, g] = canvas(64, 256);
  const gr = g.createLinearGradient(0, 0, 0, 256);
  // topo do canvas = ponta do cone (onde fica o refletor)
  gr.addColorStop(0, 'rgba(255,255,255,0.9)'); gr.addColorStop(0.3, 'rgba(255,255,255,0.35)'); gr.addColorStop(1, 'rgba(255,255,255,0)');
  g.fillStyle = gr; g.fillRect(0, 0, 64, 256);
  const t = tex(c, false); return t;
}
