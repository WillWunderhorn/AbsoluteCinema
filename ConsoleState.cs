using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace DIrectorMode
{
    internal static class ConsoleState
    {
        private static readonly string[] TypeNames = { "Il2Cpp.uConsole", "uConsole" };
        private static readonly string[] InstanceNames = {
            "m_Instance", "Instance", "s_Instance", "m_Singleton"
        };
        private static readonly string[] OpenNames =
        {
            "m_On", "m_Active", "m_Activated", "m_Visible", "m_IsVisible",
            "m_Open", "m_IsOpen", "m_Shown", "m_Enabled",
            "On", "Active", "Visible", "IsOn", "IsOpen", "IsActive", "IsVisible"
        };

        private const BindingFlags All =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        private static bool resolved;
        private static PropertyInfo openProp;
        private static PropertyInfo instanceProp;
        private static bool openIsStatic;
        private static int cachedFrame = -1;
        private static bool cachedValue;
        public static bool IsOpen
        {
            get
            {
                int frame = Time.frameCount;
                if (frame == cachedFrame) return cachedValue;

                cachedFrame = frame;
                cachedValue = Read();
                return cachedValue;
            }
        }

        internal static void Invalidate()
        {
            resolved = false;
            openProp = null;
            instanceProp = null;
            openIsStatic = false;
            cachedFrame = -1;
            cachedValue = false;
        }

        private static bool Read()
        {
            if (!resolved) Resolve();
            if (openProp == null) return false;

            try
            {
                object target = null;

                if (!openIsStatic)
                {
                    if (instanceProp == null) return false;
                    target = instanceProp.GetValue(null);
                    if (target == null) return false;
                }

                object value = openProp.GetValue(target);
                return value is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        private static void Resolve()
        {
            resolved = true;

            Type type = FindConsoleType();
            
            //debug
            if (type == null)
            {
                MelonLogger.Warning("Console class was not found!!!");
                return;
            }

            foreach (string name in OpenNames)
            {
                PropertyInfo p = SafeGetProperty(type, name);

                if (p != null && p.PropertyType == typeof(bool) && p.GetIndexParameters().Length == 0)
                {
                    openProp = p;
                    break;
                }
            }

            if (openProp == null)
            {
                MelonLogger.Warning("ConsoleState: no 'open' flag found on " + type.FullName +
                                    ". Mod keys will NOT be blocked while the console is open. " +
                                    "Bool members of that class: " + ListBoolMembers(type));
                return;
            }

            MethodInfo getter = openProp.GetGetMethod(true);
            openIsStatic = getter != null && getter.IsStatic;

            if (!openIsStatic)
            {
                foreach (string name in InstanceNames)
                {
                    PropertyInfo p = SafeGetProperty(type, name);
                    MethodInfo g = p != null ? p.GetGetMethod(true) : null;

                    if (p != null && g != null && g.IsStatic)
                    {
                        instanceProp = p;
                        break;
                    }
                }

                if (instanceProp == null)
                {
                    //Debug
                    //MelonLogger.Warning("ConsoleState: found " + type.Name + "." + openProp.Name +
                    //                    ", but no static instance accessor. Mod keys will NOT be blocked.");
                    openProp = null;
                    return;
                }
            }

            //Debug
            //MelonLogger.Msg("ConsoleState: using " + type.Name + "." + openProp.Name +
            //                (openIsStatic ? " (static)" : " (instance)"));
        }

        private static Type FindConsoleType()
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (string name in TypeNames)
                {
                    try
                    {
                        Type t = asm.GetType(name, false);
                        if (t != null) return t;
                    }
                    catch
                    {
                        //Hello log!
                    }
                }
            }

            return null;
        }

        private static PropertyInfo SafeGetProperty(Type type, string name)
        {
            try { return type.GetProperty(name, All); }
            catch { return null; }
        }

        private static string ListBoolMembers(Type type)
        {
            var names = new List<string>();

            try
            {
                foreach (PropertyInfo p in type.GetProperties(All))
                    if (p.PropertyType == typeof(bool)) names.Add(p.Name);
            }
            catch
            {
                // Hi log!
            }

            return names.Count == 0 ? "(none)" : string.Join(", ", names);
        }
    }
}