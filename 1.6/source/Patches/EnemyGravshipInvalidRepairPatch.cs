using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Gravship_Raids
{
    [HarmonyPatch(typeof(JobGiver_AIFightEnemy), "TryGiveJob")]
    [HarmonyAfter("com.VanillaGravshipExpanded2")]
    internal static class EnemyGravshipInvalidRepairPatch
    {
        private static void Postfix(Pawn pawn, ref Job __result)
        {
            Thing target = __result?.def == JobDefOf.Repair ? __result.targetA.Thing : null;
            if (!(target is Building) || target.def.useHitPoints)
            {
                return;
            }
            if (MapComponent_GravshipRaid.GetFor(target.Map)?.IsActiveEnemyGravshipPart(target) == true)
            {
                __result = null;
            }
        }
    }
}
