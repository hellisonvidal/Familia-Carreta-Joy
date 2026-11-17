using UnityEngine;

namespace CarretaJoy
{
    /// <summary>
    /// Câmera em terceira pessoa: segue o personagem (ou a carreta quando dirige), gira arrastando a tela
    /// e não atravessa prédios. Modos: Normal, Perto, Longe e Aérea.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public Kid target;
        public Transform follow;            // carreta (quando dirige)
        public int mode;
        public float yaw, pitch = 16f, distance = 5.5f;
        float zoom = 1f, curDist = 5.5f, curPitch = 16f;
        Vector3 focus;
        bool init;

        public void Rotate(Vector2 delta)
        {
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, -5f, 70f);
        }

        public void Zoom(float wheel) { if (wheel != 0) zoom = Mathf.Clamp(zoom * (wheel > 0 ? 0.9f : 1.1f), 0.5f, 2.5f); }

        public void Tick(float dt)
        {
            if (target == null && follow == null) return;
            bool drive = follow != null;
            float baseD = drive ? 17f : target.OnTruck ? 8.5f : distance;
            float k = mode == 1 ? 0.6f : mode == 2 ? 1.8f : 1f;
            float want = baseD * k * zoom;
            float p = pitch;
            if (mode == 3) { want = (drive ? 60f : 38f) * zoom; p = 62f; }
            else if (mode == 1) p = Mathf.Min(pitch, 12f);
            if (drive && mode != 3) p = Mathf.Max(p, 14f);
            curPitch = init ? Mathf.Lerp(curPitch, p, 1 - Mathf.Exp(-dt * 6f)) : p;

            Vector3 f = drive ? follow.position + Vector3.up * 3.2f
                              : target.transform.position + Vector3.up * (target.Height * 0.85f + (target.OnTruck ? 0.6f : 0f));
            focus = init ? Vector3.Lerp(focus, f, 1 - Mathf.Exp(-dt * 14f)) : f;

            // sem mexer na câmera, ela volta devagar para trás de quem anda/dirige
            float spd = drive ? 3f : target.HorizontalSpeed;
            if (spd > 2.5f && !dragging && mode != 3)
            {
                float behind = drive ? follow.eulerAngles.y : target.transform.eulerAngles.y;
                yaw = Mathf.LerpAngle(yaw, behind, 1 - Mathf.Exp(-dt * (drive ? 1.6f : 0.8f)));
            }

            var rot = Quaternion.Euler(curPitch, yaw, 0);
            Vector3 back = rot * Vector3.back;
            float d = want;
            if (Physics.SphereCast(focus, 0.3f, back, out var hit, want, ~(1 << 2), QueryTriggerInteraction.Ignore))
                d = Mathf.Max(1.2f, hit.distance - 0.1f);
            curDist = init ? (d < curDist ? d : Mathf.Lerp(curDist, d, 1 - Mathf.Exp(-dt * 4f))) : d;
            transform.position = focus + back * curDist;
            transform.rotation = rot;
            init = true;
        }

        public bool dragging;
        public Vector3 Flat(Vector2 joy)
        {
            var f = Quaternion.Euler(0, yaw, 0);
            return f * new Vector3(joy.x, 0, joy.y);
        }
    }
}
