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

        private const int MaxCloseToColonyAttempts = 6;

        private const int MaxMapAttempts = 40;

        private const int MaxTemplatesConsideredForViability = 3;

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

        public static int MakeSelectionSeed(FactionDef factionDef, float points, Map map)
        {
            int factionHash = factionDef?.shortHash ?? 0;
            int mapHash = map?.uniqueID ?? 0;
            int pointsHash = Mathf.RoundToInt(points);
            return Gen.HashCombineInt(factionHash, mapHash, pointsHash, 1);
        }

        public static ShuttleRaidTemplateDef SelectTemplate(FactionDef factionDef, float points, Map map, int seed)
        {
            List<ShuttleRaidTemplateDef> candidates = GetEligibleTemplates(factionDef, points, map).ToList();
            if (candidates.Count == 0)
            {
                return null;
            }
            ShuttleRaidTemplateDef result = null;
            Rand.PushState();
            try
            {
                Rand.Seed = seed;
                candidates.TryRandomElementByWeight((ShuttleRaidTemplateDef t) => t.GetSelectionWeight(points), out result);
            }
            finally
            {
                Rand.PopState();
            }
            return result;
        }

        public static ShuttleRaidTemplateDef SelectTemplate(FactionDef factionDef, float points, Map map)
        {
            return SelectTemplate(factionDef, points, map, MakeSelectionSeed(factionDef, points, map));
        }

        public static bool HasViableLandingArea(Map map, FactionDef factionDef, float points)
        {
            if (map == null)
            {
                return false;
            }
            List<ShuttleRaidTemplateDef> candidates = GetEligibleTemplates(factionDef, points, map).ToList();
            if (candidates.Count == 0)
            {
                return false;
            }
            int seed = Gen.HashCombineInt(MakeSelectionSeed(factionDef, points, map), 2);
            int templatesTried = 0;
            foreach (ShuttleRaidTemplateDef template in candidates)
            {
                if (templatesTried >= MaxTemplatesConsideredForViability)
                {
                    break;
                }
                templatesTried++;
                if (TryFindLandingSite(map, template, seed, out _, out _))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool TryFindLandingSite(Map map, ShuttleRaidTemplateDef template, int seed, out IntVec3 root, out Rot4 rotation)
        {
            root = IntVec3.Invalid;
            rotation = Rot4.North;
            if (map == null || template?.shuttle == null)
            {
                return false;
            }

            ThingDef shuttleDef = template.shuttle;
            List<Rot4> allowedRotations = GetAllowedRotations(shuttleDef);
            bool found = false;

            Rand.PushState();
            try
            {
                Rand.Seed = seed;

                for (int attempt = 0; attempt < MaxCloseToColonyAttempts; attempt++)
                {
                    IntVec3 candidate = DropCellFinder.TryFindSafeLandingSpotCloseToColony(map, shuttleDef.Size);
                    if (!candidate.IsValid)
                    {
                        continue;
                    }
                    Rot4 rot = allowedRotations[Rand.Range(0, allowedRotations.Count)];
                    if (!ShuttleCanLandAt(candidate, map, shuttleDef, rot))
                    {
                        continue;
                    }
                    root = candidate;
                    rotation = rot;
                    found = true;
                    break;
                }

                if (!found)
                {
                    for (int attempt = 0; attempt < MaxMapAttempts; attempt++)
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
                        if (!ShuttleCanLandAt(candidate, map, shuttleDef, rot))
                        {
                            continue;
                        }
                        root = candidate;
                        rotation = rot;
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

        private static bool ShuttleCanLandAt(IntVec3 cell, Map map, ThingDef shuttleDef, Rot4 rot)
        {
            return RoyalTitlePermitWorker_CallShuttle.ShuttleCanLandHere(cell, map, shuttleDef, rot).Accepted;
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
