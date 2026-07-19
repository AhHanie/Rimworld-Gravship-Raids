using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace Gravship_Raids
{
    [StaticConstructorOnStartup]
    public static class DebugActionsGravshipRaidTemplates
    {
        private static Rot4 rotation = Rot4.North;

        [DebugAction("Gravship Raids", "Rotate gravship template spawn", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RotateTemplateSpawn()
        {
            rotation.Rotate(RotationDirection.Clockwise);
            Messages.Message("Gravship template spawn rotation: " + rotation.ToStringHuman(), MessageTypeDefOf.NeutralEvent, historical: false);
        }

        [DebugAction("Gravship Raids", "Spawn gravship template...", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static List<DebugActionNode> SpawnTemplate()
        {
            List<DebugActionNode> list = new List<DebugActionNode>();
            foreach (GravshipRaidTemplateDef template in DefDatabase<GravshipRaidTemplateDef>.AllDefsListForReading.OrderBy((GravshipRaidTemplateDef t) => t.defName))
            {
                GravshipRaidTemplateDef localTemplate = template;
                bool valid = GravshipRaidTemplateUtility.IsValidTemplate(localTemplate);
                string label = localTemplate.defName + (valid ? string.Empty : " [INVALID]");
                list.Add(new DebugActionNode(label, DebugActionType.ToolMap)
                {
                    action = delegate
                    {
                        SpawnAtMouseCell(localTemplate);
                    }
                });
            }
            return list;
        }

        private static void SpawnAtMouseCell(GravshipRaidTemplateDef template)
        {
            Map map = Find.CurrentMap;
            IntVec3 cell = UI.MouseCell();
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

            PrefabUtility.SpawnPrefab(template.prefab, map, cell, rotation);

            IntVec3 coreCell = GravshipRaidTemplateUtility.GetCoreCell(template, cell, rotation);
            Log.Message($"[Gravship Raids] Core cell: {coreCell}.");

            List<IntVec3> openCells = GravshipRaidTemplateUtility.GetOpenInteriorCells(template, map, cell, rotation).ToList();
            Log.Message($"[Gravship Raids] Open interior spawn cells ({openCells.Count}): {string.Join(", ", openCells)}.");
        }

        [DebugAction("Gravship Raids", "Test gravship landing search", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TestLandingSearch()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }
            float points = StorytellerUtility.DefaultThreatPointsNow(map);
            bool found = GravshipLandingSiteFinder.TryFindLandingSite(map, null, points, Rand.Int, out GravshipRaidTemplateDef template, out IntVec3 root, out Rot4 rot);
            string message = found
                ? $"[Gravship Raids] Landing search @ {points:F0} points: found '{template.defName}' at {root}, rotation {rot}."
                : $"[Gravship Raids] Landing search @ {points:F0} points: no viable site found.";
            Log.Message(message);
            Messages.Message(message, MessageTypeDefOf.NeutralEvent, historical: false);
        }

        [DebugAction("Gravship Raids", "Force gravship raid here", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.ToolMap)]
        private static void ForceGravshipRaid()
        {
            Map map = Find.CurrentMap;
            IntVec3 cell = UI.MouseCell();
            if (map == null || !cell.InBounds(map))
            {
                return;
            }

            bool previousDebugLogging = GravshipRaidsSettings.debugLogging;
            GravshipRaidsSettings.debugLogging = true;
            PawnsArrivalModeWorker_GravshipLanding.DebugForcedRoot = cell;
            PawnsArrivalModeWorker_GravshipLanding.DebugForcedRotation = rotation;
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
                string message = "[Gravship Raids] Forced gravship raid at " + cell + " " + (success ? "succeeded." : "failed - see log for the declining faction/landing-search reason.");
                Log.Message(message);
                Messages.Message(message, success ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.RejectInput, historical: false);
            }
            finally
            {
                PawnsArrivalModeWorker_GravshipLanding.DebugForcedRoot = null;
                GravshipRaidsSettings.debugLogging = previousDebugLogging;
            }
        }

        [DebugAction("Gravship Raids", "Force enemy gravship departure", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForceEnemyGravshipDeparture()
        {
            Map map = Find.CurrentMap;
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

        private static void Report(string reason)
        {
            string message = "[Gravship Raids] Force enemy gravship departure: cannot run - " + reason;
            Log.Message(message);
            Messages.Message(message, MessageTypeDefOf.RejectInput, historical: false);
        }
    }
}
