using Il2Cpp;
using UnityEngine;

namespace DIrectorMode
{
    public class SmoothCamera
    {
        private const float PitchMin = -85f;
        private const float PitchMax = 85f;
        private bool cinematic;
        private bool forced;
        private bool needSync = true;
        private float targetYaw;
        private float targetPitch;
        private float smoothYaw;
        private float smoothPitch;
        private Vector2 lastWritten;
        private float smoothLevel = -1f;
        private float lastSettingLevel;
        private float lastLevelNotify;
        private const float LevelMin = 1f;
        private const float LevelMax = 20f;
        private float effLevel = -1f;
        public bool IsEnabled => cinematic;

        // smoothing
        public bool IsActive => cinematic || forced;

        private static bool IsZooming()
        {
            if (Main.Key(Settings.Instance.zoomKey)) return true;
            return Main.CamZoom != null && Main.CamZoom.ZoomFactor > 1.005f;
        }

        public void Toggle()
        {
            bool wasActive = IsActive;
            cinematic = !cinematic;
            if (wasActive != IsActive) needSync = true;

            Main.Notify("Smooth camera is " +
                (cinematic ? "ON" : "OFF"));
        }

        // current smoothness
        private float GetLevel()
        {
            float setting = Settings.Instance.smoothSpeed;

            if (smoothLevel < 0f || setting != lastSettingLevel)
            {
                lastSettingLevel = setting;
                smoothLevel = setting;
            }

            return smoothLevel;
        }

        private static bool WheelAdjustAllowed()
        {
            if (Main.Key(Settings.Instance.zoomKey)) return false;
            if (Main.CamZoom != null && Main.CamZoom.ZoomFactor > 1.005f) return false;
            if (Main.FastFlyMode != null && Main.FastFlyMode.IsFlying) return false;
            if (Main.CinematicFly != null && Main.CinematicFly.IsMoving) return false;
            if (Main.Tutorial != null && Main.Tutorial.IsOpen) return false;
            if (Main.Vertigo != null && Main.Vertigo.IsActive) return false;
            return true;
        }

        private void HandleWheel()
        {
            if (!cinematic || !WheelAdjustAllowed()) return;

            float scroll = Main.Scroll;
            if (scroll == 0f) return;

            smoothLevel = Mathf.Clamp(GetLevel() + scroll, LevelMin, LevelMax);

            if (Time.unscaledTime - lastLevelNotify > 0.25f)
            {
                lastLevelNotify = Time.unscaledTime;
            }
        }

        public void SetForced(bool on)
        {
            if (forced == on) return;

            bool wasActive = IsActive;
            forced = on;
            if (wasActive != IsActive) needSync = true;
        }

        public void RequestSync()
        {
            needSync = true;
        }

        public void Reset()
        {
            cinematic = false;
            forced = false;
            needSync = true;
        }

        public void Update()
        {
            if (!IsActive)
                return;

            HandleWheel();
            UpdateCinematicMouse();
        }

        private void UpdateCinematicMouse()
        {
            vp_FPSCamera cam = GameManager.GetVpFPSCamera();

            if (cam == null)
            {
                needSync = true;
                return;
            }

            Vector2 a = cam.Angle;

            if (needSync)
            {
                Sync(a);
                return;
            }

            float dYaw = Mathf.DeltaAngle(lastWritten.x, a.x);
            float dPitch = a.y - lastWritten.y;

            if (Mathf.Abs(dYaw) > 90f || Mathf.Abs(dPitch) > 90f)
            {
                Sync(a);
                return;
            }

            // more zoomed, slower the view turns
            float zoom = 1f;
            if (Main.Instance != null && Main.Instance.Zoom != null)
                zoom = Mathf.Max(1f, Main.Instance.Zoom.ZoomFactor);
            if (zoom > 1.001f)
            {
                dYaw /= zoom;
                dPitch /= zoom;
            }

            targetYaw += dYaw;
            targetPitch = Mathf.Clamp(targetPitch + dPitch, PitchMin, PitchMax);

            float targetLevel = IsZooming() ? Settings.Instance.zoomCameraSmoothness : GetLevel();

            if (effLevel < 0f) effLevel = targetLevel;
            effLevel = Mathf.Lerp(effLevel, targetLevel, 1f - Mathf.Exp(-8f * Time.deltaTime));

            float catchUpSpeed = Mathf.Max(0.5f, 21f - effLevel);

            float kk = 1f - Mathf.Exp(
                -catchUpSpeed *
                Time.deltaTime);

            smoothYaw = Mathf.Lerp(
                smoothYaw,
                targetYaw,
                kk);

            smoothPitch = Mathf.Lerp(
                smoothPitch,
                targetPitch,
                kk);

            float y = Mathf.Repeat(smoothYaw, 360f);
            float p = Mathf.Clamp(
                smoothPitch,
                PitchMin,
                PitchMax);

            SetAllAngles(cam, y, p);

            lastWritten = new Vector2(y, p);

        }

        private static void SetAllAngles(
            vp_FPSCamera cam,
            float y,
            float p)
        {
            cam.Angle = new Vector2(y, p);
            cam.m_Yaw = y;
            cam.m_Pitch = p;
            cam.m_CurrentYaw = y;
            cam.m_TargetYaw = y;
            cam.m_CurrentPitch = p;
            cam.m_TargetPitch = p;
        }

        private void Sync(Vector2 a)
        {
            targetYaw = smoothYaw = a.x;
            targetPitch = smoothPitch = a.y;
            lastWritten = a;
            needSync = false;
        }
    }
}
