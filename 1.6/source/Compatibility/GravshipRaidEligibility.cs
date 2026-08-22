using System.Linq;
using RimWorld;
using Verse;

namespace Gravship_Raids
{
    // Shared applicability gate for "can a gravship raid happen here, for this faction, right now".
    // IncidentWorker_GravshipRaid uses the two granular halves (map-only, faction-only) to keep its existing
    // CanFireNowSub/FactionCanBeGroupSource split; WinstonWavesCompatibility uses the combined, parms-based
    // overload since it only ever has one candidate faction/map/points triple to check at a time.
    internal static class GravshipRaidEligibility
    {
        internal static bool CanUseGravshipRaidOnMap(Map map, out string reason, IncidentParms cacheKey = null)
        {
            reason = null;

            if (!ModsConfig.OdysseyActive)
            {
                reason = "Odyssey DLC is not active";
                return false;
            }

            if (!GravshipRaidsSettings.enabled)
            {
                reason = "gravship raids are disabled in settings";
                return false;
            }

            if (GravshipRaidsSettings.enableMinPlayerTechLevel && (int)Faction.OfPlayer.def.techLevel < (int)GravshipRaidsSettings.minPlayerTechLevel)
            {
                reason = $"player faction techLevel {Faction.OfPlayer.def.techLevel} is below settings.minPlayerTechLevel {GravshipRaidsSettings.minPlayerTechLevel}";
                return false;
            }

            if (map == null)
            {
                // Callers that don't have a Map target yet (or never will) just skip the map-specific checks
                // below, matching the original IncidentWorker_GravshipRaid.CanFireNowSub behavior.
                return true;
            }

            if (map.IsPocketMap)
            {
                reason = "map is a pocket map - not a spatially-open map a gravship can land on";
                return false;
            }

            MapComponent_GravshipRaid component = MapComponent_GravshipRaid.GetFor(map);
            int maxConcurrent = GravshipRaidsSettings.maxConcurrentShipsPerMap;
            if (component != null && component.ActiveInstanceCount >= maxConcurrent)
            {
                reason = $"map already has {component.ActiveInstanceCount} active gravship instance(s), at or above settings.maxConcurrentShipsPerMap {maxConcurrent}";
                return false;
            }

            if (!MapSuitabilityUtility.IsMapSuitable(map, cacheKey, out string unsuitableReason))
            {
                reason = unsuitableReason;
                return false;
            }

            return true;
        }

        internal static bool CanUseGravshipRaidForFaction(Faction faction, Map map, float points, out string reason)
        {
            reason = null;

            if (faction == null)
            {
                reason = "faction is null";
                return false;
            }

            if (!faction.def.humanlikeFaction)
            {
                reason = $"faction '{faction.def.defName}' is not humanlike";
                return false;
            }

            if (!faction.HostileTo(Faction.OfPlayer))
            {
                reason = $"faction '{faction.def.defName}' is not hostile to the player";
                return false;
            }

            if ((int)faction.def.techLevel < (int)GravshipRaidsSettings.minEnemyFactionTechLevel)
            {
                reason = $"faction '{faction.def.defName}' techLevel {faction.def.techLevel} is below settings.minEnemyFactionTechLevel {GravshipRaidsSettings.minEnemyFactionTechLevel}";
                return false;
            }

            if (!GravshipRaidsSettings.AllowsFactionGlobally(faction.def))
            {
                reason = $"faction '{faction.def.defName}' is excluded by the global faction filter";
                return false;
            }

            if (points < GravshipRaidsSettings.minThreatPoints)
            {
                reason = $"points {points} is below settings.minThreatPoints {GravshipRaidsSettings.minThreatPoints}";
                return false;
            }

            if (faction.def.pawnGroupMakers == null
                || !faction.def.pawnGroupMakers.Any(p => p.kindDef == PawnGroupKindDefOf.Combat && points <= p.maxTotalPoints)
                || points <= faction.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat))
            {
                reason = $"faction '{faction.def.defName}' has no viable Combat pawn group at {points} points";
                return false;
            }

            if (map != null && map.Tile.Valid && map.Tile.LayerDef != PlanetLayerDefOf.Surface)
            {
                reason = $"map '{map}' is not a Surface-layer tile (layer '{map.Tile.LayerDef}')";
                return false;
            }

            GravshipRaidTemplateDef selectedTemplate = PawnsArrivalModeWorker_GravshipLanding.DebugForcedRequest?.SelectedTemplate;
            if (selectedTemplate != null)
            {
                if (!GravshipRaidTemplateUtility.IsEligibleTemplate(selectedTemplate, faction.def, points, map))
                {
                    reason = $"debug-selected template '{selectedTemplate.defName}' is not eligible for faction '{faction.def.defName}' at {points} points";
                    return false;
                }
            }
            else if (!GravshipRaidTemplateUtility.HasEligibleTemplate(faction.def, points, map))
            {
                reason = $"no GravshipRaidTemplateDef is eligible for faction '{faction.def.defName}' at {points} points";
                return false;
            }

            if (map != null && !GravshipLandingSiteFinder.HasViableLandingArea(map, faction.def, points))
            {
                reason = $"GravshipLandingSiteFinder found no viable landing area on map '{map}'";
                return false;
            }

            return true;
        }

        // Full gate for callers (Winston Waves compatibility) that only have an IncidentParms in hand rather
        // than the incident worker's own faction-candidate loop. The dispatch-time recheck in
        // WinstonWavesCompatibility re-runs this against the same parms, since the map can change between when
        // a Winston wave is queued and when it actually arrives.
        internal static bool CanUseGravshipRaid(IncidentParms parms, out string reason)
        {
            reason = null;

            if (!(parms.target is Map map))
            {
                reason = "incident target is not a Map";
                return false;
            }

            if (!CanUseGravshipRaidOnMap(map, out reason, parms))
            {
                return false;
            }

            if (!CanUseGravshipRaidForFaction(parms.faction, map, parms.points, out reason))
            {
                return false;
            }

            if (!GravshipRaidsDefOf.GR_GravshipLanding.Worker.CanUseWith(parms))
            {
                reason = "PawnsArrivalModeWorker_GravshipLanding.CanUseWith rejected these parms";
                return false;
            }

            return true;
        }
    }
}
