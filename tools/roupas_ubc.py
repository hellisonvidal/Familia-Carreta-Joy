# Roupas para as pessoas (Universal Base Characters, CC0): pinta camiseta/regata/manga longa, calça/bermuda/saia
# e tênis por cima da textura de pele, usando o mapa de regiões do corpo (tools/pessoas_ubc.py).
# uso: python3 roupas_ubc.py pasta_texturas_ubc regioes.json pasta_saida
import sys, os, json, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
TEX, REGJ, OUT = sys.argv[1:4]
os.makedirs(OUT, exist_ok=True)
S = 1024
regions = json.load(open(REGJ))
rnd = random.Random(21)
ID = {'quadril': 1, 'tronco': 2, 'braco': 3, 'antebraco': 4, 'coxa': 5, 'canela': 6, 'pe': 7, 'pescoco': 8, 'cabeca': 9, 'mao': 10}
masks = {}
for g, faces in regions.items():
    m = Image.new('L', (S, S), 0); d = ImageDraw.Draw(m)
    for r, uv in faces:
        d.polygon([(u * S, (1 - v) * S) for u, v in uv], fill=ID[r])
    # cresce um pouco cada região para fechar costuras
    a = np.asarray(m); grown = a.copy()
    for _ in range(3):
        z = grown == 0
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            sh = np.roll(np.roll(grown, dy, 0), dx, 1)
            grown = np.where(z & (sh > 0), sh, grown); z = grown == 0
    masks[g] = grown
SHIRT = [(220, 50, 55), (40, 90, 200), (35, 150, 80), (245, 200, 40), (240, 120, 40), (130, 60, 170), (245, 245, 245), (40, 40, 48),
         (240, 110, 160), (60, 170, 200), (90, 110, 140), (120, 200, 120), (200, 30, 90), (20, 130, 140)]
PANTS = [(40, 60, 110), (30, 40, 70), (60, 60, 64), (25, 25, 30), (180, 160, 120), (110, 80, 50), (70, 90, 60), (200, 200, 205)]
SHOES = [(245, 245, 245), (30, 30, 34), (200, 60, 60), (60, 90, 170), (120, 90, 60)]
skins = {'M': ['T_Superhero_Male_Ligh.png', 'T_Superhero_Male_Dark.png'], 'F': ['T_Superhero_Female_Light_BaseColor.png', 'T_Superhero_Female_Dark_BaseColor.png']}
n = 0
for g, files in skins.items():
    mask = masks['Male' if g == 'M' else 'Female']
    bases = [np.asarray(Image.open(os.path.join(TEX, f)).convert('RGB').resize((S, S), Image.LANCZOS)).astype(np.float32) for f in files]
    bases.append((bases[0] * 0.55 + bases[1] * 0.45))   # tom de pele intermediário
    bases.append(bases[0] * np.array([1.0, 0.93, 0.86]))  # tom mais quente
    for k in range(10):
        base = bases[k % len(bases)].copy()
        lum = base.mean(-1, keepdims=True)
        # só o relevo fino da textura (músculos, dobras), sem as manchas grandes (roupa íntima escura do modelo)
        blur = np.asarray(Image.fromarray(lum[..., 0].astype(np.uint8)).filter(ImageFilter.GaussianBlur(10))).astype(np.float32)[..., None]
        detail = np.clip(1.0 + (lum - blur) / 90.0, 0.7, 1.25)
        shirt = np.array(rnd.choice(SHIRT), np.float32); pants = np.array(rnd.choice(PANTS), np.float32); shoes = np.array(rnd.choice(SHOES), np.float32)
        sleeve = rnd.random()                                      # regata / camiseta / manga longa
        short = rnd.random() < (0.45 if g == 'F' else 0.35)        # bermuda/saia
        rows = np.arange(S)[:, None]
        top = np.isin(mask, [2]) | ((mask == 9) & (rows > S * 0.45)) | ((mask == 3) & (sleeve > 0.2)) | ((mask == 4) & (sleeve > 0.85))
        if g == 'M': top |= (mask == 2)
        bottom = np.isin(mask, [1, 5]) | ((mask == 6) & (not short))
        feet = mask == 7
        noise = (np.random.default_rng(k).random((S, S, 1)) * 10 - 5).astype(np.float32)
        out = base.copy()
        for sel, col in ((top, shirt), (bottom, pants), (feet, shoes)):
            # volume/dobras relativos a cada peça (a roupa íntima escura do modelo não escurece a calça)
            out = np.where(sel[..., None], col * detail + noise, out)
        # listras na camiseta de vez em quando
        if rnd.random() < 0.3:
            stripes = ((np.arange(S)[:, None] // 22) % 2 == 0)[..., None] & top[..., None]
            out = np.where(stripes, np.array(rnd.choice(SHIRT), np.float32) * detail + noise, out)
        img = Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).resize((512, 512), Image.LANCZOS)
        img.save(os.path.join(OUT, f'roupa_{g}_{k:02d}.png')); n += 1
print(n, 'texturas de roupa')
