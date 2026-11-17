using System.Collections.Generic;
using UnityEngine;

namespace CarretaJoy
{
    /// <summary>
    /// A carreta: modelo original do rascunho, anda sozinha pela avenida de fora da cidade,
    /// para nos pontos de festa, tem neon e luzes coloridas no ritmo da música.
    /// O teto é o "palco": os meninos sobem pela lateral e dançam lá em cima.
    /// </summary>
    public class Truck : MonoBehaviour
    {
        public const float LENGTH = 16f;
        public float HalfWidth { get; private set; } = 1.6f;
        public float RoofY { get; private set; } = 3.6f;
        float roofZ0, roofZ1;

        List<Vector3> path; float[] cum; float total;
        public float dist;
        public float speed;
        public float cruise = 11f;
        List<float> stopList;
        public void SetStops(List<float> s) { stopList = s; if (s != null && s.Count > 0) nextStop = s[0] + 0.01f; }
        float stopTimer, nextStop;
        public bool Stopped => stopTimer > 0;
        public Vector3 Velocity { get; private set; }

        readonly List<Light> lights = new List<Light>();
        readonly List<Material> neon = new List<Material>();
        readonly List<Color> neonColor = new List<Color>();
        Light headL, headR;
        Material headMat;
        BoxCollider body;
        bool night;

        public void Build(GameObject prefab, List<Vector3> route)
        {
            path = route;
            cum = new float[path.Count + 1];
            for (int i = 0; i < path.Count; i++) { cum[i + 1] = cum[i] + Vector3.Distance(path[i], path[(i + 1) % path.Count]); }
            total = cum[path.Count];
            nextStop = total * 0.25f;

            var m = Instantiate(prefab, transform);
            m.name = "Modelo";
            m.transform.localPosition = Vector3.zero; m.transform.localRotation = Quaternion.identity;
            // mede o modelo (cabine virada para +Z)
            var rends = m.GetComponentsInChildren<Renderer>();
            Bounds b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
            if (b.size.x > b.size.z) { m.transform.localRotation = Quaternion.Euler(0, 90, 0); b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds); }
            float s = LENGTH / Mathf.Max(b.size.x, b.size.z);
            m.transform.localScale *= s;
            b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
            m.transform.position += new Vector3(transform.position.x - b.center.x, transform.position.y - b.min.y, transform.position.z - b.center.z);
            var tmat = Resources.Load<Material>("Materials/Carreta");
            foreach (var r in rends) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; if (tmat != null) r.sharedMaterial = tmat; }
            float H = b.size.y, L = Mathf.Max(b.size.x, b.size.z);
            HalfWidth = Mathf.Min(b.size.x, b.size.z) / 2f;
            RoofY = H - 0.12f;
            // teto da carreta (sem a cabine): de -0.86 a +0.58 do comprimento do modelo (medido no jogo antigo)
            roofZ0 = -0.86f / 1.9f * L + 0.5f; roofZ1 = 0.58f / 1.9f * L - 0.3f;

            body = gameObject.AddComponent<BoxCollider>();
            body.center = new Vector3(0, H / 2f, 0); body.size = new Vector3(HalfWidth * 2, H, L);
            var rb = gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None;

            Decorate(L, H);
            // camada "Ignore Raycast": a câmera não bate na carreta (os meninos continuam colidindo)
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            Place(0);
        }

        void Decorate(float L, float H)
        {
            // barras de neon nas bordas do teto
            Color[] cols = { new Color(1, 0.1f, 0.6f), new Color(0.1f, 0.8f, 1), new Color(1, 0.85f, 0.1f), new Color(0.4f, 1, 0.2f), new Color(0.7f, 0.2f, 1) };
            int k = 0;
            for (float z = roofZ0; z < roofZ1 - 0.5f; z += 2.2f)
                for (int side = -1; side <= 1; side += 2)
                {
                    var c = cols[k++ % cols.Length];
                    var mat = Mats.Glow(c, 3f);
                    neon.Add(mat); neonColor.Add(c);
                    var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(bar.GetComponent<Collider>());
                    bar.transform.SetParent(transform, false);
                    bar.transform.localPosition = new Vector3(side * (HalfWidth - 0.05f), RoofY + 0.06f, z + 1.0f);
                    bar.transform.localScale = new Vector3(0.12f, 0.12f, 2.0f);
                    var mr = bar.GetComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            // luzes coloridas de verdade (iluminam a rua e os meninos)
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Luz" + i);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3((i % 2 == 0 ? -1 : 1) * (HalfWidth + 0.3f), RoofY + 1.2f, Mathf.Lerp(roofZ0, roofZ1, i < 2 ? 0.2f : 0.8f));
                var l = go.AddComponent<Light>();
                l.type = LightType.Point; l.range = 14f; l.intensity = 3f; l.shadows = LightShadows.None;
                lights.Add(l);
            }
            // faróis
            headMat = Mats.Glow(new Color(1, 0.97f, 0.85f), 4f);
            for (int side = -1; side <= 1; side += 2)
            {
                var hl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(hl.GetComponent<Collider>());
                hl.transform.SetParent(transform, false);
                hl.transform.localPosition = new Vector3(side * (HalfWidth - 0.35f), 0.9f, L / 2f - 0.05f);
                hl.transform.localScale = new Vector3(0.35f, 0.25f, 0.1f);
                hl.GetComponent<MeshRenderer>().sharedMaterial = headMat;
                var go = new GameObject("Farol");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(side * (HalfWidth - 0.35f), 1.0f, L / 2f + 0.2f);
                go.transform.localRotation = Quaternion.Euler(12, 0, 0);
                var l = go.AddComponent<Light>();
                l.type = LightType.Spot; l.range = 30f; l.spotAngle = 70f; l.intensity = 0; l.color = new Color(1, 0.95f, 0.85f);
                if (side < 0) headL = l; else headR = l;
            }
        }

        public void SetNight(bool n)
        {
            night = n;
            headL.intensity = headR.intensity = n ? 6f : 0f;
            Mats.SetGlow(headMat, new Color(1, 0.97f, 0.85f), n ? 8f : 1.5f);
        }

        // ---------------- percurso ----------------
        Vector3 At(float d, out Vector3 dir)
        {
            d = Mathf.Repeat(d, total);
            int i = System.Array.BinarySearch(cum, d);
            if (i < 0) i = ~i - 1;
            i = Mathf.Clamp(i, 0, path.Count - 1);
            Vector3 a = path[i], b = path[(i + 1) % path.Count];
            float seg = cum[i + 1] - cum[i];
            float u = seg > 0 ? (d - cum[i]) / seg : 0;
            dir = (b - a).normalized;
            return Vector3.Lerp(a, b, u);
        }

        void Place(float dt)
        {
            Vector3 dF, dB;
            Vector3 front = At(dist + 5f, out dF), back = At(dist - 5f, out dB);
            Vector3 pos = (front + back) / 2f;
            Vector3 fwd = (front - back);
            var old = transform.position;
            transform.position = pos;
            if (fwd.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(fwd.normalized);
            Velocity = dt > 0 ? (pos - old) / dt : Vector3.zero;
        }

        public void Tick(float dt, Vector3 player, bool playerOnTruck, float beat)
        {
            // para nos pontos de festa (a cada quarto de volta)
            if (stopTimer > 0) { stopTimer -= dt; speed = Mathf.MoveTowards(speed, 0, 8f * dt); }
            else
            {
                float want = cruise;
                float dp = DistanceTo(player);
                if (!playerOnTruck && dp < 10f) want = 2.2f;               // perto: vai devagar para dar para subir
                if (BlockedAhead(player)) want = 0;                          // não atropela ninguém
                float ahead = Mathf.Repeat(nextStop - dist, total);
                if (ahead < 25f) want = Mathf.Min(want, 1.5f + ahead * 0.4f);
                if (ahead < 0.5f || ahead > total - 1f)
                {
                    stopTimer = 15f;
                    if (stopList != null && stopList.Count > 0)
                    {
                        float cur = Mathf.Repeat(dist, total), nx = stopList[0];
                        foreach (var s in stopList) if (s > cur + 1f) { nx = s; break; }
                        nextStop = nx;
                    }
                    else nextStop = Mathf.Repeat(nextStop + total * 0.25f, total);
                }
                speed = Mathf.MoveTowards(speed, want, (want > speed ? 2.5f : 6f) * dt);
            }
            dist += speed * dt;
            Place(dt);

            // luzes no ritmo
            float pulse = Mathf.Pow(1f - Mathf.Repeat(beat, 1f), 2f);
            for (int i = 0; i < lights.Count; i++)
            {
                lights[i].color = Color.HSVToRGB(Mathf.Repeat(beat * 0.125f + i * 0.25f, 1f), 0.9f, 1f);
                lights[i].intensity = (night ? 5f : 1.5f) * (0.5f + pulse);
            }
            for (int i = 0; i < neon.Count; i++)
            {
                bool on = ((int)(beat * 2) + i) % 3 != 0;
                float hue = Mathf.Repeat(beat * 0.0625f + i * 0.07f, 1f);
                var c = Color.HSVToRGB(hue, 0.85f, 1f);
                Mats.SetGlow(neon[i], c, (on ? (night ? 6f : 3f) : 0.6f) * (0.7f + pulse * 0.6f));
            }
        }

        bool BlockedAhead(Vector3 p)
        {
            var lp = transform.InverseTransformPoint(p);
            return Mathf.Abs(lp.x) < HalfWidth + 0.8f && lp.z > LENGTH / 2f - 0.5f && lp.z < LENGTH / 2f + 4f && lp.y < 2f;
        }

        // ---------------- teto ----------------
        public Vector3 ClampToRoof(Vector3 lp)
        {
            lp.x = Mathf.Clamp(lp.x, -HalfWidth + 0.35f, HalfWidth - 0.35f);
            lp.z = Mathf.Clamp(lp.z, roofZ0, roofZ1);
            return lp;
        }

        public Vector3 RoofToWorld(Vector3 lp) => transform.TransformPoint(new Vector3(lp.x, RoofY + lp.y, lp.z));

        public float DistanceTo(Vector3 p)
        {
            var lp = transform.InverseTransformPoint(p);
            float dx = Mathf.Max(0, Mathf.Abs(lp.x) - HalfWidth), dz = Mathf.Max(0, Mathf.Abs(lp.z) - LENGTH / 2f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
