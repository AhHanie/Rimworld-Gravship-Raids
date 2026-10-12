using System.Linq;
using RimWorld;
using Verse;

namespace Gravship_Raids
{
    public class IncidentWorker_GravshipRaid : IncidentWorker_RaidEnemy
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!CanRunAsShipRaid(parms, out string reason))
            {
                Logger.Message($"IncidentWorker_GravshipRaid.CanFireNowSub: declining - {reason}.");
                return false;
            }

            return true;
        }

        internal bool CanRunAsShipRaid(IncidentParms parms, out string reason)
        {
            if (!(parms.target is Map map))
            {
                reason = "incident target is not a Map";
                return false;
            }

            if (!GravshipRaidEligibility.CanUseGravshipRaidOnMap(map, out reason, parms))
            {
                return false;
            }

            if (parms.faction != null)
            {
                return GravshipRaidEligibility.CanUseGravshipRaidForFaction(parms.faction, map, parms.points, out reason);
            }

            IncidentParms probe = parms.ShallowCopy();
            if (!Find.FactionManager.AllFactions.Any(f => FactionCanBeGroupSource(f, probe)))
            {
                reason = $"no hostile faction can send a gravship raid at {parms.points} points";
                return false;
            }

            return true;
        }

        public override float ChanceFactorNow(IIncidentTarget target)
        {
            float factor = base.ChanceFactorNow(target);
            if (!GravshipRaidsSettings.enabled)
            {
                return 0f;
            }
            if (GravshipRaidsSettings.enableMinPlayerTechLevel && (int)Faction.OfPlayer.def.techLevel < (int)GravshipRaidsSettings.minPlayerTechLevel)
            {
                return 0f;
            }
            return factor * GravshipRaidsSettings.incidentWeightFactor;
        }

        public override bool FactionCanBeGroupSource(Faction f, IncidentParms parms, bool desperate = false)
        {
            if (!base.FactionCanBeGroupSource(f, parms, desperate))
            {
                return false;
            }

            Map map = parms.target as Map;
            if (!GravshipRaidEligibility.CanUseGravshipRaidForFaction(f, map, parms.points, out string reason))
            {
                Logger.Message($"IncidentWorker_GravshipRaid.FactionCanBeGroupSource: probe found faction '{f.def.defName}' ineligible - {reason}.");
                return false;
            }

            return true;
        }

        public override void ResolveRaidStrategy(IncidentParms parms, PawnGroupKindDef groupKind)
        {
            // Force our own strategy unconditionally instead of running the vanilla weighted-selection
            // loop over every registered RaidStrategyDef - falling back to a vanilla strategy here would
            // silently bypass the ship, which is not acceptable.
            if (parms.raidStrategy == null)
            {
                parms.raidStrategy = GravshipRaidsDefOf.GR_GravshipAssault;
            }
        }
    }
}
