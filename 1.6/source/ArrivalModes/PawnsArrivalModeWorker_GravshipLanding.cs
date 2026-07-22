using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace Gravship_Raids
{
    public class PawnsArrivalModeWorker_GravshipLanding : PawnsArrivalModeWorker
    {
        private static readonly ConditionalWeakTable<IncidentParms, LandingPlan> LandingPlans = new ConditionalWeakTable<IncidentParms, LandingPlan>();

        internal static ForcedLandingRequest? DebugForcedRequest;

        private IncidentParms currentCanUseWithParms;

        internal readonly struct ForcedLandingRequest
        {
            public readonly IntVec3 Root;
            public readonly Rot4 Rotation;

            // Non-null for "Force gravship raid with template..."; null for the generic forced-root cheat,
            // which still picks the first eligible template for the clicked cell.
            public readonly GravshipRaidTemplateDef SelectedTemplate;

            public ForcedLandingRequest(IntVec3 root, Rot4 rotation, GravshipRaidTemplateDef selectedTemplate)
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

            if (!ModsConfig.OdysseyActive)
            {
                return false;
            }

            if (!GravshipRaidsSettings.enabled)
            {
                return false;
            }

            if (map.Tile.Valid && map.Tile.LayerDef != PlanetLayerDefOf.Surface)
            {
                return false;
            }

            MapComponent_GravshipRaid component = MapComponent_GravshipRaid.GetFor(map);
            int maxConcurrent = GravshipRaidsSettings.maxConcurrentShipsPerMap;
            if (component != null && component.ActiveInstanceCount >= maxConcurrent)
            {
                return false;
            }

            if (!MapSuitabilityUtility.IsMapSuitable(map, currentCanUseWithParms, out string unsuitableReason))
            {
                Logger.Message($"PawnsArrivalModeWorker_GravshipLanding.CanUseOnMap: declining on map '{map}' - {unsuitableReason}.");
                return false;
            }

            return true;
        }

        public override bool CanUseWith(IncidentParms parms)
        {
            currentCanUseWithParms = parms;
            if (!base.CanUseWith(parms))
            {
                return false;
            }

            if (!(parms.target is Map map))
            {
                return false;
            }

            if (parms.faction != null && (int)parms.faction.def.techLevel < (int)GravshipRaidsSettings.minEnemyFactionTechLevel)
            {
                return false;
            }

            if (DebugForcedRequest.HasValue && DebugForcedRequest.Value.SelectedTemplate != null)
            {
                ForcedLandingRequest request = DebugForcedRequest.Value;
                if (!GravshipRaidTemplateUtility.IsEligibleTemplate(request.SelectedTemplate, parms.faction?.def, parms.points, map))
                {
                    return false;
                }
                return GravshipRaidTemplateUtility.CanSpawnPrefab(request.SelectedTemplate, map, request.Root, request.Rotation, canWipeEdifices: false);
            }

            if (!GravshipRaidTemplateUtility.HasEligibleTemplate(parms.faction?.def, parms.points, map))
            {
                return false;
            }

            if (!GravshipLandingSiteFinder.HasViableLandingArea(map, parms.faction?.def, parms.points))
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

                if (request.SelectedTemplate != null)
                {
                    if (!GravshipRaidTemplateUtility.IsEligibleTemplate(request.SelectedTemplate, parms.faction?.def, parms.points, map) ||
                        !GravshipRaidTemplateUtility.CanSpawnPrefab(request.SelectedTemplate, map, request.Root, request.Rotation, canWipeEdifices: false))
                    {
                        Logger.Warning($"PawnsArrivalModeWorker_GravshipLanding.TryResolveRaidSpawnCenter: selected template '{request.SelectedTemplate.defName}' is not eligible/placeable for faction '{parms.faction?.def?.defName ?? "null"}' at {parms.points} points at the debug-forced cell {request.Root} (rot {request.Rotation}); declining this arrival mode so the incident fails cleanly before any pawns are generated.");
                        return false;
                    }

                    parms.spawnCenter = request.Root;
                    parms.spawnRotation = request.Rotation;
                    LandingPlans.Remove(parms);
                    LandingPlans.Add(parms, new LandingPlan(request.SelectedTemplate, request.Root, request.Rotation));
                    return true;
                }

                if (!TryFindTemplateForForcedCell(map, parms.faction?.def, parms.points, request.Root, request.Rotation, out GravshipRaidTemplateDef forcedTemplate))
                {
                    Logger.Warning($"PawnsArrivalModeWorker_GravshipLanding.TryResolveRaidSpawnCenter: no eligible template for faction '{parms.faction?.def?.defName ?? "null"}' at {parms.points} points can be spawned at the debug-forced cell {request.Root} (rot {request.Rotation}); declining this arrival mode so the incident fails cleanly before any pawns are generated.");
                    return false;
                }

                parms.spawnCenter = request.Root;
                parms.spawnRotation = request.Rotation;
                LandingPlans.Remove(parms);
                LandingPlans.Add(parms, new LandingPlan(forcedTemplate, request.Root, request.Rotation));
                return true;
            }

            int seed = Gen.HashCombineInt(GravshipRaidTemplateUtility.MakeSelectionSeed(parms.faction?.def, parms.points, map), Find.TickManager.TicksGame);

            if (!GravshipLandingSiteFinder.TryFindLandingSite(map, parms.faction?.def, parms.points, seed, out GravshipRaidTemplateDef template, out IntVec3 root, out Rot4 rotation))
            {
                Logger.Warning($"PawnsArrivalModeWorker_GravshipLanding.TryResolveRaidSpawnCenter: no viable landing site found on map '{map}' for faction '{parms.faction?.def?.defName ?? "null"}' at {parms.points} points; declining this arrival mode so the incident fails cleanly before any pawns are generated.");
                return false;
            }

            parms.spawnCenter = root;
            parms.spawnRotation = rotation;
            LandingPlans.Remove(parms);
            LandingPlans.Add(parms, new LandingPlan(template, root, rotation));
            return true;
        }

        private static bool TryFindTemplateForForcedCell(Map map, FactionDef factionDef, float points, IntVec3 root, Rot4 rotation, out GravshipRaidTemplateDef template)
        {
            foreach (GravshipRaidTemplateDef candidate in GravshipRaidTemplateUtility.GetEligibleTemplates(factionDef, points, map))
            {
                if (GravshipRaidTemplateUtility.CanSpawnPrefab(candidate, map, root, rotation, canWipeEdifices: false))
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
                // Should never happen on the normal incident call path (see TryResolveRaidSpawnCenter above),
                // but never lose already-generated pawns over a missing cache entry.
                Logger.Error("PawnsArrivalModeWorker_GravshipLanding.Arrive: no landing plan was stashed for this IncidentParms by TryResolveRaidSpawnCenter; falling back to a plain drop near spawnCenter so pawns are not lost.");
                FallbackArrive(pawns, map, parms.spawnCenter, parms.spawnRotation);
                return;
            }

            EnemyGravshipInstance instance = new EnemyGravshipInstance(plan.Template, plan.Root, plan.Rotation, parms.faction, core: null, spawnedThings: null);
            instance.state = GravshipRaidState.Landing;
            instance.crew.AddRange(pawns);
            MapComponent_GravshipRaid.GetFor(map).RegisterInstance(instance);

            ThingDef arrivalSkyfallerDef = ResolveArrivalSkyfaller(plan.Template);
            GravshipArrivalSkyfaller skyfaller = (GravshipArrivalSkyfaller)ThingMaker.MakeThing(arrivalSkyfallerDef);
            skyfaller.instance = instance;
            for (int i = 0; i < pawns.Count; i++)
            {
                skyfaller.innerContainer.TryAdd(pawns[i], canMergeWithExistingStacks: false);
            }

            // Not the ThingDef-based GenSpawn.Spawn overload (which would run CanSpawnAt against plan.Root/
            // plan.Rotation) - this Thing is flying overhead, not actually touching the ground yet, exactly
            // like vanilla's own shuttle/drop-pod incoming skyfallers, which are spawned the same direct way.
            GenSpawn.Spawn(skyfaller, plan.Root, map, plan.Rotation);

            Logger.Message($"PawnsArrivalModeWorker_GravshipLanding.Arrive: registered {instance} and spawned its arrival skyfaller for template '{plan.Template.defName}' at {plan.Root} (rot {plan.Rotation}) for faction '{parms.faction?.Name ?? "null"}'; {pawns.Count} pawn(s) aboard, landing in {skyfaller.ticksToImpact} ticks.");
        }

        internal static ThingDef ResolveArrivalSkyfaller(GravshipRaidTemplateDef template)
        {
            if (template.arrivalSkyfaller != null)
            {
                return template.arrivalSkyfaller;
            }
            int footprintArea = (template.prefab != null) ? template.prefab.size.x * template.prefab.size.z : 0;
            if (footprintArea <= 60)
            {
                return GravshipRaidsDefOf.GR_GravshipArrivalSkyfaller_Small;
            }
            if (footprintArea <= 120)
            {
                return GravshipRaidsDefOf.GR_GravshipArrivalSkyfaller_Medium;
            }
            return GravshipRaidsDefOf.GR_GravshipArrivalSkyfaller_Large;
        }

        internal static void FinishLanding(EnemyGravshipInstance instance, Map map, List<Pawn> pawns)
        {
            GravshipRaidTemplateDef template = instance.template;
            IntVec3 root = instance.root;
            Rot4 rotation = instance.rotation;
            Faction faction = instance.faction;

            if (map == null || map.Disposed)
            {
                Logger.Error("PawnsArrivalModeWorker_GravshipLanding.FinishLanding: map is gone before the arrival skyfaller landed; pawns cannot be placed and are lost.");
                instance.state = GravshipRaidState.Destroyed;
                return;
            }

            if (template == null || !GravshipRaidTemplateUtility.IsValidTemplate(template) || !GravshipRaidTemplateUtility.CanSpawnPrefab(template, map, root, rotation))
            {
                Logger.Warning($"PawnsArrivalModeWorker_GravshipLanding.FinishLanding: template '{template?.defName ?? "null"}' can no longer be spawned at {root} (site changed during the several-second flight-in?); falling back to a plain pawn drop with no ship.");
                instance.state = GravshipRaidState.Destroyed;
                FallbackArrive(pawns, map, root, rotation);
                return;
            }

            List<GravshipRaidTemplateUtility.TerrainCellSnapshot> terrainSnapshot = GravshipRaidTemplateUtility.SnapshotTerrain(template, map, root, rotation);

            List<Thing> spawned = new List<Thing>();
            using (HAROutfitStandCompatibility.BeginRaidPrefabSpawn())
            {
                PrefabUtility.SpawnPrefab(template.prefab, map, root, rotation, faction, spawned);
            }

            RandomizeFuelTankLevels(spawned);

            Thing core = spawned.Find((Thing t) => t.TryGetComp<CompEnemyGravshipCore>() != null);
            if (core == null)
            {
                Logger.Error($"PawnsArrivalModeWorker_GravshipLanding.FinishLanding: template '{template.defName}' produced no CompEnemyGravshipCore-bearing thing among {spawned.Count} spawned things; the raid will proceed without a registered ship instance.");
                instance.state = GravshipRaidState.Destroyed;
            }
            else
            {
                instance.core = core;
                instance.spawnedThings = spawned;
                instance.terrainSnapshot = terrainSnapshot;
                instance.state = GravshipRaidState.Landed;
            }

            PlaySettlingEffects(map, template, root, rotation);

            if (GravshipRaidsSettings.enableRaidshipEffects)
            {
                List<Thing> thrusters = spawned.Where((Thing t) => t.TryGetComp<CompGravshipThruster>() != null).ToList();
                if (thrusters.Count > 0)
                {
                    IntVec3 launchDirection = EnemyGravshipEffects.MajorityLaunchDirection(thrusters.Select((Thing t) => t.Rotation));
                    Vector3 groundCenter = GravshipRaidTemplateUtility.GetCoreCell(template, root, rotation).ToVector3Shifted();
                    MapComponent_GravshipRaid.GetFor(map)?.BeginSettleEffects(thrusters, groundCenter, rotation, launchDirection, 3f);
                }
            }

            DeployPawns(pawns, map, template, root, rotation);
            RefreshGravshipRaidDuties(instance, map);

            Logger.Message($"PawnsArrivalModeWorker_GravshipLanding.FinishLanding: landed '{template.defName}' at {root} (rot {rotation}) for faction '{faction?.Name ?? "null"}'; deployed {pawns.Count} pawn(s).");
        }

        private static void RefreshGravshipRaidDuties(EnemyGravshipInstance instance, Map map)
        {
            if (map == null || map.Disposed)
            {
                return;
            }
            List<Lord> lords = map.lordManager.lords;
            for (int i = 0; i < lords.Count; i++)
            {
                if (lords[i].LordJob is LordJob_GravshipRaid job && job.Instance == instance)
                {
                    lords[i].CurLordToil?.UpdateAllDuties();
                }
            }
        }

        private static void RandomizeFuelTankLevels(List<Thing> spawned)
        {
            foreach (Thing thing in spawned)
            {
                CompRefuelable fuelComp = thing.TryGetComp<CompRefuelable>();
                if (fuelComp == null)
                {
                    continue;
                }
                fuelComp.Refuel(Rand.Range(0.2f, 1f) * fuelComp.Props.fuelCapacity);
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

        private static void DeployPawns(List<Pawn> pawns, Map map, GravshipRaidTemplateDef template, IntVec3 root, Rot4 rotation)
        {
            List<IntVec3> interiorCells = GravshipRaidTemplateUtility.GetOpenInteriorCells(template, map, root, rotation).InRandomOrder().ToList();

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                IntVec3 spawnCell;
                if (interiorCells.Count > 0)
                {
                    // Multiple pawns can share one map cell in RimWorld, so cycling back through the list once
                    // every pawn has a distinct cell is fine.
                    spawnCell = interiorCells[i % interiorCells.Count];
                }
                else
                {
                    Logger.Warning($"PawnsArrivalModeWorker_GravshipLanding.DeployPawns: template '{template.defName}' produced zero open interior spawn cells at {root} (rot {rotation}); falling back to a wide closewalk search for pawn {i}. A valid template should never hit this path.");
                    spawnCell = CellFinder.RandomClosewalkCellNear(root, map, 6);
                }
                GenSpawn.Spawn(pawn, spawnCell, map, Rot4.Random);
            }
        }

        private static void PlaySettlingEffects(Map map, GravshipRaidTemplateDef template, IntVec3 root, Rot4 rotation)
        {
            IntVec3 coreCell = GravshipRaidTemplateUtility.GetCoreCell(template, root, rotation);

            CellRect footprint = GravshipRaidTemplateUtility.GetRotatedBounds(template, root, rotation);
            foreach (IntVec3 cell in footprint.Cells)
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }
                if (Rand.Chance(0.35f))
                {
                    FleckMaker.ThrowDustPuff(cell, map, Rand.Range(1f, 2f));
                }
            }
            FleckMaker.ThrowHeatGlow(coreCell, map, 3.5f);
        }

        private sealed class LandingPlan
        {
            public readonly GravshipRaidTemplateDef Template;
            public readonly IntVec3 Root;
            public readonly Rot4 Rotation;

            public LandingPlan(GravshipRaidTemplateDef template, IntVec3 root, Rot4 rotation)
            {
                Template = template;
                Root = root;
                Rotation = rotation;
            }
        }
    }
}
