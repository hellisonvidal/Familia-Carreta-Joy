using UnityEngine;
using System;

namespace FamiliaCarretaJoy
{
    /// <summary>
    /// Pose definitions ported from the original POSES object in characters.js.
    /// </summary>
    public static class PoseDefinitions
    {
        public static PoseState Lerp(PoseState a, PoseState b, float t)
        {
            if (a == null) return b?.Clone();
            if (b == null) return a.Clone();

            var result = new PoseState
            {
                lean = Mathf.Lerp(a.lean, b.lean, t),
                twist = Mathf.Lerp(a.twist, b.twist, t),
                sway = Mathf.Lerp(a.sway, b.sway, t),
                bob = Mathf.Lerp(a.bob, b.bob, t),
                spineTwist = Mathf.Lerp(a.spineTwist, b.spineTwist, t),
                spineSway = Mathf.Lerp(a.spineSway, b.spineSway, t)
            };

            for (int i = 0; i < 4; i++)
            {
                float av = i < a.sh.Length ? a.sh[i] : 0f;
                float bv = i < b.sh.Length ? b.sh[i] : 0f;
                result.sh[i] = Mathf.Lerp(av, bv, t);
            }
            for (int i = 0; i < 2; i++)
            {
                float av = i < a.el.Length ? a.el[i] : 0f;
                float bv = i < b.el.Length ? b.el[i] : 0f;
                result.el[i] = Mathf.Lerp(av, bv, t);
            }
            for (int i = 0; i < 4; i++)
            {
                float av = i < a.th.Length ? a.th[i] : 0f;
                float bv = i < b.th.Length ? b.th[i] : 0f;
                result.th[i] = Mathf.Lerp(av, bv, t);
            }
            for (int i = 0; i < 2; i++)
            {
                float av = i < a.kn.Length ? a.kn[i] : 0f;
                float bv = i < b.kn.Length ? b.kn[i] : 0f;
                result.kn[i] = Mathf.Lerp(av, bv, t);
            }
            for (int i = 0; i < 3; i++)
            {
                float av = i < a.head.Length ? a.head[i] : 0f;
                float bv = i < b.head.Length ? b.head[i] : 0f;
                result.head[i] = Mathf.Lerp(av, bv, t);
            }

            return result;
        }

        public static PoseState Idle(float time, float seed = 0f)
        {
            float b = Mathf.Sin(time * 2.2f) * 0.025f;
            float w = Mathf.Sin(time * 0.45f + seed);
            float look = Mathf.Sin(time * 0.31f + seed * 2f) * 0.35f + Mathf.Sin(time * 0.83f + seed) * 0.12f;

            return new PoseState
            {
                lean = 0.02f + b * 0.4f,
                sway = w * 0.05f,
                spineSway = -w * 0.03f,
                bob = -Mathf.Abs(w) * 0.01f,
                sh = new float[4] { b, 0.12f + b, -b, -0.12f - b },
                el = new float[2] { -0.22f, -0.22f },
                th = new float[4] { 0f, 0.03f + w * 0.05f, 0f, -0.03f + w * 0.05f },
                kn = new float[2] { w > 0 ? 0.02f : 0.12f * -w, w < 0 ? 0.02f : 0.12f * w },
                head = new float[3] { Mathf.Sin(time * 0.5f + seed) * 0.05f, look, 0f }
            };
        }

        public static PoseState Run(float phase, float amount, float height)
        {
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            return new PoseState
            {
                lean = 0.18f * amount,
                bob = Mathf.Abs(s) * 0.04f * amount * height,
                twist = s * 0.15f * amount,
                sh = new float[4] { s * 1.0f * amount, 0.12f, -s * 1.0f * amount, -0.12f },
                el = new float[2] { -0.9f - 0.4f * amount, -0.9f - 0.4f * amount },
                th = new float[4] { -s * 0.95f * amount, 0f, s * 0.95f * amount, 0f },
                kn = new float[2] { Mathf.Max(0f, c) * 1.5f * amount + 0.1f, Mathf.Max(0f, -c) * 1.5f * amount + 0.1f },
                head = new float[3] { -0.1f * amount, 0f, 0f }
            };
        }

        public static PoseState Walk(float phase, float amount)
        {
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            return new PoseState
            {
                lean = 0.05f * amount,
                bob = Mathf.Abs(c) * 0.02f * amount,
                twist = s * 0.08f * amount,
                sway = s * 0.03f * amount,
                sh = new float[4] { s * 0.45f * amount, 0.1f, -s * 0.45f * amount, -0.1f },
                el = new float[2] { -0.3f - Mathf.Max(0f, s) * 0.3f, -0.3f - Mathf.Max(0f, -s) * 0.3f },
                th = new float[4] { -s * 0.45f * amount, 0.02f, s * 0.45f * amount, -0.02f },
                kn = new float[2] { Mathf.Max(0f, c) * 0.7f * amount + 0.05f, Mathf.Max(0f, -c) * 0.7f * amount + 0.05f },
                head = new float[3] { 0f, -s * 0.05f, 0f }
            };
        }

        public static PoseState Land()
        {
            return new PoseState
            {
                lean = 0.3f,
                bob = -0.18f,
                sh = new float[4] { -0.5f, 0.35f, -0.5f, -0.35f },
                el = new float[2] { -0.9f, -0.9f },
                th = new float[4] { -0.9f, 0.12f, -0.9f, -0.12f },
                kn = new float[2] { 1.5f, 1.5f },
                head = new float[3] { -0.15f, 0f, 0f }
            };
        }

        public static PoseState Air(float vy, float tuck)
        {
            int up = vy > 0 ? 1 : 0;
            var p = new PoseState
            {
                lean = 0.1f,
                sh = new float[4] { -0.4f - up * 0.6f, 0.7f, -0.4f - up * 0.6f, -0.7f },
                el = new float[2] { -0.6f, -0.6f },
                th = new float[4] { -0.7f, 0.05f, -0.2f, -0.05f },
                kn = new float[2] { 1.2f, 0.6f },
                head = new float[3] { 0f, 0f, 0f }
            };

            if (tuck > 0f)
            {
                var tucked = new PoseState
                {
                    lean = 0.5f,
                    sh = new float[4] { -1.2f, 0.3f, -1.2f, -0.3f },
                    el = new float[2] { -2.0f, -2.0f },
                    th = new float[4] { -2.0f, 0.1f, -2.0f, -0.1f },
                    kn = new float[2] { 2.4f, 2.4f },
                    head = new float[3] { 0.3f, 0f, 0f }
                };
                return Lerp(p, tucked, tuck);
            }
            return p;
        }

        public static PoseState Star(float k = 0.5f)
        {
            if (k < 0.18f)
            {
                float t = k / 0.18f;
                return new PoseState
                {
                    lean = 0.14f * t,
                    sh = new float[4] { -0.08f * t, 3.0f * t, -0.08f * t, -3.0f * t },
                    el = new float[2] { -0.05f * t, -0.05f * t },
                    th = new float[4] { -0.25f * t, 0.42f * t, 0.05f * t, -0.42f * t },
                    kn = new float[2] { 0.35f * t, 0.15f * t },
                    head = new float[3] { -0.2f * t, 0f, 0f }
                };
            }
            if (k > 0.78f)
            {
                float t = (k - 0.78f) / 0.22f;
                return new PoseState
                {
                    lean = 0.12f * t,
                    bob = -0.05f * Mathf.Sin(t * Mathf.PI),
                    sh = new float[4] { -0.08f * t, 3.04f - 1.9f * t, -0.08f * t, -3.04f + 1.9f * t },
                    el = new float[2] { -0.35f * t, -0.35f * t },
                    th = new float[4] { -0.3f * t, 0.62f * (1f - t), -0.2f * t, -0.62f * (1f - t) },
                    kn = new float[2] { 0.5f * t, 0.4f * t },
                    head = new float[3] { -0.2f * (1f - t), 0f, 0f }
                };
            }
            return new PoseState
            {
                lean = -0.03f,
                sh = new float[4] { -0.04f, 3.04f, -0.04f, -3.04f },
                el = new float[2] { -0.02f, -0.02f },
                th = new float[4] { 0f, 0.62f, 0f, -0.62f },
                kn = new float[2] { 0.03f, 0.03f },
                head = new float[3] { -0.28f, 0f, 0f }
            };
        }

        public static PoseState Climb(float t)
        {
            float s = Mathf.Sin(t * 9f);
            return new PoseState
            {
                lean = -0.1f,
                sh = new float[4] { -2.6f + s * 0.4f, 0.2f, -2.6f - s * 0.4f, -0.2f },
                el = new float[2] { -0.4f, -0.4f },
                th = new float[4] { -0.9f - s * 0.5f, 0.1f, -0.9f + s * 0.5f, -0.1f },
                kn = new float[2] { 1.4f, 1.4f },
                head = new float[3] { -0.2f, 0f, 0f }
            };
        }

        public static PoseState CrouchStart()
        {
            return new PoseState
            {
                lean = 0.55f,
                bob = -0.36f,
                twist = 0.25f,
                sway = 0.08f,
                sh = new float[4] { -0.9f, 0.35f, 0.6f, -2.5f },
                el = new float[2] { 0f, -0.4f },
                th = new float[4] { -1.9f, 0.25f, -0.2f, -0.95f },
                kn = new float[2] { 2.3f, 1.7f },
                head = new float[3] { -0.45f, -0.2f, 0.15f }
            };
        }

        public static PoseState Sit()
        {
            return new PoseState
            {
                lean = -0.1f,
                sh = new float[4] { -0.5f, 0.1f, -0.5f, -0.1f },
                el = new float[2] { -0.8f, -0.8f },
                th = new float[4] { -1.5f, 0.1f, -1.5f, -0.1f },
                kn = new float[2] { 1.5f, 1.5f },
                head = new float[3] { 0f, 0f, 0f }
            };
        }

        public static PoseState Crouch()
        {
            return new PoseState
            {
                lean = 0.35f,
                bob = -0.28f,
                twist = 0.12f,
                sh = new float[4] { -0.7f, 0.35f, -0.5f, -0.35f },
                el = new float[2] { -1.1f, -1.1f },
                th = new float[4] { -1.35f, 0.16f, -1.05f, -0.16f },
                kn = new float[2] { 1.9f, 1.55f },
                head = new float[3] { -0.28f, 0f, 0f }
            };
        }

        public static PoseState Slide()
        {
            return new PoseState
            {
                lean = -0.78f,
                bob = -0.46f,
                twist = 0.28f,
                sway = -0.1f,
                sh = new float[4] { 0.35f, 0.65f, -0.85f, -0.45f },
                el = new float[2] { -0.7f, -1.1f },
                th = new float[4] { -1.35f, 0.1f, -0.35f, -0.18f },
                kn = new float[2] { 0.15f, 1.85f },
                head = new float[3] { 0.52f, -0.15f, 0f }
            };
        }

        public static PoseState Dance(float beat, int style, string move, float mk)
        {
            float ph = beat * Mathf.PI;
            float s = Mathf.Sin(ph);
            float s2 = Mathf.Sin(ph * 2f);
            float b = Mathf.Abs(Mathf.Sin(ph));

            PoseState p = null;

            switch (style % 6)
            {
                case 0:
                    p = new PoseState
                    {
                        bob = -b * 0.06f,
                        sway = s * 0.12f,
                        sh = new float[4] { -0.4f + s * 0.5f, 0.4f, -0.4f - s * 0.5f, -0.4f },
                        el = new float[2] { -1.4f, -1.4f },
                        th = new float[4] { 0f, 0.1f, 0f, -0.1f },
                        kn = new float[2] { b * 0.4f, b * 0.4f },
                        head = new float[3] { 0f, s * 0.3f, 0f }
                    };
                    break;
                case 1:
                    p = new PoseState
                    {
                        bob = -b * 0.08f,
                        twist = s * 0.3f,
                        sh = new float[4] { -2.6f * (s > 0 ? 1f : 0.5f), 0.5f, -2.6f * (s > 0 ? 0.5f : 1f), -0.5f },
                        el = new float[2] { -0.3f, -0.3f },
                        th = new float[4] { -0.1f, 0.15f, -0.1f, -0.15f },
                        kn = new float[2] { b * 0.5f, b * 0.5f },
                        head = new float[3] { -0.2f, 0f, 0f }
                    };
                    break;
                case 2:
                    p = new PoseState
                    {
                        bob = -b * 0.05f,
                        sway = s2 * 0.18f,
                        spineSway = -s2 * 0.15f,
                        sh = new float[4] { 0f, 1.2f + s2 * 0.6f, 0f, -1.2f + s2 * 0.6f },
                        el = new float[2] { -0.5f, -0.5f },
                        th = new float[4] { 0f, 0.1f, 0f, -0.1f },
                        kn = new float[2] { 0.3f, 0.3f },
                        head = new float[3] { 0f, 0f, s2 * 0.2f }
                    };
                    break;
                case 3:
                    p = new PoseState
                    {
                        bob = -b * 0.1f,
                        lean = 0.2f,
                        sh = new float[4] { -1.0f + s * 0.8f, 0.3f, -1.0f - s * 0.8f, -0.3f },
                        el = new float[2] { -1.6f, -1.6f },
                        th = new float[4] { -0.3f * b, 0.2f, -0.3f * (1f - b), -0.2f },
                        kn = new float[2] { 0.6f * b, 0.6f * (1f - b) },
                        head = new float[3] { 0.1f, 0f, 0f }
                    };
                    break;
                case 4:
                    p = new PoseState
                    {
                        bob = -b * 0.07f,
                        twist = s2 * 0.4f,
                        sh = new float[4] { -1.6f, 1.0f, -0.3f, -0.3f },
                        el = new float[2] { -0.6f + s * 0.6f, -1.2f },
                        th = new float[4] { 0f, 0.1f, 0f, -0.1f },
                        kn = new float[2] { 0.3f, 0.3f },
                        head = new float[3] { 0f, -s2 * 0.3f, 0f }
                    };
                    break;
                default:
                    p = new PoseState
                    {
                        bob = -b * 0.12f,
                        sh = new float[4] { -3.0f, 0.3f + s * 0.3f, -3.0f, -0.3f + s * 0.3f },
                        el = new float[2] { -0.2f, -0.2f },
                        th = new float[4] { 0f, 0.2f, 0f, -0.2f },
                        kn = new float[2] { b * 0.6f, b * 0.6f },
                        head = new float[3] { -0.3f, 0f, 0f }
                    };
                    break;
            }

            if (!string.IsNullOrEmpty(move))
            {
                float k = Mathf.Sin(mk * Mathf.PI);
                PoseState m = null;

                switch (move)
                {
                    case "esq":
                        m = new PoseState { sway = 0.3f, sh = new float[4] { 0f, 2.0f, 0f, -0.3f }, el = new float[2] { -0.3f, -1.0f }, th = new float[4] { 0f, 0.5f, 0f, -0.1f }, kn = new float[2] { 0.2f, 0.4f } };
                        break;
                    case "cima":
                        m = new PoseState { sh = new float[4] { -3.0f, 0.2f, -3.0f, -0.2f }, el = new float[2] { 0f, 0f }, th = new float[4] { -0.5f, 0f, -0.5f, 0f }, kn = new float[2] { 1.0f, 1.0f } };
                        break;
                    case "baixo":
                        m = new PoseState { bob = -0.25f, lean = 0.25f, sh = new float[4] { -0.8f, 0.6f, -0.8f, -0.6f }, el = new float[2] { -1.2f, -1.2f }, th = new float[4] { -1.2f, 0.4f, -1.2f, -0.4f }, kn = new float[2] { 1.9f, 1.9f } };
                        break;
                    default:
                        m = Star();
                        break;
                }

                if (m.head == null) m.head = new float[3] { 0f, 0f, 0f };
                p = Lerp(p, m, k);
            }

            return p;
        }
    }
}