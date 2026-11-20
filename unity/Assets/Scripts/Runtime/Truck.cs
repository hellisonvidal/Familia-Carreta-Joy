using System.Collections.Generic;
using UnityEngine;

namespace CarretaJoy
{
    /// <summary>
    /// A carreta: anda sozinha pelas ruas reais de Ponta Grossa, para nos pontos de festa,
    /// tem neon, luzes coloridas no ritmo da música e passageiros comuns sentados DENTRO dela.
    /// A turma de personagens acompanha correndo e dançando na rua ao redor da carreta.
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
        public float cruise = 3.5f;   // devagar: a turma a pé acompanha na rua (ajuste no menu)
        List<float> stopList;
        public void SetStops(List<float> s) { stopList = s; if (s != null && s.Count > 0) nextStop = s[0] + 0.01f; }
        float stopTimer, nextStop;
        public bool Stopped => stopTimer > 0;
        public float StopLeft => stopTimer;
        public Vector3 Velocity { get; private set; }

        readonly List<Light> lights = new List<Light>();
        readonly List<Material> neon = new List<Material>();
        readonly List<Color> neonColor = new List<Color>();
        Light headL, headR;
        Material headMat;
        BoxCollider body;
        bool night;
        float smoothPitch, smoothRoll;

        // variações: modelo, nome no menu e as duas cores de pintura
        public static readonly (string file, string nome, Color c1, Color c2)[] Variants = {
            ("Carreta", "Carreta Joy (original)", Color.white, Color.white),
            ("CarretaNeon", "Carreta Neon", new Color(0.92f, 0.12f, 0.55f), new Color(0.38f, 0.1f, 0.75f)),
            ("CarretaTropical", "Carreta Tropical", new Color(1f, 0.78f, 0.1f), new Color(0.05f, 0.62f, 0.35f)),
            ("CarretaDisco", "Carreta Disco", new Color(0.1f, 0.35f, 0.95f), new Color(0.94f, 0.95f, 0.98f)),
        };
        public int Variant { get; private set; }

        // rodas que giram (e as da frente esterçam ao dirigir)
        readonly List<Transform> wheels = new List<Transform>();
        readonly List<Quaternion> wheelBase = new List<Quaternion>();
        readonly List<Vector3> wheelAxis = new List<Vector3>(), wheelUp = new List<Vector3>();
        readonly List<bool> wheelFront = new List<bool>();
        float wheelR = 0.52f, spin, steerVis;
        Vector3 lastPos; bool hasLast;
        // bancos DENTRO da carreta para as pessoas comuns (posição local no piso interno e direção)
        public readonly List<(Vector3 pos, float yaw)> Seats = new List<(Vector3, float)>();

        public void Build(GameObject prefab, List<Vector3> route, int variant = 0)
        {
            Variant = Mathf.Clamp(variant, 0, Variants.Length - 1);
            path = route;
            cum = new float[path.Count + 1];
            for (int i = 0; i < path.Count; i++) { cum[i + 1] = cum[i] + Vector3.Distance(path[i], path[(i + 1) % path.Count]); }
            total = cum[path.Count];
            nextStop = total * 0.25f;

            var m = Instantiate(prefab, transform);
            m.name = "Modelo";
            m.transform.localPosition = Vector3.zero;   // mantém a rotação de importação (o FBX do Blender vem deitado com -90° em X)
            // mede o modelo (cabine virada para +Z)
            var rends = m.GetComponentsInChildren<Renderer>();
            Bounds b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
            // segurança: se ainda estiver em pé (comprimento na vertical), deita; depois alinha o comprimento com Z
            if (b.size.y > Mathf.Max(b.size.x, b.size.z)) { m.transform.localRotation = Quaternion.Euler(-90, 0, 0) * m.transform.localRotation; b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds); }
            if (b.size.x > b.size.z) { m.transform.localRotation = Quaternion.Euler(0, 90, 0) * m.transform.localRotation; b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds); }
            float s = LENGTH / Mathf.Max(b.size.x, b.size.z);
            m.transform.localScale *= s;
            b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
            m.transform.position += new Vector3(transform.position.x - b.center.x, transform.position.y - b.min.y, transform.position.z - b.center.z);
            float H = b.size.y, L = Mathf.Max(b.size.x, b.size.z);
            HalfWidth = Mathf.Min(b.size.x, b.size.z) / 2f;
            headMat = Mats.Glow(new Color(1, 0.97f, 0.85f), 4f);
            Paint(m, Variants[Variant]);

            // altura do teto e área da carroceria (para colisor e luzes; ninguém sobe no teto)
            RoofY = H - 0.12f;
            roofZ0 = -0.86f / 1.9f * L + 0.5f; roofZ1 = 0.58f / 1.9f * L - 0.3f;

            body = gameObject.AddComponent<BoxCollider>();
            body.center = new Vector3(0, RoofY / 2f, 0); body.size = new Vector3(HalfWidth * 2, RoofY, L);
            var rb = gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None;

            FindWheels(m.transform);
            Decorate(L, H);
            SetupSeats(m.transform);
            // camada "Ignore Raycast": a câmera não bate na carreta (os meninos continuam colidindo)
            foreach (var t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            Place(0);
        }

        /// <summary>Troca os materiais pelo nome das peças ("M_Pintura", "Roda_3"...).</summary>
        void Paint(GameObject m, (string file, string nome, Color c1, Color c2) v)
        {
            var tmat = Resources.Load<Material>("Materials/Carreta");
            var rubber = Mats.Lit(new Color(0.07f, 0.07f, 0.08f), 0.15f);
            foreach (var r in m.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                string n = r.gameObject.name;
                Material mat = null;
                if (n.StartsWith("Roda")) mat = rubber;
                else if (n.StartsWith("M_"))
                {
                    string k = n.Substring(2); int cut = k.IndexOfAny(new[] { '_', '.', ' ' }); if (cut > 0) k = k.Substring(0, cut);
                    switch (k)
                    {
                        case "Pintura": mat = Mats.Lit(v.c1, 0.6f, 0.15f); break;
                        case "Pintura2": mat = Mats.Lit(v.c2, 0.6f, 0.15f); break;
                        case "Cromo": mat = Mats.Lit(new Color(0.86f, 0.87f, 0.9f), 0.88f, 1f); break;
                        case "Preto": mat = Mats.Lit(new Color(0.05f, 0.05f, 0.055f), 0.3f); break;
                        case "Vidro": mat = Mats.Lit(new Color(0.07f, 0.1f, 0.14f), 0.95f, 0.3f); break;
                        case "Piso": mat = Mats.Lit(new Color(0.13f, 0.13f, 0.15f), 0.2f); break;
                        case "Caixa": mat = Mats.Lit(new Color(0.04f, 0.04f, 0.05f), 0.25f); break;
                        case "Madeira": mat = Mats.Lit(new Color(0.42f, 0.28f, 0.15f), 0.15f); break;
                        case "Folha": mat = Mats.Lit(new Color(0.12f, 0.55f, 0.2f), 0.2f); break;
                        case "Toldo": mat = Mats.Lit(new Color(0.97f, 0.97f, 0.93f), 0.3f); break;
                        case "Farol": mat = headMat; break;
                        case "Lanterna": mat = Mats.Glow(new Color(1f, 0.35f, 0.05f), 2.5f); break;
                        case "Neon": case "Tela":
                            mat = Mats.Glow(Color.cyan, 3f); neon.Add(mat); neonColor.Add(Color.cyan);
                            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; break;
                    }
                }
                r.sharedMaterial = mat != null ? mat : tmat;
            }
        }

        void FindWheels(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Roda_")) wheels.Add(t);
            if (wheels.Count == 0) return;
            float maxZ = float.MinValue;
            foreach (var w in wheels) maxZ = Mathf.Max(maxZ, transform.InverseTransformPoint(w.position).z);
            var rr = wheels[0].GetComponent<Renderer>();
            if (rr != null) wheelR = Mathf.Max(0.2f, rr.bounds.extents.y);
            foreach (var w in wheels)
            {
                wheelBase.Add(w.localRotation);
                wheelAxis.Add((Quaternion.Inverse(w.rotation) * transform.right).normalized);
                wheelUp.Add((Quaternion.Inverse(w.rotation) * transform.up).normalized);
                wheelFront.Add(transform.InverseTransformPoint(w.position).z > maxZ - 0.6f);
            }
        }

        /// <summary>Gira as rodas pelo quanto a carreta andou (e vira as da frente no volante).</summary>
        void SpinWheels(float steer, float dt)
        {
            if (wheels.Count == 0) return;
            var p = transform.position;
            if (hasLast)
            {
                var d = p - lastPos;
                float fwd = Vector3.Dot(d, transform.forward);
                if (Mathf.Abs(fwd) < 20f) spin = Mathf.Repeat(spin + fwd / wheelR * Mathf.Rad2Deg, 360f);
            }
            lastPos = p; hasLast = true;
            steerVis = Mathf.MoveTowards(steerVis, Mathf.Clamp(steer, -1f, 1f) * 28f, 120f * dt);
            for (int i = 0; i < wheels.Count; i++)
            {
                if (wheels[i] == null) continue;
                var q = wheelBase[i];
                if (wheelFront[i]) q *= Quaternion.AngleAxis(steerVis, wheelUp[i]);
                wheels[i].localRotation = q * Quaternion.AngleAxis(spin, wheelAxis[i]);
            }
        }

        /// <summary>
        /// Assentos internos DENTRO da carreta para as pessoas comuns (nunca em cima do teto).
        /// Usa os marcadores Assento_* do modelo quando existem, ou o layout interno da Carreta original.
        /// </summary>
        void SetupSeats(Transform root)
        {
            Seats.Clear();
            var markers = new List<(int idx, Transform t)>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Assento_")) continue;
                int idx = 0;
                int.TryParse(t.name.Substring(8), out idx);
                markers.Add((idx, t));
            }
            if (markers.Count > 0)
            {
                markers.Sort((a, b) => a.idx.CompareTo(b.idx));
                foreach (var m in markers)
                {
                    Vector3 lp = transform.InverseTransformPoint(m.t.position);
                    Seats.Add((lp, 0f));   // olhando para frente (+Z), sentados nos bancos internos
                }
                return;
            }
            // Carreta original (Carreta.fbx): piso interno em Y = 1.54m, duas fileiras de bancos em X = ±0.76m
            float[] zs = { 3.4f, 1.7f, 0.0f, -1.7f, -3.4f, -5.0f };
            foreach (float z in zs)
                for (int side = -1; side <= 1; side += 2)
                    Seats.Add((new Vector3(side * 0.76f, 1.54f, z), 0f));
        }

        void Decorate(float L, float H)
        {
            // barras de neon nas bordas do teto (as variações já vêm com neon no modelo)
            if (Variant == 0)
            {
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
            if (fwd.sqrMagnitude > 0.01f)
            {
                var targetRot = Quaternion.LookRotation(fwd.normalized);
                if (dt > 0)
                {
                    Vector3 flatF = new Vector3(dF.x, 0, dF.z), flatB = new Vector3(dB.x, 0, dB.z);
                    float turnAng = flatF.sqrMagnitude > 0.001f && flatB.sqrMagnitude > 0.001f
                        ? Vector3.SignedAngle(flatB, flatF, Vector3.up) : 0f;
                    lastSteer = Mathf.Clamp(turnAng / 24f, -1f, 1f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-dt * 10f));
                }
                else
                {
                    lastSteer = 0f;
                    transform.rotation = targetRot;
                }
                var e = transform.rotation.eulerAngles;
                smoothPitch = e.x > 180f ? e.x - 360f : e.x;
                smoothRoll = e.z > 180f ? e.z - 360f : e.z;
            }
            Velocity = dt > 0 ? (pos - old) / dt : Vector3.zero;
        }

        // ---------------- dirigir (jogador no volante) ----------------
        public bool Manual;
        /// <summary>Modo seguro: dirigindo, a carreta não passa de ~15 km/h e responde com mais suavidade.</summary>
        public bool SafeMode = true;
        public System.Func<float, float, float> GroundHeight;
        float yaw;
        public float ManualSpeed => speed;

        /// <summary>Posição na RUA (ao lado e atrás da carreta) onde cada personagem acompanha correndo ou dançando.</summary>
        public Vector3 StreetSlot(int k, bool stopped)
        {
            int side = (k % 2 == 0) ? 1 : -1;
            int row = k / 2;
            float x = side * (HalfWidth + (stopped ? 2.8f + (row % 2) * 0.9f : 2.6f + (row % 2) * 0.7f));
            float z = stopped ? (4.0f - row * 2.5f) : (2.2f - row * 2.4f);
            Vector3 wp = transform.TransformPoint(new Vector3(x, 0f, z));
            wp.y = Ground(wp);
            return wp;
        }

        /// <summary>Empurra suavemente um personagem na rua para fora do corpo da carreta se houver sobreposição.</summary>
        public Vector3 PushOut(Vector3 worldPos, float radius)
        {
            Vector3 lp = transform.InverseTransformPoint(worldPos);
            float hw = HalfWidth + radius + 0.18f;
            float hl = LENGTH * 0.5f + radius + 0.18f;
            if (Mathf.Abs(lp.x) >= hw || Mathf.Abs(lp.z) >= hl || lp.y > RoofY + 0.4f) return Vector3.zero;
            float dx = hw - Mathf.Abs(lp.x);
            float dz = hl - Mathf.Abs(lp.z);
            Vector3 localPush = dx <= dz
                ? new Vector3((lp.x >= 0f ? 1f : -1f) * dx, 0f, 0f)
                : new Vector3(0f, 0f, (lp.z >= 0f ? 1f : -1f) * dz);
            Vector3 wp = transform.TransformDirection(localPush);
            wp.y = 0f;
            return wp;
        }

        /// <summary>Volante: steer -1..1, acelerador -1 (ré/freio) .. 1.</summary>
        float lastSteer;
        public void Drive(float steer, float throttle, float dt)
        {
            lastSteer = steer;
            if (!Manual)
            {
                Manual = true;
                var e = transform.eulerAngles;
                yaw = e.y;
                smoothPitch = e.x > 180f ? e.x - 360f : e.x;
                smoothRoll = e.z > 180f ? e.z - 360f : e.z;
            }
            float maxF = SafeMode ? 4.2f : 16f, maxR = SafeMode ? 2.2f : 5f;
            float accel = SafeMode ? 2.4f : 3.8f;
            if (throttle > 0.05f) speed += throttle * (speed < 0 ? 10f : accel) * dt;
            else if (throttle < -0.05f) speed += throttle * (speed > 0 ? 10f : accel * 0.7f) * dt;
            if (SafeMode && speed > maxF) speed = Mathf.MoveTowards(speed, maxF, 6f * dt);   // ligou o modo seguro andando rápido: freia
            else if (Mathf.Abs(throttle) <= 0.05f) speed = Mathf.MoveTowards(speed, 0, 2.8f * dt);
            speed = Mathf.Clamp(speed, -maxR, SafeMode ? Mathf.Max(maxF, speed) : maxF);
            // vira mais devagar em alta velocidade (carreta comprida)
            float turn = steer * Mathf.Clamp(speed / 5.5f, -1f, 1f) * (40f / (1f + Mathf.Abs(speed) * 0.08f));
            float newYaw = yaw + turn * dt;
            var rot = Quaternion.Euler(0, newYaw, 0);
            Vector3 step = rot * Vector3.forward * speed * dt;
            Vector3 next = transform.position + step;
            if (GroundHeight != null) next.y = Ground(next);
            // bate em prédios: confere o corpo da carreta na posição nova (acima do meio-fio)
            var half = new Vector3(HalfWidth * 0.94f, 1.1f, LENGTH / 2f * 0.96f);
            if (Physics.CheckBox(next + Vector3.up * 2.0f, half, rot, 1 << 0, QueryTriggerInteraction.Ignore))
            {
                // tenta girar no lugar sem avançar antes de frear suave (evita tranco oscilando)
                Vector3 stay = transform.position;
                if (!Physics.CheckBox(stay + Vector3.up * 2.0f, half, rot, 1 << 0, QueryTriggerInteraction.Ignore))
                    yaw = newYaw;
                speed = Mathf.MoveTowards(speed, 0f, 18f * dt);
                if (Mathf.Abs(speed) > 0.5f) speed *= 0.35f;
                Velocity = Vector3.zero;
                return;
            }
            yaw = newYaw;
            var old = transform.position;
            // inclina suavemente conforme o chão nas rodas da frente/trás e laterais
            Vector3 fwd = rot * Vector3.forward, right = rot * Vector3.right;
            float hf = Ground(next + fwd * LENGTH * 0.4f), hb = Ground(next - fwd * LENGTH * 0.4f);
            float hl = Ground(next - right * HalfWidth), hr = Ground(next + right * HalfWidth);
            float targetPitch = Mathf.Clamp(-Mathf.Atan2(hf - hb, LENGTH * 0.8f) * Mathf.Rad2Deg, -18f, 18f);
            float targetRoll = Mathf.Clamp(Mathf.Atan2(hl - hr, HalfWidth * 2f) * Mathf.Rad2Deg, -12f, 12f);
            float k = dt > 0 ? 1f - Mathf.Exp(-dt * 10f) : 1f;
            smoothPitch = Mathf.LerpAngle(smoothPitch, targetPitch, k);
            smoothRoll = Mathf.LerpAngle(smoothRoll, targetRoll, k);
            next.y = (hf + hb) / 2f;
            transform.SetPositionAndRotation(next, Quaternion.Euler(smoothPitch, yaw, smoothRoll));
            Velocity = dt > 0 ? (next - old) / dt : Vector3.zero;
        }
        float Ground(Vector3 p) => GroundHeight != null ? GroundHeight(p.x, p.z) : 0f;

        // volta sozinha para o percurso depois que o jogador larga o volante
        public bool Returning;
        int retIdx; float stuckT, retT;
        public void ReturnToRoute()
        {
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < path.Count; i++) { float d = (path[i] - transform.position).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
            retIdx = best; Returning = true; stuckT = 0; retT = 0;
            if (bd < 6f * 6f) { Returning = false; ResumeRoute(); }
        }
        void TickReturn(float dt)
        {
            Vector3 tgt = path[retIdx];
            Vector3 to = tgt - transform.position; to.y = 0;
            if (to.magnitude < 7f) { Returning = false; ResumeRoute(); return; }
            float ang = Vector3.SignedAngle(transform.forward, to, Vector3.up);
            bool reverse = Mathf.Abs(ang) > 120f && to.magnitude < 25f;
            float steer = Mathf.Clamp(ang / 35f, -1f, 1f) * (reverse ? -1 : 1);
            float throttle = reverse ? -0.5f : Mathf.Lerp(0.6f, 0.15f, Mathf.Abs(ang) / 90f);
            Drive(steer, throttle, dt);
            stuckT = Mathf.Abs(speed) < 0.4f ? stuckT + dt : 0;
            retT += dt;
            // presa em algum prédio ou demorando demais: volta direto para o percurso
            if (stuckT > 3f || retT > 60f) { Returning = false; ResumeRoute(); }
        }

        /// <summary>Volta para o percurso (piloto automático) a partir do ponto mais perto.</summary>
        public void ResumeRoute()
        {
            Manual = false; Returning = false;
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < path.Count; i++) { float d = (path[i] - transform.position).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
            dist = cum[best]; speed = 0; stopTimer = 2f;
            PickNextStop();
            Place(0);
        }

        /// <summary>Começa a volta em um ponto do percurso (ponto de partida escolhido no menu).</summary>
        public void StartAt(float d)
        {
            Manual = false; Returning = false; dist = d; speed = 0; stopTimer = 4f;
            PickNextStop();
            Place(0);
        }

        void PickNextStop()
        {
            if (stopList == null || stopList.Count == 0) return;
            float cur = Mathf.Repeat(dist, total), nx = stopList[0];
            foreach (var s in stopList) if (s > cur + 1f) { nx = s; break; }
            nextStop = nx;
        }

        public void Tick(float dt, Vector3 player, bool ignorePlayer, float beat)
        {
            if (Returning) { TickReturn(dt); Lights(beat); SpinWheels(lastSteer, dt); return; }
            if (Manual) { Lights(beat); SpinWheels(lastSteer, dt); return; }
            // para nos pontos de festa
            if (stopTimer > 0) { stopTimer -= dt; speed = Mathf.MoveTowards(speed, 0, 8f * dt); }
            else
            {
                float want = cruise;
                float dp = DistanceTo(player);
                // comboio na rua: mantém ritmo confortável e espera quem ficou um pouco para trás
                if (!ignorePlayer && dp > 35f && dp < 120f) want = 0;
                else if (!ignorePlayer && dp > 20f && dp < 120f) want = Mathf.Min(want, 1.4f);
                if (!ignorePlayer && BlockedAhead(player)) want = 0;          // não atropela ninguém
                float ahead = Mathf.Repeat(nextStop - dist, total);
                if (ahead < 25f) want = Mathf.Min(want, 1.5f + ahead * 0.4f);
                if (ahead < 0.5f || ahead > total - 1f)
                {
                    stopTimer = 15f;
                    if (stopList != null && stopList.Count > 0) PickNextStop();
                    else nextStop = Mathf.Repeat(nextStop + total * 0.25f, total);
                }
                speed = Mathf.MoveTowards(speed, want, (want > speed ? 2.5f : 6f) * dt);
            }
            dist += speed * dt;
            Place(dt);
            Lights(beat);
            SpinWheels(lastSteer, dt);
        }

        void Lights(float beat)
        {
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

        public float DistanceTo(Vector3 p)
        {
            var lp = transform.InverseTransformPoint(p);
            float dx = Mathf.Max(0, Mathf.Abs(lp.x) - HalfWidth), dz = Mathf.Max(0, Mathf.Abs(lp.z) - LENGTH / 2f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
