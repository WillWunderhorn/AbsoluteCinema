using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace DIrectorMode
{
    // Cancel the time-scale factor on the player's requested movement speed,
    // keeping walking, crouching, and sprinting at their normal world speed.
    [HarmonyPatch(typeof(vp_FPSController), nameof(vp_FPSController.GetSlopeMultiplier))]
    internal static class SlowMotionMovementPatch
    {
        private static void Postfix(ref float __result)
        {
            SlowMotion slowMotion = Main.SlowMotion;
            if (slowMotion == null)
                return;

            float scale = slowMotion.CurrentScale;
            if (scale > 0f && !Mathf.Approximately(scale, 1f))
                __result *= 1f / Mathf.Max(0.01f, scale);
        }
    }
}
