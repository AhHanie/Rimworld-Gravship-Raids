using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace Gravship_Raids
{
    public static class GravshipRaidDebugApi
    {
        private static Rot4 rotation = Rot4.North;

        private static Rot4 shuttleRotation = Rot4.North;

        public static void RotateTemplateSpawn()
        {
            rotation.Rotate(RotationDirection.Clockwise);
            Messages.Message("Gravship template spawn rotation: " + rotation.ToStringHuman(), MessageTypeDefOf.NeutralEvent, historical: false);
        }

        public static List<GravshipRaidTemplateDef> GetTemplates()
        {
            return DefDatabase<GravshipRaidTemplateDef>.AllDefsListForReading
                .OrderBy((GravshipRaidTemplateDef t) => t.defName)
                .ToList();
        }

        public static void SpawnTemplateAt(GravshipRaidTemplateDef template, Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map))
            {
                return;
            }
            if (template.prefab == null)
            {
                Log.Error($"[Gravship Raids] Cannot spawn template '{template.defName}': prefab is not assigned.");
                return;
            }

            IEnumerable<string> errors = template.ConfigErrors().ToList();
            if (errors.Any())
            {
                foreach (string error in errors)
                {
                    Log.Error($"[Gravship Raids] Template '{template.defName}' is invalid: {error}");
                }
            }

            CellRect bounds = GravshipRaidTemplateUtility.GetRotatedBounds(template, cell, rotation);
            bool canSpawn = GravshipRaidTemplateUtility.CanSpawnPrefab(template, map, cell, rotation);
            Log.Message($"[Gravship Raids] Spawning template '{template.defName}' at {cell}, rotation {rotation}. Bounds: {bounds}. CanSpawnPrefab: {canSpawn}.");

            using (HAROutfitStandCompatibility.BeginRaidPrefabSpawn())
            {
                PrefabUtility.SpawnPrefab(template.prefab, map, cell, rotation);
            }

            GravshipRaidTemplateUtility.ApplyPrefabInteriorRoofs(template.prefab, map, cell, rotation);

            IntVec3 coreCell = GravshipRaidTemplateUtility.GetCoreCell(template, cell, rotation);
            Log.Message($"[Gravship Raids] Core cell: {coreCell}.");

            List<IntVec3> openCells = GravshipRaidTemplateUtility.GetOpenInteriorCells(template, map, cell, rotation).ToList();
            Log.Message($"[Gravship Raids] Open interior spawn cells ({openCells.Count}): {string.Join(", ", openCells)}.");
        }

        public static void TestLandingSearch(Map map)
        {
            if (map == null)
            {
                return;
            }
            float points = StorytellerUtility.DefaultThreatPointsNow(map);
            bool found = GravshipLandingSiteFinder.TryFindLandingSite(map, null, points, Rand.Int, out GravshipRaidTemplateDef template, out IntVec3 root, out Rot4 rot, out GravshipLandingSiteFinder.LandingSearchDiagnostics diagnostics);
            string layerName = map.Tile.Valid ? map.Tile.LayerDef.defName : "unknown";
            string message = found
                ? $"[Gravship Raids] Landing search @ {points:F0} points (layer '{layerName}'): found '{template.defName}' at {root}, rotation {rot}."
                : $"[Gravship Raids] Landing search @ {points:F0} points (layer '{layerName}'): no viable site found ({diagnostics.Summarize()}).";
            Log.Message(message);
            Messages.Message(message, MessageTypeDefOf.NeutralEvent, historical: false);
        }

        private const int OrbitDeckVisualizationDurationTicks = 900;

        public static void VisualizeOrbitalLandingEligibility(Map map)
        {
            if (map == null)
            {
                return;
            }

            if (!map.Tile.Valid || map.Tile.LayerDef != PlanetLayerDefOf.Orbit)
            {
                string notOrbitMessage = "[Gravship Raids] Visualize orbital landing eligibility: this map's layer is not Orbit - nothing to visualize.";
                Log.Message(notOrbitMessage);
                Messages.Message(notOrbitMessage, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            HashSet<IntVec3> eligibleCells = GravshipLandingSiteFinder.GetOrbitDeckEligibleCells(map);
            foreach (IntVec3 cell in eligibleCells)
            {
                map.debugDrawer.FlashCell(cell, 0.33f, null, OrbitDeckVisualizationDurationTicks);
            }

            float points = StorytellerUtility.DefaultThreatPointsNow(map);
            GravshipLandingSiteFinder.HasViableLandingArea(map, null, points, out GravshipLandingSiteFinder.LandingSearchDiagnostics diagnostics);
            string message = $"[Gravship Raids] Visualize orbital landing eligibility @ {points:F0} points: {diagnostics.Summarize()}. Flashed {eligibleCells.Count} eligible deck cell(s) in green for {OrbitDeckVisualizationDurationTicks} ticks.";
            Log.Message(message);
            Messages.Message(message, MessageTypeDefOf.NeutralEvent, historical: false);
        }

        public static void ForceGravshipRaidAt(Map map, IntVec3 cell)
        {
            ForceGravshipRaidAtInternal(null, map, cell);
        }

        public static void ForceGravshipRaidAt(GravshipRaidTemplateDef selectedTemplate, Map map, IntVec3 cell)
        {
            if (selectedTemplate == null)
            {
                Log.Error("[Gravship Raids] Force gravship raid with template: no template was supplied.");
                return;
            }
            if (!GravshipRaidTemplateUtility.IsValidTemplate(selectedTemplate))
            {
                string message = $"[Gravship Raids] Force gravship raid with template: '{selectedTemplate.defName}' is invalid (missing prefab or has config errors) - see log for details.";
                Log.Error(message);
                foreach (string error in selectedTemplate.ConfigErrors())
                {
                    Log.Error($"[Gravship Raids] Template '{selectedTemplate.defName}' config error: {error}");
                }
                Messages.Message(message, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            ForceGravshipRaidAtInternal(selectedTemplate, map, cell);
        }

        private static void ForceGravshipRaidAtInternal(GravshipRaidTemplateDef selectedTemplate, Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map))
            {
                return;
            }

            bool previousDebugLogging = GravshipRaidsSettings.debugLogging;
            GravshipRaidsSettings.debugLogging = true;
            PawnsArrivalModeWorker_GravshipLanding.DebugForcedRequest =
                new PawnsArrivalModeWorker_GravshipLanding.ForcedLandingRequest(cell, rotation, selectedTemplate);
            try
            {
                IncidentParms parms = StorytellerUtility.DefaultParmsNow(GravshipRaidsDefOf.GR_GravshipRaid.category, map);
                parms.forced = true;

                if (parms.points < GravshipRaidsSettings.minThreatPoints)
                {
                    Log.Message($"[Gravship Raids] Forced gravship raid: raising parms.points from {parms.points} to settings.minThreatPoints {GravshipRaidsSettings.minThreatPoints} so faction selection isn't blocked by colony wealth during debug testing.");
                    parms.points = GravshipRaidsSettings.minThreatPoints;
                }

                bool success = GravshipRaidsDefOf.GR_GravshipRaid.Worker.TryExecute(parms);
                string templateDesc = (selectedTemplate != null) ? $" with template '{selectedTemplate.defName}'" : string.Empty;
                string message = $"[Gravship Raids] Forced gravship raid{templateDesc} at {cell} (rot {rotation}) " + (success ? "succeeded." : "failed - see log for the declining faction/landing-search reason.");
                Log.Message(message);
                Messages.Message(message, success ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.RejectInput, historical: false);
            }
            finally
            {
                PawnsArrivalModeWorker_GravshipLanding.DebugForcedRequest = null;
                GravshipRaidsSettings.debugLogging = previousDebugLogging;
            }
        }

        public static void ForceEnemyGravshipDeparture(Map map)
        {
            if (map == null)
            {
                return;
            }

            MapComponent_GravshipRaid component = MapComponent_GravshipRaid.GetFor(map);
            EnemyGravshipInstance instance = component?.Instances.FirstOrDefault(
                (EnemyGravshipInstance i) => i.state != GravshipRaidState.Departed && i.state != GravshipRaidState.Destroyed);

            if (instance == null)
            {
                Report("no active enemy gravship instance exists on this map.");
                return;
            }

            if (instance.core == null || !instance.core.Spawned)
            {
                Report($"{instance} has no valid spawned core.");
                return;
            }

            if (instance.flightDisabled)
            {
                Report($"{instance} is flight-disabled and cannot depart.");
                return;
            }

            if (instance.state != GravshipRaidState.Boarding)
            {
                Report($"{instance} is not in a departure-capable state (state={instance.state}); it must be Boarding.");
                return;
            }

            bool previousDebugLogging = GravshipRaidsSettings.debugLogging;
            GravshipRaidsSettings.debugLogging = true;
            try
            {
                bool success = EnemyGravshipRaidUtility.BeginDeparture(instance, map);
                string message = success
                    ? $"[Gravship Raids] Force enemy gravship departure: {instance} departure sequence started."
                    : $"[Gravship Raids] Force enemy gravship departure: BeginDeparture failed for {instance}.";
                Log.Message(message);
                Messages.Message(message, success ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.RejectInput, historical: false);
            }
            finally
            {
                GravshipRaidsSettings.debugLogging = previousDebugLogging;
            }
        }

        public static void RotateShuttleTemplateSpawn()
        {
            shuttleRotation.Rotate(RotationDirection.Clockwise);
            Messages.Message("Shuttle template spawn rotation: " + shuttleRotation.ToStringHuman(), MessageTypeDefOf.NeutralEvent, historical: false);
        }

        public static List<ShuttleRaidTemplateDef> GetShuttleTemplates()
        {
            return DefDatabase<ShuttleRaidTemplateDef>.AllDefsListForReading
                .OrderBy((ShuttleRaidTemplateDef t) => t.defName)
                .ToList();
        }

        public static void SpawnShuttleTemplateAt(ShuttleRaidTemplateDef template, Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map))
            {
                return;
            }
            if (template.shuttle == null)
            {
                Log.Error($"[Gravship Raids] Cannot spawn shuttle template '{template.defName}': shuttle is not assigned.");
                return;
            }

            IEnumerable<string> errors = template.ConfigErrors().ToList();
            if (errors.Any())
            {
                foreach (string error in errors)
                {
                    Log.Error($"[Gravship Raids] Shuttle template '{template.defName}' is invalid: {error}");
                }
            }

            AcceptanceReport report = RoyalTitlePermitWorker_CallShuttle.ShuttleCanLandHere(cell, map, template.shuttle, shuttleRotation);
            Log.Message($"[Gravship Raids] Shuttle template '{template.defName}' at {cell}, rotation {shuttleRotation}. ShuttleCanLandHere: {report.Accepted}" + (report.Accepted ? string.Empty : $" ({report.Reason})") + ".");

            if (!report.Accepted)
            {
                Messages.Message($"[Gravship Raids] Cannot land here: {report.Reason}", MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            Thing shuttleThing = ThingMaker.MakeThing(template.shuttle);
            shuttleThing.Rotation = shuttleRotation;
            GenSpawn.Spawn(shuttleThing, cell, map, shuttleRotation);
        }

        public static void ForceShuttleRaidAt(Map map, IntVec3 cell)
        {
            ForceShuttleRaidAtInternal(null, map, cell);
        }

        public static void ForceShuttleRaidAt(ShuttleRaidTemplateDef selectedTemplate, Map map, IntVec3 cell)
        {
            if (selectedTemplate == null)
            {
                Log.Error("[Gravship Raids] Force shuttle raid with template: no template was supplied.");
                return;
            }
            if (!ShuttleRaidTemplateUtility.IsValidTemplate(selectedTemplate))
            {
                string message = $"[Gravship Raids] Force shuttle raid with template: '{selectedTemplate.defName}' is invalid - see log for details.";
                Log.Error(message);
                foreach (string error in selectedTemplate.ConfigErrors())
                {
                    Log.Error($"[Gravship Raids] Shuttle template '{selectedTemplate.defName}' config error: {error}");
                }
                Messages.Message(message, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            ForceShuttleRaidAtInternal(selectedTemplate, map, cell);
        }

        private static void ForceShuttleRaidAtInternal(ShuttleRaidTemplateDef selectedTemplate, Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map))
            {
                return;
            }

            if (!ModsConfig.RoyaltyActive || GravshipRaidsDefOf.GR_ShuttleRaid == null)
            {
                string royaltyMessage = "[Gravship Raids] Force shuttle raid: the Royalty DLC is not active; shuttle raids are unavailable.";
                Log.Warning(royaltyMessage);
                Messages.Message(royaltyMessage, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            if (selectedTemplate != null)
            {
                AcceptanceReport report = ShuttleRaidTemplateUtility.CanShuttleLandAtReport(cell, map, selectedTemplate.shuttle, shuttleRotation);
                if (!report.Accepted)
                {
                    string rejectMessage = $"[Gravship Raids] Force shuttle raid with template '{selectedTemplate.defName}': cannot land at {cell} (rot {shuttleRotation}) - {report.Reason}.";
                    Log.Warning(rejectMessage);
                    Messages.Message(rejectMessage, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }
            }

            bool previousDebugLogging = GravshipRaidsSettings.debugLogging;
            GravshipRaidsSettings.debugLogging = true;
            PawnsArrivalModeWorker_ShuttleLanding.DebugForcedRequest =
                new PawnsArrivalModeWorker_ShuttleLanding.ForcedLandingRequest(cell, shuttleRotation, selectedTemplate);
            try
            {
                IncidentParms parms = StorytellerUtility.DefaultParmsNow(GravshipRaidsDefOf.GR_ShuttleRaid.category, map);
                parms.forced = true;

                if (parms.points < GravshipRaidsSettings.shuttleMinThreatPoints)
                {
                    Log.Message($"[Gravship Raids] Forced shuttle raid: raising parms.points from {parms.points} to settings.shuttleMinThreatPoints {GravshipRaidsSettings.shuttleMinThreatPoints} so faction selection isn't blocked by colony wealth during debug testing.");
                    parms.points = GravshipRaidsSettings.shuttleMinThreatPoints;
                }

                bool success = GravshipRaidsDefOf.GR_ShuttleRaid.Worker.TryExecute(parms);
                string templateDesc = (selectedTemplate != null) ? $" with template '{selectedTemplate.defName}'" : string.Empty;
                string message = $"[Gravship Raids] Forced shuttle raid{templateDesc} at {cell} (rot {shuttleRotation}) " + (success ? "succeeded." : "failed - see log for the declining faction/landing-search reason.");
                Log.Message(message);
                Messages.Message(message, success ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.RejectInput, historical: false);
            }
            finally
            {
                PawnsArrivalModeWorker_ShuttleLanding.DebugForcedRequest = null;
                GravshipRaidsSettings.debugLogging = previousDebugLogging;
            }
        }

        public static void ForceEnemyShuttleDeparture(Map map)
        {
            if (map == null)
            {
                return;
            }

            MapComponent_ShuttleRaid component = MapComponent_ShuttleRaid.GetFor(map);
            EnemyShuttleRaidInstance instance = component?.Instances.FirstOrDefault(
                (EnemyShuttleRaidInstance i) => i.state != ShuttleRaidState.Departed && i.state != ShuttleRaidState.Lost);

            if (instance == null)
            {
                Report("no active enemy shuttle raid instance exists on this map.");
                return;
            }

            if (!instance.ShuttleAvailable)
            {
                Report($"{instance} has no valid spawned shuttle.");
                return;
            }

            if (instance.state != ShuttleRaidState.Boarding)
            {
                Report($"{instance} is not in a departure-capable state (state={instance.state}); it must be Boarding.");
                return;
            }

            bool previousDebugLogging = GravshipRaidsSettings.debugLogging;
            GravshipRaidsSettings.debugLogging = true;
            try
            {
                bool success = EnemyShuttleRaidUtility.TryDepart(instance);
                string message = success
                    ? $"[Gravship Raids] Force enemy shuttle departure: {instance} departure sequence started."
                    : $"[Gravship Raids] Force enemy shuttle departure: TryDepart failed for {instance}.";
                Log.Message(message);
                Messages.Message(message, success ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.RejectInput, historical: false);
            }
            finally
            {
                GravshipRaidsSettings.debugLogging = previousDebugLogging;
            }
        }

        public static void StartPrefabCaptureWithTerrain()
        {
            DebugToolsGeneral.GenericRectTool("Capture", delegate (CellRect rect)
            {
                try
                {
                    PrefabDef prefab = PrefabUtility.CreatePrefab(rect, copyAllThings: true, copyTerrain: false);
                    List<(PrefabTerrainData data, IntVec3 cell)> foundation = CaptureFoundationTerrain(Find.CurrentMap, rect);
                    HashSet<IntVec3> substructureCells = foundation.Select((t) => t.cell).ToHashSet();
                    List<(PrefabThingData data, IntVec3 cell)> things = prefab.GetThings().Where((t) => substructureCells.Contains(t.cell)).ToList();

                    string xml = DebugActionsGravshipRaidPrefabCapture.BuildPrefabXml(rect, things, foundation);
                    GUIUtility.systemCopyBuffer = xml;

                    string message = $"[Gravship Raids] Captured prefab {rect.Size.x}x{rect.Size.z} ({things.Count} thing cell(s), {foundation.Count} foundation terrain cell(s)). Copied to clipboard - rename NewPrefab before pasting into a defs file.";
                    Log.Message(message);
                    Messages.Message(message, MessageTypeDefOf.NeutralEvent, historical: false);
                }
                catch (Exception ex)
                {
                    Log.Error($"[Gravship Raids] Prefab capture failed for rect {rect}: {ex}");
                    Messages.Message("[Gravship Raids] Prefab capture failed - see log for details.", MessageTypeDefOf.RejectInput, historical: false);
                }
            }, closeOnComplete: true);
        }

        private static List<(PrefabTerrainData data, IntVec3 cell)> CaptureFoundationTerrain(Map map, CellRect rect)
        {
            // CreatePrefab's copyTerrain only reads the visible top terrain layer (cell.GetTerrain), so a floor
            // built over a foundation would be captured as that floor and the foundation beneath silently dropped.
            // FoundationAt reads the separate foundation layer directly, independent of whatever floor sits above it.
            List<(PrefabTerrainData data, IntVec3 cell)> result = new List<(PrefabTerrainData, IntVec3)>();
            TerrainGrid terrainGrid = map.terrainGrid;
            foreach (IntVec3 cell in rect.Cells)
            {
                TerrainDef foundationDef = terrainGrid.FoundationAt(cell);
                if (foundationDef == null)
                {
                    continue;
                }
                IntVec3 localCell = cell - rect.Min;
                PrefabTerrainData data = new PrefabTerrainData
                {
                    def = foundationDef,
                    color = terrainGrid.ColorAt(cell),
                    rects = new List<CellRect> { new CellRect(localCell.x, localCell.z, 1, 1) }
                };
                result.Add((data, localCell));
            }
            return result;
        }

        private static void Report(string reason)
        {
            string message = "[Gravship Raids] Force enemy gravship departure: cannot run - " + reason;
            Log.Message(message);
            Messages.Message(message, MessageTypeDefOf.RejectInput, historical: false);
        }
    }
}
