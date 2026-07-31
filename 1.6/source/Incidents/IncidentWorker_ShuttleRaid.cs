using RimWorld;
using Verse;

namespace Gravship_Raids
{
    public class IncidentWorker_ShuttleRaid : IncidentWorker_RaidEnemy
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!ModsConfig.RoyaltyActive)
            {
                return false;
            }

            if (!GravshipRaidsSettings.enableShuttleRaids)
            {
                return false;
            }

            if (GravshipRaidsSettings.shuttleEnableMinPlayerTechLevel && (int)Faction.OfPlayer.def.techLevel < (int)GravshipRaidsSettings.shuttleMinPlayerTechLevel)
            {
                Logger.Message($"IncidentWorker_ShuttleRaid.CanFireNowSub: declining - player faction techLevel {Faction.OfPlayer.def.techLevel} is below settings.shuttleMinPlayerTechLevel {GravshipRaidsSettings.shuttleMinPlayerTechLevel}.");
                return false;
            }

            Map map = parms.target as Map;
            if (map != null)
            {
                MapComponent_ShuttleRaid component = MapComponent_ShuttleRaid.GetFor(map);
                int maxConcurrent = GravshipRaidsSettings.shuttleMaxConcurrentPerMap;
                if (component != null && component.ActiveInstanceCount >= maxConcurrent)
                {
                    return false;
                }

                if (map.IsPocketMap)
                {
                    Logger.Message($"IncidentWorker_ShuttleRaid.CanFireNowSub: declining on map '{map}' - map is a pocket map.");
                    return false;
                }

                if (map.Tile.Valid && map.Tile.LayerDef != PlanetLayerDefOf.Surface)
                {
                    Logger.Message($"IncidentWorker_ShuttleRaid.CanFireNowSub: declining on map '{map}' - not a Surface-layer tile.");
                    return false;
                }

                int minColonists = GravshipRaidsSettings.shuttleMinColonistCount;
                if (map.mapPawns.FreeColonistsSpawnedCount < minColonists)
                {
                    Logger.Message($"IncidentWorker_ShuttleRaid.CanFireNowSub: declining on map '{map}' - fewer than {minColonists} free spawned colonist(s).");
                    return false;
                }
            }

            return base.CanFireNowSub(parms);
        }

        public override float ChanceFactorNow(IIncidentTarget target)
        {
            float factor = base.ChanceFactorNow(target);
            if (!GravshipRaidsSettings.enableShuttleRaids)
            {
                return 0f;
            }
            if (!ModsConfig.RoyaltyActive)
            {
                return 0f;
            }
            if (GravshipRaidsSettings.shuttleEnableMinPlayerTechLevel && (int)Faction.OfPlayer.def.techLevel < (int)GravshipRaidsSettings.shuttleMinPlayerTechLevel)
            {
                return 0f;
            }
            return factor * GravshipRaidsSettings.shuttleIncidentWeightFactor;
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

            if ((int)f.def.techLevel < (int)GravshipRaidsSettings.shuttleMinEnemyFactionTechLevel)
            {
                Logger.Message($"IncidentWorker_ShuttleRaid.FactionCanBeGroupSource: excluding faction '{f.def.defName}' - techLevel {f.def.techLevel} is below settings.shuttleMinEnemyFactionTechLevel {GravshipRaidsSettings.shuttleMinEnemyFactionTechLevel}.");
                return false;
            }

            if (!GravshipRaidsSettings.enableShuttleRaids)
            {
                return false;
            }
            if (parms.points < GravshipRaidsSettings.shuttleMinThreatPoints)
            {
                Logger.Message($"IncidentWorker_ShuttleRaid.FactionCanBeGroupSource: excluding faction '{f.def.defName}' - parms.points {parms.points} is below settings.shuttleMinThreatPoints {GravshipRaidsSettings.shuttleMinThreatPoints}.");
                return false;
            }

            Map map = parms.target as Map;
            if (map != null && map.Tile.Valid && map.Tile.LayerDef != PlanetLayerDefOf.Surface)
            {
                Logger.Message($"IncidentWorker_ShuttleRaid.FactionCanBeGroupSource: excluding faction '{f.def.defName}' - map '{map}' is not a Surface-layer tile.");
                return false;
            }

            ShuttleRaidTemplateDef selectedTemplate = PawnsArrivalModeWorker_ShuttleLanding.DebugForcedRequest?.SelectedTemplate;
            if (selectedTemplate != null)
            {
                if (!ShuttleRaidTemplateUtility.IsEligibleTemplate(selectedTemplate, f.def, parms.points, map))
                {
                    Logger.Message($"IncidentWorker_ShuttleRaid.FactionCanBeGroupSource: excluding faction '{f.def.defName}' - debug-selected template '{selectedTemplate.defName}' is not eligible for it at {parms.points} points.");
                    return false;
                }
            }
            else if (!ShuttleRaidTemplateUtility.HasEligibleTemplate(f.def, parms.points, map))
            {
                Logger.Message($"IncidentWorker_ShuttleRaid.FactionCanBeGroupSource: excluding faction '{f.def.defName}' - no ShuttleRaidTemplateDef is eligible for it at {parms.points} points.");
                return false;
            }

            if (map != null && !ShuttleRaidTemplateUtility.HasViableLandingArea(map, f.def, parms.points))
            {
                Logger.Message($"IncidentWorker_ShuttleRaid.FactionCanBeGroupSource: excluding faction '{f.def.defName}' - no viable shuttle landing site found on map '{map}'.");
                return false;
            }

            return true;
        }

        public override void ResolveRaidStrategy(IncidentParms parms, PawnGroupKindDef groupKind)
        {
            if (parms.raidStrategy == null)
            {
                parms.raidStrategy = GravshipRaidsDefOf.GR_ShuttleAssault;
            }
        }
    }
}
