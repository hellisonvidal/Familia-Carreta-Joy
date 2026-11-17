using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CarretaJoy
{
    /// <summary>
    /// Dados reais de Ponta Grossa (OpenStreetMap + relevo), gerados por tools/pg/build_map.py.
    /// Metros, origem na Catedral Sant'Ana: x = leste, z = norte, y = altitude (relativa).
    /// </summary>
    public class PgMap
    {
        public float ChunkSize, Elev0;
        public readonly Dictionary<Vector2Int, (int off, int len)> Index = new Dictionary<Vector2Int, (int, int)>();
        public readonly List<(string kind, string name, Vector2 pos, float r)> Landmarks = new List<(string, string, Vector2, float)>();
        public readonly List<(string name, Vector2 pos)> Labels = new List<(string, Vector2)>();
        public readonly List<Vector2> Route = new List<Vector2>();
        public readonly List<(string name, Vector2 pos)> Stops = new List<(string, Vector2)>();
        byte[] data; int blobBase;

        // relevo
        int hx, hz; float hx0, hz0, hcell; ushort[] heights;
        public Rect Bounds;

        public static PgMap Load()
        {
            var m = new PgMap();
            var ta = Resources.Load<TextAsset>("Map/pg_map");
            var th = Resources.Load<TextAsset>("Map/pg_height");
            if (ta == null || th == null) { Debug.LogError("Mapa de Ponta Grossa não encontrado"); return null; }
            m.ReadHeight(th.bytes);
            m.ReadMap(ta.bytes);
            return m;
        }

        static string Str(BinaryReader r) { int n = r.ReadByte(); return Encoding.UTF8.GetString(r.ReadBytes(n)); }

        void ReadMap(byte[] b)
        {
            data = b;
            using (var r = new BinaryReader(new MemoryStream(b)))
            {
                if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "PGM1") throw new IOException("formato de mapa inválido");
                ChunkSize = r.ReadSingle(); Elev0 = r.ReadSingle(); r.ReadSingle();
                int n = (int)r.ReadUInt32();
                for (int i = 0; i < n; i++) { int ci = r.ReadInt16(), cj = r.ReadInt16(); int off = (int)r.ReadUInt32(), len = (int)r.ReadUInt32(); Index[new Vector2Int(ci, cj)] = (off, len); }
                n = (int)r.ReadUInt32();
                for (int i = 0; i < n; i++) { r.ReadByte(); string k = Str(r), nm = Str(r); float x = r.ReadSingle(), z = r.ReadSingle(), rr = r.ReadSingle(); Landmarks.Add((k, nm, new Vector2(x, z), rr)); }
                n = (int)r.ReadUInt32();
                for (int i = 0; i < n; i++) { string nm = Str(r); Labels.Add((nm, new Vector2(r.ReadSingle(), r.ReadSingle()))); }
                n = (int)r.ReadUInt32();
                for (int i = 0; i < n; i++) Route.Add(new Vector2(r.ReadSingle(), r.ReadSingle()));
                n = (int)r.ReadUInt32();
                for (int i = 0; i < n; i++) { string nm = Str(r); Stops.Add((nm, new Vector2(r.ReadSingle(), r.ReadSingle()))); }
                blobBase = (int)r.BaseStream.Position;
            }
        }

        void ReadHeight(byte[] b)
        {
            using (var r = new BinaryReader(new MemoryStream(b)))
            {
                hx = (int)r.ReadUInt32(); hz = (int)r.ReadUInt32();
                hx0 = r.ReadSingle(); hz0 = r.ReadSingle(); hcell = r.ReadSingle(); r.ReadSingle();
                heights = new ushort[hx * hz];
                for (int i = 0; i < heights.Length; i++) heights[i] = r.ReadUInt16();
            }
            Bounds = new Rect(hx0, hz0, (hx - 1) * hcell, (hz - 1) * hcell);
        }

        /// <summary>Altura do chão (m) num ponto do mapa.</summary>
        public float Height(float x, float z)
        {
            float fi = Mathf.Clamp((x - hx0) / hcell, 0, hx - 1.001f), fj = Mathf.Clamp((z - hz0) / hcell, 0, hz - 1.001f);
            int i = (int)fi, j = (int)fj; float a = fi - i, c = fj - j;
            float h00 = heights[j * hx + i], h10 = heights[j * hx + i + 1], h01 = heights[(j + 1) * hx + i], h11 = heights[(j + 1) * hx + i + 1];
            return ((h00 * (1 - a) + h10 * a) * (1 - c) + (h01 * (1 - a) + h11 * a) * c) * 0.1f;
        }

        // ---------------- conteúdo de um pedaço ----------------
        public class Road { public int type; public float width; public bool unpaved, sidewalk; public Vector2[] pts; }
        public class Building { public int kind; public float height; public int roof, color; public Vector2[] pts; public ushort[] tris; }
        public class Chunk
        {
            public readonly List<Road> roads = new List<Road>();
            public readonly List<(float w, Vector2[] pts)> water = new List<(float, Vector2[])>();
            public readonly List<Building> buildings = new List<Building>();
            public readonly List<(Vector2 p, int kind)> trees = new List<(Vector2, int)>();
        }

        public Chunk ReadChunk(Vector2Int id)
        {
            if (!Index.TryGetValue(id, out var e)) return null;
            var c = new Chunk();
            using (var r = new BinaryReader(new MemoryStream(data, blobBase + e.off, e.len)))
            {
                int n = r.ReadUInt16();
                for (int i = 0; i < n; i++)
                {
                    var rd = new Road { type = r.ReadByte(), width = r.ReadSingle(), unpaved = r.ReadByte() != 0 };
                    int np = r.ReadUInt16(); rd.sidewalk = r.ReadByte() != 0;
                    rd.pts = new Vector2[np]; for (int k = 0; k < np; k++) rd.pts[k] = new Vector2(r.ReadSingle(), r.ReadSingle());
                    c.roads.Add(rd);
                }
                n = r.ReadUInt16();
                for (int i = 0; i < n; i++)
                {
                    float w = r.ReadSingle(); int np = r.ReadUInt16(); var pts = new Vector2[np];
                    for (int k = 0; k < np; k++) pts[k] = new Vector2(r.ReadSingle(), r.ReadSingle());
                    c.water.Add((w, pts));
                }
                n = r.ReadUInt16();
                for (int i = 0; i < n; i++)
                {
                    var b = new Building { kind = r.ReadByte(), height = r.ReadSingle(), roof = r.ReadByte(), color = r.ReadByte() };
                    int np = r.ReadUInt16(); b.pts = new Vector2[np];
                    for (int k = 0; k < np; k++) b.pts[k] = new Vector2(r.ReadSingle(), r.ReadSingle());
                    int ni = r.ReadUInt16(); b.tris = new ushort[ni];
                    for (int k = 0; k < ni; k++) b.tris[k] = r.ReadUInt16();
                    c.buildings.Add(b);
                }
                n = r.ReadUInt16();   // áreas: o chão já vem pintado no mapa de cores; só pula
                for (int i = 0; i < n; i++)
                {
                    r.ReadByte(); int nv = r.ReadUInt16(); r.BaseStream.Position += nv * 8;
                    uint ni = r.ReadUInt32(); r.BaseStream.Position += ni * (nv < 65535 ? 2 : 4);
                }
                n = r.ReadUInt16();
                for (int i = 0; i < n; i++) c.trees.Add((new Vector2(r.ReadSingle(), r.ReadSingle()), r.ReadByte()));
            }
            return c;
        }
    }
}
