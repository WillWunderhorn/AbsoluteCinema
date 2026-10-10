using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace DIrectorMode
{
    public class HideHUD
    {
        private bool hidden;

        private readonly List<UIPanel> panels = new List<UIPanel>();
        private readonly List<float> savedAlpha = new List<float>();

        private GameObject fallbackObject;

        public bool IsHidden => hidden;

        public void Toggle()
        {
            if (hidden) Show();
            else Hide();
        }

        // call every update, maybe I'll find a better approach
        public void LateUpdate()
        {
            if (!hidden) return;

            int alive = 0;

            for (int i = 0; i < panels.Count; i++)
            {
                UIPanel p = panels[i];
                if (p == null) continue;

                alive++;
                if (p.alpha != 0f) p.alpha = 0f;
            }

            if (fallbackObject != null) alive++;
            if (alive == 0) ClearState();
        }

        public void Reset()
        {
            if (hidden) Restore();
            ClearState();
        }

        // =====================================================================

        private static Panel_HUD FindHud()
        {
            if (InterfaceManager.m_Instance == null) return null;
            return InterfaceManager.GetPanel<Panel_HUD>();
        }

        private void Hide()
        {
            Panel_HUD hud = FindHud();

            if (hud == null)
            {
                Main.Notify("HUD is not available here");
                return;
            }

            ClearState();

            foreach (UIPanel p in hud.GetComponentsInChildren<UIPanel>(true))
            {
                panels.Add(p);
                savedAlpha.Add(p.alpha);
                p.alpha = 0f;
            }

            if (panels.Count == 0)
            {
                fallbackObject = hud.gameObject;
                fallbackObject.SetActive(false);
            }

            hidden = true;
            //MelonLogger.Msg("HUD hidden (" + panels.Count + " panels)");
        }

        private void Show()
        {
            Restore();
            ClearState();
        }

        private void Restore()
        {
            for (int i = 0; i < panels.Count; i++)
            {
                UIPanel p = panels[i];
                if (p != null) p.alpha = savedAlpha[i];
            }

            if (fallbackObject != null)
                fallbackObject.SetActive(true);
        }

        private void ClearState()
        {
            hidden = false;
            panels.Clear();
            savedAlpha.Clear();
            fallbackObject = null;
        }
    }
}