# Texturas da cidade a partir de texturas CC0 do ambientCG (reboco, tijolo, concreto, telha, asfalto, calçada):
# fachadas de prédio e casa (4x4 janelas, com mapa de luz das janelas à noite), térreo com lojas, telhado,
# laje, asfalto com faixas e calçada de pedra.
# uso: python3 texturas.py pasta_ambientcg pasta_saida
import sys, os, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

SRC, OUT = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)
rnd = random.Random(9)

def tex(name, size):
    im = Image.open(f'{SRC}/{name}/{name}_1K-JPG_Color.jpg').convert('RGB')
    return im.resize((size, size), Image.LANCZOS)

def light(im, gain=1.0, sat=1.0, bias=0):
    a = np.asarray(im).astype(np.float32)
    g = a.mean(-1, keepdims=True)
    a = g + (a - g) * sat
    a = a * gain + bias
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))

def save(im, name, q=None):
    im.save(os.path.join(OUT, name))

C = 128          # uma janela (vão de 3,2 m x um andar)
S = C * 4        # 4 x 4 janelas por textura

def facade(kind, wall, seed):
    r = random.Random(seed)
    base = Image.new('RGB', (S, S)); glow = Image.new('RGB', (S, S), (0, 0, 0))
    for j in range(4):
        for i in range(4):
            base.paste(wall.crop((i * C % wall.width, j * C % wall.height, i * C % wall.width + C, j * C % wall.height + C)), (i * C, j * C))
    d = ImageDraw.Draw(base); g = ImageDraw.Draw(glow)
    for j in range(4):
        for i in range(4):
            x0, y0 = i * C, j * C          # y0 = topo da célula (a textura é lida de baixo para cima no jogo)
            lit = r.random() < 0.45
            warm = r.choice([(255, 214, 150), (255, 236, 190), (220, 230, 255)])
            if kind == 'predio':
                wx0, wx1, wy0, wy1 = x0 + 22, x0 + C - 22, y0 + 24, y0 + C - 34
                balcony = r.random() < 0.3
                if balcony: wy1 = y0 + C - 14
                d.rectangle([wx0 - 5, wy0 - 5, wx1 + 5, wy1 + 5], fill=(238, 238, 236))                 # moldura
                for yy in range(wy0, wy1):                                                                 # vidro com reflexo do céu
                    t = (yy - wy0) / max(1, wy1 - wy0)
                    c = (int(70 + 70 * (1 - t)), int(95 + 70 * (1 - t)), int(120 + 80 * (1 - t)))
                    d.line([wx0, yy, wx1, yy], fill=c)
                d.polygon([(wx0, wy1), (wx0 + 30, wy0), (wx0 + 46, wy0), (wx0 + 16, wy1)], fill=(150, 175, 200))   # brilho
                cx = (wx0 + wx1) // 2; d.rectangle([cx - 2, wy0, cx + 2, wy1], fill=(225, 225, 222))             # caixilho
                if r.random() < 0.4:                                                                        # cortina
                    d.rectangle([wx0, wy0, wx0 + (wx1 - wx0) * r.uniform(0.2, 0.5), wy1], fill=r.choice([(200, 180, 150), (170, 190, 200), (210, 200, 190)]))
                if balcony:
                    d.rectangle([x0 + 10, y0 + C - 46, x0 + C - 10, y0 + C - 42], fill=(60, 60, 64))      # guarda-corpo
                    for k in range(x0 + 12, x0 + C - 10, 7): d.line([k, y0 + C - 44, k, y0 + C - 14], fill=(70, 70, 74), width=2)
                    d.rectangle([x0 + 6, y0 + C - 14, x0 + C - 6, y0 + C - 8], fill=(200, 200, 198))        # laje da sacada
                else:
                    d.rectangle([wx0 - 9, wy1 + 5, wx1 + 9, wy1 + 11], fill=(214, 212, 206))                 # peitoril
                if lit: g.rectangle([wx0, wy0, wx1, wy1], fill=warm)
            else:  # casa
                door = j == 3 and i in (1, 3) and seed % 2 == i % 2
                if door:
                    d.rectangle([x0 + 40, y0 + 30, x0 + 88, y0 + C], fill=(235, 232, 225))
                    d.rectangle([x0 + 45, y0 + 35, x0 + 83, y0 + C], fill=(120, 78, 45))
                    for k in range(3): d.rectangle([x0 + 50, y0 + 42 + k * 26, x0 + 78, y0 + 62 + k * 26], outline=(95, 60, 35), width=2)
                    d.ellipse([x0 + 74, y0 + 82, x0 + 79, y0 + 87], fill=(210, 180, 90))
                    if lit: g.rectangle([x0 + 50, y0 + 42, x0 + 78, y0 + 60], fill=warm)
                    continue
                wx0, wx1, wy0, wy1 = x0 + 30, x0 + C - 30, y0 + 34, y0 + C - 36
                d.rectangle([wx0 - 6, wy0 - 6, wx1 + 6, wy1 + 6], fill=(245, 242, 235))
                for yy in range(wy0, wy1):
                    t = (yy - wy0) / max(1, wy1 - wy0)
                    d.line([wx0, yy, wx1, yy], fill=(int(60 + 60 * (1 - t)), int(80 + 60 * (1 - t)), int(100 + 70 * (1 - t))))
                d.rectangle([(wx0 + wx1) // 2 - 2, wy0, (wx0 + wx1) // 2 + 2, wy1], fill=(240, 238, 230))
                d.rectangle([wx0, (wy0 + wy1) // 2 - 2, wx1, (wy0 + wy1) // 2 + 2], fill=(240, 238, 230))
                sh = r.choice([(115, 75, 45), (60, 110, 70), (70, 90, 130), (140, 140, 140)])
                for side in (-1, 1):                                                                       # venezianas
                    sx0 = wx0 - 26 if side < 0 else wx1 + 6
                    d.rectangle([sx0, wy0 - 4, sx0 + 20, wy1 + 4], fill=sh)
                    for k in range(wy0, wy1, 6): d.line([sx0 + 2, k, sx0 + 18, k], fill=tuple(int(c * 0.75) for c in sh), width=2)
                d.rectangle([wx0 - 10, wy1 + 6, wx1 + 10, wy1 + 12], fill=(220, 215, 205))
                if lit: g.rectangle([wx0, wy0, wx1, wy1], fill=warm)
    return base, glow.filter(ImageFilter.GaussianBlur(1.2))

# reboco claro (o jogo pinta cada prédio com a sua cor) e tijolo
reboco = light(tex('Plaster001', 512), 1.08, 0.2, 10)
concreto = light(tex('Concrete034', 512), 1.12, 0.1, 12)
tijolo = light(tex('Bricks059', 512), 1.35, 0.55, 30)
for name, kind, wall, seed in (('fachada_predio', 'predio', concreto, 1), ('fachada_predio2', 'predio', tijolo, 2),
                               ('fachada_casa', 'casa', reboco, 3), ('fachada_casa2', 'casa', reboco, 4)):
    b, g = facade(kind, wall, seed)
    save(b, name + '.png'); save(g, name + '_luz.png')

# térreo com lojas: vitrine, toldo listrado e letreiro (4 lojas lado a lado)
loja = Image.new('RGB', (S, C)); lg = Image.new('RGB', (S, C), (0, 0, 0))
loja.paste(concreto.crop((0, 0, S, C)), (0, 0))
d = ImageDraw.Draw(loja); g = ImageDraw.Draw(lg)
for i in range(4):
    x0 = i * C
    col = rnd.choice([(220, 60, 60), (40, 120, 200), (40, 160, 90), (240, 180, 40), (230, 120, 40), (150, 70, 170)])
    d.rectangle([x0 + 6, 10, x0 + C - 6, 30], fill=(250, 250, 248)); d.rectangle([x0 + 14, 15, x0 + C - 14, 25], fill=col)   # letreiro
    for k in range(0, C - 12, 12): d.rectangle([x0 + 6 + k, 30, x0 + 12 + k, 44], fill=col if (k // 12) % 2 == 0 else (250, 250, 245))   # toldo
    d.rectangle([x0 + 8, 46, x0 + C - 8, C - 4], fill=(60, 60, 64))
    for yy in range(50, C - 8):
        t = (yy - 50) / (C - 58)
        d.line([x0 + 12, yy, x0 + C - 12, yy], fill=(int(90 + 60 * (1 - t)), int(110 + 60 * (1 - t)), int(130 + 60 * (1 - t))))
    d.rectangle([x0 + C // 2 - 2, 50, x0 + C // 2 + 2, C - 8], fill=(60, 60, 64))
    for k in range(4):   # mercadorias na vitrine
        bx = x0 + 18 + k * 24; d.rectangle([bx, C - 30, bx + 14, C - 10], fill=rnd.choice([(230, 200, 80), (200, 80, 80), (80, 150, 200), (240, 240, 240)]))
    g.rectangle([x0 + 12, 50, x0 + C - 12, C - 8], fill=(255, 230, 180)); g.rectangle([x0 + 14, 15, x0 + C - 14, 25], fill=col)
save(loja, 'terreo_loja.png'); save(lg.filter(ImageFilter.GaussianBlur(1.0)), 'terreo_loja_luz.png')

# telhado de cerâmica, laje de concreto
save(light(tex('RoofingTiles014A', 512), 1.15, 1.0, 0), 'telha.png')
save(light(tex('Concrete034', 256), 0.95, 0.1, 0), 'laje.png')
# calçada de pedra (petit-pavê / paralelepípedo), clareada
save(light(tex('PavingStones092', 512), 1.1, 0.6, 15), 'calcada.png')
# asfalto: textura real + faixa central tracejada e bordas (64 m de rua por textura de 256x1024)
asf = light(tex('Asphalt026B', 512), 1.0, 0.3, 4).resize((256, 1024))
d = ImageDraw.Draw(asf)
for y in range(0, 1024, 512): d.rectangle([123, y, 132, y + 190], fill=(232, 226, 200))
d.rectangle([0, 0, 6, 1024], fill=(210, 210, 204)); d.rectangle([249, 0, 255, 1024], fill=(210, 210, 204))
save(asf, 'asfalto.png')
print('texturas em', OUT, sorted(os.listdir(OUT)))
