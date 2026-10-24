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
        public float flySpeedRegular = 2f;

        [Name("Smooth start and stop")]
        [Description("Fly mode speeds up slowly when you press the movement keys and glides to a stop when you release them")]
        public bool flySmoothStartStop = true;

        [Name("Start smoothness")]
        [Description("How slowly you pick up speed (1 = instant, 100 = about 3 seconds)")]
        [Slider(1, 100)]
        public int flyEaseStrength = 55;

        [Name("Stop smoothness")]
        [Description("How long you glide after releasing the keys (1 = stops at once, 100 = about 3 seconds)")]
        [Slider(1, 100)]
        public int flyStopEaseStrength = 65;

        [Name("Fly bank")]
        [Description("Tilt the camera when turning while flying")]
        public bool flyBankEnabled = true;

        [Name("Bank strength")]
        [Description("How much the camera banks when turning in fly mode")]
        [Slider(0.1f, 3f)]
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

        [Section("Motion tracker")]

        [Name("Record / stop")]
        [Description("Press to start recording movement and camera, press again to stop")]
        public KeyCode motionRecordKey = KeyCode.LeftBracket;

        [Name("Play / stop")]
        [Description("Press to replay the last recording, press again to stop")]
        public KeyCode motionPlayKey = KeyCode.RightBracket;

        [Section("Green screen")] // ---------------------------------------------------------------------------------- Green screen

        [Name("Green screen key")]
        [Description("Hold to place a green screen, LMB to place it, press again to remove")]
        public KeyCode greenscreenKey = KeyCode.Semicolon;

        [Name("Increase Width")]
        [Description("Key to make the green screen wider")]
        public KeyCode greenscreenWidthIncreaseKey = KeyCode.RightArrow;

        [Name("Decrease Width")]
        [Description("Key to make the green screen narrower")]
        public KeyCode greenscreenWidthDecreaseKey = KeyCode.LeftArrow;

        [Name("Increase Height")]
        [Description("Key to make the green screen taller")]
        public KeyCode greenscreenHeightIncreaseKey = KeyCode.UpArrow;

        [Name("Decrease Height")]
        [Description("Key to make the green screen shorter")]
        public KeyCode greenscreenHeightDecreaseKey = KeyCode.DownArrow;

        [Name("Rotate key")]
        [Description("Hold while placing, then move the mouse to rotate the green screen in 3D")]
        public KeyCode greenscreenRotateKey = KeyCode.Quote;

        [Name("Rotate sensitivity")]
        [Description("How fast the green screen turns when rotating with the mouse")]
        [Slider(0.1f, 5f)]
        public float greenscreenRotateSensitivity = 1f;

        [Name("Distance wheel step")]
        [Description("How much each mouse wheel notch changes distance while placing (%)")]
        [Slider(1f, 50f)]
        public float greenscreenWheelStep = 15f;
    }
}