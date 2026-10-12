using System.Linq;
using RimWorld;
using Verse;

namespace Gravship_Raids
{
    public class IncidentWorker_ShuttleRaid : IncidentWorker_RaidEnemy
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!CanRunAsShipRaid(parms, out string reason))
            {
                Logger.Message($"IncidentWorker_ShuttleRaid.CanFireNowSub: declining - {reason}.");
                return false;
            }

            return true;
        }

        internal bool CanRunAsShipRaid(IncidentParms parms, out string reason)
        {
            reason = null;

            if (!ModsConfig.RoyaltyActive)
            {
                reason = "Royalty DLC is not active";
                return false;
            }

            if (!GravshipRaidsSettings.enableShuttleRaids)
            {
                reason = "shuttle raids are disabled in settings";
                return false;
            }

            if (GravshipRaidsSettings.shuttleEnableMinPlayerTechLevel && (int)Faction.OfPlayer.def.techLevel < (int)GravshipRaidsSettings.shuttleMinPlayerTechLevel)
            {
                reason = $"player faction techLevel {Faction.OfPlayer.def.techLevel} is below settings.shuttleMinPlayerTechLevel {GravshipRaidsSettings.shuttleMinPlayerTechLevel}";
                return false;
            }

            if (!(parms.target is Map map))
            {
                reason = "incident target is not a Map";
                return false;
            }

            MapComponent_ShuttleRaid component = MapComponent_ShuttleRaid.GetFor(map);
            int maxConcurrent = GravshipRaidsSettings.shuttleMaxConcurrentPerMap;
            if (component != null && component.ActiveInstanceCount >= maxConcurrent)
            {
                reason = $"map already has {component.ActiveInstanceCount} active shuttle instance(s), at or above settings.shuttleMaxConcurrentPerMap {maxConcurrent}";
                return false;
            }

            if (map.IsPocketMap)
            {
                reason = $"map '{map}' is a pocket map";
                return false;
            }

            if (map.Tile.Valid && map.Tile.LayerDef != PlanetLayerDefOf.Surface)
            {
                reason = $"map '{map}' is not a Surface-layer tile";
                return false;
            }

            int minColonists = GravshipRaidsSettings.shuttleMinColonistCount;
            if (map.mapPawns.FreeColonistsSpawnedCount < minColonists)
            {
                reason = $"map '{map}' has fewer than {minColonists} free spawned colonist(s)";
                return false;
            }

            if (parms.faction != null)
            {
                return CanUseShuttleRaidForFaction(parms.faction, map, parms.points, out reason);
            }

            IncidentParms probe = parms.ShallowCopy();
            if (!Find.FactionManager.AllFactions.Any(f => FactionCanBeGroupSource(f, probe)))
            {
                reason = $"no hostile faction can send a shuttle raid at {parms.points} points";
                return false;
            }

            return true;
        }

        internal static bool CanUseShuttleRaidForFaction(Faction f, Map map, float points, out string reason)
        {
            reason = null;

            if (f == null)
            {
                reason = "faction is null";
                return false;
            }

            if (!f.def.humanlikeFaction)
            {
                reason = $"faction '{f.def.defName}' is not humanlike";
                return false;
            }

            if (!f.HostileTo(Faction.OfPlayer))
            {
                reason = $"faction '{f.def.defName}' is not hostile to the player";
                return false;
            }

            if ((int)f.def.techLevel < (int)GravshipRaidsSettings.shuttleMinEnemyFactionTechLevel)
            {
                reason = $"techLevel {f.def.techLevel} is below settings.shuttleMinEnemyFactionTechLevel {GravshipRaidsSettings.shuttleMinEnemyFactionTechLevel}";
                return false;
            }

            if (!GravshipRaidsSettings.enableShuttleRaids)
            {
                reason = "shuttle raids are disabled in settings";
                return false;
            }

            if (points < GravshipRaidsSettings.shuttleMinThreatPoints)
            {
                reason = $"points {points} is below settings.shuttleMinThreatPoints {GravshipRaidsSettings.shuttleMinThreatPoints}";
                return false;
            }

            if (map != null && map.Tile.Valid && map.Tile.LayerDef != PlanetLayerDefOf.Surface)
            {
                reason = $"map '{map}' is not a Surface-layer tile";
                return false;
            }

            if (!GravshipRaidEligibility.HasViableCombatPawnGroup(f.def, points))
            {
                reason = $"no viable Combat pawn group at {points} points";
                return false;
            }

            ShuttleRaidTemplateDef selectedTemplate = PawnsArrivalModeWorker_ShuttleLanding.DebugForcedRequest?.SelectedTemplate;
            if (selectedTemplate != null)
            {
                if (!ShuttleRaidTemplateUtility.IsEligibleTemplate(selectedTemplate, f.def, points, map))
                {
                    reason = $"debug-selected template '{selectedTemplate.defName}' is not eligible at {points} points";
                    return false;
                }
            }
            else if (!ShuttleRaidTemplateUtility.HasEligibleTemplate(f.def, points, map))
            {
                reason = $"no ShuttleRaidTemplateDef is eligible at {points} points";
                return false;
            }

            if (map != null && !ShuttleRaidTemplateUtility.HasViableLandingArea(map, f.def, points))
            {
                reason = $"no viable shuttle landing site found on map '{map}'";
                return false;
            }

            return true;
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

            if (!CanUseShuttleRaidForFaction(f, parms.target as Map, parms.points, out string reason))
            {
                Logger.Message($"IncidentWorker_ShuttleRaid.FactionCanBeGroupSource: probe found faction '{f.def.defName}' ineligible - {reason}.");
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
