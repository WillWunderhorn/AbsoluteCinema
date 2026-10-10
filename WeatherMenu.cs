using Il2Cpp;
using MelonLoader;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace DIrectorMode
{
    internal sealed class WeatherMenu
    {
        private const float DesignWidth = 480f;
        private const float DesignHeight = 700f;
        private static readonly Color TextColor = Color.black;
        private static class Layout
        {
            public const float WeatherTitleY = 32f, WeatherRowY = 88f, WeatherActionsY = 145f;
            public const float WindTitleY = 226f, WindStrengthY = 292f, WindDirectionY = 340f, WindActionsY = 405f;
            public const float TimeTitleY = 476f, TimeLabelY = 520f, TimeSliderY = 565f, TimeButtonY = 610f;
            public const float ChoiceLabelX = 22f, ChoiceLabelWidth = 205f;
            public const float PreviousArrowX = 235f, ChoiceValueX = 270f, ChoiceValueWidth = 150f, NextArrowX = 438f;
            public const float LeftButtonX = 24f, RightButtonX = 252f, ButtonWidth = 204f, ButtonHeight = 70f;
            public const float SliderX = 42f, SliderWidth = 396f, TimeButtonWidth = 204f;
            public const float ToggleX = 350f, ToggleLabelOffsetX = 31f;
            public const float GodToggleY = 621f, GhostToggleY = 655f;
            // Visual size controls: titles are 20% smaller, controls 10% smaller, row text 5% larger.
            public const float TitleFontSize = 32f; // previous size: 40px
            public const float ControlScale = 0.72f; // previous scale: 0.80
            public const float PressedScale = 0.92f;
            public const float RowTextScale = 1.05f;
        }

        private readonly WeatherController weather;
        private bool isOpen;
        private int weatherIndex;
        private int strengthIndex;
        private int directionIndex;
        private int timeOfDayMinutes = 12 * 60;
        private bool weatherLocked;
        private bool windLocked;
        private bool godModeEnabled;
        private bool ghostModeEnabled;
        private PlayerControlMode previousControlMode;
        private bool controlModeSaved;
        private GUIStyle titleStyle = null!;
        private GUIStyle labelStyle = null!;
        private GUIStyle buttonStyle = null!;
        private bool stylesReady;
        private Vector2 lockedAngle;
        private bool assetsLoaded;
        private bool soundsLoadStarted;
        private string? pressedControl;
        private AudioClip? clickSound;
        private AudioClip? scrollSound;
        private AudioSource? soundSource;
        private AudioListener? menuListener;
        private Texture2D? menuBackground;
        private Texture2D? applyButton;
        private Texture2D? applyWindButton;
        private Texture2D? lockWeatherButton;
        private Texture2D? lockWindButton;
        private Texture2D? unlockWeatherButton;
        private Texture2D? unlockWindButton;
        private Texture2D? setTimeButton;
        private Texture2D? checkMark;
        private Texture2D? checkBoxUnchecked;
        private Texture2D? godModeLabel;
        private Texture2D? ghostLabel;
        private Texture2D? arrowLeftActive;
        private Texture2D? arrowLeftInactive;
        private Texture2D? arrowRightActive;
        private Texture2D? arrowRightInactive;
        private Texture2D? sliderLine;
        private Texture2D? sliderKnob;
        public bool IsOpen => isOpen;
        public WeatherMenu(WeatherController controller) { weather = controller; }

        public void Toggle()
        {
            if (!isOpen && !CanOpen())
            {
                Main.Notify("The weather menu works in free roam only");
                return;
            }

            SyncFromGame();
            isOpen = !isOpen;
            pressedControl = null;
            if (isOpen)
            {
                LockPlayerControl();
                //MelonLoader.MelonLogger.Msg("Weather menu opened."); 

                vp_FPSCamera cam = GameManager.GetVpFPSCamera();
                lockedAngle = cam != null ? cam.Angle : Vector2.zero;
            }
            else
            {
                RestorePlayerControl();
                //MelonLoader.MelonLogger.Msg("Weather menu closed.");
            }
        }

        private static bool CanOpen()
        {
            if (GameManager.GetVpFPSCamera() == null) return false;
            return Main.Tutorial != null && Main.Tutorial.IsFreeRoam();
        }

        private void SyncFromGame()
        {
            try
            {
                WeatherOption w = weather.GetCurrentWeather();
                WindStrengthOption s = weather.GetCurrentWindStrength();
                WindDirectionOption d = weather.GetCurrentWindDirection();

                int i = w != null ? IndexWhere(weather.Weathers, x => x.Id == w.Id) : -1;
                if (i >= 0) weatherIndex = i;

                i = s != null ? IndexWhere(weather.WindStrengths, x => x.Id == s.Id) : -1;
                if (i >= 0) strengthIndex = i;

                i = d != null ? IndexWhere(weather.WindDirections, x => x.Id == d.Id) : -1;
                if (i >= 0) directionIndex = i;

                weatherLocked = weather.IsWeatherLocked();
                windLocked = weather.IsWindLocked();
                godModeEnabled = weather.IsGodModeEnabled();
                ghostModeEnabled = weather.IsGhostModeEnabled();

                int minuteOfDay = weather.GetCurrentTimeMinutes();
                if (minuteOfDay >= 0) timeOfDayMinutes = minuteOfDay;
            }
            catch { }
        }

        private static int IndexWhere<T>(System.Collections.Generic.IReadOnlyList<T> list, System.Func<T, bool> match)
        {
            for (int i = 0; i < list.Count; i++)
                if (match(list[i])) return i;
            return -1;
        }

        public void OnGUI()
        {
            if (!isOpen) return;

            try
            {
                EnsureAssets();
                EnsureStyles();
                float scale = Mathf.Min((Screen.width - 60f) / DesignWidth, (Screen.height - 60f) / DesignHeight);
                scale = Mathf.Clamp(scale, 0.25f, 1.15f);
                float width = DesignWidth * scale;
                float height = DesignHeight * scale;
                const float panelX = 30f;
                const float panelY = 30f;
                GUI.BeginGroup(new Rect(panelX, panelY, width, height));
                bool interactive = !ConsoleState.IsOpen && !UnityExplorerState.IsOpen;
                try { DrawMenuContents(scale, width, interactive); }
                finally { GUI.EndGroup(); }
            }
            catch (System.Exception e)
            {
                MelonLoader.MelonLogger.Error("Weather menu drawing failed: " + e);
                Close();
            }
        }

        public void Update()
        {
            if (!isOpen) return;

            if (!ConsoleState.IsOpen && !UnityExplorerState.IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        public void LateUpdate()
        {
            if (!isOpen) return;

            vp_FPSCamera cam = GameManager.GetVpFPSCamera();
            if (cam == null) return;

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

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            pressedControl = null;
            RestorePlayerControl();
        }

        private void DrawChoice<T>(string caption, System.Collections.Generic.IReadOnlyList<T> items,
            ref int index, System.Func<T, string> getName, float left, float top, float scale, float y, bool interactive)
        {
            DrawLabel(caption, left + Layout.ChoiceLabelX * scale, top + y * scale,
                Layout.ChoiceLabelWidth * scale, 46f * scale, Mathf.RoundToInt(20f * Layout.RowTextScale), false, scale);

            if (items == null || items.Count == 0)
            {
                DrawLabel("Unavailable", left + 260f * scale, top + y * scale, 170f * scale, 46f * scale,
                    Mathf.RoundToInt(18f * Layout.RowTextScale), true, scale);
                return;
            }

            index = Mathf.Clamp(index, 0, items.Count - 1);
            if (DrawArrow("weather_prev_" + caption, arrowLeftActive, arrowLeftInactive,
                new Rect(left + Layout.PreviousArrowX * scale, top + y * scale + 4f * scale, 22f * scale, 38f * scale),
                index > 0, interactive)) index--;
            DrawLabel(getName(items[index]), left + Layout.ChoiceValueX * scale, top + y * scale,
                Layout.ChoiceValueWidth * scale, 46f * scale, Mathf.RoundToInt(18f * Layout.RowTextScale), true, scale);
            if (DrawArrow("weather_next_" + caption, arrowRightActive, arrowRightInactive,
                new Rect(left + Layout.NextArrowX * scale, top + y * scale + 4f * scale, 22f * scale, 38f * scale),
                index < items.Count - 1, interactive)) index++;
        }

        private void DrawActionPair(float left, float top, float scale, float y,
            System.Action leftAction, System.Action rightAction, Texture2D? leftTexture, Texture2D? rightTexture,
            string? leftLabel, string? rightLabel, string leftId, string rightId, bool interactive)
        {
            DrawImageButton(leftId, leftTexture,
                new Rect(left + Layout.LeftButtonX * scale, top + y * scale, Layout.ButtonWidth * scale, Layout.ButtonHeight * scale),
                scale, leftAction, leftLabel, interactive);
            DrawImageButton(rightId, rightTexture,
                new Rect(left + Layout.RightButtonX * scale, top + y * scale, Layout.ButtonWidth * scale, Layout.ButtonHeight * scale),
                scale, rightAction, rightLabel, interactive);
        }

        private void DrawCheckbox(string id, Texture2D? uncheckedTexture, Texture2D? checkTexture,
            Texture2D? labelTexture, bool isChecked, float x, float y, float scale,
            System.Action toggle, bool interactive)
        {
            const float boxWidth = 23f;
            const float boxHeight = 22f;
            const float rowWidth = 112f;
            Rect rowRect = new Rect(x, y - 2f * scale, rowWidth * scale, 26f * scale);
            bool down = false;
            bool clicked = interactive && HandleButtonInput(id, rowRect, out down);
            float pressScale = down ? Layout.PressedScale : 1f;

            Rect boxRect = ScaleAroundCenter(new Rect(x, y, boxWidth * scale, boxHeight * scale), pressScale);
            if (uncheckedTexture != null)
                GUI.DrawTexture(boxRect, uncheckedTexture, ScaleMode.StretchToFill, true);

            if (isChecked && checkTexture != null)
            {
                Rect checkRect = new Rect(
                    x + ((boxWidth - 21f) * 0.5f + 4f) * scale,
                    y + (boxHeight - 13f) * 0.5f * scale,
                    21f * scale,
                    13f * scale);
                checkRect = ScaleAroundCenter(checkRect, pressScale);
                GUI.DrawTexture(checkRect, checkTexture, ScaleMode.StretchToFill, true);
            }

            if (labelTexture != null)
            {
                Rect labelRect = new Rect(x + Layout.ToggleLabelOffsetX * scale, y + 3f * scale,
                    labelTexture.width * scale, labelTexture.height * scale);
                labelRect = ScaleAroundCenter(labelRect, pressScale);
                GUI.DrawTexture(labelRect, labelTexture, ScaleMode.StretchToFill, true);
            }

            if (clicked)
            {
                PlaySound(clickSound);
                toggle();
            }
        }

        private void DrawImageButton(string id, Texture2D? texture, Rect rect, float scale,
            System.Action action, string? overlayLabel = null, bool interactive = true)
        {
            rect = ScaleAroundCenter(rect, Layout.ControlScale);
            bool down = false;
            bool clicked = false;
            if (interactive) clicked = HandleButtonInput(id, rect, out down);
            Rect imageRect = down ? ScaleAroundCenter(rect, Layout.PressedScale) : rect;
            if (texture != null)
                GUI.DrawTexture(imageRect, texture, ScaleMode.StretchToFill, true);
            else
                DrawRect(imageRect, new Color(0.92f, 0.89f, 0.81f, 1f));

            if (overlayLabel != null)
            {
                labelStyle.fontSize = Mathf.RoundToInt(20f * scale);
                labelStyle.alignment = TextAnchor.MiddleCenter;
                GUI.Label(imageRect, overlayLabel, labelStyle);
            }

            if (clicked)
            {
                PlaySound(clickSound);
                action();
            }
        }

        private bool DrawArrow(string id, Texture2D? active, Texture2D? inactive, Rect rect, bool canClick, bool interactive)
        {
            Texture2D? texture = canClick ? active : inactive;
            rect = ScaleAroundCenter(rect, Layout.ControlScale);
            bool down = false;
            bool clicked = false;
            if (interactive && canClick)
                clicked = HandleButtonInput(id, rect, out down);
            Rect imageRect = down ? ScaleAroundCenter(rect, Layout.PressedScale) : rect;
            if (texture != null)
                GUI.DrawTexture(imageRect, texture, ScaleMode.StretchToFill, true);
            else
                GUI.Label(imageRect, id.Contains("prev") ? "◀" : "▶", buttonStyle);

            if (canClick && clicked) PlaySound(clickSound);
            return canClick && clicked;
        }

        private bool HandleButtonInput(string id, Rect rect, out bool down)
        {
            Event e = Event.current;
            bool clicked = false;

            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                pressedControl = id;
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && pressedControl == id)
            {
                clicked = rect.Contains(e.mousePosition);
                pressedControl = null;
                e.Use();
            }

            down = pressedControl == id && Input.GetMouseButton(0) && rect.Contains(e.mousePosition);
            return clicked;
        }

        private static Rect ScaleAroundCenter(Rect rect, float factor)
        {
            float w = rect.width * factor;
            float h = rect.height * factor;
            return new Rect(rect.center.x - w * 0.5f, rect.center.y - h * 0.5f, w, h);
        }

        private void DrawLabel(string text, float x, float y, float width, float height,
            int fontSize, bool centered, float scale)
        {
            labelStyle.fontSize = Mathf.RoundToInt(fontSize * scale);
            labelStyle.alignment = centered ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            GUI.Label(new Rect(x, y, width, height), text, labelStyle);
        }

        private void DrawTitle(string text, float x, float y, float width, float height, float scale)
        {
            titleStyle.fontSize = Mathf.RoundToInt(Layout.TitleFontSize * scale);
            GUI.Label(new Rect(x, y, width, height), text, titleStyle);
        }

        private static void DrawRect(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Normal,
                normal = { textColor = TextColor }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = TextColor }
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Normal,
                normal = { textColor = TextColor },
                hover = { textColor = TextColor },
                active = { textColor = TextColor }
            };
            Font font = Main.Tutorial != null ? Main.Tutorial.GetFont() : null;
            if (font != null)
            {
                titleStyle.font = font;
                labelStyle.font = font;
                buttonStyle.font = font;
            }
            stylesReady = true;
        }

        private void EnsureAssets()
        {
            if (!assetsLoaded)
            {
                assetsLoaded = true;

                Assembly assembly = typeof(WeatherMenu).Assembly;
                menuBackground = LoadTexture(assembly, "MenuBG.png");
                applyButton = LoadTexture(assembly, "ApplyWeatherButton.png");
                applyWindButton = LoadTexture(assembly, "ApplyWindButton.png");
                arrowLeftActive = LoadTexture(assembly, "ArrowActiveLeft.png");
                arrowLeftInactive = LoadTexture(assembly, "ArrowInactiveLeft.png");
                arrowRightActive = LoadTexture(assembly, "ArrowActiveRight.png");
                arrowRightInactive = LoadTexture(assembly, "ArrowInactiveRight.png");
                lockWeatherButton = LoadTexture(assembly, "LockWeatherButton.png");
                lockWindButton = LoadTexture(assembly, "LockWindButton.png");
                unlockWeatherButton = LoadTexture(assembly, "UnlockWeatherButton.png");
                unlockWindButton = LoadTexture(assembly, "UnlockWindButton.png");
                setTimeButton = LoadTexture(assembly, "SetTimeButton.png");
                checkMark = LoadTexture(assembly, "Check.png");
                checkBoxUnchecked = LoadTexture(assembly, "CheckBoxUnchecked.png");
                godModeLabel = LoadTexture(assembly, "GodMode.png");
                ghostLabel = LoadTexture(assembly, "Ghost.png");
                sliderKnob = LoadTexture(assembly, "SliderKnob.png");
                sliderLine = LoadTexture(assembly, "SliderLine.png");
            }

            if (!soundsLoadStarted)
            {
                soundsLoadStarted = true;
                Assembly assembly = typeof(WeatherMenu).Assembly;
                clickSound = LoadWav(assembly, "MenuSounds.click.wav");
                scrollSound = LoadWav(assembly, "MenuSounds.scroll.wav");
            }
        }

        private void DrawMenuContents(float scale, float width, bool interactive)
        {
            const float left = 0f;
            const float top = 0f;

            if (menuBackground != null)
                GUI.DrawTexture(new Rect(0f, 0f, width, DesignHeight * scale), menuBackground, ScaleMode.StretchToFill, true);
            else
                DrawRect(new Rect(0f, 0f, width, DesignHeight * scale), new Color(0.78f, 0.70f, 0.57f, 1f));

            DrawTitle("WEATHER", left, top + Layout.WeatherTitleY * scale, width, 54f * scale, scale);
            DrawChoice("Set type", weather.Weathers, ref weatherIndex, x => x.Name, left, top, scale, Layout.WeatherRowY, interactive);
            DrawActionPair(left, top, scale, Layout.WeatherActionsY,
                () => weather.SetWeather(SelectedWeather(), weatherLocked),
                () =>
                {
                    if (weatherLocked)
                    {
                        bool unlocked = weather.UnlockWeather();
                        weatherLocked = false;
                        if (!unlocked) MelonLoader.MelonLogger.Warning("Weather menu: unlock was requested, but the game still reports weather locked.");
                    }
                    else weatherLocked = weather.SetWeather(SelectedWeather(), true);
                },
                applyButton, weatherLocked ? unlockWeatherButton : lockWeatherButton,
                "", null, "weather_apply", "weather_lock", interactive);

            DrawTitle("WIND", left, top + Layout.WindTitleY * scale, width, 54f * scale, scale);
            DrawChoice("Set wind", weather.WindStrengths, ref strengthIndex, x => x.Name, left, top, scale, Layout.WindStrengthY, interactive);
            DrawChoice("Set direction", weather.WindDirections, ref directionIndex, x => x.Name, left, top, scale, Layout.WindDirectionY, interactive);
            DrawActionPair(left, top, scale, Layout.WindActionsY,
                () => weather.SetWind(SelectedStrength(), SelectedDirection(), windLocked),
                () =>
                {
                    if (windLocked)
                    {
                        bool unlocked = weather.UnlockWind();
                        windLocked = false;
                        if (!unlocked) MelonLoader.MelonLogger.Warning("Weather menu: wind unlock was requested, but the game did not confirm it.");
                    }
                    else windLocked = weather.SetWind(SelectedStrength(), SelectedDirection(), true);
                },
                applyWindButton, windLocked ? unlockWindButton : lockWindButton,
                null, null, "wind_apply", "wind_lock", interactive);

            DrawTitle("TIME OF THE DAY", left, top + Layout.TimeTitleY * scale, width, 54f * scale, scale);
            float sliderX = Layout.SliderX * scale;
            float sliderY = Layout.TimeSliderY * scale;
            float sliderWidth = Layout.SliderWidth * scale;
            if (interactive)
            {
                Rect sliderHitRect = new Rect(sliderX, sliderY - 25f * scale, sliderWidth, 50f * scale);
                int previousMinute = timeOfDayMinutes;
                timeOfDayMinutes = Mathf.Clamp(Mathf.RoundToInt(GUI.HorizontalSlider(sliderHitRect,
                    timeOfDayMinutes, 0f, 1439f, GUIStyle.none, GUIStyle.none)), 0, 1439);
                if (timeOfDayMinutes != previousMinute) PlaySound(scrollSound);
            }
            if (sliderLine != null)
                GUI.DrawTexture(new Rect(sliderX, sliderY - 1f * scale, sliderWidth, 11f * scale), sliderLine, ScaleMode.StretchToFill, true);
            if (sliderKnob != null)
            {
                float normalized = timeOfDayMinutes / 1439f;
                float knobWidth = 17f * scale;
                float knobHeight = 56f * scale;
                float knobCenterX = sliderX + knobWidth * 0.5f + normalized * (sliderWidth - knobWidth);
                GUI.DrawTexture(new Rect(knobCenterX - knobWidth * 0.5f,
                    sliderY - 15f * scale - knobHeight * 0.1f, knobWidth, knobHeight),
                    sliderKnob, ScaleMode.StretchToFill, true);
            }
            DrawLabel(string.Format("{0:00}:{1:00}", timeOfDayMinutes / 60, timeOfDayMinutes % 60), left + 186f * scale,
                top + Layout.TimeLabelY * scale, 108f * scale, 30f * scale, 20, true, scale);
            DrawImageButton("set_time", setTimeButton,
                new Rect(left + (DesignWidth - Layout.TimeButtonWidth) * 0.5f * scale,
                    top + Layout.TimeButtonY * scale, Layout.TimeButtonWidth * scale, Layout.ButtonHeight * scale),
                scale, () => weather.SetTimeOfDay(timeOfDayMinutes), null, interactive);
            DrawCheckbox("god_mode", checkBoxUnchecked, checkMark, godModeLabel, godModeEnabled,
                Layout.ToggleX * scale, Layout.GodToggleY * scale, scale,
                () =>
                {
                    if (weather.ToggleGodMode()) godModeEnabled = weather.IsGodModeEnabled();
                }, interactive);
            DrawCheckbox("ghost_mode", checkBoxUnchecked, checkMark, ghostLabel, ghostModeEnabled,
                Layout.ToggleX * scale, Layout.GhostToggleY * scale, scale,
                () =>
                {
                    if (weather.ToggleGhostMode()) ghostModeEnabled = weather.IsGhostModeEnabled();
                }, interactive);
        }

        private static AudioClip? LoadWav(Assembly assembly, string resourceSuffix)
        {
            try
            {
                string? resourceName = null;
                foreach (string name in assembly.GetManifestResourceNames())
                {
                    if (name.EndsWith("." + resourceSuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        resourceName = name;
                        break;
                    }
                }

                if (resourceName == null)
                {
                    MelonLogger.Warning("Weather menu sound missing: " + resourceSuffix);
                    return null;
                }

                using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return null;
                    using var memory = new MemoryStream();
                    stream.CopyTo(memory);
                    return Tutorial.WavToClip(memory.ToArray(), resourceSuffix);
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning("Weather menu sound failed to load (" + resourceSuffix + "): " + e.Message);
                return null;
            }
        }

        private void PlaySound(AudioClip? clip)
        {
            if (clip == null) return;
            EnsureAudioSource();
            EnsureAudioListener();
            soundSource!.PlayOneShot(clip, 1f);
        }

        private void EnsureAudioSource()
        {
            if (soundSource != null) return;

            GameObject go = new GameObject("DirectorMode_WeatherMenuSfx");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            soundSource = go.AddComponent<AudioSource>();
            soundSource.playOnAwake = false;
            soundSource.spatialBlend = 0f;
            soundSource.volume = 1f;
            soundSource.mute = false;
            soundSource.ignoreListenerPause = true;
            soundSource.ignoreListenerVolume = true;
        }

        private void EnsureAudioListener()
        {
            bool activeListenerFound = false;
            foreach (AudioListener listener in UnityEngine.Object.FindObjectsOfType<AudioListener>())
            {
                if (listener != menuListener && listener.enabled && listener.gameObject.activeInHierarchy)
                {
                    activeListenerFound = true;
                    break;
                }
            }

            if (menuListener != null) menuListener.enabled = !activeListenerFound;
            else if (!activeListenerFound) menuListener = soundSource!.gameObject.AddComponent<AudioListener>();
        }

        private static Texture2D? LoadTexture(System.Reflection.Assembly assembly, string fileName)
        {
            try
            {
                string? resourceName = null;
                foreach (string name in assembly.GetManifestResourceNames())
                {
                    if (name.EndsWith("." + fileName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        resourceName = name;
                        break;
                    }
                }

                if (resourceName == null)
                {
                    MelonLoader.MelonLogger.Warning("Weather menu asset missing: " + fileName);
                    return null;
                }

                using System.IO.Stream? stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null) return null;
                using var memory = new System.IO.MemoryStream();
                stream.CopyTo(memory);

                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, memory.ToArray()))
                {
                    UnityEngine.Object.Destroy(texture);
                    MelonLoader.MelonLogger.Warning("Weather menu asset could not be decoded: " + fileName);
                    return null;
                }

                texture.hideFlags = HideFlags.HideAndDontSave;
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                return texture;
            }
            catch (System.Exception e)
            {
                MelonLoader.MelonLogger.Warning("Weather menu asset failed to load (" + fileName + "): " + e.Message);
                return null;
            }
        }

        private WeatherOption SelectedWeather() => weather.Weathers.Count == 0 ? null : weather.Weathers[Mathf.Clamp(weatherIndex, 0, weather.Weathers.Count - 1)];
        private WindStrengthOption SelectedStrength() => weather.WindStrengths.Count == 0 ? null : weather.WindStrengths[Mathf.Clamp(strengthIndex, 0, weather.WindStrengths.Count - 1)];
        private WindDirectionOption SelectedDirection() => weather.WindDirections.Count == 0 ? null : weather.WindDirections[Mathf.Clamp(directionIndex, 0, weather.WindDirections.Count - 1)];

        private void LockPlayerControl()
        {
            try
            {
                PlayerManager player = GameManager.GetPlayerManagerComponent();
                if (player == null) return;

                previousControlMode = player.GetControlMode();
                controlModeSaved = true;
                player.SetControlMode(PlayerControlMode.Locked);
            }
            catch (Exception e)
            {
                controlModeSaved = false;
                MelonLogger.Warning("Weather menu: could not lock player control: " + e.Message);
            }
        }

        private void RestorePlayerControl()
        {
            if (!controlModeSaved) return;

            try
            {
                PlayerManager player = GameManager.GetPlayerManagerComponent();
                if (player != null) player.SetControlMode(previousControlMode);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("Weather menu: could not restore player control: " + e.Message);
            }
            finally
            {
                controlModeSaved = false;
            }
        }

    }
}
