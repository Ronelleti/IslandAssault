using UnityEngine;

namespace IslandAssault
{
    /// <summary>
    /// Strategy camera: drag to pan, mouse wheel / pinch to zoom. The view tilts a little as you zoom.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        public static CameraController I;

        public Camera cam;
        public Vector3 pivot = new Vector3(0, 0.8f, -4f);
        public float distance = 44f;
        public float minDistance = 16f;
        public float maxDistance = 85f;
        public float yaw = 0f;
        public float boundRadius = 42f;

        /// <summary>When true, dragging doesn't pan (e.g. while dragging a building).</summary>
        public bool PanBlocked;
        public bool InputEnabled = true;

        float targetDistance;
        float shake;
        Vector3 panVelocity;
        Vector3 lastGround;
        bool panning;
        Vector3? flyTarget;

        public static CameraController Setup()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            var cc = cam.gameObject.GetComponent<CameraController>();
            if (cc == null) cc = cam.gameObject.AddComponent<CameraController>();
            cc.cam = cam;
            cam.fieldOfView = 32f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 600f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = new Color(0.55f, 0.78f, 0.95f);
            I = cc;
            RenderQuality.Apply(cam);
            cc.targetDistance = cc.distance;
            return cc;
        }

        public void Focus(Vector3 point, float dist)
        {
            pivot = new Vector3(point.x, Island.PlateauHeight, point.z);
            distance = targetDistance = Mathf.Clamp(dist, minDistance, maxDistance);
            panVelocity = Vector3.zero;
            flyTarget = null;
        }

        public void FlyTo(Vector3 point)
        {
            flyTarget = new Vector3(point.x, Island.PlateauHeight, point.z);
        }

        public void Shake(float amount) { shake = Mathf.Min(1.2f, shake + amount); }

        Quaternion Rotation
        {
            get
            {
                float t = Mathf.InverseLerp(minDistance, maxDistance, distance);
                float pitch = Mathf.Lerp(40f, 56f, t);
                return Quaternion.Euler(pitch, yaw, 0f);
            }
        }

        bool GroundPoint(Vector2 screen, out Vector3 p)
        {
            var ray = cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, new Vector3(0, Island.PlateauHeight, 0));
            float enter;
            if (plane.Raycast(ray, out enter)) { p = ray.GetPoint(enter); return true; }
            p = Vector3.zero;
            return false;
        }

        void LateUpdate()
        {
            if (cam == null) return;
            float dt = Time.unscaledDeltaTime;

            if (InputEnabled)
            {
                // Zoom
                if (Mathf.Abs(GameInput.Scroll) > 0.001f && !GameInput.IsOverUI(GameInput.Pos))
                    targetDistance *= Mathf.Pow(0.88f, GameInput.Scroll);
                if (GameInput.Touches >= 2 && Mathf.Abs(GameInput.PinchRatio - 1f) > 0.0001f)
                    targetDistance /= GameInput.PinchRatio;
                targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);

                // Pan (one finger / mouse drag)
                bool canPan = GameInput.Held && !GameInput.OverUI && !PanBlocked && GameInput.Touches < 2;
                if (canPan)
                {
                    Vector3 g;
                    if (GroundPoint(GameInput.Pos, out g))
                    {
                        if (!panning) { panning = true; lastGround = g; }
                        else if (GameInput.Dragging)
                        {
                            // keep the grabbed ground point under the finger
                            Vector3 delta = lastGround - g;
                            delta.y = 0;
                            pivot += delta;
                            panVelocity = delta / Mathf.Max(dt, 0.001f);
                            flyTarget = null;
                            ApplyTransform();
                            GroundPoint(GameInput.Pos, out lastGround);
                        }
                    }
                }
                else
                {
                    if (panning && GameInput.Touches >= 2) panVelocity = Vector3.zero;
                    panning = false;
                    // inertia
                    pivot += panVelocity * dt;
                    panVelocity = Vector3.Lerp(panVelocity, Vector3.zero, dt * 6f);
                }
            }

            if (flyTarget.HasValue)
            {
                pivot = Vector3.Lerp(pivot, flyTarget.Value, dt * 3f);
                if ((pivot - flyTarget.Value).sqrMagnitude < 0.01f) flyTarget = null;
            }

            // keep inside bounds
            Vector3 flat = new Vector3(pivot.x, 0, pivot.z);
            if (flat.magnitude > boundRadius) { flat = flat.normalized * boundRadius; pivot.x = flat.x; pivot.z = flat.z; panVelocity = Vector3.zero; }
            pivot.y = Island.PlateauHeight;

            distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-dt * 10f));
            ApplyTransform();

            if (shake > 0f)
            {
                cam.transform.position += Random.insideUnitSphere * shake * 0.6f;
                shake = Mathf.Max(0f, shake - dt * 2.5f);
            }
        }

        void ApplyTransform()
        {
            var rot = Rotation;
            cam.transform.rotation = rot;
            cam.transform.position = pivot - rot * Vector3.forward * distance;
        }
    }
}
