using UnityEngine;
using System.Collections.Generic;

namespace FamiliaCarretaJoy
{
    /// <summary>
    /// Character body with procedural anatomy and Mixamo animation retargeting.
    /// Ported from the Body class and RigBody/XBotBody in characters.js.
    /// </summary>
    public class CharacterBody : MonoBehaviour
    {
        [Header("References")]
        public CharacterDefinition def;
        public CostumeDefinition costume;
        public GameObject rigRoot;
        public GameObject pivot;
        public MixamoDriver driver;

        [Header("Body Parts")]
        public Transform hips;
        public Transform spine;
        public Transform neck;
        public Transform head;
        public Transform shL, shR;
        public Transform elL, elR;
        public Transform thL, thR;
        public Transform knL, knR;
        public Transform handL, handR;

        [Header("Proportions")]
        public float hipHeight;
        public float torsoHeight;
        public float neckHeight;
        public float shoulderHeight;
        public float upperArmLength;
        public float forearmLength;
        public float handRadius;
        public float thighLength;
        public float shinLength;
        public float footRadius;

        [Header("Animation")]
        public bool hasDriver = false;
        public Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        public PoseState restPose = null;
        public PoseState currentPose = null;

        // Materials
        private Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
        private const float SKIN_COLOR = 0xf3c9a5f / 0xffffff;

        public void Initialize(CharacterDefinition definition, CostumeDefinition costumeDef, GameObject modelAssets)
        {
            def = definition;
            costume = costumeDef;
            BuildBody(modelAssets);
            SetupDriver(modelAssets);
        }

        private void BuildBody(GameObject modelAssets)
        {
            // Create hierarchy
            rigRoot = new GameObject("Rig");
            rigRoot.transform.SetParent(transform);
            rigRoot.transform.localPosition = Vector3.zero;
            rigRoot.transform.localRotation = Quaternion.identity;

            pivot = new GameObject("Pivot");
            pivot.transform.SetParent(rigRoot.transform);
            pivot.transform.localPosition = Vector3.zero;
            pivot.transform.localRotation = Quaternion.identity;

            float H = def.altura;
            float F = def.forte;
            bool kid = def.kid;

            // Calculate proportions (ported from characters.js)
            float hip, torso, neck, sh, uarm, farm, rl, ra, tw, td;
            if (kid)
            {
                hip = 0.44f * H; torso = 0.27f * H; neck = 0.05f * H; sh = 0.11f * H;
                uarm = 0.165f * H; farm = 0.15f * H; rl = 0.05f * H; ra = 0.034f * H;
                tw = 0.112f * H; td = 0.072f * H;
            }
            else
            {
                hip = 0.5f * H; torso = 0.3f * H; neck = 0.035f * H; sh = 0.125f * H * Mathf.Sqrt(F);
                uarm = 0.175f * H; farm = 0.16f * H; rl = 0.045f * H * F; ra = 0.03f * H * F;
                tw = 0.12f * H * F; td = 0.075f * H * F;
            }
            float thigh = hip * 0.5f;
            float shin = hip * 0.46f;

            hipHeight = hip;
            torsoHeight = torso;
            neckHeight = neck;
            shoulderHeight = sh;
            upperArmLength = uarm;
            forearmLength = farm;
            handRadius = ra;
            thighLength = thigh;
            shinLength = shin;
            footRadius = rl;

            pivot.transform.localPosition = new Vector3(0f, hip, 0f);

            // Build joint hierarchy
            hips = CreateJoint(pivot.transform, "Hips", Vector3.zero);
            var pelvis = CreateLimb(hips, "Pelvis", Vector3.zero, tw * 0.95f, td * 1.3f, td * 1.05f, GetCostumeColor("shorts", "calca"));
            pelvis.transform.localRotation = Quaternion.identity;

            spine = CreateJoint(hips, "Spine", new Vector3(0f, 0.02f, 0f));
            var torsoObj = CreateLimb(spine, "Torso", new Vector3(0f, -torso * 0.04f, 0f), tw * 1.05f, torso * 0.97f, td * 1.1f, GetCostumeColor("tronco"));
            AddTorsoDetails(spine, torso, tw, td, torsoObj);

            neck = CreateJoint(spine, "Neck", new Vector3(0f, torso * (kid ? 0.98f : 0.93f) + (F > 1.5f ? torso * 0.08f : 0f), 0f));
            var neckMesh = CreateLimb(neck, "NeckMesh", new Vector3(0f, neck * 0.6f, 0f), tw * 0.28f, neck * 2f, tw * 0.32f, GetSkinColor());
            neckMesh.transform.localRotation = Quaternion.identity;

            head = CreateJoint(neck, "Head", new Vector3(0f, neck * 1.2f, 0f));

            // Arms
            for (int s = 0; s < 2; s++)
            {
                int side = s == 0 ? 1 : -1;
                string suffix = s == 0 ? "L" : "R";
                var shoulder = CreateJoint(spine, "Sh" + suffix, new Vector3(side * sh, torso * 0.86f, 0f));
                float mus = costume.manga == "nenhuma" ? 1.3f : 1f;
                CreateLimb(shoulder, "UpArm" + suffix, new Vector3(0f, -uarm * 0.5f, 0f), ra * 1.15f * mus, ra * 0.85f * mus, uarm, GetCostumeColor("bracos"));
                var elbow = CreateJoint(shoulder, "El" + suffix, new Vector3(0f, -uarm, 0f));
                float faMat = costume.manga == "longa" || costume.manga == "nenhuma" ? GetCostumeColor("bracos") : GetSkinColor();
                CreateLimb(elbow, "Forearm" + suffix, new Vector3(0f, -(farm - ra * 1.6f) * 0.5f, 0f), ra * 0.95f * mus, ra * 0.7f * mus, farm - ra * 1.6f, faMat);
                var hand = CreateJoint(elbow, "Hand" + suffix, new Vector3(0f, -farm + ra * 0.9f, 0f));
                if (s == 0) handL = hand; else handR = hand;
                CreateHand(hand, ra, GetCostumeColor("maos"));
                if (costume.escudo && s == 0) CreateShield(elbow, farm, H);
            }

            // Legs
            for (int s = 0; s < 2; s++)
            {
                int side = s == 0 ? 1 : -1;
                string suffix = s == 0 ? "L" : "R";
                var thighJoint = CreateJoint(hips, "Th" + suffix, new Vector3(side * tw * 0.5f, -td * 0.3f, 0f));
                float thighMat = costume.shorts != 0 && !costume.calcaLonga ? GetCostumeColor("shorts") : GetCostumeColor("calca");
                CreateLimb(thighJoint, "Thigh" + suffix, new Vector3(0f, -thigh * 0.5f, 0f), rl * 1.15f, rl * 0.82f, thigh, thighMat);
                var knee = CreateJoint(thighJoint, "Kn" + suffix, new Vector3(0f, -thigh, 0f));
                float shinMat = costume.pernasPele != 0 ? GetCostumeColor("pernasPele") :
                    (costume.shorts != 0 && !costume.calcaLonga && costume.shorts != costume.calca ? GetCostumeColor("calca") : GetCostumeColor("calca"));
                CreateLimb(knee, "Shin" + suffix, new Vector3(0f, -(shin - rl * 0.5f) * 0.5f, 0f), rl * 0.85f, rl * 0.6f, shin - rl * 0.5f, shinMat);
                var foot = CreateJoint(knee, "Foot" + suffix, new Vector3(0f, -shin - rl * 0.35f, rl * 0.8f));
                foot.localRotation = Quaternion.Euler(90f, 0f, 0f);
                CreateLimb(foot, "FootMesh" + suffix, Vector3.zero, rl * 0.85f, rl * 0.85f, rl * 1.6f, GetCostumeColor("sapatos"));
                foot.localScale = new Vector3(1f, 1f, 0.75f);
                if (costume.sola != 0) CreateSole(knee, rl, shin, H);
            }

            // Disable shadows on procedural meshes
            foreach (var renderer in rigRoot.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private void AddTorsoDetails(Transform spine, float torso, float tw, float td, GameObject torsoObj)
        {
            var C = costume;

            // Stripes
            if (C.listras)
            {
                for (int k = 0; k < 3; k++)
                {
                    var ring = CreateLimb(spine, "Stripe" + k, new Vector3(0f, torso * (0.12f + k * 0.07f), 0f),
                        tw * 0.84f, torso * 0.07f, td * 0.9f, k % 2 == 0 ? Color.white : new Color(0xd32f2f / 255f, 0, 0));
                    ring.transform.localRotation = Quaternion.identity;
                }
            }

            // Belt
            if (C.cinto != 0)
            {
                var belt = CreateLimb(spine, "Belt", new Vector3(0f, torso * 0.04f, 0f), tw * 0.87f, torso * 0.08f, td * 0.93f, IntToColor(C.cinto));
                belt.transform.localRotation = Quaternion.identity;
            }

            // Suspenders
            if (C.suspensorio)
            {
                for (int s = 0; s < 2; s++)
                {
                    int side = s == 0 ? 1 : -1;
                    var susp = CreateLimb(spine, "Suspender" + side, new Vector3(side * tw * 0.45f, torso * 0.5f, 0f),
                        0.035f * def.altura, torso * 0.85f, td * 2.15f, IntToColor(C.suspensorioColor));
                }
            }

            // Tie
            if (C.gravata)
            {
                var tie = CreateLimb(spine, "Tie", new Vector3(0f, torso * 0.62f, td * 0.98f), tw * 0.18f, torso * 0.55f, tw * 0.18f, IntToColor(C.gravataColor));
                tie.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
            }

            // Collar
            if (C.gola)
            {
                var collar = CreateLimb(spine, "Collar", new Vector3(0f, torso * 0.92f, 0f), tw * 0.62f, tw * 0.18f, tw * 0.62f, IntToColor(C.golaColor));
                collar.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            // Emblem
            if (!string.IsNullOrEmpty(C.emblema))
            {
                var emblem = CreateLimb(spine, "Emblem", new Vector3(0f, torso * 0.6f, td * 1.12f), tw * 0.5f, tw * 0.5f, 0.01f, Color.white);
                // Would need texture for emblem
            }

            // Shoulder pads
            if (C.ombreiras)
            {
                for (int s = 0; s < 2; s++)
                {
                    int side = s == 0 ? 1 : -1;
                    var pad = CreateLimb(spine, "ShoulderPad" + side, new Vector3(side * sh, torso * 0.93f, 0f), ra * 2f, ra * 2f, ra * 2f, IntToColor(C.ombreirasColor));
                    pad.transform.localScale = new Vector3(1.2f, 0.9f, 1.1f);
                }
            }

            // Ripped
            if (C.rasgado)
            {
                var rip = CreateLimb(spine, "Rip", new Vector3(0f, -torso * 0.02f, 0f), tw * 0.9f, torso * 0.1f, td * 0.95f, IntToColor(C.shorts));
            }
        }

        private void CreateHand(Transform parent, float ra, Color skinColor)
        {
            var palm = CreateLimb(parent, "Palm", Vector3.zero, ra * 1.05f, ra * 1.05f, ra * 1.05f, skinColor);
            palm.transform.localScale = new Vector3(0.75f, 1.3f, 1.05f);

            var thumb = CreateLimb(parent, "Thumb", new Vector3(0f, ra * 0.1f, ra * 0.75f), ra * 0.32f, ra * 0.32f, ra * 0.6f, skinColor);
            thumb.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
        }

        private void CreateShield(Transform elbow, float farm, float H)
        {
            var shield = CreateLimb(elbow, "Shield", new Vector3(ra * 2.2f, -farm * 0.5f, 0f), 0.2f * H * 0.75f, 0.03f, 0.2f * H * 0.75f, new Color(0xd32f2f / 255f, 0, 0));
            shield.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        private void CreateSole(Transform knee, float rl, float shin, float H)
        {
            var sole = CreateLimb(knee, "Sole", new Vector3(0f, -shin - rl * 1.05f, rl * 0.8f), rl * 1.7f, rl * 0.35f, rl * 3.6f, IntToColor(costume.sola));
        }

        private Transform CreateJoint(Transform parent, string name, Vector3 localPos)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(parent);
            joint.localPosition = localPos;
            joint.localRotation = Quaternion.identity;
            return joint;
        }

        private GameObject CreateLimb(Transform parent, string name, Vector3 localPos, float radiusTop, float radiusBottom, float length, Color color)
        {
            var limb = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            limb.name = name;
            limb.transform.SetParent(parent);
            limb.transform.localPosition = localPos;
            limb.transform.localRotation = Quaternion.identity;

            // Adjust capsule to have different top/bottom radii
            var collider = limb.GetComponent<CapsuleCollider>();
            if (collider != null) DestroyImmediate(collider);

            var renderer = limb.GetComponent<MeshRenderer>();
            renderer.material = GetMaterial(color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Scale to match radii
            limb.transform.localScale = new Vector3(radiusTop * 2f, length * 0.5f, radiusBottom * 2f);
            limb.transform.localPosition += Vector3.up * (length * 0.5f);

            return limb;
        }

        private void CreateHead()
        {
            if (def.kid)
            {
                // Kids use real head from model
                // This would be handled by attaching the actual head model
            }
            else
            {
                // Adults use procedural head with face texture
                float R = 0.068f * def.altura * def.cabecaGrande;
                var face = CreateLimb(head, "Face", new Vector3(0f, R * 1.05f, 0f), R, R, R, IntToColor(def.rosto?.pele ?? 0xf3c9a5));
                face.transform.localScale = new Vector3(0.95f, 1.08f, 1f);

                // Add facial features (eyes, nose, ears, etc.)
                // This would be more complex with actual textures
            }

            // Hair and headwear
            if (def.cabelo != 0 && (def.rosto?.mascara?.tipo != "total"))
            {
                var hair = CreateLimb(head, "Hair", new Vector3(0f, R * 1.08f, 0f), R * 1.04f, R * 1.04f, R * 1.04f, IntToColor(def.cabelo));
                hair.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
                hair.transform.localScale = new Vector3(1f, 0.55f, 1f);
            }

            // Headwear
            if (!string.IsNullOrEmpty(costume.chapeu))
            {
                CreateHeadwear(costume.chapeu);
            }
        }

        private void CreateHeadwear(string type)
        {
            // Implementation for different hat types
            // This would create the appropriate hat geometry
        }

        private void SetupDriver(GameObject modelAssets)
        {
            // Find XBot model for Mixamo retargeting
            var xbot = modelAssets.transform.Find("xbot");
            if (xbot != null)
            {
                driver = gameObject.AddComponent<MixamoDriver>();
                driver.Initialize(xbot.gameObject, this);
                hasDriver = true;
            }
        }

        public void ApplyPose(PoseState pose)
        {
            currentPose = pose;
            if (pose == null) return;

            pivot.transform.localPosition = new Vector3(0f, pose.bob ?? 0f, 0f);
            pivot.transform.localRotation = Quaternion.Euler(0f, pose.twist ?? 0f, pose.sway ?? 0f);
            spine.transform.localRotation = Quaternion.Euler(pose.lean ?? 0f, pose.spineTwist ?? 0f, pose.spineSway ?? 0f);

            if (pose.head != null && pose.head.Length >= 2)
                head.transform.localRotation = Quaternion.Euler(pose.head[0], pose.head[1], pose.head.Length > 2 ? pose.head[2] : 0f);

            if (pose.sh != null && pose.sh.Length >= 4)
            {
                shL.localRotation = Quaternion.Euler(pose.sh[0], 0f, pose.sh[1]);
                shR.localRotation = Quaternion.Euler(pose.sh[2], 0f, pose.sh[3]);
            }

            if (pose.el != null && pose.el.Length >= 2)
            {
                elL.localRotation = Quaternion.Euler(pose.el[0], 0f, 0f);
                elR.localRotation = Quaternion.Euler(pose.el[1], 0f, 0f);
            }

            if (pose.th != null && pose.th.Length >= 4)
            {
                thL.localRotation = Quaternion.Euler(pose.th[0], 0f, pose.th[1]);
                thR.localRotation = Quaternion.Euler(pose.th[2], 0f, pose.th[3]);
            }

            if (pose.kn != null && pose.kn.Length >= 2)
            {
                knL.localRotation = Quaternion.Euler(pose.kn[0], 0f, 0f);
                knR.localRotation = Quaternion.Euler(pose.kn[1], 0f, 0f);
            }
        }

        public void PlayClip(string clipName, float timeScale = 1f, bool restart = false)
        {
            if (driver != null)
                driver.PlayClip(clipName, timeScale, restart);
        }

        public float GetClipDuration(string clipName)
        {
            if (clips.ContainsKey(clipName))
                return clips[clipName].length;
            return 1f;
        }

        public bool HasClip(string clipName)
        {
            return clips.ContainsKey(clipName);
        }

        private Color GetCostumeColor(string key, string fallback = "")
        {
            var field = costume.GetType().GetField(key);
            if (field != null)
            {
                int val = (int)field.GetValue(costume);
                if (val != 0) return IntToColor(val);
            }
            if (!string.IsNullOrEmpty(fallback))
            {
                var fbField = costume.GetType().GetField(fallback);
                if (fbField != null)
                {
                    int val = (int)fbField.GetValue(costume);
                    if (val != 0) return IntToColor(val);
                }
            }
            return IntToColor(0xffffff);
        }

        private Color GetSkinColor()
        {
            if (def.rosto?.mascara != null && def.rosto.mascara.tipo != "olhos")
                return IntToColor(def.rosto.mascara.cor);
            return def.rosto != null ? IntToColor(def.rosto.pele) : IntToColor(0xf3c9a5);
        }

        private static Color IntToColor(int hex)
        {
            return new Color(
                ((hex >> 16) & 0xFF) / 255f,
                ((hex >> 8) & 0xFF) / 255f,
                (hex & 0xFF) / 255f
            );
        }

        public void SetCostume(string costumeKey)
        {
            // Would reload costume - for now just store the key
            // In a full implementation, this would rebuild the body
        }
    }

    /// <summary>
    /// Mixamo animation driver for retargeting animations to procedural body.
    /// Ported from Driver in anim.js and XBotBody in xbotbody.js.
    /// </summary>
    public class MixamoDriver : MonoBehaviour
    {
        public GameObject xbotModel;
        public CharacterBody body;
        public Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        public Animator animator;
        public Dictionary<string, Transform> boneMap = new Dictionary<string, Transform>();

        private string currentClip = "";
        private float clipTimer = 0f;
        private float clipTimeScale = 1f;
        private bool clipRestart = false;

        public void Initialize(GameObject xbot, CharacterBody characterBody)
        {
            xbotModel = xbot;
            body = characterBody;
            animator = xbot.GetComponent<Animator>();

            // Map XBot bones to our procedural bones
            MapBones(xbot.transform);

            // Load animation clips from XBot
            LoadClips(xbot);
        }

        private void MapBones(Transform root)
        {
            // Map Mixamo bone names to our bone hierarchy
            var mappings = new Dictionary<string, string>
            {
                { "mixamorig:Hips", "Hips" },
                { "mixamorig:Spine", "Spine" },
                { "mixamorig:Spine1", "Spine" },
                { "mixamorig:Spine2", "Spine" },
                { "mixamorig:Neck", "Neck" },
                { "mixamorig:Head", "Head" },
                { "mixamorig:LeftShoulder", "ShL" },
                { "mixamorig:LeftArm", "UpArmL" },
                { "mixamorig:LeftForeArm", "ForearmL" },
                { "mixamorig:LeftHand", "HandL" },
                { "mixamorig:RightShoulder", "ShR" },
                { "mixamorig:RightArm", "UpArmR" },
                { "mixamorig:RightForeArm", "ForearmR" },
                { "mixamorig:RightHand", "HandR" },
                { "mixamorig:LeftUpLeg", "ThL" },
                { "mixamorig:LeftLeg", "ShinL" },
                { "mixamorig:LeftFoot", "FootL" },
                { "mixamorig:RightUpLeg", "ThR" },
                { "mixamorig:RightLeg", "ShinR" },
                { "mixamorig:RightFoot", "FootR" }
            };

            foreach (var kvp in mappings)
            {
                var bone = FindDeepChild(root, kvp.Key);
                if (bone != null)
                    boneMap[kvp.Value] = bone;
            }
        }

        private Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var result = FindDeepChild(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private void LoadClips(GameObject xbot)
        {
            // In Unity, clips would be loaded from the model's AnimationClips
            // For now, we'll create placeholder clips
            string[] clipNames = new string[]
            {
                "idle", "andar", "correr", "pulo", "queda", "mortal", "mortal2",
                "flip", "estrela", "pirueta", "tombo", "danca", "danca2", "danca3", "danca4",
                "correrK", "andarK", "correrT", "ladoD", "ladoE", "esquivaD", "esquivaE",
                "drible", "giroD", "giroE"
            };

            foreach (var name in clipNames)
            {
                // In real implementation, load from model
                // clips[name] = Resources.Load<AnimationClip>($"Animations/{name}");
            }
        }

        public void Update(float dt, object state)
        {
            // Update animation state
            if (!string.IsNullOrEmpty(currentClip) && clips.ContainsKey(currentClip))
            {
                clipTimer += dt * clipTimeScale;
                if (clipTimer >= clips[currentClip].length / clipTimeScale)
                {
                    clipTimer = 0f;
                    if (!clipRestart) currentClip = "idle";
                }

                // Sample animation and retarget to procedural bones
                RetargetPose();
            }
        }

        public void PlayClip(string clipName, float timeScale = 1f, bool restart = false)
        {
            if (!clips.ContainsKey(clipName)) return;
            currentClip = clipName;
            clipTimeScale = timeScale;
            clipRestart = restart;
            clipTimer = 0f;
        }

        private void RetargetPose()
        {
            // Sample animator at current time and apply to procedural bones
            // This is a simplified version - full implementation would use AnimationStream
            if (animator == null) return;

            // The original JS does per-bone retargeting with relative rotations
            // In Unity, we'd use Playable API or AnimationStream for this
        }

        public void RetargetToProcedural(Dictionary<string, Transform> proceduralBones, System.Action<float> onHipY)
        {
            // Apply hip bob
            if (boneMap.ContainsKey("Hips"))
            {
                float bob = boneMap["Hips"].localPosition.y;
                onHipY?.Invoke(bob);
            }

            // Retarget each bone (simplified)
            foreach (var kvp in boneMap)
            {
                if (proceduralBones.ContainsKey(kvp.Key))
                {
                    // In full implementation, copy rotation with offsets
                    // proceduralBones[kvp.Key].localRotation = kvp.Value.localRotation * offsetRotation;
                }
            }

            // Limit shoulder rotation for skinned bodies
            if (body != null && body.def != null && body.def.kid)
            {
                LimitShoulder("ShL");
                LimitShoulder("ShR");
            }
        }

        private void LimitShoulder(string boneName)
        {
            if (proceduralBones.ContainsKey(boneName))
            {
                var q = proceduralBones[boneName].localRotation;
                float angle = 2f * Mathf.Acos(Mathf.Min(1f, Mathf.Abs(q.w)));
                if (angle > 1.5f)
                {
                    q = Quaternion.Slerp(q, Quaternion.identity, 1f - 1.5f / angle);
                    proceduralBones[boneName].localRotation = q;
                }
            }
        }
    }
}