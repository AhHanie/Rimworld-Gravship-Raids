using HarmonyLib;
using RimWorld;
using Verse;

namespace Gravship_Raids
{
    [HarmonyPatch(typeof(ShortCircuitUtility), nameof(ShortCircuitUtility.TryShortCircuitInRain))]
    internal static class EnemyGravshipRainShortCircuitPatch
    {
        private static bool Prefix(Thing thing, ref bool __result)
        {
            if (thing == null || !thing.Spawned)
            {
                return true;
            }
            if (MapComponent_GravshipRaid.GetFor(thing.Map)?.IsActiveEnemyGravshipPart(thing) != true)
            {
                return true;
            }

            CompPowerTrader powerTrader = thing.TryGetComp<CompPowerTrader>();
            CompPowerBattery battery = thing.TryGetComp<CompPowerBattery>();
            bool shortCircuits = (powerTrader != null && powerTrader.PowerOn && powerTrader.Props.shortCircuitInRain)
                || (battery != null && battery.StoredEnergy > 100f);
            if (!shortCircuits)
            {
                __result = false;
                return false;
            }

            GenExplosion.DoExplosion(thing.OccupiedRect().RandomCell, thing.Map, 1.9f, DamageDefOf.Flame, null);
            __result = true;
            return false;
        }
    }
}
