using UnityEngine;

namespace FamiliaCarretaJoy
{
    /// <summary>
    /// Camera controller with smooth follow, collision avoidance, and gamepad support.
    /// Ported from the camera logic in main.js.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("References")]
        public Transform target;
        public GameState gameState;

        [Header("Settings")]
        public float distance = 12f;
        public float height = 5f;
        public float minDistance = 3f;
        public float maxDistance = 20f;
        public float heightOffset = 2f;
        public float lookAheadDistance = 3f;
        public float rotationSpeed = 5f;
        public float zoomSpeed = 10f;
        public float collisionRadius = 0.3f;

        [Header("Limits")]
        public float minPitch = 0.08f;
        public float maxPitch = 1.1f;
        public float minYaw = -180f;
        public float maxYaw = 180f;

        // State
        private float currentYaw = 0f;
        private float currentPitch = 0.32f;
        private float currentDistance = 12f;
        private float targetYaw = 0f;
        private float targetPitch = 0.32f;
        private float targetDistance = 12f;
        private float dragTime = -10f;
        private Vector3 lastTargetPosition;
        private bool isDragging = false;
        private int dragPointerId = -1;
        private Vector2 dragStartPos;

        private void LateUpdate()
        {
            if (gameState == null || gameState.player == null) return;

            UpdateInput();
            UpdateCameraPosition();
        }

        private void UpdateInput()
        {
            // Gamepad right stick
            var inputManager = GameManager.Instance?.inputManager;
            if (inputManager != null)
            {
                float rx = inputManager.gamepad.rx;
                float ry = inputManager.gamepad.ry;

                if (Mathf.Abs(rx) > 0.1f || Mathf.Abs(ry) > 0.1f)
                {
                    currentYaw -= rx * Time.deltaTime * 3.4f;
                    currentPitch = Mathf.Clamp(currentPitch + ry * Time.deltaTime * 1.9f, minPitch, maxPitch);
                    dragTime = Time.time;
                }
            }

            // Touch/Mouse drag for camera rotation
            if (Input.touchCount > 0)
            {
                foreach (Touch touch in Input.touches)
                {
                    if (!isDragging && touch.phase == TouchPhase.Began)
                    {
                        // Check if not over joystick or buttons
                        var uiManager = UIManager.Instance;
                        if (uiManager != null &&
                            !RectTransformUtility.RectangleContainsScreenPoint(uiManager.joystickZone, touch.position) &&
                            touch.position.x > Screen.width * 0.3f) // Right side of screen
                        {
                            isDragging = true;
                            dragPointerId = touch.fingerId;
                            dragStartPos = touch.position;
                        }
                    }
                    else if (touch.fingerId == dragPointerId)
                    {
                        if (touch.phase == TouchPhase.Moved)
                        {
                            Vector2 delta = touch.position - dragStartPos;
                            dragStartPos = touch.position;
                            currentYaw -= delta.x * 0.008f;
                            currentPitch = Mathf.Clamp(currentPitch + delta.y * 0.005f, minPitch, maxPitch);
                            dragTime = Time.time;
                        }
                        else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                        {
                            isDragging = false;
                            dragPointerId = -1;
                        }
                    }
                }
            }
            else if (Application.isEditor)
            {
                if (Input.GetMouseButtonDown(1) || (Input.GetMouseButtonDown(0) && Input.mousePosition.x > Screen.width * 0.3f))
                {
                    isDragging = true;
                    dragPointerId = 0;
                    dragStartPos = Input.mousePosition;
                }
                else if (Input.GetMouseButton(0) && isDragging && dragPointerId == 0)
                {
                    Vector2 delta = (Vector2)Input.mousePosition - dragStartPos;
                    dragStartPos = Input.mousePosition;
                    currentYaw -= delta.x * 0.008f;
                    currentPitch = Mathf.Clamp(currentPitch + delta.y * 0.005f, minPitch, maxPitch);
                    dragTime = Time.time;
                }
                else if (Input.GetMouseButtonUp(0) && isDragging && dragPointerId == 0)
                {
                    isDragging = false;
                    dragPointerId = -1;
                }
            }

            // Scroll wheel for zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                targetDistance = Mathf.Clamp(targetDistance - scroll * zoomSpeed, minDistance, maxDistance);
            }

            // Keyboard camera reset
            if (Input.GetKeyDown(KeyCode.K))
            {
                ResetCamera();
            }
        }

        private void UpdateCameraPosition()
        {
            var player = gameState.player;
            if (player == null) return;

            // Determine target position and rotation
            Vector3 targetPos;
            float targetYaw;

            if (player.mode == CharacterMode.Drive)
            {
                // Behind truck
                targetPos = gameState.truck.transform.position;
                targetYaw = gameState.truck.yaw;
            }
            else if (player.frame == CharacterFrame.Truck)
            {
                // On truck roof
                targetPos = player.transform.position;
                targetYaw = player.yaw + gameState.truck.yaw;
            }
            else
            {
                // In world
                targetPos = player.transform.position;
                targetYaw = player.yaw;
            }

            // Smooth yaw interpolation
            float yawDiff = Mathf.DeltaAngle(currentYaw, targetYaw);
            if (Time.time - dragTime > 2f && Mathf.Abs(yawDiff) > 0.1f)
            {
                currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, Time.deltaTime * rotationSpeed);
            }

            // Calculate desired camera position
            Vector3 offset = Quaternion.Euler(currentPitch * Mathf.Rad2Deg, currentYaw * Mathf.Rad2Deg, 0f) * new Vector3(0f, heightOffset, -currentDistance);
            Vector3 desiredPos = targetPos + offset;

            // Collision detection
            RaycastHit hit;
            if (Physics.SphereCast(targetPos, collisionRadius, (desiredPos - targetPos).normalized, out hit, currentDistance, ~LayerMask.GetMask("CharacterLayer")))
            {
                desiredPos = hit.point + hit.normal * collisionRadius;
            }

            // Smooth position
            transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * 10f);

            // Look at target with slight offset
            Vector3 lookPos = targetPos + Vector3.up * 1.5f;
            if (player.mode == CharacterMode.Drive)
            {
                lookPos += gameState.truck.transform.forward * lookAheadDistance;
            }
            else if (player.velocity.magnitude > 1f)
            {
                lookPos += player.transform.forward * lookAheadDistance;
            }

            transform.LookAt(lookPos);

            // Update game state camera position for lighting/shadows
            gameState.camPos = transform.position;
        }

        public void ResetCamera()
        {
            var player = gameState?.player;
            if (player == null) return;

            float yaw;
            if (player.mode == CharacterMode.Drive)
                yaw = gameState.truck.yaw;
            else if (player.frame == CharacterFrame.Truck)
                yaw = player.yaw + gameState.truck.yaw;
            else
                yaw = player.yaw;

            currentYaw = yaw + Mathf.PI;
            currentPitch = 0.32f;
            targetDistance = distance;
            dragTime = Time.time - 10f;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private void OnDrawGizmosSelected()
        {
            if (target != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(target.position, collisionRadius);
            }
        }
    }
}