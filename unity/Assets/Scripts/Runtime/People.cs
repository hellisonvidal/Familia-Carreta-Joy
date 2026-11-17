using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CarretaJoy
{
    /// <summary>
    /// Gente da cidade (personagens CC0 da Quaternius com cores sorteadas): passeia pelas calçadas,
    /// conversa, dança quando a carreta passa e vai sentada nos bancos em cima da carreta.
    /// Só existe perto do jogador (aparece e some fora da vista).
    /// </summary>
    public class People : MonoBehaviour
    {
        static readonly string[] Types = { "Casual_Male", "Casual_Female", "Casual2_Male", "Casual2_Female", "Casual3_Male", "Casual3_Female",
            "Casual_Bald", "Suit_Male", "Suit_Female", "Worker_Male", "Worker_Female", "Chef_Female", "Doctor_Male_Young", "Doctor_Female_Young",
            "OldClassy_Male", "OldClassy_Female", "Cowboy_Male", "Cowboy_Female" };
        static readonly Color[] Skin = { new Color(0.98f, 0.82f, 0.7f), new Color(0.93f, 0.74f, 0.6f), new Color(0.82f, 0.6f, 0.45f),
                                         new Color(0.68f, 0.47f, 0.33f), new Color(0.52f, 0.34f, 0.22f), new Color(0.36f, 0.23f, 0.15f) };
        static readonly Color[] Hair = { new Color(0.1f, 0.07f, 0.05f), new Color(0.25f, 0.15f, 0.08f), new Color(0.45f, 0.3f, 0.15f),
                                         new Color(0.85f, 0.7f, 0.4f), new Color(0.6f, 0.25f, 0.1f), new Color(0.7f, 0.7f, 0.72f) };
        static readonly Color[] Cloth = { new Color(0.9f, 0.2f, 0.25f), new Color(0.2f, 0.45f, 0.9f), new Color(0.2f, 0.7f, 0.4f), new Color(0.98f, 0.8f, 0.2f),
                                          new Color(0.95f, 0.5f, 0.15f), new Color(0.6f, 0.3f, 0.8f), new Color(0.95f, 0.95f, 0.95f), new Color(0.15f, 0.15f, 0.2f),
                                          new Color(0.95f, 0.45f, 0.65f), new Color(0.3f, 0.75f, 0.85f), new Color(0.5f, 0.35f, 0.25f), new Color(0.35f, 0.4f, 0.5f) };

        class Person
        {
            public GameObject go; public Animator an;
            public PlayableGraph graph; public AnimationPlayableOutput output; public AnimationClipPlayable play; public AnimationClip cur;
            public int mode;            // 0 anda, 1 conversa/parado, 2 sentado na carreta, 3 dança
            public Vector2[] path; public int seg; public float u, dir = 1, speed;
            public float yaw, think; public int seat = -1;
        }

        PgCity city; Truck truck;
        readonly List<GameObject> prefabs = new List<GameObject>();
        AnimationClip walk, idle, talk, sit;
        AnimationClip[] dances;
        readonly List<Person> street = new List<Person>(), riders = new List<Person>();
        readonly System.Random rnd = new System.Random(7);
        public int MaxStreet = 18;
        float spawnT;
        Material faceMat;

        public void Init(PgCity c, Truck t, AnimationClip[] danceClips)
        {
            city = c; truck = t; dances = danceClips;
            foreach (var n in Types) { var p = Resources.Load<GameObject>("People/" + n); if (p != null) prefabs.Add(p); }
            walk = KidClips.Load("Animations/Livres/Passeio_Loop") ?? KidClips.Load("Animations/Walk");
            idle = KidClips.Load("Animations/Idle");
            talk = KidClips.Load("Animations/Livres/Conversa_Loop") ?? idle;
            sit = KidClips.Load("Animations/Livres/Sentado_Loop") ?? idle;
            faceMat = Mats.Lit(new Color(0.08f, 0.06f, 0.06f), 0.4f);
            SeatRiders();
        }

        /// <summary>Carreta nova (trocou a variação): refaz quem vai sentado nela.</summary>
        public void SetTruck(Truck t)
        {
            truck = t;
            foreach (var p in riders) Kill(p);
            riders.Clear();
            SeatRiders();
        }

        void SeatRiders()
        {
            if (truck == null || prefabs.Count == 0) return;
            int n = Mathf.Min(truck.Seats.Count, Settings.Quality == 0 ? 4 : 6);
            for (int i = 0; i < n; i++)
            {
                var p = Spawn(Random(1.5f, 1.85f));
                if (p == null) return;
                p.mode = 2; p.seat = i;
                p.go.transform.SetParent(truck.transform, false);
                Play(p, sit, Random(0, 2f));
                riders.Add(p);
            }
        }

        float Random(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
        T Pick<T>(T[] a) => a[rnd.Next(a.Length)];

        Person Spawn(float height)
        {
            if (prefabs.Count == 0) return null;
            var prefab = prefabs[rnd.Next(prefabs.Count)];
            var root = new GameObject("Pessoa");
            var m = Instantiate(prefab, root.transform);
            m.transform.localPosition = Vector3.zero;
            // altura pela caixa das partes no mundo (o modelo vem girado do Blender)
            var parts = m.GetComponentsInChildren<SkinnedMeshRenderer>();
            Bounds bb = new Bounds(root.transform.position, Vector3.zero); bool any = false;
            foreach (var smr in parts)
            {
                if (smr.sharedMesh == null) continue;
                smr.updateWhenOffscreen = false;
                smr.quality = SkinQuality.Bone2;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                smr.sharedMaterial = ColorFor(smr.gameObject.name);
                if (!any) { bb = smr.bounds; any = true; } else bb.Encapsulate(smr.bounds);
            }
            var an = m.GetComponent<Animator>();
            float h = any ? bb.size.y : 0;
            if ((h < 0.3f || h > 8f) && an != null && an.isHuman)
            {
                var head = an.GetBoneTransform(HumanBodyBones.Head); var foot = an.GetBoneTransform(HumanBodyBones.LeftFoot);
                if (head && foot) h = (head.position.y - foot.position.y) * 1.3f;
            }
            if (h < 0.05f) h = 1.7f;
            float k = height / h;
            m.transform.localScale *= k;
            if (any) m.transform.localPosition += Vector3.up * (root.transform.position.y - bb.min.y) * k;   // pés no chão
            foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.layer = 2;
            if (an == null) an = m.AddComponent<Animator>();
            an.applyRootMotion = false;
            an.cullingMode = AnimatorCullingMode.CullCompletely;
            an.runtimeAnimatorController = null;
            var p = new Person { go = root, an = an };
            p.graph = PlayableGraph.Create("Pessoa");
            p.graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            return p;
        }

        Material ColorFor(string part)
        {
            string k = part.StartsWith("M_") ? part.Substring(2) : part;
            int cut = k.IndexOfAny(new[] { '.', ' ' }); if (cut > 0) k = k.Substring(0, cut);
            switch (k)
            {
                case "Skin": return Mats.Lit(Pick(Skin), 0.3f);
                case "Face": return faceMat;
                case "Hair": return Mats.Lit(Pick(Hair), 0.35f);
                case "Black": case "DarkClothes": return Mats.Lit(new Color(0.08f, 0.08f, 0.1f), 0.3f);
                case "Belt": case "Brown": case "HatBrown": return Mats.Lit(new Color(0.3f, 0.18f, 0.1f), 0.3f);
                case "HatLightBrown": return Mats.Lit(new Color(0.7f, 0.5f, 0.3f), 0.3f);
                case "Main": case "Clothes": return Mats.Lit(new Color(0.95f, 0.95f, 0.95f), 0.3f);   // jaleco / roupa de chef
                default: return Mats.Lit(Pick(Cloth), 0.3f);
            }
        }

        void Play(Person p, AnimationClip clip, float t0 = 0)
        {
            if (clip == null || p.cur == clip) return;
            if (p.play.IsValid()) p.play.Destroy();
            if (!p.graph.IsValid()) return;
            if (!p.output.IsOutputValid()) p.output = AnimationPlayableOutput.Create(p.graph, "o", p.an);
            p.play = AnimationClipPlayable.Create(p.graph, clip);
            p.play.SetTime(t0);
            p.output.SetSourcePlayable(p.play);
            p.cur = clip;
            if (!p.graph.IsPlaying()) p.graph.Play();
        }

        void Kill(Person p)
        {
            if (p.graph.IsValid()) p.graph.Destroy();
            if (p.go != null) Destroy(p.go);
        }

        // ---------------- rua ----------------
        bool SpawnOnStreet(Vector3 focus)
        {
            // escolhe uma rua carregada, perto mas fora do foco (atrás das casas ou longe)
            var roads = city.RoadsNear(focus, 140f);
            if (roads.Count == 0) return false;
            for (int tries = 0; tries < 6; tries++)
            {
                var r = roads[rnd.Next(roads.Count)];
                if (r.pts.Length < 2 || r.unpaved) continue;
                int seg = rnd.Next(r.pts.Length - 1);
                float u = Random(0, 1);
                Vector2 a = r.pts[seg], b = r.pts[seg + 1];
                Vector2 pos = Vector2.Lerp(a, b, u);
                float d = Vector2.Distance(pos, new Vector2(focus.x, focus.z));
                if (d < 25f || d > 140f) continue;
                // calçada: ao lado da rua
                var dir = (b - a).normalized; var nrm = new Vector2(-dir.y, dir.x);
                float side = rnd.Next(2) == 0 ? 1 : -1;
                var off = nrm * side * (r.width / 2f + 1.3f);
                var p = Spawn(Random(1.55f, 1.85f));
                if (p == null) return false;
                p.path = new Vector2[r.pts.Length];
                for (int i = 0; i < r.pts.Length; i++)
                {
                    Vector2 dd = i < r.pts.Length - 1 ? (r.pts[i + 1] - r.pts[i]).normalized : (r.pts[i] - r.pts[i - 1]).normalized;
                    p.path[i] = r.pts[i] + new Vector2(-dd.y, dd.x) * side * (r.width / 2f + 1.3f);
                }
                p.seg = seg; p.u = u; p.dir = rnd.Next(2) == 0 ? 1 : -1;
                p.speed = Random(1.0f, 1.45f);
                p.mode = rnd.NextDouble() < 0.72 ? 0 : 1;
                p.think = Random(4, 14);
                p.go.transform.position = Ground(pos + off);
                p.yaw = Mathf.Atan2(dir.x * p.dir, dir.y * p.dir) * Mathf.Rad2Deg;
                if (p.mode == 1) p.yaw += Random(-120, 120);
                p.go.transform.rotation = Quaternion.Euler(0, p.yaw, 0);
                Play(p, p.mode == 0 ? walk : (rnd.NextDouble() < 0.6 ? talk : idle), Random(0, 2f));
                street.Add(p);
                return true;
            }
            return false;
        }

        Vector3 Ground(Vector2 p) => new Vector3(p.x, city.Height(p.x, p.y) + 0.12f, p.y);

        public void Tick(Vector3 focus, float dt, bool truckStopped)
        {
            if (city == null) return;
            int max = Settings.Quality == 0 ? MaxStreet / 2 : Settings.Quality == 1 ? MaxStreet : MaxStreet + 8;
            // some quem ficou longe; aparece gente nova aos poucos
            for (int i = street.Count - 1; i >= 0; i--)
            {
                var p = street[i];
                if (p.go == null || (p.go.transform.position - focus).sqrMagnitude > 175f * 175f) { Kill(p); street.RemoveAt(i); }
            }
            spawnT -= dt;
            if (street.Count < max && spawnT <= 0) { spawnT = 0.25f; SpawnOnStreet(focus); }

            Vector3 tp = truck != null ? truck.transform.position : new Vector3(1e6f, 0, 0);
            foreach (var p in street) TickStreet(p, dt, tp, truckStopped);
            // na carreta: sentados olhando a festa; nas paradas levantam e dançam
            for (int i = 0; i < riders.Count; i++)
            {
                var p = riders[i];
                if (truck == null || p.seat >= truck.Seats.Count) continue;
                var s = truck.Seats[p.seat];
                // sentado: os pés na frente do banco; na parada fica de pé ao lado dele
                p.go.transform.localPosition = s.pos + Quaternion.Euler(0, s.yaw, 0) * Vector3.forward * (truckStopped ? 0.45f : 0.12f);
                p.go.transform.localRotation = Quaternion.Euler(0, s.yaw, 0);
                Play(p, truckStopped && dances != null && dances.Length > 0 ? dances[(i + 1) % dances.Length] : sit, Random(0, 1f));
            }
        }

        void TickStreet(Person p, float dt, Vector3 truckPos, bool truckStopped)
        {
            var tr = p.go.transform;
            float dTruck = (tr.position - truckPos).sqrMagnitude;
            // a carreta da alegria passou perto: todo mundo dança
            bool party = dTruck < 26f * 26f;
            if (party && p.mode != 3 && dances != null && dances.Length > 0)
            {
                p.mode = 3; Play(p, dances[rnd.Next(dances.Length)], Random(0, 1f));
                var look = truckPos - tr.position; look.y = 0;
                if (look.sqrMagnitude > 0.1f) tr.rotation = Quaternion.LookRotation(look);
            }
            else if (!party && p.mode == 3) { p.mode = 0; Play(p, walk); }

            if (p.mode == 0)
            {
                // anda pela calçada; no fim da rua dá meia-volta
                var a = p.path[p.seg]; var b = p.path[p.seg + 1];
                float L = Mathf.Max(0.01f, Vector2.Distance(a, b));
                p.u += p.dir * p.speed * dt / L;
                if (p.u > 1) { if (p.seg + 2 < p.path.Length) { p.seg++; p.u = 0; } else { p.u = 1; p.dir = -1; } }
                else if (p.u < 0) { if (p.seg > 0) { p.seg--; p.u = 1; } else { p.u = 0; p.dir = 1; } }
                a = p.path[p.seg]; b = p.path[p.seg + 1];
                var pos = Vector2.Lerp(a, b, p.u);
                tr.position = Ground(pos);
                var d = (b - a) * p.dir;
                if (d.sqrMagnitude > 0.0001f)
                    tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(new Vector3(d.x, 0, d.y)), 1 - Mathf.Exp(-dt * 6f));
                p.think -= dt;
                if (p.think < 0) { p.think = Random(4, 12); p.mode = 1; Play(p, rnd.NextDouble() < 0.5 ? talk : idle); }
            }
            else if (p.mode == 1)
            {
                p.think -= dt;
                if (p.think < 0) { p.think = Random(6, 16); p.mode = 0; Play(p, walk); }
            }
        }

        void OnDestroy()
        {
            foreach (var p in street) if (p.graph.IsValid()) p.graph.Destroy();
            foreach (var p in riders) if (p.graph.IsValid()) p.graph.Destroy();
        }
    }
}
