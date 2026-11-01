using HarmonyLib;
using Il2Cpp;

// enable/disable
namespace DIrectorMode
{
    [HarmonyPatch(typeof(FlyMode), nameof(FlyMode.Enter))]
    internal static class FlyModeEnterPatch
    {
        internal static void Postfix()
        {
            if (Main.FastFlyMode != null) Main.FastFlyMode.OnGameEntered();
        }
    }

    [HarmonyPatch(typeof(FlyMode), nameof(FlyMode.Exit))]
    internal static class FlyModeExitPatch
    {
        internal static void Postfix()
        {
            if (Main.FastFlyMode != null) Main.FastFlyMode.OnGameExited();
        }
    }

    [HarmonyPatch(typeof(vp_FPSCamera), nameof(vp_FPSCamera.LateUpdate))]
    internal static class FreeCamAfterCameraLateUpdatePatch
    {
        internal static void Postfix()
        {
            if (Main.FastFlyMode != null && Main.FastFlyMode.IsFreeCam)
                Main.FastFlyMode.LateUpdateMove();
        }
    }
}