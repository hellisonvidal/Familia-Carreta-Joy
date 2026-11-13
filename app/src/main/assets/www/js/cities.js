// As 5 cidades do piloto. Cada uma tem relevo, estilo de prédios, paisagem e música próprios.
export const CITIES = [
  {
    id: 'sp', nome: 'São Paulo', uf: 'SP', musica: 'funk', noite: true,
    descricao: 'Avenidão cheio de arranha-céus e luzes',
    seed: 11, comprimento: 1500,
    // rampas possíveis (inclinação) e curvas possíveis (curvatura 1/raio)
    rampas: [0, 0, 0.04, -0.04, 0.07, -0.06], curvas: [0, 0, 0, 1 / 300, -1 / 300],
    predios: { tipo: 'torre', altMin: 18, altMax: 90, cores: [0x8d9199, 0xb5b0a6, 0x6f7680, 0xcfc8bb, 0x4f5966, 0x9aa7b5], larg: [12, 22] },
    chao: 0x5d6168, calcada: 'cinza', arvores: 'normal', mar: null,
    ceuDia: [0x6fa8dc, 0xdfe9f2], ceuNoite: [0x070b1d, 0x2b1d4a], nevoa: 0.0032,
    marcos: ['masp', 'antenas'], montanhas: { cor: 0x6b7a72, alt: 60 },
  },
  {
    id: 'rio', nome: 'Rio de Janeiro', uf: 'RJ', musica: 'samba', noite: false,
    descricao: 'Orla com praia, calçadão e morros',
    seed: 23, comprimento: 1500,
    rampas: [0, 0, 0, 0.02, -0.02], curvas: [0, 0, 1 / 400, -1 / 400],
    predios: { tipo: 'medio', altMin: 12, altMax: 40, cores: [0xf1e3c8, 0xe9d2b4, 0xffffff, 0xd8c3a5, 0xc9dbe0], larg: [12, 18] },
    chao: 0x6b6d55, calcada: 'ondas', arvores: 'coqueiro', mar: { lado: -1, cor: 0x1f7fa8 },
    ceuDia: [0x3f8fd8, 0xffd7a8], ceuNoite: [0x081028, 0x40204a], nevoa: 0.0022,
    marcos: ['paodeacucar', 'cristo'], montanhas: { cor: 0x3f6b3a, alt: 140 },
  },
  {
    id: 'bh', nome: 'Belo Horizonte', uf: 'MG', musica: 'forro', noite: false,
    descricao: 'Ladeiras de montanha: sobe e desce!',
    seed: 37, comprimento: 1500,
    rampas: [0, 0.09, -0.09, 0.12, -0.11, 0.06, -0.06], curvas: [0, 1 / 220, -1 / 220, 0],
    predios: { tipo: 'medio', altMin: 9, altMax: 35, cores: [0xd6b38a, 0xc28f6a, 0xe8dcc4, 0xa7a29a, 0xd0c6b7], larg: [10, 16] },
    chao: 0x7a5c3e, calcada: 'cinza', arvores: 'ipe', mar: null,
    ceuDia: [0x5a9bd5, 0xf2e2c4], ceuNoite: [0x0a0f24, 0x3a2340], nevoa: 0.0028,
    marcos: ['serra', 'pampulha'], montanhas: { cor: 0x5b6e45, alt: 180 },
  },
  {
    id: 'ssa', nome: 'Salvador', uf: 'BA', musica: 'axe', noite: true,
    descricao: 'Casarões coloridos e ladeiras do centro histórico',
    seed: 51, comprimento: 1500,
    rampas: [0, 0.08, -0.08, 0.1, -0.1, 0], curvas: [0, 1 / 180, -1 / 180, 1 / 260],
    predios: { tipo: 'casarao', altMin: 7, altMax: 13, cores: [0xff7aa8, 0xffd23f, 0x4fc3f7, 0x7ed957, 0xff9f43, 0xb388ff, 0xff5e5b, 0x2ec4b6], larg: [7, 11] },
    chao: 0x8a6f4d, calcada: 'pedra', arvores: 'coqueiro', mar: { lado: -1, cor: 0x1a6f9a },
    ceuDia: [0x2f8fe0, 0xffe0b0], ceuNoite: [0x0b0a26, 0x5a1f45], nevoa: 0.0026,
    marcos: ['elevador', 'igreja'], montanhas: { cor: 0x4e7a4a, alt: 50 },
  },
  {
    id: 'cwb', nome: 'Curitiba', uf: 'PR', musica: 'eletro', noite: false,
    descricao: 'Cidade verde, estufa de vidro e estação-tubo',
    seed: 67, comprimento: 1500,
    rampas: [0, 0.03, -0.03, 0.05, -0.05], curvas: [0, 1 / 260, -1 / 260],
    predios: { tipo: 'torre', altMin: 14, altMax: 45, cores: [0xdedede, 0xb8c4c8, 0x9fb3a6, 0xe7e1d3, 0x7f8c8d], larg: [12, 18] },
    chao: 0x4f7a3f, calcada: 'petit', arvores: 'araucaria', mar: null,
    ceuDia: [0x8fb4d6, 0xe8eef2], ceuNoite: [0x0a1020, 0x24304a], nevoa: 0.003,
    marcos: ['estufa', 'tubo'], montanhas: { cor: 0x55704f, alt: 70 },
  },
];

// RNG determinístico (cada cidade é sempre igual)
export function rng(seed) {
  let s = seed >>> 0 || 1;
  return () => {
    s ^= s << 13; s >>>= 0; s ^= s >> 17; s ^= s << 5; s >>>= 0;
    return (s >>> 0) / 4294967296;
  };
}
export const pick = (r, arr) => arr[Math.floor(r() * arr.length)];
export const range = (r, a, b) => a + (b - a) * r();
