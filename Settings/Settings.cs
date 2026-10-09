using ModSettings;
using UnityEngine;
using System.Reflection;

namespace DIrectorMode
{
    internal sealed class Settings : JsonModSettings
    {
        public static Settings Instance { get; } = new Settings();

        [Section("Tutorial")] // -------------------------------------------------------------------------------------- Tutorial

        [Name("Open tutorial")]
        [Description("Little wolf Storm will show you how to use the AbsoluteCinema!")]
        public KeyCode tutorialKey = KeyCode.P;

        [Section("Notifications")] // --------------------------------------------------------------------------------- Notifications

        [Name("Show notifications")]
        [Description("Show/Hide all notifications")]
        public bool notifications = true;

        [Section("Cinematic flight")] // ------------------------------------------------------------------------------ Cinematic flight

        [Name("Add path point")]
        [Description("Press to add camera positions to the path")]
        public KeyCode grabPos1Key = KeyCode.B;

        [Name("Finish path")]
        [Description("Finish building the path")]
        public KeyCode grabPos2Key = KeyCode.N;

        [Name("Reset positions")]
        [Description("Clears the recorded path")]
        public KeyCode resetKey = KeyCode.U;

        [Name("Start cinematic flight")]
        [Description("Starts the flight along the finished path. Press again to stop immediately")]
        public KeyCode startKey = KeyCode.V;

        [Name("Movement speed")]
        [Description("Can be changed during movement via mouse wheel")]
        [Slider(0.1f, 50)]
        public int speed = 15;

        [Name("Ease start and stop")]
        [Description("Ease in and out of the flight. Turn off for constant speed")]
        public bool smoothStartStop = true;

        [Name("Ease strength")]
        [Description("How soft the start and stop of the flight are")]
        [Slider(1, 50)]
        public int easeStrength = 15;

        [Name("Path curve amount")]
        [Description("How smoothly the flight curves through the recorded points, 0 follows straight segments")]
        [Slider(0, 1000)]
        public int pathCurveAmount = 350;

        [Name("Show path preview")]
        [Description("Draw the recorded cinematic path in the world")]
        public bool showPathPreview = true;

        [Name("Look along path")]
        [Description("During cinematic flight, turn the camera to face the direction of travel")]
        public bool lookAlongPath = false;

        [Name("Path camera smoothing")]
        [Description("Higher values smooth camera turns more, but make the camera lag farther behind the path")]
        [Slider(1f, 30f)]
        public float pathCameraSmoothing = 20f;

        [Name("Path preview opacity")]
        [Description("Opacity of the path preview, 0.5 is half-visible")]
        [Slider(0.05f, 1f)]
        public float pathPreviewOpacity = 0.5f;

        [Section("Smooth camera")] // --------------------------------------------------------------------------------- Smooth camera

        [Name("Camera smoothing")]
        [Description("Can be changed during movement via mouse wheel")]
        [Slider(1f, 20f, 39)]
        public float smoothSpeed = 20f;

        [Name("Toggle smooth camera")]
        [Description("Smooth camera ON/OFF")]
        public KeyCode cinematicKey = KeyCode.LeftAlt;

        [Section("Zoom")] // ------------------------------------------------------------------------------------------ Zoom

        [Name("Zoom key")]
        [Description("Hold to zoom in, release to zoom back out")]
        public KeyCode zoomKey = KeyCode.Z;

        [Name("Wheel step")]
        [Description("How much each mouse wheel notch changes the zoom")]
        [Slider(5, 50)]
        public int zoomWheelStep = 15;

        [Name("Zoom speed")]
        [Description("How fast the zoom eases in and out")]
        [Slider(1f, 30f)]
        public float zoomSmoothing = 8f;

        [Name("Zoom smoothness")]
        [Description("Camera smoothness while zooming")]
        [Slider(1f, 20f)]
        public float zoomCameraSmoothness = 15f;

        [Section("Look at")] // --------------------------------------------------------------------------------------- Look at

        [Name("Target spawn key")]
        [Description("Hold to enter placing mode. While held, LMB to place the target, Double-tap to reset")]
        public KeyCode lookAtKey = KeyCode.T;

        [Section("Dutch angle")] // ----------------------------------------------------------------------------------- Dutch angle

        [Name("Dutch angle key")]
        [Description("Press wtice to reset the angle")]
        public KeyCode hollandKey = KeyCode.X;

        [Name("Look relative to tilt")]
        [Description("The mouse moves the view along the tilted screen instead of the world axes")]
        public bool hollandRelativeLook = true;

        [Name("Invert relative look")]
        [Description("Turn on if the mouse moves the view the wrong way while the camera is tilted")]
        public bool hollandRelativeInvert = false;

        [Name("Tilt smoothness")]
        [Description("How smoothly the camera tilts")]
        [Slider(1, 100)]
        public int hollandSmoothness = 70;

        [Name("Tilt sensitivity")]
        [Description("How sensetive rotation is")]
        [Slider(0.1f, 3f)]
        public float hollandSensitivity = 1f;

        [Name("Snap to level")]
        [Description("Snaping radius")]
        [Slider(0f, 15f)]
        public float hollandSnap = 3f;

        [Name("Invert tilt direction")]
        //[Description("Turn on if the camera tilts the wrong way when you move the mouse.")]
        public bool hollandInvert = true;

        [Section("Hide HUD")] // -------------------------------------------------------------------------------------- Hide HUD

        [Name("Hide HUD key")]
        [Description("Press to hide the HUD, press again to show it")]
        public KeyCode hideHudKey = KeyCode.L;

        [Section("Vertigo")] // -------------------------------------------------------------------------------------- Vertigo

        [Name("Hold Vertigo effect")]
        [Description("Hold to enable Vertigo, move the mouse forward or back to dolly")]
        public KeyCode vertigoKey = KeyCode.Y;

        [Name("Mouse sensitivity")]
        [Description("Distance moved per unit of vertical mouse movement")]
        [Slider(0.01f, 1f)]
        public float vertigoMouseSensitivity = 0.1f;

        [Name("Movement smoothing")]
        [Description("Higher values make the camera respond more slowly, this does not change dolly distance")]
        [Slider(1f, 20f)]
        public float vertigoSmoothing = 8f;

        [Section("Fly mode")] // -------------------------------------------------------------------------------------- Fly mode

        [Name("Toggle fly mode")]
        [Description("FlyMode ON/OFF")]
        public KeyCode flyKey = KeyCode.R;

        [Name("Fly speed")]
        [Description("Can be changed during movement via mouse wheel")]
        [Slider(0.1f, 20f)]
        public float flySpeedRegular = 0.5f;

        [Name("Bank while turning")]
        [Description("Tilt the camera into turns while FlyMode is active")]
        public bool flyBankEnabled = true;

        [Name("Turn banking strength")]
        [Description("Controls how strongly the camera banks while turning")]
        [Slider(0f, 5f)]
        public float flyBankStrength = 1f;

        [Section("Weather menu")] // ---------------------------------------------------------------------------------- Weather menu

        [Name("Weather menu key")]
        [Description("Open the weather menu")]
        public KeyCode weatherMenuKey = KeyCode.O;

        [Section("Slow Motion")] // ----------------------------------------------------------------------------------- Slow Motion

        [Name("Slow motion key")]
        [Description("Hold + mouse wheel to change time scale. Double-tap to reset.")]
        public KeyCode slowMoKey = KeyCode.H;

        [Name("Time scale step")]
        [Description("Fractional change per wheel notch")]
        [Slider(0.05f, 0.5f)]
        public float slowMoWheelStep = 0.5f;
    }
}
