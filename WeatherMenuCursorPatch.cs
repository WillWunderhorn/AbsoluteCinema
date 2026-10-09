using HarmonyLib;
using Il2Cpp;

namespace DIrectorMode
{
    // Let the game show its native mouse pointer while the weather menu owns input.
    [HarmonyPatch(typeof(InterfaceManager), nameof(InterfaceManager.ShouldEnableMousePointer))]
    internal static class WeatherMenuCursorPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (Main.WeatherMenu != null && Main.WeatherMenu.IsOpen)
                __result = true;
        }
    }
}
