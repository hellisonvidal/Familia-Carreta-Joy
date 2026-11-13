// As 5 cidades do piloto (mundo aberto em quarteirões).
// relevo = altura das ladeiras (m); mar = lado onde fica o mar
export const CITIES = [
  {
    id: 'sp', nome: 'São Paulo', uf: 'SP', musica: 'funk', noite: true,
    descricao: 'Avenidões, arranha-céus e muita luz', seed: 11, relevo: 6,
    predios: { tipo: 'torre', altMin: 18, altMax: 85, cores: [0x8d9199, 0xb5b0a6, 0x6f7680, 0xcfc8bb, 0x5f6976, 0x9aa7b5], grade: 3 },
    chao: 0x5d6168, calcada: 'cinza', arvores: 'normal', mar: null,
    ceuDia: [0x6fa8dc, 0xdfe9f2], ceuNoite: [0x070b1d, 0x2b1d4a], nevoa: 0.0022,
    marcos: ['masp', 'antenas'], montanhas: { cor: 0x6b7a72, alt: 70 },
  },
  {
    id: 'rio', nome: 'Rio de Janeiro', uf: 'RJ', musica: 'samba', noite: false,
    descricao: 'Orla, praia, calçadão de ondas e morros', seed: 23, relevo: 2,
    predios: { tipo: 'medio', altMin: 12, altMax: 38, cores: [0xf1e3c8, 0xe9d2b4, 0xffffff, 0xd8c3a5, 0xc9dbe0], grade: 3 },
    chao: 0x6b6d55, calcada: 'ondas', arvores: 'coqueiro', mar: { lado: 'sul', cor: 0x1f7fa8 },
    ceuDia: [0x3f8fd8, 0xffd7a8], ceuNoite: [0x081028, 0x40204a], nevoa: 0.0016,
    marcos: ['paodeacucar', 'cristo'], montanhas: { cor: 0x3f6b3a, alt: 160 },
  },
  {
    id: 'bh', nome: 'Belo Horizonte', uf: 'MG', musica: 'forro', noite: false,
    descricao: 'Ladeiras de montanha: sobe e desce!', seed: 37, relevo: 13,
    predios: { tipo: 'medio', altMin: 9, altMax: 32, cores: [0xd6b38a, 0xc28f6a, 0xe8dcc4, 0xa7a29a, 0xd0c6b7], grade: 3 },
    chao: 0x7a5c3e, calcada: 'cinza', arvores: 'ipe', mar: null,
    ceuDia: [0x5a9bd5, 0xf2e2c4], ceuNoite: [0x0a0f24, 0x3a2340], nevoa: 0.002,
    marcos: ['serra', 'pampulha'], montanhas: { cor: 0x5b6e45, alt: 190 },
  },
  {
    id: 'ssa', nome: 'Salvador', uf: 'BA', musica: 'axe', noite: true,
    descricao: 'Casarões coloridos e ladeiras do centro histórico', seed: 51, relevo: 10,
    predios: { tipo: 'casarao', altMin: 7, altMax: 13, cores: [0xff7aa8, 0xffd23f, 0x4fc3f7, 0x7ed957, 0xff9f43, 0xb388ff, 0xff5e5b, 0x2ec4b6], grade: 4 },
    chao: 0x8a6f4d, calcada: 'pedra', arvores: 'coqueiro', mar: { lado: 'oeste', cor: 0x1a6f9a },
    ceuDia: [0x2f8fe0, 0xffe0b0], ceuNoite: [0x0b0a26, 0x5a1f45], nevoa: 0.0018,
    marcos: ['elevador', 'igreja'], montanhas: { cor: 0x4e7a4a, alt: 60 },
  },
  {
    id: 'cwb', nome: 'Curitiba', uf: 'PR', musica: 'eletro', noite: false,
    descricao: 'Cidade verde, estufa de vidro e estação-tubo', seed: 67, relevo: 4,
    predios: { tipo: 'torre', altMin: 14, altMax: 45, cores: [0xdedede, 0xb8c4c8, 0x9fb3a6, 0xe7e1d3, 0x7f8c8d], grade: 3 },
    chao: 0x4f7a3f, calcada: 'petit', arvores: 'araucaria', mar: null,
    ceuDia: [0x8fb4d6, 0xe8eef2], ceuNoite: [0x0a1020, 0x24304a], nevoa: 0.002,
    marcos: ['estufa', 'tubo'], montanhas: { cor: 0x55704f, alt: 80 },
  },
];

export const DURATIONS = [5, 10, 15, 20];

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
