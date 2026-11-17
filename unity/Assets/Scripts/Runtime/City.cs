using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CarretaJoy
{
    /// <summary>
    /// Cidade procedural: ruas de asfalto em grade, quarteirões com prédios coloridos
    /// (janelas acendem à noite), árvores, postes e uma praça com pista de dança no centro.
    /// A geometria é juntada por material e por região (poucas chamadas de desenho no celular).
    /// </summary>
    public class City : MonoBehaviour
    {
        public const int N = 5;            // quarteirões por lado
        public const float BLOCK = 36f, ROAD = 12f, P = BLOCK + ROAD;
        public static float Half => N * P / 2f;

        public readonly List<Vector3> Ring = new List<Vector3>();   // percurso da carreta
        public Vector3 PlazaCenter;
        public readonly List<Material> DanceTiles = new List<Material>();

        readonly Dictionary<(Material, int, int), List<CombineInstance>> batches = new Dictionary<(Material, int, int), List<CombineInstance>>();
        Mesh cube, cyl, sphere, quad;
        System.Random rnd;
        Transform colliders;
        Texture2D winBase, winGlow;

        static readonly Color[] Palette = {
            new Color(0.98f,0.80f,0.35f), new Color(0.95f,0.55f,0.45f), new Color(0.55f,0.80f,0.90f),
            new Color(0.70f,0.88f,0.62f), new Color(0.98f,0.95f,0.88f), new Color(0.90f,0.65f,0.85f),
            new Color(0.75f,0.75f,0.80f), new Color(1.00f,0.70f,0.30f), new Color(0.60f,0.70f,0.95f)
        };

        public void Build(int seed)
        {
            rnd = new System.Random(seed);
            cube = Prim(PrimitiveType.Cube); cyl = Prim(PrimitiveType.Cylinder);
            sphere = Prim(PrimitiveType.Sphere); quad = Prim(PrimitiveType.Quad);
            colliders = new GameObject("Colisores").transform; colliders.SetParent(transform);
            MakeWindowTextures();

            float H = Half, ext = H + ROAD / 2f;
            var grass = Mats.Lit(new Color(0.36f, 0.62f, 0.30f), 0.05f);
            var asphalt = Mats.Lit(new Color(0.16f, 0.16f, 0.18f), 0.45f);
            var walk = Mats.Lit(new Color(0.78f, 0.76f, 0.72f), 0.15f);
            var paint = Mats.Lit(new Color(0.95f, 0.95f, 0.92f), 0.3f);
            var yellow = Mats.Lit(new Color(1f, 0.82f, 0.15f), 0.3f);

            // chão e asfalto
            Add(quad, Matrix4x4.TRS(new Vector3(0, -0.03f, 0), Quaternion.Euler(90, 0, 0), new Vector3(900, 900, 1)), grass, false);
            Add(quad, Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(90, 0, 0), new Vector3(ext * 2, ext * 2, 1)), asphalt, false);
            Box(new Vector3(0, -0.5f, 0), new Vector3(2000, 1, 2000)); // chão físico

            // faixas
            for (int i = 0; i <= N; i++)
            {
                float c = i * P - H;
                for (float s = -H; s < H; s += P)
                    for (float d = s + ROAD / 2f + 2f; d < s + P - ROAD / 2f - 2f; d += 6f)
                    {
                        bool outer = i == 0 || i == N;
                        var m = outer ? yellow : paint;
                        Add(cube, Matrix4x4.TRS(new Vector3(c, 0.01f, d + 1.5f), Quaternion.identity, new Vector3(0.22f, 0.02f, 3f)), m, false);
                        Add(cube, Matrix4x4.TRS(new Vector3(d + 1.5f, 0.01f, c), Quaternion.identity, new Vector3(3f, 0.02f, 0.22f)), m, false);
                    }
            }
            // faixas de pedestre perto dos cruzamentos
            for (int i = 0; i <= N; i++)
                for (int j = 0; j <= N; j++)
                {
                    float x = i * P - H, z = j * P - H;
                    for (int k = -4; k <= 4; k++)
                    {
                        if (j < N) Add(cube, Matrix4x4.TRS(new Vector3(x + k * 1.1f, 0.012f, z + ROAD / 2f + 1.3f), Quaternion.identity, new Vector3(0.55f, 0.02f, 2.2f)), paint, false);
                        if (i < N) Add(cube, Matrix4x4.TRS(new Vector3(x + ROAD / 2f + 1.3f, 0.012f, z + k * 1.1f), Quaternion.identity, new Vector3(2.2f, 0.02f, 0.55f)), paint, false);
                    }
                }

            // quarteirões
            int mid = N / 2;
            for (int bx = 0; bx < N; bx++)
                for (int bz = 0; bz < N; bz++)
                {
                    float x0 = bx * P - H + ROAD / 2f, z0 = bz * P - H + ROAD / 2f;
                    var center = new Vector3(x0 + BLOCK / 2f, 0, z0 + BLOCK / 2f);
                    // calçada (meio-fio de 18 cm: dá para subir andando)
                    Add(cube, Matrix4x4.TRS(center + Vector3.up * 0.09f, Quaternion.identity, new Vector3(BLOCK, 0.18f, BLOCK)), walk, true);
                    Box(center + Vector3.up * 0.09f, new Vector3(BLOCK, 0.18f, BLOCK));
                    if (bx == mid && bz == mid) { Plaza(center); continue; }
                    Buildings(center, Vector3.Distance(center, Vector3.zero));
                    Street(center);
                }

            // percurso da carreta: avenida de fora, mão direita, cantos arredondados
            float r = H - 3f, rad = 10f;
            var corners = new[] { new Vector3(-r, 0, -r), new Vector3(-r, 0, r), new Vector3(r, 0, r), new Vector3(r, 0, -r) };
            for (int c = 0; c < 4; c++)
            {
                Vector3 a = corners[c], b = corners[(c + 1) % 4], n = corners[(c + 2) % 4];
                Vector3 dir = (b - a).normalized, dir2 = (n - b).normalized;
                for (float t = rad; t < (b - a).magnitude - rad; t += 4f) Ring.Add(a + dir * t);
                Vector3 p0 = b - dir * rad, p2 = b + dir2 * rad;
                for (int k = 0; k <= 6; k++)
                {
                    float u = k / 6f;
                    Ring.Add((1 - u) * (1 - u) * p0 + 2 * (1 - u) * u * b + u * u * p2);
                }
            }

            Flush();
        }

        // ---------- partes ----------
        void Buildings(Vector3 c, float distFromCenter)
        {
            float lot = BLOCK / 3f;
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    if (i == 1 && j == 1) continue; // miolo do quarteirão
                    if (rnd.NextDouble() < 0.12) continue; // terreno vazio
                    float w = lot - 1.5f - (float)rnd.NextDouble() * 1.5f, d = lot - 1.5f - (float)rnd.NextDouble() * 1.5f;
                    float near = Mathf.Clamp01(1f - distFromCenter / (Half * 1.3f));
                    float h = 3f * Mathf.Round(2 + (float)rnd.NextDouble() * (3 + 9 * near));
                    var p = c + new Vector3((i - 1) * lot, 0.18f, (j - 1) * lot);
                    var col = Palette[rnd.Next(Palette.Length)];
                    BuildingMesh(p, w, h, d, BuildingMat(col));
                    Box(p + Vector3.up * h / 2f, new Vector3(w, h, d));
                    // marquise/telhado
                    var roof = Mats.Lit(col * 0.75f, 0.2f);
                    Add(cube, Matrix4x4.TRS(p + Vector3.up * (h + 0.25f), Quaternion.identity, new Vector3(w + 0.4f, 0.5f, d + 0.4f)), roof, true);
                }
        }

        readonly Dictionary<Color, Material> bmats = new Dictionary<Color, Material>();
        Material BuildingMat(Color c)
        {
            if (bmats.TryGetValue(c, out var m)) return m;
            m = Mats.Glow(new Color(1f, 0.85f, 0.55f), 1.6f, true);
            m.SetColor("_BaseColor", c);
            m.SetTexture("_BaseMap", winBase);
            m.SetTexture("_EmissionMap", winGlow);
            m.SetFloat("_Smoothness", 0.35f);
            bmats[c] = m;
            return m;
        }

        void Street(Vector3 c)
        {
            float e = BLOCK / 2f - 1.2f;
            var trunk = Mats.Lit(new Color(0.40f, 0.27f, 0.17f), 0.1f);
            var leaf = Mats.Lit(new Color(0.22f, 0.55f, 0.22f), 0.1f);
            var leaf2 = Mats.Lit(new Color(0.30f, 0.62f, 0.25f), 0.1f);
            var pole = Mats.Lit(new Color(0.25f, 0.27f, 0.30f), 0.5f, 0.6f);
            for (int s = 0; s < 4; s++)
            {
                Quaternion q = Quaternion.Euler(0, s * 90, 0);
                for (float t = -e + 3; t < e - 2; t += 10f)
                {
                    var p = c + q * new Vector3(t, 0.18f, e);
                    if (((int)(t + 100) / 10) % 2 == 0)
                    {
                        float hs = 0.8f + (float)rnd.NextDouble() * 0.5f;
                        Add(cyl, Matrix4x4.TRS(p + Vector3.up * 1.3f * hs, Quaternion.identity, new Vector3(0.25f, 1.3f * hs, 0.25f)), trunk, true);
                        Add(sphere, Matrix4x4.TRS(p + Vector3.up * 3.3f * hs, Quaternion.identity, Vector3.one * 2.8f * hs), rnd.NextDouble() < 0.5 ? leaf : leaf2, true);
                        Add(sphere, Matrix4x4.TRS(p + new Vector3(0.6f, 3.9f, 0.3f) * hs, Quaternion.identity, Vector3.one * 1.9f * hs), leaf2, true);
                        Box(p + Vector3.up * 1.3f, new Vector3(0.4f, 2.6f, 0.4f));
                    }
                    else
                    {
                        Add(cyl, Matrix4x4.TRS(p + Vector3.up * 3f, Quaternion.identity, new Vector3(0.14f, 3f, 0.14f)), pole, true);
                        var arm = q * Vector3.forward;
                        Add(cube, Matrix4x4.TRS(p + Vector3.up * 5.9f + arm * 0.8f, q, new Vector3(0.1f, 0.1f, 1.6f)), pole, true);
                        Add(cube, Matrix4x4.TRS(p + Vector3.up * 5.75f + arm * 1.5f, q, new Vector3(0.45f, 0.18f, 0.7f)), LampMat, false);
                        Box(p + Vector3.up * 3f, new Vector3(0.3f, 6f, 0.3f));
                    }
                }
            }
        }

        Material lampMat;
        Material LampMat => lampMat != null ? lampMat : (lampMat = Mats.Glow(new Color(1f, 0.8f, 0.5f), 6f, true));

        void Plaza(Vector3 c)
        {
            PlazaCenter = c + Vector3.up * 0.18f;
            var grass = Mats.Lit(new Color(0.33f, 0.66f, 0.30f), 0.05f);
            var stone = Mats.Lit(new Color(0.85f, 0.80f, 0.70f), 0.2f);
            Add(cube, Matrix4x4.TRS(c + Vector3.up * 0.19f, Quaternion.identity, new Vector3(BLOCK - 3, 0.02f, BLOCK - 3)), grass, false);
            // caminhos em cruz
            Add(cube, Matrix4x4.TRS(c + Vector3.up * 0.2f, Quaternion.identity, new Vector3(BLOCK - 3, 0.02f, 4)), stone, false);
            Add(cube, Matrix4x4.TRS(c + Vector3.up * 0.2f, Quaternion.identity, new Vector3(4, 0.02f, BLOCK - 3)), stone, false);
            // pista de dança 6x6 (os ladrilhos piscam no ritmo)
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
                    go.transform.position = c + new Vector3((i - 2.5f) * tile, 0.24f, (j - 2.5f) * tile);
                    go.transform.localScale = new Vector3(tile - 0.08f, 0.06f, tile - 0.08f);
                    var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = ShadowCastingMode.Off;
                }
            // árvores e bancos em volta
            var trunk = Mats.Lit(new Color(0.40f, 0.27f, 0.17f), 0.1f);
            var leaf = Mats.Lit(new Color(0.20f, 0.52f, 0.25f), 0.1f);
            var wood = Mats.Lit(new Color(0.55f, 0.35f, 0.20f), 0.3f);
            for (int k = 0; k < 8; k++)
            {
                var p = c + Quaternion.Euler(0, k * 45 + 22.5f, 0) * Vector3.forward * 13f + Vector3.up * 0.18f;
                Add(cyl, Matrix4x4.TRS(p + Vector3.up * 1.6f, Quaternion.identity, new Vector3(0.3f, 1.6f, 0.3f)), trunk, true);
                Add(sphere, Matrix4x4.TRS(p + Vector3.up * 4f, Quaternion.identity, Vector3.one * 3.6f), leaf, true);
                Box(p + Vector3.up * 1.6f, new Vector3(0.5f, 3.2f, 0.5f));
                var b = c + Quaternion.Euler(0, k * 45, 0) * Vector3.forward * 9f + Vector3.up * 0.18f;
                var q = Quaternion.LookRotation(c - b);
                Add(cube, Matrix4x4.TRS(b + Vector3.up * 0.45f, q, new Vector3(2.2f, 0.12f, 0.6f)), wood, true);
                Add(cube, Matrix4x4.TRS(b + Vector3.up * 0.2f, q, new Vector3(2.0f, 0.4f, 0.4f)), trunk, true);
                Box(b + Vector3.up * 0.25f, new Vector3(1.2f, 0.5f, 1.2f));
            }
        }

        // ---------- utilidades ----------
        static Mesh Prim(PrimitiveType t)
        {
            var go = GameObject.CreatePrimitive(t);
            var m = go.GetComponent<MeshFilter>().sharedMesh;
            Destroy(go);
            return m;
        }

        void Add(Mesh mesh, Matrix4x4 m, Material mat, bool shadows)
        {
            Vector3 p = m.GetColumn(3);
            int cx = Mathf.FloorToInt((p.x + Half) / (P * 2)), cz = Mathf.FloorToInt((p.z + Half) / (P * 2));
            if (!shadows) { cx += 1000; } // separa o que não projeta sombra
            var key = (mat, cx, cz);
            if (!batches.TryGetValue(key, out var list)) batches[key] = list = new List<CombineInstance>();
            list.Add(new CombineInstance { mesh = mesh, transform = m });
        }

        void Box(Vector3 center, Vector3 size)
        {
            var go = new GameObject("c");
            go.transform.SetParent(colliders);
            go.transform.position = center;
            var bc = go.AddComponent<BoxCollider>(); bc.size = size;
        }

        // prédio: caixa com UV em metros (janela a cada 3 m)
        void BuildingMesh(Vector3 basePos, float w, float h, float d, Material mat)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            void Face(Vector3 o, Vector3 right, Vector3 up, Vector3 normal, float len, float height, bool windows)
            {
                int s = v.Count;
                v.Add(o); v.Add(o + right * len); v.Add(o + right * len + up * height); v.Add(o + up * height);
                for (int k = 0; k < 4; k++) n.Add(normal);
                if (windows)
                {
                    float off = (float)rnd.NextDouble();
                    uv.Add(new Vector2(off, 0)); uv.Add(new Vector2(off + len / 3f, 0)); uv.Add(new Vector2(off + len / 3f, height / 3f)); uv.Add(new Vector2(off, height / 3f));
                }
                else for (int k = 0; k < 4; k++) uv.Add(new Vector2(0.02f, 0.02f));
                tri.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
            }
            float x = w / 2, z = d / 2;
            Face(new Vector3(-x, 0, -z), Vector3.right, Vector3.up, Vector3.back, w, h, true);
            Face(new Vector3(x, 0, -z), Vector3.forward, Vector3.up, Vector3.right, d, h, true);
            Face(new Vector3(x, 0, z), Vector3.left, Vector3.up, Vector3.forward, w, h, true);
            Face(new Vector3(-x, 0, z), Vector3.back, Vector3.up, Vector3.left, d, h, true);
            Face(new Vector3(-x, h, -z), Vector3.right, Vector3.forward, Vector3.up, w, d, false);
            var mesh = new Mesh();
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
            mesh.RecalculateTangents();
            Add(mesh, Matrix4x4.Translate(basePos), mat, true);
        }

        void MakeWindowTextures()
        {
            const int S = 256, C = 64;
            winBase = new Texture2D(S, S, TextureFormat.RGBA32, true);
            winGlow = new Texture2D(S, S, TextureFormat.RGBA32, true);
            var pb = new Color32[S * S]; var pg = new Color32[S * S];
            var lit = new bool[16];
            for (int i = 0; i < 16; i++) lit[i] = rnd.NextDouble() < 0.55;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int cx = x % C, cy = y % C, cell = (y / C) * 4 + x / C;
                    bool win = cx >= 14 && cx < 50 && cy >= 18 && cy < 52;
                    bool frame = !win && cx >= 11 && cx < 53 && cy >= 15 && cy < 55;
                    byte wall = (byte)(235 - (y % 2) * 4);
                    pb[y * S + x] = win ? new Color32(70, 85, 105, 255) : frame ? new Color32(250, 250, 250, 255) : new Color32(wall, wall, wall, 255);
                    float shade = win ? 1f - Mathf.Abs(cx - 32) / 40f : 0f;
                    pg[y * S + x] = win && lit[cell] ? new Color32((byte)(255 * shade), (byte)(220 * shade), (byte)(150 * shade), 255) : new Color32(0, 0, 0, 255);
                }
            winBase.SetPixels32(pb); winBase.Apply(true); winBase.wrapMode = TextureWrapMode.Repeat; winBase.anisoLevel = 4;
            winGlow.SetPixels32(pg); winGlow.Apply(true); winGlow.wrapMode = TextureWrapMode.Repeat; winGlow.anisoLevel = 4;
        }

        void Flush()
        {
            foreach (var kv in batches)
            {
                var go = new GameObject("Cidade_" + kv.Key.Item1.name);
                go.transform.SetParent(transform, false);
                go.isStatic = true;
                var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                mesh.RecalculateBounds();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = kv.Key.Item1;
                bool shadows = kv.Key.Item2 < 1000;
                mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                mr.receiveShadows = true;
            }
            batches.Clear();
        }
    }
}
