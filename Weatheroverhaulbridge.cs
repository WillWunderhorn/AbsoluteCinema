using System;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace DIrectorMode
{
    internal static class WeatherOverhaulBridge
    {
        private const string TypeName = "WeatherOverhaul.WeatherOverhaulApi";
        private const string MethodName = "TryGetCurrentStageDisplayName";

        private static bool resolved;
        private static MethodInfo method;

        private static int cachedFrame = -1;
        private static bool cachedOk;
        private static string cachedName = "";

        public static bool IsInstalled
        {
            get
            {
                Resolve();
                return method != null;
            }
        }

        internal static void Invalidate()
        {
            resolved = false;
            method = null;
            cachedFrame = -1;
            cachedOk = false;
            cachedName = "";
        }

        public static bool TryGetCurrentStageName(out string name)
        {
            int frame = Time.frameCount;

            if (frame != cachedFrame)
            {
                cachedFrame = frame;
                cachedOk = Query(out cachedName);
            }

            name = cachedName;
            return cachedOk;
        }

        private static bool Query(out string name)
        {
            name = "";

            Resolve();
            if (method == null) return false;

            try
            {
                object[] args = { "" };
                object result = method.Invoke(null, args);
                string value = args[0] as string;

                if (result is bool ok && ok && !string.IsNullOrWhiteSpace(value))
                {
                    name = value;
                    return true;
                }
            }
            catch
            {}

            return false;
        }

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type type = asm.GetType(TypeName, false);
                    if (type == null) continue;

                    MethodInfo m = type.GetMethod(MethodName, BindingFlags.Public | BindingFlags.Static);
                    if (m == null || m.ReturnType != typeof(bool)) continue;

                    ParameterInfo[] p = m.GetParameters();
                    if (p.Length != 1 || p[0].ParameterType != typeof(string).MakeByRefType()) continue;

                    method = m;
                    return;
                }
                catch
                {}
            }
        }
    }
}