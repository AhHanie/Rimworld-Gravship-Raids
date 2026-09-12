using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Gravship_Raids
{
    public static class ShuttleRaidTemplateUtility
    {
        private const int MinDistanceToMapEdge = 8;

        private const int MaxTemplatesConsidered = 3;

        private const int MaxCandidateAttemptsFull = 40;

        private const int MaxCandidateAttemptsCheap = 25;

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
            return TryFindLandingSiteInternal(map, factionDef, points, Rand.Int, validateFully: false, out _, out _, out _);
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

                    if (TryFindPlacementForTemplate(candidate, map, maxCandidateAttempts, out IntVec3 foundRoot, out Rot4 foundRot))
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

            return found;
        }

        private static bool TryFindPlacementForTemplate(ShuttleRaidTemplateDef template, Map map, int maxCandidateAttempts, out IntVec3 root, out Rot4 rotation)
        {
            root = IntVec3.Invalid;
            rotation = Rot4.North;

            ThingDef shuttleDef = template?.shuttle;
            if (shuttleDef == null)
            {
                return false;
            }

            List<Rot4> allowedRotations = GetAllowedRotations(shuttleDef);

            for (int attempt = 0; attempt < maxCandidateAttempts; attempt++)
            {
                IntVec3 candidate = CellFinder.RandomNotEdgeCell(MinDistanceToMapEdge, map);
                if (!candidate.IsValid)
                {
                    continue;
                }

                if (!map.reachability.CanReachColony(candidate))
                {
                    continue;
                }

                Rot4 rot = allowedRotations[Rand.Range(0, allowedRotations.Count)];
                if (!CanShuttleLandAt(candidate, map, shuttleDef, rot))
                {
                    continue;
                }

                root = candidate;
                rotation = rot;
                return true;
            }

            return false;
        }

        public static bool CanShuttleLandAt(IntVec3 cell, Map map, ThingDef shuttleDef, Rot4 rotation)
        {
            return CanShuttleLandAtReport(cell, map, shuttleDef, rotation).Accepted;
        }

        public static AcceptanceReport CanShuttleLandAtReport(IntVec3 cell, Map map, ThingDef shuttleDef, Rot4 rotation)
        {
            if (map == null || shuttleDef == null)
            {
                return "No map or shuttle def to land.";
            }
            if (!IsFootprintUnroofed(cell, map, shuttleDef, rotation))
            {
                return "Landing site is roofed.";
            }
            return RoyalTitlePermitWorker_CallShuttle.ShuttleCanLandHere(cell, map, shuttleDef, rotation);
        }

        private static bool IsFootprintUnroofed(IntVec3 cell, Map map, ThingDef shuttleDef, Rot4 rotation)
        {
            foreach (IntVec3 occupiedCell in GenAdj.OccupiedRect(cell, rotation, shuttleDef.size).Cells)
            {
                if (!occupiedCell.InBounds(map) || occupiedCell.Roofed(map))
                {
                    return false;
                }
            }
            IntVec3 interactionCell = ThingUtility.InteractionCellWhenAt(shuttleDef, cell, rotation, map);
            if (interactionCell.InBounds(map) && interactionCell.Roofed(map))
            {
                return false;
            }
            return true;
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
