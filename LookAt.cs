using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace DIrectorMode
{
    public class LookAt
    {
        private const float MinDistance = 1.0f;
        private const float MaxDistance = 80f;
        private const float DistanceStep = 1.15f;
        private const float SphereSize = 0.35f;

        private bool placing;// holding T
        private bool hasTarget;// tracker has been placed, camera is locked
        private GameObject sphere;
        private Vector3 targetPoint;
        private float placeDistance = 4.5f;
        private Material sphereMat;
        public bool HasTarget => hasTarget;
        public bool IsPlacing => placing;
        public Vector3 TargetPoint => targetPoint;

        private static KeyCode GetKey()
        {
            try
            {
                KeyCode k = Settings.Instance.lookAtKey;
                if (k != KeyCode.None)
                    return k;
            }
            catch { }
            return KeyCode.T;
        }

        public void Update()
        {
            if (Main.Vertigo != null && Main.Vertigo.IsActive)
            {
                if (placing || hasTarget)
                    Reset();
                return;
            }

            if (Main.CinematicFly != null && Main.CinematicFly.IsMoving && Settings.Instance.lookAlongPath)
            {
                if (placing || hasTarget)
                    Reset();
                return;
            }

            vp_FPSCamera cam = GameManager.GetVpFPSCamera();
            if (cam == null || cam.m_Camera == null)
            {
                if (placing)
                    EndPlacement();
                return;
            }

            bool allowed = Time.timeScale > 0f &&
                           !(Main.Tutorial != null && Main.Tutorial.IsOpen);

            KeyCode key = GetKey();

            // if a tracker is already placed
            if (allowed && Main.KeyDown(key) && hasTarget)
            {
                ClearTarget();
                return;
            }

            // placement mode
            bool hold = allowed && Main.Key(key) && !hasTarget;

            if (hold && !placing)
                BeginPlacement();
            else if (!hold && placing)
                EndPlacement();

            if (placing)
            {
                float scroll = Main.Scroll;
                if (scroll != 0f)
                {
                    placeDistance = Mathf.Clamp(
                        placeDistance * Mathf.Pow(DistanceStep, scroll),
                        MinDistance,
                        MaxDistance);
                }

                UpdatePlacement(cam);

                if (Main.MouseDown(0))
                    PlaceTracker();
            }

            if (hasTarget && !placing && !(Main.HollandAngle != null && Main.HollandAngle.IsLocking))
                ApplyLookAt(cam);
        }

        public void Reset()
        {
            ClearTarget();
            placing = false;
            placeDistance = 4.5f;
            if (Main.SmoothCamera != null) Main.SmoothCamera.RequestSync();
        }

        public void ClearTarget()
        {
            if (hasTarget || sphere != null)
                Main.Notify("Look-at tracker removed");

            hasTarget = false;
            placing = false;
            DestroySphere();
        }

        private void BeginPlacement()
        {
            placing = true;

            if (sphere == null)
                CreateSphere();

            if (sphere != null)
            {
                sphere.SetActive(true);
                SetSphereVisible(true);
            }

            if (Main.SmoothCamera != null)
                Main.SmoothCamera.RequestSync();
        }

        private void EndPlacement()
        {
            placing = false;

            // destroy the sphere
            if (!hasTarget)
                DestroySphere();
        }

        private void UpdatePlacement(vp_FPSCamera cam)
        {
            if (sphere == null) return;

            Transform ct = cam.m_Camera.transform;
            sphere.transform.position = ct.position + ct.forward * placeDistance;
            sphere.transform.rotation = Quaternion.identity;
        }

        private void PlaceTracker()
        {
            if (sphere == null) return;

            targetPoint = sphere.transform.position;
            hasTarget = true;
            placing = false;

            // tracker placed and becomes invisible
            SetSphereVisible(false);

            Main.Notify("Tracker placed");
        }

        private void ApplyLookAt(vp_FPSCamera cam)
        {
            Vector3 eye = cam.m_Camera.transform.position;
            Vector3 dir = targetPoint - eye;

            if (dir.sqrMagnitude < 0.0001f)
                return;

            Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up);
            Vector3 euler = look.eulerAngles;

            float yaw = euler.y;
            float pitch = euler.x;
            if (pitch > 180f) pitch -= 360f;

            // vp_FPSCamera
            pitch = -pitch;
            // allow full look up/down

            SetAllAngles(cam, yaw, pitch);
        }

        private static void SetAllAngles(vp_FPSCamera cam, float y, float p)
        {
            cam.Angle = new Vector2(y, p);
            cam.m_Yaw = y;
            cam.m_Pitch = p;
            cam.m_CurrentYaw = y;
            cam.m_TargetYaw = y;
            cam.m_CurrentPitch = p;
            cam.m_TargetPitch = p;
        }

        // Sphere

        private void CreateSphere()
        {
            EnsureMaterial();

            sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "DirectorMode_LookAtTracker";
            sphere.transform.localScale = Vector3.one * SphereSize;

            Collider col = sphere.GetComponent<Collider>();
            if (col != null)
                UnityEngine.Object.Destroy(col);

            Renderer rend = sphere.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = sphereMat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
            }

            UnityEngine.Object.DontDestroyOnLoad(sphere);
            sphere.hideFlags = HideFlags.HideAndDontSave;
        }

        private void DestroySphere()
        {
            if (sphere != null)
            {
                UnityEngine.Object.Destroy(sphere);
                sphere = null;
            }
            hasTarget = false;
        }

        private void SetSphereVisible(bool visible)
        {
            if (sphere == null) return;

            Renderer r = sphere.GetComponent<Renderer>();
            if (r != null)
                r.enabled = visible;
        }

        private void EnsureMaterial()
        {
            if (sphereMat != null) return;

            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            if (sh == null) sh = Shader.Find("UI/Default");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            if (sh == null) sh = Shader.Find("Standard");

            sphereMat = new Material(sh != null ? sh : Shader.Find("Hidden/InternalErrorShader"));
            sphereMat.color = new Color(0.2f, 1f, 0.3f, 0.55f); // color
            sphereMat.hideFlags = HideFlags.HideAndDontSave;

            if (sphereMat.HasProperty("_Mode"))
            {
                sphereMat.SetFloat("_Mode", 3f);
                sphereMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                sphereMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                sphereMat.SetInt("_ZWrite", 0);
                sphereMat.DisableKeyword("_ALPHATEST_ON");
                sphereMat.EnableKeyword("_ALPHABLEND_ON");
                sphereMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }

            sphereMat.renderQueue = 3000;
        }
    }
}
