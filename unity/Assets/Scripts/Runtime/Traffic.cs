using System.Collections.Generic;
using UnityEngine;

namespace CarretaJoy
{
    /// <summary>
    /// Trânsito: carros (Car Kit da Kenney, CC0) andando pelas ruas reais perto do jogador, na mão direita,
    /// com rodas girando. Param para os meninos e para a carreta; somem longe e aparecem em outra rua.
    /// </summary>
    public class Traffic : MonoBehaviour
    {
        static readonly (string file, float len)[] Models = {
            ("sedan", 4.5f), ("sedan-sports", 4.4f), ("hatchback-sports", 4.0f), ("suv", 4.7f), ("suv-luxury", 4.9f), ("taxi", 4.5f),
            ("police", 4.6f), ("van", 5.0f), ("delivery", 5.6f), ("truck", 6.2f), ("ambulance", 5.8f), ("garbage-truck", 7.0f), ("firetruck", 7.5f) };
        static readonly float[] Weight = { 6, 3, 4, 5, 3, 3, 1, 3, 2, 2, 1, 1, 0.5f };

        class Car
        {
            public Transform t; public Transform model; public readonly List<Transform> wheels = new List<Transform>(); public float wheelR = 0.35f;
            public Vector2[] path; public int seg; public float u, dir = 1, speed, cruise, len, spin;
        }

        PgCity city; Truck truck;
        readonly List<GameObject> prefabs = new List<GameObject>();
        readonly List<float> lens = new List<float>(), weights = new List<float>();
        readonly List<Car> cars = new List<Car>();
        Material mat;
        readonly System.Random rnd = new System.Random(11);
        float spawnT;
        public System.Func<Vector3, float, bool> Blocked;   // tem alguém (menino) na frente?

        public void Init(PgCity c, Truck t)
        {
            city = c; truck = t;
            mat = Resources.Load<Material>("Materials/Transito");
            for (int i = 0; i < Models.Length; i++)
            {
                var p = Resources.Load<GameObject>("Traffic/" + Models[i].file);
                if (p == null) continue;
                prefabs.Add(p); lens.Add(Models[i].len); weights.Add(Weight[i]);
            }
        }
        public void SetTruck(Truck t) { truck = t; }

        int Max => Settings.Quality == 0 ? 6 : Settings.Quality == 1 ? 12 : 18;
        float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

        int PickModel()
        {
            float sum = 0; foreach (var w in weights) sum += w;
            float x = R(0, sum);
            for (int i = 0; i < weights.Count; i++) { x -= weights[i]; if (x <= 0) return i; }
            return 0;
        }

        Car Build(int k)
        {
            var root = new GameObject("Carro");
            var m = Instantiate(prefabs[k], root.transform);
            m.transform.localPosition = Vector3.zero;
            var car = new Car { t = root.transform, model = m.transform };
            Transform fl = null, bl = null;
            foreach (var tr in m.GetComponentsInChildren<Transform>(true))
            {
                if (tr.name.StartsWith("wheel")) { car.wheels.Add(tr); if (tr.name.Contains("front")) fl = tr; else if (tr.name.Contains("back")) bl = tr; }
            }
            foreach (var r in m.GetComponentsInChildren<Renderer>()) { if (mat != null) r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
            // vira o modelo para a frente ficar em +Z (das rodas de trás para as da frente)
            if (fl != null && bl != null)
            {
                var f = root.transform.InverseTransformPoint(fl.position) - root.transform.InverseTransformPoint(bl.position); f.y = 0;
                if (f.sqrMagnitude > 1e-6f) m.transform.localRotation = Quaternion.FromToRotation(f.normalized, Vector3.forward) * m.transform.localRotation;
            }
            // tamanho real
            var rends = m.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
                float s = lens[k] / Mathf.Max(0.1f, b.size.z);
                m.transform.localScale *= s;
                b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
                m.transform.position += new Vector3(root.transform.position.x - b.center.x, root.transform.position.y - b.min.y, root.transform.position.z - b.center.z);
                car.len = b.size.z;
                var wr = car.wheels.Count > 0 ? car.wheels[0].GetComponent<Renderer>() : null;
                if (wr != null) car.wheelR = Mathf.Max(0.15f, wr.bounds.extents.y);
            }
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.8f, 0); col.size = new Vector3(1.9f, 1.6f, car.len * 0.95f);
            foreach (var tr in root.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = 2;
            return car;
        }

        bool Spawn(Vector3 focus)
        {
            if (prefabs.Count == 0) return false;
            var roads = city.RoadsNear(focus, 260f, true);
            if (roads.Count == 0) return false;
            for (int tries = 0; tries < 6; tries++)
            {
                var r = roads[rnd.Next(roads.Count)];
                int seg = rnd.Next(r.pts.Length - 1);
                float u = R(0, 1);
                var pos = Vector2.Lerp(r.pts[seg], r.pts[seg + 1], u);
                float d = Vector2.Distance(pos, new Vector2(focus.x, focus.z));
                if (d < 60f || d > 250f) continue;
                if (TooClose(pos, 12f)) continue;
                var car = Build(PickModel());
                car.path = r.pts; car.seg = seg; car.u = u; car.dir = rnd.Next(2) == 0 ? 1 : -1;
                car.cruise = R(7f, 11f) * (r.width >= 8 ? 1.25f : 1f); car.speed = car.cruise;
                Place(car, 0);
                cars.Add(car);
                return true;
            }
            return false;
        }

        bool TooClose(Vector2 p, float dmin)
        {
            foreach (var c in cars) { var q = c.t.position; if ((new Vector2(q.x, q.z) - p).sqrMagnitude < dmin * dmin) return true; }
            return false;
        }

        // posição na faixa da direita (mão brasileira), virado no sentido da rua
        void Place(Car c, float dt)
        {
            Vector2 a = c.path[c.seg], b = c.path[c.seg + 1];
            var dir = (b - a); float L = dir.magnitude; if (L < 0.01f) return;
            dir /= L;
            var fwd = dir * c.dir;
            var right = new Vector2(fwd.y, -fwd.x);
            var p = Vector2.Lerp(a, b, c.u) + right * 1.8f;
            float hf = city.Height(p.x + fwd.x * 1.6f, p.y + fwd.y * 1.6f), hb = city.Height(p.x - fwd.x * 1.6f, p.y - fwd.y * 1.6f);
            var old = c.t.position;
            c.t.position = new Vector3(p.x, (hf + hb) / 2f + 0.12f, p.y);
            var f3 = new Vector3(fwd.x, (hf - hb) / 3.2f, fwd.y);
            var want = Quaternion.LookRotation(f3.normalized);
            c.t.rotation = dt > 0 ? Quaternion.Slerp(c.t.rotation, want, 1 - Mathf.Exp(-dt * 8f)) : want;
            if (dt > 0)
            {
                float moved = Vector3.Distance(old, c.t.position);
                c.spin = Mathf.Repeat(c.spin + moved / c.wheelR * Mathf.Rad2Deg, 360f);
                foreach (var w in c.wheels) w.Rotate(c.t.right, moved / c.wheelR * Mathf.Rad2Deg, Space.World);
            }
        }

        public void Tick(Vector3 focus, float dt)
        {
            if (city == null) return;
            for (int i = cars.Count - 1; i >= 0; i--)
            {
                var c = cars[i];
                if ((c.t.position - focus).sqrMagnitude > 330f * 330f) { Destroy(c.t.gameObject); cars.RemoveAt(i); }
            }
            spawnT -= dt;
            if (cars.Count < Max && spawnT <= 0) { spawnT = 0.4f; Spawn(focus); }
            foreach (var c in cars) Drive(c, dt);
        }

        void Drive(Car c, float dt)
        {
            // freia para meninos, para a carreta e para o carro da frente
            var p = c.t.position; var f = c.t.forward;
            float want = c.cruise;
            if (truck != null && truck.DistanceTo(p + f * (c.len / 2f + 3f)) < 3f) want = 0;
            if (Blocked != null && Blocked(p + f * (c.len / 2f + 3f), 3.5f)) want = 0;
            foreach (var o in cars)
            {
                if (o == c) continue;
                var d = o.t.position - p;
                float ahead = Vector3.Dot(d, f);
                if (ahead > 0 && ahead < c.len / 2f + o.len / 2f + 4f && Mathf.Abs(Vector3.Dot(d, c.t.right)) < 1.6f) { want = Mathf.Min(want, Mathf.Max(0, o.speed - 1f)); }
            }
            c.speed = Mathf.MoveTowards(c.speed, want, (want < c.speed ? 9f : 2.5f) * dt);
            // avança pela rua; no fim procura uma rua que continue dali ou faz o retorno
            Vector2 a = c.path[c.seg], b = c.path[c.seg + 1];
            float L = Mathf.Max(0.01f, Vector2.Distance(a, b));
            c.u += c.dir * c.speed * dt / L;
            if (c.u > 1 || c.u < 0)
            {
                bool fwdEnd = c.u > 1;
                int next = c.seg + (fwdEnd ? 1 : -1);
                if (next >= 0 && next < c.path.Length - 1) { c.seg = next; c.u = fwdEnd ? 0 : 1; }
                else if (!Continue(c, fwdEnd ? c.path[c.path.Length - 1] : c.path[0])) { c.dir = -c.dir; c.u = Mathf.Clamp01(c.u); }
            }
            Place(c, dt);
        }

        /// <summary>Esquina: segue por outra rua que começa (ou termina) neste ponto.</summary>
        bool Continue(Car c, Vector2 end)
        {
            var roads = city.RoadsNear(new Vector3(end.x, 0, end.y), 10f, true);
            int n = roads.Count; if (n == 0) return false;
            int start = rnd.Next(n);
            for (int k = 0; k < n; k++)
            {
                var r = roads[(start + k) % n];
                if (r.pts == c.path) continue;
                if ((r.pts[0] - end).sqrMagnitude < 4f) { c.path = r.pts; c.seg = 0; c.u = 0; c.dir = 1; return true; }
                if ((r.pts[r.pts.Length - 1] - end).sqrMagnitude < 4f) { c.path = r.pts; c.seg = r.pts.Length - 2; c.u = 1; c.dir = -1; return true; }
            }
            return false;
        }

        public void Clear() { foreach (var c in cars) if (c.t) Destroy(c.t.gameObject); cars.Clear(); }

        public void Dots(List<(Vector2 pos, Color c, float size)> list)
        {
            foreach (var c in cars) { var p = c.t.position; list.Add((new Vector2(p.x, p.z), new Color(0.85f, 0.85f, 0.9f), 1.1f)); }
        }
    }
}
