using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace Gravship_Raids
{
    public class RaidStrategyWorker_GravshipAssault : RaidStrategyWorker
    {
        public override bool CanUsePawnGenOption(float pointsTotal, PawnGenOption option, List<PawnGenOptionWithXenotype> chosenGroups, Faction faction = null)
        {
            if (!base.CanUsePawnGenOption(pointsTotal, option, chosenGroups, faction))
            {
                return false;
            }
            return !option.kind.RaceProps.Animal;
        }

        public override void MakeLords(IncidentParms parms, List<Pawn> pawns)
        {
            EnemyGravshipInstance instance = FindInstance((Map)parms.target, pawns);
            AssignGuardCrew(instance, pawns);
            base.MakeLords(parms, pawns);
        }

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

        private static void AssignGuardCrew(EnemyGravshipInstance instance, List<Pawn> pawns)
        {
            if (instance == null || pawns.Count <= 1)
            {
                return;
            }
            if (!GravshipRaidsSettings.enableGravshipGuards)
            {
                return;
            }
            float fraction = GravshipRaidsSettings.ClampedGravshipGuardFraction();
            int guardCount = Mathf.Clamp(Mathf.Max(1, Mathf.FloorToInt(pawns.Count * fraction)), 1, pawns.Count - 1);

            List<Pawn> candidates = new List<Pawn>(pawns);
            int seed = Gen.HashCombineInt(instance.loadID, pawns.Count);
            Rand.PushState();
            try
            {
                Rand.Seed = seed;
                for (int i = 0; i < guardCount; i++)
                {
                    int index = Rand.Range(0, candidates.Count);
                    instance.guardCrew.Add(candidates[index]);
                    candidates.RemoveAt(index);
                }
            }
            finally
            {
                Rand.PopState();
            }

            Logger.Message($"RaidStrategyWorker_GravshipAssault.AssignGuardCrew: {instance} - total={pawns.Count}, guards={instance.guardCrew.Count}, attackers={pawns.Count - instance.guardCrew.Count}.");
        }
    }
}
