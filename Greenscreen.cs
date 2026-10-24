using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace DIrectorMode
{
    public class Greenscreen
    {
        private const float MinDistance = 1f;
        private const float MaxDistance = 100f;
        private const float MinSize = 0.2f;
        private const float MaxSize = 200f;
        private const float ResizeRate = 0.8f;

        private bool placing;
        private bool placed;

        private GameObject root;
        private Material material;

        private float distance = 6f;
        private float width = 6f;
        private float height = 4f;
        private Quaternion rotOffset = Quaternion.identity;
        private Vector2 lockedAngle;
        private bool hasLockedAngle;
        private bool createFailed;

        public bool IsPlacing => placing;
        public bool IsPlaced => placed;
        public bool IsRotating => placing && hasLockedAngle;

        // used by MotionTracker to capture or restroe a placed panel
        public bool TryGetPlacedPose(out Vector3 position, out Quaternion rotation, out float w, out float h)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            w = width;
            h = height;
            if (!placed || root == null)
                return false;
            position = root.transform.position;
            rotation = root.transform.rotation;
            return true;
        }

        public void RemovePlaced()
        {
            placing = false;
            hasLockedAngle = false;
            placed = false;
            DestroyAllPanels();
        }

        public void SpawnPlacedAt(Vector3 position, Quaternion rotation, float w, float h)
        {
            placing = false;
            hasLockedAngle = false;

            width = Mathf.Clamp(w, MinSize, MaxSize);
            height = Mathf.Clamp(h, MinSize, MaxSize);

            try
            {
                if (root == null)
                    CreatePanel();
                if (root == null)
                    return;

                root.transform.position = position;
                root.transform.rotation = rotation;
                ApplySize();
                root.SetActive(true);
                placed = true;
                createFailed = false;
            }
            catch (System.Exception e)
            {
                MelonLogger.Error("Greenscreen: spawn failed: " + e); //debug
            }
        }

        public void Update()
        {
            KeyCode key = Settings.Instance.greenscreenKey;
            if (key == KeyCode.None)
                return;

            // Game is placing a world object – do not fight its input
            if (PlacingState.IsPlacing)
            {
                if (placing) EndPlacement();
                return;
            }

            if (!Input.GetKey(key))
                createFailed = false;

            if (placed && Input.GetKeyDown(key) && !ConsoleState.IsOpen && !UnityExplorerState.IsOpen)
            {
                Clear(true);
                return;
            }

            vp_FPSCamera cam = GameManager.GetVpFPSCamera();
            if (cam == null || cam.m_Camera == null)
            {
                if (placing) EndPlacement();
                return;
            }

            bool canPlace = !placed && !createFailed &&
                            Time.timeScale > 0f &&
                            Main.Tutorial != null &&
                            !Main.Tutorial.IsOpen &&
                            Main.Tutorial.IsFreeRoam() &&
                            !Main.InputBlocked;

            bool hold = canPlace && Main.Key(key);

            if (hold && !placing) BeginPlacement(cam);
            else if (!hold && placing) EndPlacement();

            if (!placing)
                return;

            HandleWheel();
            HandleResize();
            HandleRotate();
            FollowCamera(cam);

            if (Main.MouseDown(0))
                Place();
        }

        public void LateUpdate()
        {
            if (!placing || !hasLockedAngle)
                return;

            vp_FPSCamera cam = GameManager.GetVpFPSCamera();
            if (cam == null)
                return;

            float y = lockedAngle.x;
            float p = lockedAngle.y;
            cam.Angle = new Vector2(y, p);
            cam.m_Yaw = y;
            cam.m_Pitch = p;
            cam.m_CurrentYaw = y;
            cam.m_TargetYaw = y;
            cam.m_CurrentPitch = p;
            cam.m_TargetPitch = p;
        }

        public void Reset()
        {
            Clear(false);
            distance = 6f;
            rotOffset = Quaternion.identity;
        }

        public void CancelPlacement()
        {
            if (!placing) return;
            placing = false;
            hasLockedAngle = false;
            rotOffset = Quaternion.identity;
            if (!placed)
                DestroyPanel();
        }

        private void BeginPlacement(vp_FPSCamera cam)
        {
            placing = true;
            rotOffset = Quaternion.identity;
            hasLockedAngle = false;

            try
            {
                if (root == null)
                    CreatePanel();

                if (root == null)
                {
                    createFailed = true;
                    placing = false;
                    return;
                }

                ApplySize();
                root.SetActive(true);
            }
            catch (System.Exception e)
            {
                MelonLogger.Error("Greenscreen: could not create the panel: " + e); //debug
                createFailed = true;
                placing = false;
                DestroyPanel();
            }
        }

        private void EndPlacement()
        {
            placing = false;
            hasLockedAngle = false;
            if (!placed)
                DestroyPanel();
        }

        private void Place()
        {
            if (root == null) return;

            placing = false;
            placed = true;
            createFailed = false;
            hasLockedAngle = false;
        }

        private void Clear(bool notify)
        {
            placing = false;
            placed = false;
            createFailed = false;
            hasLockedAngle = false;
            rotOffset = Quaternion.identity;
            DestroyAllPanels();
        }

        private void HandleWheel()
        {
            float scroll = Main.Scroll;
            if (scroll == 0f) return;

            float stepPct = 15f;
            try { stepPct = Mathf.Clamp(Settings.Instance.greenscreenWheelStep, 1f, 50f); }
            catch { }

            float step = 1f + stepPct / 100f;
            distance = Mathf.Clamp(distance * Mathf.Pow(step, scroll), MinDistance, MaxDistance);
        }

        private void HandleResize()
        {
            float dw = 0f;
            float dh = 0f;

            Settings s = Settings.Instance;

            if (Main.Key(s.greenscreenWidthIncreaseKey)) dw += 1f;
            if (Main.Key(s.greenscreenWidthDecreaseKey)) dw -= 1f;
            if (Main.Key(s.greenscreenHeightIncreaseKey)) dh += 1f;
            if (Main.Key(s.greenscreenHeightDecreaseKey)) dh -= 1f;

            if (dw == 0f && dh == 0f) return;

            float dt = Time.unscaledDeltaTime;

            width = Mathf.Clamp(width * Mathf.Exp(dw * ResizeRate * dt), MinSize, MaxSize);
            height = Mathf.Clamp(height * Mathf.Exp(dh * ResizeRate * dt), MinSize, MaxSize);

            ApplySize();
        }

        private void HandleRotate()
        {
            Settings s = Settings.Instance;
            KeyCode rotateKey = s.greenscreenRotateKey;
            bool rotateHeld = rotateKey != KeyCode.None && Input.GetKey(rotateKey);

            if (!rotateHeld)
            {
                hasLockedAngle = false;
                return;
            }

            if (!hasLockedAngle)
            {
                vp_FPSCamera cam = GameManager.GetVpFPSCamera();
                if (cam != null)
                {
                    lockedAngle = cam.Angle;
                    hasLockedAngle = true;
                    if (Main.SmoothCamera != null)
                        Main.SmoothCamera.RequestSync();
                }
            }

            float sens = Mathf.Clamp(s.greenscreenRotateSensitivity, 0.1f, 5f) * 4f;
            float mx = Input.GetAxisRaw("Mouse X") * sens;
            float my = Input.GetAxisRaw("Mouse Y") * sens;

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                rotOffset = rotOffset * Quaternion.AngleAxis(-mx, Vector3.forward);
            }
            else
            {
                rotOffset = rotOffset * Quaternion.AngleAxis(mx, Vector3.up);
                rotOffset = rotOffset * Quaternion.AngleAxis(-my, Vector3.right);
            }
        }

        private void FollowCamera(vp_FPSCamera cam)
        {
            if (root == null) return;

            Transform ct = cam.m_Camera.transform;

            Vector3 forward = ct.forward;
            if (forward.sqrMagnitude < 0.0001f) return;

            root.transform.position = ct.position + forward * distance;

            Quaternion faceCam = Quaternion.LookRotation(forward, Vector3.up);
            root.transform.rotation = faceCam * rotOffset;
        }

        private void ApplySize()
        {
            if (root != null)
                root.transform.localScale = new Vector3(width, height, 1f);
        }

        private void CreatePanel()
        {
            if (!EnsureMaterial())
            {
                createFailed = true;
                return;
            }

            root = new GameObject("AbsoluteCinema_Greenscreen");
            root.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(root);

            AddFace(root.transform, 0f, 0f);
            AddFace(root.transform, 180f, 0.01f);
        }

        private void AddFace(Transform parent, float yRotation, float zOffset)
        {
            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            face.name = "Face";

            Collider col = face.GetComponent<Collider>();
            if (col != null)
                UnityEngine.Object.Destroy(col);

            Transform t = face.transform;
            t.SetParent(parent, false);
            t.localRotation = Quaternion.Euler(0f, yRotation, 0f);
            t.localPosition = new Vector3(0f, 0f, zOffset);
            t.localScale = Vector3.one;

            Renderer rend = face.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = material;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
                rend.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                rend.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
        }

        private void DestroyPanel()
        {
            if (root != null && root)
                UnityEngine.Object.Destroy(root);
            root = null;
        }

        private void DestroyPanelImmediate()
        {
            if (root != null)
            {
                try { UnityEngine.Object.DestroyImmediate(root); }
                catch
                {
                    try { UnityEngine.Object.Destroy(root); } catch { }
                }
            }
            root = null;
        }

        private void DestroyAllPanels()
        {
            if (root != null)
            {
                root.hideFlags = HideFlags.None;
                try { UnityEngine.Object.DestroyImmediate(root); }
                catch
                {
                    try { UnityEngine.Object.Destroy(root); } catch { }
                }
                root = null;
            }

            try
            {
                GameObject[] objs = Resources.FindObjectsOfTypeAll<GameObject>();
                for (int i = 0; i < objs.Length; i++)
                {
                    GameObject go = objs[i];
                    if (go == null) continue;
                    if (go.name != "AbsoluteCinema_Greenscreen") continue;

                    go.hideFlags = HideFlags.None;
                    try { UnityEngine.Object.DestroyImmediate(go); }
                    catch
                    {
                        try { UnityEngine.Object.Destroy(go); } catch { }
                    }
                }
            }
            catch (System.Exception e)
            {
                MelonLogger.Warning("Greenscreen: cleanup scan failed: " + e.Message); //debug
            }

            root = null;
            placed = false;
            placing = false;
        }

        private bool EnsureMaterial()
        {
            if (material != null) return true;

            Shader sh = Shader.Find("Unlit/Color");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("UI/Default");
            if (sh == null)
            {
                MelonLogger.Warning("Greenscreen: no unlit shader found!!!");//debug
                sh = Shader.Find("Hidden/InternalErrorShader");
            }

            if (sh == null) return false;

            material = new Material(sh);
            material.color = new Color(0f, 1f, 0f, 1f);
            material.hideFlags = HideFlags.HideAndDontSave;
            return true;
        }
    }
}