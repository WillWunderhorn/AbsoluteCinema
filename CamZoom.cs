using Il2Cpp;
using UnityEngine;

namespace DIrectorMode
{
    // this think can zoom just like in Minecraft!
    public class CamZoom
    {
        private float zoomFactor = 1f;
        private float zoomTarget = 2.5f;
        private const float ZoomMin = 1.1f;
        private const float ZoomMax = 20f;
        private float baseFov = 60f;
        private float vertigoFovScale = 1f;
        private bool vertigoRestorePending;
        public float ZoomFactor => zoomFactor;
        public float BaseFov => baseFov;

        public void Update()
        {
            vp_FPSCamera cam = GameManager.GetVpFPSCamera();

            if (cam == null || cam.m_Camera == null)
            {
                zoomFactor = 1f;
                SetSmoothForced(false);
                return;
            }

            Settings s = Settings.Instance;
            bool hold = Main.Key(s.zoomKey);

            // saving FOV
            if (!hold && zoomFactor <= 1.005f && Mathf.Approximately(vertigoFovScale, 1f) && !vertigoRestorePending)
            {
                if (zoomFactor != 1f)
                {
                    zoomFactor = 1f;
                    SetFov(cam, baseFov);
                }
                else
                {
                    baseFov = cam.m_Camera.fieldOfView;
                }

                SetSmoothForced(false);
                return;
            }

            // Zooming
            bool vertigoActive = !Mathf.Approximately(vertigoFovScale, 1f);
            SetSmoothForced(hold || vertigoActive);

            // Mouse wheel
            if (hold && !(Main.Vertigo != null && Main.Vertigo.IsActive))
            {
                float scroll = Main.Scroll;

                if (scroll != 0f)
                {
                    float step = 1f + s.zoomWheelStep / 100f;

                    zoomTarget = Mathf.Clamp(
                        zoomTarget * Mathf.Pow(step, scroll),
                        ZoomMin,
                        ZoomMax);
                }
            }

            float target = hold ? zoomTarget : 1f;
            float k = 1f - Mathf.Exp(-s.zoomSmoothing * Time.deltaTime);
            
            zoomFactor = Mathf.Lerp(zoomFactor, target, k);
            
            SetFov(cam, CalculateCombinedFov());
            vertigoRestorePending = false;
        }

        public void Reset()
        {
            zoomFactor = 1f;
            vertigoFovScale = 1f;
            vertigoRestorePending = false;
            vp_FPSCamera cam = GameManager.GetVpFPSCamera();
            if (cam != null && cam.m_Camera != null)
                SetFov(cam, baseFov);
            SetSmoothForced(false);
        }

        public void SetVertigoFovScale(float scale)
        {
            float next = Mathf.Clamp(scale, 0.1f, 10f);
            if (!Mathf.Approximately(vertigoFovScale, 1f) && Mathf.Approximately(next, 1f))
                vertigoRestorePending = true;
            vertigoFovScale = next;
        }

        public float CaptureBaseFov(vp_FPSCamera cam)
        {
            if (cam != null && cam.m_Camera != null && zoomFactor <= 1.005f && Mathf.Approximately(vertigoFovScale, 1f))
                baseFov = cam.m_Camera.fieldOfView;
            return baseFov;
        }

        private float CalculateCombinedFov()
        {
            float halfRad = baseFov * 0.5f * Mathf.Deg2Rad;
            float tangent = Mathf.Tan(halfRad) * vertigoFovScale / Mathf.Max(0.01f, zoomFactor);
            return Mathf.Clamp(2f * Mathf.Atan(tangent) * Mathf.Rad2Deg, 1f, 179f);
        }

        private static void SetFov(vp_FPSCamera cam, float fov)
        {
            cam.m_Camera.fieldOfView = fov;
            cam.m_RenderingFieldOfView = fov;
        }

        private static void SetSmoothForced(bool on)
        {
            if (Main.Instance != null && Main.Instance.Smooth != null)
                Main.Instance.Smooth.SetForced(on);
        }
    }
}
