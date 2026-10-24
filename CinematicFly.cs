using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace DIrectorMode
{
    public class CinematicFly
    {
        private readonly PositionManager positions;

        private float progress;
        private float distanceAlongPath;
        private float totalDistance;
        private float currentSpeed;
        private int lastSettingSpeed;
        private List<Vector3> flightSamples;
        private List<float> sampleDistances;
        private const float SpeedStep = 1.1f;
        private const float SpeedMin = 0.1f;
        private const float SpeedMax = 200f;
        private bool moving;
        private bool wasFollowingPath;
        private Vector4 savedBobAmplitude;
        private bool savedDisableSway;
        private bool swaySuppressed;
        private Transform player;
        private Vector3 cameraOffsetFromPlayer;
        private CharacterController controller;
        private Vector3 playerPositionBeforeFlight;
        private bool hasSavedPlayerPosition;
        public bool IsMoving => moving;
        private bool hasSpeedBeenSet;

        public CinematicFly(PositionManager positions)
        {
            this.positions = positions;
        }

        // Let's flyyyyyy
        public void StartMove()
        {
            progress = 0f;
            distanceAlongPath = 0f;

            if (!hasSpeedBeenSet)
            {
                lastSettingSpeed = Settings.Instance.speed;
                currentSpeed = Mathf.Max(1, lastSettingSpeed);
                hasSpeedBeenSet = true;
            }
            else if (Settings.Instance.speed != lastSettingSpeed)
            {
                lastSettingSpeed = Settings.Instance.speed;
                currentSpeed = Mathf.Max(1, lastSettingSpeed);
            }

            if (Main.Tutorial == null || !Main.Tutorial.IsFreeRoam())
            {
                Main.Notify("Cinematic flight can only start in free roam");
                return;
            }

            if (!positions.IsPathFinalized || positions.Points.Count < 2)
            {
                Main.Notify(
                    "Add path points with " + Settings.Instance.grabPos1Key +
                    ", then press " + Settings.Instance.grabPos2Key + " to finish the path");
                return;
            }
            if (!IsZooming() && !(Main.Vertigo?.IsActive ?? false) && !(Main.Greenscreen?.IsPlacing ?? false))// debug
                player = Main.FindPlayer();
            if (player == null)
            {
                MelonLogger.Warning("WorldView not found ..."); // debug
                return;
            }


            flightSamples = positions.BuildPathSamples(Settings.Instance.pathCurveAmount);
            sampleDistances = new List<float>(flightSamples.Count);
            totalDistance = 0f;
            sampleDistances.Add(0f);
            for (int i = 1; i < flightSamples.Count; i++)
            {
                totalDistance += Vector3.Distance(flightSamples[i - 1], flightSamples[i]);
                sampleDistances.Add(totalDistance);
            }

            // if points are too close
            if (totalDistance < 0.01f)
            {
                flightSamples = null;
                sampleDistances = null;
                Main.Notify("Path points are too close together");
                return;
            }

            if (Main.FastFlyMode.IsFlying)
                Main.FastFlyMode.StopFly(false);
            if (Main.Vertigo != null && Main.Vertigo.IsActive)
                Main.Vertigo.Reset();

            if (Settings.Instance.lookAlongPath && Main.LookAt != null)
                Main.LookAt.Reset();

            vp_FPSCamera camera = GameManager.GetVpFPSCamera();
            cameraOffsetFromPlayer = camera != null
                ? camera.transform.position - player.position
                : Vector3.zero;

            playerPositionBeforeFlight = player.position;
            hasSavedPlayerPosition = true;

            progress = 0f;
            distanceAlongPath = 0f;

            controller = player.GetComponent<CharacterController>();
            if (controller != null)
                controller.enabled = false;

            player.position = positions.Points[0] - cameraOffsetFromPlayer;
            Main.SmoothCamera.RequestSync();
            moving = true;
            SuppressCameraSway(true);

            //MelonLogger.Msg(
            //    "Flight started: " + totalDistance.ToString("F1") + " m, " +
            //    (totalDistance / currentSpeed).ToString("F1") + " s, " +
            //    positions.Points.Count + " points");
        }

        public void Update()
        {
            if (!moving)
                return;

            if (Main.Tutorial == null || !Main.Tutorial.IsFreeRoam())
            {
                HandlePausedInput();
                return;
            }

            if (player == null)
            {
                StopMove("!!DEBUG!! player not found");
                return;
            }

            Settings s = Settings.Instance;

            if (!IsZooming() && !(Main.Vertigo != null && Main.Vertigo.IsActive))
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

            progress = Mathf.Clamp01(progress + currentSpeed * Time.deltaTime / totalDistance);
            float easedProgress = s.smoothStartStop
                ? Ease(progress, s.easeStrength)
                : progress;

            SuppressCameraSway(true);
            distanceAlongPath = easedProgress * totalDistance;
            player.position = EvaluatePath(distanceAlongPath) - cameraOffsetFromPlayer;

            if (progress >= 1f)
                StopMove("finished");
        }

        public void HandlePausedInput()
        {
            if (!moving)
                return;

            if (Main.KeyDown(KeyCode.Escape) || Main.KeyDown(Settings.Instance.startKey))
                StopMove("stopped by key");
        }

        // returns true if zoom is active either way
        private static bool IsZooming()
        {
            if (Main.Key(Settings.Instance.zoomKey))
                return true;

            return Main.CamZoom != null && Main.CamZoom.ZoomFactor > 1.005f;
        }

        public void StopMove(string reason)
        {
            moving = false;
            SuppressCameraSway(false);

            // if scene changed
            if (hasSavedPlayerPosition && player != null && reason != "scene changed")
                player.position = playerPositionBeforeFlight;
            hasSavedPlayerPosition = false;

            Main.SmoothCamera.RequestSync();

            if (controller != null)
                controller.enabled = true;

            flightSamples = null;
            sampleDistances = null;
            controller = null;
            player = null;

            MelonLogger.Msg("Flight " + reason);
        }

        //uuuuh I need a drink! coffee would be nice! Does wolves even drink coffee? I don't think so. But I do :3
        private Vector3 EvaluatePath(float distanceAlongPath)
        {
            if (flightSamples == null || flightSamples.Count == 0 || sampleDistances == null)
                return player != null ? player.position : Vector3.zero;

            if (distanceAlongPath <= 0f)
                return flightSamples[0];
            if (distanceAlongPath >= totalDistance)
                return flightSamples[flightSamples.Count - 1];

            int low = 0;
            int high = sampleDistances.Count - 1;
            while (low < high)
            {
                int middle = (low + high) / 2;
                if (sampleDistances[middle] < distanceAlongPath)
                    low = middle + 1;
                else
                    high = middle;
            }

            int next = low;
            int previous = Mathf.Max(0, next - 1);
            float segmentLength = sampleDistances[next] - sampleDistances[previous];
            float t = segmentLength > 0.0001f
                ? (distanceAlongPath - sampleDistances[previous]) / segmentLength
                : 0f;
            return Vector3.Lerp(flightSamples[previous], flightSamples[next], t);
        }

        public void UpdateCameraDirection()
        {
            if (!moving || !Settings.Instance.lookAlongPath || flightSamples == null || flightSamples.Count < 2)
            {
                if (wasFollowingPath)
                    Main.SmoothCamera.RequestSync();
                wasFollowingPath = false;
                return;
            }

            vp_FPSCamera cam = GameManager.GetVpFPSCamera();
            if (cam == null || sampleDistances == null)
                return;

            wasFollowingPath = true;
            const float tangentDistance = 0.75f;
            Vector3 behind = EvaluatePath(Mathf.Max(0f, distanceAlongPath - tangentDistance));
            Vector3 ahead = EvaluatePath(Mathf.Min(totalDistance, distanceAlongPath + tangentDistance));
            Vector3 direction = ahead - behind;
            if (direction.sqrMagnitude < 0.0001f)
                return;

            Vector3 euler = Quaternion.LookRotation(direction.normalized, Vector3.up).eulerAngles;
            float pitch = euler.x;
            if (pitch > 180f) pitch -= 360f;
            pitch = -pitch; // vp_FPSCamera!
            pitch = Mathf.Clamp(pitch, -85f, 85f);

            float currentYaw = cam.Angle.x;
            float currentPitch = cam.Angle.y;
            float smoothing = Mathf.Clamp(Settings.Instance.pathCameraSmoothing, 1f, 20f);
            float followRate = Mathf.Lerp(30f, 4f, (smoothing - 1f) / 19f);
            float blend = 1f - Mathf.Exp(-followRate * Time.unscaledDeltaTime);
            float yaw = Mathf.LerpAngle(currentYaw, euler.y, blend);
            pitch = Mathf.Lerp(currentPitch, pitch, blend);

            cam.Angle = new Vector2(yaw, pitch);
            cam.m_Yaw = yaw;
            cam.m_Pitch = pitch;
            cam.m_CurrentYaw = yaw;
            cam.m_TargetYaw = yaw;
            cam.m_CurrentPitch = pitch;
            cam.m_TargetPitch = pitch;
        }

        private void SuppressCameraSway(bool on)
        {
            vp_FPSCamera cam = GameManager.GetVpFPSCamera();
            if (cam == null) return;

            if (on)
            {
                if (!swaySuppressed)
                {
                    savedBobAmplitude = cam.BobAmplitude;
                    savedDisableSway = vp_FPSCamera.m_DisableAmbientSway;
                    swaySuppressed = true;
                }

                cam.BobAmplitude = Vector4.zero;
                vp_FPSCamera.m_DisableAmbientSway = true;
            }
            else if (swaySuppressed)
            {
                cam.BobAmplitude = savedBobAmplitude;
                vp_FPSCamera.m_DisableAmbientSway = savedDisableSway;
                swaySuppressed = false;
            }
        }

        private static float Ease(float t, int strength)
        {
            float a = 1f + (Mathf.Clamp(strength, 1, 100) - 1) / 99f * 4f;
            float ta = Mathf.Pow(t, a);
            float ua = Mathf.Pow(1f - t, a);
            return ta / (ta + ua);
        }
    }
}
