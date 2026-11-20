using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CarretaJoy
{
    /// <summary>Animações de um menino (todas Humanoid: o Unity adapta ao corpo de cada um).</summary>
    public class KidClips
    {
        public AnimationClip idle, walk, run, dance, jump, mortal, flip, pirueta;
        // livres (Quaternius, CC0): parkour e acrobacias
        public AnimationClip air, roll, slideIn, slideLoop, slideOut, climb, ninjaIn, ninjaLoop, ninjaOut, crouch, dance2;
        // capturas da CMU (uso livre): acrobacias dos combos e danças extras
        public AnimationClip estrela, mortalLateral, reversao, chute, giro, breakdance, moonwalk;
        public AnimationClip[] dances;   // estilos de dança, na ordem do botão DANÇAR

        public KidClips Clone() => (KidClips)MemberwiseClone();

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
            Set(IDLE, c.idle); Set(WALK, c.walk); Set(RUN, c.run); Set(DANCE, c.dance); Set(AIR, c.air ?? c.jump);
            if (c.air == null && slots[AIR].IsValid() && c.jump != null) { slots[AIR].SetTime(c.jump.length * 0.42f); slots[AIR].SetSpeed(0); }
            w[IDLE] = 1;
            Apply();
            graph.Play();
        }

        void Set(int i, AnimationClip clip)
        {
            if (slots[i].IsValid()) { graph.Disconnect(mixer, i); slots[i].Destroy(); }
            if (clip == null) return;
            var p = AnimationClipPlayable.Create(graph, clip);
            // FootIK apenas nos estados no chão; em acrobacias e no ar FootIK entorta as pernas tentando alcançar o piso
            p.SetApplyFootIK(i != TRICK && i != AIR);
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
        public void SetDance(AnimationClip c) { if (c != null) { Set(DANCE, c); Apply(); } }
        public void SetIdle(AnimationClip c) { if (c != null) { Set(IDLE, c); Apply(); } }

        /// <summary>speed em m/s; height = altura do menino (passos proporcionais sem deslizar os pés)</summary>
        public void Update(float dt, float speed, float height, bool grounded, bool dancing, bool trick)
        {
            System.Array.Clear(t, 0, COUNT);
            if (trick) t[TRICK] = 1;
            else if (dancing) t[DANCE] = 1;
            else if (!grounded) t[AIR] = 1;
            else
            {
                float k = Mathf.Lerp(1f, height / 1.3f, 0.35f);
                float walkV = 1.65f * k, runV = 4.3f * k;
                if (speed < 0.12f) t[IDLE] = 1;
                else if (speed < walkV) { float a = Mathf.InverseLerp(0.12f, walkV, speed); t[IDLE] = 1 - a; t[WALK] = a; }
                else { float a = Mathf.Clamp01(Mathf.InverseLerp(walkV * 1.15f, runV, speed)); t[WALK] = 1 - a; t[RUN] = a; }
                if (slots[WALK].IsValid()) slots[WALK].SetSpeed(Mathf.Clamp(speed / walkV, 0.75f, 1.45f));
                if (slots[RUN].IsValid()) slots[RUN].SetSpeed(Mathf.Clamp(speed / runV, 0.80f, 1.35f));
            }
            float rate = trick ? 24f : 11f;
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

    /// <summary>Um personagem jogável na rua: anda, corre, pula, faz parkour/acrobacias e dança junto com a carreta.</summary>
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

        // carreta (referência para colisão na rua; personagens ficam sempre na rua)
        public Truck truck;
        float climbT = -1; Vector3 climbFrom;

        /// <summary>Altura do chão (definida pela cidade); null = chão plano em y = 0.</summary>
        public static System.Func<float, float, float> GroundHeight;
        float Ground(Vector3 p) => GroundHeight != null ? GroundHeight(p.x, p.z) : 0f;

        public float WalkSpeed => 1.85f * Mathf.Lerp(1f, Height / 1.3f, 0.35f);
        public float RunSpeed => 5.4f * Mathf.Lerp(1f, Height / 1.3f, 0.25f);
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
                // altura medida na vertical de verdade: o modelo vindo do Blender chega girado
                // (o eixo "para cima" do arquivo é o Z), então não dá para usar só o tamanho em Y
                var b = smr.sharedMesh.bounds;
                Vector3 upL = smr.transform.InverseTransformDirection(Vector3.up);
                Vector3 ls = smr.transform.lossyScale;
                h = Mathf.Abs(upL.x) * b.size.x * Mathf.Abs(ls.x) + Mathf.Abs(upL.y) * b.size.y * Mathf.Abs(ls.y) + Mathf.Abs(upL.z) * b.size.z * Mathf.Abs(ls.z);
                var anim0 = m.GetComponent<Animator>();
                if ((h < 0.3f || h > 6f) && anim0 != null && anim0.isHuman)
                {
                    // plano B: do pé até a cabeça pelo esqueleto
                    var head = anim0.GetBoneTransform(HumanBodyBones.Head); var foot = anim0.GetBoneTransform(HumanBodyBones.LeftFoot);
                    if (head && foot) h = (head.position.y - foot.position.y) * 1.15f;
                }
                h = Mathf.Max(0.1f, h);
                smr.updateWhenOffscreen = true;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                smr.quality = SkinQuality.Bone4;
            }
            m.transform.localScale *= height / h;
            foreach (var t in m.GetComponentsInChildren<Transform>()) t.gameObject.layer = 2;
            kid.model = m.transform;

            var cc = root.AddComponent<CharacterController>();
            cc.height = height;
            cc.radius = Mathf.Clamp(0.20f * height, 0.22f, 0.36f);
            cc.center = Vector3.up * (height * 0.5f);
            cc.stepOffset = Mathf.Min(0.32f, height * 0.28f);
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.03f;
            // compensa o skinWidth do CharacterController para a sola tocar exatamente o asfalto
            m.transform.localPosition = new Vector3(0f, -cc.skinWidth, 0f);
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
        float jumpBuf, airT;   // pulo apertado um pouco antes de tocar o chão / logo depois de sair dele
        public void Jump()
        {
            if (Busy || Trick != null) { jumpBuf = 0.18f; return; }
            bool canJump = Grounded || airT < 0.12f;
            if (Grounded && TryLedge()) { jumpBuf = 0; return; }
            if (canJump && vel.y <= 0.5f) { vel.y = 6.2f; Grounded = false; airT = 1f; jumpBuf = 0; Dancing = false; StandUp(); }
            else jumpBuf = 0.18f;
        }

        // ---------------- parkour: mureta / obstáculo baixo à frente ----------------
        Vector3 ledgeTo;
        bool TryLedge()
        {
            if (clips.climb == null) return false;
            int mask = ~((1 << 2) | (1 << 8));   // ignora personagens/carreta e o chão
            Vector3 f = transform.forward, p = transform.position;
            if (!Physics.Raycast(p + Vector3.up * 0.45f * Height, f, out var hit, 1.0f + cc.radius, mask, QueryTriggerInteraction.Ignore)) return false;
            Vector3 top0 = hit.point + f * 0.35f + Vector3.up * 2.3f;
            if (!Physics.Raycast(top0, Vector3.down, out var top, 2.3f, ~(1 << 2), QueryTriggerInteraction.Ignore)) return false;
            float dh = top.point.y - p.y;
            if (dh < 0.45f || dh > 2.1f) return false;
            // precisa caber em cima
            if (Physics.CheckCapsule(top.point + Vector3.up * (cc.radius + 0.05f), top.point + Vector3.up * (Height - cc.radius), cc.radius * 0.9f, ~(1 << 2), QueryTriggerInteraction.Ignore)) return false;
            ledgeTo = top.point + Vector3.up * 0.05f;
            climbT = 0; climbFrom = p;
            cc.enabled = false; Dancing = false; StandUp();
            anim.PlayTrick(clips.climb, 0.9f); Trick = "escalar"; trickLen = 0.75f; trickT = 0;
            Game.I?.AddScore(60, "PARKOUR!");
            return true;
        }

        // ---------------- acrobacias em etapas (rolar, deslizar, salto ninja) ----------------
        class Step { public AnimationClip clip; public float len = -1, speed = 1, push, jumpV; public bool untilLand, physics; }
        readonly System.Collections.Generic.List<Step> steps = new System.Collections.Generic.List<Step>();
        int stepI; float pushV;
        Step CurStep => stepI >= 0 && stepI < steps.Count ? steps[stepI] : null;

        void BeginSteps(string name, params Step[] s)
        {
            steps.Clear(); foreach (var x in s) if (x.clip != null) steps.Add(x);
            if (steps.Count == 0) return;
            Dancing = false; StandUp(); Trick = name; stepI = -1; NextStep();
        }
        void NextStep()
        {
            stepI++;
            var st = CurStep;
            if (st == null) { Trick = null; steps.Clear(); pushV = 0; return; }
            anim.PlayTrick(st.clip, st.speed);
            trickT = 0; trickLen = st.len > 0 ? st.len : anim.TrickLength;
            if (st.push > 0) pushV = st.push;
            if (st.jumpV > 0) { vel.y = st.jumpV; Grounded = false; }
        }

        bool crouching;
        /// <summary>Começa agachado (pose do Afonso) até andar.</summary>
        public void StartCrouched() { if (clips.crouch == null) return; crouching = true; anim.SetIdle(clips.crouch); }
        void StandUp() { if (crouching) { crouching = false; anim.SetIdle(clips.idle); } }
        int danceN;

        public void DoTrick(string name, bool score = true)
        {
            if (Busy || Trick != null) return;
            float sp = Mathf.Max(HorizontalSpeed, 0f);
            int pts;
            switch (name)
            {
                case "rolar":
                    if (!Grounded) return;
                    BeginSteps(name, new Step { clip = clips.roll, speed = 1.15f, physics = true, push = Mathf.Max(sp, 3.5f) * Height / 1.3f });
                    pts = 90; break;
                case "deslizar":
                    if (!Grounded) return;
                    BeginSteps(name, new Step { clip = clips.slideIn, physics = true, push = Mathf.Max(sp, 5.5f) * Height / 1.3f },
                                     new Step { clip = clips.slideLoop, len = 0.45f, physics = true },
                                     new Step { clip = clips.slideOut, physics = true });
                    pts = 110; break;
                case "ninja":
                    if (!Grounded) return;
                    BeginSteps(name, new Step { clip = clips.ninjaIn, len = clips.ninjaIn ? clips.ninjaIn.length * 0.5f : 0.3f, physics = true },
                                     new Step { clip = clips.ninjaLoop, physics = true, untilLand = true, jumpV = 8.5f, push = Mathf.Max(sp, 2.5f) },
                                     new Step { clip = clips.ninjaOut, len = clips.ninjaOut ? clips.ninjaOut.length * 0.6f : 0.4f, physics = true });
                    pts = 200; break;
                case "estrela": case "mortal_lateral": case "reversao": case "chute": case "giro": case "break": case "moonwalk":
                {
                    // acrobacias dos combos (capturas da CMU)
                    AnimationClip c = name == "estrela" ? clips.estrela : name == "mortal_lateral" ? clips.mortalLateral : name == "reversao" ? clips.reversao
                                    : name == "chute" ? clips.chute : name == "giro" ? clips.giro : name == "break" ? clips.breakdance : clips.moonwalk;
                    if (c == null || !Grounded) return;
                    float spd = name == "break" || name == "moonwalk" ? 1.0f : 1.15f;
                    BeginSteps(name, new Step { clip = c, speed = spd });
                    pts = name == "break" ? 250 : name == "moonwalk" ? 220 : name == "giro" ? 260 : 200; break;
                }
                default:
                {
                    AnimationClip c = name == "mortal" ? clips.mortal : name == "flip" ? clips.flip : clips.pirueta;
                    if (c == null) return;
                    BeginSteps(name, new Step { clip = c, speed = 1.1f });
                    pts = name == "mortal" ? 150 : name == "flip" ? 120 : 180; break;
                }
            }
            if (Trick == null) return;
            if (score) Game.I?.AddScore(pts, TrickTitle(name) + "!");
        }

        public static readonly string[] DanceNames = { "Gangnam", "Balanço", "Charleston", "Dança russa", "Salsa" };
        public int DanceStyle => danceN;
        AnimationClip DanceClip(int i) => clips.dances != null && clips.dances.Length > 0 ? clips.dances[Mathf.Clamp(i, 0, clips.dances.Length - 1)] : i == 1 && clips.dance2 ? clips.dance2 : clips.dance;
        int DanceCount => clips.dances != null && clips.dances.Length > 0 ? clips.dances.Length : 2;

        /// <summary>DANÇAR: começa a dançar; apertando de novo troca o estilo; depois do último, para.</summary>
        public static string TrickTitle(string n)
        {
            switch (n)
            {
                case "mortal_lateral": return "MORTAL LATERAL";
                case "reversao": return "REVERSÃO";
                case "chute": return "CHUTE VOADOR";
                case "giro": return "GIRO NO AR";
                case "break": return "BREAK";
                case "moonwalk": return "MOONWALK";
                default: return n.ToUpper();
            }
        }

        public void ToggleDance()
        {
            if (Busy || Trick != null) return;
            StandUp();
            if (!Dancing) SetDanceStyle(0);
            else if (danceN + 1 < DanceCount) SetDanceStyle(danceN + 1);
            else Dancing = false;
        }

        public void StopDance() { Dancing = false; }

        /// <summary>Dança um estilo direto (combos: DANÇAR + botão de manobra).</summary>
        public void SetDanceStyle(int i)
        {
            if (Busy || Trick != null) return;
            StandUp();
            var c = DanceClip(i);
            if (c == null) return;
            Dancing = true; danceN = Mathf.Clamp(i, 0, DanceCount - 1);
            anim.SetDance(c); anim.RestartDance();
        }

        public void OnRootMotion(Vector3 delta)
        {
            if (Trick != null) rootAccum += delta;
        }

        bool hidden;
        /// <summary>Volta ao estado normal (no chão, sem acrobacia, sem dança).</summary>
        public void ResetState()
        {
            climbT = -1; Trick = null; Dancing = false; vel = Vector3.zero; rootAccum = Vector3.zero;
            steps.Clear(); pushV = 0; airT = 0; jumpBuf = 0;
            if (!hidden) cc.enabled = true;
        }
        public void Teleport(Vector3 p, Quaternion rot)
        {
            cc.enabled = false; transform.SetPositionAndRotation(p, rot); vel = Vector3.zero;
            cc.enabled = !hidden;
        }
        /// <summary>Some (motorista dentro da cabine) ou reaparece.</summary>
        public void SetHidden(bool h)
        {
            hidden = h; model.gameObject.SetActive(!h); cc.enabled = !h;
            if (!h) { anim?.Destroy(); anim = new KidAnim(model.GetComponent<Animator>(), clips, Name); }
        }
        public void FollowCab(Vector3 p) { transform.position = p; }

        /// <summary>Separação suave na rua entre personagens para ninguém atravessar o outro.</summary>
        public void PushHorizontal(Vector3 delta)
        {
            if (hidden || Busy || !cc.enabled) return;
            delta.y = 0f;
            if (delta.sqrMagnitude > 1e-6f) cc.Move(delta);
        }

        // ---------------- movimento ----------------
        /// <summary>wish: direção no mundo (comprimento 0..1).</summary>
        public void Tick(Vector3 wish, float dt)
        {
            wish.y = 0;
            if (wish.sqrMagnitude > 1) wish.Normalize();
            if (wish.sqrMagnitude > 0.04f && Dancing && Trick == null) Dancing = false;
            if (wish.sqrMagnitude > 0.04f) StandUp();

            if (climbT >= 0) { TickClimb(dt); anim.Update(dt, 0, Height, true, false, true); return; }

            bool tricking = Trick != null;
            if (tricking)
            {
                trickT += dt;
                var st = CurStep;
                if (st != null && st.untilLand) { if (Grounded && trickT > 0.2f) NextStep(); }
                else if (trickT >= trickLen)
                {
                    bool rm = st != null && !st.physics;
                    if (st != null) NextStep(); else Trick = null;
                    if (Trick == null && rm) vel.y = 0;
                }
                tricking = Trick != null;
            }

            float target = Running ? RunSpeed : WalkSpeed;
            Vector3 hv;
            TickGround(wish, target, dt, tricking, out hv);

            // vira para onde está indo
            if (hv.sqrMagnitude > 0.05f && !tricking)
            {
                var q = Quaternion.LookRotation(new Vector3(hv.x, 0, hv.z));
                transform.rotation = Quaternion.Slerp(transform.rotation, q, 1 - Mathf.Exp(-dt * 17f));
            }
            HorizontalSpeed = new Vector3(hv.x, 0, hv.z).magnitude;
            // histerese curta evita piscar a pose de queda em pequenas descidas/meios-fios
            bool animGrounded = Grounded || (airT < 0.14f && vel.y <= 0.5f);
            anim.Update(dt, HorizontalSpeed, Height, animGrounded, Dancing, Trick != null);
        }

        void PushOutOfTruck()
        {
            if (truck == null || !cc.enabled) return;
            Vector3 push = truck.PushOut(transform.position, cc.radius);
            if (push.sqrMagnitude > 1e-6f) cc.Move(push);
        }

        void TickGround(Vector3 wish, float target, float dt, bool tricking, out Vector3 hv)
        {
            Vector3 h = new Vector3(vel.x, 0, vel.z);
            var cs = CurStep;
            if (tricking && cs != null && cs.physics)
            {
                // rolar / deslizar / salto ninja: o jogo empurra para frente e a gravidade age
                rootAccum = Vector3.zero;
                pushV = Mathf.MoveTowards(pushV, 0, (Grounded ? 4f : 0.5f) * dt);
                Vector3 hp = transform.forward * pushV;
                vel.x = hp.x; vel.z = hp.z;
                vel.y += -20f * dt;
                var fl = cc.Move(vel * dt);
                PushOutOfTruck();
                Grounded = (fl & CollisionFlags.Below) != 0;
                if (Grounded && vel.y < 0) vel.y = -4f;
                airT = Grounded ? 0f : airT + dt;
                hv = hp;
                return;
            }
            if (tricking)
            {
                // acrobacias com root motion: acompanha o terreno nas ladeiras (sem flutuar nem afundar)
                var d = Vector3.ClampMagnitude(rootAccum, 0.6f); rootAccum = Vector3.zero;
                d.y = 0f;
                h = Vector3.Lerp(h, Vector3.zero, 1 - Mathf.Exp(-dt * 6f));
                vel.y = Mathf.Max(vel.y - 18f * dt, -10f);
                var fl = cc.Move(d + new Vector3(h.x, vel.y, h.z) * dt);
                PushOutOfTruck();
                Grounded = (fl & CollisionFlags.Below) != 0;
                if (Grounded && vel.y < 0) vel.y = -4f;
                vel = new Vector3(h.x, vel.y, h.z);
                airT = 0f;
                hv = h;
                return;
            }
            rootAccum = Vector3.zero;
            // caiu para fora do mapa (buraco entre pedaços, colisor que ainda não carregou): volta para o chão
            float g = Ground(transform.position);
            if (transform.position.y < g - 3f)
            {
                cc.enabled = false; transform.position = new Vector3(transform.position.x, g + 0.5f, transform.position.z); cc.enabled = true;
                vel = Vector3.zero;
            }
            Vector3 want = wish * target;
            // resposta rápida: acelera e freia firme no chão, e ainda dá para corrigir no ar
            float acc = wish.sqrMagnitude > 0.01f ? (Grounded ? 48f : 12f) : (Grounded ? 34f : 3f);
            h = Vector3.MoveTowards(h, want, acc * dt);
            vel.x = h.x; vel.z = h.z;
            if (Grounded && vel.y <= 0f) vel.y = -5.5f;   // mantém grudado nas descidas de Ponta Grossa
            else vel.y = Mathf.Max(vel.y - 20f * dt, -24f);
            bool was = Grounded;
            var flags = cc.Move(vel * dt);
            Grounded = (flags & CollisionFlags.Below) != 0;
            // encaixe suave descendo ladeiras / degraus de calçada
            if (was && !Grounded && vel.y <= 0f)
            {
                if (Physics.Raycast(transform.position + Vector3.up * 0.15f, Vector3.down, out var hit, 0.5f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                {
                    cc.Move(Vector3.down * Mathf.Max(0f, hit.distance - 0.12f));
                    Grounded = true;
                }
            }
            PushOutOfTruck();
            if (Grounded && vel.y < 0) vel.y = -5.5f;
            if (!was && Grounded) Game.I?.OnLand(this);
            airT = Grounded ? 0f : airT + dt;
            if (jumpBuf > 0) { jumpBuf -= dt; if (Grounded && Trick == null && !Busy) Jump(); }
            hv = h;
        }

        void TickClimb(float dt)
        {
            climbT += dt / 0.75f;
            float u = Mathf.Clamp01(climbT);
            float e = u * u * (3 - 2 * u);
            Vector3 p = Vector3.Lerp(climbFrom, ledgeTo, e);
            p.y += Mathf.Sin(u * Mathf.PI) * 0.3f;
            transform.position = p;
            var dir = ledgeTo - climbFrom; dir.y = 0;
            if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1 - Mathf.Exp(-dt * 10f));
            if (u >= 1)
            {
                climbT = -1; Trick = null;
                cc.enabled = true; vel = Vector3.zero; transform.position = ledgeTo; Grounded = true; airT = 0f;
            }
        }
    }
}
