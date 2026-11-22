using UnityEngine;

namespace CarretaJoy
{
    /// <summary>
    /// Controle de videogame (Xbox, PlayStation, genéricos) pelo Input Manager clássico.
    /// O Builder cria os eixos "PadAxis1".."PadAxis12" (todos os controles).
    /// Botões no padrão Xbox (PlayStation equivalente): A/✕ 0, B/○ 1, X/▢ 2, Y/△ 3, LB/L1 4, RB/R1 5.
    /// </summary>
    public static class Pad
    {
        static int frame = -1;
        public static Vector2 Left, Right, DPad;
        public static float LT, RT;
        public static bool Connected;
        static Vector2 prevD;
        public static bool DUp, DDown, DLeft, DRight;   // apertou neste quadro
        static float checkT;

        static bool Android => Application.platform == RuntimePlatform.Android;

        static float Ax(int i)
        {
            try { return Input.GetAxisRaw("PadAxis" + i); } catch { return 0f; }
        }

        static Vector2 Dead(Vector2 v, float d = 0.2f)
        {
            float m = v.magnitude;
            if (m < d) return Vector2.zero;
            return v / m * Mathf.Clamp01((m - d) / (1 - d));
        }

        public static void Update()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            if (Time.unscaledTime > checkT)
            {
                checkT = Time.unscaledTime + 2f;
                Connected = false;
                foreach (var n in Input.GetJoystickNames()) if (!string.IsNullOrEmpty(n)) Connected = true;
            }
            if (!Connected) { Left = Right = DPad = Vector2.zero; LT = RT = 0; DUp = DDown = DLeft = DRight = false; return; }
            Left = Dead(new Vector2(Ax(1), -Ax(2)));
            if (Android)
            {
                Right = Dead(new Vector2(Ax(3), -Ax(4)));
                DPad = new Vector2(Ax(5), -Ax(6));
                RT = Mathf.Max(0, Mathf.Max(Ax(12), Ax(8))); LT = Mathf.Max(0, Mathf.Max(Ax(13), Ax(7)));
            }
            else
            {
                Right = Dead(new Vector2(Ax(4), -Ax(5)));
                DPad = new Vector2(Ax(6), Ax(7));
                LT = Mathf.Max(0, Ax(9)); RT = Mathf.Max(0, Ax(10));
            }
            DUp = DPad.y > 0.5f && prevD.y <= 0.5f; DDown = DPad.y < -0.5f && prevD.y >= -0.5f;
            DRight = DPad.x > 0.5f && prevD.x <= 0.5f; DLeft = DPad.x < -0.5f && prevD.x >= -0.5f;
            prevD = DPad;
        }

        public static bool Down(int b) => Connected && Input.GetKeyDown(KeyCode.JoystickButton0 + b);
        public static bool Held(int b) => Connected && Input.GetKey(KeyCode.JoystickButton0 + b);
        public static bool Start => Down(7) && !Android || Down(10) && Android;
        public static bool Select => Down(6) && !Android || Down(11);
        public const int A = 0, B = 1, X = 2, Y = 3, LB = 4, RB = 5, L3W = 8, L3A = 8;
    }
}
