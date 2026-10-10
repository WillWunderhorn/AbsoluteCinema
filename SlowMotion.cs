using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace DIrectorMode
{
    public class SlowMotion
    {
        private const float ScaleMin = 0.05f;
        private const float ScaleMax = 10.0f;
        private const float DoublePressTime = 0.35f;
        private float lastPress = -10f;
        private float currentScale = 1f;
        private bool holding;

        public float CurrentScale => currentScale;

        private static KeyCode GetKey()
        {
            try
            {
                KeyCode k = Settings.Instance.slowMoKey;
                if (k != KeyCode.None)
                    return k;
            }
            catch { }
            return KeyCode.O;
        }

        private static float GetStepFactor()
        {
            try
            {
                float s = Settings.Instance.slowMoWheelStep;
                if (s > 0f)
                    return 1f + Mathf.Clamp(s, 0.05f, 0.5f);
            }
            catch { }
            return 1.15f; // +15% per notch
        }

        public void Update()
        {
            if (Main.Vertigo != null && Main.Vertigo.IsActive)
                return;

            if (Main.Tutorial != null && Main.Tutorial.IsOpen)
                return;

            try
            {
                if (InterfaceManager.DetermineIfOverlayIsActive())
                    return;
            }
            catch { }

            KeyCode key = GetKey();

            if (Main.KeyDown(key))
            {
                if (Time.unscaledTime - lastPress <= DoublePressTime)
                {
                    currentScale = 1f;
                    Apply(1f);
                    lastPress = -10f;
                    Main.Notify("Time scale: normal");
                }
                else
                {
                    lastPress = Time.unscaledTime;
                }
            }
            bool hold = Main.Key(key);

            if (hold && !holding)
            {
                holding = true;
                if (Time.timeScale > 0f)
                    currentScale = Time.timeScale;
            }
            else if (!hold && holding)
            {
                holding = false;
            }

            if (!holding)
                return;

            float scroll = Main.Scroll;
            if (scroll == 0f)
                return;

            float factor = GetStepFactor();
            // faster/slower
            currentScale *= Mathf.Pow(factor, scroll);
            currentScale = Mathf.Clamp(currentScale, ScaleMin, ScaleMax);

            Apply(currentScale);
            //MelonLogger.Msg("Time scale: " + currentScale.ToString("0.00") + "x");
        }

        public void Reset()
        {
            holding = false;
            currentScale = 1f;
            lastPress = -10f;
            Apply(1f);
        }

        public void ResetForPanel()
        {
            holding = false;
            currentScale = 1f;
            lastPress = -10f;
            // Preserve a game's own pause while clearing Director Mode's slow motion.
            if (Time.timeScale > 0f)
                Apply(1f);
        }

        private static void Apply(float scale)
        {
            scale = Mathf.Max(0.01f, scale);
            Time.timeScale = scale;
            GameManager.m_GlobalTimeScale = scale;
        }
    }
}
