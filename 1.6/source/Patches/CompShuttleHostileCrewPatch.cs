using HarmonyLib;
using RimWorld;
using Verse;

namespace Gravship_Raids
{
    [HarmonyPatch(typeof(CompShuttle), nameof(CompShuttle.IsAllowed))]
    internal static class CompShuttleHostileCrewPatch
    {
        private static void Postfix(CompShuttle __instance, Thing t, ref bool __result)
        {
            if (__result)
            {
                return;
            }
            if (!(t is Pawn pawn))
            {
                return;
            }
            Faction shuttleFaction = __instance.parent.Faction;
            if (shuttleFaction == null || shuttleFaction == Faction.OfPlayer)
            {
                return;
            }
            if (pawn.Faction == shuttleFaction)
            {
                __result = true;
            }
        }
    }
}
