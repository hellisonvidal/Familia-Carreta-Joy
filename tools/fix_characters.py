# Corrige os 9 personagens do jogo para Unity Humanoid:
#  1. Remove pesos vazados entre tronco e braços ("corpo colando no braço") e entre perna esquerda e direita.
#  2. Coloca todos os personagens em T-pose limpa (braços horizontais, mãos retas) para o Humanoid do Unity
#     não empurrar os braços para dentro do tronco nem abrir "asas" no peito.
#  3. Endireita pernas, joelhos (leve flexão simétrica de ~2° para frente, garantindo eixo do joelho no Unity)
#     e pés apontando exatamente para a frente, sem encostar/atravessar um sapato no outro.
#  4. Padroniza o roll (eixos locais) de pernas, pés e braços e alinha a sola dos pés em Z = 0.
#
# Uso:
#   blender --background --python tools/fix_characters.py -- pasta_entrada pasta_saida [render_dir]

import bpy, os, sys, math, heapq
from mathutils import Vector, Matrix, Quaternion
from mathutils.kdtree import KDTree

argv = sys.argv[sys.argv.index('--') + 1:]
IN_DIR = argv[0]
OUT_DIR = argv[1]
RENDER_DIR = argv[2] if len(argv) > 2 and argv[2] != '-' else None
ONLY = set(argv[3].split(',')) if len(argv) > 3 else None
os.makedirs(OUT_DIR, exist_ok=True)
if RENDER_DIR:
    os.makedirs(RENDER_DIR, exist_ok=True)


def find_bone(bones, suffix):
    for b in bones:
        if b.name.endswith(suffix):
            return b
    return None


def dist_point_to_seg(p, a, b):
    ab = b - a
    l2 = ab.length_squared
    if l2 < 1e-10:
        return (p - a).length, 0.0
    t = max(0.0, min(1.0, (p - a).dot(ab) / l2))
    proj = a + ab * t
    return (p - proj).length, t


def build_welded_graph(mesh, mm, weld_eps):
    """Constrói grafo de arestas soldando vértices duplicados (costuras de UV)."""
    verts = mesh.data.vertices
    n = len(verts)
    pts = [mm @ v.co for v in verts]
    kd = KDTree(n)
    for i, p in enumerate(pts):
        kd.insert(p, i)
    kd.balance()

    v2w = [-1] * n
    w_pts = []
    for i in range(n):
        if v2w[i] != -1:
            continue
        wid = len(w_pts)
        w_pts.append(pts[i])
        for (_, idx, _) in kd.find_range(pts[i], weld_eps):
            if v2w[idx] == -1:
                v2w[idx] = wid

    nw = len(w_pts)
    adj = [dict() for _ in range(nw)]
    for e in mesh.data.edges:
        a, b = v2w[e.vertices[0]], v2w[e.vertices[1]]
        if a == b:
            continue
        d = (w_pts[a] - w_pts[b]).length
        if b not in adj[a] or d < adj[a][b]:
            adj[a][b] = d
            adj[b][a] = d
    return v2w, w_pts, adj, pts


def multi_dijkstra(adj, seeds):
    """Distância geodésica a partir de um conjunto de nós semente."""
    nw = len(adj)
    INF = 1e9
    dist = [INF] * nw
    pq = []
    for s in seeds:
        if dist[s] > 0.0:
            dist[s] = 0.0
            heapq.heappush(pq, (0.0, s))
    while pq:
        d, u = heapq.heappop(pq)
        if d > dist[u]:
            continue
        for v, w in adj[u].items():
            nd = d + w
            if nd < dist[v]:
                dist[v] = nd
                heapq.heappush(pq, (nd, v))
    return dist


def clean_weights(arm, mesh, name):
    """Limpa vazamento de peso entre tronco e braços e entre perna esquerda e direita."""
    M = arm.matrix_world
    mm = mesh.matrix_world
    bones = arm.data.bones

    def bp(suffix, tail=False):
        b = find_bone(bones, suffix)
        if not b:
            return None
        return M @ (b.tail_local if tail else b.head_local)

    # Pontos chave do esqueleto
    hips_p = bp('Hips')
    sp_p = bp('Spine') or hips_p
    sp1_p = bp('Spine1') or sp_p
    sp2_p = bp('Spine2') or sp1_p
    neck_p = bp('Neck') or sp2_p
    head_p = bp('Head') or neck_p
    head_t = bp('Head', True) or (head_p + Vector((0, 0, 0.15)))

    l_sh = bp('LeftArm')
    l_el = bp('LeftForeArm')
    l_wr = bp('LeftHand')
    l_ht = bp('LeftHand', True)

    r_sh = bp('RightArm')
    r_el = bp('RightForeArm')
    r_wr = bp('RightHand')
    r_ht = bp('RightHand', True)

    l_hip = bp('LeftUpLeg')
    l_kn = bp('LeftLeg')
    l_an = bp('LeftFoot')
    r_hip = bp('RightUpLeg')
    r_kn = bp('RightLeg')
    r_an = bp('RightFoot')

    verts = mesh.data.vertices
    zs = [(mm @ v.co).z for v in verts]
    zmin, zmax = min(zs), max(zs)
    H = max(0.1, zmax - zmin)

    # Mapa de grupos de vértices
    vg_by_name = {vg.name: vg.index for vg in mesh.vertex_groups}
    vg_names = {vg.index: vg.name for vg in mesh.vertex_groups}

    def gset(suffixes):
        return {i for i, n in vg_names.items() if any(n.endswith(s) for s in suffixes)}

    def find_g(suffix):
        for i, n in vg_names.items():
            if n.endswith(suffix):
                return i
        return None

    # Dedos também pertencem ao braço
    larm_g = {i for i, n in vg_names.items() if 'LeftArm' in n or 'LeftForeArm' in n or 'LeftHand' in n}
    rarm_g = {i for i, n in vg_names.items() if 'RightArm' in n or 'RightForeArm' in n or 'RightHand' in n}
    lsh_g = gset(('LeftShoulder',))
    rsh_g = gset(('RightShoulder',))
    torso_g = gset(('Hips', 'Spine', 'Spine1', 'Spine2', 'Neck', 'Head'))
    lleg_g = gset(('LeftUpLeg', 'LeftLeg', 'LeftFoot', 'LeftToeBase'))
    rleg_g = gset(('RightUpLeg', 'RightLeg', 'RightFoot', 'RightToeBase'))

    g_hips = find_g('Hips')
    g_sp = find_g('Spine') or g_hips
    g_sp1 = find_g('Spine1') or g_sp
    g_sp2 = find_g('Spine2') or g_sp1
    g_neck = find_g('Neck') or g_sp2
    g_head = find_g('Head') or g_neck
    g_larm = find_g('LeftArm')
    g_lfa = find_g('LeftForeArm')
    g_lhand = find_g('LeftHand')
    g_rarm = find_g('RightArm')
    g_rfa = find_g('RightForeArm')
    g_rhand = find_g('RightHand')
    g_lup = find_g('LeftUpLeg')
    g_llo = find_g('LeftLeg')
    g_lft = find_g('LeftFoot')
    g_rup = find_g('RightUpLeg')
    g_rlo = find_g('RightLeg')
    g_rft = find_g('RightFoot')

    # 1. Constrói grafo soldado para distância geodésica precisa
    v2w, w_pts, adj, pts = build_welded_graph(mesh, mm, H * 0.0012)
    nw = len(w_pts)

    # Distâncias aos segmentos dos braços
    l_segs = [(l_sh, l_el), (l_el, l_wr), (l_wr, l_ht)]
    r_segs = [(r_sh, r_el), (r_el, r_wr), (r_wr, r_ht)]
    torso_segs = [(hips_p, sp_p), (sp_p, sp1_p), (sp1_p, sp2_p), (sp2_p, neck_p), (neck_p, head_t)]
    lleg_segs = [(l_hip, l_kn), (l_kn, l_an)]
    rleg_segs = [(r_hip, r_kn), (r_kn, r_an)]

    def min_seg_dist(p, segs):
        best = 1e9
        for a, b in segs:
            if a is None or b is None:
                continue
            d, _ = dist_point_to_seg(p, a, b)
            if d < best:
                best = d
        return best

    # Verifica se o personagem está em A-pose (braços para baixo)
    l_dir = (l_el - l_sh).normalized()
    r_dir = (r_el - r_sh).normalized()
    arm_down_deg = math.degrees(math.atan2(-l_dir.z, math.hypot(l_dir.x, l_dir.y)))
    is_apose = arm_down_deg > 22.0

    # Sementes 100% PURAS para segmentação geodésica:
    # - Mãos/pulsos apenas para os braços (nunca encosta na camisa/jaqueta!)
    # - Coluna central + cabeça + pernas para o corpo
    body_seeds = []
    larm_seeds = []
    rarm_seeds = []
    head_seeds = []

    for wid, p in enumerate(w_pts):
        # Corpo central (nunca entra no braço)
        if abs(p.x) < min(abs(l_sh.x), abs(r_sh.x)) * 0.45 and p.z > zmin + 0.02 * H:
            body_seeds.append(wid)
        if p.z < hips_p.z - 0.05 * H and abs(p.x) < min(abs(l_sh.x), abs(r_sh.x)) * 1.1:
            body_seeds.append(wid)
        if p.z > neck_p.z + 0.02 * H and abs(p.x) < min(abs(l_sh.x), abs(r_sh.x)) * 0.85:
            head_seeds.append(wid)
            body_seeds.append(wid)
        # Mão/pulso esquerdo e direito (inequivocamente fora do tronco!)
        d_lw = (p - l_wr).length
        d_lht = (p - l_ht).length if l_ht else d_lw
        if min(d_lw, d_lht) < 0.065 * H and p.x > l_el.x * 0.80:
            larm_seeds.append(wid)
        d_rw = (p - r_wr).length
        d_rht = (p - r_ht).length if r_ht else d_rw
        if min(d_rw, d_rht) < 0.065 * H and p.x < r_el.x * 0.80:
            rarm_seeds.append(wid)

    if len(larm_seeds) < 5:
        for wid, p in enumerate(w_pts):
            if (p - l_wr).length < 0.09 * H:
                larm_seeds.append(wid)
    if len(rarm_seeds) < 5:
        for wid, p in enumerate(w_pts):
            if (p - r_wr).length < 0.09 * H:
                rarm_seeds.append(wid)

    gd_body = multi_dijkstra(adj, body_seeds)
    gd_larm = multi_dijkstra(adj, larm_seeds)
    gd_rarm = multi_dijkstra(adj, rarm_seeds)
    gd_head = multi_dijkstra(adj, head_seeds)

    # Calibra o "zero" geodésico EXATAMENTE na junta do ombro (axila/deltóide) de cada lado!
    # Assim delta = (gd_body - D_sh) - (gd_arm - L_arm) é 0.0 na linha do ombro, +2*s descendo o braço e -2*s entrando no tronco!
    def shoulder_ref(sh_pos, el_pos, gd_a):
        # Vértices próximos à articulação do ombro (deltóide/axila)
        cands = [w for w, p in enumerate(w_pts) if (p - sh_pos).length < 0.065 * H and gd_a[w] < 10.0 and gd_body[w] < 10.0]
        if not cands:
            best = min(range(nw), key=lambda w: (w_pts[w] - sh_pos).length)
            cands = [best]
        # Mediana de (gd_body - gd_arm) nos vértices bem no anel do ombro
        diffs = sorted(gd_body[w] - gd_a[w] for w in cands)
        return diffs[len(diffs) // 2]

    ref_l = shoulder_ref(l_sh, l_el, gd_larm)
    ref_r = shoulder_ref(r_sh, r_el, gd_rarm)

    # Sementes das pernas (esquerda vs direita) abaixo do joelho
    lleg_seeds = [wid for wid, p in enumerate(w_pts) if p.z < l_kn.z + 0.03 * H and p.x > 0.015 * H]
    rleg_seeds = [wid for wid, p in enumerate(w_pts) if p.z < r_kn.z + 0.03 * H and p.x < -0.015 * H]
    gd_lleg = multi_dijkstra(adj, lleg_seeds)
    gd_rleg = multi_dijkstra(adj, rleg_seeds)

    # Altura aproximada do cavalo (crotch) pelo ponto mais baixo da malha perto de X=0
    center_zs = [p.z for p in w_pts if abs(p.x) < 0.018 * H and l_kn.z < p.z < hips_p.z + 0.05 * H]
    crotch_z = min(center_zs) if center_zs else (l_hip.z - 0.04 * H)

    # Helper para redistribuir peso removido de tronco
    spine_chain = [(hips_p.z, g_hips), (sp_p.z, g_sp), (sp1_p.z, g_sp1), (sp2_p.z, g_sp2), (neck_p.z, g_neck)]

    def spine_weights_for_z(z):
        if z <= spine_chain[0][0]:
            return {spine_chain[0][1]: 1.0}
        for i in range(len(spine_chain) - 1):
            z0, g0 = spine_chain[i]
            z1, g1 = spine_chain[i + 1]
            if z0 <= z <= z1 and z1 > z0 + 1e-5:
                t = (z - z0) / (z1 - z0)
                if g0 == g1:
                    return {g0: 1.0}
                return {g0: 1.0 - t, g1: t}
        return {spine_chain[-1][1]: 1.0}

    def arm_weights_for_p(p, side):
        sh, el, wr, ht = (l_sh, l_el, l_wr, l_ht) if side == 'L' else (r_sh, r_el, r_wr, r_ht)
        ga, gf, gh = (g_larm, g_lfa, g_lhand) if side == 'L' else (g_rarm, g_rfa, g_rhand)
        d1, _ = dist_point_to_seg(p, sh, el)
        d2, _ = dist_point_to_seg(p, el, wr)
        d3, _ = dist_point_to_seg(p, wr, ht) if ht else (1e9, 0)
        if d1 <= d2 and d1 <= d3:
            return {ga: 1.0}
        elif d2 <= d3:
            return {gf: 1.0}
        else:
            return {gh: 1.0}

    def smoothstep(e0, e1, x):
        t = max(0.0, min(1.0, (x - e0) / max(1e-6, e1 - e0)))
        return t * t * (3.0 - 2.0 * t)

    fixed_ta = 0
    fixed_at = 0
    fixed_leg = 0

    # Armazena novos pesos por vértice: dict[group_idx -> weight]
    new_weights = []
    for vi, v in enumerate(verts):
        p = pts[vi]
        wid = v2w[vi]
        wmap = {g.group: g.weight for g in v.groups if g.weight > 1e-5}

        db = gd_body[wid]
        dla = gd_larm[wid]
        dra = gd_rarm[wid]
        dhd = gd_head[wid]

        # Cabelo comprido (Fofão): só o que é conectado geodésicamente à cabeça ou fios pendurados atrás do tronco (nunca as mangas!)
        d_l_arm_now = min_seg_dist(p, l_segs)
        d_r_arm_now = min_seg_dist(p, r_segs)
        is_hair = (name == 'Fofao' and p.z > hips_p.z - 0.18 * H and (
            (dhd < dla and dhd < dra and (sum(wmap.get(g, 0) for g in (g_head, g_neck)) > 0.01 or (d_l_arm_now > 0.055 * H and d_r_arm_now > 0.055 * H)))
            or (p.z < l_sh.z - 0.06 * H and abs(p.x) < abs(l_el.x) * 0.85 and d_l_arm_now > 0.055 * H and d_r_arm_now > 0.055 * H)
        ))

        # Rampa geodésica PURA centrada no ombro:
        # Roupas largas (camiseta do Afonso, paletó do Máscara) têm a cava da axila mais larga;
        # deslocamos o limiar ligeiramente para fora para não puxar a camisa/paletó ao levantar o braço.
        if name in ('Afonso', 'Mascara'):
            lo_edge = +0.092 * H
            hi_edge = +0.165 * H
        elif is_apose:
            lo_edge = +0.025 * H
            hi_edge = +0.095 * H
        else:
            lo_edge = -0.015 * H
            hi_edge = +0.055 * H

        diff_l = (db - dla) - ref_l
        diff_r = (db - dra) - ref_r

        # Se vértice está do lado errado do corpo (ex: X < 0 para braço esquerdo), arm_f = 0
        arm_f_l = 0.0 if (is_hair or p.x < l_sh.x * 0.55) else smoothstep(lo_edge, hi_edge, diff_l)
        arm_f_r = 0.0 if (is_hair or p.x > r_sh.x * 0.55) else smoothstep(lo_edge, hi_edge, diff_r)

        # Aplica limite suave nos pesos de braço esquerdo (arm_f_l) e braço direito (arm_f_r)
        for side, arm_g, sh_g, arm_f in (('L', larm_g, lsh_g, arm_f_l), ('R', rarm_g, rsh_g, arm_f_r)):
            w_a = sum(wmap.get(g, 0) for g in arm_g)
            # Caso 1: vértice do tronco (arm_f == 0) mas tinha peso de braço -> zera peso de braço!
            if arm_f <= 1e-4 and w_a > 0:
                for g in list(wmap.keys()):
                    if g in arm_g:
                        wmap.pop(g)
                if w_a > 0.01:
                    fixed_ta += 1
                rem_sum = sum(wmap.values())
                if rem_sum > 0.05:
                    for g in wmap:
                        wmap[g] /= rem_sum
                else:
                    if is_hair:
                        wmap[g_head] = 0.65; wmap[g_sp2] = 0.35
                    elif p.z < crotch_z:
                        wmap[g_lup if p.x >= 0 else g_rup] = 1.0
                    else:
                        wmap.update(spine_weights_for_z(p.z))
            # Caso 2: vértice claramente do braço (arm_f >= 0.999) mas tinha peso de tronco/perna -> zera tronco!
            elif arm_f >= 0.999:
                other_g = torso_g | lleg_g | rleg_g | (rarm_g if side == 'L' else larm_g)
                w_o = sum(wmap.get(g, 0) for g in other_g)
                if w_o > 0:
                    for g in list(wmap.keys()):
                        if g in other_g:
                            wmap.pop(g)
                    if w_o > 0.01:
                        fixed_at += 1
                    rem_sum = sum(wmap.values())
                    if rem_sum > 0.05:
                        for g in wmap:
                            wmap[g] /= rem_sum
                    else:
                        wmap.update(arm_weights_for_p(p, side))
            # Caso 3: faixa suave do ombro (0 < arm_f < 1): ajusta peso do braço suavemente para arm_f!
            elif 1e-4 < arm_f < 0.999:
                if w_a > arm_f + 0.02:
                    scale_a = arm_f / w_a
                    excess = w_a - arm_f
                    for g in list(wmap.keys()):
                        if g in arm_g:
                            wmap[g] *= scale_a
                    sh_idx = next(iter(sh_g), g_sp2)
                    wmap[sh_idx] = wmap.get(sh_idx, 0.0) + excess * 0.45
                    wmap[g_sp2] = wmap.get(g_sp2, 0.0) + excess * 0.55
                    fixed_ta += 1
                elif w_a < arm_f - 0.05:
                    # Garante transição contínua no deltóide/bíceps superior
                    w_non = sum(w for g, w in wmap.items() if g not in arm_g)
                    if w_non > 1e-4:
                        want_non = 1.0 - arm_f
                        s_non = want_non / w_non
                        for g in list(wmap.keys()):
                            if g not in arm_g:
                                wmap[g] *= s_non
                        if w_a > 1e-4:
                            s_arm = arm_f / w_a
                            for g in list(wmap.keys()):
                                if g in arm_g:
                                    wmap[g] *= s_arm
                        else:
                            ga = g_larm if side == 'L' else g_rarm
                            wmap[ga] = wmap.get(ga, 0.0) + arm_f

        # 5) Limpa pesos cruzados entre perna esquerda e perna direita (causa perna torta / calça repuxada!)
        if p.z < crotch_z + 0.035 * H:
            w_ll = sum(wmap.get(g, 0) for g in lleg_g)
            w_rl = sum(wmap.get(g, 0) for g in rleg_g)
            if w_ll > 0.01 and w_rl > 0.01:
                is_left = (gd_lleg[wid] < gd_rleg[wid]) if abs(gd_lleg[wid] - gd_rleg[wid]) > 0.01 * H else (p.x >= 0)
                if p.z < crotch_z - 0.015 * H:
                    wrong = rleg_g if is_left else lleg_g
                    removed = 0.0
                    for g in list(wmap.keys()):
                        if g in wrong:
                            removed += wmap.pop(g)
                    if removed > 0.01:
                        fixed_leg += 1
                        rem_sum = sum(wmap.values())
                        if rem_sum > 0.05:
                            for g in wmap:
                                wmap[g] /= rem_sum
                        else:
                            tgt = (g_lup if p.z > l_kn.z else g_llo if p.z > l_an.z else g_lft) if is_left else (g_rup if p.z > r_kn.z else g_rlo if p.z > r_an.z else g_rft)
                            wmap[tgt] = 1.0
                else:
                    if p.x > 0.015 * H:
                        for g in list(wmap.keys()):
                            if g in rleg_g:
                                wmap[g] *= 0.15
                        fixed_leg += 1
                    elif p.x < -0.015 * H:
                        for g in list(wmap.keys()):
                            if g in lleg_g:
                                wmap[g] *= 0.15
                        fixed_leg += 1

        # Normaliza (e limita aos 4 maiores pesos, igual ao SkinQuality.Bone4 do Unity!)
        tot = sum(wmap.values())
        if tot < 1e-5:
            wmap = {g_hips: 1.0}
            tot = 1.0
        items = sorted(wmap.items(), key=lambda kv: -kv[1])[:4]
        tot4 = sum(w for _, w in items)
        new_weights.append([(g, w / tot4) for g, w in items])

    # Aplica novos pesos de volta nos vertex groups
    for vg in mesh.vertex_groups:
        vg.remove(range(len(verts)))
    for vi, wl in enumerate(new_weights):
        for gi, w in wl:
            mesh.vertex_groups[gi].add([vi], w, 'REPLACE')

    return fixed_ta, fixed_at, fixed_leg, is_apose, arm_down_deg


def pose_to_tpose_and_straighten(arm, meshes, name):
    """Coloca braços em T-pose, endireita pernas/joelhos/pés sem colidir sapatos e normaliza roll e chão."""
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='POSE')
    bpy.ops.pose.select_all(action='SELECT')
    bpy.ops.pose.transforms_clear()
    bpy.context.view_layer.update()

    P = arm.pose.bones
    M = arm.matrix_world
    W = lambda p: M @ p

    def pb(suffix):
        return find_bone(P, suffix)

    def rot_world(b, R, pivot):
        if hasattr(R, "to_matrix"):
            R = R.to_matrix()
        T = Matrix.Translation(pivot) @ R.to_4x4() @ Matrix.Translation(-pivot)
        b.matrix = M.inverted() @ T @ M @ b.matrix
        bpy.context.view_layer.update()

    # Altura do personagem para folgas proporcionais
    zs = [(o.matrix_world @ v.co).z for o in meshes for v in o.data.vertices]
    H = max(0.1, max(zs) - min(zs))

    report = []

    # ---- 1. BRAÇOS EM T-POSE ----
    # Deixa ombro neutro, braço e antebraço horizontais (+X esquerda, -X direita), leve ângulo natural de 3° para baixo
    for side, sgn in (('Left', 1.0), ('Right', -1.0)):
        ua = pb(side + 'Arm')
        fa = pb(side + 'ForeArm')
        ha = pb(side + 'Hand')
        if not (ua and fa):
            continue
        # Braço superior -> apontando para (sgn, 0, -0.05) (quase 100% horizontal T-pose!)
        a, c = W(ua.head), W(fa.head)
        d = (c - a)
        if d.length > 1e-5:
            want = Vector((sgn, 0.0, -0.05)).normalized()
            ang = math.degrees(d.normalized().angle(want))
            if ang > 2.0:
                rot_world(ua, d.normalized().rotation_difference(want), a)
                report.append(f"{side[0]}Arm:{ang:.0f}°")
        # Antebraço -> alinhado reto na horizontal (sgn, 0, -0.03)
        if ha:
            a, c = W(fa.head), W(ha.head)
            d = (c - a)
            if d.length > 1e-5:
                want = Vector((sgn, 0.0, -0.03)).normalized()
                ang = math.degrees(d.normalized().angle(want))
                if ang > 2.0:
                    rot_world(fa, d.normalized().rotation_difference(want), a)
                    report.append(f"{side[0]}FA:{ang:.0f}°")
            # Mão -> reta na horizontal
            a, c = W(ha.head), W(ha.tail)
            d = (c - a)
            if d.length > 1e-5:
                want = Vector((sgn, 0.0, -0.02)).normalized()
                ang = math.degrees(d.normalized().angle(want))
                if ang > 4.0:
                    rot_world(ha, d.normalized().rotation_difference(want), a)

    # ---- 2. PERNAS RETAS, SIMÉTRICAS E SEM COLIDIR SAPATOS ----
    # Primeiro verifica largura atual dos quadris; se os quadris forem estreitos (Afonso/Fofão),
    # mantém uma leve abertura (2-3°) para os tênis não se atravessarem!
    lu, ru = pb('LeftUpLeg'), pb('RightUpLeg')
    hip_span = abs(W(lu.head).x - W(ru.head).x) if (lu and ru) else 0.16 * H
    # Largura mínima confortável entre centros dos tornozelos (evita tênis colidindo em Afonso/Fofão)
    min_ankle_span = 0.21 * H if hip_span < 0.12 * H else 0.175 * H
    extra_per_side = max(0.0, (min_ankle_span - hip_span) / 2.0)

    for side, sgn in (('Left', 1.0), ('Right', -1.0)):
        up, lo, ft, toe = pb(side + 'UpLeg'), pb(side + 'Leg'), pb(side + 'Foot'), pb(side + 'ToeBase')
        if not (up and lo and ft):
            continue
        leg_len = (W(lo.head) - W(up.head)).length + (W(ft.head) - W(lo.head)).length
        # Leve abertura lateral só se o quadril for muito estreito (evita tênis colidindo)
        target_lat = sgn * min(0.115, extra_per_side / max(leg_len, 0.1))  # máx ~6.5°
        # Coxa: reta para baixo, com +1.8° para frente (-Y) para garantir eixo do joelho no Unity!
        a, c = W(up.head), W(lo.head)
        d = (c - a)
        want_up = Vector((target_lat, -0.032, -1.0)).normalized()
        ang = math.degrees(d.normalized().angle(want_up))
        if ang > 0.5:
            rot_world(up, d.normalized().rotation_difference(want_up), a)
            report.append(f"{side[0]}Th:{ang:.1f}°")

        # Canela: reta para baixo, voltando +1.8° para trás (+Y) -> tornozelo exatamente sob o quadril!
        a, c = W(lo.head), W(ft.head)
        d = (c - a)
        want_lo = Vector((target_lat * 0.65, +0.032, -1.0)).normalized()
        ang = math.degrees(d.normalized().angle(want_lo))
        if ang > 0.5:
            rot_world(lo, d.normalized().rotation_difference(want_lo), a)
            report.append(f"{side[0]}Sh:{ang:.1f}°")

        # Pé: ponta apontando exatamente para a frente (-Y) no plano XY!
        # Usa tanto Foot.head->ToeBase.tail quanto Foot.head->Foot.tail para não ficar ziguezague
        h = W(ft.head)
        t = W(toe.tail) if toe else W(ft.tail)
        d = t - h
        d.z = 0
        if d.length > 1e-5:
            ang_z = math.atan2(d.x, -d.y)
            if abs(math.degrees(ang_z)) > 0.5:
                rot_world(ft, Matrix.Rotation(-ang_z, 3, 'Z'), h)
                report.append(f"{side[0]}Ft:{math.degrees(ang_z):.1f}°")

    bpy.ops.object.mode_set(mode='OBJECT')

    # ---- 3. ASSA A POSE NOVA NA MALHA E APLICA COMO REST POSE ----
    for o in meshes:
        mods = [m for m in o.modifiers if m.type == 'ARMATURE']
        if not mods:
            continue
        if o.data.shape_keys:
            o.shape_key_clear()
        bpy.ops.object.select_all(action='DESELECT')
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        mname = mods[0].name
        bpy.ops.object.modifier_apply(modifier=mname)
        m = o.modifiers.new('Armature', 'ARMATURE')
        m.object = arm

    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='POSE')
    bpy.ops.pose.select_all(action='SELECT')
    bpy.ops.pose.armature_apply(selected=False)

    # ---- 4. PADRONIZA POSIÇÃO DOS OSSOS DOS PÉS E ROLL (EIXOS LOCAIS) EM EDIT MODE ----
    # Alterar roll em Edit Mode NÃO deforma a malha (o skinning usa a matriz de bind) mas garante que
    # o Unity Humanoid calcule eixos de joelho/tornozelo/ombro 100% retos e simétricos!
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm.data.edit_bones

    def feb(suffix):
        for b in eb:
            if b.name.endswith(suffix):
                return b
        return None

    # Endireita ziguezague de Foot.tail / ToeBase.head (alinha em X com o tornozelo!)
    for side in ('Left', 'Right'):
        ft = feb(side + 'Foot')
        toe = feb(side + 'ToeBase')
        if ft and toe:
            ax = ft.head.x
            ft.tail.x = ax
            toe.head.x = ax
            toe.tail.x = ax
            # Mantém ToeBase nivelado com o chão (horizontal para frente -Y)
            toe.tail.z = toe.head.z

    # Alinha roll dos ossos ao padrão Mixamo (igual ao Run.fbx / Walk.fbx / Idle.fbx)
    # No espaço local da Armature (já com rotação/escala = 1):
    # - Pernas (UpLeg, Leg, Foot, ToeBase): eixo Z do osso apontando para frente (-Y) ou para cima (+Z no ToeBase)
    #   Em Blender align_roll(vector) alinha o eixo Z do osso ao vetor dado!
    for suffix, zvec in [
        ('Hips', Vector((0, -1, 0))),
        ('Spine', Vector((0, -1, 0))),
        ('Spine1', Vector((0, -1, 0))),
        ('Spine2', Vector((0, -1, 0))),
        ('Neck', Vector((0, -1, 0))),
        ('Head', Vector((0, -1, 0))),
        ('LeftUpLeg', Vector((0, -1, 0))),
        ('LeftLeg', Vector((0, -1, 0))),
        ('RightUpLeg', Vector((0, -1, 0))),
        ('RightLeg', Vector((0, -1, 0))),
        ('LeftFoot', Vector((0, -0.6, 0.8))),
        ('RightFoot', Vector((0, -0.6, 0.8))),
        ('LeftToeBase', Vector((0, 0, 1))),
        ('RightToeBase', Vector((0, 0, 1))),
        ('LeftShoulder', Vector((0, 0, 1))),
        ('RightShoulder', Vector((0, 0, 1))),
        ('LeftArm', Vector((0, -1, 0))),
        ('LeftForeArm', Vector((0, -1, 0))),
        ('LeftHand', Vector((0, -1, 0))),
        ('RightArm', Vector((0, -1, 0))),
        ('RightForeArm', Vector((0, -1, 0))),
        ('RightHand', Vector((0, -1, 0))),
    ]:
        b = feb(suffix)
        if b:
            b.align_roll(zvec)

    bpy.ops.object.mode_set(mode='OBJECT')

    # ---- 5. ALINHA A SOLA DOS PÉS EXATAMENTE EM Z = 0.000 ----
    bpy.context.view_layer.update()
    zmin_new = min((o.matrix_world @ v.co).z for o in meshes for v in o.data.vertices)
    if abs(zmin_new) > 1e-4:
        arm.location.z -= zmin_new
        for o in meshes:
            if o.parent != arm:
                o.location.z -= zmin_new
        bpy.ops.object.select_all(action='DESELECT')
        arm.select_set(True)
        for o in meshes:
            o.select_set(True)
        bpy.context.view_layer.objects.active = arm
        bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)

    return report


def render_verify(arm, meshes, name, tex_path):
    if not RENDER_DIR:
        return
    if os.path.exists(tex_path):
        mat = bpy.data.materials.new("mat")
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes["Principled BSDF"]
        img = mat.node_tree.nodes.new("ShaderNodeTexImage")
        img.image = bpy.data.images.load(tex_path)
        mat.node_tree.links.new(img.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = 0.85
        for m in meshes:
            m.data.materials.clear()
            m.data.materials.append(mat)

    zs = [(m.matrix_world @ v.co).z for m in meshes for v in m.data.vertices]
    h = max(0.1, max(zs) - min(zs))
    s = 1.7 / h
    arm.scale *= s
    bpy.context.view_layer.update()

    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'TEXTURE'
    sc.render.resolution_x = 360
    sc.render.resolution_y = 460
    cam_data = bpy.data.cameras.new("cam")
    cam_data.lens = 50
    cam = bpy.data.objects.new("cam", cam_data)
    sc.collection.objects.link(cam)
    sc.camera = cam

    def look_from(pos, target=(0, 0, 0.85)):
        cam.location = Vector(pos)
        cam.rotation_euler = (Vector(target) - cam.location).to_track_quat('-Z', 'Y').to_euler()

    def pb(suffix):
        return find_bone(arm.pose.bones, suffix)

    def rot_world(b, axis, deg):
        M = arm.matrix_world
        piv = M @ b.head
        R = Matrix.Rotation(math.radians(deg), 3, axis)
        T = Matrix.Translation(piv) @ R.to_4x4() @ Matrix.Translation(-piv)
        b.matrix = M.inverted() @ T @ M @ b.matrix
        bpy.context.view_layer.update()

    # 1. Rest T-pose front
    look_from((0, -3.4, 0.9))
    sc.render.filepath = os.path.join(RENDER_DIR, f"{name}_1_tpose.png")
    bpy.ops.render.render(write_still=True)

    # 2. Arms raised +45° (teste de "corpo colando no braço")
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='POSE')
    la, ra = pb('LeftArm'), pb('RightArm')
    if la:
        rot_world(la, 'Y', -45)
    if ra:
        rot_world(ra, 'Y', +45)
    bpy.ops.object.mode_set(mode='OBJECT')
    look_from((0, -3.4, 0.95))
    sc.render.filepath = os.path.join(RENDER_DIR, f"{name}_2_armsup.png")
    bpy.ops.render.render(write_still=True)

    # 3. Arms lowered -68° (pose Idle: braços ao lado do corpo, sem entrar no tronco!)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='POSE')
    bpy.ops.pose.select_all(action='SELECT')
    bpy.ops.pose.transforms_clear()
    bpy.context.view_layer.update()
    if la:
        rot_world(la, 'Y', +68)
    if ra:
        rot_world(ra, 'Y', -68)
    bpy.ops.object.mode_set(mode='OBJECT')
    look_from((0, -3.2, 0.9))
    sc.render.filepath = os.path.join(RENDER_DIR, f"{name}_3_idle.png")
    bpy.ops.render.render(write_still=True)


# Processa todos os personagens encontrados
chars_repo = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "unity", "Assets", "Resources", "Characters")
for fname in sorted(os.listdir(IN_DIR)):
    if not fname.endswith('.fbx'):
        continue
    name = fname[:-4]
    if ONLY and name not in ONLY:
        continue
    src = os.path.join(IN_DIR, fname)
    out = os.path.join(OUT_DIR, f"{name}.fbx")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src)
    arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    for ac in list(bpy.data.actions):
        bpy.data.actions.remove(ac)
    if arm.animation_data:
        arm.animation_data.action = None

    ta_tot = at_tot = leg_tot = 0
    is_apose = False
    arm_deg = 0.0
    for m in meshes:
        ta, at, lg, ap, ad = clean_weights(arm, m, name)
        ta_tot += ta; at_tot += at; leg_tot += lg; is_apose = ap; arm_deg = ad

    rep = pose_to_tpose_and_straighten(arm, meshes, name)

    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(
        filepath=out, use_selection=False, object_types={'ARMATURE', 'MESH'},
        add_leaf_bones=False, bake_anim=False,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        path_mode='STRIP', use_armature_deform_only=True
    )
    print(f"FIXED {name:10s} | apose={is_apose}({arm_deg:+.0f}°) | wFix(tor<-arm:{ta_tot}, arm<-tor:{at_tot}, xLeg:{leg_tot}) | {' '.join(rep)}")

    tex = os.path.join(chars_repo, name, f"{name}_albedo.jpg")
    render_verify(arm, meshes, name, tex)
