using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace Gravship_Raids
{
    public class RaidStrategyWorker_GravshipAssault : RaidStrategyWorker
    {
        protected override LordJob MakeLordJob(IncidentParms parms, Map map, List<Pawn> pawns, int raidSeed)
        {
            EnemyGravshipInstance instance = FindInstance(map, pawns);
            if (instance == null)
            {
                Logger.Error("RaidStrategyWorker_GravshipAssault.MakeLordJob: no EnemyGravshipInstance found for any generated pawn; falling back to LordJob_AssaultColony so the raid is not silently lost. This should never happen on the normal incident path - PawnsArrivalModeWorker_GravshipLanding.Arrive always registers deployed pawns into an instance's crew list before this runs.");
                return new LordJob_AssaultColony(parms.faction, canTimeoutOrFlee: parms.canTimeoutOrFlee, canKidnap: false, sappers: false, useAvoidGridSmart: false, canSteal: false);
            }

            Logger.Message($"RaidStrategyWorker_GravshipAssault.MakeLordJob: assigning LordJob_GravshipRaid to {pawns.Count} pawn(s) for {instance}.");
            return new LordJob_GravshipRaid(instance, parms.faction, parms.canTimeoutOrFlee);
        }

        private static EnemyGravshipInstance FindInstance(Map map, List<Pawn> pawns)
        {
            MapComponent_GravshipRaid component = MapComponent_GravshipRaid.GetFor(map);
            if (component == null)
            {
                return null;
            }
            for (int i = 0; i < pawns.Count; i++)
            {
                EnemyGravshipInstance found = component.GetInstanceForPawn(pawns[i]);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }
    }
}
