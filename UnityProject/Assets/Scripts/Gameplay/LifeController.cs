using UnityEngine;
using System.Collections.Generic;

namespace FamiliaCarretaJoy
{
    /// <summary>
    /// Life and effects system - pedestrians, cars, particles, confetti, fireworks.
    /// Ported from the Life class in life.js.
    /// </summary>
    public class LifeController : MonoBehaviour
    {
        [Header("References")]
        public CityController city;

        [Header("Prefabs")]
        public GameObject pedestrianPrefab;
        public GameObject carPrefab;
        public GameObject pigeonPrefab;
        public GameObject ballPrefab;
        public GameObject confettiParticlePrefab;
        public GameObject fireworkParticlePrefab;
        public GameObject fountainParticlePrefab;

        // Pools
        private List<Pedestrian> pedestrians = new List<Pedestrian>();
        private List<AICar> cars = new List<AICar>();
        private List<Pigeon> pigeons = new List<Pigeon>();
        private List<GameObject> balls = new List<GameObject>();
        private Queue<GameObject> confettiPool = new Queue<GameObject>();
        private Queue<GameObject> fireworkPool = new Queue<GameObject>();

        // State
        private float fireworkTimer = 0f;
        private bool night;

        public void Initialize(CityController cityController, bool isNight)
        {
            city = cityController;
            night = isNight;
            SpawnInitialLife();
        }

        private void SpawnInitialLife()
        {
            // Spawn cars on traffic streets
            SpawnCars();

            // Spawn pedestrians on sidewalks
            SpawnPedestrians();

            // Spawn pigeons in plazas
            SpawnPigeons();

            // Spawn balls in parks
            SpawnBalls();

            // Fountain in park
            CreateFountain();
        }

        private void SpawnCars()
        {
            var trafficStreets = GetTrafficStreets();
            foreach (var street in trafficStreets)
            {
                // Parked cars
                for (int i = 0; i < 2; i++)
                {
                    float t = 0.3f + i * 0.32f;
                    var pos = GetPositionOnStreet(street, t, 5.2f);
                    CreateCar(pos, street.axis);
                }
            }
        }

        private void SpawnPedestrians()
        {
            foreach (var block in city.blocks)
            {
                if (block.type == "praca" || block.type == "inicio")
                {
                    for (int i = 0; i < 10; i++)
                    {
                        float x = Random.Range(block.x0 + 6f, block.x1 - 6f);
                        float z = Random.Range(block.z0 + 6f, block.z1 - 6f);
                        if (Vector2.Distance(new Vector2(x, z), new Vector2(block.cx, block.cz)) < 22f) continue;
                        CreatePedestrian(new Vector3(x, city.FloorY(x, z).height, z));
                    }
                }
            }
        }

        private void SpawnPigeons()
        {
            foreach (var block in city.blocks)
            {
                if (block.type == "praca")
                {
                    for (int i = 0; i < 8; i++)
                    {
                        float x = Random.Range(block.x0 + 6f, block.x1 - 6f);
                        float z = Random.Range(block.z0 + 6f, block.z1 - 6f);
                        CreatePigeon(new Vector3(x, city.FloorY(x, z).height + 2f, z));
                    }
                }
            }
        }

        private void SpawnBalls()
        {
            var park = city.blocks.FirstOrDefault(b => b.type == "parque");
            if (park != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    float x = Random.Range(park.x0 + 6f, park.x1 - 6f);
                    float z = Random.Range(park.z0 + 6f, park.z1 - 6f);
                    CreateBall(new Vector3(x, city.FloorY(x, z).height + 0.5f, z));
                }
            }
        }

        private void CreateFountain()
        {
            var park = city.blocks.FirstOrDefault(b => b.type == "parque");
            if (park != null && fountainParticlePrefab != null)
            {
                var fountain = Instantiate(fountainParticlePrefab, new Vector3(park.cx, city.FloorY(park.cx, park.cz).height + 1f, park.cz), Quaternion.identity);
                fountain.transform.SetParent(transform);
            }
        }

        private List<(char axis, float c)> GetTrafficStreets()
        {
            var streets = new List<(char, float)>();
            foreach (int k in CityController.TrafficX)
            {
                streets.Add(('x', CityController.LineC(k)));
            }
            foreach (int k in CityController.TrafficZ)
            {
                streets.Add(('z', CityController.LineC(k)));
            }
            return streets;
        }

        private Vector3 GetPositionOnStreet((char axis, float c) street, float t, float lateral)
        {
            float from = -CityController.HalfCitySize - CityController.RoadHalfWidth;
            float to = CityController.HalfCitySize + CityController.RoadHalfWidth;
            float pos = from + t * (to - from);

            if (street.axis == 'x')
                return new Vector3(pos, city.FloorY(pos, street.c + lateral).height, street.c + lateral);
            else
                return new Vector3(street.c + lateral, city.FloorY(street.c + lateral, pos).height, pos);
        }

        private void CreateCar(Vector3 position, char axis)
        {
            if (carPrefab == null) return;

            var car = Instantiate(carPrefab, position, Quaternion.identity);
            car.transform.SetParent(transform);
            car.transform.rotation = axis == 'x' ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;

            var aiCar = car.GetComponent<AICar>();
            if (aiCar == null) aiCar = car.AddComponent<AICar>();
            aiCar.Initialize(city, axis == 'x');

            cars.Add(aiCar);
        }

        private void CreatePedestrian(Vector3 position)
        {
            if (pedestrianPrefab == null) return;

            var ped = Instantiate(pedestrianPrefab, position, Quaternion.Euler(0f, Random.value * 360f, 0f));
            ped.transform.SetParent(transform);

            var ai = ped.GetComponent<Pedestrian>();
            if (ai == null) ai = ped.AddComponent<Pedestrian>();
            ai.Initialize(city);

            pedestrians.Add(ai);
        }

        private void CreatePigeon(Vector3 position)
        {
            if (pigeonPrefab == null) return;

            var pigeon = Instantiate(pigeonPrefab, position, Quaternion.identity);
            pigeon.transform.SetParent(transform);

            var ai = pigeon.GetComponent<Pigeon>();
            if (ai == null) ai = pigeon.AddComponent<Pigeon>();
            ai.Initialize();

            pigeons.Add(ai);
        }

        private void CreateBall(Vector3 position)
        {
            if (ballPrefab == null) return;

            var ball = Instantiate(ballPrefab, position, Quaternion.identity);
            ball.transform.SetParent(transform);
            ball.GetComponent<Rigidbody>().mass = 0.5f;
            balls.Add(ball);
        }

        public void UpdateLife(float dt)
        {
            // Update cars
            foreach (var car in cars)
            {
                car.UpdateAI(dt);
            }

            // Update pedestrians
            foreach (var ped in pedestrians)
            {
                ped.UpdateAI(dt);
            }

            // Update pigeons
            foreach (var pigeon in pigeons)
            {
                pigeon.UpdateAI(dt);
            }

            // Fireworks at night during parties
            if (night && GameManager.Instance?.gameState?.dancing == true)
            {
                fireworkTimer += dt;
                if (fireworkTimer > 15f)
                {
                    fireworkTimer = 0f;
                    LaunchFirework();
                }
            }
        }

        public void PushActor(CharacterController actor)
        {
            // Cars honk and stop if actor in front
            foreach (var car in cars)
            {
                car.CheckActor(actor);
            }
        }

        public void Confetti(Vector3 position)
        {
            if (confettiParticlePrefab == null) return;

            GameObject confetti;
            if (confettiPool.Count > 0)
            {
                confetti = confettiPool.Dequeue();
                confetti.SetActive(true);
            }
            else
            {
                confetti = Instantiate(confettiParticlePrefab);
            }

            confetti.transform.position = position;
            var ps = confetti.GetComponent<ParticleSystem>();
            if (ps != null) ps.Play();

            StartCoroutine(ReturnConfettiToPool(confetti, 3f));
        }

        private IEnumerator ReturnConfettiToPool(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            obj.SetActive(false);
            confettiPool.Enqueue(obj);
        }

        private void LaunchFirework()
        {
            if (fireworkParticlePrefab == null) return;

            GameObject firework;
            if (fireworkPool.Count > 0)
            {
                firework = fireworkPool.Dequeue();
                firework.SetActive(true);
            }
            else
            {
                firework = Instantiate(fireworkParticlePrefab);
            }

            // Random position in sky
            float x = Random.Range(-CityController.HalfCitySize, CityController.HalfCitySize);
            float z = Random.Range(-CityController.HalfCitySize, CityController.HalfCitySize);
            float y = 100f + Random.value * 50f;

            firework.transform.position = new Vector3(x, y, z);
            var ps = firework.GetComponent<ParticleSystem>();
            if (ps != null) ps.Play();

            StartCoroutine(ReturnFireworkToPool(firework, 5f));
        }

        private IEnumerator ReturnFireworkToPool(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            obj.SetActive(false);
            fireworkPool.Enqueue(obj);
        }

        public void Dispose()
        {
            // Clean up
            foreach (var car in cars) if (car != null) Destroy(car.gameObject);
            foreach (var ped in pedestrians) if (ped != null) Destroy(ped.gameObject);
            foreach (var pigeon in pigeons) if (pigeon != null) Destroy(pigeon.gameObject);
            foreach (var ball in balls) if (ball != null) Destroy(ball);
            foreach (var confetti in confettiPool) if (confetti != null) Destroy(confetti);
            foreach (var firework in fireworkPool) if (firework != null) Destroy(firework);

            cars.Clear();
            pedestrians.Clear();
            pigeons.Clear();
            balls.Clear();
            confettiPool.Clear();
            fireworkPool.Clear();
        }
    }

    /// <summary>
    /// AI Car behavior
    /// </summary>
    public class AICar : MonoBehaviour
    {
        private CityController city;
        private bool drivingOnX;
        private float speed = 0f;
        private float targetSpeed = 8f;
        private float direction = 1f; // 1 or -1
        private float laneOffset = 0f;

        public void Initialize(CityController cityController, bool onX)
        {
            city = cityController;
            drivingOnX = onX;
            direction = Random.value > 0.5f ? 1f : -1f;
            laneOffset = Random.Range(-1.5f, 1.5f);
        }

        public void UpdateAI(float dt)
        {
            speed = Mathf.MoveTowards(speed, targetSpeed, dt * 4f);

            Vector3 pos = transform.position;
            if (drivingOnX)
            {
                pos.x += direction * speed * dt;
                pos.z += laneOffset * dt; // Slight lane variation
            }
            else
            {
                pos.z += direction * speed * dt;
                pos.x += laneOffset * dt;
            }

            pos.y = city.FloorY(pos.x, pos.z).height + 0.5f;
            transform.position = pos;

            // Check bounds and reverse
            if (drivingOnX)
            {
                if ((direction > 0 && pos.x > CityController.HalfCitySize + 10f) ||
                    (direction < 0 && pos.x < -CityController.HalfCitySize - 10f))
                {
                    direction *= -1f;
                    transform.rotation = Quaternion.Euler(0f, direction > 0 ? 90f : -90f, 0f);
                }
            }
            else
            {
                if ((direction > 0 && pos.z > CityController.HalfCitySize + 10f) ||
                    (direction < 0 && pos.z < -CityController.HalfCitySize - 10f))
                {
                    direction *= -1f;
                    transform.rotation = direction > 0 ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
                }
            }
        }

        public void CheckActor(CharacterController actor)
        {
            float dist = Vector3.Distance(transform.position, actor.transform.position);
            if (dist < 5f)
            {
                // Honk and brake
                targetSpeed = 0f;
                GameManager.Instance?.musicController?.PlaySFX("horn");
            }
            else if (dist > 10f)
            {
                targetSpeed = 8f;
            }
        }
    }

    /// <summary>
    /// Pedestrian AI
    /// </summary>
    public class Pedestrian : MonoBehaviour
    {
        private CityController city;
        private Vector3 target;
        private float speed = 1.5f;
        private float waitTimer = 0f;
        private float stateTimer = 0f;
        private int state = 0; // 0 = walking, 1 = waiting

        public void Initialize(CityController cityController)
        {
            city = cityController;
            PickNewTarget();
        }

        public void UpdateAI(float dt)
        {
            if (state == 0) // Walking
            {
                Vector3 dir = (target - transform.position).normalized;
                transform.position += dir * speed * dt;
                transform.position = new Vector3(transform.position.x, city.FloorY(transform.position.x, transform.position.z).height, transform.position.z);

                if (Vector3.Distance(transform.position, target) < 1f)
                {
                    state = 1;
                    waitTimer = Random.Range(2f, 8f);
                }

                // Look at truck if nearby
                var truck = GameManager.Instance?.truck;
                if (truck != null)
                {
                    float dist = Vector3.Distance(transform.position, new Vector3(truck.x, truck.y, truck.z));
                    if (dist < 15f)
                    {
                        transform.LookAt(new Vector3(truck.x, transform.position.y, truck.z));
                    }
                }
            }
            else // Waiting
            {
                waitTimer -= dt;
                if (waitTimer <= 0f)
                {
                    state = 0;
                    PickNewTarget();
                }
            }
        }

        private void PickNewTarget()
        {
            // Find a random point on sidewalk
            for (int i = 0; i < 10; i++)
            {
                float x = Random.Range(-CityController.HalfCitySize, CityController.HalfCitySize);
                float z = Random.Range(-CityController.HalfCitySize, CityController.HalfCitySize);

                if (city.OnRoad(x, z)) continue; // Not on road

                target = new Vector3(x, city.FloorY(x, z).height, z);
                transform.LookAt(target);
                return;
            }
        }
    }

    /// <summary>
    /// Pigeon AI
    /// </summary>
    public class Pigeon : MonoBehaviour
    {
        private Vector3 velocity;
        private Vector3 target;
        private float stateTimer = 0f;
        private int state = 0; // 0 = flying, 1 = perched
        private float flyHeight = 10f;

        public void Initialize()
        {
            PickNewTarget();
        }

        public void UpdateAI(float dt)
        {
            if (state == 0) // Flying
            {
                Vector3 toTarget = target - transform.position;
                float dist = toTarget.magnitude;

                if (dist < 2f)
                {
                    // Land
                    state = 1;
                    stateTimer = Random.Range(3f, 10f);
                    velocity = Vector3.zero;
                    return;
                }

                // Flocking behavior (simplified)
                Vector3 dir = toTarget.normalized;
                velocity = Vector3.Lerp(velocity, dir * 8f, dt * 2f);
                velocity.y = Mathf.Lerp(velocity.y, (flyHeight - transform.position.y) * 0.5f, dt);

                transform.position += velocity * dt;

                // Face velocity
                if (velocity.magnitude > 0.1f)
                    transform.rotation = Quaternion.LookRotation(velocity.normalized);
            }
            else // Perched
            {
                stateTimer -= dt;
                if (stateTimer <= 0f)
                {
                    state = 0;
                    PickNewTarget();
                }
            }

            // Fly away from players
            var players = GameManager.Instance?.allCharacters;
            if (players != null)
            {
                foreach (var p in players)
                {
                    float dist = Vector3.Distance(transform.position, p.transform.position);
                    if (dist < 5f && state == 1)
                    {
                        state = 0;
                        PickNewTarget();
                        break;
                    }
                }
            }
        }

        private void PickNewTarget()
        {
            float x = Random.Range(-CityController.HalfCitySize, CityController.HalfCitySize);
            float z = Random.Range(-CityController.HalfCitySize, CityController.HalfCitySize);
            target = new Vector3(x, flyHeight + Random.value * 10f, z);
        }
    }
}