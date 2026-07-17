using RimWorld;
using Verse;

namespace Gravship_Raids
{
    public class IncidentWorker_GravshipRaid : IncidentWorker_RaidEnemy
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!ModsConfig.OdysseyActive)
            {
                return false;
            }

            if (!GravshipRaidsSettings.enabled)
            {
                return false;
            }

            if (GravshipRaidsSettings.enableMinPlayerTechLevel && (int)Faction.OfPlayer.def.techLevel < (int)GravshipRaidsSettings.minPlayerTechLevel)
            {
                Logger.Message($"IncidentWorker_GravshipRaid.CanFireNowSub: declining - player faction techLevel {Faction.OfPlayer.def.techLevel} is below settings.minPlayerTechLevel {GravshipRaidsSettings.minPlayerTechLevel}.");
                return false;
            }

            Map map = parms.target as Map;
            if (map != null)
            {
                MapComponent_GravshipRaid component = MapComponent_GravshipRaid.GetFor(map);
                int maxConcurrent = GravshipRaidsSettings.maxConcurrentShipsPerMap;
                if (component != null && component.ActiveInstanceCount >= maxConcurrent)
                {
                    return false;
                }

                if (!MapSuitabilityUtility.IsMapSuitable(map, parms, out string unsuitableReason))
                {
                    Logger.Message($"IncidentWorker_GravshipRaid.CanFireNowSub: declining on map '{map}' - {unsuitableReason}.");
                    return false;
                }
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

            if (!f.def.humanlikeFaction)
            {
                return false;
            }

            if ((int)f.def.techLevel < (int)GravshipRaidsSettings.minEnemyFactionTechLevel)
            {
                Logger.Message($"IncidentWorker_GravshipRaid.FactionCanBeGroupSource: excluding faction '{f.def.defName}' - techLevel {f.def.techLevel} is below settings.minEnemyFactionTechLevel {GravshipRaidsSettings.minEnemyFactionTechLevel}.");
                return false;
            }

            if (!GravshipRaidsSettings.enabled)
            {
                return false;
            }
            if (parms.points < GravshipRaidsSettings.minThreatPoints)
            {
                Logger.Message($"IncidentWorker_GravshipRaid.FactionCanBeGroupSource: excluding faction '{f.def.defName}' - parms.points {parms.points} is below settings.minThreatPoints {GravshipRaidsSettings.minThreatPoints}.");
                return false;
            }

            Map map = parms.target as Map;
            if (map != null && map.Tile.Valid && map.Tile.LayerDef != PlanetLayerDefOf.Surface)
            {
                Logger.Message($"IncidentWorker_GravshipRaid.FactionCanBeGroupSource: excluding faction '{f.def.defName}' - map '{map}' is not a Surface-layer tile (layer '{map.Tile.LayerDef}').");
                return false;
            }

            if (!GravshipRaidTemplateUtility.HasEligibleTemplate(f.def, parms.points, map))
            {
                Logger.Message($"IncidentWorker_GravshipRaid.FactionCanBeGroupSource: excluding faction '{f.def.defName}' - no GravshipRaidTemplateDef is eligible for it at {parms.points} points.");
                return false;
            }

            if (map != null && !GravshipLandingSiteFinder.HasViableLandingArea(map, f.def, parms.points))
            {
                Logger.Message($"IncidentWorker_GravshipRaid.FactionCanBeGroupSource: excluding faction '{f.def.defName}' - GravshipLandingSiteFinder found no viable landing area on map '{map}' (roofed/home-area/foundation/building/reachability pre-screen found nothing in the cheap search).");
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
