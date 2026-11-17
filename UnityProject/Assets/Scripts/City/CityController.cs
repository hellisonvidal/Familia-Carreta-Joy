using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace FamiliaCarretaJoy
{
    /// <summary>
    /// City controller, ported from the City class in city.js.
    /// Handles city generation, terrain, buildings, collisions, and props.
    /// </summary>
    public class CityController : MonoBehaviour
    {
        [Header("City Definition")]
        public CityDefinition cityDef;
        public bool night = false;

        [Header("Generation")]
        public int seed = 0;
        public float terrainAmplitude = 12f;
        public float[] terrainPhases = new float[3];

        [Header("Dimensions")]
        public const int BlocksPerSide = 6;
        public const float BlockSize = 84f;
        public const float RoadHalfWidth = 7f;
        public const float SidewalkWidth = 3.5f;
        public const float HalfCitySize = BlocksPerSide * BlockSize / 2f;
        public const float RingRadius = HalfCitySize - 3.5f;

        [Header("Traffic Streets")]
        public static readonly int[] TrafficX = { 1, 3, 5 };
        public static readonly int[] TrafficZ = { 2, 4 };

        [Header("Generated Data")]
        public List<CityBlock> blocks = new List<CityBlock>();
        public List<ColliderData> colliders = new List<ColliderData>();
        public Dictionary<int, List<ColliderData>> spatialGrid = new Dictionary<int, List<ColliderData>>();
        public CityBlock startBlock;
        public Vector3 spawnPosition;
        public List<CityStop> stops = new List<CityStop>();
        public List<DanceZone> zones = new List<DanceZone>();

        // Sky
        public GameObject skySphere;
        public Light sunLight;
        public Light hemiLight;
        public GameObject skyObjects;
        public List<GameObject> clouds = new List<GameObject>();
        public GameObject waterPlane;

        // Terrain
        public GameObject terrainMesh;

        // Notes/Collectibles
        public List<CollectibleNote> notes = new List<CollectibleNote>();
        public GameObject noteMesh;
        public GameObject starMesh;

        // Components
        private System.Random rng;

        public void Initialize(CityDefinition def, bool isNight)
        {
            cityDef = def;
            night = isNight;
            seed = def.seed != 0 ? def.seed : Random.Range(0, 100000);
            rng = new System.Random(seed);

            terrainAmplitude = def.relevo;
            terrainPhases[0] = (float)(rng.NextDouble() * 6f);
            terrainPhases[1] = (float)(rng.NextDouble() * 6f);
            terrainPhases[2] = (float)(rng.NextDouble() * 6f);

            GenerateCity();
        }

        private void GenerateCity()
        {
            PlanBlocks();
            BuildSky();
            BuildTerrain();
            BuildStreets();
            BuildBlocks();
            BuildLamps();
            BuildTrees();
            BuildProps();
            BuildPlazas();
            BuildLandmarks();
            BuildMountains();
            BuildRing();
        }

        #region Block Planning

        private void PlanBlocks()
        {
            var blockTypes = new Dictionary<string, CityBlock>();

            // Define special blocks
            blockTypes["2,0"] = new CityBlock { type = "inicio", i = 2, j = 0 };
            blockTypes["5,3"] = new CityBlock { type = "praca", i = 5, j = 3, nome = "Praça Leste" };
            blockTypes["2,5"] = new CityBlock { type = "praca", i = 2, j = 5, nome = "Praça Norte" };
            blockTypes["1,2"] = new CityBlock { type = "parque", i = 1, j = 2 };
            blockTypes["3,3"] = new CityBlock { type = "parkour", i = 3, j = 3 };

            // City-specific landmarks
            var landmarks = GetLandmarksForCity(cityDef.id);
            foreach (var lm in landmarks)
            {
                blockTypes[$"{lm.i},{lm.j}"] = new CityBlock { type = "marco", i = lm.i, j = lm.j, marco = lm.marco };
            }

            // Create all blocks
            blocks.Clear();
            for (int i = 0; i < BlocksPerSide; i++)
            {
                for (int j = 0; j < BlocksPerSide; j++)
                {
                    string key = $"{i},{j}";
                    CityBlock block = blockTypes.ContainsKey(key) ? blockTypes[key] : new CityBlock { type = "normal", i = i, j = j };
                    block.x0 = LineC(i) + RoadHalfWidth;
                    block.x1 = LineC(i + 1) - RoadHalfWidth;
                    block.z0 = LineC(j) + RoadHalfWidth;
                    block.z1 = LineC(j + 1) - RoadHalfWidth;
                    block.cx = (block.x0 + block.x1) * 0.5f;
                    block.cz = (block.z0 + block.z1) * 0.5f;
                    blocks.Add(block);
                }
            }

            // Start position
            startBlock = blocks.First(b => b.type == "inicio");
            spawnPosition = new Vector3(startBlock.cx - 6f, 0f, startBlock.z0 + 6f);

            // Stops and dance zones
            stops.Clear();
            zones.Clear();
            foreach (var block in blocks)
            {
                if (block.type != "praca" && block.type != "inicio") continue;

                float sx, sz, nx = 0f, nz = 0f;
                if (block.j == 0) { sx = block.cx; sz = -RingRadius; nz = 1f; }
                else if (block.j == BlocksPerSide - 1) { sx = block.cx; sz = RingRadius; nz = -1f; }
                else if (block.i == BlocksPerSide - 1) { sx = RingRadius; sz = block.cz; nx = -1f; }
                else { sx = -RingRadius; sz = block.cz; nx = 1f; }

                float tx = -nz, tz = nx;
                float cx = sx + nx * 13f, cz = sz + nz * 13f;

                var spots = new List<Vector3>();
                var spotYaws = new List<float>();
                foreach (var rowData in new (int row, int count)[] { (0, 6), (1, 6) })
                {
                    for (int k = 0; k < rowData.count; k++)
                    {
                        float off = (k - (rowData.count - 1) / 2f) * 2.9f;
                        float x = cx + tx * off + nx * rowData.row * 3.4f;
                        float z = cz + tz * off + nz * rowData.row * 3.4f;
                        spots.Add(new Vector3(x, 0f, z));
                        spotYaws.Add(Mathf.Atan2(-nx, -nz));
                    }
                }

                // Reorder: front-center first
                int[] order = { 2, 3, 1, 4, 0, 5, 8, 9, 7, 10, 6, 11 };
                var orderedSpots = order.Select(i => new StopSpot
                {
                    position = spots[i],
                    yaw = spotYaws[i]
                }).ToArray();

                var stop = new CityStop
                {
                    position = new Vector3(sx, 0f, sz),
                    block = block,
                    nome = block.nome ?? "Praça Inicial",
                    inicio = block.type == "inicio",
                    normal = new Vector2(nx, nz),
                    tangent = new Vector2(tx, tz),
                    centerX = cx,
                    centerZ = cz,
                    spots = orderedSpots
                };
                stops.Add(stop);

                // Dance zone rectangle
                float hw = 10f, d0 = 6.5f, d1 = 21f;
                Vector3 p1 = new Vector3(sx + nx * d0 + tx * hw, 0f, sz + nz * d0 + tz * hw);
                Vector3 p2 = new Vector3(sx + nx * d1 - tx * hw, 0f, sz + nz * d1 - tz * hw);
                zones.Add(new DanceZone
                {
                    x0 = Mathf.Min(p1.x, p2.x), x1 = Mathf.Max(p1.x, p2.x),
                    z0 = Mathf.Min(p1.z, p2.z), z1 = Mathf.Max(p1.z, p2.z),
                    stop = stop
                });
            }
        }

        private (int i, int j, string marco)[] GetLandmarksForCity(string cityId)
        {
            switch (cityId)
            {
                case "sp": return new[] { (3, 2, "masp") };
                case "rio": return new (int, int, string)[0];
                case "bh": return new[] { (4, 1, "pampulha") };
                case "ssa": return new[] { (3, 1, "igreja") };
                case "cwb": return new[] { (4, 2, "estufa") };
                default: return new (int, int, string)[0];
            }
        }

        public float LineC(int k) => -HalfCitySize + k * BlockSize;

        public bool InCity(float x, float z) => Mathf.Abs(x) <= HalfCitySize + RoadHalfWidth && Mathf.Abs(z) <= HalfCitySize + RoadHalfWidth;

        public bool OnRoad(float x, float z)
        {
            if (!InCity(x, z)) return false;
            float fx = ((x + HalfCitySize) % BlockSize + BlockSize) % BlockSize;
            float fz = ((z + HalfCitySize) % BlockSize + BlockSize) % BlockSize;
            return fx < RoadHalfWidth || fx > BlockSize - RoadHalfWidth || fz < RoadHalfWidth || fz > BlockSize - RoadHalfWidth;
        }

        public float HeightAt(float x, float z)
        {
            float A = terrainAmplitude;
            float a = terrainPhases[0], b = terrainPhases[1], c = terrainPhases[2];
            float h = A * (0.55f * Mathf.Sin(x / 95f + a) * Mathf.Cos(z / 110f + b) + 0.45f * Mathf.Sin((x * 0.6f + z * 0.8f) / 130f + c));

            if (cityDef.mar != null)
            {
                float d = cityDef.mar.lado == "sul" ? -z - (HalfCitySize + RoadHalfWidth + 2f) : -x - (HalfCitySize + RoadHalfWidth + 2f);
                if (d > 0f)
                {
                    float t = Mathf.Clamp01(d / 60f);
                    float seaY = cityDef.mar != null ? -A - 2.5f : 0f;
                    h = h + (seaY - 1.5f - h) * t * t * (3f - 2f * t);
                }
            }
            return h;
        }

        public GroundInfo FloorY(float x, float z, float yHint = 0f)
        {
            float h = HeightAt(x, z);
            if (!InCity(x, z)) return new GroundInfo { height = h - 0.12f };

            if (OnRoad(x, z)) return new GroundInfo { height = h };

            return new GroundInfo { height = h + 0.2f };
        }

        public float TopAt(ColliderData collider, float x, float z)
        {
            if (collider.ramp == null) return collider.top;
            float t = collider.ramp.axis == 'x' ? (x - collider.x0) / (collider.x1 - collider.x0) : (z - collider.z0) / (collider.z1 - collider.z0);
            float k = Mathf.Clamp01(collider.ramp.dir > 0 ? t : 1f - t);
            return collider.baseHeight + (collider.top - collider.baseHeight) * k;
        }

        #endregion

        #region Sky & Lighting

        private void BuildSky()
        {
            // Sky sphere
            skySphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            skySphere.transform.SetParent(transform);
            skySphere.transform.localScale = Vector3.one * 2200f;
            var skyMr = skySphere.GetComponent<MeshRenderer>();
            var skyMat = new Material(Shader.Find("Skybox/6 Sided")); // Custom shader would be better
            var colors = night ? cityDef.ceuNoite : cityDef.ceuDia;
            skyMat.SetColor("_TopColor", HexToColor(colors[0]));
            skyMat.SetColor("_BottomColor", HexToColor(colors[1]));
            skyMr.material = skyMat;
            DestroyImmediate(skySphere.GetComponent<Collider>());

            // Fog
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = cityDef.nevoa * (night ? 1.1f : 1f);
            RenderSettings.fogColor = HexToColor(colors[1]);

            // Lights
            hemiLight = new GameObject("HemiLight").AddComponent<Light>();
            hemiLight.transform.SetParent(transform);
            hemiLight.type = LightType.Directional;
            hemiLight.color = night ? HexToColor("7080c0") : HexToColor("e8f2ff");
            hemiLight.intensity = night ? 1f : 1.9f;

            sunLight = new GameObject("SunLight").AddComponent<Light>();
            sunLight.transform.SetParent(transform);
            sunLight.type = LightType.Directional;
            sunLight.color = night ? HexToColor("a8b8ff") : HexToColor("fff3dc");
            sunLight.intensity = night ? 0.7f : 2.6f;
            sunLight.transform.position = new Vector3(-300f, 500f, -200f);
            sunLight.shadows = LightShadows.Soft;
            sunLight.shadowStrength = 1f;

            // Sky objects (sun/moon, stars)
            skyObjects = new GameObject("SkyObjects");
            skyObjects.transform.SetParent(transform);

            // Glow sprite for sun/moon
            var glowTex = CreateRadialTexture(night ? "220,230,255" : "255,240,200");
            var glowObj = new GameObject("SunMoon");
            glowObj.transform.SetParent(skyObjects.transform);
            var glowSr = glowObj.AddComponent<SpriteRenderer>();
            glowSr.sprite = Sprite.Create(glowTex, new Rect(0, 0, glowTex.width, glowTex.height), new Vector2(0.5f, 0.5f));
            glowSr.material = new Material(Shader.Find("Sprites/Default"));
            glowSr.material.SetFloat("_Mode", 3); // Additive
            glowObj.transform.localScale = Vector3.one * (night ? 160f : 360f);
            glowObj.transform.position = new Vector3(-700f, 900f, -500f).normalized * 1800f;

            if (!night)
            {
                // Clouds
                var cloudTex = CreateCloudTexture();
                for (int k = 0; k < 36; k++)
                {
                    float angle = (float)rng.NextDouble() * 6.28f;
                    float R = 500f + (float)rng.NextDouble() * 1000f;
                    var cloud = new GameObject($"Cloud_{k}");
                    cloud.transform.SetParent(skyObjects.transform);
                    var sr = cloud.AddComponent<SpriteRenderer>();
                    sr.sprite = Sprite.Create(cloudTex, new Rect(0, 0, cloudTex.width, cloudTex.height), new Vector2(0.5f, 0.5f));
                    sr.material = new Material(Shader.Find("Sprites/Default"));
                    sr.color = new Color(1f, 1f, 1f, 0.9f);
                    cloud.transform.position = new Vector3(Mathf.Cos(angle) * R, 280f + (float)rng.NextDouble() * 260f, Mathf.Sin(angle) * R);
                    cloud.transform.localScale = new Vector3(260f + (float)rng.NextDouble() * 260f, 110f + (float)rng.NextDouble() * 80f, 1f);
                    clouds.Add(cloud);
                }
            }
            else
            {
                // Moon
                var moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                moon.transform.SetParent(skyObjects.transform);
                moon.transform.position = glowObj.transform.position;
                moon.transform.LookAt(Vector3.zero);
                moon.transform.localScale = Vector3.one * 34f;
                var moonMr = moon.GetComponent<MeshRenderer>();
                moonMr.material = new Material(Shader.Find("Standard"));
                moonMr.material.color = HexToColor("f4f1e6");
                DestroyImmediate(moon.GetComponent<Collider>());

                // Stars
                var stars = new GameObject("Stars");
                stars.transform.SetParent(skyObjects.transform);
                var ps = stars.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.maxParticles = 1500;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1f);
                main.startSize = new ParticleSystem.MinMaxCurve(2f, 2f);
                main.startColor = Color.white;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var emit = ps.emission;
                emit.rateOverTime = 0f;
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 1800f;
                // Emit stars
                var particles = new ParticleSystem.Particle[1500];
                for (int i = 0; i < 1500; i++)
                {
                    float th = (float)rng.NextDouble() * 6.28f;
                    float ph = Mathf.Acos((float)rng.NextDouble() * 0.95f);
                    particles[i].position = new Vector3(Mathf.Sin(ph) * Mathf.Cos(th) * 1800f, Mathf.Cos(ph) * 1800f + 40f, Mathf.Sin(ph) * Mathf.Sin(th) * 1800f);
                    particles[i].startColor = Color.white;
                    particles[i].startSize = 2f;
                    particles[i].remainingLifetime = 1f;
                }
                ps.SetParticles(particles, particles.Length);
            }
        }

        #endregion

        #region Terrain

        private void BuildTerrain()
        {
            float S = HalfCitySize + 420f;
            int step = 8;
            int n = Mathf.RoundToInt(2f * S / step) + 1;

            var terrain = new GameObject("Terrain");
            terrain.transform.SetParent(transform);
            var mf = terrain.AddComponent<MeshFilter>();
            var mr = terrain.AddComponent<MeshRenderer>();

            var mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            Color baseColor = HexToColor(cityDef.chao);
            Color sandColor = HexToColor("e6d29a");

            for (int gz = 0; gz < n; gz++)
            {
                for (int gx = 0; gx < n; gx++)
                {
                    float x = -S + gx * step;
                    float z = -S + gz * step;
                    float y = HeightAt(x, z) - (InCity(x, z) ? 0.25f : 0.12f);

                    vertices.Add(new Vector3(x, y, z));
                    uvs.Add(new Vector2(x / 12f, z / 12f));

                    Color c = baseColor;
                    c.r += ((float)rng.NextDouble() - 0.5f) * 0.04f;
                    c.g += ((float)rng.NextDouble() - 0.5f) * 0.04f;
                    c.b += ((float)rng.NextDouble() - 0.5f) * 0.04f;

                    if (cityDef.mar != null && y < (cityDef.mar != null ? -terrainAmplitude - 2.5f : 0f) + 3f)
                        c = sandColor;

                    colors.Add(c);
                }
            }

            for (int gz = 0; gz < n - 1; gz++)
            {
                for (int gx = 0; gx < n - 1; gx++)
                {
                    int a = gz * n + gx;
                    int b = gz * n + gx + 1;
                    int c = (gz + 1) * n + gx;
                    int d = (gz + 1) * n + gx + 1;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }

            mesh.vertices = vertices.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.colors = colors.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();

            mf.mesh = mesh;
            var terrainMat = new Material(Shader.Find("Standard"));
            terrainMat.enableInstancing = false;
            mr.material = terrainMat;

            // Water
            if (cityDef.mar != null)
            {
                BuildWater();
            }

            terrainMesh = terrain;
        }

        private void BuildWater()
        {
            waterPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            waterPlane.transform.SetParent(transform);
            waterPlane.transform.localScale = new Vector3(800f, 800f, 1f);
            waterPlane.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            waterPlane.transform.position = new Vector3(0f, -terrainAmplitude - 2.5f, 0f);

            var mr = waterPlane.GetComponent<MeshRenderer>();
            var waterMat = new Material(Shader.Find("Standard"));
            waterMat.color = HexToColor(cityDef.mar.cor);
            waterMat.SetFloat("_Metallic", 0.15f);
            waterMat.SetFloat("_Glossiness", 0.88f);
            mr.material = waterMat;
            DestroyImmediate(waterPlane.GetComponent<Collider>());
        }

        #endregion

        #region Streets

        private void BuildStreets()
        {
            var streetParent = new GameObject("Streets");
            streetParent.transform.SetParent(transform);

            var asphaltMat = new Material(Shader.Find("Standard"));
            asphaltMat.color = new Color(0.3f, 0.3f, 0.3f);
            asphaltMat.SetFloat("_Glossiness", 0.12f);

            // Generate street meshes
            for (int k = 0; k <= BlocksPerSide; k++)
            {
                // X streets
                CreateStreetMesh(streetParent, 'x', LineC(k), asphaltMat);
                // Z streets
                CreateStreetMesh(streetParent, 'z', LineC(k), asphaltMat);
            }

            BuildCrosswalks(streetParent);
        }

        private void CreateStreetMesh(GameObject parent, char axis, float c, Material mat)
        {
            var mesh = new Mesh();
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            float from = -HalfCitySize - RoadHalfWidth;
            float to = HalfCitySize + RoadHalfWidth;
            int row = 0;

            for (float s = from; s <= to + 0.01f; s += 3f)
            {
                float ss = Mathf.Min(s, to);
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = axis == 'x' ? ss : c + side * RoadHalfWidth;
                    float z = axis == 'x' ? c + side * RoadHalfWidth : ss;
                    float y = HeightAt(x, z);
                    vertices.Add(new Vector3(x, y, z));
                    uvs.Add(new Vector2(side < 0 ? 0f : 1f, ss / 14f));
                }

                if (row > 0)
                {
                    int p = (row - 1) * 2;
                    if (axis == 'x')
                    {
                        triangles.Add(p); triangles.Add(p + 1); triangles.Add(p + 2);
                        triangles.Add(p + 1); triangles.Add(p + 3); triangles.Add(p + 2);
                    }
                    else
                    {
                        triangles.Add(p); triangles.Add(p + 2); triangles.Add(p + 1);
                        triangles.Add(p + 1); triangles.Add(p + 2); triangles.Add(p + 3);
                    }
                }
                row++;
            }

            mesh.vertices = vertices.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();

            var go = new GameObject($"Street_{axis}_{c}");
            go.transform.SetParent(parent);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.mesh = mesh;
            mr.material = mat;
        }

        private void BuildCrosswalks(GameObject parent)
        {
            var crosswalkMat = new Material(Shader.Find("Standard"));
            crosswalkMat.color = new Color(0.95f, 0.95f, 0.93f);
            crosswalkMat.SetFloat("_Glossiness", 0.15f);

            for (int ix = 0; ix <= BlocksPerSide; ix++)
            {
                for (int iz = 0; iz <= BlocksPerSide; iz++)
                {
                    float cx = LineC(ix), cz = LineC(iz);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        for (int k = -4; k <= 4; k += 2)
                        {
                            CreateCrosswalkStripe(parent, cx + k * 0.72f, cz + s * (RoadHalfWidth + 1.5f), 0.75f, 2.2f, crosswalkMat);
                            CreateCrosswalkStripe(parent, cx + s * (RoadHalfWidth + 1.5f), cz + k * 0.72f, 2.2f, 0.75f, crosswalkMat);
                        }
                    }
                }
            }
        }

        private void CreateCrosswalkStripe(GameObject parent, float cx, float cz, float w, float d, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.transform.SetParent(parent);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(w, d, 1f);
            float y = HeightAt(cx, cz) + 0.04f;
            go.transform.position = new Vector3(cx, y, cz);
            var mr = go.GetComponent<MeshRenderer>();
            mr.material = mat;
            DestroyImmediate(go.GetComponent<Collider>());
        }

        #endregion

        #region Blocks & Buildings

        private void BuildBlocks()
        {
            // Sidewalks, plazas, curbs, grass
            // This would generate the block geometry
            // For brevity, creating placeholder
            var blocksParent = new GameObject("Blocks");
            blocksParent.transform.SetParent(transform);

            foreach (var block in blocks)
            {
                // Create block ground
                var blockGround = GameObject.CreatePrimitive(PrimitiveType.Plane);
                blockGround.transform.SetParent(blocksParent.transform);
                blockGround.transform.position = new Vector3(block.cx, FloorY(block.cx, block.cz).height, block.cz);
                blockGround.transform.localScale = new Vector3((block.x1 - block.x0) / 10f, (block.z1 - block.z0) / 10f, 1f);
                var mr = blockGround.GetComponent<MeshRenderer>();
                mr.material = new Material(Shader.Find("Standard"));
                mr.material.color = block.type == "inicio" || block.type == "praca" ? new Color(0.72f, 0.7f, 0.66f) : new Color(0.85f, 0.83f, 0.8f);
                DestroyImmediate(blockGround.GetComponent<Collider>());
            }
        }

        #endregion

        #region Props & Collectibles

        private void BuildProps()
        {
            // Parkour props, notes, vehicles, etc.
            notes.Clear();

            // Notes on ring (inner sidewalk)
            for (float t = -HalfCitySize + 20f; t < HalfCitySize - 20f; t += 9f)
            {
                AddNote(t, -HalfCitySize + RoadHalfWidth + 1.6f);
                AddNote(t, HalfCitySize - RoadHalfWidth - 1.6f);
                AddNote(-HalfCitySize + RoadHalfWidth + 1.6f, t);
                AddNote(HalfCitySize - RoadHalfWidth - 1.6f, t);
            }

            // Parkour park
            var pk = blocks.FirstOrDefault(b => b.type == "parkour");
            if (pk != null)
            {
                // Add parkour structures
                AddNote(pk.cx + 14f, pk.cz + 18f, true); // Gold star on platform
            }
        }

        private void AddNote(float x, float z, bool gold = false)
        {
            float y = FloorY(x, z).height + 0.9f;
            notes.Add(new CollectibleNote
            {
                position = new Vector3(x, y, z),
                gold = gold,
                taken = false
            });
        }

        public void Collect(Vector3 pos, float centerY, System.Action<CollectibleNote> onTake)
        {
            foreach (var note in notes)
            {
                if (note.taken) continue;
                float dx = note.position.x - pos.x;
                float dz = note.position.z - pos.z;
                if (dx * dx + dz * dz > 1.3f || Mathf.Abs(note.position.y - centerY) > 1.35f) continue;
                note.taken = true;
                onTake?.Invoke(note);
            }
        }

        public void UpdateNotes(float time, float dt)
        {
            // Animate notes (bobbing, rotation)
            foreach (var note in notes)
            {
                float k = 1f;
                float y = note.position.y + Mathf.Sin(time * 3f + note.position.x) * 0.08f;
                if (note.taken)
                {
                    // Handle taken animation
                }
                // Update visual representation
            }
        }

        #endregion

        #region Collision System

        public void AddCollider(ColliderData collider)
        {
            collider.baseHeight = collider.baseHeight != 0f ? collider.baseHeight :
                Mathf.Min(FloorY(collider.x0, collider.z0).height, FloorY(collider.x1, collider.z0).height,
                         FloorY(collider.x0, collider.z1).height, FloorY(collider.x1, collider.z1).height);

            colliders.Add(collider);

            // Spatial grid
            int cellSize = 16;
            int margin = 2;
            for (int gx = Mathf.FloorToInt((collider.x0 - margin) / cellSize); gx <= Mathf.FloorToInt((collider.x1 + margin) / cellSize); gx++)
            {
                for (int gz = Mathf.FloorToInt((collider.z0 - margin) / cellSize); gz <= Mathf.FloorToInt((collider.z1 + margin) / cellSize); gz++)
                {
                    int key = gx * 1000 + gz;
                    if (!spatialGrid.ContainsKey(key)) spatialGrid[key] = new List<ColliderData>();
                    spatialGrid[key].Add(collider);
                }
            }
        }

        public List<ColliderData> Near(float x, float z)
        {
            int key = Mathf.FloorToInt(x / 16f) * 1000 + Mathf.FloorToInt(z / 16f);
            return spatialGrid.ContainsKey(key) ? spatialGrid[key] : new List<ColliderData>();
        }

        public CollideResult Collide(Vector3 pos, float radius, float feetY, HashSet<string> skipKinds = null)
        {
            foreach (var collider in Near(pos.x, pos.z))
            {
                if (collider.ramp != null || (skipKinds != null && skipKinds.Contains(collider.kind))) continue;

                float top = TopAt(collider, pos.x, pos.z);
                if (feetY >= top - 0.45f) continue;
                if (feetY + 1.6f < collider.baseHeight) continue;

                float cx = Mathf.Clamp(pos.x, collider.x0, collider.x1);
                float cz = Mathf.Clamp(pos.z, collider.z0, collider.z1);
                float dx = pos.x - cx, dz = pos.z - cz;
                float d2 = dx * dx + dz * dz;

                if (d2 >= radius * radius) continue;

                CollideResult result = new CollideResult { collider = collider };
                if (d2 > 1e-6f)
                {
                    float d = Mathf.Sqrt(d2);
                    float k = (radius - d) / d;
                    pos.x += dx * k; pos.z += dz * k;
                    result.normal = new Vector2(dx / d, dz / d);
                    result.position = pos;
                }
                else
                {
                    // Inside collider - push out nearest side
                    var opts = new (float dist, float nx, float nz)[]
                    {
                        (pos.x - collider.x0, -1f, 0f),
                        (collider.x1 - pos.x, 1f, 0f),
                        (pos.z - collider.z0, 0f, -1f),
                        (collider.z1 - pos.z, 0f, 1f)
                    };
                    System.Array.Sort(opts, (a, b) => a.dist.CompareTo(b.dist));
                    var best = opts[0];
                    pos.x += best.nx * (best.dist + radius);
                    pos.z += best.nz * (best.dist + radius);
                    result.normal = new Vector2(best.nx, best.nz);
                    result.position = pos;
                }
                return result;
            }
            return null;
        }

        public GroundInfo GroundAt(float x, float z, float yHint = 0f, float radius = 0.25f)
        {
            float h = FloorY(x, z).height;
            ColliderData topCollider = null;

            foreach (var c in Near(x, z))
            {
                if (!c.walk) continue;
                if (x < c.x0 - radius || x > c.x1 + radius || z < c.z0 - radius || z > c.z1 + radius) continue;
                float t = TopAt(c, x, z);
                if (yHint >= t - 0.5f && t > h)
                {
                    h = t;
                    topCollider = c;
                }
            }
            return new GroundInfo { height = h, top = topCollider };
        }

        #endregion

        #region Ring & Autopilot

        public Vector2 GetRingPoint(float s)
        {
            // Convert distance along ring to world position
            float perimeter = 8f * (HalfCitySize - 3.5f);
            s = s % perimeter;
            if (s < 0) s += perimeter;

            float sideLen = 2f * (HalfCitySize - 3.5f);
            int side = Mathf.FloorToInt(s / sideLen);
            float t = (s % sideLen) / sideLen;

            float r = RingRadius;
            switch (side)
            {
                case 0: return new Vector2(-r + t * 2f * r, -r); // Bottom, left to right
                case 1: return new Vector2(r, -r + t * 2f * r);  // Right, bottom to top
                case 2: return new Vector2(r - t * 2f * r, r);   // Top, right to left
                case 3: return new Vector2(-r, r - t * 2f * r);  // Left, top to bottom
            }
            return Vector2.zero;
        }

        public float GetNearestRingS(float x, float z)
        {
            float r = RingRadius;
            float minDist = float.MaxValue;
            float bestS = 0f;

            // Check each side
            for (int side = 0; side < 4; side++)
            {
                float sideLen = 2f * r;
                for (float t = 0f; t <= 1f; t += 0.01f)
                {
                    float s = side * sideLen + t * sideLen;
                    var p = GetRingPoint(s);
                    float d = Vector2.Distance(new Vector2(x, z), p);
                    if (d < minDist) { minDist = d; bestS = s; }
                }
            }
            return bestS;
        }

        public List<Vector2> GetRouteToRing(float x, float z)
        {
            // Simple A* or direct path to nearest ring point
            var target = GetRingPoint(GetNearestRingS(x, z));
            var route = new List<Vector2>();
            // Simplified: just add a few waypoints
            route.Add(new Vector2(x, z));
            route.Add(target);
            return route;
        }

        #endregion

        #region Helpers

        private void BuildLamps() { /* Street lamps */ }
        private void BuildTrees() { /* Trees */ }
        private void BuildPlazas() { /* Plaza centers, dance floors */ }
        private void BuildLandmarks() { /* City-specific landmarks */ }
        private void BuildMountains() { /* Background mountains */ }
        private void BuildRing() { /* Ring path for truck */ }

        private Texture2D CreateRadialTexture(string colorStr)
        {
            var parts = colorStr.Split(',');
            Color center = new Color(int.Parse(parts[0]) / 255f, int.Parse(parts[1]) / 255f, int.Parse(parts[2]) / 255f);
            var tex = new Texture2D(128, 128);
            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    float dx = (x - 64) / 64f;
                    float dy = (y - 64) / 64f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - dist);
                    tex.SetPixel(x, y, new Color(center.r, center.g, center.b, a));
                }
            }
            tex.Apply();
            return tex;
        }

        private Texture2D CreateCloudTexture()
        {
            var tex = new Texture2D(128, 128);
            var g = tex.GetPixels();
            for (int k = 0; k < 14; k++)
            {
                float gx = 30f + (float)rng.NextDouble() * 68f;
                float gy = 50f + (float)rng.NextDouble() * 30f;
                float gr = 18f + (float)rng.NextDouble() * 22f;
                for (int y = 0; y < 128; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        float dx = x - gx, dy = y - gy;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d < gr)
                        {
                            float a = 1f - d / gr;
                            int idx = y * 128 + x;
                            g[idx] = new Color(1f, 1f, 1f, a * 0.9f);
                        }
                    }
                }
            }
            tex.SetPixels(g);
            tex.Apply();
            return tex;
        }

        private Color HexToColor(string hex)
        {
            hex = hex.Replace("#", "");
            if (hex.Length == 6)
            {
                return new Color(
                    int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
                    int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
                    int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f
                );
            }
            else if (hex.Length == 3)
            {
                return new Color(
                    int.Parse(hex.Substring(0, 1), System.Globalization.NumberStyles.HexNumber) * 17 / 255f,
                    int.Parse(hex.Substring(1, 1), System.Globalization.NumberStyles.HexNumber) * 17 / 255f,
                    int.Parse(hex.Substring(2, 1), System.Globalization.NumberStyles.HexNumber) * 17 / 255f
                );
            }
            return Color.white;
        }

        private Color HexToColor(int hex)
        {
            return new Color(
                ((hex >> 16) & 0xFF) / 255f,
                ((hex >> 8) & 0xFF) / 255f,
                (hex & 0xFF) / 255f
            );
        }

        #endregion
    }

    // Supporting data classes
    [System.Serializable]
    public class CityBlock
    {
        public string type = "normal";
        public int i, j;
        public string nome;
        public string marco;
        public float x0, x1, z0, z1;
        public float cx, cz;
    }

    [System.Serializable]
    public class CityStop
    {
        public Vector3 position;
        public CityBlock block;
        public string nome;
        public bool inicio;
        public Vector2 normal;
        public Vector2 tangent;
        public float centerX, centerZ;
        public StopSpot[] spots;
        public bool announced = false;
    }

    [System.Serializable]
    public class StopSpot
    {
        public Vector3 position;
        public float yaw;
    }

    [System.Serializable]
    public class DanceZone
    {
        public float x0, x1, z0, z1;
        public CityStop stop;
    }

    [System.Serializable]
    public class ColliderData
    {
        public float x0, x1, z0, z1;
        public float top, baseHeight;
        public bool walk = true;
        public RampInfo ramp = null;
        public string kind = "predio";
        public bool truck = false;
    }

    [System.Serializable]
    public class RampInfo
    {
        public char axis;
        public int dir;
    }

    [System.Serializable]
    public class CollideResult
    {
        public ColliderData collider;
        public Vector2 normal;
        public Vector3 position;
    }

    [System.Serializable]
    public class GroundInfo
    {
        public float height;
        public ColliderData top;
    }

    [System.Serializable]
    public class CollectibleNote
    {
        public Vector3 position;
        public bool gold = false;
        public bool taken = false;
        public float takenTime = 0f;
        public bool hidden = false;
    }
}