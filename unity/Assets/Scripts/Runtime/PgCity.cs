using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarretaJoy
{
    /// <summary>
    /// Ponta Grossa de verdade: ruas, prédios, casas, parques e relevo reais (OpenStreetMap + satélite).
    /// A cidade é carregada em pedaços de 200 m em volta do jogador (o celular não aguentaria tudo de uma vez);
    /// longe fica só o relevo pintado, coberto pela neblina.
    /// </summary>
    public class PgCity : MonoBehaviour
    {
        public PgMap map;
        public float loadRadius = 420f, unloadRadius = 620f;
        public readonly List<Vector3> Ring = new List<Vector3>();
        public readonly List<float> StopDistances = new List<float>();        // em ordem ao longo do percurso
        public readonly List<float> StopDistancesByStop = new List<float>();  // na ordem das paradas (menu)
        public Vector3 PlazaCenter;
        public readonly List<Material> DanceTiles = new List<Material>();
        public const int GroundLayer = 8;
        public Texture2D GroundTexture => groundTex;
        public Rect GroundRect => groundRect;

        readonly Dictionary<Vector2Int, GameObject> live = new Dictionary<Vector2Int, GameObject>();
        // ruas dos pedaços carregados (para o trânsito e as pessoas)
        readonly Dictionary<Vector2Int, List<PgMap.Road>> liveRoads = new Dictionary<Vector2Int, List<PgMap.Road>>();
        readonly List<PgMap.Road> near = new List<PgMap.Road>();

        /// <summary>Ruas carregadas cujo pedaço fica a menos de "radius" do ponto (para carros: só as de carro).</summary>
        public List<PgMap.Road> RoadsNear(Vector3 p, float radius, bool cars = false)
        {
            near.Clear();
            foreach (var kv in liveRoads)
            {
                if (DistToChunk(p, kv.Key) > radius) continue;
                foreach (var r in kv.Value)
                {
                    if (r.pts == null || r.pts.Length < 2) continue;
                    if (cars && !IsCarRoad(r)) continue;
                    near.Add(r);
                }
            }
            return near;
        }

        public static bool IsCarRoad(PgMap.Road r)
        {
            string t = r.type < RoadTypes.Length ? RoadTypes[r.type] : "residential";
            if (r.unpaved || t == "footway" || t == "path" || t == "steps" || t == "cycleway" || t == "pedestrian" || t == "track") return false;
            return r.width >= 5f;
        }
        readonly HashSet<Vector2Int> queued = new HashSet<Vector2Int>();
        Material ground, asphalt, dirt, sidewalk, water, roofTile, roofTile2, roofFlat, trunkMat, leafMat, araucariaMat;
        readonly Dictionary<int, Material> wallMats = new Dictionary<int, Material>();
        Texture2D groundTex, winApt, winApt2, winHouse, winHouse2, asphaltTex, dirtTex;
        Rect groundRect;
        Mesh treeRound, treeArauc;

        static readonly Color[] HouseCols = { new Color(0.97f, 0.95f, 0.88f), new Color(0.98f, 0.88f, 0.6f), new Color(0.72f, 0.85f, 0.95f), new Color(0.98f, 0.78f, 0.65f),
                                              new Color(0.75f, 0.92f, 0.78f), new Color(0.85f, 0.85f, 0.85f), new Color(0.97f, 0.75f, 0.8f), new Color(0.92f, 0.83f, 0.7f) };
        static readonly Color[] AptCols = { new Color(0.95f, 0.94f, 0.9f), new Color(0.86f, 0.8f, 0.7f), new Color(0.7f, 0.72f, 0.75f), new Color(0.78f, 0.62f, 0.5f),
                                            new Color(0.6f, 0.68f, 0.78f), new Color(0.9f, 0.88f, 0.8f) };
        static readonly Color[] ShopCols = { new Color(0.9f, 0.3f, 0.25f), new Color(0.98f, 0.82f, 0.25f), new Color(0.3f, 0.55f, 0.9f), new Color(0.35f, 0.75f, 0.45f),
                                             new Color(0.97f, 0.96f, 0.94f), new Color(0.98f, 0.6f, 0.2f) };

        // =====================================================================
        // casas de verdade (City Kit Suburban da Kenney, CC0) nos quarteirões perto do jogador
        class HouseModel { public Mesh mesh; public Matrix4x4 baseM; public float sx, sz, sy; }
        readonly List<HouseModel> houseModels = new List<HouseModel>();
        Material[] houseMats;
        readonly HashSet<Vector2Int> detailed = new HashSet<Vector2Int>();
        public float detailRadius = 220f;   // 0 = desliga (gráficos leves)

        void LoadHouses()
        {
            foreach (var go in Resources.LoadAll<GameObject>("Houses"))
            {
                var mf = go.GetComponentInChildren<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                // posição/rotação/escala da malha dentro do modelo importado (o FBX do Blender vem girado)
                var m = Matrix4x4.identity; var t = mf.transform;
                while (t != null && t != go.transform.parent) { m = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * m; t = t.parent; }
                m = Matrix4x4.TRS(Vector3.zero, go.transform.localRotation, go.transform.localScale) * Matrix4x4.Inverse(Matrix4x4.TRS(go.transform.localPosition, go.transform.localRotation, go.transform.localScale)) * m;
                var vs = mf.sharedMesh.vertices;
                Vector3 mn = Vector3.one * 1e9f, mx = -Vector3.one * 1e9f;
                foreach (var v in vs) { var w = m.MultiplyPoint3x4(v); mn = Vector3.Min(mn, w); mx = Vector3.Max(mx, w); }
                var size = mx - mn;
                if (size.x < 0.01f || size.z < 0.01f) continue;
                // centraliza e põe a base no chão
                m = Matrix4x4.Translate(new Vector3(-(mn.x + mx.x) / 2f, -mn.y, -(mn.z + mx.z) / 2f)) * m;
                houseModels.Add(new HouseModel { mesh = mf.sharedMesh, baseM = m, sx = size.x, sz = size.z, sy = size.y });
            }
            var tex = new[] { "telhado_barro", "telhado_barro", "telhado_cinza", "telhado_marrom" };
            houseMats = new Material[tex.Length];
            for (int i = 0; i < tex.Length; i++)
            {
                houseMats[i] = new Material(Resources.Load<Material>("Materials/Lit"));
                var t2 = Resources.Load<Texture2D>("Houses/" + tex[i]);
                if (t2) houseMats[i].SetTexture("_BaseMap", t2);
                houseMats[i].SetColor("_BaseColor", Color.white); houseMats[i].SetFloat("_Smoothness", 0.2f);
            }
        }

        /// <summary>Troca a casa-caixote por uma casa modelada, encaixada no terreno (orientada pelo lado maior).</summary>
        bool KenneyHouse(MultiMesh mb, MultiMesh coll, PgMap.Building b, float gmin)
        {
            int n = b.pts.Length;
            if (houseModels.Count == 0 || n < 4 || n > 6) return false;
            // lado mais comprido = direção da casa
            int best = 0; float bl = 0;
            for (int i = 0; i < n; i++) { float l = (b.pts[(i + 1) % n] - b.pts[i]).sqrMagnitude; if (l > bl) { bl = l; best = i; } }
            Vector2 dir = (b.pts[(best + 1) % n] - b.pts[best]).normalized, per = new Vector2(-dir.y, dir.x);
            float a0 = 1e9f, a1 = -1e9f, c0 = 1e9f, c1 = -1e9f, area = 0;
            for (int i = 0; i < n; i++)
            {
                var p = b.pts[i]; var q = b.pts[(i + 1) % n];
                float pa = Vector2.Dot(p, dir), pc = Vector2.Dot(p, per);
                a0 = Mathf.Min(a0, pa); a1 = Mathf.Max(a1, pa); c0 = Mathf.Min(c0, pc); c1 = Mathf.Max(c1, pc);
                area += p.x * q.y - q.x * p.y;
            }
            float L = a1 - a0, Wd = c1 - c0;
            if (L < 4.5f || Wd < 4f || L > 20f || Wd > 16f) return false;
            if (Mathf.Abs(area) / 2f < 0.72f * L * Wd) return false;   // só formatos quase retangulares
            Vector2 cen = dir * ((a0 + a1) / 2f) + per * ((c0 + c1) / 2f);
            int h = Mathf.Abs((int)(cen.x * 7.31f) * 73856093 ^ (int)(cen.y * 3.17f) * 19349663);
            var hm = houseModels[h % houseModels.Count];
            bool longX = hm.sx >= hm.sz;
            float mLong = longX ? hm.sx : hm.sz, mShort = longX ? hm.sz : hm.sx;
            float kL = L / mLong, kW = Wd / mShort, kY = Mathf.Sqrt(kL * kW) * 1.05f;
            float yaw = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg + (longX ? 0f : 90f) + ((h >> 3) % 2 == 0 ? 180f : 0f);
            var scale = longX ? new Vector3(kL, kY, kW) : new Vector3(kW, kY, kL);
            var mtx = Matrix4x4.TRS(new Vector3(cen.x, gmin - 0.08f, cen.y), Quaternion.Euler(0, yaw, 0), scale) * hm.baseM;
            mb.Get(houseMats[(h >> 5) % houseMats.Length]).AddMesh(hm.mesh, 0, mtx);
            // colisão: paredes no contorno do terreno
            var Cc = coll.Get(roofFlat);
            float top = gmin + hm.sy * kY * 0.55f;
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = b.pts[i], p1 = b.pts[(i + 1) % n];
                var outN = new Vector3(p1.y - p0.y, 0, -(p1.x - p0.x)).normalized;
                Cc.Quad(new Vector3(p0.x, gmin - 0.6f, p0.y), new Vector3(p1.x, gmin - 0.6f, p1.y), new Vector3(p1.x, top, p1.y), new Vector3(p0.x, top, p0.y), outN, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            }
            return true;
        }

        public void Build()
        {
            map = PgMap.Load();
            MakeTextures();
            MakeMaterials();
            LoadHouses();
            treeRound = MakeRoundTree(); treeArauc = MakeAraucaria();
            BuildFarTerrain();

            foreach (var p in map.Route) Ring.Add(new Vector3(p.x, map.Height(p.x, p.y) + 0.05f, p.y));
            // distância (ao longo do percurso) de cada parada de festa
            float acc = 0; var cum = new float[Ring.Count];
            for (int i = 1; i < Ring.Count; i++) { acc += Vector3.Distance(Ring[i - 1], Ring[i]); cum[i] = acc; }
            foreach (var s in map.Stops)
            {
                int best = 0; float bd = float.MaxValue;
                for (int i = 0; i < Ring.Count; i++) { float d = (new Vector2(Ring[i].x, Ring[i].z) - s.pos).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
                StopDistances.Add(cum[best]); StopDistancesByStop.Add(cum[best]);
            }
            StopDistances.Sort();

            // praça da Catedral: ponto de partida e pista de dança
            var cat = map.Stops.Count > 0 ? map.Stops[0].pos : Vector2.zero;
            PlazaCenter = new Vector3(cat.x, map.Height(cat.x, cat.y), cat.y);
            BuildDanceFloor(PlazaCenter);
            BuildLandmarks();
            EnsureLoaded(PlazaCenter, true);
        }

        public float Height(float x, float z) => map.Height(x, z);

        // ---------------- carregamento em pedaços ----------------
        Vector3 lastScan = new Vector3(1e9f, 0, 0); bool pending = true;
        public void EnsureLoaded(Vector3 p, bool immediate)
        {
            if (!immediate && !pending && (p - lastScan).sqrMagnitude < 15f * 15f) return;
            lastScan = p;
            float C = map.ChunkSize;
            int ci = Mathf.FloorToInt(p.x / C), cj = Mathf.FloorToInt(p.z / C);
            int R = Mathf.CeilToInt(loadRadius / C);
            var want = new List<(float d, Vector2Int id)>();
            for (int i = ci - R; i <= ci + R; i++)
                for (int j = cj - R; j <= cj + R; j++)
                {
                    var id = new Vector2Int(i, j);
                    float d = DistToChunk(p, id);
                    if (d <= loadRadius && !live.ContainsKey(id)) want.Add((d, id));
                }
            want.Sort((a, b) => a.d.CompareTo(b.d));
            if (immediate) { foreach (var w in want) BuildChunk(w.id, detailRadius > 0 && w.d < detailRadius); }
            else
            {
                // um pedaço por quadro (o mais perto primeiro): sem travadas
                pending = want.Count > 1;
                if (want.Count > 0) BuildChunk(want[0].id, detailRadius > 0 && want[0].d < detailRadius);
                var drop = new List<Vector2Int>();
                foreach (var kv in live) if (DistToChunk(p, kv.Key) > unloadRadius) drop.Add(kv.Key);
                foreach (var id in drop) { DestroyChunk(live[id]); live.Remove(id); liveRoads.Remove(id); detailed.Remove(id); }
                // nível de detalhe: casas modeladas perto, casas simples longe (um pedaço por vez)
                if (want.Count == 0)
                {
                    Vector2Int? swap = null; bool toDetail = false; float bestD = float.MaxValue;
                    foreach (var kv in live)
                    {
                        float d = DistToChunk(p, kv.Key);
                        bool isD = detailed.Contains(kv.Key);
                        if (detailRadius > 0 && !isD && d < detailRadius && d < bestD) { swap = kv.Key; toDetail = true; bestD = d; }
                        else if (isD && (detailRadius <= 0 || d > detailRadius + 120f) && swap == null) { swap = kv.Key; toDetail = false; }
                    }
                    if (swap.HasValue)
                    {
                        var id = swap.Value;
                        DestroyChunk(live[id]); live.Remove(id); liveRoads.Remove(id);
                        BuildChunk(id, toDetail);
                        pending = true;
                    }
                }
            }
        }

        float DistToChunk(Vector3 p, Vector2Int id)
        {
            float C = map.ChunkSize;
            float dx = Mathf.Max(0, Mathf.Max(id.x * C - p.x, p.x - (id.x + 1) * C));
            float dz = Mathf.Max(0, Mathf.Max(id.y * C - p.z, p.z - (id.y + 1) * C));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static void DestroyChunk(GameObject go)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) Destroy(mf.sharedMesh);
            foreach (var mc in go.GetComponentsInChildren<MeshCollider>()) if (mc.sharedMesh) Destroy(mc.sharedMesh);
            Destroy(go);
        }

        void BuildChunk(Vector2Int id) => BuildChunk(id, false);
        void BuildChunk(Vector2Int id, bool detail)
        {
            if (detail) detailed.Add(id); else detailed.Remove(id);
            var root = new GameObject($"Pedaco_{id.x}_{id.y}");
            root.transform.SetParent(transform, false);
            live[id] = root;
            BuildTerrainChunk(id, root.transform);
            var c = map.ReadChunk(id);
            if (c == null) return;
            liveRoads[id] = c.roads;
            var mb = new MultiMesh();
            foreach (var r in c.roads) AddRoad(mb, r);
            foreach (var w in c.water) AddRibbon(mb, water, w.pts, w.w, 0.06f, false, 6f);
            var coll = new MultiMesh();
            foreach (var b in c.buildings) AddBuilding(mb, coll, b, detail);
            foreach (var t in c.trees) AddTree(mb, t.p, t.kind);
            mb.Emit(root.transform, "Cidade", true);
            var cgo = coll.Emit(root.transform, "Colisao", false);
            if (cgo != null)
            {
                var mr = cgo.GetComponent<MeshRenderer>(); if (mr) Destroy(mr);
                cgo.AddComponent<MeshCollider>().sharedMesh = cgo.GetComponent<MeshFilter>().sharedMesh;
            }
        }

        // ---------------- relevo ----------------
        void BuildTerrainChunk(Vector2Int id, Transform parent)
        {
            float C = map.ChunkSize, step = 4f;
            int n = Mathf.RoundToInt(C / step) + 1;
            var v = new Vector3[n * n]; var nr = new Vector3[n * n]; var uv = new Vector2[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float x = id.x * C + i * step, z = id.y * C + j * step;
                    float h = map.Height(x, z);
                    v[j * n + i] = new Vector3(x, h, z);
                    float hx = map.Height(x + 2, z) - map.Height(x - 2, z), hz = map.Height(x, z + 2) - map.Height(x, z - 2);
                    nr[j * n + i] = new Vector3(-hx, 4f, -hz).normalized;
                    uv[j * n + i] = GroundUV(x, z);
                }
            var tri = new int[(n - 1) * (n - 1) * 6]; int k = 0;
            for (int j = 0; j < n - 1; j++)
                for (int i = 0; i < n - 1; i++)
                {
                    int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                    tri[k++] = a; tri[k++] = c; tri[k++] = d; tri[k++] = a; tri[k++] = d; tri[k++] = b;
                }
            var m = new Mesh { name = "relevo" };
            m.vertices = v; m.normals = nr; m.uv = uv; m.triangles = tri; m.RecalculateBounds();
            var go = new GameObject("Relevo");
            go.layer = GroundLayer;   // chão: a carreta não "bate" nele
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = ground; r.shadowCastingMode = ShadowCastingMode.Off;
            go.AddComponent<MeshCollider>().sharedMesh = m;
        }

        Vector2 GroundUV(float x, float z) => new Vector2((x - groundRect.xMin) / groundRect.width, (z - groundRect.yMin) / groundRect.height);

        void BuildFarTerrain()
        {
            // a cidade inteira em baixa resolução (horizonte), um pouco abaixo do relevo detalhado
            var B = map.Bounds; float step = 48f;
            int nx = Mathf.FloorToInt(B.width / step) + 1, nz = Mathf.FloorToInt(B.height / step) + 1;
            var v = new Vector3[nx * nz]; var uv = new Vector2[nx * nz];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    float x = B.xMin + i * step, z = B.yMin + j * step;
                    v[j * nx + i] = new Vector3(x, map.Height(x, z) - 1.5f, z);
                    uv[j * nx + i] = GroundUV(x, z);
                }
            var tri = new int[(nx - 1) * (nz - 1) * 6]; int k = 0;
            for (int j = 0; j < nz - 1; j++)
                for (int i = 0; i < nx - 1; i++)
                {
                    int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                    tri[k++] = a; tri[k++] = c; tri[k++] = d; tri[k++] = a; tri[k++] = d; tri[k++] = b;
                }
            var m = new Mesh { name = "horizonte", indexFormat = IndexFormat.UInt32 };
            m.vertices = v; m.uv = uv; m.triangles = tri; m.RecalculateNormals(); m.RecalculateBounds();
            var go = new GameObject("Horizonte");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = ground; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            // chão "de segurança" bem abaixo de tudo: ninguém cai no vazio
            var floor = new GameObject("PisoSeguranca");
            floor.layer = GroundLayer;
            floor.transform.SetParent(transform, false);
            floor.transform.position = new Vector3(B.center.x, -40, B.center.y);
            floor.AddComponent<BoxCollider>().size = new Vector3(B.width + 2000, 1, B.height + 2000);
        }

        // ---------------- ruas ----------------
        // tipos na mesma ordem do build_map.py
        static readonly string[] RoadTypes = { "motorway", "trunk", "primary", "secondary", "tertiary", "unclassified", "residential", "living_street", "service", "pedestrian", "footway", "path", "steps", "track", "cycleway" };
        void AddRoad(MultiMesh mb, PgMap.Road r)
        {
            string t = r.type < RoadTypes.Length ? RoadTypes[r.type] : "residential";
            bool foot = t == "footway" || t == "path" || t == "steps" || t == "cycleway" || t == "pedestrian";
            float rank = t == "primary" || t == "trunk" || t == "motorway" ? 4 : t == "secondary" ? 3 : t == "tertiary" ? 2 : 1;
            if (r.sidewalk && !foot)
                AddRibbon(mb, sidewalk, r.pts, r.width + 4.4f, 0.07f, false, (r.width + 4.4f) * 0.5f);
            Material m = foot ? sidewalk : r.unpaved || t == "track" ? dirt : asphalt;
            AddRibbon(mb, m, r.pts, r.width, foot ? 0.1f : 0.11f + rank * 0.006f, !foot && !r.unpaved, foot ? 3f : 20f);
        }

        void AddRibbon(MultiMesh mb, Material mat, Vector2[] pts, float width, float lift, bool lanes, float vScale)
        {
            if (pts.Length < 2) return;
            var L = mb.Get(mat);
            int start = L.v.Count; float dist = 0; float hw = width / 2;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 dir;
                if (i == 0) dir = pts[1] - pts[0];
                else if (i == pts.Length - 1) dir = pts[i] - pts[i - 1];
                else dir = (pts[i + 1] - pts[i]).normalized + (pts[i] - pts[i - 1]).normalized;
                if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;
                dir.Normalize();
                var nrm = new Vector2(-dir.y, dir.x);
                if (i > 0) dist += Vector2.Distance(pts[i], pts[i - 1]);
                var pl = pts[i] + nrm * hw; var pr = pts[i] - nrm * hw;
                L.v.Add(new Vector3(pl.x, map.Height(pl.x, pl.y) + lift, pl.y));
                L.v.Add(new Vector3(pr.x, map.Height(pr.x, pr.y) + lift, pr.y));
                L.n.Add(Vector3.up); L.n.Add(Vector3.up);
                float vv = dist / vScale;
                L.uv.Add(new Vector2(lanes ? 0 : 0, vv)); L.uv.Add(new Vector2(1, vv));
                if (i > 0)
                {
                    int a = start + (i - 1) * 2, b = a + 1, c = a + 2, d = a + 3;
                    L.Tri(a, c, d, Vector3.up); L.Tri(a, d, b, Vector3.up);
                }
            }
        }

        // ---------------- construções ----------------
        void AddBuilding(MultiMesh mb, MultiMesh coll, PgMap.Building b, bool detail = false)
        {
            int n = b.pts.Length; if (n < 3) return;
            float gmin = float.MaxValue, gmax = float.MinValue;
            Vector2 cen = Vector2.zero;
            foreach (var p in b.pts) { float h = map.Height(p.x, p.y); gmin = Mathf.Min(gmin, h); gmax = Mathf.Max(gmax, h); cen += p; }
            cen /= n;
            float baseY = gmin - 0.6f, top = gmax + b.height;
            bool house = b.kind == 0, ind = b.kind == 4;
            if (house && detail && b.height < 9f && KenneyHouse(mb, coll, b, gmin)) return;
            Material wall = WallMat(b.kind, b.color);
            var W = mb.Get(wall); var Cc = coll.Get(wall);
            // lojas no térreo: comércio sempre, metade dos prédios de apartamento
            bool shop = b.kind == 2 || (b.kind == 1 && (b.color & 1) == 0 && b.height < 40f);
            var S = shop ? mb.Get(ShopMat(b.color)) : null;
            float per = 0;
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = b.pts[i], p1 = b.pts[(i + 1) % n];
                float len = Vector2.Distance(p0, p1); if (len < 0.05f) continue;
                var outN = new Vector3(p1.y - p0.y, 0, -(p1.x - p0.x)).normalized;
                if (Vector2.Dot(new Vector2(outN.x, outN.z), (p0 + p1) / 2 - cen) < 0) outN = -outN;
                var a = new Vector3(p0.x, baseY, p0.y); var bb = new Vector3(p1.x, baseY, p1.y);
                var c = new Vector3(p1.x, top, p1.y); var d = new Vector3(p0.x, top, p0.y);
                // a textura tem 4 x 4 janelas: cada janela = um vão de 3,2 m (3,6 m na casa) x um andar
                float cell = house ? 3.6f : 3.2f, floorH = ind ? 7f : 3.1f;
                float u0 = per / cell / 4f, u1 = (per + len) / cell / 4f, v0 = (baseY - gmin) / floorH / 4f, v1 = (top - gmin) / floorH / 4f;
                if (ind) { u0 = u1 = 0.02f; v0 = v1 = 0.02f; }
                if (shop && shopTex != null && top - gmin > 6.5f)
                {
                    // térreo com lojas e os andares de cima com janelas
                    float gy = gmin + 3.6f;
                    var am = new Vector3(p0.x, gy, p0.y); var bm = new Vector3(p1.x, gy, p1.y);
                    S.Quad(a, bb, bm, am, outN, new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u1, 1f), new Vector2(u0, 1f));
                    float vg = (gy - gmin) / floorH / 4f;
                    W.Quad(am, bm, c, d, outN, new Vector2(u0, vg), new Vector2(u1, vg), new Vector2(u1, v1), new Vector2(u0, v1));
                }
                else W.Quad(a, bb, c, d, outN, new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1));
                Cc.Quad(a, bb, c, d, outN, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                per += len;
            }
            // telhado
            if (b.roof == 1 && n == 4) AddGable(mb, b.pts, top, b.color);
            else
            {
                var R = mb.Get(roofFlat); var Rc = coll.Get(roofFlat);
                if (b.tris != null && b.tris.Length >= 3)
                    for (int i = 0; i + 2 < b.tris.Length; i += 3)
                    {
                        var p0 = b.pts[b.tris[i]]; var p1 = b.pts[b.tris[i + 1]]; var p2 = b.pts[b.tris[i + 2]];
                        var A = new Vector3(p0.x, top, p0.y); var B = new Vector3(p1.x, top, p1.y); var Cv = new Vector3(p2.x, top, p2.y);
                        R.TriV(A, B, Cv, Vector3.up, new Vector2(p0.x, p0.y) * 0.25f, new Vector2(p1.x, p1.y) * 0.25f, new Vector2(p2.x, p2.y) * 0.25f);
                        Rc.TriV(A, B, Cv, Vector3.up, Vector2.zero, Vector2.zero, Vector2.zero);
                    }
                // caixa d'água/platibanda nos prédios altos
                if (b.kind == 1 && b.height > 15)
                {
                    var s = 2.2f; var cc = new Vector3(cen.x, top, cen.y);
                    BoxTo(R, cc + Vector3.up * 1.2f, new Vector3(s, 2.4f, s));
                }
            }
        }

        void AddGable(MultiMesh mb, Vector2[] p, float top, int color)
        {
            Vector2 e0 = p[1] - p[0], e1 = p[2] - p[1];
            Vector2[] q = p;
            if (e1.magnitude > e0.magnitude) q = new[] { p[1], p[2], p[3], p[0] };
            // beiral de 40 cm
            Vector2 cen = (q[0] + q[1] + q[2] + q[3]) / 4;
            var o = new Vector2[4]; for (int i = 0; i < 4; i++) o[i] = cen + (q[i] - cen) * 1.0f + (q[i] - cen).normalized * 0.5f;
            float span = Vector2.Distance(q[1], q[2]);
            float rh = Mathf.Clamp(span * 0.32f, 1.2f, 3.2f);
            Vector2 m0 = (o[0] + o[3]) / 2, m1 = (o[1] + o[2]) / 2;
            Vector3 P(Vector2 v, float y) => new Vector3(v.x, y, v.y);
            float ey = top - 0.15f;
            var rt = color % 3 == 0 ? roofTile2 : roofTile;
            var R = mb.Get(rt);
            Vector3 r0 = P(m0, top + rh), r1 = P(m1, top + rh);
            Vector2 side1 = ((o[0] + o[1]) / 2 - cen).normalized, side2 = ((o[2] + o[3]) / 2 - cen).normalized;
            var hint1 = new Vector3(side1.x, 1.2f, side1.y); var hint2 = new Vector3(side2.x, 1.2f, side2.y);
            float L = Vector2.Distance(o[0], o[1]);
            float sl = Mathf.Sqrt(rh * rh + span * span / 4f) / 3f;   // telha: uma repetição a cada 3 m
            R.Quad(P(o[0], ey), P(o[1], ey), r1, r0, hint1, new Vector2(0, 0), new Vector2(L / 3f, 0), new Vector2(L / 3f, sl), new Vector2(0, sl));
            R.Quad(P(o[2], ey), P(o[3], ey), r0, r1, hint2, new Vector2(0, 0), new Vector2(L / 3f, 0), new Vector2(L / 3f, sl), new Vector2(0, sl));
            // oitões (triângulos das pontas) na cor da parede
            var W = mb.Get(WallMat(0, color));
            Vector2 g1 = (m0 - cen).normalized, g2 = (m1 - cen).normalized;
            W.TriV(P(o[3], ey), P(o[0], ey), r0, new Vector3(g1.x, 0, g1.y), Vector2.zero, new Vector2(0.2f, 0), new Vector2(0.1f, 0.2f));
            W.TriV(P(o[1], ey), P(o[2], ey), r1, new Vector3(g2.x, 0, g2.y), Vector2.zero, new Vector2(0.2f, 0), new Vector2(0.1f, 0.2f));
        }

        static void BoxTo(MultiMesh.Layer L, Vector3 c, Vector3 s)
        {
            Vector3 h = s / 2;
            Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back, Vector3.up };
            foreach (var d in dirs)
            {
                Vector3 u = Mathf.Abs(d.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 w = Vector3.Cross(d, u);
                Vector3 fc = c + Vector3.Scale(d, h);
                Vector3 hu = Vector3.Scale(u, h), hw = Vector3.Scale(w, h);
                hu = u * Mathf.Abs(Vector3.Dot(hu, u)); hw = w * Mathf.Abs(Vector3.Dot(hw, w));
                L.Quad(fc - hu - hw, fc - hu + hw, fc + hu + hw, fc + hu - hw, d, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            }
        }

        Material WallMat(int kind, int color)
        {
            int fac = kind == 0 ? 0 : kind == 4 ? 3 : (kind == 2 ? 2 : 1);
            int key = fac * 1000 + color % 8;
            if (wallMats.TryGetValue(key, out var m)) return m;
            Color c = fac == 0 ? HouseCols[color % HouseCols.Length] : fac == 1 ? AptCols[color % AptCols.Length] : fac == 2 ? ShopCols[color % ShopCols.Length] : new Color(0.72f, 0.74f, 0.76f);
            if (fac == 3) { m = Mats.Lit(c, 0.35f, 0.3f); }
            else
            {
                m = Mats.Glow(new Color(1f, 0.85f, 0.55f), 1.5f, true);
                m.SetColor("_BaseColor", c);
                bool alt = (color / 8) % 2 == 1;
                var bt = fac == 0 ? (alt ? winHouse2 : winHouse) : (alt ? winApt2 : winApt);
                m.SetTexture("_BaseMap", bt);
                m.SetTexture("_EmissionMap", fac == 0 ? (alt && winHouse2Glow ? winHouse2Glow : winHouseGlow) : (alt && winApt2Glow ? winApt2Glow : winAptGlow));
                m.SetFloat("_Smoothness", 0.25f);
            }
            wallMats[key] = m;
            return m;
        }

        /// <summary>Térreo de loja (vitrine, toldo e letreiro) na cor do prédio.</summary>
        Material ShopMat(int color)
        {
            int key = 9000 + color % 8;
            if (wallMats.TryGetValue(key, out var m)) return m;
            m = Mats.Glow(new Color(1f, 0.9f, 0.7f), 1.2f, true);
            m.SetColor("_BaseColor", Color.Lerp(Color.white, AptCols[color % AptCols.Length], 0.5f));
            m.SetTexture("_BaseMap", shopTex); m.SetTexture("_EmissionMap", shopGlow);
            m.SetFloat("_Smoothness", 0.35f);
            wallMats[key] = m;
            return m;
        }

        // ---------------- árvores ----------------
        void AddTree(MultiMesh mb, Vector2 p, int kind)
        {
            float h = map.Height(p.x, p.y);
            float s = 0.8f + Mathf.Abs(Mathf.Sin(p.x * 12.9898f + p.y * 78.233f)) * 0.6f;
            float rot = (p.x * 31.7f + p.y * 17.3f) % 360f;
            var mtx = Matrix4x4.TRS(new Vector3(p.x, h - 0.2f, p.y), Quaternion.Euler(0, rot, 0), Vector3.one * s);
            var mesh = kind == 0 ? treeArauc : treeRound;
            // submalhas: 0 = tronco, 1 = copa
            var T = mb.Get(trunkMat); var Lf = mb.Get(kind == 0 ? araucariaMat : leafMat);
            T.AddMesh(mesh, 0, mtx); Lf.AddMesh(mesh, 1, mtx);
        }

        static Mesh MakeAraucaria()
        {
            // a árvore símbolo do Paraná: tronco alto e copa em "taça"
            var tb = new MultiMesh.Layer(); var lb = new MultiMesh.Layer();
            Cyl(tb, Vector3.zero, 0.35f, 0.22f, 13f, 6);
            for (int k = 0; k < 3; k++)
            {
                float y = 10.5f + k * 1.3f, r = 4.2f - k * 0.9f;
                Disc(lb, new Vector3(0, y, 0), r, 0.9f, 9, k * 20f);
            }
            for (int a = 0; a < 5; a++)
            {
                float ang = a * 72f * Mathf.Deg2Rad;
                Disc(lb, new Vector3(Mathf.Cos(ang) * 2.6f, 11.2f + (a % 2) * 0.5f, Mathf.Sin(ang) * 2.6f), 1.6f, 0.6f, 6, a * 10f);
            }
            return MultiMesh.ToMesh(tb, lb);
        }

        static Mesh MakeRoundTree()
        {
            var tb = new MultiMesh.Layer(); var lb = new MultiMesh.Layer();
            Cyl(tb, Vector3.zero, 0.28f, 0.2f, 3.2f, 6);
            Blob(lb, new Vector3(0, 4.4f, 0), new Vector3(2.6f, 2.2f, 2.6f));
            Blob(lb, new Vector3(1.0f, 5.2f, 0.4f), new Vector3(1.7f, 1.5f, 1.7f));
            Blob(lb, new Vector3(-0.8f, 5.0f, -0.6f), new Vector3(1.6f, 1.4f, 1.6f));
            return MultiMesh.ToMesh(tb, lb);
        }

        static void Cyl(MultiMesh.Layer L, Vector3 b, float r0, float r1, float h, int seg)
        {
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                L.Quad(b + d0 * r0, b + d1 * r0, b + d1 * r1 + Vector3.up * h, b + d0 * r1 + Vector3.up * h, (d0 + d1).normalized, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            }
        }

        static void Disc(MultiMesh.Layer L, Vector3 c, float r, float th, int seg, float rotDeg)
        {
            // cone achatado (copa em camadas)
            var top = c + Vector3.up * th; var bot = c - Vector3.up * th * 0.3f;
            for (int i = 0; i < seg; i++)
            {
                float a0 = (i * 360f / seg + rotDeg) * Mathf.Deg2Rad, a1 = ((i + 1) * 360f / seg + rotDeg) * Mathf.Deg2Rad;
                var p0 = c + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * r; var p1 = c + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * r;
                var mid = ((p0 + p1) / 2 - c).normalized;
                L.TriV(p0, p1, top, mid + Vector3.up * 1.5f, Vector2.zero, Vector2.right, Vector2.up);
                L.TriV(p1, p0, bot, mid - Vector3.up * 0.8f, Vector2.zero, Vector2.right, Vector2.up);
            }
        }

        static void Blob(MultiMesh.Layer L, Vector3 c, Vector3 s)
        {
            // icosaedro achatado: copa redonda com poucos triângulos
            float t = (1 + Mathf.Sqrt(5)) / 2;
            var v = new[] { new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0), new Vector3(0, -1, t), new Vector3(0, 1, t),
                            new Vector3(0, -1, -t), new Vector3(0, 1, -t), new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
            int[] f = { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8, 3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
            for (int i = 0; i < f.Length; i += 3)
            {
                Vector3 a = c + Vector3.Scale(v[f[i]].normalized, s), b = c + Vector3.Scale(v[f[i + 1]].normalized, s), d = c + Vector3.Scale(v[f[i + 2]].normalized, s);
                L.TriV(a, b, d, (a + b + d) / 3 - c, Vector2.zero, Vector2.right, Vector2.up);
            }
        }

        // ---------------- pontos famosos ----------------
        void BuildLandmarks()
        {
            var white = Mats.Lit(new Color(0.96f, 0.95f, 0.92f), 0.3f);
            var red = Mats.Lit(new Color(0.75f, 0.15f, 0.12f), 0.4f);
            var cream = Mats.Lit(new Color(0.95f, 0.88f, 0.72f), 0.2f);
            var tile = roofTile;
            var steel = Mats.Lit(new Color(0.35f, 0.35f, 0.38f), 0.5f, 0.6f);
            var seat = Mats.Lit(new Color(0.1f, 0.1f, 0.1f), 0.3f);
            foreach (var L in map.Landmarks)
            {
                float y = map.Height(L.pos.x, L.pos.y);
                var basePos = new Vector3(L.pos.x, y, L.pos.y);
                var root = new GameObject("Marco_" + L.kind).transform; root.SetParent(transform, false); root.position = basePos;
                switch (L.kind)
                {
                    case "catedral":
                        // torre alta e fina ao lado da igreja, com cruz no alto (bem visível de longe)
                        Part(root, PrimitiveType.Cube, new Vector3(14, 22, 0), new Vector3(3.2f, 44, 3.2f), white, true);
                        Part(root, PrimitiveType.Cube, new Vector3(14, 47, 0), new Vector3(0.5f, 5, 0.5f), white, false);
                        Part(root, PrimitiveType.Cube, new Vector3(14, 48, 0), new Vector3(2.6f, 0.5f, 0.5f), white, false);
                        var bell = Part(root, PrimitiveType.Cube, new Vector3(14, 38, 0), new Vector3(3.4f, 3, 3.4f), null, false);
                        bell.GetComponent<MeshRenderer>().sharedMaterial = Mats.Glow(new Color(1f, 0.9f, 0.6f), 2.5f, true);
                        break;
                    case "estacao":
                        // vagão antigo de passageiros em exposição, sobre trilhos
                        Part(root, PrimitiveType.Cube, new Vector3(0, 1.9f, -22), new Vector3(3, 2.6f, 14), red, true);
                        Part(root, PrimitiveType.Cube, new Vector3(0, 3.35f, -22), new Vector3(3.2f, 0.3f, 14.4f), steel, false);
                        Part(root, PrimitiveType.Cube, new Vector3(0, 0.35f, -22), new Vector3(2.4f, 0.5f, 13), steel, false);
                        for (int s = -1; s <= 1; s += 2) Part(root, PrimitiveType.Cube, new Vector3(s * 0.75f, 0.08f, -22), new Vector3(0.12f, 0.12f, 30), steel, false);
                        for (int i = 0; i < 5; i++)
                            Part(root, PrimitiveType.Cube, new Vector3(1.52f, 2.4f, -27.5f + i * 2.8f), new Vector3(0.05f, 1, 1.6f), null, false)
                                .GetComponent<MeshRenderer>().sharedMaterial = Mats.Glow(new Color(1, 0.85f, 0.5f), 1.2f, true);
                        break;
                    case "vilahilda":
                        // mansão histórica: dois andares, varanda e telhado de telhas
                        Part(root, PrimitiveType.Cube, new Vector3(0, 3.5f, 0), new Vector3(16, 7, 12), cream, true);
                        Part(root, PrimitiveType.Cube, new Vector3(0, 7.6f, 0), new Vector3(17, 1.2f, 13), tile, true);
                        Part(root, PrimitiveType.Cube, new Vector3(0, 8.8f, 0), new Vector3(13, 1.4f, 9), tile, false);
                        for (int i = -2; i <= 2; i++) Part(root, PrimitiveType.Cylinder, new Vector3(i * 3, 1.8f, -6.8f), new Vector3(0.4f, 1.8f, 0.4f), white, false);
                        break;
                    case "estadio":
                        // Germano Krüger: arquibancadas em volta do campo (Operário Ferroviário)
                        var grass = Mats.Lit(new Color(0.25f, 0.6f, 0.25f), 0.1f);
                        Part(root, PrimitiveType.Cube, new Vector3(0, 0.1f, 0), new Vector3(68, 0.2f, 105), grass, false).GetComponent<Collider>().enabled = false;
                        var black = Mats.Lit(new Color(0.08f, 0.08f, 0.08f), 0.3f);
                        for (int s = -1; s <= 1; s += 2)
                        {
                            Part(root, PrimitiveType.Cube, new Vector3(s * 44, 4, 0), new Vector3(10, 8, 100), white, true);
                            Part(root, PrimitiveType.Cube, new Vector3(s * 41, 6.5f, 0), new Vector3(5, 0.4f, 98), black, false);
                            Part(root, PrimitiveType.Cube, new Vector3(0, 3, s * 62), new Vector3(80, 6, 9), white, true);
                        }
                        for (int s = -1; s <= 1; s += 2)
                            for (int e = -1; e <= 1; e += 2)
                            {
                                Part(root, PrimitiveType.Cylinder, new Vector3(s * 50, 15, e * 60), new Vector3(0.8f, 15, 0.8f), steel, true);
                                Part(root, PrimitiveType.Cube, new Vector3(s * 50, 30, e * 60), new Vector3(5, 3, 1), null, false)
                                    .GetComponent<MeshRenderer>().sharedMaterial = Mats.Glow(Color.white, 6f, true);
                            }
                        break;
                }
            }
        }

        GameObject Part(Transform root, PrimitiveType t, Vector3 lp, Vector3 size, Material m, bool collide)
        {
            var g = GameObject.CreatePrimitive(t);
            g.transform.SetParent(root, false);
            g.transform.localPosition = lp; g.transform.localScale = size;
            if (m != null) g.GetComponent<MeshRenderer>().sharedMaterial = m;
            if (!collide) Destroy(g.GetComponent<Collider>());
            return g;
        }

        void BuildDanceFloor(Vector3 c)
        {
            float tile = 1.6f;
            for (int i = 0; i < 6; i++)
                for (int j = 0; j < 6; j++)
                {
                    var m = Mats.Glow(Color.HSVToRGB(((i + j) % 6) / 6f, 0.8f, 1f), 1.5f);
                    m.SetFloat("_Smoothness", 0.8f);
                    DanceTiles.Add(m);
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(go.GetComponent<Collider>());
                    go.transform.SetParent(transform);
                    float x = c.x + (i - 2.5f) * tile, z = c.z + (j - 2.5f) * tile;
                    go.transform.position = new Vector3(x, map.Height(x, z) + 0.12f, z);
                    go.transform.localScale = new Vector3(tile - 0.08f, 0.1f, tile - 0.08f);
                    var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = ShadowCastingMode.Off;
                }
        }

        // ---------------- materiais e texturas ----------------
        Texture2D winAptGlow, winHouseGlow, winApt2Glow, winHouse2Glow, shopTex, shopGlow, tileTex, slabTex, walkTex;
        void MakeTextures()
        {
            groundTex = Resources.Load<Texture2D>("Map/pg_ground");
            var meta = Resources.Load<TextAsset>("Map/pg_ground_meta");
            if (meta != null)
            {
                var p = meta.text.Trim().Split(' ');
                float x0 = float.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture), z0 = float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
                float x1 = float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture), z1 = float.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture);
                groundRect = Rect.MinMaxRect(x0, z0, x1, z1);
            }
            else groundRect = map.Bounds;
            var rnd = new System.Random(5);
            // texturas feitas a partir de fotos CC0 (ambientCG; tools/pg/texturas.py); se faltarem, desenha na hora
            winApt = Resources.Load<Texture2D>("City/fachada_predio"); winApt2 = Resources.Load<Texture2D>("City/fachada_predio2");
            winHouse = Resources.Load<Texture2D>("City/fachada_casa"); winHouse2 = Resources.Load<Texture2D>("City/fachada_casa2");
            winAptGlow = Resources.Load<Texture2D>("City/fachada_predio_luz"); winApt2Glow = Resources.Load<Texture2D>("City/fachada_predio2_luz");
            winHouseGlow = Resources.Load<Texture2D>("City/fachada_casa_luz"); winHouse2Glow = Resources.Load<Texture2D>("City/fachada_casa2_luz");
            shopTex = Resources.Load<Texture2D>("City/terreo_loja"); shopGlow = Resources.Load<Texture2D>("City/terreo_loja_luz");
            tileTex = Resources.Load<Texture2D>("City/telha"); slabTex = Resources.Load<Texture2D>("City/laje"); walkTex = Resources.Load<Texture2D>("City/calcada");
            asphaltTex = Resources.Load<Texture2D>("City/asfalto");
            if (winApt == null) { winApt = Facade(rnd, false, out winAptGlow, 0); winApt2 = Facade(rnd, false, out winApt2Glow, 1); }
            if (winHouse == null) { winHouse = Facade(rnd, true, out winHouseGlow, 0); winHouse2 = Facade(rnd, true, out winHouse2Glow, 1); }
            if (asphaltTex == null) asphaltTex = Noise(rnd, new Color32(52, 52, 56, 255), 14, true);
            dirtTex = Noise(rnd, new Color32(150, 112, 78, 255), 26, false);
        }

        static Texture2D Noise(System.Random rnd, Color32 baseC, int amp, bool lane)
        {
            const int W = 64, H = 256;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int n = rnd.Next(-amp, amp);
                    var c = new Color32((byte)Mathf.Clamp(baseC.r + n, 0, 255), (byte)Mathf.Clamp(baseC.g + n, 0, 255), (byte)Mathf.Clamp(baseC.b + n, 0, 255), 255);
                    if (lane && x >= 30 && x <= 33 && (y % 128) < 48) c = new Color32(235, 235, 225, 255);   // faixa central tracejada
                    if (lane && (x <= 1 || x >= 62)) c = new Color32(200, 200, 195, 255);                    // borda
                    px[y * W + x] = c;
                }
            t.SetPixels32(px); t.Apply(true);
            return t;
        }

        static Texture2D Facade(System.Random rnd, bool house, out Texture2D glow, int variant)
        {
            const int S = 256, C = 64;
            var b = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            glow = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 2 };
            var pb = new Color32[S * S]; var pg = new Color32[S * S];
            var lit = new bool[16]; for (int i = 0; i < 16; i++) lit[i] = rnd.NextDouble() < 0.5;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int cx = x % C, cy = y % C, cell = (y / C) * 4 + x / C;
                    bool win, frame, door = false;
                    if (house)
                    {
                        // casa: janela larga com veneziana; na primeira fileira, porta em uma das células
                        win = cx >= 10 && cx < 54 && cy >= 22 && cy < 50;
                        door = (y / C) == 0 && (x / C) % 3 == variant && cx >= 22 && cx < 42 && cy >= 2 && cy < 46;
                        frame = !win && cx >= 8 && cx < 56 && cy >= 20 && cy < 52;
                        if (door) win = false;
                    }
                    else
                    {
                        win = cx >= 12 + variant * 4 && cx < 52 - variant * 4 && cy >= 16 && cy < 52;
                        frame = !win && cx >= 10 && cx < 54 && cy >= 14 && cy < 54;
                    }
                    byte wall = (byte)(236 - (y % 2) * 5);
                    Color32 c = new Color32(wall, wall, wall, 255);
                    if (frame) c = house ? new Color32(120, 80, 50, 255) : new Color32(250, 250, 250, 255);
                    if (win) c = house && (cx < 14 || cx > 50) ? new Color32(90, 60, 40, 255) : new Color32(60, 80, 100, 255);
                    if (door) c = new Color32(110, 70, 40, 255);
                    pb[y * S + x] = c;
                    float sh = win ? 1f - Mathf.Abs(cx - 32) / 40f : 0f;
                    pg[y * S + x] = win && lit[cell] ? new Color32((byte)(255 * sh), (byte)(215 * sh), (byte)(140 * sh), 255) : new Color32(0, 0, 0, 255);
                }
            b.SetPixels32(pb); b.Apply(true); glow.SetPixels32(pg); glow.Apply(true);
            return b;
        }

        void MakeMaterials()
        {
            ground = new Material(Resources.Load<Material>("Materials/Lit"));
            ground.SetTexture("_BaseMap", groundTex); ground.SetColor("_BaseColor", Color.white); ground.SetFloat("_Smoothness", 0.05f);
            asphalt = Mats.Lit(Color.white, 0.4f); asphalt = new Material(asphalt); asphalt.SetTexture("_BaseMap", asphaltTex);
            dirt = new Material(Mats.Lit(Color.white, 0.05f)); dirt.SetTexture("_BaseMap", dirtTex);
            sidewalk = new Material(Mats.Lit(new Color(0.9f, 0.88f, 0.84f), 0.15f)); if (walkTex) sidewalk.SetTexture("_BaseMap", walkTex);
            water = Mats.Lit(new Color(0.18f, 0.42f, 0.62f), 0.92f);
            roofTile = new Material(Mats.Lit(tileTex ? Color.white : new Color(0.72f, 0.33f, 0.2f), 0.2f)); if (tileTex) roofTile.SetTexture("_BaseMap", tileTex);
            roofTile2 = new Material(Mats.Lit(tileTex ? new Color(0.8f, 0.72f, 0.7f) : new Color(0.55f, 0.27f, 0.2f), 0.2f)); if (tileTex) roofTile2.SetTexture("_BaseMap", tileTex);
            roofFlat = new Material(Mats.Lit(new Color(0.8f, 0.8f, 0.8f), 0.15f)); if (slabTex) roofFlat.SetTexture("_BaseMap", slabTex);
            trunkMat = Mats.Lit(new Color(0.38f, 0.26f, 0.17f), 0.1f);
            leafMat = Mats.Lit(new Color(0.24f, 0.52f, 0.22f), 0.1f);
            araucariaMat = Mats.Lit(new Color(0.12f, 0.33f, 0.16f), 0.1f);
        }
    }

    /// <summary>Junta triângulos por material e gera uma malha com submalhas (uma por material).</summary>
    public class MultiMesh
    {
        static readonly Dictionary<Mesh, (Vector3[], Vector3[], Vector2[], int[][])> cache = new Dictionary<Mesh, (Vector3[], Vector3[], Vector2[], int[][])>();
        public class Layer
        {
            public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();

            /// <summary>Triângulo com a frente virada para "facing".</summary>
            public void Tri(int a, int b, int c, Vector3 facing)
            {
                var nn = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                if (Vector3.Dot(nn, facing) < 0) { t.Add(a); t.Add(c); t.Add(b); } else { t.Add(a); t.Add(b); t.Add(c); }
            }
            public void TriV(Vector3 a, Vector3 b, Vector3 c, Vector3 facing, Vector2 ua, Vector2 ub, Vector2 uc)
            {
                int s = v.Count;
                var nn = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(nn, facing) < 0) nn = -nn;
                v.Add(a); v.Add(b); v.Add(c); n.Add(nn); n.Add(nn); n.Add(nn); uv.Add(ua); uv.Add(ub); uv.Add(uc);
                Tri(s, s + 1, s + 2, facing);
            }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int s = v.Count;
                var nn = Vector3.Cross(b - a, d - a).normalized;
                if (Vector3.Dot(nn, facing) < 0) nn = -nn;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                for (int i = 0; i < 4; i++) n.Add(nn);
                uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
                Tri(s, s + 1, s + 2, facing); Tri(s, s + 2, s + 3, facing);
            }
            public void AddMesh(Mesh m, int sub, Matrix4x4 mtx)
            {
                if (!cache.TryGetValue(m, out var cd)) { cd = (m.vertices, m.normals, m.uv, new[] { m.GetTriangles(0), m.subMeshCount > 1 ? m.GetTriangles(1) : new int[0] }); cache[m] = cd; }
                var mv = cd.Item1; var mn = cd.Item2; var mu = cd.Item3; var mt = cd.Item4[sub];
                var map = new Dictionary<int, int>();
                foreach (int idx in mt)
                {
                    if (!map.TryGetValue(idx, out int ni))
                    {
                        ni = v.Count; map[idx] = ni;
                        v.Add(mtx.MultiplyPoint3x4(mv[idx])); n.Add(mtx.MultiplyVector(mn[idx]).normalized); uv.Add(mu.Length > idx ? mu[idx] : Vector2.zero);
                    }
                    t.Add(ni);
                }
            }
        }

        readonly Dictionary<Material, Layer> layers = new Dictionary<Material, Layer>();
        public Layer Get(Material m) { if (!layers.TryGetValue(m, out var l)) layers[m] = l = new Layer(); return l; }

        public GameObject Emit(Transform parent, string name, bool shadows)
        {
            var mats = new List<Material>(); var subs = new List<Layer>();
            foreach (var kv in layers) if (kv.Value.t.Count > 0) { mats.Add(kv.Key); subs.Add(kv.Value); }
            if (subs.Count == 0) return null;
            var vv = new List<Vector3>(); var nn = new List<Vector3>(); var uu = new List<Vector2>();
            var offs = new List<int>();
            foreach (var l in subs) { offs.Add(vv.Count); vv.AddRange(l.v); nn.AddRange(l.n); uu.AddRange(l.uv); }
            var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            m.SetVertices(vv); m.SetNormals(nn); m.SetUVs(0, uu);
            m.subMeshCount = subs.Count;
            for (int i = 0; i < subs.Count; i++)
            {
                var tri = new int[subs[i].t.Count];
                for (int k = 0; k < tri.Length; k++) tri[k] = subs[i].t[k] + offs[i];
                m.SetTriangles(tri, i, false);
            }
            m.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats.ToArray();
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        public static Mesh ToMesh(params Layer[] subs)
        {
            var vv = new List<Vector3>(); var nn = new List<Vector3>(); var uu = new List<Vector2>(); var offs = new List<int>();
            foreach (var l in subs) { offs.Add(vv.Count); vv.AddRange(l.v); nn.AddRange(l.n); uu.AddRange(l.uv); }
            var m = new Mesh(); m.SetVertices(vv); m.SetNormals(nn); m.SetUVs(0, uu); m.subMeshCount = subs.Length;
            for (int i = 0; i < subs.Length; i++)
            {
                var tri = new int[subs[i].t.Count];
                for (int k = 0; k < tri.Length; k++) tri[k] = subs[i].t[k] + offs[i];
                m.SetTriangles(tri, i);
            }
            m.RecalculateBounds();
            return m;
        }
    }
}
