using System;
using System.Reflection;
using UnityEngine;

namespace DIrectorMode
{
    // UnityExplorer is an optional mod, so detect its menu through reflection instead of referencing it.
    internal static class UnityExplorerState
    {
        private const BindingFlags StaticPublic = BindingFlags.Public | BindingFlags.Static;
        private static PropertyInfo? showMenuProperty;
        private static int cachedFrame = -1;
        private static bool cachedValue;

        public static bool IsOpen
        {
            get
            {
                int frame = Time.frameCount;
                if (frame == cachedFrame) return cachedValue;

                cachedFrame = frame;
                cachedValue = ReadOpenState();
                return cachedValue;
            }
        }

        private static bool ReadOpenState()
        {
            try
            {
                if (showMenuProperty == null)
                {
                    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        Type? type = assembly.GetType("UnityExplorer.UI.UIManager", false);
                        PropertyInfo? property = type?.GetProperty("ShowMenu", StaticPublic);
                        if (property?.PropertyType != typeof(bool) || property.GetIndexParameters().Length != 0)
                            continue;

                        showMenuProperty = property;
                        break;
                    }
                }

                return showMenuProperty?.GetValue(null) is bool isOpen && isOpen;
            }
            catch
            {
                return false;
            }
        }
    }
}
