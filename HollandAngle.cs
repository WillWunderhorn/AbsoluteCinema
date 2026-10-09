using Il2Cpp;
using UnityEngine;

namespace DIrectorMode
{
    public class HollandAngle
    {
        private const float DoublePressTime = 0.35f;// double press
        private const float PitchLimit = 85f;// pitch

        private bool locking;
        private Vector2 lockedAngle;

        private float rawRoll;// roll while dragging
        private float roll;
        private float shownRoll;// roll actually smoothed
        private bool applied;
        private float flyBankRoll;
        private float lastPress = -10f;
        private Vector2 baseline;
        private int baselineFrame = -10;
        private bool hasBaseline;
        public bool IsLocking => locking;
        public bool IsActive => locking || applied || Mathf.Abs(roll) > 0.001f || Mathf.Abs(flyBankRoll) > 0.001f;

        public void Update()
        {
            vp_FPSCamera cam = GameManager.GetVpFPSCamera();

            if (cam == null)
            {
                locking = false;
                hasBaseline = false;
                return;
            }

            Settings s = Settings.Instance;

            bool allowed = Time.timeScale > 0f &&
                           !(Main.Tutorial != null && Main.Tutorial.IsOpen);

            // reset
            if (allowed && Main.KeyDown(s.hollandKey))
            {
                if (Time.unscaledTime - lastPress <= DoublePressTime)
                {
                    ResetAngle();
                    lastPress = -10f;
                }
                else
                {
                    lastPress = Time.unscaledTime;
                }
            }

            bool hold = allowed && Main.Key(s.hollandKey);

            if (hold && !locking) BeginLock(cam);
            else if (!hold && locking) EndLock();

            float target;

            if (locking)
            {
                UpdateDrag(cam, s);
                float turn = Mathf.Round(rawRoll / 360f) * 360f;
                target = Mathf.Abs(rawRoll - turn) < s.hollandSnap ? turn : rawRoll;
            }
            else
            {
                target = roll;
                ApplyRelativeLook(cam, s);
            }

            float k = 1f - Mathf.Exp(-FollowSpeed(s.hollandSmoothness) * Time.unscaledDeltaTime);
            shownRoll = Mathf.Lerp(shownRoll, target, k);
            if (Mathf.Abs(shownRoll - target) < 0.05f) shownRoll = target;
        }

        public void EndFrame()
        {
            vp_FPSCamera cam = GameManager.GetVpFPSCamera();

            if (cam == null)
            {
                hasBaseline = false;
                return;
            }

            float total = Mathf.DeltaAngle(0f, shownRoll) + flyBankRoll;

            if (Mathf.Abs(total) > 0.001f)
            {
                cam.m_BaseRoll = total;
                applied = true;
            }
            else if (applied)
            {
                cam.m_BaseRoll = 0f;
                applied = false;
            }

            baseline = cam.Angle;
            baselineFrame = Time.frameCount;
            hasBaseline = true;
        }

        public void Reset()
        {
            locking = false;
            roll = 0f;
            rawRoll = 0f;
            shownRoll = 0f;
            lastPress = -10f;
            hasBaseline = false;
            flyBankRoll = 0f;

            vp_FPSCamera cam = GameManager.GetVpFPSCamera();
            if (cam != null && applied) cam.m_BaseRoll = 0f;
            applied = false;
        }

        private void ApplyRelativeLook(vp_FPSCamera cam, Settings s)
        {
            if (!s.hollandRelativeLook) return;
            if (!hasBaseline || Time.frameCount - baselineFrame > 1) return;

            float tilt = Mathf.DeltaAngle(0f, shownRoll);
            if (Mathf.Abs(tilt) < 0.5f) return;// level

            Vector2 a = cam.Angle;
            float dYaw = Mathf.DeltaAngle(baseline.x, a.x);
            float dPitch = a.y - baseline.y;

            if (dYaw == 0f && dPitch == 0f) return;
            if (Mathf.Abs(dYaw) > 90f || Mathf.Abs(dPitch) > 90f) return;

            float rad = tilt * (s.hollandRelativeInvert ? -1f : 1f) * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            float sn = Mathf.Sin(rad);

            float newYaw = dYaw * c - dPitch * sn;
            float newPitch = dYaw * sn + dPitch * c;

            float y = Mathf.Repeat(baseline.x + newYaw, 360f);
            float p = Mathf.Clamp(baseline.y + newPitch, -PitchLimit, PitchLimit);

            SetAllAngles(cam, y, p);
        }

        private void ResetAngle()
        {
            roll = 0f;
            rawRoll = 0f;

            // shortest way back
            shownRoll = Mathf.DeltaAngle(0f, shownRoll);

            Main.Notify("Holland angle: reset");
        }

        private static float FollowSpeed(int level)
        {
            float t = (Mathf.Clamp(level, 1, 100) - 1) / 99f;
            return 40f * Mathf.Pow(1.2f / 40f, t);
        }

        private void BeginLock(vp_FPSCamera cam)
        {
            locking = true;
            lockedAngle = cam.Angle;
            rawRoll = roll;
        }

        private void EndLock()
        {
            locking = false;
            float turn = Mathf.Round(rawRoll / 360f) * 360f;
            float rest = rawRoll - turn;

            roll = Mathf.Abs(rest) < Settings.Instance.hollandSnap ? 0f : rest;
            shownRoll -= turn;

            if (Main.SmoothCamera != null) Main.SmoothCamera.RequestSync();
        }

        private void UpdateDrag(vp_FPSCamera cam, Settings s)
        {
            Vector2 a = cam.Angle;

            float dYaw = Mathf.DeltaAngle(lockedAngle.x, a.x);

            if (Mathf.Abs(dYaw) < 90f)
            {
                float sign = s.hollandInvert ? -1f : 1f;
                rawRoll += dYaw * s.hollandSensitivity * sign;
            }

            SetAllAngles(cam, lockedAngle.x, lockedAngle.y);
        }

        public void SetFlyBank(float value)
        {
            flyBankRoll = value;
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
    }
}
