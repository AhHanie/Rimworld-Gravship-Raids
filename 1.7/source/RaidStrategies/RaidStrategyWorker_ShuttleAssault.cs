using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace Gravship_Raids
{
    public class RaidStrategyWorker_ShuttleAssault : RaidStrategyWorker
    {
        public override bool CanUsePawnGenOption(float pointsTotal, PawnGenOption option, List<PawnGenOptionWithXenotype> chosenGroups, Faction faction = null)
        {
            if (!base.CanUsePawnGenOption(pointsTotal, option, chosenGroups, faction))
            {
                return false;
            }
            return !option.kind.RaceProps.Animal;
        }

        protected override LordJob MakeLordJob(IncidentParms parms, Map map, List<Pawn> pawns, int raidSeed)
        {
            EnemyShuttleRaidInstance instance = FindInstance(map, pawns);
            if (instance == null)
            {
                Logger.Error("RaidStrategyWorker_ShuttleAssault.MakeLordJob: no EnemyShuttleRaidInstance found for any generated pawn; falling back to LordJob_AssaultColony so the raid is not silently lost. This should never happen on the normal incident path - PawnsArrivalModeWorker_ShuttleLanding.Arrive always registers deployed pawns into an instance's crew list before this runs.");
                return new LordJob_AssaultColony(parms.faction, canTimeoutOrFlee: parms.canTimeoutOrFlee, canKidnap: false, sappers: false, useAvoidGridSmart: false, canSteal: false);
            }

            Logger.Message($"RaidStrategyWorker_ShuttleAssault.MakeLordJob: assigning LordJob_ShuttleRaid to {pawns.Count} pawn(s) for {instance}.");
            return new LordJob_ShuttleRaid(instance, parms.faction, parms.canTimeoutOrFlee);
        }

        private static EnemyShuttleRaidInstance FindInstance(Map map, List<Pawn> pawns)
        {
            MapComponent_ShuttleRaid component = MapComponent_ShuttleRaid.GetFor(map);
            if (component == null)
            {
                return null;
            }
            for (int i = 0; i < pawns.Count; i++)
            {
                EnemyShuttleRaidInstance found = component.GetInstanceForPawn(pawns[i]);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }
    }
}
