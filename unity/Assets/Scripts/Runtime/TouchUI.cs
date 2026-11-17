using System.Collections.Generic;
using UnityEngine;

namespace CarretaJoy
{
    /// <summary>
    /// Controles de tela (multitoque): joystick que aparece onde o dedo encosta, botões grandes
    /// e arrastar no lado direito gira a câmera. No computador: WASD/setas, espaço, Q, E, R,
    /// Shift corre, F sobe/desce, G dança, Tab troca, N dia/noite.
    /// </summary>
    public class TouchUI : MonoBehaviour
    {
        public class Btn
        {
            public string id, label; public Rect r; public bool round; public Color color; public bool visible = true; public bool toggled;
            public bool pressed, held; public int finger = -1;
        }

        public Vector2 Move;               // -1..1
        public Vector2 CamDelta;           // graus neste quadro
        public readonly Dictionary<string, Btn> B = new Dictionary<string, Btn>();
        public string Toast; float toastT;
        public string Score = "0", Info = "", Who = "";
        public float Fps;

        int joyFinger = -1, camFinger = -1; Vector2 joyOrigin, joyPos, camLast;
        Vector2 mouseLast; bool mouseCam;
        Texture2D circle, rrect;
        GUIStyle label, big, small;
        float u;

        void Awake()
        {
            circle = MakeCircle(128);
            rrect = MakeRoundRect(64, 18);
            Add("pular", "PULAR", true, new Color(0.15f, 0.75f, 0.35f));
            Add("mortal", "MORTAL", true, new Color(0.95f, 0.35f, 0.25f));
            Add("flip", "FLIP", true, new Color(0.25f, 0.55f, 0.95f));
            Add("pirueta", "PIRUETA", true, new Color(0.75f, 0.35f, 0.95f));
            Add("correr", "CORRER", true, new Color(1f, 0.6f, 0.1f));
            Add("subir", "SUBIR", false, new Color(0.1f, 0.65f, 0.75f));
            Add("dancar", "DANÇAR", false, new Color(0.95f, 0.25f, 0.6f));
            Add("trocar", "TROCAR", false, new Color(0.2f, 0.2f, 0.3f));
            Add("dia", "NOITE", false, new Color(0.2f, 0.2f, 0.45f));
            Add("som", "SOM", false, new Color(0.2f, 0.2f, 0.3f));
        }

        void Add(string id, string l, bool round, Color c) { B[id] = new Btn { id = id, label = l, round = round, color = c }; }

        public bool Pressed(string id) => B.TryGetValue(id, out var b) && b.pressed;
        public void ShowToast(string s) { Toast = s; toastT = 1.6f; }

        void Layout()
        {
            float W = Screen.width, H = Screen.height;
            u = Mathf.Min(W, H) / 100f;
            float s = 1f;
            B["pular"].r = new Rect(W - 30 * u, H - 30 * u, 24 * u * s, 24 * u * s);
            B["mortal"].r = new Rect(W - 52 * u, H - 22 * u, 17 * u, 17 * u);
            B["flip"].r = new Rect(W - 50 * u, H - 43 * u, 17 * u, 17 * u);
            B["pirueta"].r = new Rect(W - 30 * u, H - 52 * u, 17 * u, 17 * u);
            B["correr"].r = new Rect(W - 21 * u, H - 72 * u, 16 * u, 16 * u);
            B["subir"].r = new Rect(W / 2 + 2 * u, H - 15 * u, 26 * u, 11 * u);
            B["dancar"].r = new Rect(W / 2 - 28 * u, H - 15 * u, 26 * u, 11 * u);
            B["trocar"].r = new Rect(3 * u, 13 * u, 30 * u, 9 * u);
            B["dia"].r = new Rect(W - 64 * u, 3 * u, 22 * u, 9 * u);
            B["som"].r = new Rect(W - 40 * u, 3 * u, 16 * u, 9 * u);
        }

        Btn HitButton(Vector2 p)
        {
            foreach (var b in B.Values)
            {
                if (!b.visible) continue;
                var r = b.r;
                if (b.round) { if ((p - r.center).magnitude < r.width * 0.62f) return b; }
                else if (new Rect(r.x - u, r.y - u, r.width + 2 * u, r.height + 2 * u).Contains(p)) return b;
            }
            return null;
        }

        void Update()
        {
            Layout();
            foreach (var b in B.Values) b.pressed = false;
            CamDelta = Vector2.zero;
            Move = Vector2.zero;
            float W = Screen.width, H = Screen.height;

            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                var p = new Vector2(t.position.x, H - t.position.y); // coordenadas da tela (de cima)
                if (t.phase == TouchPhase.Began)
                {
                    var b = HitButton(p);
                    if (b != null) { b.pressed = true; b.held = true; b.finger = t.fingerId; Buzz(); }
                    else if (p.x < W * 0.45f && p.y > H * 0.25f && joyFinger < 0) { joyFinger = t.fingerId; joyOrigin = joyPos = p; }
                    else if (camFinger < 0) { camFinger = t.fingerId; camLast = p; }
                }
                else if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                {
                    if (t.fingerId == joyFinger) joyPos = p;
                    else if (t.fingerId == camFinger) { var d = p - camLast; camLast = p; CamDelta += new Vector2(d.x, d.y) * (0.22f * 1000f / Mathf.Max(600, H)); }
                }
                else
                {
                    if (t.fingerId == joyFinger) joyFinger = -1;
                    if (t.fingerId == camFinger) camFinger = -1;
                    foreach (var b in B.Values) if (b.finger == t.fingerId) { b.held = false; b.finger = -1; }
                }
            }
            if (joyFinger >= 0)
            {
                float R = 13 * u;
                var d = joyPos - joyOrigin;
                if (d.magnitude > R) { joyOrigin = joyPos - d.normalized * R; d = joyPos - joyOrigin; } // a base segue o dedo
                Move = new Vector2(d.x, -d.y) / R;
                float m = Move.magnitude; if (m < 0.12f) Move = Vector2.zero; else Move *= Mathf.Clamp01((m - 0.12f) / 0.88f) / m;
            }

            // teclado e mouse (para testar no computador)
            if (Input.touchCount == 0)
            {
                Vector2 k = new Vector2((Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0),
                                        (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0));
                if (k != Vector2.zero) Move = k.normalized;
                if (Input.GetKeyDown(KeyCode.Space)) B["pular"].pressed = true;
                if (Input.GetKeyDown(KeyCode.Q)) B["mortal"].pressed = true;
                if (Input.GetKeyDown(KeyCode.E)) B["flip"].pressed = true;
                if (Input.GetKeyDown(KeyCode.R)) B["pirueta"].pressed = true;
                if (Input.GetKeyDown(KeyCode.LeftShift)) B["correr"].pressed = true;
                if (Input.GetKeyDown(KeyCode.F)) B["subir"].pressed = true;
                if (Input.GetKeyDown(KeyCode.G)) B["dancar"].pressed = true;
                if (Input.GetKeyDown(KeyCode.Tab)) B["trocar"].pressed = true;
                if (Input.GetKeyDown(KeyCode.N)) B["dia"].pressed = true;
                var mp = new Vector2(Input.mousePosition.x, H - Input.mousePosition.y);
                if (Input.GetMouseButtonDown(0)) { var b = HitButton(mp); if (b != null) b.pressed = true; else { mouseCam = true; mouseLast = mp; } }
                if (Input.GetMouseButtonUp(0)) mouseCam = false;
                if (mouseCam && Input.GetMouseButton(0)) { var d = mp - mouseLast; mouseLast = mp; CamDelta += d * 0.25f; }
            }
            if (toastT > 0) toastT -= Time.unscaledDeltaTime;
        }

        public bool Dragging => camFinger >= 0 || mouseCam;

        void Buzz()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // vibração curtinha não existe na API simples; Handheld.Vibrate é longo demais, então fica sem.
#endif
        }

        // ---------------- desenho ----------------
        void OnGUI()
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
                big = new GUIStyle(label); small = new GUIStyle(label) { alignment = TextAnchor.UpperLeft, fontStyle = FontStyle.Normal };
            }
            label.fontSize = Mathf.RoundToInt(3.4f * u); big.fontSize = Mathf.RoundToInt(7f * u); small.fontSize = Mathf.RoundToInt(2.6f * u);

            // botões
            foreach (var b in B.Values)
            {
                if (!b.visible) continue;
                var c = b.color; c.a = b.held ? 0.95f : 0.72f;
                if (b.toggled) c = Color.Lerp(c, Color.white, 0.35f);
                GUI.color = new Color(0, 0, 0, 0.25f);
                GUI.DrawTexture(new Rect(b.r.x + 0.5f * u, b.r.y + 0.7f * u, b.r.width, b.r.height), b.round ? circle : rrect);
                GUI.color = c;
                if (b.round) GUI.DrawTexture(b.r, circle);
                else GUI.DrawTexture(b.r, rrect, ScaleMode.StretchToFill, true, 0, c, 0, 2.2f * u);
                GUI.color = Color.white;
                Shadowed(b.r, b.label, label);
            }

            // joystick
            if (joyFinger >= 0)
            {
                float R = 13 * u;
                GUI.color = new Color(1, 1, 1, 0.25f);
                GUI.DrawTexture(new Rect(joyOrigin.x - R, joyOrigin.y - R, 2 * R, 2 * R), circle);
                GUI.color = new Color(1, 1, 1, 0.75f);
                var k = joyOrigin + new Vector2(Move.x, -Move.y) * R;
                GUI.DrawTexture(new Rect(k.x - 5 * u, k.y - 5 * u, 10 * u, 10 * u), circle);
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = new Color(1, 1, 1, 0.12f);
                float R = 11 * u; var o = new Vector2(20 * u, Screen.height - 22 * u);
                GUI.DrawTexture(new Rect(o.x - R, o.y - R, 2 * R, 2 * R), circle);
                GUI.color = Color.white;
            }

            // placar e informações
            Shadowed(new Rect(3 * u, 2 * u, 40 * u, 10 * u), Score, big, TextAnchor.MiddleLeft);
            Shadowed(new Rect(Screen.width / 2 - 40 * u, 2 * u, 80 * u, 6 * u), Info, label);
            Shadowed(new Rect(Screen.width - 22 * u, 13 * u, 20 * u, 4 * u), Mathf.RoundToInt(Fps) + " FPS", small);
            if (toastT > 0)
            {
                var c = GUI.color; GUI.color = new Color(1, 1, 1, Mathf.Clamp01(toastT * 2));
                Shadowed(new Rect(0, Screen.height * 0.28f, Screen.width, 10 * u), Toast, big);
                GUI.color = c;
            }
        }

        static void Shadowed(Rect r, string s, GUIStyle st, TextAnchor? a = null)
        {
            var old = st.alignment; if (a.HasValue) st.alignment = a.Value;
            var c = st.normal.textColor;
            st.normal.textColor = new Color(0, 0, 0, 0.6f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), s, st);
            st.normal.textColor = c;
            GUI.Label(r, s, st);
            st.alignment = old;
        }

        static Texture2D MakeCircle(int s)
        {
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float R = s / 2f - 1;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = new Vector2(x - s / 2f + 0.5f, y - s / 2f + 0.5f).magnitude;
                    float a = Mathf.Clamp01(R - d);
                    float rim = Mathf.Clamp01(1 - Mathf.Abs(d - (R - 3)) / 2.5f) * 0.35f;
                    t.SetPixel(x, y, new Color(1, 1, 1, a) + new Color(rim, rim, rim, 0));
                }
            t.Apply();
            return t;
        }

        static Texture2D MakeRoundRect(int s, int rad)
        {
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, rad, s - rad), cy = Mathf.Clamp(y + 0.5f, rad, s - rad);
                    float d = new Vector2(x + 0.5f - cx, y + 0.5f - cy).magnitude;
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(rad - d)));
                }
            t.Apply();
            return t;
        }
    }
}
