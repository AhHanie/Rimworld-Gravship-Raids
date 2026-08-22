using RimWorld;
using Verse;

namespace Gravship_Raids
{
    public class IncidentWorker_GravshipRaid : IncidentWorker_RaidEnemy
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!GravshipRaidEligibility.CanUseGravshipRaidOnMap(parms.target as Map, out string reason, parms))
            {
                Logger.Message($"IncidentWorker_GravshipRaid.CanFireNowSub: declining - {reason}.");
                return false;
            }

            return base.CanFireNowSub(parms);
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
                Logger.Message($"IncidentWorker_GravshipRaid.FactionCanBeGroupSource: excluding faction '{f.def.defName}' - {reason}.");
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
