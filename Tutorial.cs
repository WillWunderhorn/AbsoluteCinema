using Il2Cpp;
using MelonLoader;
using MelonLoader.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using System.Text;

namespace DIrectorMode
{
    public class Tutorial
    {

        private Texture2D arrowLeftActive, arrowLeftInactive;
        private Texture2D arrowRightActive, arrowRightInactive;
        private Texture2D exitActive, exitInactive;
        private float hoverLeft, hoverRight, hoverExit;
        private Font font;
        private bool fontTried;
        private const float HoverEaseTime = 0.15f;
        private const float HoverScale = 1.05f;
        private const float HoverAlpha = 0.80f;
        private const float ExitScale = 0.5f;
        private const string FolderName = "TutorialMaterials";
        private const string PagePrefix = "TutorialPage_";
        private readonly List<Texture2D> pages = new List<Texture2D>();
        private bool open;
        private int index;
        private CursorLockMode prevLock;
        private bool prevVisible;
        private GUIStyle arrowStyle;
        private GUIStyle labelStyle;
        private float freeRoamTime;
        private const float StartDelay = 3f;
        public bool IsOpen => open;
        private Transform panelRoot; // parent object
        private float prevTimeScale = 1f;
        private Vector2 lockedAngle;
        private bool restorePending;
        private float restoreAt;
        private RenderTexture blurTex;
        private bool blurReady;
        private const int BlurSteps = 4;
        private const float DimAlpha = 0.6f;
        private bool pending;
        private readonly List<AudioClip> sounds = new List<AudioClip>();
        private AudioSource sfx;
        private int lastSound = -1;
        private const float SfxVolume = 0.8f;
        private const string SoundsMarker = ".Sounds.";
        private readonly Queue<Action> loadQueue = new Queue<Action>();
        private bool queueBuilt;

        private void BuildLoadQueue()
        {
            queueBuilt = true;

            System.Reflection.Assembly asm = typeof(Tutorial).Assembly;

            string startRes = null, endRes = null;
            var numbered = new List<KeyValuePair<int, string>>();
            var wavs = new List<string>();

            foreach (string res in asm.GetManifestResourceNames())
            {
                string lower = res.ToLowerInvariant();

                if (lower.EndsWith(".wav") && lower.Contains("tutorialmaterials"))
                {
                    wavs.Add(res);
                    continue;
                }

                int dot = res.LastIndexOf('.');
                if (dot < 0) continue;

                string ext = lower.Substring(dot);
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") continue;

                string noExt = res.Substring(0, dot);
                int p = noExt.IndexOf(PagePrefix, StringComparison.OrdinalIgnoreCase);
                if (p < 0) continue;

                string suffix = noExt.Substring(p + PagePrefix.Length);

                if (suffix.Equals("Start", StringComparison.OrdinalIgnoreCase)) startRes = res;
                else if (suffix.Equals("End", StringComparison.OrdinalIgnoreCase)) endRes = res;
                else if (int.TryParse(suffix, out int n)) numbered.Add(new KeyValuePair<int, string>(n, res));
            }

            numbered.Sort((a, b) => a.Key.CompareTo(b.Key));

            var pageRes = new List<string>();
            if (startRes != null) pageRes.Add(startRes);
            foreach (var kv in numbered) pageRes.Add(kv.Value);
            if (endRes != null) pageRes.Add(endRes);

            // UI images
            loadQueue.Enqueue(() => arrowLeftActive = LoadUiTexture(asm, "ArrowLeft_Active"));
            loadQueue.Enqueue(() => arrowLeftInactive = LoadUiTexture(asm, "ArrowLeft_Inactive"));
            loadQueue.Enqueue(() => arrowRightActive = LoadUiTexture(asm, "ArrowRight_Active"));
            loadQueue.Enqueue(() => arrowRightInactive = LoadUiTexture(asm, "ArrowRight_Inactive"));

            // pages
            var slots = new Texture2D[pageRes.Count];
            for (int i = 0; i < pageRes.Count; i++)
            {
                int idx = i;
                loadQueue.Enqueue(() => slots[idx] = LoadTexture(asm, pageRes[idx]));
            }

            loadQueue.Enqueue(() =>
            {
                pages.Clear();
                foreach (Texture2D t in slots)
                    if (t != null) pages.Add(t);
            });

            // sounds
            foreach (string res in wavs)
            {
                string r = res;
                loadQueue.Enqueue(() => LoadOneSound(asm, r));
            }

            loadQueue.Enqueue(() => GetFont());
            //loadQueue.Enqueue(() => 
            //MelonLogger.Msg(
            //    "Tutorial: preloaded " + pages.Count + " pages and " + sounds.Count + " sounds"));
        }

        private void LoadOneSound(System.Reflection.Assembly asm, string res)
        {
            try
            {
                using Stream stream = asm.GetManifestResourceStream(res);
                if (stream == null) return;

                using var ms = new MemoryStream();
                stream.CopyTo(ms);

                AudioClip clip = WavToClip(ms.ToArray(), res);
                if (clip != null) sounds.Add(clip);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("Tutorial: could not load sound " + res + ": " + e.Message);
            }
        }

        // called when the tutorial opens
        private void FinishLoading()
        {
            if (!queueBuilt) BuildLoadQueue();
            while (loadQueue.Count > 0) loadQueue.Dequeue()();
        }


        public void Request()
        {
            pending = true;
        }

        private static readonly HashSet<string> AllowedPanels = new HashSet<string>
        {
            "Panel_HUD",
            "Panel_SaveIcon",
            "Panel_Subtitles"
        };

        // open/close

        public void Open()
        {
            if (open) return;

            FinishLoading();
            PlaySwipeSound();

            if (pages.Count == 0)
            {
                Main.Notify("Tutorial images not found");
                return;
            }

            FinishLoading();

            open = true;
            index = 0;
            hoverLeft = hoverRight = hoverExit = 0f;

            // grab the game picture for the blurred background
            blurReady = false;
            MelonCoroutines.Start(CaptureBackground());

            // pause the world and remember where the camera is looking
            if (restorePending) restorePending = false;
            else prevTimeScale = Time.timeScale;

            vp_FPSCamera cam = GameManager.GetVpFPSCamera();
            lockedAngle = cam != null ? cam.Angle : Vector2.zero;

            prevLock = Cursor.lockState;
            prevVisible = Cursor.visible;
        }

        public void Close()
        {
            if (!open) return;

            open = false;
            ReleaseBlur();

            //restore the time scale a moment later
            restorePending = true;
            restoreAt = Time.unscaledTime + 0.15f;

            Cursor.lockState = prevLock;
            Cursor.visible = prevVisible;
        }

        private void Next()
        {
            if (index < pages.Count - 1)
            {
                index++;
                PlaySwipeSound();
            }
            else
            {
                Close();// last page
            }
        }

        private void Prev()
        {
            if (index > 0)
            {
                index--;
                PlaySwipeSound();
            }
        }

        // Reads the WAV files and turns them into AudioClips
        internal static AudioClip WavToClip(byte[] wav, string name)
        {
            if (wav.Length < 44 ||
                Encoding.ASCII.GetString(wav, 0, 4) != "RIFF" ||
                Encoding.ASCII.GetString(wav, 8, 4) != "WAVE")
            {
                MelonLogger.Warning("Tutorial: " + name + " is not a WAV file.");
                return null;
            }

            int format = 0, channels = 0, sampleRate = 0, bits = 0;
            int dataStart = -1, dataLen = 0;
            int pos = 12;

            while (pos + 8 <= wav.Length)
            {
                string id = Encoding.ASCII.GetString(wav, pos, 4);
                int size = BitConverter.ToInt32(wav, pos + 4);
                int body = pos + 8;

                if (id == "fmt ")
                {
                    format = BitConverter.ToUInt16(wav, body);
                    channels = BitConverter.ToUInt16(wav, body + 2);
                    sampleRate = BitConverter.ToInt32(wav, body + 4);
                    bits = BitConverter.ToUInt16(wav, body + 14);

                    if (format == 0xFFFE && size >= 26)
                        format = BitConverter.ToUInt16(wav, body + 24);
                }
                else if (id == "data")
                {
                    dataStart = body;
                    dataLen = Math.Min(size, wav.Length - body);
                    break;
                }

                pos = body + size + (size & 1);
            }

            if (dataStart < 0 || channels <= 0 || sampleRate <= 0 || bits < 8)
            {
                MelonLogger.Warning("Tutorial: " + name + " has an unreadable WAV header.");
                return null;
            }

            int bytesPerSample = bits / 8;
            int frames = dataLen / bytesPerSample / channels;
            float[] samples = new float[frames * channels];

            for (int i = 0; i < samples.Length; i++)
            {
                int o = dataStart + i * bytesPerSample;

                if (format == 1 && bits == 8) samples[i] = (wav[o] - 128) / 128f;
                else if (format == 1 && bits == 16) samples[i] = BitConverter.ToInt16(wav, o) / 32768f;
                else if (format == 1 && bits == 24) samples[i] = ((wav[o] | (wav[o + 1] << 8) | ((sbyte)wav[o + 2] << 16))) / 8388608f;
                else if (format == 1 && bits == 32) samples[i] = BitConverter.ToInt32(wav, o) / 2147483648f;
                else if (format == 3 && bits == 32) samples[i] = BitConverter.ToSingle(wav, o);
                else
                {
                    MelonLogger.Warning("Tutorial: " + name + " uses an unsupported WAV format (" + format + ", " + bits + " bit).");
                    return null;
                }
            }

            AudioClip clip = AudioClip.Create("DirectorMode_" + frames, frames, channels, sampleRate, false);
            clip.SetData(samples, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            return clip;
        }

        private AudioListener ourListener;

        private void EnsureAudioSource()
        {
            if (sfx != null) return;

            GameObject go = new GameObject("DirectorMode_Sfx");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;

            sfx = go.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;// plain 2D sound
            sfx.volume = 1f;
            sfx.mute = false;
            sfx.ignoreListenerPause = true;
            sfx.ignoreListenerVolume = true;
        }

        //Unity only plays sound if an active AudioListener exists, whyyyyyy??????
        private void EnsureListener()
        {
            bool other = false;

            foreach (AudioListener l in UnityEngine.Object.FindObjectsOfType<AudioListener>())
            {
                if (l != ourListener && l.enabled && l.gameObject.activeInHierarchy)
                    other = true;
            }

            if (ourListener != null) ourListener.enabled = !other;
            else if (!other) ourListener = sfx.gameObject.AddComponent<AudioListener>();
        }

        private void PlaySwipeSound()
        {
            if (sounds.Count == 0)
            {
                MelonLogger.Warning("Tutorial: no swipe sounds loaded.");
                return;
            }

            EnsureAudioSource();
            EnsureListener();

            // random
            int i = 0;
            if (sounds.Count > 1)
            {
                do { i = UnityEngine.Random.Range(0, sounds.Count); }
                while (i == lastSound);
            }

            lastSound = i;
            sfx.PlayOneShot(sounds[i], SfxVolume);

            //MelonLogger.Msg("Tutorial: played sound " + i + " (listener volume " +
            //    AudioListener.volume + ", paused " + AudioListener.pause + ")");
        }

        private IEnumerator CaptureBackground()
        {
            yield return new WaitForEndOfFrame();

            if (!open) yield break;

            RenderTexture result = null;

            try
            {
                int w = Screen.width;
                int h = Screen.height;

                Texture2D shot = new Texture2D(w, h, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
                shot.Apply();

                Texture src = shot;
                RenderTexture prev = null;
                int cw = w;
                int ch = h;

                for (int i = 0; i < BlurSteps; i++)
                {
                    cw = Mathf.Max(2, cw / 2);
                    ch = Mathf.Max(2, ch / 2);

                    RenderTexture rt = new RenderTexture(cw, ch, 0);
                    rt.filterMode = FilterMode.Bilinear;
                    Graphics.Blit(src, rt);

                    if (prev != null)
                    {
                        prev.Release();
                        UnityEngine.Object.Destroy(prev);
                    }

                    prev = rt;
                    src = rt;
                }

                UnityEngine.Object.Destroy(shot);
                result = prev;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("Tutorial: background blur failed: " + e.Message);
            }

            blurTex = result;
            blurReady = true;
        }

        private void ReleaseBlur()
        {
            blurReady = false;

            if (blurTex != null)
            {
                blurTex.Release();
                UnityEngine.Object.Destroy(blurTex);
                blurTex = null;
            }
        }

        public void Update()
        {
            // background preloading
            if (!queueBuilt)
            {
                BuildLoadQueue();
            }
            else if (loadQueue.Count > 0)
            {
                try { loadQueue.Dequeue()(); }
                catch (Exception e) { MelonLogger.Warning("Tutorial: preload step failed: " + e.Message); }
            }

            if (restorePending && Time.unscaledTime >= restoreAt)
            {
                restorePending = false;
                if (!IsPanelActive("Panel_PauseMenu"))
                    Time.timeScale = prevTimeScale > 0f ? prevTimeScale : 1f;
            }

            // key
            if (Main.KeyDown(Settings.Instance.tutorialKey))
            {
                if (open)
                {
                    Close();
                    return;
                }

                if (IsFreeRoam()) Open();
                else Main.Notify("The tutorial can only be opened in free roam");
            }

            if (!open) return;

            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (Main.KeyDown(KeyCode.Escape)) { Close(); return; }
            if (Main.KeyDown(KeyCode.LeftArrow)) Prev();
            if (Main.KeyDown(KeyCode.RightArrow) || Main.KeyDown(KeyCode.Return)) Next();
        }

        public void LateUpdate()
        {
            if (!open) return;

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

        internal bool IsFreeRoam()
        {
            if (Main.FindPlayer() == null) return false;
            if (Time.timeScale <= 0f) return false;

            try
            {
                if (GameManager.IsMainMenuActive()) return false;
            }
            catch
            {
                return false;
            }

            if (AnyBlockingPanelOpen()) return false;

            return true;
        }

        private Transform GetPanelRoot()
        {
            if (panelRoot == null)
            {
                GameObject hud = GameObject.Find("Panel_HUD");
                if (hud != null) panelRoot = hud.transform.parent;
            }
            return panelRoot;
        }

        private bool AnyBlockingPanelOpen()
        {
            Transform root = GetPanelRoot();
            if (root == null) return true;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null) continue;

                string name = child.name;
                bool isPanel = name.StartsWith("Panel_", StringComparison.Ordinal) || name == "TimeWidget";
                if (!isPanel) continue;

                if (AllowedPanels.Contains(name)) continue;

                if (child.gameObject.activeSelf) return true;
            }

            return false;
        }

        internal bool IsPanelActive(string panelName)
        {
            Transform root = GetPanelRoot();
            if (root == null) return false;

            Transform t = root.Find(panelName);
            return t != null && t.gameObject.activeSelf;
        }

        public void OnGUI()
        {
            if (!open || pages.Count == 0) return;
            if (!blurReady) return;

            EnsureStyles();

            float sw = Screen.width;
            float sh = Screen.height;
            float ui = Mathf.Clamp(sh / 1080f, 0.6f, 1.5f);   // UI scale

            if (blurTex != null)
                GUI.DrawTexture(new Rect(0f, 0f, sw, sh), blurTex, ScaleMode.StretchToFill, false);

            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, DimAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, sw, sh), Texture2D.whiteTexture);
            GUI.color = oldColor;

            Texture2D tex = pages[index];
            float scale = Mathf.Min((sw * 0.7f) / tex.width, (sh * 0.8f) / tex.height);
            float w = tex.width * scale;
            float h = tex.height * scale;
            Rect r = new Rect((sw - w) * 0.5f, (sh - h) * 0.5f, w, h);

            GUI.DrawTexture(r, tex, ScaleMode.StretchToFill);

            // arrows < >
            float arrow = 95f * ui;
            float gap = 20f * ui;
            float ay = r.y + (r.height - arrow) * 0.5f;

            bool canPrev = index > 0;
            bool canNext = index < pages.Count - 1;

            if (DrawButton(new Rect(r.x - arrow - gap, ay, arrow, arrow),
                    canPrev ? arrowLeftActive : arrowLeftInactive, canPrev, ref hoverLeft))
                Prev();

            if (DrawButton(new Rect(r.xMax + gap, ay, arrow, arrow),
                    canNext ? arrowRightActive : arrowRightInactive, canNext, ref hoverRight))
                Next();

            // Nazhdak
            labelStyle.fontSize = Mathf.RoundToInt(28f * ui);
            GUI.Label(new Rect(r.x, r.yMax + 8f * ui, r.width, 44f * ui),
                (index + 1) + " / " + pages.Count, labelStyle);
        }

        private void EnsureStyles()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.alignment = TextAnchor.MiddleCenter;
                labelStyle.normal.textColor = Color.white;
            }

            Font f = GetFont();
            if (f != null && labelStyle.font != f)
                labelStyle.font = f;
        }

        // Loading the pages

        private static Texture2D LoadUiTexture(System.Reflection.Assembly asm, string baseName)
        {
            foreach (string res in asm.GetManifestResourceNames())
            {
                if (res.EndsWith("." + baseName + ".png", StringComparison.OrdinalIgnoreCase))
                    return LoadTexture(asm, res);
            }

            MelonLogger.Warning("Tutorial: UI image '" + baseName + ".png' not found.");
            return null;
        }

        private bool fontFailed;

        internal Font GetFont()
        {
            if (font != null) return font;// null check
            if (fontFailed) return null;

            try
            {
                System.Reflection.Assembly asm = typeof(Tutorial).Assembly;

                string resName = null;
                string ext = null;

                foreach (string res in asm.GetManifestResourceNames())
                {
                    string lower = res.ToLowerInvariant();
                    if (!lower.Contains("nazhdak")) continue;

                    if (lower.EndsWith(".ttf")) ext = ".ttf";
                    else if (lower.EndsWith(".otf")) ext = ".otf";
                    else continue;

                    resName = res;
                    break;
                }

                if (resName == null)
                {
                    MelonLogger.Warning("Tutorial: Nazhdak font (.ttf/.otf) not found in embedded resources.");
                    fontFailed = true;
                    return null;
                }

                using Stream stream = asm.GetManifestResourceStream(resName);
                using var ms = new MemoryStream();
                stream.CopyTo(ms);

                string dir = Path.Combine(MelonEnvironment.UserDataDirectory, "DirectorMode");
                Directory.CreateDirectory(dir);

                string path = Path.Combine(dir, "Nazhdak" + ext);
                File.WriteAllBytes(path, ms.ToArray());

                font = new Font(path);

                font.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(font);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("Tutorial: could not load font: " + e.Message);
                fontFailed = true;
            }

            return font;
        }

        private bool DrawButton(Rect baseRect, Texture2D tex, bool interactive, ref float hover, bool hoverEffect = true)
        {
            bool over = hoverEffect && interactive && baseRect.Contains(Event.current.mousePosition);

            if (Event.current.type == EventType.Repaint)
                hover = Mathf.MoveTowards(hover, over ? 1f : 0f, Time.unscaledDeltaTime / HoverEaseTime);

            float e = Mathf.SmoothStep(0f, 1f, hover);
            float scale = 1f + (HoverScale - 1f) * e;
            float alpha = 1f - (1f - HoverAlpha) * e;

            Vector2 c = baseRect.center;
            float w = baseRect.width * scale;
            float h = baseRect.height * scale;
            Rect r = new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);

            Color old = GUI.color;
            GUI.color = new Color(old.r, old.g, old.b, old.a * alpha);

            if (tex != null) GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, true);
            GUI.color = old;

            return interactive && GUI.Button(baseRect, "", GUIStyle.none);
        }



        private void AddPage(System.Reflection.Assembly asm, string resourceName)
        {
            Texture2D tex = LoadTexture(asm, resourceName);
            if (tex != null) pages.Add(tex);
        }

        private static Texture2D LoadTexture(System.Reflection.Assembly asm, string resourceName)
        {
            try
            {
                using Stream stream = asm.GetManifestResourceStream(resourceName);
                if (stream == null) return null;

                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                byte[] data = ms.ToArray();

                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(tex, data))
                {
                    MelonLogger.Warning("Tutorial: could not decode " + resourceName);
                    return null;
                }

                tex.hideFlags = HideFlags.HideAndDontSave;
                return tex;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("Tutorial: failed to load " + resourceName + ": " + e.Message);
                return null;
            }
        }

        private static string FindFolder()
        {
            string mods = MelonEnvironment.ModsDirectory;

            string[] candidates =
            {
                Path.Combine(mods, "DirectorMode", FolderName),
                Path.Combine(mods, FolderName),
                Path.Combine(MelonEnvironment.UserDataDirectory, "DirectorMode", FolderName)
            };

            foreach (string c in candidates)
                if (Directory.Exists(c)) return c;

            return null;
        }

        private static string SeenFile =>
            Path.Combine(MelonEnvironment.UserDataDirectory, "DirectorMode_TutorialSeen.txt");

    }
}
