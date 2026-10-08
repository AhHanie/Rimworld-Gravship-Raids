using HarmonyLib;
using RimWorld;
using Verse;

namespace Gravship_Raids
{
    [HarmonyPatch(typeof(Storyteller), nameof(Storyteller.TryFire))]
    internal static class ShipRaidFallbackPatch
    {
        private static readonly AccessTools.FieldRef<IncidentWorker, int> LastCheckCanRunTick =
            AccessTools.FieldRefAccess<IncidentWorker, int>("lastCheckCanRunTick");

        private static void Prefix(ref FiringIncident fi)
        {
            if (fi?.def == null || fi.parms == null || fi.parms.forced)
            {
                return;
            }

            string reason;
            bool viable;
            IncidentWorker worker = fi.def.Worker;
            if (worker is IncidentWorker_GravshipRaid gravshipWorker)
            {
                viable = gravshipWorker.CanRunAsShipRaid(fi.parms, out reason);
            }
            else if (worker is IncidentWorker_ShuttleRaid shuttleWorker)
            {
                viable = shuttleWorker.CanRunAsShipRaid(fi.parms, out reason);
            }
            else
            {
                return;
            }

            if (viable)
            {
                return;
            }

            IncidentDef original = fi.def;
            IncidentParms parms = fi.parms.ShallowCopy();
            if (parms.raidStrategy == GravshipRaidsDefOf.GR_GravshipAssault || parms.raidStrategy == GravshipRaidsDefOf.GR_ShuttleAssault)
            {
                parms.raidStrategy = null;
            }
            if (parms.raidArrivalMode == GravshipRaidsDefOf.GR_GravshipLanding || parms.raidArrivalMode == GravshipRaidsDefOf.GR_ShuttleLanding)
            {
                parms.raidArrivalMode = null;
            }
            if (parms.faction != null && (!parms.faction.HostileTo(Faction.OfPlayer) || parms.faction.deactivated))
            {
                parms.faction = null;
            }

            Logger.Message($"ShipRaidFallbackPatch: redirecting '{original.defName}' to '{IncidentDefOf.RaidEnemy.defName}' (target {parms.target}, {parms.points} points) - {reason}.");

            fi = new FiringIncident(IncidentDefOf.RaidEnemy, fi.source, parms)
            {
                sourceQuestPart = fi.sourceQuestPart
            };

            LastCheckCanRunTick(IncidentDefOf.RaidEnemy.Worker) = -1;
        }
    }
}
