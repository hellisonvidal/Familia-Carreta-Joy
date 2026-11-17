using UnityEngine;
using System.Collections.Generic;

namespace FamiliaCarretaJoy
{
    /// <summary>
    /// Input manager handling keyboard, mouse, touch, and gamepad input.
    /// Ported from the input handling in main.js.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        [Header("Joystick")]
        public RectTransform joystickZone;
        public RectTransform joystickBase;
        public RectTransform joystickKnob;
        public float joystickRadius = 100f;
        public float deadZone = 0.06f;

        [Header("Gamepad")]
        public bool useGamepad = true;
        public float gamepadDeadZone = 0.1f;

        [Header("Touch")]
        public bool useTouch = true;

        // State
        private Vector2 joystickInput = Vector2.zero;
        private int joystickPointerId = -1;
        private Vector2 joystickCenter = Vector2.zero;
        private bool joystickActive = false;

        private Vector2 cameraDrag = Vector2.zero;
        private bool cameraDragging = false;
        private int cameraDragPointerId = -1;

        private HashSet<string> keysPressed = new HashSet<string>();
        private Dictionary<string, bool> buttonHolds = new Dictionary<string, bool>();

        // Gamepad
        private GamepadState gamepad = new GamepadState();
        private bool gamepadNative = false;
        private double gamepadNativeTime = 0;

        public struct InputData
        {
            public float mx, mz, want;
        }

        private void Update()
        {
            UpdateJoystick();
            UpdateCameraDrag();
            UpdateKeyboard();
            UpdateGamepad();
        }

        #region Joystick (Touch/Mouse)

        private void UpdateJoystick()
        {
            if (joystickZone == null) return;

            // Touch input
            if (useTouch && Input.touchCount > 0)
            {
                foreach (Touch touch in Input.touches)
                {
                    if (joystickPointerId == -1 && RectTransformUtility.RectangleContainsScreenPoint(joystickZone, touch.position))
                    {
                        joystickPointerId = touch.fingerId;
                        joystickCenter = touch.position;
                        joystickActive = true;
                        UpdateJoystickVisual(touch.position);
                    }
                    else if (touch.fingerId == joystickPointerId)
                    {
                        if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                        {
                            joystickPointerId = -1;
                            joystickInput = Vector2.zero;
                            joystickActive = false;
                            ResetJoystickVisual();
                        }
                        else
                        {
                            UpdateJoystickVisual(touch.position);
                        }
                    }
                }
            }

            // Mouse input (for testing in editor)
            if (!useTouch || Application.isEditor)
            {
                if (Input.GetMouseButtonDown(0) && RectTransformUtility.RectangleContainsScreenPoint(joystickZone, Input.mousePosition))
                {
                    joystickPointerId = 0;
                    joystickCenter = Input.mousePosition;
                    joystickActive = true;
                    UpdateJoystickVisual(Input.mousePosition);
                }
                else if (Input.GetMouseButton(0) && joystickPointerId == 0)
                {
                    UpdateJoystickVisual(Input.mousePosition);
                }
                else if (Input.GetMouseButtonUp(0) && joystickPointerId == 0)
                {
                    joystickPointerId = -1;
                    joystickInput = Vector2.zero;
                    joystickActive = false;
                    ResetJoystickVisual();
                }
            }
        }

        private void UpdateJoystickVisual(Vector2 screenPos)
        {
            if (joystickBase == null || joystickKnob == null) return;

            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(joystickZone, screenPos, null, out localPos);

            float dx = localPos.x - joystickCenter.x;
            float dy = localPos.y - joystickCenter.y;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);

            if (dist > joystickRadius)
            {
                // Move base to follow finger
                joystickCenter += new Vector2(dx, dy) * (dist - joystickRadius) / dist;
                joystickBase.anchoredPosition = joystickCenter;
                dx = localPos.x - joystickCenter.x;
                dy = localPos.y - joystickCenter.y;
                dist = Mathf.Sqrt(dx * dx + dy * dy);
            }

            // Dead zone + response curve
            if (dist < deadZone * joystickRadius)
            {
                joystickInput = Vector2.zero;
            }
            else
            {
                float k = Mathf.Min(1f, (dist - deadZone * joystickRadius) / (joystickRadius * (1f - deadZone)));
                float output = Mathf.Pow(k, 0.92f) / (dist / joystickRadius);
                joystickInput = new Vector2(dx * output / joystickRadius, -dy * output / joystickRadius);
            }

            joystickKnob.anchoredPosition = new Vector2(dx / joystickRadius * 0.62f * joystickRadius, dy / joystickRadius * 0.62f * joystickRadius);
        }

        private void ResetJoystickVisual()
        {
            if (joystickKnob != null) joystickKnob.anchoredPosition = Vector2.zero;
            if (joystickBase != null) joystickBase.anchoredPosition = joystickCenter;
        }

        #endregion

        #region Camera Drag

        private void UpdateCameraDrag()
        {
            if (useTouch && Input.touchCount > 0)
            {
                foreach (Touch touch in Input.touches)
                {
                    if (!cameraDragging && touch.phase == TouchPhase.Began &&
                        !RectTransformUtility.RectangleContainsScreenPoint(joystickZone, touch.position))
                    {
                        cameraDragging = true;
                        cameraDragPointerId = touch.fingerId;
                        cameraDrag = touch.position;
                    }
                    else if (touch.fingerId == cameraDragPointerId)
                    {
                        if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                        {
                            cameraDragging = false;
                            cameraDragPointerId = -1;
                        }
                        else
                        {
                            Vector2 delta = touch.position - cameraDrag;
                            cameraDrag = touch.position;
                            GameState.Instance.camYaw -= delta.x * 0.008f;
                            GameState.Instance.camPitch = Mathf.Clamp(GameState.Instance.camPitch + delta.y * 0.005f, 0.08f, 1.1f);
                            GameState.Instance.camDragTime = Time.time;
                        }
                    }
                }
            }

            if (!useTouch || Application.isEditor)
            {
                if (Input.GetMouseButtonDown(1) || (Input.GetMouseButtonDown(0) && !RectTransformUtility.RectangleContainsScreenPoint(joystickZone, Input.mousePosition)))
                {
                    cameraDragging = true;
                    cameraDragPointerId = 0;
                    cameraDrag = Input.mousePosition;
                }
                else if (Input.GetMouseButton(0) && cameraDragPointerId == 0)
                {
                    Vector2 delta = (Vector2)Input.mousePosition - cameraDrag;
                    cameraDrag = Input.mousePosition;
                    GameState.Instance.camYaw -= delta.x * 0.008f;
                    GameState.Instance.camPitch = Mathf.Clamp(GameState.Instance.camPitch + delta.y * 0.005f, 0.08f, 1.1f);
                    GameState.Instance.camDragTime = Time.time;
                }
                else if (Input.GetMouseButtonUp(0) && cameraDragPointerId == 0)
                {
                    cameraDragging = false;
                    cameraDragPointerId = -1;
                }
            }
        }

        #endregion

        #region Keyboard

        private void UpdateKeyboard()
        {
            foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
            {
                string keyName = key.ToString().ToLower();
                if (Input.GetKeyDown(key)) keysPressed.Add(keyName);
                if (Input.GetKeyUp(key)) keysPressed.Remove(keyName);
            }
        }

        public bool IsKeyPressed(string key) => keysPressed.Contains(key.ToLower());

        #endregion

        #region Gamepad

        private void UpdateGamepad()
        {
            if (!useGamepad) return;

            // Check for native gamepad input (from Android app)
            if (gamepadNative && Time.realtimeSinceStartupAsDouble - gamepadNativeTime < 1.5)
            {
                // Use native input
                return;
            }

            // Unity Gamepad API
            var gamepads = UnityEngine.InputSystem.Gamepad.all;
            if (gamepads.Count == 0) return;

            var gp = gamepads[0]; // Use first gamepad

            // Left stick
            gamepad.lx = ApplyDeadZone(gp.leftStick.x.ReadValue(), gamepadDeadZone);
            gamepad.ly = ApplyDeadZone(gp.leftStick.y.ReadValue(), gamepadDeadZone);

            // Right stick
            gamepad.rx = ApplyDeadZone(gp.rightStick.x.ReadValue(), gamepadDeadZone);
            gamepad.ry = ApplyDeadZone(gp.rightStick.y.ReadValue(), gamepadDeadZone);

            // Triggers
            gamepad.lt = gp.leftTrigger.ReadValue();
            gamepad.rt = gp.rightTrigger.ReadValue();

            // Buttons
            UpdateGamepadButton("A", gp.buttonSouth.isPressed);
            UpdateGamepadButton("B", gp.buttonEast.isPressed);
            UpdateGamepadButton("X", gp.buttonWest.isPressed);
            UpdateGamepadButton("Y", gp.buttonNorth.isPressed);
            UpdateGamepadButton("LB", gp.leftShoulder.isPressed);
            UpdateGamepadButton("RB", gp.rightShoulder.isPressed);
            UpdateGamepadButton("LT", gp.leftTrigger.ReadValue() > 0.5f);
            UpdateGamepadButton("RT", gp.rightTrigger.ReadValue() > 0.5f);
            UpdateGamepadButton("SELECT", gp.selectButton.isPressed);
            UpdateGamepadButton("START", gp.startButton.isPressed);
            UpdateGamepadButton("L3", gp.leftStickButton.isPressed);
            UpdateGamepadButton("R3", gp.rightStickButton.isPressed);
            UpdateGamepadButton("UP", gp.dpad.up.isPressed);
            UpdateGamepadButton("DOWN", gp.dpad.down.isPressed);
            UpdateGamepadButton("LEFT", gp.dpad.left.isPressed);
            UpdateGamepadButton("RIGHT", gp.dpad.right.isPressed);
        }

        private float ApplyDeadZone(float value, float deadZone)
        {
            if (Mathf.Abs(value) < deadZone) return 0f;
            float sign = Mathf.Sign(value);
            return sign * (Mathf.Abs(value) - deadZone) / (1f - deadZone);
        }

        private void UpdateGamepadButton(string name, bool pressed)
        {
            bool wasPressed = buttonHolds.ContainsKey(name) && buttonHolds[name];
            buttonHolds[name] = pressed;

            if (pressed && !wasPressed)
            {
                OnGamepadButtonPress(name);
            }
        }

        private void OnGamepadButtonPress(string button)
        {
            // Map gamepad buttons to actions
            var actionMap = new Dictionary<string, string>
            {
                { "A", "jump" },
                { "B", "estrela" },
                { "X", "mortal" },
                { "Y", "flip" },
                { "RT", "pirueta" },
                { "RB", "dash" },
                { "LT", "slide" },
                { "LB", "ctx" },
                { "L3", "run" },
                { "R3", "camReset" },
                { "SELECT", "chars" },
                { "UP", "drive" },
                { "DOWN", "combo" },
                { "LEFT", "horn" },
                { "RIGHT", "party" }
            };

            if (actionMap.ContainsKey(button))
            {
                GameState.Instance?.OnGamepadAction(actionMap[button]);
            }
        }

        #endregion

        #region Public API

        public InputData GetCharacterInput()
        {
            float mx = joystickInput.x + gamepad.lx;
            float my = joystickInput.y - gamepad.ly; // Note: inverted Y

            // Keyboard overrides
            if (IsKeyPressed("a") || IsKeyPressed("leftarrow")) mx -= 1f;
            if (IsKeyPressed("d") || IsKeyPressed("rightarrow")) mx += 1f;
            if (IsKeyPressed("w") || IsKeyPressed("uparrow")) my += 1f;
            if (IsKeyPressed("s") || IsKeyPressed("downarrow")) my -= 1f;

            float mag = Mathf.Sqrt(mx * mx + my * my);
            if (mag > 1f) { mx /= mag; my /= mag; }

            return new InputData { mx = mx, mz = my, want = Mathf.Min(1f, mag) };
        }

        public float GetThrottle()
        {
            // Gamepad triggers
            float throttle = gamepad.rt - gamepad.lt;

            // Keyboard
            if (IsKeyPressed("uparrow") || IsKeyPressed("w")) throttle = 1f;
            else if (IsKeyPressed("downarrow") || IsKeyPressed("s")) throttle = -1f;

            // Button holds
            if (buttonHolds.ContainsKey("gas") && buttonHolds["gas"]) throttle = 1f;
            else if (buttonHolds.ContainsKey("brake") && buttonHolds["brake"]) throttle = -1f;

            return throttle;
        }

        public float GetSteer()
        {
            // Gamepad right stick X or left stick X when driving
            float steer = gamepad.rx;

            // Keyboard
            if (IsKeyPressed("leftarrow") || IsKeyPressed("a")) steer = -1f;
            else if (IsKeyPressed("rightarrow") || IsKeyPressed("d")) steer = 1f;

            return steer;
        }

        public void SetButtonHold(string button, bool held)
        {
            buttonHolds[button] = held;
        }

        public bool GetButtonHold(string button)
        {
            return buttonHolds.ContainsKey(button) && buttonHolds[button];
        }

        public void OnPointerDown(string action)
        {
            GameState.Instance?.OnGamepadAction(action);
        }

        #endregion

        [System.Serializable]
        public class GamepadState
        {
            public float lx, ly, rx, ry, lt, rt;
            public HashSet<string> down = new HashSet<string>();
            public bool native = false;
            public double nativeTime = 0;
            public string name = "";
        }
    }
}