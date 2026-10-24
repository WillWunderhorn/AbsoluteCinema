using Il2Cpp;
using UnityEngine;

namespace DIrectorMode
{
    public class VertigoControl
    {
        private const float MinDistance = 0.1f;
        private const float MaxDistance = 300f;
        private const float DefaultTargetDistance = 1f;
        private const float VertigoNearClip = 0.01f;
        private const float CameraCollisionRadius = 0.12f;
        private const float MaxVertigoFov = 130f; //FOV
        private bool active;
        private float startDistance;
        private float targetDistance;
        private float shownDistance;
        private float minimumDistance = MinDistance;
        private float startZoomFactor = 1f;
        private float lastNotify;
        private Vector3 targetPoint;
        private Vector3 directionToTarget;
        private Transform cameraRig;
        private Transform cameraEye;
        private Camera renderCamera;
        private Vector3 originalRigPosition;
        private Vector3 originalEyePosition;
        private Vector2 originalCameraAngle;
        private float originalNearClip;

        public bool IsActive => active;

        public void Update()
        {
            DisableIfUnavailable();

            vp_FPSCamera cam = GameManager.GetVpFPSCamera();
            if (active && (cam == null || cam.m_Camera == null || cameraRig == null || cameraEye == null))
            {
                Disable(false);
                return;
            }

            bool keyHeld = Main.Key(Settings.Instance.vertigoKey);
            if (keyHeld && !active)
            {
                Enable(cam);
            }
            else if (!keyHeld && active)
            {
                Disable(true);
            }

            if (!active)
                return;

            // mouse Y
            float mouseY = Input.GetAxisRaw("Mouse Y");
            if (Mathf.Abs(mouseY) > 0.001f)
            {
                float sensitivity = Mathf.Clamp(Settings.Instance.vertigoMouseSensitivity, 0.01f, 1f);
                targetDistance = Mathf.Clamp(targetDistance - mouseY * sensitivity, minimumDistance, MaxDistance);

                if (Time.unscaledTime - lastNotify > 0.25f)
                {
                    lastNotify = Time.unscaledTime;
                    //Main.Notify("Vertigo distance: " + targetDistance.ToString("0.0") + " m");
                }
            }
        }

        public void LateUpdate()
        {
            if (!active)
                return;

            DisableIfUnavailable();
            if (!active)
                return;

            vp_FPSCamera cam = GameManager.GetVpFPSCamera();
            if (cam == null || cam.m_Camera == null || cameraRig == null || cameraEye == null)
            {
                Disable(false);
                return;
            }

            float smoothing = Mathf.Clamp(Settings.Instance.vertigoSmoothing, 1f, 20f);
            float followRate = Mathf.Lerp(24f, 2f, (smoothing - 1f) / 19f);
            float blend = 1f - Mathf.Exp(-followRate * Time.unscaledDeltaTime);
            float requestedDistance = Mathf.Lerp(shownDistance, targetDistance, blend);

            Vector3 desiredEyePosition = targetPoint - directionToTarget * requestedDistance;
            Vector3 currentEyePosition = cameraEye.position;
            Vector3 delta = desiredEyePosition - currentEyePosition;
            float deltaLength = delta.magnitude;
            if (deltaLength > 0.001f)
            {
                float collisionRadius = Main.FastFlyMode != null && Main.FastFlyMode.IsFlying
                    ? VertigoNearClip
                    : CameraCollisionRadius;
                RaycastHit obstruction;
                if (Physics.SphereCast(currentEyePosition, collisionRadius, delta / deltaLength,
                    out obstruction, deltaLength, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    float safeDistance = Mathf.Max(0f, obstruction.distance - collisionRadius);
                    desiredEyePosition = currentEyePosition + (delta / deltaLength) * safeDistance;
                }
            }

            delta = desiredEyePosition - currentEyePosition;
            shownDistance = Mathf.Max(minimumDistance, Vector3.Distance(targetPoint, desiredEyePosition));

            if (cameraEye.IsChildOf(cameraRig))
                cameraRig.position += delta;
            else
                cameraEye.position = desiredEyePosition;

            if (renderCamera != null)
                renderCamera.nearClipPlane = VertigoNearClip;

            SetAllAngles(cam, originalCameraAngle);

            float currentZoomFactor = Main.CamZoom != null
                ? Mathf.Max(0.01f, Main.CamZoom.ZoomFactor)
                : 1f;
            float fovScale = (startDistance / Mathf.Max(minimumDistance, shownDistance)) *
                             (currentZoomFactor / startZoomFactor);
            if (Main.CamZoom != null)
                Main.CamZoom.SetVertigoFovScale(fovScale);
        }

        public void Reset()
        {
            Disable(false);
        }

        public void DisableIfUnavailable()
        {
            if (active && !IsFreeRoam())
                Disable(true);
        }

        private static bool IsFreeRoam()
        {
            return Main.Tutorial != null && Main.Tutorial.IsFreeRoam();
        }

        private void Enable(vp_FPSCamera cam)
        {
            if (!IsFreeRoam())
            {
                Main.Notify("Vertigo is only available in free roam");
                return;
            }

            if (cam == null || cam.m_Camera == null)
                return;

            if (Main.CinematicFly != null && Main.CinematicFly.IsMoving)
            {
                Main.Notify("Vertigo can't be started during cinematic flight");
                return;
            }

            cameraRig = cam.transform;
            cameraEye = cam.m_Camera.transform;
            renderCamera = cam.m_Camera;
            originalRigPosition = cameraRig.position;
            originalEyePosition = cameraEye.position;
            originalCameraAngle = cam.Angle;
            originalNearClip = renderCamera.nearClipPlane;
            renderCamera.nearClipPlane = VertigoNearClip;

            Vector3 eyePosition = cameraEye.position;
            bool hadLookAtTarget = Main.LookAt != null && Main.LookAt.HasTarget;
            if (hadLookAtTarget)
            {
                targetPoint = Main.LookAt.TargetPoint;
                Main.LookAt.Reset();
            }
            else
            {
                if (Main.LookAt != null && Main.LookAt.IsPlacing)
                    Main.LookAt.Reset();

                RaycastHit hit;
                if (Physics.Raycast(eyePosition, cameraEye.forward, out hit, MaxDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    targetPoint = hit.point;
                else
                    targetPoint = eyePosition + cameraEye.forward * DefaultTargetDistance;
            }

            Vector3 toTarget = targetPoint - eyePosition;
            if (toTarget.sqrMagnitude < 0.0001f)
                toTarget = cameraEye.forward * DefaultTargetDistance;
            directionToTarget = toTarget.normalized;
            startDistance = Mathf.Clamp(toTarget.magnitude, MinDistance, MaxDistance);
            targetDistance = shownDistance = startDistance;

            startZoomFactor = Main.CamZoom != null ? Mathf.Max(0.01f, Main.CamZoom.ZoomFactor) : 1f;
            if (Main.CamZoom != null)
                Main.CamZoom.CaptureBaseFov(cam);

            float baseFov = Main.CamZoom != null ? Main.CamZoom.BaseFov : cam.m_Camera.fieldOfView;
            float baseTangent = Mathf.Max(0.01f, Mathf.Tan(baseFov * 0.5f * Mathf.Deg2Rad));
            float maxScaleAtStart = Mathf.Tan(MaxVertigoFov * 0.5f * Mathf.Deg2Rad) / baseTangent;
            minimumDistance = Mathf.Clamp(startDistance / Mathf.Max(1f, maxScaleAtStart * startZoomFactor), MinDistance, startDistance);

            active = true;
        }

        private void Disable(bool notify)
        {
            if (active)
            {
                vp_FPSCamera cam = GameManager.GetVpFPSCamera();
                if (cam != null && cam.m_Camera != null)
                    SetAllAngles(cam, originalCameraAngle);

                if (cameraRig != null)
                    cameraRig.position = originalRigPosition;
                if (cameraEye != null && (cameraRig == null || !cameraEye.IsChildOf(cameraRig)))
                    cameraEye.position = originalEyePosition;
                if (renderCamera != null)
                    renderCamera.nearClipPlane = originalNearClip;
            }

            active = false;
            cameraRig = null;
            cameraEye = null;
            renderCamera = null;

            if (Main.CamZoom != null)
                Main.CamZoom.SetVertigoFovScale(1f);

            if (notify);
                //Main.Notify("Vertigo off");
        }

        private static void SetAllAngles(vp_FPSCamera cam, Vector2 angle)
        {
            cam.Angle = angle;
            cam.m_Yaw = angle.x;
            cam.m_Pitch = angle.y;
            cam.m_CurrentYaw = angle.x;
            cam.m_TargetYaw = angle.x;
            cam.m_CurrentPitch = angle.y;
            cam.m_TargetPitch = angle.y;
        }
    }
}
