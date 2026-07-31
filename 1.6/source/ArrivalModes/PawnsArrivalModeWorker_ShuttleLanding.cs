using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace Gravship_Raids
{
    public class PawnsArrivalModeWorker_ShuttleLanding : PawnsArrivalModeWorker
    {
        private static readonly ConditionalWeakTable<IncidentParms, LandingPlan> LandingPlans = new ConditionalWeakTable<IncidentParms, LandingPlan>();

        internal static ForcedLandingRequest? DebugForcedRequest;

        internal readonly struct ForcedLandingRequest
        {
            public readonly IntVec3 Root;
            public readonly Rot4 Rotation;
            public readonly ShuttleRaidTemplateDef SelectedTemplate;

            public ForcedLandingRequest(IntVec3 root, Rot4 rotation, ShuttleRaidTemplateDef selectedTemplate)
            {
                Root = root;
                Rotation = rotation;
                SelectedTemplate = selectedTemplate;
            }
        }

        public override bool CanUseOnMap(Map map)
        {
            if (!base.CanUseOnMap(map))
            {
                return false;
            }

            if (!ModsConfig.RoyaltyActive)
            {
                return false;
            }

            if (!GravshipRaidsSettings.enableShuttleRaids)
            {
                return false;
            }

            if (map.IsPocketMap)
            {
                return false;
            }

            if (map.Tile.Valid && map.Tile.LayerDef != PlanetLayerDefOf.Surface)
            {
                return false;
            }

            MapComponent_ShuttleRaid component = MapComponent_ShuttleRaid.GetFor(map);
            int maxConcurrent = GravshipRaidsSettings.shuttleMaxConcurrentPerMap;
            if (component != null && component.ActiveInstanceCount >= maxConcurrent)
            {
                return false;
            }

            return true;
        }

        public override bool CanUseWith(IncidentParms parms)
        {
            if (!base.CanUseWith(parms))
            {
                return false;
            }

            if (!(parms.target is Map map))
            {
                return false;
            }

            if (parms.faction != null && (int)parms.faction.def.techLevel < (int)GravshipRaidsSettings.shuttleMinEnemyFactionTechLevel)
            {
                return false;
            }

            if (DebugForcedRequest.HasValue && DebugForcedRequest.Value.SelectedTemplate != null)
            {
                ForcedLandingRequest request = DebugForcedRequest.Value;
                if (!ShuttleRaidTemplateUtility.IsEligibleTemplate(request.SelectedTemplate, parms.faction?.def, parms.points, map))
                {
                    return false;
                }
                return RoyalTitlePermitWorker_CallShuttle.ShuttleCanLandHere(request.Root, map, request.SelectedTemplate.shuttle, request.Rotation).Accepted;
            }

            if (!ShuttleRaidTemplateUtility.HasEligibleTemplate(parms.faction?.def, parms.points, map))
            {
                return false;
            }

            if (!ShuttleRaidTemplateUtility.HasViableLandingArea(map, parms.faction?.def, parms.points))
            {
                return false;
            }

            return true;
        }

        public override bool TryResolveRaidSpawnCenter(IncidentParms parms)
        {
            Map map = (Map)parms.target;

            if (DebugForcedRequest.HasValue)
            {
                ForcedLandingRequest request = DebugForcedRequest.Value;
                DebugForcedRequest = null;

                ShuttleRaidTemplateDef template = request.SelectedTemplate;
                if (template == null)
                {
                    if (!TryFindTemplateForForcedCell(map, parms.faction?.def, parms.points, request.Root, request.Rotation, out template))
                    {
                        Logger.Warning($"PawnsArrivalModeWorker_ShuttleLanding.TryResolveRaidSpawnCenter: no eligible template for faction '{parms.faction?.def?.defName ?? "null"}' at {parms.points} points can land at the debug-forced cell {request.Root} (rot {request.Rotation}); declining this arrival mode so the incident fails cleanly before any pawns are generated.");
                        return false;
                    }
                }
                else if (!ShuttleRaidTemplateUtility.IsEligibleTemplate(template, parms.faction?.def, parms.points, map) ||
                         !RoyalTitlePermitWorker_CallShuttle.ShuttleCanLandHere(request.Root, map, template.shuttle, request.Rotation).Accepted)
                {
                    Logger.Warning($"PawnsArrivalModeWorker_ShuttleLanding.TryResolveRaidSpawnCenter: selected template '{template.defName}' is not eligible/landable for faction '{parms.faction?.def?.defName ?? "null"}' at {parms.points} points at the debug-forced cell {request.Root} (rot {request.Rotation}); declining this arrival mode so the incident fails cleanly before any pawns are generated.");
                    return false;
                }

                parms.spawnCenter = request.Root;
                parms.spawnRotation = request.Rotation;
                LandingPlans.Remove(parms);
                LandingPlans.Add(parms, new LandingPlan(template, request.Root, request.Rotation));
                return true;
            }

            int seed = Gen.HashCombineInt(ShuttleRaidTemplateUtility.MakeSelectionSeed(parms.faction?.def, parms.points, map), Find.TickManager.TicksGame);

            ShuttleRaidTemplateDef selected = ShuttleRaidTemplateUtility.SelectTemplate(parms.faction?.def, parms.points, map, seed);
            if (selected == null)
            {
                Logger.Warning($"PawnsArrivalModeWorker_ShuttleLanding.TryResolveRaidSpawnCenter: no eligible ShuttleRaidTemplateDef for faction '{parms.faction?.def?.defName ?? "null"}' at {parms.points} points on map '{map}'; declining this arrival mode so the incident fails cleanly before any pawns are generated.");
                return false;
            }

            if (!ShuttleRaidTemplateUtility.TryFindLandingSite(map, selected, seed, out IntVec3 root, out Rot4 rotation))
            {
                Logger.Warning($"PawnsArrivalModeWorker_ShuttleLanding.TryResolveRaidSpawnCenter: no viable landing site found on map '{map}' for template '{selected.defName}'; declining this arrival mode so the incident fails cleanly before any pawns are generated.");
                return false;
            }

            parms.spawnCenter = root;
            parms.spawnRotation = rotation;
            LandingPlans.Remove(parms);
            LandingPlans.Add(parms, new LandingPlan(selected, root, rotation));
            return true;
        }

        private static bool TryFindTemplateForForcedCell(Map map, FactionDef factionDef, float points, IntVec3 root, Rot4 rotation, out ShuttleRaidTemplateDef template)
        {
            foreach (ShuttleRaidTemplateDef candidate in ShuttleRaidTemplateUtility.GetEligibleTemplates(factionDef, points, map))
            {
                if (RoyalTitlePermitWorker_CallShuttle.ShuttleCanLandHere(root, map, candidate.shuttle, rotation).Accepted)
                {
                    template = candidate;
                    return true;
                }
            }
            template = null;
            return false;
        }

        public override void Arrive(List<Pawn> pawns, IncidentParms parms)
        {
            Map map = (Map)parms.target;

            if (!TryTakeLandingPlan(parms, out LandingPlan plan))
            {
                Logger.Error("PawnsArrivalModeWorker_ShuttleLanding.Arrive: no landing plan was stashed for this IncidentParms by TryResolveRaidSpawnCenter; falling back to a plain drop near spawnCenter so pawns are not lost.");
                FallbackArrive(pawns, map, parms.spawnCenter, parms.spawnRotation);
                return;
            }

            EnemyShuttleRaidInstance instance = null;
            try
            {
                Thing shuttleThing = ThingMaker.MakeThing(plan.Template.shuttle);
                shuttleThing.SetFaction(parms.faction);
                shuttleThing.Rotation = plan.Rotation;

                instance = new EnemyShuttleRaidInstance(plan.Template, parms.faction, shuttleThing);
                instance.state = ShuttleRaidState.Landing;
                instance.crew.AddRange(pawns);
                MapComponent_ShuttleRaid.GetFor(map).RegisterInstance(instance);

                TransportShip transportShip = TransportShipMaker.MakeTransportShip(plan.Template.shipDef, pawns, shuttleThing);
                transportShip.ArriveAt(plan.Root, map.Parent);

                ShipJob_WaitForever waitJob = (ShipJob_WaitForever)ShipJobMaker.MakeShipJob(ShipJobDefOf.WaitForever);
                waitJob.showGizmos = false;
                transportShip.AddJob(ShipJobDefOf.Unload);
                transportShip.AddJob(waitJob);

                Logger.Message($"PawnsArrivalModeWorker_ShuttleLanding.Arrive: registered {instance} and queued its arrival for template '{plan.Template.defName}' at {plan.Root} (rot {plan.Rotation}) for faction '{parms.faction?.Name ?? "null"}'; {pawns.Count} pawn(s) aboard.");
            }
            catch (Exception ex)
            {
                Logger.Error($"PawnsArrivalModeWorker_ShuttleLanding.Arrive: unexpected setup failure before landing for template '{plan.Template?.defName ?? "null"}': {ex}. Falling back to a plain pawn drop so the raid is not lost.");
                if (instance != null)
                {
                    MapComponent_ShuttleRaid.GetFor(map)?.DeregisterInstance(instance);
                }
                FallbackArrive(pawns, map, plan.Root, plan.Rotation);
            }
        }

        private static bool TryTakeLandingPlan(IncidentParms parms, out LandingPlan plan)
        {
            bool found = LandingPlans.TryGetValue(parms, out plan);
            if (found)
            {
                LandingPlans.Remove(parms);
            }
            return found;
        }

        private static void FallbackArrive(List<Pawn> pawns, Map map, IntVec3 spawnCenter, Rot4 spawnRotation)
        {
            for (int i = 0; i < pawns.Count; i++)
            {
                IntVec3 loc = CellFinder.RandomClosewalkCellNear(spawnCenter, map, 8);
                GenSpawn.Spawn(pawns[i], loc, map, spawnRotation);
            }
        }

        private sealed class LandingPlan
        {
            public readonly ShuttleRaidTemplateDef Template;
            public readonly IntVec3 Root;
            public readonly Rot4 Rotation;

            public LandingPlan(ShuttleRaidTemplateDef template, IntVec3 root, Rot4 rotation)
            {
                Template = template;
                Root = root;
                Rotation = rotation;
            }
        }
    }
}
