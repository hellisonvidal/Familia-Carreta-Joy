using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CarretaJoy
{
    /// <summary>
    /// Gente da cidade (personagem humano da Kenney, CC0, com roupas e tons de pele sorteados):
    /// passeia pelas calçadas e conversa; quando a carreta passa, uns acenam, outros gritam e chamam,
    /// outros dançam e alguns nem ligam. Vai gente sentada na arquibancada da parte de trás da carreta.
    /// Só existe perto do jogador (aparece e some fora da vista).
    /// </summary>
    public class People : MonoBehaviour
    {
        class Person
        {
            public GameObject go; public Animator an;
            public PlayableGraph graph; public AnimationPlayableOutput output; public AnimationClipPlayable play; public AnimationClip cur;
            public int mode;            // 0 anda, 1 conversa/parado, 2 sentado na carreta, 3 reage à carreta
            public Vector2[] path; public int seg; public float u, dir = 1, speed;
            public float yaw, think, shoutT; public int seat = -1;
            public int reaction = -1;   // 0 acena, 1 grita/chama, 2 dança, 3 ignora
        }

        PgCity city; Truck truck;
        GameObject prefab;
        readonly List<Material> skins = new List<Material>();
        AnimationClip walk, idle, talk, sit, wave, call;
        AnimationClip[] dances;
        AudioClip[] shouts; AudioClip claps, crowd;
        readonly List<Person> street = new List<Person>(), riders = new List<Person>();
        readonly System.Random rnd = new System.Random(7);
        public int MaxStreet = 18;
        float spawnT, crowdT;
        AudioSource voice;

        public void Init(PgCity c, Truck t, AnimationClip[] danceClips)
        {
            city = c; truck = t; dances = danceClips;
            prefab = Resources.Load<GameObject>("People/Pessoa");
            var baseMat = Resources.Load<Material>("Materials/Lit");
            foreach (var tex in Resources.LoadAll<Texture2D>("People/Peles"))
            {
                var m = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", 0.15f);
                skins.Add(m);
            }
            walk = KidClips.Load("Animations/Livres/Passeio_Loop") ?? KidClips.Load("Animations/Walk");
            idle = KidClips.Load("Animations/Idle");
            talk = KidClips.Load("Animations/Livres/Conversa_Loop") ?? idle;
            sit = KidClips.Load("Animations/Livres/Sentado_Loop") ?? idle;
            wave = KidClips.Load("Animations/CMU/Acenar_Loop") ?? idle;
            call = KidClips.Load("Animations/Livres/Chamar_Loop") ?? wave;
            shouts = new[] { Resources.Load<AudioClip>("Sounds/Grito_1"), Resources.Load<AudioClip>("Sounds/Grito_2"), Resources.Load<AudioClip>("Sounds/Uuu") };
            claps = Resources.Load<AudioClip>("Sounds/Palmas"); crowd = Resources.Load<AudioClip>("Sounds/Torcida");
            voice = gameObject.AddComponent<AudioSource>(); voice.playOnAwake = false; voice.spatialBlend = 0f;
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
            if (truck == null || prefab == null) return;
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

        Person Spawn(float height)
        {
            if (prefab == null) return null;
            var root = new GameObject("Pessoa");
            var m = Instantiate(prefab, root.transform);
            m.transform.localPosition = Vector3.zero;
            var mat = skins.Count > 0 ? skins[rnd.Next(skins.Count)] : null;
            // altura pela caixa das partes no mundo (o modelo vem girado do Blender)
            Bounds bb = new Bounds(root.transform.position, Vector3.zero); bool any = false;
            foreach (var smr in m.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.sharedMesh == null) continue;
                smr.updateWhenOffscreen = false;
                smr.quality = SkinQuality.Bone2;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (mat != null) smr.sharedMaterial = mat;
                if (!any) { bb = smr.bounds; any = true; } else bb.Encapsulate(smr.bounds);
            }
            var an = m.GetComponent<Animator>();
            float h = any ? bb.size.y : 0;
            if ((h < 0.3f || h > 8f) && an != null && an.isHuman)
            {
                var head = an.GetBoneTransform(HumanBodyBones.Head); var foot = an.GetBoneTransform(HumanBodyBones.LeftFoot);
                if (head && foot) h = (head.position.y - foot.position.y) * 1.2f;
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

        void Shout(Vector3 at, float vol)
        {
            if (shouts == null) return;
            var c = shouts[rnd.Next(shouts.Length)];
            if (c != null) AudioSource.PlayClipAtPoint(c, at, vol);
        }

        /// <summary>Palmas e gritos da torcida (combos, festa da turma).</summary>
        public void Cheer(bool big)
        {
            if (voice == null) return;
            var c = big ? (crowd != null ? crowd : claps) : claps;
            if (c != null) voice.PlayOneShot(c, big ? 0.55f : 0.35f);
            if (big && claps != null) voice.PlayOneShot(claps, 0.4f);
        }

        // ---------------- rua ----------------
        bool SpawnOnStreet(Vector3 focus)
        {
            // escolhe uma rua carregada, perto mas fora do foco
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
            for (int i = street.Count - 1; i >= 0; i--)
            {
                var p = street[i];
                if (p.go == null || (p.go.transform.position - focus).sqrMagnitude > 175f * 175f) { Kill(p); street.RemoveAt(i); }
            }
            spawnT -= dt;
            if (street.Count < max && spawnT <= 0) { spawnT = 0.25f; SpawnOnStreet(focus); }

            Vector3 tp = truck != null ? truck.transform.position : new Vector3(1e6f, 0, 0);
            foreach (var p in street) TickStreet(p, dt, tp);
            // na carreta: sentados na arquibancada; nas paradas levantam e dançam
            for (int i = 0; i < riders.Count; i++)
            {
                var p = riders[i];
                if (truck == null || p.seat >= truck.Seats.Count) continue;
                var s = truck.Seats[p.seat];
                var rot = Quaternion.Euler(0, s.yaw, 0);
                p.go.transform.localPosition = truckStopped ? new Vector3(s.pos.x, truck.RoofY, s.pos.z) + rot * Vector3.forward * 0.55f
                                                            : s.pos + rot * Vector3.forward * 0.12f;
                p.go.transform.localRotation = truckStopped ? Quaternion.Euler(0, 0, 0) : rot;
                Play(p, truckStopped && dances != null && dances.Length > 0 ? dances[(i + 1) % dances.Length] : sit, Random(0, 1f));
            }
            // torcida nas paradas de festa
            crowdT -= dt;
            if (truckStopped && crowdT <= 0 && (tp - focus).sqrMagnitude < 40f * 40f) { crowdT = 14f; Cheer(false); }
        }

        void TickStreet(Person p, float dt, Vector3 truckPos)
        {
            var tr = p.go.transform;
            float dTruck = (tr.position - truckPos).sqrMagnitude;
            bool near = dTruck < 28f * 28f;
            if (near && p.mode != 3)
            {
                // a carreta da alegria chegou: cada um reage do seu jeito
                if (p.reaction < 0)
                {
                    double x = rnd.NextDouble();
                    p.reaction = x < 0.4 ? 0 : x < 0.62 ? 1 : x < 0.85 ? 2 : 3;
                }
                if (p.reaction != 3)
                {
                    p.mode = 3;
                    Play(p, p.reaction == 0 ? wave : p.reaction == 1 ? call : dances[rnd.Next(dances.Length)], Random(0, 1f));
                    p.shoutT = Random(0.2f, 1.5f);
                }
            }
            else if (!near && p.mode == 3) { p.mode = 0; p.reaction = -1; Play(p, walk); }
            else if (!near) p.reaction = -1;

            if (p.mode == 3)
            {
                var look = truckPos - tr.position; look.y = 0;
                if (look.sqrMagnitude > 0.1f) tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(look), 1 - Mathf.Exp(-dt * 5f));
                if (p.reaction == 1)
                {
                    p.shoutT -= dt;
                    if (p.shoutT <= 0) { p.shoutT = Random(2.5f, 5f); Shout(tr.position + Vector3.up * 1.6f, 0.9f); }
                }
                return;
            }
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
