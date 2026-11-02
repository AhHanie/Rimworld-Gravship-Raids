using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Gravship_Raids
{
    public static class ShuttleRaidTemplateUtility
    {
        private const int MinFootprintDistanceToMapEdge = 8;

        internal const int RelocationRadius = 12;

        private const int MaxTemplatesConsidered = 3;

        private const int MaxCandidateAttemptsFull = 40;

        private const int MaxCandidateAttemptsCheap = 25;

        internal enum LandingRejection
        {
            None,
            MissingDef,
            EdgeClearance,
            Water,
            Roofed,
            ShuttleTerrain,
            SkyfallerSpawn,
            InteractionCell,
            Blocked,
            Vanilla,
            Unreachable
        }

        public static bool IsValidTemplate(ShuttleRaidTemplateDef template)
        {
            return template?.shuttle != null && !template.ConfigErrors().Any();
        }

        public static bool IsEligibleTemplate(ShuttleRaidTemplateDef template, FactionDef factionDef, float points, Map map)
        {
            if (template.disabled)
            {
                return false;
            }
            if (!IsValidTemplate(template))
            {
                return false;
            }
            if (!template.PointsInRange(points))
            {
                return false;
            }
            if (!template.AllowsFaction(factionDef))
            {
                return false;
            }
            if (!GravshipRaidsSettings.AllowsShuttleFactionGlobally(factionDef))
            {
                return false;
            }
            if (map != null)
            {
                if (!template.AllowsBiome(map.Biome))
                {
                    return false;
                }
                if (map.Tile.Valid && !template.AllowsLayer(map.Tile.LayerDef))
                {
                    return false;
                }
            }
            return true;
        }

        public static IEnumerable<ShuttleRaidTemplateDef> GetEligibleTemplates(FactionDef factionDef, float points, Map map)
        {
            foreach (ShuttleRaidTemplateDef template in DefDatabase<ShuttleRaidTemplateDef>.AllDefsListForReading)
            {
                if (IsEligibleTemplate(template, factionDef, points, map))
                {
                    yield return template;
                }
            }
        }

        public static bool HasEligibleTemplate(FactionDef factionDef, float points, Map map)
        {
            return GetEligibleTemplates(factionDef, points, map).Any();
        }

        private static int MakeSelectionSeed(FactionDef factionDef, float points, Map map)
        {
            int factionHash = factionDef?.shortHash ?? 0;
            int mapHash = map?.uniqueID ?? 0;
            int pointsHash = Mathf.RoundToInt(points);
            return Gen.HashCombineInt(factionHash, mapHash, pointsHash, 1);
        }

        public static bool HasViableLandingArea(Map map, FactionDef factionDef, float points)
        {
            int seed = Gen.HashCombineInt(MakeSelectionSeed(factionDef, points, map), Find.TickManager.TicksGame / GenDate.TicksPerHour);
            return TryFindLandingSiteInternal(map, factionDef, points, seed, validateFully: false, out _, out _, out _);
        }

        public static bool TryFindLandingSite(Map map, FactionDef factionDef, float points, out ShuttleRaidTemplateDef template, out IntVec3 root, out Rot4 rotation)
        {
            int seed = Gen.HashCombineInt(MakeSelectionSeed(factionDef, points, map), Find.TickManager.TicksGame);
            return TryFindLandingSiteInternal(map, factionDef, points, seed, validateFully: true, out template, out root, out rotation);
        }

        private static bool TryFindLandingSiteInternal(Map map, FactionDef factionDef, float points, int seed, bool validateFully, out ShuttleRaidTemplateDef template, out IntVec3 root, out Rot4 rotation)
        {
            template = null;
            root = IntVec3.Invalid;
            rotation = Rot4.North;

            if (map == null)
            {
                return false;
            }

            List<ShuttleRaidTemplateDef> pool = GetEligibleTemplates(factionDef, points, map).ToList();
            if (pool.Count == 0)
            {
                return false;
            }

            int maxCandidateAttempts = validateFully ? MaxCandidateAttemptsFull : MaxCandidateAttemptsCheap;
            bool found = false;
            Dictionary<LandingRejection, int> tally = (validateFully && GravshipRaidsSettings.debugLogging) ? new Dictionary<LandingRejection, int>() : null;

            Rand.PushState();
            try
            {
                Rand.Seed = seed;
                int templatesTried = 0;
                while (pool.Count > 0 && templatesTried < MaxTemplatesConsidered)
                {
                    if (!pool.TryRandomElementByWeight((ShuttleRaidTemplateDef t) => t.GetSelectionWeight(points), out ShuttleRaidTemplateDef candidate))
                    {
                        break;
                    }
                    pool.Remove(candidate);
                    templatesTried++;

                    if (TryFindPlacementForTemplate(candidate, map, maxCandidateAttempts, tally, out IntVec3 foundRoot, out Rot4 foundRot))
                    {
                        template = candidate;
                        root = foundRoot;
                        rotation = foundRot;
                        found = true;
                        break;
                    }
                }
            }
            finally
            {
                Rand.PopState();
            }

            if (!found && tally != null)
            {
                Logger.Message($"ShuttleRaidTemplateUtility.TryFindLandingSite: no site found on {map}; rejection tally: {string.Join(", ", tally.Select((KeyValuePair<LandingRejection, int> kv) => kv.Key + "=" + kv.Value))}.");
            }

            return found;
        }

        private static bool TryFindPlacementForTemplate(ShuttleRaidTemplateDef template, Map map, int maxCandidateAttempts, Dictionary<LandingRejection, int> tally, out IntVec3 root, out Rot4 rotation)
        {
            root = IntVec3.Invalid;
            rotation = Rot4.North;

            ThingDef shuttleDef = template?.shuttle;
            if (shuttleDef == null)
            {
                return false;
            }

            List<Rot4> allowedRotations = GetAllowedRotations(shuttleDef);
            int sampleMargin = MinFootprintDistanceToMapEdge + GetMaxExtent(template) / 2 + 1;

            for (int attempt = 0; attempt < maxCandidateAttempts; attempt++)
            {
                IntVec3 candidate = CellFinder.RandomNotEdgeCell(sampleMargin, map);
                if (!candidate.IsValid)
                {
                    continue;
                }

                Rot4 rot = allowedRotations[Rand.Range(0, allowedRotations.Count)];
                LandingRejection rejection = Evaluate(template, candidate, map, rot, atImpact: false, out _);
                if (rejection != LandingRejection.None)
                {
                    if (tally != null)
                    {
                        tally[rejection] = (tally.TryGetValue(rejection, out int count) ? count : 0) + 1;
                    }
                    continue;
                }

                root = candidate;
                rotation = rot;
                return true;
            }

            return false;
        }

        internal static bool TryFindRelocatedSite(ShuttleRaidTemplateDef template, Map map, IntVec3 center, Rot4 preferredRotation, int radius, out IntVec3 root, out Rot4 rotation)
        {
            root = IntVec3.Invalid;
            rotation = preferredRotation;
            if (template?.shuttle == null || map == null)
            {
                return false;
            }

            List<Rot4> rotations = GetAllowedRotations(template.shuttle);
            if (template.shuttle.rotatable)
            {
                rotations.Remove(preferredRotation);
                rotations.Insert(0, preferredRotation);
            }

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, useCenter: true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }
                for (int i = 0; i < rotations.Count; i++)
                {
                    if (Evaluate(template, cell, map, rotations[i], atImpact: true, out _) != LandingRejection.None)
                    {
                        continue;
                    }
                    IntVec3 interactionCell = ThingUtility.InteractionCellWhenAt(template.shuttle, cell, rotations[i], map);
                    if (!map.reachability.CanReachColony(interactionCell))
                    {
                        continue;
                    }
                    root = cell;
                    rotation = rotations[i];
                    return true;
                }
            }
            return false;
        }

        public static bool CanShuttleLandAt(ShuttleRaidTemplateDef template, IntVec3 cell, Map map, Rot4 rotation)
        {
            return Evaluate(template, cell, map, rotation, atImpact: false, out _) == LandingRejection.None;
        }

        public static AcceptanceReport CanShuttleLandAtReport(ShuttleRaidTemplateDef template, IntVec3 cell, Map map, Rot4 rotation)
        {
            LandingRejection rejection = Evaluate(template, cell, map, rotation, atImpact: false, out string reason);
            return rejection == LandingRejection.None ? AcceptanceReport.WasAccepted : new AcceptanceReport(reason);
        }

        internal static bool IsSiteStillValidAtImpact(ShuttleRaidTemplateDef template, IntVec3 cell, Map map, Rot4 rotation, out string reason)
        {
            return Evaluate(template, cell, map, rotation, atImpact: true, out reason) == LandingRejection.None;
        }

        private static LandingRejection Evaluate(ShuttleRaidTemplateDef template, IntVec3 cell, Map map, Rot4 rotation, bool atImpact, out string reason)
        {
            reason = null;
            ThingDef shuttleDef = template?.shuttle;
            if (map == null || shuttleDef == null)
            {
                reason = "No map or shuttle def to land.";
                return LandingRejection.MissingDef;
            }

            ThingDef skyfallerDef = template.shipDef?.arrivingSkyfaller;
            CellRect footprint = GenAdj.OccupiedRect(cell, rotation, shuttleDef.size);
            CellRect skyfallerFootprint = skyfallerDef != null ? GenAdj.OccupiedRect(cell, rotation, skyfallerDef.size) : footprint;
            IntVec3 interactionCell = ThingUtility.InteractionCellWhenAt(shuttleDef, cell, rotation, map);

            if (atImpact)
            {
                if (!cell.InBounds(map) || !footprint.InBounds(map))
                {
                    reason = "Landing site is out of bounds.";
                    return LandingRejection.EdgeClearance;
                }
            }
            else if (!cell.InBounds(map)
                || !footprint.ExpandedBy(MinFootprintDistanceToMapEdge).InBounds(map)
                || !skyfallerFootprint.ExpandedBy(MinFootprintDistanceToMapEdge).InBounds(map))
            {
                reason = $"Landing site is within {MinFootprintDistanceToMapEdge} cells of the map edge.";
                return LandingRejection.EdgeClearance;
            }

            if (!interactionCell.InBounds(map))
            {
                reason = "Interaction cell is out of bounds.";
                return LandingRejection.InteractionCell;
            }

            foreach (IntVec3 c in footprint.Cells)
            {
                if (IsWaterAt(c, map))
                {
                    reason = $"Shuttle footprint overlaps water ({DescribeTerrain(c, map)} at {c}).";
                    return LandingRejection.Water;
                }
            }
            if (IsWaterAt(interactionCell, map))
            {
                reason = $"Interaction cell is on water ({DescribeTerrain(interactionCell, map)} at {interactionCell}).";
                return LandingRejection.Water;
            }

            if (!atImpact)
            {
                foreach (IntVec3 c in footprint.Cells)
                {
                    if (c.Roofed(map))
                    {
                        reason = "Landing site is roofed.";
                        return LandingRejection.Roofed;
                    }
                }
                if (interactionCell.Roofed(map))
                {
                    reason = "Landing site is roofed.";
                    return LandingRejection.Roofed;
                }
            }

            if (!GenSpawn.CanSpawnAt(shuttleDef, cell, map, rotation))
            {
                reason = $"Shuttle '{shuttleDef.defName}' cannot be placed at {cell} (needs {shuttleDef.terrainAffordanceNeeded?.defName ?? "no"} terrain affordance; terrain {DescribeTerrain(cell, map)}).";
                return LandingRejection.ShuttleTerrain;
            }
            if (!atImpact && skyfallerDef != null && !GenSpawn.CanSpawnAt(skyfallerDef, cell, map, rotation))
            {
                reason = $"Arriving skyfaller '{skyfallerDef.defName}' cannot spawn at {cell}.";
                return LandingRejection.SkyfallerSpawn;
            }

            if (!interactionCell.Walkable(map))
            {
                reason = $"Interaction cell {interactionCell} is not walkable.";
                return LandingRejection.InteractionCell;
            }

            if (atImpact)
            {
                string blocker = FindBlocker(footprint, interactionCell, map);
                if (blocker != null)
                {
                    reason = blocker;
                    return LandingRejection.Blocked;
                }
                return LandingRejection.None;
            }

            AcceptanceReport vanilla = RoyalTitlePermitWorker_CallShuttle.ShuttleCanLandHere(cell, map, shuttleDef, rotation);
            if (!vanilla.Accepted)
            {
                reason = vanilla.Reason;
                return LandingRejection.Vanilla;
            }

            if (!map.reachability.CanReachColony(interactionCell))
            {
                reason = $"Interaction cell {interactionCell} cannot reach the colony.";
                return LandingRejection.Unreachable;
            }

            return LandingRejection.None;
        }

        private static string FindBlocker(CellRect footprint, IntVec3 interactionCell, Map map)
        {
            foreach (IntVec3 c in footprint.Cells.Concat(Gen.YieldSingle(interactionCell)))
            {
                List<Thing> things = c.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing thing = things[i];
                    if (thing is Skyfaller)
                    {
                        continue;
                    }
                    if (thing is IActiveTransporter
                        || (thing.def.category == ThingCategory.Building && !thing.def.building.isPowerConduit)
                        || (thing.def.plant != null && thing.def.plant.IsTree))
                    {
                        return $"Blocked by {thing.LabelShort} at {c}.";
                    }
                }
            }
            return null;
        }

        internal static bool IsWaterAt(IntVec3 cell, Map map)
        {
            TerrainGrid grid = map.terrainGrid;
            return (grid.TerrainAt(cell)?.IsWater ?? false)
                || (grid.BaseTerrainAt(cell)?.IsWater ?? false)
                || (grid.UnderTerrainAt(cell)?.IsWater ?? false);
        }

        internal static string DescribeTerrain(IntVec3 cell, Map map)
        {
            return cell.InBounds(map) ? (cell.GetTerrain(map)?.defName ?? "null") : "out of bounds";
        }

        private static int GetMaxExtent(ShuttleRaidTemplateDef template)
        {
            IntVec2 shuttleSize = template.shuttle.size;
            int extent = Mathf.Max(shuttleSize.x, shuttleSize.z);
            if (template.shipDef?.arrivingSkyfaller != null)
            {
                IntVec2 skyfallerSize = template.shipDef.arrivingSkyfaller.size;
                extent = Mathf.Max(extent, Mathf.Max(skyfallerSize.x, skyfallerSize.z));
            }
            return extent;
        }

        private static List<Rot4> GetAllowedRotations(ThingDef shuttleDef)
        {
            if (!shuttleDef.rotatable)
            {
                return new List<Rot4> { shuttleDef.defaultPlacingRot };
            }
            return new List<Rot4> { Rot4.North, Rot4.East, Rot4.South, Rot4.West };
        }
    }
}
