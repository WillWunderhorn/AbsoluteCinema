using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace DIrectorMode
{
    public class FastFlyMode
    {
        private float currentSpeed;
        private float lastSettingSpeed;
        private const float SpeedStep = 1.1f;
        private const float SpeedMin = 0.01f;
        private const float SpeedMax = 20f;
        private bool flying;
        private FlyMode flyMode;
        private bool hasBankYaw;
        private float previousBankYaw;
        private float bankRoll;
        private readonly CinematicFly cinematicFly;
        public bool IsFlying => flying;
        private const KeyCode GameExitKey = KeyCode.Space;
        private float bankVelocity;
        private float filteredRate;

        public FastFlyMode(CinematicFly cinematicFly)
        {
            this.cinematicFly = cinematicFly;
        }

        public void Update()
        {
            if (!flying) return;
            if (flyMode == null) flyMode = GetFlyMode();
            if (flyMode == null) return;

            if (Main.KeyDown(GameExitKey))
            {
                flying = false;
                return;
            }

            Settings s = Settings.Instance;

            // adopt the new slider value
            if (s.flySpeedRegular != lastSettingSpeed)
            {
                lastSettingSpeed = s.flySpeedRegular;
                currentSpeed = Mathf.Max(0.01f, lastSettingSpeed);
            }

            // wheel changes fly speed, unless zoom or look-at
            if (!IsWheelBlocked())
            {
                float scroll = Main.Scroll;

                if (scroll != 0f)
                {
                    currentSpeed = Mathf.Clamp(
                        currentSpeed * Mathf.Pow(SpeedStep, scroll),
                        SpeedMin,
                        SpeedMax);
                }
            }

            ApplyFlySpeeds(flyMode);
        }

        public void OnGameEntered()
        {
            flying = true;
            hasBankYaw = false;

            if (currentSpeed <= 0f)
            {
                currentSpeed = Mathf.Max(0.01f, Settings.Instance.flySpeedRegular);
                lastSettingSpeed = Settings.Instance.flySpeedRegular;
            }
        }

        public void StartFly()
        {
            if (cinematicFly.IsMoving)
                cinematicFly.StopMove("stopped by fly mode");

            FlyMode fm = GetFlyMode();

            if (fm == null)
            {
                return;
            }

            currentSpeed = Settings.Instance.flySpeedRegular;
            lastSettingSpeed = currentSpeed;
            ApplyFlySpeeds(fm);

            FlyMode.Enter();

            flying = true;
            hasBankYaw = false;
        }

        public void OnGameExited()
        {
            flying = false;
            hasBankYaw = false;
        }

        // after HollandAngle.Update so the fly bank composes with dutch angle
        public void UpdateBank()
        {
            vp_FPSCamera cam = GameManager.GetVpFPSCamera();
            if (cam == null) return;

            float dt = Time.unscaledDeltaTime;
            if (dt < 0.0001f) return;

            Settings s = Settings.Instance;

            if (Main.LookAt != null && (Main.LookAt.IsPlacing || Main.LookAt.HasTarget))
            {
                hasBankYaw = false;
                filteredRate = 0f;
                bankRoll = 0f;
                bankVelocity = 0f;
                if (Main.HollandAngle != null)
                    Main.HollandAngle.SetFlyBank(0f);
                return;
            }

            float targetRoll = 0f;

            if (flying && s.flyBankEnabled && Time.timeScale > 0f &&
                !(Main.Tutorial != null && Main.Tutorial.IsOpen))
            {
                float yaw = cam.Angle.x;

                if (hasBankYaw)
                {
                    float delta = Mathf.DeltaAngle(previousBankYaw, yaw);

                    // A big jump = something else moved the view (resync, teleport): ignore this frame, keep the last rate
                    if (Mathf.Abs(delta) < 45f)
                    {
                        float rate = delta / dt;                                  // degrees per second, + = turning right
                        float k = 1f - Mathf.Exp(-10f * dt);                      // low-pass: mouse input is very uneven per frame
                        filteredRate = Mathf.Lerp(filteredRate, rate, k);
                    }
                }

                previousBankYaw = yaw;
                hasBankYaw = true;

                // same strength scale as before (per-frame value at 60 fps), now in degrees per second
                targetRoll = Mathf.Clamp(-filteredRate * s.flyBankStrength * (5f / 60f), -30f, 30f);
            }
            else
            {
                hasBankYaw = false;
                filteredRate = 0f;
            }

            bankRoll = Mathf.SmoothDamp(bankRoll, targetRoll, ref bankVelocity, 0.12f, Mathf.Infinity, dt);
            if (targetRoll == 0f && Mathf.Abs(bankRoll) < 0.02f) { bankRoll = 0f; bankVelocity = 0f; }

            if (Main.HollandAngle != null)
                Main.HollandAngle.SetFlyBank(bankRoll);
        }



        public void StopFly(bool notify = true)
        {
            if (!flying) return;

            FlyMode.Exit();

            flying = false;
        }

        public void Reset()
        {
            if (flying)
                StopFly(false);

            flyMode = null;
            flying = false;
            hasBankYaw = false;
            bankRoll = 0f;
            if (Main.HollandAngle != null) Main.HollandAngle.SetFlyBank(0f);
            filteredRate = 0f; bankVelocity = 0f;
        }

        private FlyMode GetFlyMode()
        {
            if (flyMode == null)
                flyMode = UnityEngine.Object.FindObjectOfType<FlyMode>();

            return flyMode;
        }

        private void ApplyFlySpeeds(FlyMode fm)
        {
            float timeScale = Main.SlowMotion != null ? Mathf.Max(0.01f, Main.SlowMotion.CurrentScale) : 1f;
            fm.m_MoveSpeedNormal = currentSpeed / timeScale;
        }

        // wheel is reserved
        private static bool IsWheelBlocked()
        {
            if (Main.Key(Settings.Instance.zoomKey))
                return true;

            if (Main.CamZoom != null && Main.CamZoom.ZoomFactor > 1.005f)
                return true;

            // wheel moves the sphere
            if (Main.LookAt != null && Main.LookAt.IsPlacing)
                return true;

            if (Main.Vertigo != null && Main.Vertigo.IsActive)
                return true;

            return false;
        }
    }
}
