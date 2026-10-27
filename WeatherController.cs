using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace DIrectorMode
{
    public class WeatherOption
    {
        public WeatherStage Stage;
        public string Id;
        public string Name;
    }

    public class WindStrengthOption
    {
        public WindStrength Value;
        public string Id;
        public string Name;
    }

    public class WindDirectionOption
    {
        public WindDirection Value;
        public string Id;
        public string Name;
    }

    public class WeatherController
    {
        private static readonly string[] WeatherOrder =
        {
            "clear", "partly_cloudy", "cloudy", "light_fog", "dense_fog",
            "light_snow", "heavy_snow", "blizzard", "clear_aurora", "electrostatic_fog"
        };

        private static readonly string[] DirectionOrder =
        {
            "North", "NorthEast", "East", "SouthEast", "South", "SouthWest", "West", "NorthWest"
        };

        private static readonly Dictionary<string, string> DirectionIds = new Dictionary<string, string>
        {
            { "North", "n" }, { "NorthEast", "ne" }, { "East", "e" }, { "SouthEast", "se" },
            { "South", "s" }, { "SouthWest", "sw" }, { "West", "w" }, { "NorthWest", "nw" }
        };

        private const BindingFlags All =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        private readonly List<WeatherOption> weathers = new List<WeatherOption>();
        private readonly List<WindStrengthOption> strengths = new List<WindStrengthOption>();
        private readonly List<WindDirectionOption> directions = new List<WindDirectionOption>();

        private bool consoleResolved;
        private MethodInfo runCommand;
        private MethodInfo runCommandSilent;

        private bool haveUnlockedWindSpeed;
        private float unlockedWindSpeed;
        public bool TriggerSave() => RunConsoleSilent("save");

        private int weatherIndex = -1;
        private int windIndex = -1;

        public IReadOnlyList<WeatherOption> Weathers => weathers;
        public IReadOnlyList<WindStrengthOption> WindStrengths => strengths;
        public IReadOnlyList<WindDirectionOption> WindDirections => directions;

        // Catalog
        public void Init()
        {
            weathers.Clear();
            strengths.Clear();
            directions.Clear();
            BuildWeatherList();
            BuildWindLists();
        }

        private void BuildWeatherList()
        {
            foreach (WeatherStage stage in Enum.GetValues(typeof(WeatherStage)))
            {
                string name = stage.ToString();
                if (IsPlaceholder(name)) continue;

                weathers.Add(new WeatherOption { Stage = stage, Id = ToSnake(name), Name = Humanize(name) });
            }

            weathers.Sort((a, b) => OrderOf(WeatherOrder, a.Id).CompareTo(OrderOf(WeatherOrder, b.Id)));
        }

        public string GetCurrentWeatherName()
        {
            string name;
            if (WeatherOverhaulBridge.TryGetCurrentStageName(out name))
                return name;

            WeatherOption w = GetCurrentWeather();
            return w != null ? GetDisplayName(w) : "?";
        }

        private void BuildWindLists()
        {
            foreach (WindStrength s in Enum.GetValues(typeof(WindStrength)))
            {
                string name = s.ToString();
                if (IsPlaceholder(name)) continue;

                strengths.Add(new WindStrengthOption { Value = s, Id = ToSnake(name), Name = Humanize(name) });
            }

            foreach (WindDirection d in Enum.GetValues(typeof(WindDirection)))
            {
                string name = d.ToString();
                if (IsPlaceholder(name)) continue;

                string id;
                if (!DirectionIds.TryGetValue(name, out id)) id = ToSnake(name);

                directions.Add(new WindDirectionOption { Value = d, Id = id, Name = id.ToUpperInvariant() });
            }

            //(N, NE, E, SE, S, SW, W, NW)
            directions.Sort((a, b) => OrderOf(DirectionOrder, a.Value.ToString()).CompareTo(OrderOf(DirectionOrder, b.Value.ToString())));
        }

        private static bool IsPlaceholder(string name)
        {
            return name.Equals("Undefined", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Invalid", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Count", StringComparison.OrdinalIgnoreCase);
        }

        private static int OrderOf(string[] order, string id)
        {
            int i = Array.IndexOf(order, id);
            return i < 0 ? 999 : i;
        }

        // Reading the current state
        public string GetDisplayName(WeatherOption weather)
        {
            if (weather == null) return "";

            try
            {
                Weather w = GetWeather();
                if (w != null)
                {
                    string name = w.GetWeatherStageDisplayName(weather.Stage);
                    if (!string.IsNullOrEmpty(name)) return name;
                }
            }
            catch
            {
                // fall through to the plain name
            }

            return weather.Name;
        }

        public WeatherOption GetCurrentWeather()
        {
            try
            {
                Weather w = GetWeather();
                if (w == null) return null;

                WeatherStage stage = w.GetWeatherStage();
                return weathers.FirstOrDefault(x => x.Stage == stage);
            }
            catch
            {
                return null;
            }
        }

        public WindStrengthOption GetCurrentWindStrength()
        {
            try
            {
                Wind w = GetWind();
                if (w == null) return null;

                WindStrength s = w.GetStrength();
                return strengths.FirstOrDefault(x => x.Value == s);
            }
            catch
            {
                return null;
            }
        }

        public WindDirectionOption GetCurrentWindDirection()
        {
            try
            {
                Wind w = GetWind();
                if (w == null) return null;

                float angle = w.m_CurrentAngleDeg;
                WindDirectionOption best = null;
                float bestDiff = float.MaxValue;

                foreach (WindDirectionOption d in directions)
                {
                    float diff = Mathf.Abs(Mathf.DeltaAngle(Wind.DirectionToAngle(d.Value), angle));

                    if (diff < bestDiff)
                    {
                        bestDiff = diff;
                        best = d;
                    }
                }

                return best;
            }
            catch
            {
                return null;
            }
        }

        // lockWeather
        public bool SetWeather(WeatherOption weather, bool lockWeather = true, bool instant = false)
        {
            if (weather == null) return false;

            string command = lockWeather
                ? (instant ? "lock_weather_instant " : "lock_weather ") + weather.Id
                : "set_weather " + weather.Id;

            return RunConsole(command);
        }

        public bool SetWind(WindStrengthOption strength, WindDirectionOption direction, bool lockWind = true, bool instant = false)
        {
            if (strength == null || direction == null) return false;

            // "not locked"
            if (lockWind) RememberUnlockedWindSpeed();

            string verb = lockWind ? "lock_wind" : (instant ? "set_wind_immediate" : "set_wind");

            return RunConsole(verb + " " + strength.Id + " " + direction.Id);
        }

        public bool SetTimeOfDay(int minuteOfDay)
        {
            minuteOfDay = Mathf.Clamp(minuteOfDay, 0, 1439);

            try
            {
                object tod = CallGameManager("GetTimeOfDayComponent");
                if (tod == null) return false;

                MethodInfo setter = tod.GetType().GetMethod("SetNormalizedTime", All, null,
                    new[] { typeof(float) }, null);
                if (setter == null)
                {
                    MelonLogger.Warning("WeatherController: minute-precision time setter was not found");
                    return false;
                }

                setter.Invoke(tod, new object[] { minuteOfDay / 1440f });
                //MelonLogger.Msg("WeatherController: set time to " + (minuteOfDay / 60).ToString("00") + ":" + (minuteOfDay % 60).ToString("00"));
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("WeatherController: could not set time: " + e.Message);
                return false;
            }
        }

        public bool UnlockWind()
        {
            try
            {
                Wind w = GetWind();
                if (w == null) return false;

                w.m_LockedWindSpeed = float.NegativeInfinity;
                //MelonLogger.Msg("WeatherController: wind unlocked");
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("WeatherController: could not unlock wind: " + e.Message);
                return false;
            }
        }

        public bool IsWindLocked()
        {
            try
            {
                Wind w = GetWind();
                return w != null && !float.IsNegativeInfinity(w.m_LockedWindSpeed);
            }
            catch
            {
                return false;
            }
        }

        public bool IsWeatherLocked()
        {
            try
            {
                WeatherTransition wt = GetWeatherTransition();
                return wt != null && wt.IsLocked();
            }
            catch
            {
                return false;
            }
        }

        public bool UnlockWeather()
        {
            // F6 scan result: only console:set_weather clear clears IsLocked()
            try
            {
                if (!IsWeatherLocked())
                    return true;

                if (!RunConsoleSilent("set_weather clear"))
                    return false;

                try
                {
                    WeatherTransition wt = GetWeatherTransition();
                    if (wt != null && wt.IsLocked())
                        wt.m_UnmanagedWeatherStage = WeatherStage.Undefined;
                }
                catch { }

                return !IsWeatherLocked();
            }
            catch (Exception e)
            {
                MelonLogger.Warning("WeatherController: could not unlock weather: " + e.Message);
                return false;
            }
        }

        public bool UnlockAll()
        {
            bool wind = UnlockWind();
            bool weather = UnlockWeather();
            return wind && weather;
        }

        private void RememberUnlockedWindSpeed()
        {
            if (haveUnlockedWindSpeed) return;

            try
            {
                Wind w = GetWind();
                if (w == null) return;

                unlockedWindSpeed = w.m_LockedWindSpeed;
                haveUnlockedWindSpeed = true;
                //MelonLogger.Msg("WeatherController: wind not locked yet, m_LockedWindSpeed = " + unlockedWindSpeed);
            }
            catch
            {
                // 
            }
        }

        public void CycleWeather()
        {
            if (weathers.Count == 0) return;

            weatherIndex = (weatherIndex + 1) % weathers.Count;
            WeatherOption w = weathers[weatherIndex];

            if (SetWeather(w))
                Main.Notify("Weather: " + GetDisplayName(w));
        }

        public void CycleWind()
        {
            if (strengths.Count == 0 || directions.Count == 0) return;

            windIndex = (windIndex + 1) % (strengths.Count * directions.Count);

            WindStrengthOption s = strengths[windIndex / directions.Count];
            WindDirectionOption d = directions[windIndex % directions.Count];

            if (SetWind(s, d))
                Main.Notify("Wind: " + s.Name + " " + d.Name);
        }

        public void ShowCurrent()
        {
            WeatherOption w = GetCurrentWeather();
            WindStrengthOption s = GetCurrentWindStrength();
            WindDirectionOption d = GetCurrentWindDirection();

            if (w == null && s == null)
            {
                Main.Notify("Weather: not available here");
                return;
            }

            Main.Notify("Now: " + (w != null ? GetDisplayName(w) : "?") + (IsWeatherLocked() ? " (locked)" : "") +
            ", wind " + (s != null ? s.Name : "?") + " " + (d != null ? d.Name : "?") +
            (IsWindLocked() ? " (locked)" : ""));
        }

        //console
        private static Weather GetWeather()
        {
            return CallGameManager("GetWeatherComponent") as Weather;
        }


        private static Wind GetWind()
        {
            return CallGameManager("GetWindComponent") as Wind;
        }

        private static WeatherTransition GetWeatherTransition()
        {
            return CallGameManager("GetWeatherTransitionComponent") as WeatherTransition;
        }

        private static object CallGameManager(string methodName)
        {
            try
            {
                MethodInfo m = typeof(GameManager).GetMethod(methodName, All, null, Type.EmptyTypes, null);
                return m != null ? m.Invoke(null, null) : null;
            }
            catch
            {
                return null;
            }
        }

        private bool RunConsole(string command)
        {
            if (!consoleResolved) ResolveConsole();

            if (runCommand == null)
            {
                Main.Notify("Weather: the game's console command runner was not found");
                return false;
            }

            try
            {
                runCommand.Invoke(null, new object[] { command });
                //MelonLogger.Msg("WeatherController: ran '" + command + "'");
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("WeatherController: command '" + command + "' failed: " + e.Message);
                return false;
            }
        }

        private bool RunConsoleSilent(string command)
        {
            if (!consoleResolved) ResolveConsole();

            MethodInfo runner = runCommandSilent ?? runCommand;
            if (runner == null)
            {
                Main.Notify("Director Mode: the game's console command runner was not found");
                return false;
            }

            try
            {
                runner.Invoke(null, new object[] { command });
                //MelonLogger.Msg("WeatherController: ran command silently: " + command);
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("WeatherController: silent command '" + command + "' failed: " + e.Message);
                return false;
            }
        }

        private void ResolveConsole()
        {
            consoleResolved = true;

            Type type = typeof(GameManager).Assembly.GetType("Il2Cpp.uConsole", false);

            if (type == null)
            {
                MelonLogger.Warning("WeatherController: class Il2Cpp.uConsole not found.");
                return;
            }

            foreach (MethodInfo m in type.GetMethods(All))
            {
                if (!m.IsStatic || (m.Name != "RunCommand" && m.Name != "RunCommandSilent")) continue;

                ParameterInfo[] p = m.GetParameters();
                if (p.Length == 1 && p[0].ParameterType == typeof(string))
                {
                    if (m.Name == "RunCommandSilent") runCommandSilent = m;
                    else runCommand = m;
                }
            }

            if (runCommand == null && runCommandSilent == null)
                MelonLogger.Warning("WeatherController: uConsole.RunCommand(string) not found.");
        }

        private static void DumpMembers(string typeName, Func<string, bool> nameFilter)
        {
            Type t = typeof(GameManager).Assembly.GetType(typeName, false);

            if (t == null)
            {
                //MelonLogger.Msg("WeatherController: class " + typeName + " not found");
                return;
            }

            var lines = new List<string>();

            foreach (MemberInfo m in t.GetMembers(All | BindingFlags.DeclaredOnly))
            {
                string n = m.Name;

                if (n.StartsWith("get_") || n.StartsWith("set_") || n.StartsWith("Native")) continue;
                if (m.MemberType == MemberTypes.Constructor || m.MemberType == MemberTypes.NestedType) continue;
                if (nameFilter != null && !nameFilter(n)) continue;

                string extra = "";
                if (m is PropertyInfo p) extra = " : " + p.PropertyType.Name;
                else if (m is MethodInfo mi) extra = "(" + string.Join(", ", mi.GetParameters().Select(x => x.ParameterType.Name)) + ")";

                lines.Add(m.MemberType + " " + n + extra);
            }

            //MelonLogger.Msg("WeatherController: " + typeName + " members: " + string.Join(" | ", lines));
        }

        private static string ToSnake(string name)
        {
            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];

                if (char.IsUpper(c) && i > 0 && name[i - 1] != '_')
                    sb.Append('_');

                sb.Append(char.ToLowerInvariant(c));
            }

            return sb.ToString();
        }

        private static string Humanize(string name)
        {
            string[] parts = ToSnake(name).Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);

            return string.Join(" ", parts);
        }

        public int GetCurrentHour()
        {
            try
            {
                object tod = CallGameManager("GetTimeOfDayComponent");
                if (tod == null) return -1;

                MethodInfo m = tod.GetType().GetMethod("GetHour", All, null, Type.EmptyTypes, null);
                object v = m != null ? m.Invoke(tod, null) : null;
                return v is int h ? h : -1;
            }
            catch
            {
                return -1;
            }
        }

        public bool IsGodModeEnabled()
        {
            try
            {
                PlayerManager player = GameManager.GetPlayerManagerComponent();
                return player != null && player.m_God;
            }
            catch { return false; }
        }

        public bool IsGhostModeEnabled()
        {
            try
            {
                PlayerManager player = GameManager.GetPlayerManagerComponent();
                return player != null && player.m_Ghost;
            }
            catch { return false; }
        }

        public bool ToggleGodMode() => RunConsoleSilent("god");
        public bool ToggleGhostMode() => RunConsoleSilent("ghost");

        public int GetCurrentTimeMinutes()
        {
            try
            {
                object tod = CallGameManager("GetTimeOfDayComponent");
                if (tod == null) return -1;

                MethodInfo getter = tod.GetType().GetMethod("GetNormalizedTime", All, null, Type.EmptyTypes, null);
                object value = getter != null ? getter.Invoke(tod, null) : null;
                if (value is float normalized)
                    return Mathf.Clamp(Mathf.RoundToInt(Mathf.Repeat(normalized, 1f) * 1440f), 0, 1439);

                MelonLogger.Warning("WeatherController: normalized game time could not be read");
                return -1;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("WeatherController: could not read current time: " + e.Message);
                return -1;
            }
        }

        public string GetWeatherOverhaulStageId()
        {
            try
            {
                Type runtimeType = typeof(GameManager).Assembly.GetType("WeatherOverhaul.Weather.CustomWeatherStageRuntime");
                if (runtimeType == null) return null;

                MethodInfo tryGet = runtimeType.GetMethod("TryGetActiveStageId", BindingFlags.Public | BindingFlags.Static);
                if (tryGet == null) return null;

                object[] args = { null };
                bool success = (bool)tryGet.Invoke(null, args);

                if (success && args[0] != null)
                {
                    return args[0].ToString();
                }
            }
            catch { }
            return null;
        }
    }

}