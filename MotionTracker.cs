using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace DIrectorMode
{
    // Records player motion relative to the start pose, then replays it from
    // wherever the player is standing (survives scene changes).
    public class MotionTracker
    {
        private struct Sample
        {
            public float time;
            public Vector3 localPosition; // relative to recording origin
            public float localYaw;        // relative to origin yaw
            public float pitch;
        }

        private readonly List<Sample> samples = new List<Sample>(4096);
        private bool recording;
        private bool playing;
        private float recordStartUnscaled;
        private float playStartUnscaled;
        private float duration;
        private int playIndex;

        // Recording origin (world) – used only while recording
        private Vector3 recordOriginPos;
        private float recordOriginYaw;

        // Playback origin – current player pose when play starts
        private Vector3 playOriginPos;
        private float playOriginYaw;

        // Greenscreen captured with the clip (relative to record origin)
        private bool hasGreenscreen;
        private Vector3 gsLocalPosition;
        private Quaternion gsLocalRotation;
        private float gsWidth;
        private float gsHeight;

        private CharacterController controller;
        private bool controllerWasEnabled;
        private Transform body;

        public bool IsRecording => recording;
        public bool IsPlaying => playing;
        public int SampleCount => samples.Count;
        public float Duration => duration;

        public void Update()
        {
            Settings s = Settings.Instance;

            KeyCode recKey = s.motionRecordKey;
            KeyCode playKey = s.motionPlayKey;

            // Raw input so record/play still work during game placing (InputBlocked)
            if (recKey != KeyCode.None && Input.GetKeyDown(recKey)
                && !ConsoleState.IsOpen && !UnityExplorerState.IsOpen)
            {
                if (recording)
                    StopRecording();
                else if (!playing)
                    StartRecording();
            }

            if (playKey != KeyCode.None && Input.GetKeyDown(playKey)
                && !ConsoleState.IsOpen && !UnityExplorerState.IsOpen)
            {
                if (playing)
                    StopPlayback(false);
                else if (!recording)
                    StartPlayback();
            }

            // U clears the clip only when not blocked by placing/menus
            if (Main.KeyDown(s.resetKey) && !recording && !playing)
                ClearClip(true);
        }

        public void LateUpdate()
        {
            if (recording)
                CaptureSample();
            else if (playing)
                ApplyPlayback();
        }

        public void Reset()
        {
            StopActive();
            samples.Clear();
            duration = 0f;
            body = null;
            controller = null;
        }

        public void StopActive()
        {
            if (recording) StopRecording();
            if (playing) StopPlayback(false);
        }

        public void ClearClip(bool notify)
        {
            StopActive();
            samples.Clear();
            duration = 0f;
            hasGreenscreen = false;
            //if (notify)
            //    Main.Notify("Motion clip cleared");
        }

        // -----------------------------------------------------------------

        private void StartRecording()
        {
            if (Main.Tutorial == null || !Main.Tutorial.IsFreeRoam())
            {
                Main.Notify("Motion record only works in free roam");
                return;
            }

            if (playing)
                StopPlayback(false);

            body = ResolveBody();
            vp_FPSCamera cam = GameManager.GetVpFPSCamera();
            if (body == null || cam == null)
            {
                //Main.Notify("Motion record: player not found"); //debug
                return;
            }

            samples.Clear();
            duration = 0f;
            hasGreenscreen = false;
            recordOriginPos = body.position;
            recordOriginYaw = cam.Angle.x;
            recordStartUnscaled = Time.unscaledTime;
            CaptureGreenscreenRelative();
            recording = true;
            CaptureSample();
            Main.Notify("Recording");
        }

        private void StopRecording()
        {
            if (!recording) return;
            recording = false;

            // If GS was placed after record started, still capture it
            if (!hasGreenscreen)
                CaptureGreenscreenRelative();

            if (samples.Count > 0)
                duration = samples[samples.Count - 1].time;
            else
                duration = 0f;

            Main.Notify("Motion saved: " + samples.Count + " frames, " +
                        duration.ToString("0.00") + "s" +
                        (hasGreenscreen ? " + greenscreen" : ""));
        }

        private void CaptureSample()
        {
            body = ResolveBody();
            vp_FPSCamera cam = GameManager.GetVpFPSCamera();
            if (body == null || cam == null)
            {
                StopRecording();
                return;
            }

            Quaternion invYaw = Quaternion.Euler(0f, -recordOriginYaw, 0f);
            Vector3 worldDelta = body.position - recordOriginPos;

            Sample s;
            s.time = Time.unscaledTime - recordStartUnscaled;
            s.localPosition = invYaw * worldDelta;
            s.localYaw = Mathf.DeltaAngle(recordOriginYaw, cam.Angle.x);
            s.pitch = cam.Angle.y;
            samples.Add(s);
        }

        private void StartPlayback()
        {
            if (samples.Count < 2)
            {
                Main.Notify("No motion recorded");
                return;
            }

            if (Main.Tutorial == null || !Main.Tutorial.IsFreeRoam())
            {
                Main.Notify("Motion playback only works in free roam");
                return;
            }

            if (Main.CinematicFly != null && Main.CinematicFly.IsMoving)
                Main.CinematicFly.StopMove("stopped by motion playback");

            if (Main.FastFlyMode != null && Main.FastFlyMode.IsFlying)
                Main.FastFlyMode.StopFly(false);

            body = ResolveBody();
            vp_FPSCamera cam = GameManager.GetVpFPSCamera();
            if (body == null || cam == null)
            {
                //Main.Notify("Motion playback: player not found"); //debug
                return;
            }

            // Anchor playback to where the player is standing now
            playOriginPos = body.position;
            playOriginYaw = cam.Angle.x;

            controller = body.GetComponent<CharacterController>();
            if (controller != null)
            {
                controllerWasEnabled = controller.enabled;
                controller.enabled = false;
            }

            playIndex = 0;
            playStartUnscaled = Time.unscaledTime;
            playing = true;

            ApplySample(samples[0]);

            if (Main.Greenscreen != null)
                Main.Greenscreen.RemovePlaced();
            if (Main.SmoothCamera != null)
                Main.SmoothCamera.RequestSync();
        }

        private void StopPlayback(bool finished)
        {
            if (!playing) return;
            playing = false;

            if (controller != null)
            {
                controller.enabled = controllerWasEnabled;
                controller = null;
            }

            if (Main.SmoothCamera != null)
                Main.SmoothCamera.RequestSync();
        }

        private void ApplyPlayback()
        {
            float t = Time.unscaledTime - playStartUnscaled;
            if (t >= duration)
            {
                ApplySample(samples[samples.Count - 1]);
                StopPlayback(true);
                return;
            }

            while (playIndex < samples.Count - 2 && samples[playIndex + 1].time < t)
                playIndex++;

            Sample a = samples[playIndex];
            Sample b = samples[Mathf.Min(playIndex + 1, samples.Count - 1)];
            float span = b.time - a.time;
            float u = span > 0.0001f ? Mathf.Clamp01((t - a.time) / span) : 0f;

            Sample mixed;
            mixed.time = t;
            mixed.localPosition = Vector3.Lerp(a.localPosition, b.localPosition, u);
            mixed.localYaw = Mathf.LerpAngle(a.localYaw, b.localYaw, u);
            mixed.pitch = Mathf.Lerp(a.pitch, b.pitch, u);
            ApplySample(mixed);
        }

        private void ApplySample(Sample s)
        {
            Quaternion yawRot = Quaternion.Euler(0f, playOriginYaw, 0f);
            Vector3 worldPos = playOriginPos + yawRot * s.localPosition;
            float worldYaw = playOriginYaw + s.localYaw;
            float worldPitch = s.pitch;

            body = ResolveBody();
            if (body != null)
                body.position = worldPos;

            vp_FPSCamera cam = GameManager.GetVpFPSCamera();
            if (cam == null) return;

            cam.Angle = new Vector2(worldYaw, worldPitch);
            cam.m_Yaw = worldYaw;
            cam.m_Pitch = worldPitch;
            cam.m_CurrentYaw = worldYaw;
            cam.m_TargetYaw = worldYaw;
            cam.m_CurrentPitch = worldPitch;
            cam.m_TargetPitch = worldPitch;
        }

        private void CaptureGreenscreenRelative()
        {
            hasGreenscreen = false;
            if (Main.Greenscreen == null)
                return;

            Vector3 pos;
            Quaternion rot;
            float w, h;
            if (!Main.Greenscreen.TryGetPlacedPose(out pos, out rot, out w, out h))
                return;

            Quaternion invYaw = Quaternion.Euler(0f, -recordOriginYaw, 0f);
            gsLocalPosition = invYaw * (pos - recordOriginPos);
            gsLocalRotation = invYaw * rot;
            gsWidth = w;
            gsHeight = h;
            hasGreenscreen = true;
        }

        private void SpawnGreenscreenRelative() //old
        {
            if (!hasGreenscreen || Main.Greenscreen == null)
                return;

            Quaternion yawRot = Quaternion.Euler(0f, playOriginYaw, 0f);
            Vector3 worldPos = playOriginPos + yawRot * gsLocalPosition;
            Quaternion worldRot = yawRot * gsLocalRotation;
            Main.Greenscreen.SpawnPlacedAt(worldPos, worldRot, gsWidth, gsHeight);
        }

        private static Transform ResolveBody()
        {
            return Main.FindPlayer();
        }
    }
}