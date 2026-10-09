using Il2Cpp;
using MelonLoader;
using ModSettings;
using UnityEngine;

namespace DIrectorMode
{
    //Hey look! Little wolf is still working on this mod, so there might be a pesky bugs uhhggg...
    public class Main : MelonMod
    {
        public static Main Instance { get; private set; }
        internal static PositionManager Positions { get; private set; }
        internal static CinematicFly CinematicFly { get; private set; }
        internal static FastFlyMode FastFlyMode { get; private set; }
        public SmoothCamera Smooth { get; private set; }
        public CamZoom Zoom { get; private set; }
        internal static SmoothCamera SmoothCamera { get; private set; }
        internal static CamZoom CamZoom { get; private set; }
        internal static HollandAngle HollandAngle { get; private set; }
        internal static LookAt LookAt { get; private set; }
        internal static SlowMotion SlowMotion { get; private set; }
        internal static HideHUD HideHUD { get; private set; }
        internal static VertigoControl Vertigo { get; private set; }
        internal static WeatherController WeatherController { get; private set; }
        internal static WeatherMenu WeatherMenu { get; private set; }
        private static bool wasFreeRoam;

        // ignore mod hotkeys while another overlay owns input
        internal static bool InputBlocked => ConsoleState.IsOpen || UnityExplorerState.IsOpen || (WeatherMenu != null && WeatherMenu.IsOpen);

        internal static bool Key(KeyCode key) => !InputBlocked && Input.GetKey(key);
        internal static bool KeyDown(KeyCode key) => !InputBlocked && Input.GetKeyDown(key);
        internal static bool MouseDown(int button) => !InputBlocked && Input.GetMouseButtonDown(button);
        internal static float Scroll => InputBlocked ? 0f : Input.mouseScrollDelta.y;

        internal static Tutorial Tutorial { get; private set; }

        public override void OnInitializeMelon()
        {
            Instance = this;

            Settings.Instance.AddToModSettings("Absolute Cinema");
            if (Settings.Instance.weatherMenuKey == KeyCode.F8)
                Settings.Instance.weatherMenuKey = KeyCode.O;
            if (Settings.Instance.slowMoKey == KeyCode.O)
                Settings.Instance.slowMoKey = KeyCode.F7;

            Positions = new PositionManager();
            CinematicFly = new CinematicFly(Positions);
            FastFlyMode = new FastFlyMode(CinematicFly);
            HollandAngle = new HollandAngle();
            Smooth = new SmoothCamera();
            Zoom = new CamZoom();
            SmoothCamera = Smooth;
            CamZoom = Zoom;
            LookAt = new LookAt();
            SlowMotion = new SlowMotion();
            HideHUD = new HideHUD();
            Tutorial = new Tutorial();
            Vertigo = new VertigoControl();
            WeatherController = new WeatherController();
            WeatherController.Init();
            WeatherMenu = new WeatherMenu(WeatherController);
        }

        public override void OnUpdate()
        {
            UpdateFreeRoamState();
            Settings s = Settings.Instance;
            WeatherMenu.Update();

            if (!ConsoleState.IsOpen && !UnityExplorerState.IsOpen && !Tutorial.IsOpen && Input.GetKeyDown(s.weatherMenuKey))
            {
                WeatherMenu.Toggle();
                return; // same keypress is not trigger another action
            }

            if (WeatherMenu.IsOpen)
            {
                return;
            }

            // tutorial on every frame
            Tutorial.Update();
            if (!Tutorial.IsFreeRoam())
            {
                UpdateFreeRoamState();
                return;
            }
            if (Tutorial.IsOpen) return;

            if (Main.KeyDown(s.cinematicKey))
                SmoothCamera.Toggle();

            if (Main.KeyDown(s.grabPos1Key))
                Positions.AddPathPoint();

            if (Main.KeyDown(s.grabPos2Key))
                Positions.FinishPath();

            if (Main.KeyDown(s.startKey))
            {
                if (CinematicFly.IsMoving)
                    CinematicFly.StopMove("stopped by key");
                else
                    CinematicFly.StartMove();
            }

            if (Main.KeyDown(s.resetKey))
                Positions.ResetPositions();

            if (Main.KeyDown(s.flyKey))
            {
                if (FastFlyMode.IsFlying)
                    FastFlyMode.StopFly();
                else
                    FastFlyMode.StartFly();
            }

            if (Main.KeyDown(s.hideHudKey))
                HideHUD.Toggle();

            Vertigo.Update();
            FastFlyMode.Update();
            SlowMotion.Update();
        }

        public override void OnLateUpdate()
        {
            HideHUD.LateUpdate();

            // tutorial and weather menu pause
            Tutorial.LateUpdate();
            Positions.UpdatePreview();
            if (!Tutorial.IsFreeRoam())
            {
                UpdateFreeRoamState();
                return;
            }
            if (Tutorial.IsOpen) return;

            if (WeatherMenu.IsOpen)
            {
                WeatherMenu.LateUpdate();
                return;
            }

            CinematicFly.Update();
            CamZoom.Update();
            HollandAngle.Update();

            // smooth camera stays out of the way while the tilt tool or a look-at target controls the view
            bool lookAtTracking = LookAt.HasTarget && !LookAt.IsPlacing;
            bool pathCameraFollowing = CinematicFly.IsMoving && Settings.Instance.lookAlongPath;
            if (!HollandAngle.IsLocking && !lookAtTracking && !pathCameraFollowing)
            SmoothCamera.Update();
            LookAt.Update();
            CinematicFly.UpdateCameraDirection();
            FastFlyMode.UpdateBank();
            HollandAngle.EndFrame();
            Vertigo.LateUpdate();
        }

        private static void UpdateFreeRoamState()
        {
            bool freeRoam = Tutorial != null && Tutorial.IsFreeRoam();
            if (freeRoam)
            {
                wasFreeRoam = true;
                return;
            }

            if (WeatherMenu != null && WeatherMenu.IsOpen)
                WeatherMenu.Close();

            DisableFreeRoamTools();
            if (CinematicFly != null)
                CinematicFly.HandlePausedInput();

            if (wasFreeRoam)
            {
                // Clear the menu's persistent world toggles when leaving roam.
                if (WeatherController != null)
                {
                    if (WeatherController.IsWeatherLocked()) WeatherController.UnlockWeather();
                    if (WeatherController.IsWindLocked()) WeatherController.UnlockWind();
                }
            }

            wasFreeRoam = false;
        }

        private static void DisableFreeRoamTools()
        {
            bool vertigoWasActive = Vertigo != null && Vertigo.IsActive;
            if (vertigoWasActive) Vertigo.Reset();
            if (FastFlyMode != null && FastFlyMode.IsFlying) FastFlyMode.Reset();
            if (SmoothCamera != null && SmoothCamera.IsActive) SmoothCamera.Reset();
            if (CamZoom != null && (vertigoWasActive || CamZoom.ZoomFactor > 1.005f)) CamZoom.Reset();
            if (HollandAngle != null && HollandAngle.IsActive) HollandAngle.Reset();
            if (LookAt != null && (LookAt.HasTarget || LookAt.IsPlacing)) LookAt.Reset();
            if (SlowMotion != null && (SlowMotion.CurrentScale != 1f || Time.timeScale > 0f && Time.timeScale != 1f))
                SlowMotion.ResetForPanel();
            if (HideHUD != null && HideHUD.IsHidden) HideHUD.Reset();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            SmoothCamera.Reset();
            Vertigo.Reset();
            CamZoom.Reset();
            FastFlyMode.Reset();
            HollandAngle.Reset();
            LookAt.Reset();
            SlowMotion.Reset();
            HideHUD.Reset();

            if (CinematicFly.IsMoving)
                CinematicFly.StopMove("scene changed");
            if (Tutorial.IsOpen) Tutorial.Close();
            WeatherMenu.Close();
            // all points belong to one scene
            if (sceneName == UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)
            {
                Positions.ResetPositions(false);
                Positions.DestroyPreview();
            }
        }

        internal static void Notify(string text)
        {
            MelonLogger.Msg(text);

            if (!Settings.Instance.notifications)
                return;

            try
            {
                HUDMessage.AddMessage(text, false, true);
            }
            catch (System.Exception e)
            {
                MelonLogger.Warning("HUD message failed: " + e.Message);
            }
        }

        // WorldView
        internal static Transform FindPlayer()
        {
            vp_FPSCamera cam = GameManager.GetVpFPSCamera();
            if (cam != null && cam.Controller != null) return cam.Controller.transform;

            GameObject go = GameObject.Find("WorldView");
            return go != null ? go.transform : null;
        }

        public override void OnGUI()
        {
            if (Tutorial != null) Tutorial.OnGUI();
            if (WeatherMenu != null) WeatherMenu.OnGUI();
        }
    }
}
