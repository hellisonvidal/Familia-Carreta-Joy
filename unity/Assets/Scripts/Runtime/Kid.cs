using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CarretaJoy
{
    /// <summary>Animações de um menino (todas Humanoid: o Unity adapta ao corpo de cada um).</summary>
    public class KidClips
    {
        public AnimationClip idle, walk, run, dance, jump, mortal, flip, pirueta;

        public static AnimationClip Load(string path, string contains = null)
        {
            foreach (var c in Resources.LoadAll<AnimationClip>(path))
            {
                if (c.name.StartsWith("__preview__")) continue;
                if (contains == null || c.name.Contains(contains)) return c;
            }
            Debug.LogWarning("Animação não encontrada: " + path + " " + contains);
            return null;
        }
    }

    /// <summary>
    /// Mistura de animações com a Playables API: parado/andando/correndo pela velocidade,
    /// dança, pose no ar e as acrobacias por cima de tudo.
    /// </summary>
    public class KidAnim
    {
        const int IDLE = 0, WALK = 1, RUN = 2, DANCE = 3, AIR = 4, TRICK = 5, COUNT = 6;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        readonly AnimationClipPlayable[] slots = new AnimationClipPlayable[COUNT];
        readonly float[] w = new float[COUNT], t = new float[COUNT];
        readonly KidClips clips;
        public float TrickLength { get; private set; }

        public KidAnim(Animator animator, KidClips c, string name)
        {
            clips = c;
            graph = PlayableGraph.Create("Anim_" + name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "out", animator);
            mixer = AnimationMixerPlayable.Create(graph, COUNT);
            output.SetSourcePlayable(mixer);
            Set(IDLE, c.idle); Set(WALK, c.walk); Set(RUN, c.run); Set(DANCE, c.dance); Set(AIR, c.jump);
            if (slots[AIR].IsValid()) { slots[AIR].SetTime(c.jump.length * 0.42f); slots[AIR].SetSpeed(0); }
            w[IDLE] = 1;
            Apply();
            graph.Play();
        }

        void Set(int i, AnimationClip clip)
        {
            if (slots[i].IsValid()) { graph.Disconnect(mixer, i); slots[i].Destroy(); }
            if (clip == null) return;
            var p = AnimationClipPlayable.Create(graph, clip);
            p.SetApplyFootIK(true);
            graph.Connect(p, 0, mixer, i);
            slots[i] = p;
        }

        public void PlayTrick(AnimationClip clip, float speed = 1f)
        {
            if (clip == null) return;
            Set(TRICK, clip);
            slots[TRICK].SetTime(0);
            slots[TRICK].SetSpeed(speed);
            slots[TRICK].SetDuration(clip.length);
            TrickLength = clip.length / speed;
        }

        public void RestartDance() { if (slots[DANCE].IsValid()) slots[DANCE].SetTime(0); }

        /// <summary>speed em m/s; height = altura do menino (passos menores para os pequenos)</summary>
        public void Update(float dt, float speed, float height, bool grounded, bool dancing, bool trick)
        {
            System.Array.Clear(t, 0, COUNT);
            if (trick) t[TRICK] = 1;
            else if (dancing) t[DANCE] = 1;
            else if (!grounded) t[AIR] = 1;
            else
            {
                float k = height / 1.3f;
                float walkV = 1.25f * k, runV = 3.2f * k;
                if (speed < 0.15f) t[IDLE] = 1;
                else if (speed < walkV) { float a = Mathf.InverseLerp(0.15f, walkV, speed); t[IDLE] = 1 - a; t[WALK] = a; }
                else { float a = Mathf.Clamp01(Mathf.InverseLerp(walkV * 1.3f, runV, speed)); t[WALK] = 1 - a; t[RUN] = a; }
                if (slots[WALK].IsValid()) slots[WALK].SetSpeed(Mathf.Clamp(speed / walkV, 0.7f, 1.5f));
                if (slots[RUN].IsValid()) slots[RUN].SetSpeed(Mathf.Clamp(speed / runV, 0.8f, 1.4f));
            }
            float rate = trick ? 30f : 9f;
            float kk = 1 - Mathf.Exp(-dt * rate), sum = 0;
            for (int i = 0; i < COUNT; i++) { w[i] = Mathf.Lerp(w[i], slots[i].IsValid() ? t[i] : 0, kk); sum += w[i]; }
            if (sum < 1e-4f) { w[IDLE] = 1; sum = 1; }
            for (int i = 0; i < COUNT; i++) w[i] /= sum;
            Apply();
        }

        void Apply() { for (int i = 0; i < COUNT; i++) mixer.SetInputWeight(i, slots[i].IsValid() ? w[i] : 0); }

        public void Destroy() { if (graph.IsValid()) graph.Destroy(); }
    }

    /// <summary>Repassa o movimento das acrobacias (root motion) para o menino.</summary>
    public class RootRelay : MonoBehaviour
    {
        public Kid kid;
        Animator an;
        void Awake() { an = GetComponent<Animator>(); }
        void OnAnimatorMove() { if (kid != null && an != null) kid.OnRootMotion(an.deltaPosition); }
    }

    /// <summary>Um menino jogável: anda, corre, pula, faz acrobacias, sobe na carreta e dança.</summary>
    public class Kid : MonoBehaviour
    {
        public string Name;
        public float Height = 1.3f;
        public CharacterController cc;
        public KidAnim anim;
        public KidClips clips;
        public Transform model;

        public Vector3 vel;
        public bool Grounded = true;
        public bool Dancing;
        public bool Running;
        public string Trick;
        float trickT, trickLen;
        Vector3 rootAccum;

        // carreta
        public Truck truck;
        public bool OnTruck;
        Vector3 local; float localVy;
        float climbT = -1; Vector3 climbFrom, climbToLocal; bool climbingUp;

        public float WalkSpeed => 1.7f * Height / 1.3f;
        public float RunSpeed => 4.6f * Height / 1.3f;
        public float HorizontalSpeed { get; private set; }
        public bool Busy => climbT >= 0;

        public static Kid Create(string name, GameObject prefab, float height, KidClips clips, Vector3 pos)
        {
            var root = new GameObject(name);
            root.transform.position = pos;
            root.layer = 2; // Ignore Raycast (a câmera não bate nos meninos)
            var kid = root.AddComponent<Kid>();
            kid.Name = name; kid.Height = height; kid.clips = clips;

            var m = Instantiate(prefab, root.transform);
            m.name = "Corpo";
            float h = 1.7f;
            var smr = m.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null && smr.sharedMesh != null)
            {
                var b = smr.sharedMesh.bounds;
                h = Mathf.Max(0.1f, b.size.y * smr.transform.lossyScale.y);
                smr.updateWhenOffscreen = true;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                smr.quality = SkinQuality.Bone4;
            }
            m.transform.localScale *= height / h;
            m.transform.localPosition = Vector3.zero;
            m.transform.localRotation = Quaternion.identity;
            foreach (var t in m.GetComponentsInChildren<Transform>()) t.gameObject.layer = 2;
            kid.model = m.transform;

            var cc = root.AddComponent<CharacterController>();
            cc.height = height; cc.radius = 0.2f * height; cc.center = Vector3.up * height / 2f;
            cc.stepOffset = 0.35f; cc.slopeLimit = 50f; cc.skinWidth = 0.03f;
            kid.cc = cc;

            var an = m.GetComponent<Animator>();
            if (an == null) an = m.AddComponent<Animator>();
            an.applyRootMotion = true;
            an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            an.runtimeAnimatorController = null;
            m.AddComponent<RootRelay>().kid = kid;
            kid.anim = new KidAnim(an, clips, name);
            return kid;
        }

        void OnDestroy() { anim?.Destroy(); }

        // ---------------- ações ----------------
        public void Jump()
        {
            if (Busy || Trick != null) return;
            if (OnTruck) { if (localVy == 0) localVy = 5.2f; Dancing = false; return; }
            if (Grounded) { vel.y = 6.2f; Grounded = false; Dancing = false; }
        }

        public void DoTrick(string name)
        {
            if (Busy || Trick != null) return;
            AnimationClip c = name == "mortal" ? clips.mortal : name == "flip" ? clips.flip : clips.pirueta;
            if (c == null) return;
            Dancing = false;
            Trick = name; trickT = 0;
            anim.PlayTrick(c, 1.1f);
            trickLen = anim.TrickLength;
            Game.I?.AddScore(name == "mortal" ? 150 : name == "flip" ? 120 : 180, name.ToUpper() + "!");
        }

        public void ToggleDance()
        {
            if (Busy || Trick != null) return;
            Dancing = !Dancing;
            if (Dancing) anim.RestartDance();
        }

        public void OnRootMotion(Vector3 delta)
        {
            if (Trick != null) rootAccum += delta;
        }

        /// <summary>Perto da carreta: sobe pela lateral. Em cima: pula para a calçada.</summary>
        public bool ClimbOrDescend()
        {
            if (truck == null || Busy || Trick != null) return false;
            if (!OnTruck)
            {
                if (truck.DistanceTo(transform.position) > 3.5f) return false;
                climbingUp = true; climbT = 0; climbFrom = transform.position;
                var lp = truck.transform.InverseTransformPoint(transform.position);
                climbToLocal = truck.ClampToRoof(new Vector3(lp.x * 0.3f, 0, lp.z));
                cc.enabled = false; Dancing = false;
                anim.PlayTrick(clips.jump, 1.2f); Trick = "subir"; trickLen = 0.75f; trickT = 0;
                return true;
            }
            else
            {
                climbingUp = false; climbT = 0;
                climbFrom = transform.position;
                float side = local.x >= 0 ? 1 : -1;
                climbToLocal = new Vector3(side * (truck.HalfWidth + 1.4f), 0, local.z);
                Dancing = false;
                anim.PlayTrick(clips.jump, 1.2f); Trick = "descer"; trickLen = 0.75f; trickT = 0;
                return true;
            }
        }

        public void PlaceOnTruck(Truck t, Vector3 localPos)
        {
            truck = t; OnTruck = true; cc.enabled = false; local = t.ClampToRoof(localPos); localVy = 0;
            transform.position = t.RoofToWorld(local);
        }

        // ---------------- movimento ----------------
        /// <summary>wish: direção no mundo (comprimento 0..1).</summary>
        public void Tick(Vector3 wish, float dt)
        {
            wish.y = 0;
            if (wish.sqrMagnitude > 1) wish.Normalize();
            if (wish.sqrMagnitude > 0.04f && Dancing && Trick == null) Dancing = false;

            if (climbT >= 0) { TickClimb(dt); anim.Update(dt, 0, Height, true, false, true); return; }

            bool tricking = Trick != null;
            if (tricking)
            {
                trickT += dt;
                if (trickT >= trickLen) { Trick = null; if (!OnTruck) vel.y = 0; }
            }

            float target = Running ? RunSpeed : WalkSpeed;
            Vector3 hv;
            if (OnTruck) TickOnTruck(wish, target, dt, tricking, out hv);
            else TickGround(wish, target, dt, tricking, out hv);

            // vira para onde está indo
            if (hv.sqrMagnitude > 0.05f && !tricking)
            {
                var q = Quaternion.LookRotation(new Vector3(hv.x, 0, hv.z));
                transform.rotation = Quaternion.Slerp(transform.rotation, q, 1 - Mathf.Exp(-dt * 12f));
            }
            HorizontalSpeed = new Vector3(hv.x, 0, hv.z).magnitude;
            anim.Update(dt, HorizontalSpeed, Height, Grounded, Dancing, Trick != null);
        }

        void TickGround(Vector3 wish, float target, float dt, bool tricking, out Vector3 hv)
        {
            Vector3 h = new Vector3(vel.x, 0, vel.z);
            if (tricking)
            {
                // a própria animação move o corpo (mortal sobe e volta ao chão)
                var d = rootAccum; rootAccum = Vector3.zero;
                h = Vector3.Lerp(h, Vector3.zero, 1 - Mathf.Exp(-dt * 6f));
                cc.Move(d + h * dt);
                vel = new Vector3(h.x, 0, h.z);
                Grounded = true;
                hv = h;
                return;
            }
            rootAccum = Vector3.zero;
            Vector3 want = wish * target;
            float acc = wish.sqrMagnitude > 0.01f ? (Grounded ? 30f : 8f) : (Grounded ? 18f : 2f);
            h = Vector3.MoveTowards(h, want, acc * dt);
            vel.x = h.x; vel.z = h.z;
            vel.y += -20f * dt;
            var flags = cc.Move(vel * dt);
            bool was = Grounded;
            Grounded = (flags & CollisionFlags.Below) != 0;
            if (Grounded && vel.y < 0) vel.y = -2f;
            if (!was && Grounded) Game.I?.OnLand(this);
            hv = h;
        }

        void TickOnTruck(Vector3 wish, float target, float dt, bool tricking, out Vector3 hv)
        {
            // movimento no "chão" do teto da carreta, em coordenadas da carreta
            Vector3 lw = truck.transform.InverseTransformDirection(wish) * target * 0.8f;
            Vector3 cur = new Vector3(vel.x, 0, vel.z);
            cur = Vector3.MoveTowards(cur, tricking ? Vector3.zero : lw, 20f * dt);
            vel.x = cur.x; vel.z = cur.z;
            local += cur * dt;
            if (tricking)
            {
                var d = truck.transform.InverseTransformDirection(rootAccum); rootAccum = Vector3.zero;
                local += new Vector3(d.x, 0, d.z);
                local.y = Mathf.Max(0, local.y + d.y);
            }
            else
            {
                rootAccum = Vector3.zero;
                if (localVy != 0 || local.y > 0)
                {
                    localVy -= 20f * dt; local.y += localVy * dt;
                    if (local.y <= 0) { local.y = 0; localVy = 0; Game.I?.OnLand(this); }
                }
            }
            local = truck.ClampToRoof(local);
            Grounded = local.y <= 0.001f;
            transform.position = truck.RoofToWorld(local);
            hv = truck.transform.TransformDirection(cur);
            if (hv.sqrMagnitude > 0.05f && !tricking)
            {
                var q = Quaternion.LookRotation(new Vector3(hv.x, 0, hv.z));
                transform.rotation = Quaternion.Slerp(transform.rotation, q, 1 - Mathf.Exp(-dt * 12f));
            }
        }

        void TickClimb(float dt)
        {
            climbT += dt / 0.75f;
            float u = Mathf.Clamp01(climbT);
            float e = u * u * (3 - 2 * u);
            Vector3 to = climbingUp ? truck.RoofToWorld(climbToLocal) : truck.transform.TransformPoint(climbToLocal) + Vector3.up * 0.05f;
            if (!climbingUp) to.y = 0.2f;
            Vector3 p = Vector3.Lerp(climbFrom, to, e);
            p.y += Mathf.Sin(u * Mathf.PI) * 1.2f;
            transform.position = p;
            var dir = to - climbFrom; dir.y = 0;
            if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1 - Mathf.Exp(-dt * 10f));
            if (u >= 1)
            {
                climbT = -1; Trick = null;
                if (climbingUp) { OnTruck = true; local = climbToLocal; localVy = 0; vel = Vector3.zero; Game.I?.AddScore(50, "SUBIU!"); }
                else
                {
                    OnTruck = false; cc.enabled = true; vel = Vector3.zero; Grounded = false;
                    transform.position = to;
                }
            }
        }
    }
}
