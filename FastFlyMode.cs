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
        private const float SpeedMax = 100f;
        private bool flying;
        private FlyMode flyMode;
        private bool hasBankYaw;
        private float previousBankYaw;
        private float bankRoll;
        private readonly CinematicFly cinematicFly;
        public bool IsFlying => flying;
        public bool IsFreeCam => freeCam;
        public bool IsAnyFly => flying || freeCam;
        private const KeyCode GameExitKey = KeyCode.Space;
        private float bankVelocity;
        private float filteredRate;
        private bool freeCam;
        private Vector3 freeCamPos;
        private float freeCamAccel;
        private Vector3 freeCamLastDir = Vector3.forward;
        private float freeCamYaw;
        private float freeCamPitch;
        private Vector3 freeCamBodyPos;   // world freeze for player model
        private Quaternion freeCamBodyRot;
        private bool freeCamBodyFrozen;
        private PlayerControlMode savedControlMode;
        private bool controlModeSaved;
        private bool controllerWasEnabled;
        private bool controllerSaved;
        private CharacterController lockedController;

        public FastFlyMode(CinematicFly cinematicFly)
        {
            this.cinematicFly = cinematicFly;
        }

        public void Update()
        {
            if (freeCam)
            {
                UpdateFreeCam();
                return;
            }

            if (!flying) return;
            if (flyMode == null) flyMode = GetFlyMode();
            if (flyMode == null) return;

            if (Main.KeyDown(GameExitKey))
            {
                flying = false;
                return;
            }

            Settings s = Settings.Instance;

            if (s.flySpeedRegular != lastSettingSpeed)
            {
                lastSettingSpeed = s.flySpeedRegular;
                currentSpeed = Mathf.Max(0.01f, lastSettingSpeed);
            }

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
        public void LateUpdateMove()
        {
            if (!freeCam) return;
            EnforcePlayerLock();
            EnforceBodyWorldFreeze();
            ApplyFreeCamPose();
        }

        public void OnGameEntered()
        {
            if (freeCam) return;

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
            if (freeCam)
                StopFreeCam(false);

            if (cinematicFly.IsMoving)
                cinematicFly.StopMove("stopped by fly mode");

            FlyMode fm = GetFlyMode();

            if (fm == null)
            {
                return;
            }

            float slider = Settings.Instance.flySpeedRegular;
            if (currentSpeed <= 0f || slider != lastSettingSpeed)
                currentSpeed = Mathf.Max(0.01f, slider);
            lastSettingSpeed = slider;

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

        // after HollandAngle.Update() so the fly bank composes with dutch angle
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

            if ((flying || freeCam) && s.flyBankEnabled && Time.timeScale > 0f &&
                !(Main.Tutorial != null && Main.Tutorial.IsOpen))
            {
                float yaw = cam.Angle.x;

                if (hasBankYaw)
                {
                    float delta = Mathf.DeltaAngle(previousBankYaw, yaw);

                    if (Mathf.Abs(delta) < 45f)
                    {
                        float rate = delta / dt;
                        float k = 1f - Mathf.Exp(-10f * dt);
                        filteredRate = Mathf.Lerp(filteredRate, rate, k);
                    }
                }

                previousBankYaw = yaw;
                hasBankYaw = true;

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
            if (freeCam)
            {
                StopFreeCam(notify);
                return;
            }

            if (!flying) return;

            FlyMode.Exit();

            flying = false;
        }

        public void StopAll(bool notify = true)
        {
            if (freeCam)
                StopFreeCam(notify);
            else if (flying)
                StopFly(notify);
        }

        public void StartFreeCam()
        {
            if (freeCam) return;

            if (flying)
            {
                try { FlyMode.Exit(); } catch { }
                flying = false;
            }

            if (cinematicFly != null && cinematicFly.IsMoving)
                cinematicFly.StopMove("stopped by free camera");

            vp_FPSCamera cam = GameManager.GetVpFPSCamera();
            if (cam == null || cam.m_Camera == null)
            {
                //Main.Notify("Free camera: no camera");
                return;
            }

            freeCamPos = cam.m_Camera.transform.position;
            freeCamYaw = cam.Angle.x;
            freeCamPitch = cam.Angle.y;
            freeCam = true;
            freeCamAccel = 0f;
            freeCamLastDir = Vector3.forward;
            hasBankYaw = false;

            if (currentSpeed <= 0f)
            {
                currentSpeed = Mathf.Max(0.01f, Settings.Instance.flySpeedRegular);
                lastSettingSpeed = Settings.Instance.flySpeedRegular;
            }

            FreezePlayerBodyWorld();
            LockPlayerBody();

            FlyMode fm = GetFlyMode();
            if (fm != null)
            {
                flyModeWasEnabled = fm.enabled;
                fm.enabled = false;
            }

            if (Main.SmoothCamera != null)
                Main.SmoothCamera.RequestSync();

            //Main.Notify("independent camera is on");
        }

        public void StopFreeCam(bool notify = true)
        {
            if (!freeCam) return;
            freeCam = false;
            freeCamAccel = 0f;
            hasBankYaw = false;
            bankRoll = 0f;

            UnlockPlayerBody();
            freeCamBodyFrozen = false;

            FlyMode fm = GetFlyMode();
            if (fm != null)
                fm.enabled = flyModeWasEnabled;

            if (Main.HollandAngle != null) Main.HollandAngle.SetFlyBank(0f);
            if (Main.SmoothCamera != null) Main.SmoothCamera.RequestSync();
            //if (notify) Main.Notify("independent camera off");
        }

        private void UpdateFreeCam()
        {
            EnforcePlayerLock();
            EnforceBodyWorldFreeze();

            if (Main.KeyDown(GameExitKey))
            {
                StopFreeCam();
                return;
            }

            Settings s = Settings.Instance;

            if (s.flySpeedRegular != lastSettingSpeed)
            {
                lastSettingSpeed = s.flySpeedRegular;
                currentSpeed = Mathf.Max(0.01f, lastSettingSpeed);
            }

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

            float sens = 2f;
            try
            {
                // mild scaling with time so slowmo still feels usable
                sens = 2f;
            }
            catch { }

            freeCamYaw += Input.GetAxisRaw("Mouse X") * sens;
            freeCamPitch -= Input.GetAxisRaw("Mouse Y") * sens;
            freeCamPitch = Mathf.Clamp(freeCamPitch, -89.9f, 89.9f);

            vp_FPSCamera cam = GameManager.GetVpFPSCamera();
            if (cam != null)
            {
                cam.Angle = new Vector2(freeCamYaw, freeCamPitch);
                cam.m_Yaw = freeCamYaw;
                cam.m_Pitch = freeCamPitch;
                cam.m_CurrentYaw = freeCamYaw;
                cam.m_TargetYaw = freeCamYaw;
                cam.m_CurrentPitch = freeCamPitch;
                cam.m_TargetPitch = freeCamPitch;
            }

            Vector3 wish = GetFreeCamWishDirFromAngles();
            bool input = wish.sqrMagnitude > 0.0001f;
            if (input)
                freeCamLastDir = wish.normalized;

            bool wantSmooth = false;
            try { wantSmooth = s.flySmoothStartStop; } catch { }

            if (!wantSmooth)
                freeCamAccel = input ? 1f : 0f;
            else
            {
                int strength = 25;
                try { strength = s.flyEaseStrength; } catch { }
                float u = (Mathf.Clamp(strength, 1, 100) - 1) / 99f;
                float rate = Mathf.Lerp(14f, 0.9f, u);
                freeCamAccel = Mathf.MoveTowards(freeCamAccel, input ? 1f : 0f, rate * Time.unscaledDeltaTime);
            }

            if (freeCamAccel > 0.001f && freeCamLastDir.sqrMagnitude > 0.0001f)
            {
                float speed = currentSpeed * freeCamAccel;
                freeCamPos += freeCamLastDir * (speed * Time.unscaledDeltaTime);
            }

            ApplyFreeCamPose();
        }

        private void ApplyFreeCamPose()
        {
            vp_FPSCamera cam = GameManager.GetVpFPSCamera();
            if (cam == null || cam.m_Camera == null) return;

            float yaw = freeCamYaw;
            float pitch = freeCamPitch;
            if (Main.SmoothCamera != null && Main.SmoothCamera.IsActive)
            {
                yaw = cam.Angle.x;
                pitch = cam.Angle.y;
                freeCamYaw = yaw;
                freeCamPitch = pitch;
            }

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Transform eye = cam.m_Camera.transform;

            eye.position = freeCamPos;
            eye.rotation = rot;

            cam.Angle = new Vector2(yaw, pitch);
            cam.m_Yaw = yaw;
            cam.m_Pitch = pitch;
            cam.m_CurrentYaw = yaw;
            cam.m_TargetYaw = yaw;
            cam.m_CurrentPitch = pitch;
            cam.m_TargetPitch = pitch;
        }

        private Vector3 GetFreeCamWishDirFromAngles()
        {
            Quaternion rot = Quaternion.Euler(freeCamPitch, freeCamYaw, 0f);
            Vector3 forward = rot * Vector3.forward;
            Vector3 right = rot * Vector3.right;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            float up = 0f;

            if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f)
            {
                // axes empty is fine; keys may still drive Vertical/Horizontal on some configs
            }
            if (Input.GetKey(KeyCode.E)) //up
                up += 1f;
            if (Input.GetKey(KeyCode.Q)) //down
                up -= 1f;

            h = Mathf.Clamp(h, -1f, 1f);
            v = Mathf.Clamp(v, -1f, 1f);
            up = Mathf.Clamp(up, -1f, 1f);

            Vector3 dir = forward * v + right * h + Vector3.up * up;
            return dir.sqrMagnitude < 0.0001f ? Vector3.zero : dir.normalized;
        }

        private void FreezePlayerBodyWorld()
        {
            freeCamBodyFrozen = false;
            Transform body = Main.FindPlayer();
            if (body == null) return;
            freeCamBodyPos = body.position;
            freeCamBodyRot = body.rotation;
            freeCamBodyFrozen = true;
        }

        private void EnforceBodyWorldFreeze()
        {
            if (!freeCamBodyFrozen) return;
            Transform body = Main.FindPlayer();
            if (body == null) return;

            if ((body.position - freeCamBodyPos).sqrMagnitude > 0.0000001f)
                body.position = freeCamBodyPos;
            body.rotation = freeCamBodyRot;
        }

        public void Reset()
        {
            if (freeCam)
                StopFreeCam(false);
            else if (flying)
                StopFly(false);

            UnlockPlayerBody();
            flyMode = null;
            flying = false;
            freeCam = false;
            hasBankYaw = false;
            bankRoll = 0f;
            freeCamAccel = 0f;
            if (Main.HollandAngle != null) Main.HollandAngle.SetFlyBank(0f);
            filteredRate = 0f;
            bankVelocity = 0f;
        }

        private Behaviour lockedFpsController;
        private bool fpsControllerWasEnabled;
        private bool flyModeWasEnabled;

        private void LockPlayerBody()
        {
            controlModeSaved = false;
            controllerSaved = false;
            lockedController = null;
            lockedFpsController = null;

            try
            {
                PlayerManager pm = GameManager.GetPlayerManagerComponent();
                if (pm != null)
                {
                    savedControlMode = pm.GetControlMode();
                    controlModeSaved = true;
                    pm.SetControlMode(PlayerControlMode.Locked);
                }
            }
            catch (System.Exception e)
            {
                MelonLogger.Warning("Free cam: could not lock controls: " + e.Message);
            }

            try
            {
                Transform body = Main.FindPlayer();
                if (body != null)
                {
                    lockedController = body.GetComponent<CharacterController>();
                    if (lockedController != null)
                    {
                        controllerWasEnabled = lockedController.enabled;
                        controllerSaved = true;
                        lockedController.enabled = false;
                    }

                    lockedFpsController = body.GetComponent<vp_FPSController>();
                    if (lockedFpsController == null)
                        lockedFpsController = body.GetComponentInChildren<vp_FPSController>();
                    if (lockedFpsController != null)
                    {
                        fpsControllerWasEnabled = lockedFpsController.enabled;
                        lockedFpsController.enabled = false;
                    }
                }
            }
            catch { }

            EnforcePlayerLock();
        }

        private void EnforcePlayerLock()
        {
            try
            {
                PlayerManager pm = GameManager.GetPlayerManagerComponent();
                if (pm != null && pm.GetControlMode() != PlayerControlMode.Locked)
                    pm.SetControlMode(PlayerControlMode.Locked);
            }
            catch { }

            if (lockedController != null && lockedController.enabled)
                lockedController.enabled = false;

            if (lockedFpsController != null && lockedFpsController.enabled)
                lockedFpsController.enabled = false;
        }

        private void UnlockPlayerBody()
        {
            if (lockedFpsController != null)
            {
                try { lockedFpsController.enabled = fpsControllerWasEnabled; }
                catch { }
            }
            lockedFpsController = null;

            if (controllerSaved && lockedController != null)
            {
                try { lockedController.enabled = controllerWasEnabled; }
                catch { }
            }
            controllerSaved = false;
            lockedController = null;

            if (controlModeSaved)
            {
                try
                {
                    PlayerManager pm = GameManager.GetPlayerManagerComponent();
                    if (pm != null)
                        pm.SetControlMode(savedControlMode);
                }
                catch (System.Exception e)
                {
                    MelonLogger.Warning("independent cam: could not restore controls!!!!!: " + e.Message);
                }
                controlModeSaved = false;
            }
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

            if (Main.LookAt != null && Main.LookAt.IsPlacing)
                return true;

            if (Main.Vertigo != null && Main.Vertigo.IsActive)
                return true;

            if (Main.Greenscreen != null && Main.Greenscreen.IsPlacing)
                return true;

            return false;
        }



    }
}