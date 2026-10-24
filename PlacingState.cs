using System;
using System.Reflection;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace DIrectorMode
{
    internal static class PlacingState
    {
        private const BindingFlags All =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        private static readonly string[] ObjectMemberNames =
        {
            "m_ObjectToPlace"
        };

        private static bool resolved;
        private static MemberInfo boolMember;
        private static PropertyInfo objectMember;

        private static int cachedFrame = -1;
        private static bool cachedValue;
        private static bool hasCachedValue;

        public static void LateUpdatePinFov() { }

        public static bool IsPlacing
        {
            get
            {
                int frame = Time.frameCount;
                if (frame == cachedFrame) return cachedValue;

                cachedFrame = frame;

                bool value = Read();
                if (!hasCachedValue || value != cachedValue)
                    hasCachedValue = true;

                cachedValue = value;
                return cachedValue;
            }
        }

        private static bool Read()
        {
            object manager = GetPlayerManager();
            if (manager == null) return false;

            if (!resolved) Resolve(manager.GetType());

            try
            {
                if (boolMember != null)
                {
                    object result = boolMember is MethodInfo m
                        ? m.Invoke(manager, null)
                        : ((PropertyInfo)boolMember).GetValue(manager);

                    return result is bool b && b;
                }

                if (objectMember != null)
                {
                    object value = objectMember.GetValue(manager);

                    if (value is UnityEngine.Object unityObject) return unityObject != null;
                    return value != null;
                }
            }
            catch
            {
            }

            return false;
        }

        internal static void Invalidate()
        {
            resolved = false;
            boolMember = null;
            objectMember = null;
            cachedFrame = -1;
            cachedValue = false;
            hasCachedValue = false;
        }

        private static object GetPlayerManager()
        {
            try
            {
                MethodInfo m = typeof(GameManager).GetMethod("GetPlayerManagerComponent", All, null, Type.EmptyTypes, null);
                return m != null ? m.Invoke(null, null) : null;
            }
            catch
            {
                return null;
            }
        }

        private static void Resolve(Type type)
        {
            resolved = true;

            foreach (string name in ObjectMemberNames)
            {
                PropertyInfo property = SafeGetProperty(type, name);
                if (property != null && !property.PropertyType.IsValueType)
                {
                    objectMember = property;
                    return;
                }
            }
        }

        private static PropertyInfo SafeGetProperty(Type type, string name)
        {
            try { return type.GetProperty(name, All); }
            catch { return null; }
        }
    }
}