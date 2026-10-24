using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;

namespace DIrectorMode
{
    public class PositionManager
    {
        private readonly List<Vector3> points = new List<Vector3>();
        private bool pathFinalized;

        private GameObject previewObject;
        private LineRenderer previewLine;
        private Material previewMaterial;
        private bool previewDirty = true;
        private int previewCurveAmount = -1;
        private int previewPointCount = -1;
        private readonly List<Vector3> previewSampleBuffer = new List<Vector3>(2048);

        public IReadOnlyList<Vector3> Points => points;
        public bool HasPosition1 => points.Count >= 1;
        public bool HasPosition2 => pathFinalized && points.Count >= 2;
        public bool IsPathFinalized => pathFinalized;
        public Vector3 Position1 => points.Count > 0 ? points[0] : Vector3.zero;
        public Vector3 Position2 => points.Count > 0 ? points[points.Count - 1] : Vector3.zero;

        public void AddPathPoint()
        {
            if (Main.CinematicFly.IsMoving)
            {
                Main.Notify("Can't add a point during a flight");
                return;
            }

            vp_FPSCamera camera = GameManager.GetVpFPSCamera();
            Transform viewpoint = camera != null ? camera.transform : Main.FindPlayer();

            if (viewpoint == null)
            {
                MelonLogger.Warning("FPSCamera and WorldView were not found (are you in a scene?)");
                return;
            }

            if (pathFinalized)
            {
                points.Clear();
                pathFinalized = false;
            }

            Vector3 position = viewpoint.position;
            if (points.Count > 0 && Vector3.Distance(points[points.Count - 1], position) < 0.05f)
            {
                Main.Notify("Move the camera before adding the next point");
                return;
            }

            points.Add(position);
            previewDirty = true;
            Main.Notify(
                "Path point " + points.Count + " saved: " +
                position.x.ToString("F1") + " " +
                position.y.ToString("F1") + " " +
                position.z.ToString("F1"));
        }

        public void FinishPath()
        {
            if (Main.CinematicFly.IsMoving)
            {
                Main.Notify("Can't finish a path during a flight");
                return;
            }

            if (points.Count < 2)
            {
                Main.Notify("Add at least two path points first");
                return;
            }

            pathFinalized = true;
            previewDirty = true;
            Main.Notify("Path ready: " + points.Count + " points");
        }

        public List<Vector3> BuildPathSamples(int curveAmount, int samplesPerSegment = 24)
        {
            var samples = new List<Vector3>();
            if (points.Count < 2)
                return samples;

            float curve = Mathf.Clamp(curveAmount, 0, 100) / 100f;
            samples.Add(points[0]);

            for (int segment = 0; segment < points.Count - 1; segment++)
            {
                Vector3 p0 = points[Mathf.Max(0, segment - 1)];
                Vector3 p1 = points[segment];
                Vector3 p2 = points[segment + 1];
                Vector3 p3 = points[Mathf.Min(points.Count - 1, segment + 2)];

                for (int step = 1; step <= samplesPerSegment; step++)
                {
                    float t = step / (float)samplesPerSegment;
                    Vector3 straight = Vector3.Lerp(p1, p2, t);
                    Vector3 curved = CatmullRom(p0, p1, p2, p3, t);
                    samples.Add(Vector3.Lerp(straight, curved, curve));
                }
            }

            return samples;
        }

        private void FillPathSamples(List<Vector3> output, int curveAmount, int samplesPerSegment = 24)
        {
            output.Clear();

            if (points.Count < 2)
                return;

            float curve = Mathf.Clamp(curveAmount, 0, 100) / 100f;
            output.Add(points[0]);

            for (int segment = 0; segment < points.Count - 1; segment++)
            {
                Vector3 p0 = points[Mathf.Max(0, segment - 1)];
                Vector3 p1 = points[segment];
                Vector3 p2 = points[segment + 1];
                Vector3 p3 = points[Mathf.Min(points.Count - 1, segment + 2)];

                for (int step = 1; step <= samplesPerSegment; step++)
                {
                    float t = step / (float)samplesPerSegment;
                    Vector3 straight = Vector3.Lerp(p1, p2, t);
                    Vector3 curved = CatmullRom(p0, p1, p2, p3, t);
                    output.Add(Vector3.Lerp(straight, curved, curve));
                }
            }
        }

        public void UpdatePreview()
        {
            Settings settings = Settings.Instance;
            if (!settings.showPathPreview || points.Count < 2 || Main.CinematicFly.IsMoving)
            {
                if (previewLine != null) previewLine.enabled = false;
                return;
            }

            if (!EnsurePreview())
                return;

            int curveAmount = settings.pathCurveAmount;
            if (previewDirty || previewCurveAmount != curveAmount)
            {
                FillPathSamples(previewSampleBuffer, curveAmount);
                previewLine.positionCount = previewSampleBuffer.Count;
                for (int i = 0; i < previewSampleBuffer.Count; i++)
                    previewLine.SetPosition(i, previewSampleBuffer[i]);

                previewCurveAmount = curveAmount;
                previewDirty = false;
            }

            float opacity = Mathf.Clamp01(settings.pathPreviewOpacity);
            Color pathColor = new Color(1f, 0.78f, 0.25f, opacity);
            previewLine.startColor = pathColor;
            previewLine.endColor = pathColor;
            previewLine.enabled = true;        // -3 hours if my life
        }

        public void ResetPositions(bool notify = true)
        {
            if (Main.CinematicFly.IsMoving)
                Main.CinematicFly.StopMove("stopped by reset");

            points.Clear();
            pathFinalized = false;
            previewDirty = true;
            previewSampleBuffer.Clear();
            if (previewLine != null)
            {
                previewLine.positionCount = 0;
                previewLine.enabled = false;
            }

            //if (notify) Main.Notify("Path cleared");
        }

        public void DestroyPreview()
        {
            if (previewObject != null)
                UnityEngine.Object.Destroy(previewObject);
            if (previewMaterial != null)
                UnityEngine.Object.Destroy(previewMaterial);

            previewObject = null;
            previewLine = null;
            previewMaterial = null;
            previewDirty = true;
            previewCurveAmount = -1;
            previewSampleBuffer.Clear();
        }

        private bool EnsurePreview()
        {
            if (previewLine != null)
                return true;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                MelonLogger.Warning("Path preview unavailable: no supported line shader was found");
                return false;
            }

            previewObject = new GameObject("DirectorMode Cinematic Path");
            previewObject.hideFlags = HideFlags.HideAndDontSave;
            previewLine = previewObject.AddComponent<LineRenderer>();
            previewLine.useWorldSpace = true;
            previewLine.widthMultiplier = 0.035f;
            previewLine.numCapVertices = 2;
            previewLine.numCornerVertices = 2;
            previewLine.shadowCastingMode = ShadowCastingMode.Off;
            previewLine.receiveShadows = false;

            previewMaterial = new Material(shader);
            previewMaterial.hideFlags = HideFlags.HideAndDontSave;
            previewMaterial.color = Color.white;
            previewLine.material = previewMaterial;
            return true;
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
