using UnityEngine;
using System.Collections.Generic;

namespace FamiliaCarretaJoy
{
    /// <summary>
    /// Truck controller, ported from the Truck class in truck.js.
    /// Handles truck movement, physics, lights, and variants.
    /// </summary>
    public class TruckController : MonoBehaviour
    {
        [Header("Variant")]
        public TruckVariant variant;
        public GameObject modelRoot;

        [Header("Dimensions")]
        public float scale = 1f;
        public float height = 4.5f;
        public float roofY = 4.4f;
        public float halfWidth = 1.5f;
        public float roofZMin = -1.5f;
        public float roofZMax = 1f;

        [Header("State")]
        public float x = 0f, z = 0f, y = 0f;
        public float yaw = 0f;
        public float velocity = 0f;
        public float steer = 0f;
        public float pitch = 0f;
        public float roll = 0f;
        public float autoS = 0f;
        public float autoDist = 0f;
        public List<Vector2> route = null;
        public bool routeLost = true;
        public bool inited = false;
        public Vector3 worldVelocity = Vector3.zero;
        public bool night = false;
        public bool bumped = false;

        [Header("Lights & Effects")]
        public List<Vector3> bulbPositions = new List<Vector3>();
        public GameObject bulbsMesh;
        public List<LightBeam> beams = new List<LightBeam>();
        public GameObject lasers;
        public GameObject discoBall;
        public GameObject spinStar;
        public GameObject[] flags;
        public Light headlightSpot;
        public Light[] partyLights;
        public GameObject shadowPlane;

        // Components
        private Rigidbody rb;

        public void Initialize(TruckVariant truckVariant, GameObject model)
        {
            variant = truckVariant;
            modelRoot = model;
            modelRoot.transform.SetParent(transform);
            modelRoot.transform.localPosition = Vector3.zero;
            modelRoot.transform.localRotation = Quaternion.identity;

            BuildLights();
            BuildExtras();

            rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 5000f;
            rb.drag = 0.1f;
            rb.angularDrag = 10f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        private void BuildLights()
        {
            // Bulb positions around the truck roof
            bulbPositions.Clear();
            float H = roofY, W = halfWidth;
            for (float z = roofZMin + 0.3f; z <= roofZMax - 0.2f; z += 0.45f)
            {
                bulbPositions.Add(new Vector3(W + 0.05f, H + 0.08f, z));
                bulbPositions.Add(new Vector3(-W - 0.05f, H + 0.08f, z));
                bulbPositions.Add(new Vector3(W + 0.08f, 1.05f, z));
                bulbPositions.Add(new Vector3(-W - 0.08f, 1.05f, z));
            }
            for (float x = -W; x <= W; x += 0.45f)
            {
                bulbPositions.Add(new Vector3(x, H + 0.08f, roofZMin));
                bulbPositions.Add(new Vector3(x, H + 0.08f, roofZMax));
            }

            // Create bulb mesh (instanced)
            CreateBulbMesh();

            // Light beams
            CreateLightBeams();

            // Lasers
            CreateLasers();

            // Disco ball
            CreateDiscoBall();

            // Headlights
            CreateHeadlights();

            // Party lights
            CreatePartyLights();

            // Shadow plane
            CreateShadowPlane();
        }

        private void CreateBulbMesh()
        {
            bulbsMesh = new GameObject("Bulbs");
            bulbsMesh.transform.SetParent(transform);
            var mf = bulbsMesh.AddComponent<MeshFilter>();
            var mr = bulbsMesh.AddComponent<MeshRenderer>();

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mf.mesh = sphere.GetComponent<MeshFilter>().mesh;
            DestroyImmediate(sphere);

            var mat = new Material(Shader.Find("Standard"));
            mat.color = Color.white;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.white);
            mr.material = mat;

            // Use GPU instancing
            mr.enableInstancing = true;
        }

        private void CreateLightBeams()
        {
            var beamPositions = new Vector2[]
            {
                new Vector2(halfWidth - 0.2f, roofZMin + 0.4f),
                new Vector2(-halfWidth + 0.2f, roofZMin + 0.4f),
                new Vector2(halfWidth - 0.2f, roofZMax - 0.3f),
                new Vector2(-halfWidth + 0.2f, roofZMax - 0.3f)
            };

            for (int i = 0; i < beamPositions.Length; i++)
            {
                var beamObj = new GameObject($"Beam_{i}");
                beamObj.transform.SetParent(transform);
                beamObj.transform.localPosition = new Vector3(beamPositions[i].x, roofY + 0.35f, beamPositions[i].y);

                var holder = new GameObject("Holder");
                holder.transform.SetParent(beamObj.transform);
                holder.transform.localPosition = Vector3.zero;

                var light = holder.AddComponent<Light>();
                light.type = LightType.Spot;
                light.range = 26f;
                light.spotAngle = 60f;
                light.color = Color.white;
                light.intensity = 0f;
                light.shadows = LightShadows.None;

                var cone = GameObject.CreatePrimitive(PrimitiveType.Cone);
                cone.transform.SetParent(holder.transform);
                cone.transform.localPosition = new Vector3(0f, -13f, 0f);
                cone.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
                cone.transform.localScale = new Vector3(2.6f, 26f, 2.6f);
                var coneRenderer = cone.GetComponent<MeshRenderer>();
                var beamMat = new Material(Shader.Find("Particles/Additive"));
                beamMat.color = new Color(1f, 1f, 1f, 0.5f);
                coneRenderer.material = beamMat;
                coneRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                var pivot = new GameObject("Pivot").transform;
                pivot.SetParent(holder.transform);
                pivot.localPosition = Vector3.zero;
                pivot.localRotation = Quaternion.identity;
                cone.transform.SetParent(pivot);

                beams.Add(new LightBeam
                {
                    pivot = pivot,
                    cone = cone.transform,
                    light = light,
                    index = i
                });
            }
        }

        private void CreateLasers()
        {
            lasers = new GameObject("Lasers");
            lasers.transform.SetParent(transform);
            lasers.transform.localPosition = new Vector3(0f, roofY + 0.5f, roofZMin + 0.2f);

            for (int k = 0; k < 7; k++)
            {
                var laser = GameObject.CreatePrimitive(PrimitiveType.Cube);
                laser.transform.SetParent(lasers.transform);
                laser.transform.localScale = new Vector3(0.05f, 0.05f, 60f);
                laser.transform.localPosition = new Vector3(0f, 0f, -30f);
                var mr = laser.GetComponent<MeshRenderer>();
                var mat = new Material(Shader.Find("Particles/Additive"));
                mat.color = k % 2 == 0 ? new Color(0f, 1f, 0.53f, 0.8f) : new Color(1f, 0f, 0.8f, 0.8f);
                mr.material = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                laser.GetComponent<Collider>().enabled = false;
            }
        }

        private void CreateDiscoBall()
        {
            discoBall = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            discoBall.transform.SetParent(transform);
            discoBall.transform.localPosition = new Vector3(0f, roofY + 2.2f, roofZMin + 2.5f);
            discoBall.transform.localScale = Vector3.one * 0.45f;
            var mr = discoBall.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Standard"));
            mat.color = Color.gray;
            mat.metallic = 1f;
            mat.smoothness = 0.85f;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.2f, 0.2f, 0.2f));
            mr.material = mat;

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.transform.SetParent(transform);
            pole.transform.localPosition = new Vector3(0f, roofY + 1.1f, roofZMin + 2.5f);
            pole.transform.localScale = new Vector3(0.04f, 2f, 0.04f);
        }

        private void CreateHeadlights()
        {
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -1f : 1f;
                var lightObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lightObj.transform.SetParent(transform);
                lightObj.transform.localPosition = new Vector3(x, 0.95f, GameConstants.TruckLength / 2f + 0.05f);
                lightObj.transform.localScale = Vector3.one * 0.22f;
                var mr = lightObj.GetComponent<MeshRenderer>();
                var mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(1f, 0.99f, 0.91f);
                mat.EnableKeyword("_EMISSION");
                mr.material = mat;
                DestroyImmediate(lightObj.GetComponent<Collider>());
            }

            headlightSpot = new GameObject("HeadlightSpot").AddComponent<Light>();
            headlightSpot.transform.SetParent(transform);
            headlightSpot.type = LightType.Spot;
            headlightSpot.range = 60f;
            headlightSpot.spotAngle = 0.55f * Mathf.Rad2Deg * 2f;
            headlightSpot.color = new Color(1f, 0.97f, 0.81f);
            headlightSpot.intensity = 0f;
            headlightSpot.transform.localPosition = new Vector3(0f, 1.2f, GameConstants.TruckLength / 2f);
            headlightSpot.transform.localRotation = Quaternion.identity;
        }

        private void CreatePartyLights()
        {
            partyLights = new Light[3];
            Color[] colors = { new Color(1f, 0.18f, 0.58f), new Color(0f, 0.9f, 1f), new Color(1f, 0.83f, 0f) };
            for (int i = 0; i < 3; i++)
            {
                var pl = new GameObject($"PartyLight_{i}").AddComponent<Light>();
                pl.transform.SetParent(transform);
                pl.type = LightType.Point;
                pl.range = 22f;
                pl.color = colors[i];
                pl.intensity = 0f;
                float x = i == 1 ? -halfWidth - 2.5f : halfWidth + 2.5f;
                pl.transform.localPosition = new Vector3(x, roofY + 1.5f, -3f + i * 4f);
                partyLights[i] = pl;
            }
        }

        private void CreateShadowPlane()
        {
            shadowPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            shadowPlane.transform.SetParent(transform);
            shadowPlane.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            shadowPlane.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadowPlane.transform.localScale = new Vector3((halfWidth * 2f + 1.2f) / 10f, (GameConstants.TruckLength + 1f) / 10f, 1f);
            var mr = shadowPlane.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Transparent/Diffuse"));
            mat.color = new Color(0f, 0f, 0f, 0.9f);
            mr.material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            DestroyImmediate(shadowPlane.GetComponent<Collider>());
        }

        private void BuildExtras()
        {
            if (string.IsNullOrEmpty(variant.extra)) return;

            switch (variant.extra)
            {
                case "estrela":
                    CreateStar();
                    break;
                case "neon":
                    CreateNeonTubes();
                    break;
                case "coracoes":
                    CreateHearts();
                    break;
                case "bandeiras":
                    CreateFlags();
                    break;
                case "torre":
                    CreateTower();
                    break;
            }
        }

        private void CreateStar()
        {
            spinStar = new GameObject("Star");
            spinStar.transform.SetParent(transform);
            spinStar.transform.localPosition = new Vector3(0f, roofY + 2.2f, GameConstants.TruckLength / 2f - 3.2f);

            // Create star mesh procedurally
            var mf = spinStar.AddComponent<MeshFilter>();
            var mr = spinStar.AddComponent<MeshRenderer>();

            var starMesh = new Mesh();
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();

            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2f + Mathf.PI / 2f;
                float r = i % 2 == 0 ? 1.3f : 0.55f;
                vertices.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
            // Extrude
            var baseVertices = new List<Vector3>(vertices);
            for (int i = 0; i < vertices.Count; i++)
            {
                vertices.Add(vertices[i] + Vector3.up * 0.25f);
            }

            for (int i = 0; i < 10; i++)
            {
                int next = (i + 1) % 10;
                // Bottom
                triangles.Add(0); triangles.Add(next); triangles.Add(i);
                // Top
                triangles.Add(10 + i); triangles.Add(10 + i); triangles.Add(10 + next); // simplified
                // Sides
            }

            starMesh.vertices = vertices.ToArray();
            starMesh.triangles = triangles.ToArray();
            starMesh.RecalculateNormals();
            mf.mesh = starMesh;

            var mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(1f, 0.83f, 0.31f);
            mat.metallic = 0.8f;
            mat.smoothness = 0.75f;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(1f, 0.7f, 0f, 0.5f));
            mr.material = mat;
        }

        private void CreateNeonTubes()
        {
            for (int y = 0; y < 2; y++)
            {
                float yPos = y == 0 ? 0.6f : roofY - 0.15f;
                for (int s = 0; s < 2; s++)
                {
                    float x = s == 0 ? -(halfWidth + 0.1f) : halfWidth + 0.1f;
                    var tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    tube.transform.SetParent(transform);
                    tube.transform.localPosition = new Vector3(x, yPos, -1f);
                    tube.transform.localScale = new Vector3(0.06f, (GameConstants.TruckLength - 2.5f) * 0.5f, 0.06f);
                    tube.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    var mr = tube.GetComponent<MeshRenderer>();
                    var mat = new Material(Shader.Find("Standard"));
                    mat.color = yPos < 1f ? new Color(0f, 0.9f, 1f) : new Color(1f, 0f, 0.9f);
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", mat.color);
                    mr.material = mat;
                    DestroyImmediate(tube.GetComponent<Collider>());
                }
            }
        }

        private void CreateHearts()
        {
            Vector2[] positions = {
                new Vector2(halfWidth, roofZMin), new Vector2(-halfWidth, roofZMin),
                new Vector2(halfWidth, roofZMax), new Vector2(-halfWidth, roofZMax)
            };
            Color[] colors = { new Color(1f, 0.31f, 0.72f), new Color(0.9f, 0.09f, 0.27f), Color.white };

            foreach (var pos in positions)
            {
                for (int k = 0; k < 3; k++)
                {
                    var heart = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    heart.transform.SetParent(transform);
                    heart.transform.localPosition = new Vector3(pos.x + (k - 1) * 0.5f, roofY + 3f + k * 0.4f, pos.y);
                    heart.transform.localScale = new Vector3(1f, 1.15f, 1f) * 0.45f;
                    var mr = heart.GetComponent<MeshRenderer>();
                    var mat = new Material(Shader.Find("Standard"));
                    mat.color = colors[k];
                    mat.smoothness = 0.7f;
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(0.2f, 0f, 0.07f));
                    mr.material = mat;
                    DestroyImmediate(heart.GetComponent<Collider>());

                    // String
                    var str = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    str.transform.SetParent(transform);
                    str.transform.localPosition = new Vector3(pos.x + (k - 1) * 0.25f, roofY + 1.5f + k * 0.2f, pos.y);
                    str.transform.localScale = new Vector3(0.01f, 3f + k * 0.4f, 0.01f);
                    var strMr = str.GetComponent<MeshRenderer>();
                    var strMat = new Material(Shader.Find("Standard"));
                    strMat.color = Color.white;
                    strMr.material = strMat;
                    DestroyImmediate(str.GetComponent<Collider>());
                }
            }
        }

        private void CreateFlags()
        {
            flags = new GameObject[2];
            for (int s = 0; s < 2; s++)
            {
                int side = s == 0 ? -1 : 1;
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.transform.SetParent(transform);
                pole.transform.localPosition = new Vector3(side * (halfWidth - 0.2f), roofY + 2f, roofZMin + 0.3f);
                pole.transform.localScale = new Vector3(0.05f, 4f, 0.05f);
                var mr = pole.GetComponent<MeshRenderer>();
                var mat = new Material(Shader.Find("Standard"));
                mat.color = Color.gray;
                mr.material = mat;

                // Flag texture would be created here
                var flag = GameObject.CreatePrimitive(PrimitiveType.Plane);
                flag.transform.SetParent(transform);
                flag.transform.localPosition = new Vector3(side * (halfWidth - 0.2f), roofY + 3.4f, roofZMin - 0.6f);
                flag.transform.localScale = new Vector3(1.8f, 1.2f, 1f);
                flag.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                var flagMr = flag.GetComponent<MeshRenderer>();
                var flagMat = new Material(Shader.Find("Standard"));
                flagMat.color = Color.white;
                flagMr.material = flagMat;
                flags[s] = flag;
            }
        }

        private void CreateTower()
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.08f, 0.08f, 0.08f);
            mat.smoothness = 0.5f;

            var coneMat = new Material(Shader.Find("Standard"));
            coneMat.color = new Color(0.2f, 0.2f, 0.2f);
            coneMat.metallic = 0.5f;
            coneMat.smoothness = 0.6f;
            coneMat.EnableKeyword("_EMISSION");
            coneMat.SetColor("_EmissionColor", new Color(0.49f, 0.3f, 1f, 0.4f));

            for (int k = 0; k < 3; k++)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.transform.SetParent(transform);
                box.transform.localPosition = new Vector3(0f, roofY + 0.55f + k * 1.1f, roofZMin + 0.8f);
                box.transform.localScale = new Vector3(halfWidth * 2f - 0.2f, 1.1f, 1.4f);
                var mr = box.GetComponent<MeshRenderer>();
                mr.material = mat;

                for (int x = 0; x < 3; x++)
                {
                    float xPos = new float[] { -0.9f, 0f, 0.9f }[x];
                    var speaker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    speaker.transform.SetParent(transform);
                    speaker.transform.localPosition = new Vector3(xPos, roofY + 0.55f + k * 1.1f, roofZMin + 0.1f);
                    speaker.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                    speaker.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    var spMr = speaker.GetComponent<MeshRenderer>();
                    spMr.material = coneMat;
                    DestroyImmediate(speaker.GetComponent<Collider>());
                }
            }
            roofZMin += 1.6f;
        }

        public void SetNight(bool isNight)
        {
            night = isNight;
            if (lasers != null) lasers.SetActive(isNight);
            if (headlightSpot != null) headlightSpot.intensity = isNight ? 60f : 0f;
        }

        public void Place(CityController city)
        {
            float f = Mathf.Sin(yaw), c = Mathf.Cos(yaw);
            float half = GameConstants.TruckLength * 0.42f;

            float yf = city.FloorY(x + f * half, z + c * half);
            float yb = city.FloorY(x - f * half, z - c * half);
            float yl = city.FloorY(x + c * 1.4f, z - f * 1.4f);
            float yr = city.FloorY(x - c * 1.4f, z + f * 1.4f);

            float ty = (yf + yb) * 0.5f;
            y += (ty - y) * (inited ? 0.35f : 1f);
            inited = true;

            pitch = Mathf.Atan2(yf - yb, half * 2f);
            roll = Mathf.Atan2(yl - yr, 2.8f) * 0.6f;

            Quaternion q = Quaternion.Euler(-pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, -roll * Mathf.Rad2Deg);
            transform.position = new Vector3(x, y, z);
            transform.rotation = q;

            // Update velocity
            worldVelocity = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * velocity;
        }

        public Vector3 LocalToWorld(Vector3 local)
        {
            return transform.TransformPoint(local);
        }

        public Vector3 WorldToLocal(Vector3 world)
        {
            return transform.InverseTransformPoint(world);
        }

        public void Drive(float dt, CityController city, DriveInput input)
        {
            Vector3 prevPos = transform.position;

            if (input.playerControlled)
            {
                float th = input.throttle;
                if (th > 0.05f) velocity += (velocity < 0f ? 9f : 3.2f) * th * dt;
                else if (th < -0.05f) velocity += (velocity > 0f ? 9f : 2.5f) * th * dt;
                else velocity *= Mathf.Max(0f, 1f - dt * 0.8f);

                velocity = Mathf.Clamp(velocity, -5f, 14f);

                steer += (input.steer * 0.55f - steer) * Mathf.Min(1f, dt * 4f);
                yaw -= velocity * Mathf.Tan(steer) / 9f * dt;

                route = null;
                routeLost = true;
            }
            else
            {
                // Autopilot
                float targetX = 0f, targetZ = 0f;

                if (routeLost && route == null)
                {
                    float d = Vector2.Distance(new Vector2(x, z), city.GetRingPoint(city.GetNearestRingS(x, z)));
                    if (d > 10f) route = city.GetRouteToRing(x, z);
                    else autoS = city.GetNearestRingS(x, z);
                    routeLost = false;
                }

                if (route != null && route.Count > 0)
                {
                    var wp = route[0];
                    targetX = wp.x; targetZ = wp.y;
                    if (Vector2.Distance(new Vector2(x, z), wp) < 6f)
                    {
                        route.RemoveAt(0);
                        if (route.Count == 0) { route = null; autoS = city.GetNearestRingS(x, z); }
                    }
                }
                else
                {
                    for (int k = 0; k < 4; k++)
                    {
                        var p = city.GetRingPoint(autoS + 2f);
                        if ((p.x - x) * Mathf.Sin(yaw) + (p.y - z) * Mathf.Cos(yaw) < 6f)
                        {
                            autoS += 2f; autoDist += 2f;
                        }
                    }
                    var lookahead = city.GetRingPoint(autoS + 9f);
                    targetX = lookahead.x; targetZ = lookahead.y;
                }

                float want = Mathf.Atan2(targetX - x, targetZ - z);
                float d = Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, want * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                yaw += Mathf.Clamp(d * 2f, -1f, 1f) * Mathf.Min(0.8f, 0.12f + Mathf.Abs(velocity) * 0.12f) * dt;

                float targetSpeed = Mathf.Abs(d) > 1f ? Mathf.Min(input.autoSpeed, 3f) : input.autoSpeed;
                velocity += Mathf.Clamp(targetSpeed - velocity, -6f * dt, 2.5f * dt);
            }

            x += Mathf.Sin(yaw) * velocity * dt;
            z += Mathf.Cos(yaw) * velocity * dt;

            // Collision detection (4 circles along truck)
            bumped = false;
            float f = Mathf.Sin(yaw), c = Mathf.Cos(yaw);
            foreach (float off in new float[] { -6.5f, -2.2f, 2.2f, 6.5f })
            {
                Vector3 checkPos = new Vector3(x + f * off, 0f, z + c * off);
                var hit = city.Collide(checkPos, 1.75f, y + 0.3f, input.playerControlled);
                if (hit != null)
                {
                    x += checkPos.x - hit.position.x;
                    z += checkPos.z - hit.position.z;
                    bumped = true;
                }
            }

            if (bumped && input.playerControlled && Mathf.Abs(velocity) > 3f)
            {
                velocity *= 0.4f;
            }

            // Boundaries
            x = Mathf.Clamp(x, -600f, 600f);
            z = Mathf.Clamp(z, -600f, 600f);

            worldVelocity = (transform.position - prevPos) / Mathf.Max(dt, 0.0001f);
            worldVelocity.y = 0f;

            Place(city);
        }

        public void UpdateEffects(float dt, float time, float beat, float energy, bool party)
        {
            if (!night) return;

            float h0 = variant.hue.Length > 0 ? variant.hue[0] : 0f;
            float hr = variant.hue.Length > 1 ? variant.hue[1] : 1f;

            // Update bulbs
            // This would update the instanced mesh colors

            // Update beams
            for (int i = 0; i < beams.Count; i++)
            {
                var b = beams[i];
                b.pivot.localRotation = Quaternion.Euler(
                    Mathf.Cos(time * 0.6f + i) * 0.45f * Mathf.Rad2Deg,
                    0f,
                    Mathf.Sin(time * 0.8f + i * 1.6f) * 0.55f * Mathf.Rad2Deg
                );
                float hue = (h0 + ((time * 0.05f + i * 0.25f) % 1f) * hr) % 1f;
                b.cone.GetComponent<MeshRenderer>().material.color = Color.HSVToRGB(hue, 1f, 0.55f);
                b.light.intensity = (0.32f + energy * 0.4f);
            }

            // Update lasers
            if (lasers != null)
            {
                for (int k = 0; k < lasers.transform.childCount; k++)
                {
                    var l = lasers.transform.GetChild(k);
                    l.localRotation = Quaternion.Euler(
                        (25f + Mathf.Sin(time * 0.9f + k) * 0.12f * Mathf.Rad2Deg),
                        ((k - 3) * 0.18f + Mathf.Sin(time * 1.3f) * 0.25f) * Mathf.Rad2Deg,
                        0f
                    );
                    var mr = l.GetComponent<MeshRenderer>();
                    mr.material.color = new Color(mr.material.color.r, mr.material.color.g, mr.material.color.b, 0.35f + energy * 0.6f);
                }
            }

            // Disco ball
            if (discoBall != null)
                discoBall.transform.Rotate(0f, dt * 1.5f * Mathf.Rad2Deg, 0f);

            // Spinning star
            if (spinStar != null)
                spinStar.transform.Rotate(0f, dt * 1.2f * Mathf.Rad2Deg, 0f);

            // Flags
            if (flags != null)
            {
                for (int i = 0; i < flags.Length; i++)
                {
                    if (flags[i] != null)
                        flags[i].transform.localRotation = Quaternion.Euler(Mathf.Sin(time * 6f + i) * 0.15f * Mathf.Rad2Deg, 0f, 0f);
                }
            }

            // Party lights
            for (int i = 0; i < partyLights.Length; i++)
            {
                partyLights[i].intensity = (4f + 22f * (0.45f + 0.55f * (i == Mathf.FloorToInt(beat) % 3 ? 1f : energy)));
            }

            // Headlight
            if (headlightSpot != null)
                headlightSpot.intensity = night ? 60f : 0f;
        }
    }

    [System.Serializable]
    public class LightBeam
    {
        public Transform pivot;
        public Transform cone;
        public Light light;
        public int index;
    }

    [System.Serializable]
    public class DriveInput
    {
        public bool playerControlled = false;
        public float throttle = 0f;
        public float steer = 0f;
        public float autoSpeed = 0f;
    }
}