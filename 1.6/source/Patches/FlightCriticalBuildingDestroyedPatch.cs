using HarmonyLib;
using Verse;

namespace Gravship_Raids
{
    [HarmonyPatch(typeof(Thing), nameof(Thing.Destroy))]
    internal static class FlightCriticalBuildingDestroyedPatch
    {
        private static void Prefix(Thing __instance, out Map __state)
        {
            __state = __instance.Spawned ? __instance.Map : null;
        }

        private static void Postfix(Thing __instance, Map __state)
        {
            if (__state == null)
            {
                return;
            }
            MapComponent_GravshipRaid.GetFor(__state)?.NotifyShipBuildingDestroyed(__instance, __state);
        }
    }
}
