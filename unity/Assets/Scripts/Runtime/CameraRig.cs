using UnityEngine;

namespace CarretaJoy
{
    /// <summary>Câmera em terceira pessoa: segue o menino, gira arrastando a tela e não atravessa prédios.</summary>
    public class CameraRig : MonoBehaviour
    {
        public Kid target;
        public float yaw, pitch = 16f, distance = 5.5f;
        float curDist = 5.5f;
        Vector3 focus;
        bool init;

        public void Rotate(Vector2 delta)
        {
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, -5f, 70f);
        }

        public void Tick(float dt)
        {
            if (target == null) return;
            float want = target.OnTruck ? 8.5f : distance;
            Vector3 f = target.transform.position + Vector3.up * (target.Height * 0.85f + (target.OnTruck ? 0.6f : 0f));
            focus = init ? Vector3.Lerp(focus, f, 1 - Mathf.Exp(-dt * 14f)) : f;

            // sem mexer na câmera, ela volta devagar para trás do menino quando ele corre
            if (target.HorizontalSpeed > 2.5f && !dragging)
            {
                float behind = target.transform.eulerAngles.y;
                yaw = Mathf.LerpAngle(yaw, behind, 1 - Mathf.Exp(-dt * 0.8f));
            }

            var rot = Quaternion.Euler(pitch, yaw, 0);
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
