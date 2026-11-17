using UnityEngine;
using System.Collections.Generic;

namespace FamiliaCarretaJoy
{
    /// <summary>
    /// Main character controller, ported from the Actor class in actor.js.
    /// Handles movement, jumping, tricks, climbing, driving, dancing, and AI.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class CharacterController : MonoBehaviour
    {
        [Header("Definition")]
        public CharacterDefinition def;
        public CharacterBody body;
        public int costumeIndex = 0;

        [Header("State")]
        public CharacterFrame frame = CharacterFrame.World;
        public Vector3 pos;                    // Local position (world or truck)
        public Vector3 vel = Vector3.zero;
        public float yaw = 0f;
        public bool grounded = true;
        public float groundY = 0f;
        public CharacterMode mode = CharacterMode.Normal;
        public TrickState trick = null;
        public string queuedTrick = null;
        public List<string> airTricks = new List<string>();

        [Header("Physics")]
        public float stumble = 0f;
        public float invulnerability = 0f;
        public float airTime = 0f;
        public float runPhase = 0f;
        public float danceStyle = 0f;
        public bool controlled = false;
        public AIState ai = new AIState();

        [Header("Special")]
        public bool forceClimb = false;
        public Vector3 escortOffset;
        public float boost = 1f;
        public bool comboJoin = false;
        public CityStop partySpot = null;
        public DanceState dance = null;
        public float standTimer = 0f;
        public float lastPushTime = 0f;
        public PoseState currentPose = null;
        public float landTimer = 0f;
        public int seed;
        public float prevYaw = 0f;
        public float turnRate = 0f;
        public float dashTimer = 0f;
        public float dashCooldown = 0f;
        public string dashClip = "drible";
        public bool dashAir = false;
        public float slideTimer = 0f;
        public bool crouching = false;
        public bool doubleJumped = false;
        public bool diving = false;
        public WallHitInfo wallHit = null;
        public float wallTimer = 0f;
        public float cutTimer = 0f;
        public bool restartJumpClip = false;
        public bool wasGrounded = false;
        public bool stumbleOn = false;
        public float jumpBuffer = 0f;
        public float climbIntentTimer = 0f;

        // Components
        private Rigidbody rb;
        private CapsuleCollider col;
        private GameObject shadowObj;
        private GameObject fullModel; // For Afonso's initial pose

        public float speedMax => def.forte > 1.5f ? 6.2f : def.kid ? 6.6f : 7.2f;
        public float jumpVelocity => def.forte > 1.5f ? 10.5f : def.kid ? 9.4f : 9.8f;
        public float Height => def.altura;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<CapsuleCollider>();
            seed = Random.Range(0, 10000);

            // Setup rigidbody
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.mass = def.kid ? 1f : def.forte;
            rb.drag = 0f;
            rb.angularDrag = 0.05f;

            // Setup collider
            col.height = Height * 0.9f;
            col.radius = 0.3f;
            col.center = new Vector3(0, col.height * 0.5f, 0);

            // Create shadow
            CreateShadow();

            // Load full model for Afonso initial pose
            if (def.poseInicial && def.id == "afonso")
            {
                LoadFullModel();
            }
        }

        private void CreateShadow()
        {
            shadowObj = new GameObject("Shadow");
            shadowObj.transform.SetParent(transform);
            var shadowMesh = shadowObj.AddComponent<MeshFilter>();
            var shadowRenderer = shadowObj.AddComponent<MeshRenderer>();

            // Create a simple blob shadow mesh
            var mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, 0, -0.5f),
                new Vector3(0.5f, 0, -0.5f),
                new Vector3(0.5f, 0, 0.5f),
                new Vector3(-0.5f, 0, 0.5f)
            };
            mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            mesh.uv = new Vector2[]
            {
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1)
            };
            mesh.RecalculateNormals();
            shadowMesh.mesh = mesh;

            var shadowMat = new Material(Shader.Find("Transparent/Diffuse"));
            shadowMat.color = new Color(0, 0, 0, 0.5f);
            shadowMat.mainTexture = CreateShadowTexture();
            shadowRenderer.material = shadowMat;
            shadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shadowRenderer.receiveShadows = false;

            shadowObj.transform.localPosition = new Vector3(0, 0.07f, 0);
            shadowObj.transform.localRotation = Quaternion.Euler(90, 0, 0);
            shadowObj.transform.localScale = Vector3.one * 0.9f;
            shadowObj.layer = LayerMask.NameToLayer("CharacterLayer");
        }

        private Texture2D CreateShadowTexture()
        {
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var colors = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dx = (x - 32) / 32f;
                    float dy = (y - 32) / 32f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - dist) * 0.5f;
                    colors[y * 64 + x] = new Color(0, 0, 0, alpha);
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        private void LoadFullModel()
        {
            // This would load the Afonso full model for the initial crouched pose
            // For now, we'll handle this through the body system
        }

        public void Initialize(CharacterDefinition definition, CharacterBody characterBody)
        {
            def = definition;
            body = characterBody;
            body.transform.SetParent(transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;

            if (body.rig != null)
            {
                body.rig.transform.SetParent(transform);
                body.rig.transform.localPosition = new Vector3(0, body.hipHeight, 0);
            }

            Reset();
        }

        public void Reset()
        {
            frame = CharacterFrame.World;
            pos = Vector3.zero;
            vel = Vector3.zero;
            yaw = 0f;
            grounded = true;
            groundY = 0f;
            mode = CharacterMode.Normal;
            trick = null;
            queuedTrick = null;
            airTricks.Clear();
            stumble = 0f;
            invulnerability = 0f;
            airTime = 0f;
            runPhase = Random.value * 6f;
            danceStyle = Mathf.Floor(seed) % 4;
            controlled = false;
            ai = new AIState();
            forceClimb = false;
            escortOffset = Vector3.zero;
            boost = 1f;
            comboJoin = false;
            partySpot = null;
            dance = null;
            standTimer = 0f;
            lastPushTime = 0f;
            currentPose = null;
            landTimer = 0f;
            prevYaw = 0f;
            turnRate = 0f;
            dashTimer = 0f;
            dashCooldown = 0f;
            dashClip = "drible";
            dashAir = false;
            slideTimer = 0f;
            crouching = false;
            doubleJumped = false;
            diving = false;
            wallHit = null;
            wallTimer = 0f;
            cutTimer = 0f;
            restartJumpClip = false;
            wasGrounded = false;
            stumbleOn = false;
            jumpBuffer = 0f;
            climbIntentTimer = 0f;

            if (fullModel != null)
                fullModel.SetActive(false);

            body.rig.SetActive(true);
            gameObject.SetActive(true);
        }

        public Vector3 WorldPosition(GameState gameState)
        {
            if (frame == CharacterFrame.Truck && gameState.truck != null)
                return gameState.truck.LocalToWorld(pos);
            return pos;
        }

        // ==================== COMMANDS ====================

        public string Jump(float boost = 1f, GameState gameState = null)
        {
            if (mode != CharacterMode.Normal || stumble > 0f) return null;

            // Wall jump
            if (!grounded && wallHit != null && gameState != null && Time.time - wallTimer < 0.18f)
            {
                var w = wallHit;
                wallHit = null;
                vel.y = jumpVelocity * 1.04f * boost;
                vel.x = w.normal.x * 7.8f;
                vel.z = w.normal.z * 7.8f;
                yaw = Mathf.Atan2(w.normal.x, w.normal.z);
                doubleJumped = false;
                airTime = 0f;
                crouching = false;
                restartJumpClip = true;
                EventManager.TriggerEvent("walljump", this);
                return "wall";
            }

            // Coyote time
            bool coyote = !grounded && airTime < 0.14f && vel.y <= 0f && airTricks.Count == 0 && trick == null;

            if (grounded || coyote)
            {
                if (slideTimer > 0f) { boost = Mathf.Max(boost, 1.16f); slideTimer = 0f; }
                crouching = false;
                doubleJumped = false;
                diving = false;
                vel.y = jumpVelocity * boost;
                grounded = false;
                airTricks.Clear();
                airTime = 0f;
                return "jump";
            }

            // Double jump
            if (!doubleJumped && trick == null && airTime > 0.08f)
            {
                doubleJumped = true;
                diving = false;
                crouching = false;
                vel.y = Mathf.Max(vel.y * 0.25f, 0f) + jumpVelocity * 0.9f;
                airTime = 0f;
                restartJumpClip = true;
                return "double";
            }

            return null;
        }

        public string StartDash(GameState gameState, float wx = 0f, float wz = 0f)
        {
            if (mode != CharacterMode.Normal || stumble > 0f || dashCooldown > 0f || (!grounded && dashAir)) return null;

            float dx = wx, dz = wz;

            if (frame == CharacterFrame.Truck && gameState != null && (dx != 0f || dz != 0f))
            {
                float a = -gameState.truck.yaw;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                float lx = dx * c + dz * s;
                float lz = -dx * s + dz * c;
                dx = lx; dz = lz;
            }

            float len = Mathf.Sqrt(dx * dx + dz * dz);
            if (len > 0.1f) { dx /= len; dz /= len; }
            else { dx = Mathf.Sin(yaw); dz = Mathf.Cos(yaw); }

            float fx = Mathf.Sin(yaw), fz = Mathf.Cos(yaw);
            float rx = -Mathf.Cos(yaw), rz = Mathf.Sin(yaw);
            float fwdDot = dx * fx + dz * fz;
            float rightDot = dx * rx + dz * rz;

            string clip = "drible";
            if (rightDot > 0.55f) clip = "esquivaD";
            else if (rightDot < -0.55f) clip = "esquivaE";
            else if (fwdDot < -0.45f) clip = "correrT";
            else if (len > 0.1f) yaw = Mathf.Atan2(dx, dz);

            float spd = grounded ? (frame == CharacterFrame.Truck ? 7.5f : 13.5f) : 10.5f;
            vel.x = dx * spd;
            vel.z = dz * spd;
            if (!grounded) { vel.y = Mathf.Max(vel.y, 3.2f); dashAir = true; }

            dashTimer = 0.34f;
            dashCooldown = 0.55f;
            dashClip = clip;
            slideTimer = 0f;
            crouching = false;
            invulnerability = Mathf.Max(invulnerability, 0.35f);
            return clip;
        }

        public SlideResult SlideOrDance(GameState gameState)
        {
            if (mode != CharacterMode.Normal || stumble > 0f) return null;

            float hs = Mathf.Sqrt(vel.x * vel.x + vel.z * vel.z);

            if ((frame == CharacterFrame.Truck && hs < 1.2f) || (gameState != null && gameState.dancing))
            {
                danceStyle = (danceStyle + 1) % GameConstants.DanceClips.Length;
                return new SlideResult { type = "danceStyle", index = (int)danceStyle };
            }

            if (!grounded)
            {
                if (vel.y > -12f) { vel.y = Mathf.Min(vel.y, -11.5f); diving = true; return new SlideResult { type = "dive" }; }
                return null;
            }

            if (hs > 1.8f && slideTimer <= 0f)
            {
                float s = Mathf.Max(hs * 1.28f, 11.2f);
                float a = hs > 0.3f ? Mathf.Atan2(vel.x, vel.z) : yaw;
                vel.x = Mathf.Sin(a) * s;
                vel.z = Mathf.Cos(a) * s;
                yaw = a;
                slideTimer = 0.52f;
                crouching = false;
                return new SlideResult { type = "slide" };
            }

            crouching = !crouching;
            return new SlideResult { type = "crouch", active = crouching };
        }

        public bool StartTrick(string type, float wx = 0f, float wz = 0f, GameState gameState = null)
        {
            if (mode != CharacterMode.Normal || stumble > 0f) return false;
            if (trick != null)
            {
                if (trick.timer / trick.duration > 0.45f) { queuedTrick = type; return true; }
                return false;
            }

            if (grounded)
            {
                if (type == "estrela")
                {
                    float hs = Mathf.Sqrt(vel.x * vel.x + vel.z * vel.z);
                    float dx = wx, dz = wz;
                    if (frame == CharacterFrame.Truck && gameState != null && (dx != 0f || dz != 0f))
                    {
                        float a = -gameState.truck.yaw;
                        float c = Mathf.Cos(a), s = Mathf.Sin(a);
                        float lx = dx * c + dz * s;
                        float lz = -dx * s + dz * c;
                        dx = lx; dz = lz;
                    }

                    int dir = Random.value < 0.5f ? 1 : -1;
                    float moveAngle = yaw + (Mathf.PI / 2f) * dir;
                    if (Mathf.Sqrt(dx * dx + dz * dz) > 0.1f)
                    {
                        moveAngle = Mathf.Atan2(dx, dz);
                        float diffP = Mathf.Abs(Mathf.DeltaAngle(moveAngle, yaw + Mathf.PI / 2f));
                        float diffN = Mathf.Abs(Mathf.DeltaAngle(moveAngle, yaw - Mathf.PI / 2f));
                        dir = diffP <= diffN ? 1 : -1;
                        yaw = moveAngle - (Mathf.PI / 2f) * dir;
                    }

                    trick = new TrickState
                    {
                        type = type,
                        timer = 0f,
                        duration = GameConstants.TrickDefinition.GetById(type).dur,
                        grounded = true,
                        dir = dir,
                        moveAngle = moveAngle,
                        baseSpeed = Mathf.Max(4.6f, hs * 0.95f)
                    };
                    return true;
                }
                Jump();
            }

            if (airTricks.Count == 0 && vel.y > -2.5f)
            {
                var trickDef = GameConstants.TrickDefinition.GetById(type);
                vel.y += type == "flip" ? 2.2f : type == "mortal" ? 1.7f : 1.1f;
            }

            if ((type == "mortal" || type == "flip") && Mathf.Sqrt(vel.x * vel.x + vel.z * vel.z) > 0.8f)
            {
                vel.x += Mathf.Sin(yaw) * 1.1f;
                vel.z += Mathf.Cos(yaw) * 1.1f;
            }

            trick = new TrickState
            {
                type = type,
                timer = 0f,
                duration = GameConstants.TrickDefinition.GetById(type).dur,
                grounded = false,
                dir = 1
            };
            return true;
        }

        public void DoDanceMove(string type)
        {
            dance = new DanceState { type = type, timer = 0f, duration = type == "mortal" ? 0.7f : 0.5f };
        }

        // Afonso initial pose
        public void StartInitialPose()
        {
            if (fullModel == null) return;
            mode = CharacterMode.Pose;
            fullModel.SetActive(true);
            body.rig.SetActive(false);
        }

        public void StandUp(GameState gameState)
        {
            if (mode != CharacterMode.Pose) return;
            fullModel.SetActive(false);
            body.rig.SetActive(true);
            mode = CharacterMode.Stand;
            standTimer = 0f;
            EventManager.TriggerEvent("levantou", this);
        }

        // Climbing truck
        public bool CanClimbTruck(GameState gameState)
        {
            if (frame != CharacterFrame.World || mode != CharacterMode.Normal || !grounded) return false;
            var local = gameState.truck.WorldToLocal(pos);
            return Mathf.Abs(local.x) < gameState.truck.halfWidth + 2.2f &&
                   local.z > gameState.truck.roofZMin - 1f &&
                   local.z < gameState.truck.roofZMax + 1f &&
                   local.y < 2f;
        }

        public bool BeginClimbTruck(GameState gameState, Vector3? targetSpot = null)
        {
            if (!CanClimbTruck(gameState) && !forceClimb) return false;
            forceClimb = false;

            var T = gameState.truck;
            var local = T.WorldToLocal(pos);
            int side = local.x >= 0 ? 1 : -1;
            float z = Mathf.Clamp(local.z, T.roofZMin + 0.6f, T.roofZMax - 0.6f);

            climbFrom = local;
            climbSide = new Vector3(side * (T.halfWidth + 0.35f), Mathf.Max(0f, local.y), z);
            climbTop = new Vector3(side * (T.halfWidth + 0.35f), T.roofY, z);
            climbTo = targetSpot.HasValue ? targetSpot.Value :
                new Vector3(side * (T.halfWidth - 0.5f), T.roofY, z);
            climbSideSign = side;

            frame = CharacterFrame.Truck;
            this.pos = local;
            mode = CharacterMode.TruckClimb;
            climbTimer = 0f;
            trick = null;
            vel = Vector3.zero;
            return true;
        }

        public bool BeginDescend(GameState gameState)
        {
            if (frame != CharacterFrame.Truck || mode != CharacterMode.Normal) return false;
            var T = gameState.truck;
            int side = pos.x >= 0 ? 1 : -1;
            descendFrom = pos;
            descendTo = new Vector3(side * (T.halfWidth + 2.2f), 0f, pos.z + 1.5f);
            descendSide = side;
            mode = CharacterMode.Descend;
            descendTimer = 0f;
            trick = null;
            return true;
        }

        public void EnterDrive(GameState gameState)
        {
            mode = CharacterMode.Drive;
            gameObject.SetActive(false);
            shadowObj.SetActive(false);
            trick = null;
        }

        public void ExitDrive(GameState gameState)
        {
            var T = gameState.truck;
            var worldPos = T.LocalToWorld(new Vector3(T.halfWidth + 1.3f, 0f, GameConstants.TruckLength / 2f - 3f));
            frame = CharacterFrame.World;
            pos = worldPos;
            pos.y = gameState.city.GroundAt(pos.x, pos.z, pos.y + 2f).height;
            vel = Vector3.zero;
            mode = CharacterMode.Normal;
            gameObject.SetActive(true);
            grounded = true;
            yaw = T.yaw;
        }

        // Animation state variables for climbing
        private Vector3 climbFrom, climbSide, climbTop, climbTo;
        private int climbSideSign;
        private float climbTimer = 0f;

        private Vector3 descendFrom, descendTo;
        private int descendSide;
        private float descendTimer = 0f;

        private Vector3 ledgeFrom, ledgeTo;
        private float ledgeDuration = 0f;
        private float ledgeTimer = 0f;

        // ==================== UPDATE ====================

        public void UpdateCharacter(float dt, GameState gameState, InputData input)
        {
            invulnerability = Mathf.Max(0f, invulnerability - dt);
            if (jumpBuffer > 0f) jumpBuffer -= dt;
            stumble = Mathf.Max(0f, stumble - dt);
            dashTimer = Mathf.Max(0f, dashTimer - dt);
            dashCooldown = Mathf.Max(0f, dashCooldown - dt);
            slideTimer = Mathf.Max(0f, slideTimer - dt);
            cutTimer = Mathf.Max(0f, cutTimer - dt);

            if (mode == CharacterMode.Drive)
            {
                if (gameState.truck != null)
                    pos = gameState.truck.LocalToWorld(new Vector3(0f, 2f, GameConstants.TruckLength / 2f - 1.5f));
                return;
            }

            if (mode == CharacterMode.Pose)
            {
                if (input != null && input.want > 0.2f) StandUp(gameState);
                PlaceInWorld(gameState);
                Animate(dt, gameState, 0f);
                return;
            }

            if (mode == CharacterMode.TruckClimb) { UpdateClimbTruck(dt, gameState); return; }
            if (mode == CharacterMode.Descend) { UpdateDescend(dt, gameState); return; }
            if (mode == CharacterMode.Ledge) { UpdateLedge(dt, gameState); return; }
            if (mode == CharacterMode.Stand)
            {
                standTimer += dt;
                if (standTimer > 0.75f) mode = CharacterMode.Normal;
            }

            // Process input
            float mx = input?.mx ?? 0f;
            float mz = input?.mz ?? 0f;
            float want = input?.want ?? 0f;

            if (mode == CharacterMode.Stand || stumble > 0.4f || (gameState != null && gameState.freeze)) want = 0f;

            // Convert world input to truck-local if on truck
            if (frame == CharacterFrame.Truck && gameState.truck != null)
            {
                float a = -gameState.truck.yaw;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                float lx = mx * c + mz * s;
                float lz = -mx * s + mz * c;
                mx = lx; mz = lz;
            }

            float sp = speedMax * boost * want * (frame == CharacterFrame.Truck ? 0.55f : 1f) * (crouching ? 0.55f : 1f);
            float acc = grounded ? 56f : 20f;
            float hs0 = Mathf.Sqrt(vel.x * vel.x + vel.z * vel.z);

            // Sharp turn detection
            if (grounded && dashTimer <= 0f && slideTimer <= 0f && want > 0.4f && hs0 > 3.6f && cutTimer <= 0f)
            {
                float dot = (vel.x * mx + vel.z * mz) / hs0;
                if (dot < -0.55f) cutTimer = 0.36f;
            }

            if (dashTimer > 0f)
            {
                float drag = Mathf.Exp(-dt * 2.5f);
                vel.x *= drag; vel.z *= drag;
            }
            else if (slideTimer > 0f)
            {
                float drag = Mathf.Exp(-dt * 2.2f);
                vel.x *= drag; vel.z *= drag;
            }
            else
            {
                float tvx = mx * sp, tvz = mz * sp;
                vel.x += Mathf.Clamp(tvx - vel.x, -acc * dt, acc * dt);
                vel.z += Mathf.Clamp(tvz - vel.z, -acc * dt, acc * dt);
                if (want > 0.1f)
                {
                    float targetYaw = Mathf.Atan2(mx, mz);
                    float dyaw = Mathf.DeltaAngle(yaw, targetYaw);
                    yaw += dyaw * Mathf.Min(1f, dt * 22f);
                }
            }

            // Ground star (estrela) movement
            if (trick != null && trick.grounded)
            {
                var tr = trick;
                float k = Mathf.Clamp01(tr.timer / tr.duration);
                float bell = 0.45f + 0.85f * Mathf.Sin(k * Mathf.PI);
                float spd = (tr.baseSpeed ?? 4.8f) * bell;
                int dir = tr.dir ?? 1;

                if (want > 0.2f)
                {
                    float wantA = Mathf.Atan2(mx, mz);
                    float curA = tr.moveAngle ?? (yaw + Mathf.PI / 2f * dir);
                    float da = Mathf.DeltaAngle(curA, wantA);
                    tr.moveAngle = curA + da * Mathf.Min(1f, dt * 8f);
                    yaw = tr.moveAngle.Value - (Mathf.PI / 2f) * dir;
                }

                float moveA = tr.moveAngle ?? (yaw + Mathf.PI / 2f * dir);
                vel.x = Mathf.Sin(moveA) * spd;
                vel.z = Mathf.Cos(moveA) * spd;
            }

            // Apply velocity
            pos.x += vel.x * dt;
            pos.z += vel.z * dt;

            if (!grounded)
            {
                float gScale = (trick != null && Mathf.Abs(vel.y) < 4f) ? 0.72f :
                               (Mathf.Abs(vel.y) < 2.2f ? 0.85f : 1f);
                vel.y -= GameConstants.Gravity * gScale * dt;
                airTime += dt;
            }

            pos.y += vel.y * dt;

            // Physics
            if (frame == CharacterFrame.World)
                PhysicsWorld(dt, gameState);
            else
                PhysicsTruck(dt, gameState);

            UpdateTricks(dt, gameState);
            PlaceInWorld(gameState);

            float hs = Mathf.Sqrt(vel.x * vel.x + vel.z * vel.z);
            Animate(dt, gameState, hs);

            // Collectibles
            if (gameState?.city != null)
            {
                Vector3 wp = WorldPosition(gameState);
                gameState.city.Collect(wp, wp.y + Height * 0.5f, (note) =>
                    EventManager.TriggerEvent(note.gold ? "estrelaDourada" : "nota", this, note));
            }
        }

        private void PhysicsWorld(float dt, GameState gameState)
        {
            var city = gameState.city;
            var truck = gameState.truck;

            // Building/collider collisions
            var hit = city.Collide(pos, 0.32f, pos.y);
            if (hit != null && hit.collider.walk && !grounded && mode == CharacterMode.Normal)
            {
                wallHit = hit;
                wallTimer = Time.time;
            }

            if (hit != null && hit.collider.walk && mode == CharacterMode.Normal)
            {
                float ledgeHeight = city.TopAt(hit.collider, pos.x - hit.normal.x * 0.6f, pos.z - hit.normal.z * 0.6f) - pos.y;
                bool pushing = (vel.x * -hit.normal.x + vel.z * -hit.normal.z) > 0.5f ||
                               Time.time - lastPushTime < 0.15f;
                lastPushTime = Time.time;

                if (pushing && ledgeHeight > 0f &&
                    (ledgeHeight < 1.15f || (!grounded && ledgeHeight < 2.6f) || (grounded && ledgeHeight < 2.6f && climbIntentTimer > 0f)))
                {
                    mode = CharacterMode.Ledge;
                    ledgeTimer = 0f;
                    ledgeFrom = pos;
                    ledgeTo = new Vector3(pos.x - hit.normal.x * 0.7f, pos.y + ledgeHeight, pos.z - hit.normal.z * 0.7f);
                    ledgeDuration = 0.2f + ledgeHeight * 0.15f;
                    trick = null;
                    vel = Vector3.zero;
                    EventManager.TriggerEvent("escalou", this, ledgeHeight);
                    return;
                }
            }

            // Truck collision
            var local = truck.WorldToLocal(pos);
            bool inX = Mathf.Abs(local.x) < truck.halfWidth + 0.3f;
            bool inZ = local.z > -GameConstants.TruckLength / 2f && local.z < GameConstants.TruckLength / 2f;

            if (inX && inZ)
            {
                bool onRoofZ = local.z > truck.roofZMin && local.z < truck.roofZMax;
                if (onRoofZ && local.y >= truck.roofY - 0.7f && vel.y <= 0.5f)
                {
                    // Landed on roof
                    frame = CharacterFrame.Truck;
                    pos = local;
                    pos.y = truck.roofY;
                    grounded = true;
                    vel = Vector3.zero;
                    Land(gameState, null);
                    return;
                }
                else if (local.y < truck.roofY - 0.7f)
                {
                    // Push out from side
                    float pushX = (truck.halfWidth + 0.31f) * Mathf.Sign(local.x == 0 ? 1 : local.x) - local.x;
                    float pushZ = local.z > 0 ? GameConstants.TruckLength / 2f - local.z : -GameConstants.TruckLength / 2f - local.z;
                    if (Mathf.Abs(pushX) < Mathf.Abs(pushZ)) local.x += pushX; else local.z += pushZ;
                    truck.LocalToWorld(local, ref pos);
                }
            }

            // Ground
            var ground = city.GroundAt(pos.x, pos.z, pos.y);
            groundY = ground.height;

            if (grounded)
            {
                if (pos.y > ground.height + 0.35f)
                {
                    grounded = false;
                    vel.y = 0f;
                    airTricks.Clear();
                    airTime = 0f;
                }
                else
                {
                    pos.y = ground.height;
                    vel.y = 0f;
                }

                // Ramp launch
                if (ground.top != null && ground.top.ramp) onRamp = ground.top;
                else if (onRamp != null)
                {
                    var r = onRamp;
                    onRamp = null;
                    if (Mathf.Sqrt(vel.x * vel.x + vel.z * vel.z) > 2f)
                    {
                        vel.y = 12.5f;
                        grounded = false;
                        airTricks.Clear();
                        airTime = 0f;
                        EventManager.TriggerEvent("rampa", this);
                    }
                }
            }
            else if (pos.y <= ground.height && vel.y <= 0f)
            {
                pos.y = ground.height;
                grounded = true;
                vel.y = 0f;
                Land(gameState, ground.top);
            }

            if (pos.y < ground.height - 3f) pos.y = ground.height;
        }

        private RampInfo onRamp = null;

        private void PhysicsTruck(float dt, GameState gameState)
        {
            var truck = gameState.truck;
            bool inside = Mathf.Abs(pos.x) < truck.halfWidth - 0.05f &&
                          pos.z > truck.roofZMin && pos.z < truck.roofZMax;

            if (inside)
            {
                if (grounded) { pos.y = truck.roofY; vel.y = 0f; }
                else if (pos.y <= truck.roofY && vel.y <= 0f)
                {
                    pos.y = truck.roofY;
                    grounded = true;
                    vel.y = 0f;
                    Land(gameState, null);
                }
            }
            else
            {
                // Fell off truck
                var worldPos = truck.LocalToWorld(pos);
                float a = truck.yaw, c = Mathf.Cos(a), s = Mathf.Sin(a);
                float vx = vel.x * c + vel.z * s;
                float vz = -vel.x * s + vel.z * c;

                frame = CharacterFrame.World;
                pos = worldPos;
                vel.x = vx + truck.velocity.x;
                vel.z = vz + truck.velocity.z;
                yaw += truck.yaw;

                if (grounded) { grounded = false; vel.y = 1.5f; airTricks.Clear(); airTime = 0f; }
            }
        }

        private void UpdateTricks(float dt, GameState gameState)
        {
            if (trick == null) return;

            trick.timer += dt;
            if (trick.timer >= trick.duration)
            {
                if (trick.grounded)
                    EventManager.TriggerEvent("manobra", this, trick.type);
                else
                    airTricks.Add(trick.type);

                trick = null;
                if (queuedTrick != null && !grounded)
                {
                    var q = queuedTrick;
                    trick = new TrickState { type = q, timer = 0f, duration = GameConstants.TrickDefinition.GetById(q).dur, grounded = false };
                }
                queuedTrick = null;
            }
        }

        private void Land(GameState gameState, ColliderInfo top)
        {
            doubleJumped = false;
            dashAir = false;
            diving = false;

            if (trick != null && !trick.grounded)
            {
                if (trick.timer / trick.duration < 0.62f)
                {
                    trick = null; queuedTrick = null; Crash(gameState, true); return;
                }
                airTricks.Add(trick.type);
                trick = null;
            }

            var list = airTricks;
            airTricks.Clear();

            if (airTime > 0.25f) landTimer = Mathf.Min(0.3f, 0.12f + airTime * 0.12f);

            if (jumpBuffer > 0f) { jumpBuffer = 0f; grounded = true; /* schedule jump */ }

            EventManager.TriggerEvent("pousou", this, list, top, airTime);
        }

        private void Crash(GameState gameState, bool fromTrick)
        {
            if (invulnerability > 0f) return;
            stumble = 1f;
            invulnerability = 1.5f;
            trick = null; queuedTrick = null;
            vel.x *= 0.2f; vel.z *= 0.2f;
            EventManager.TriggerEvent("tombo", this, fromTrick);
        }

        private void UpdateLedge(float dt, GameState gameState)
        {
            ledgeTimer += dt;
            float k = Mathf.Clamp01(ledgeTimer / ledgeDuration);
            pos = Vector3.Lerp(ledgeFrom, ledgeTo, k);
            pos.y = ledgeFrom.y + (ledgeTo.y - ledgeFrom.y) * Mathf.Min(1f, k * 1.4f) + Mathf.Sin(k * Mathf.PI) * 0.3f;

            if (k >= 1f)
            {
                mode = CharacterMode.Normal;
                grounded = true;
                vel = Vector3.zero;
                pos = ledgeTo;
            }

            PlaceInWorld(gameState);
            currentPose = PoseDefinitions.Climb(ledgeTimer);
            body.ApplyPose(currentPose);
            body.pivot.localRotation = Quaternion.identity;
        }

        private void UpdateClimbTruck(float dt, GameState gameState)
        {
            climbTimer += dt;
            float t = climbTimer;
            float T1 = 0.25f, T2 = 1f, T3 = 1.3f;
            var p = pos;
            float face = climbSideSign > 0 ? -Mathf.PI / 2f : Mathf.PI / 2f;

            if (t < T1) p = Vector3.Lerp(climbFrom, climbSide, Ease(t / T1));
            else if (t < T2) p = Vector3.Lerp(climbSide, climbTop, (t - T1) / (T2 - T1));
            else if (t < T3)
            {
                float k = (t - T2) / (T3 - T2);
                p = Vector3.Lerp(climbTop, climbTo, Ease(k));
                p.y += Mathf.Sin(k * Mathf.PI) * 0.5f;
            }
            else
            {
                mode = CharacterMode.Normal;
                p = climbTo;
                grounded = true;
                vel = Vector3.zero;
                EventManager.TriggerEvent("subiu", this);
            }

            yaw = face;
            pos = p;
            PlaceInWorld(gameState);

            currentPose = t > T1 && t < T2 ? PoseDefinitions.Climb(t) : PoseDefinitions.Idle(t, seed);
            body.ApplyPose(currentPose);
            body.pivot.localRotation = Quaternion.identity;
        }

        private void UpdateDescend(float dt, GameState gameState)
        {
            descendTimer += dt;
            float D = 0.9f, k = Mathf.Clamp01(descendTimer / D);
            var p = pos;
            p = Vector3.Lerp(descendFrom, descendTo, k);
            p.y = descendFrom.y * (1f - k * k) + Mathf.Sin(k * Mathf.PI) * 1.6f;
            yaw = descendSide > 0 ? Mathf.PI / 2f : -Mathf.PI / 2f;
            pos = p;
            PlaceInWorld(gameState);

            if (body.hasDriver)
            {
                body.PlayClip("mortal", body.GetClipDuration("mortal") / D, descendTimer <= dt);
                body.pivot.localRotation = Quaternion.identity;
            }
            else
            {
                currentPose = PoseDefinitions.Air(1f, 1f);
                body.ApplyPose(currentPose);
                body.pivot.localRotation = Quaternion.Euler(-Ease(k) * Mathf.PI * 2f, 0f, 0f);
            }

            if (k >= 1f)
            {
                var worldPos = gameState.truck.LocalToWorld(descendTo);
                frame = CharacterFrame.World;
                pos = worldPos;
                pos.y = gameState.city.GroundAt(pos.x, pos.z, pos.y + 1.5f).height;
                vel = new Vector3(gameState.truck.velocity.x, 0f, gameState.truck.velocity.z);
                mode = CharacterMode.Normal;
                grounded = true;
                body.pivot.localRotation = Quaternion.identity;
                EventManager.TriggerEvent("desceu", this);
            }
        }

        private void PlaceInWorld(GameState gameState)
        {
            if (frame == CharacterFrame.Truck && gameState.truck != null)
            {
                gameState.truck.LocalToWorld(pos, ref transform.position);
                transform.rotation = gameState.truck.transform.rotation * Quaternion.Euler(0f, yaw, 0f);
                shadowObj.SetActive(false);
            }
            else
            {
                transform.position = pos;
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                shadowObj.SetActive(mode != CharacterMode.Drive);
                shadowObj.transform.position = new Vector3(pos.x, groundY + 0.07f, pos.z);
                float scale = Mathf.Max(0.35f, (def.forte ?? 1f) * 0.9f - (pos.y - groundY) * 0.1f);
                shadowObj.transform.localScale = Vector3.one * scale;
            }
        }

        private void Animate(float dt, GameState gameState, float hs)
        {
            var B = body;
            float rx = 0f, ry = 0f, rz = 0f;

            if (mode == CharacterMode.Pose)
            {
                currentPose = PoseDefinitions.CrouchStart();
                B.ApplyPose(currentPose);
                B.pivot.localRotation = Quaternion.identity;
                return;
            }

            if (mode == CharacterMode.Stand)
            {
                currentPose = PoseDefinitions.Lerp(PoseDefinitions.CrouchStart(), PoseDefinitions.Idle(0f), Ease(Mathf.Clamp01(standTimer / 0.7f)));
                B.ApplyPose(currentPose);
                B.pivot.localRotation = Quaternion.identity;
                return;
            }

            bool idle = false, danceContext = false;
            var tr = trick;

            B.transform.localPosition = Vector3.zero;

            if (tr != null)
            {
                float k = Mathf.Clamp01(tr.timer / tr.duration);
                float e = Ease(k);

                if (tr.type == "mortal") { rx = -e * Mathf.PI * 2f; currentPose = PoseDefinitions.Air(vel.y, Mathf.Sin(k * Mathf.PI)); }
                else if (tr.type == "flip") { rx = e * Mathf.PI * 2f; ry = e * Mathf.PI * 2f; currentPose = PoseDefinitions.Air(vel.y, Mathf.Sin(k * Mathf.PI)); }
                else if (tr.type == "pirueta") { ry = e * Mathf.PI * 2f; currentPose = PoseDefinitions.Air(vel.y, 0.35f); }
                else
                {
                    int dir = tr.dir ?? 1;
                    rz = -e * Mathf.PI * 2f * dir;
                    ry = Mathf.Sin(k * Mathf.PI) * 0.16f * dir;
                    currentPose = PoseDefinitions.Star(k);
                    if (tr.grounded) B.transform.localPosition = new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.065f * Height, 0f);
                }
            }
            else if (stumble > 0f)
            {
                float k = 1f - stumble;
                rx = Mathf.Sin(k * Mathf.PI) * 1.1f;
                currentPose = PoseDefinitions.Air(-1f, 0.5f);
            }
            else if (slideTimer > 0f)
            {
                currentPose = PoseDefinitions.Slide();
            }
            else if (!grounded)
            {
                currentPose = PoseDefinitions.Air(vel.y, 0f);
            }
            else if (hs > 0.5f)
            {
                float L = Height / 1.75f;
                if (hs < 3.2f) { runPhase += dt * hs * 3.2f / L; currentPose = PoseDefinitions.Walk(runPhase, Mathf.Min(1f, hs / 2.2f)); }
                else { runPhase += dt * (5f + hs * 1.5f) / Mathf.Sqrt(L); currentPose = PoseDefinitions.Run(runPhase, Mathf.Min(1f, hs / 6f), Height); }
            }
            else if (crouching)
            {
                currentPose = PoseDefinitions.Crouch();
            }
            else if (frame == CharacterFrame.Truck || (gameState != null && gameState.dancing &&
                     (!gameState.comboDanceActive || controlled || comboJoin)) ||
                     (gameState != null && gameState.partyStop != null) || dance != null)
            {
                float mk = 0f; string mv = null;
                if (dance != null)
                {
                    dance.timer += dt; mk = Mathf.Clamp01(dance.timer / dance.duration); mv = dance.type;
                    if (dance.type == "mortal") rx = -Ease(mk) * Mathf.PI * 2f;
                    if (mv == "cima") ry = Ease(mk) * Mathf.PI * 2f;
                    if (dance.timer > dance.duration) dance = null;
                }
                currentPose = PoseDefinitions.Dance(gameState.beat * Mathf.PI, def.estilo, mv == "mortal" ? "cima" : mv, mk);
                if (mv == null) danceContext = true;
                if (mv == "cima" || mv == "mortal") currentPose.bob = (currentPose.bob ?? 0f) + Mathf.Sin(mk * Mathf.PI) * 0.9f;
            }
            else
            {
                currentPose = PoseDefinitions.Idle(Time.time, seed);
                idle = true;
            }

            // Driver animation (Mixamo clips)
            string clip = null; float ts = 1f; bool restart = false;

            if (B.hasDriver && mode == CharacterMode.Normal && slideTimer <= 0f && !(crouching && hs < 0.5f))
            {
                float S = Height / 1.8f;
                float fx = Mathf.Sin(yaw), fz = Mathf.Cos(yaw), rxv = -Mathf.Cos(yaw), rzv = Mathf.Sin(yaw);
                float fwdDot = hs > 0.1f ? vel.x * fx + vel.z * fz : 0f;
                float rightDot = hs > 0.1f ? vel.x * rxv + vel.z * rzv : 0f;

                if (tr != null && !tr.grounded && (tr.type == "mortal" || tr.type == "flip"))
                {
                    clip = tr.type == "mortal" && airTricks.Count > 0 && B.HasClip("mortal2") ? "mortal2" : tr.type;
                    ts = B.GetClipDuration(clip) / tr.duration;
                    restart = !tr.started; tr.started = true;
                }
                else if (tr == null && stumble > 0f) { clip = "tombo"; ts = B.GetClipDuration("tombo") / 1f; restart = !stumbleOn; stumbleOn = true; }
                else if (tr == null && dashTimer > 0f && B.HasClip(dashClip)) { clip = dashClip; ts = 1.25f; restart = dashTimer > 0.31f; }
                else if (tr == null && !grounded && (diving || (vel.y < -9.5f && airTime > 0.72f)) && B.HasClip("queda")) { clip = "queda"; ts = 1.1f; }
                else if (tr == null && !grounded) { clip = "pulo"; ts = 1.1f; restart = wasGrounded || restartJumpClip; restartJumpClip = false; }
                else if (tr == null && grounded && cutTimer > 0f && B.HasClip("drible")) { clip = "drible"; ts = 1.35f; restart = cutTimer > 0.33f; }
                else if (tr == null && grounded && hs > 0.5f && (gameState == null || !gameState.dancing))
                {
                    if (rightDot > 0.65f * hs && B.HasClip("ladoD")) { clip = hs > 3.6f && B.HasClip("esquivaD") ? "esquivaD" : "ladoD"; ts = Mathf.Max(0.7f, hs / (1.8f * S)); }
                    else if (rightDot < -0.65f * hs && B.HasClip("ladoE")) { clip = hs > 3.6f && B.HasClip("esquivaE") ? "esquivaE" : "ladoE"; ts = Mathf.Max(0.7f, hs / (1.8f * S)); }
                    else if (fwdDot < -0.6f * hs && B.HasClip("correrT")) { clip = "correrT"; ts = Mathf.Max(0.7f, hs / (4.5f * S)); }
                    else if (hs < 3.2f) { clip = "andar"; ts = Mathf.Max(0.5f, Mathf.Min(2.2f, hs / (1.45f * S))); }
                    else { clip = "correr"; ts = Mathf.Max(0.6f, Mathf.Min(1.7f, hs / (5.2f * S))); }
                }
                else if (danceContext && grounded && B.HasClip("danca"))
                {
                    var avail = new List<string>();
                    foreach (var d in GameConstants.DanceClips) if (B.HasClip(d)) avail.Add(d);
                    clip = avail.Count > 0 ? avail[(int)danceStyle % avail.Count] : "danca";
                    ts = 1f;
                }
                else if (idle && grounded && frame == CharacterFrame.World && landTimer <= 0f)
                {
                    if (Mathf.Abs(turnRate) > 2.2f && B.HasClip("giroE") && B.HasClip("giroD")) { clip = turnRate > 0 ? "giroE" : "giroD"; ts = 1.2f; }
                    else clip = "idle";
                }
            }

            if (stumble <= 0f) stumbleOn = false;
            wasGrounded = grounded;

            if (clip != null)
            {
                B.PlayClip(clip, ts, restart);
                B.pivot.localRotation = Quaternion.identity;
                return;
            }

            // Landing knee bend
            if (landTimer > 0f && grounded && tr == null)
            {
                landTimer -= dt;
                currentPose = PoseDefinitions.Lerp(currentPose, PoseDefinitions.Land(), Mathf.Min(1f, landTimer / 0.12f));
            }

            // Lean in turns
            float dy = Mathf.DeltaAngle(prevYaw, yaw);
            prevYaw = yaw;
            turnRate += ((dt > 0 ? dy / dt : 0f) - turnRate) * Mathf.Min(1f, dt * 6f);
            if (hs > 2f && grounded) currentPose.sway = (currentPose.sway ?? 0f) - Mathf.Clamp(turnRate * 0.05f * hs / 6f, -0.25f, 0.25f);

            // Look at camera when idle nearby
            if (hs < 0.5f && tr == null && gameState != null && gameState.camPos != Vector3.zero && mode == CharacterMode.Normal)
            {
                Vector3 wp = WorldPosition(gameState);
                float d = Vector3.Distance(gameState.camPos, wp);
                if (d < 9f)
                {
                    float yawW = frame == CharacterFrame.Truck ? yaw + gameState.truck.yaw : yaw;
                    float a = Mathf.Atan2(gameState.camPos.x - wp.x, gameState.camPos.z - wp.z) - yawW;
                    a = Mathf.DeltaAngle(0f, a);
                    if (Mathf.Abs(a) < 1.8f)
                    {
                        currentPose.head = new float[]
                        {
                            (currentPose.head != null && currentPose.head.Length > 0 ? currentPose.head[0] : 0f) - 0.05f,
                            Mathf.Clamp(a, -0.8f, 0.8f) * 0.8f,
                            currentPose.head != null && currentPose.head.Length > 2 ? currentPose.head[2] : 0f
                        };
                    }
                }
            }

            // Smooth pose transitions
            float rate = tr != null && tr.grounded ? 40f : (tr != null || !grounded ? 22f : 13f);
            float k = 1f - Mathf.Exp(-dt * rate);
            currentPose = currentPose != null ? PoseDefinitions.Lerp(currentPose, currentPose, k) : currentPose;
            B.ApplyPose(currentPose);

            B.pivot.localRotation = Quaternion.Euler(rx, ry, rz);
        }

        private static float Ease(float t)
        {
            return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        }

        private static float Clamp01(float t) => Mathf.Clamp01(t);
    }

    // Supporting classes
    [System.Serializable]
    public class AIState
    {
        public float timer = 0f;
        public float trickTimer = 0f;
        public float stuckTimer = 0f;
        public float backTimer = 0f;
    }

    [System.Serializable]
    public class TrickState
    {
        public string type;
        public float timer;
        public float duration;
        public bool grounded;
        public int? dir;
        public float? moveAngle;
        public float? baseSpeed;
        public bool started = false;
    }

    [System.Serializable]
    public class SlideResult
    {
        public string type;
        public int index;
        public bool active;
    }

    [System.Serializable]
    public class DanceState
    {
        public string type;
        public float timer;
        public float duration;
    }

    [System.Serializable]
    public class WallHitInfo
    {
        public ColliderInfo collider;
        public Vector3 normal;
    }

    [System.Serializable]
    public class ColliderInfo
    {
        public bool walk;
        public bool ramp;
    }

    [System.Serializable]
    public class InputData
    {
        public float mx, mz, want;
    }

    [System.Serializable]
    public class PoseState
    {
        public float lean, twist, sway, bob, spineTwist, spineSway;
        public float[] sh = new float[4];
        public float[] el = new float[2];
        public float[] th = new float[4];
        public float[] kn = new float[2];
        public float[] head = new float[3];
    }
}