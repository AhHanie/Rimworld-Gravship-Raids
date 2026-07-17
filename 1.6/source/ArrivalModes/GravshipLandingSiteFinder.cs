using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Gravship_Raids
{
    public static class GravshipLandingSiteFinder
    {
        private const int MaxTemplatesConsidered = 3;

        private const int MinDistanceToMapEdge = 8;

        private const int MaxCandidateAttemptsFull = 40;

        private const int MaxCandidateAttemptsCheap = 25;

        private const int MaxExpensiveProbesPerTemplate = 8;

        public static bool HasViableLandingArea(Map map, FactionDef factionDef, float points)
        {
            return TryFindLandingSiteInternal(map, factionDef, points, Rand.Int, validateFully: false, out _, out _, out _);
        }

        public static bool TryFindLandingSite(Map map, FactionDef factionDef, float points, int seed, out GravshipRaidTemplateDef template, out IntVec3 root, out Rot4 rotation)
        {
            return TryFindLandingSiteInternal(map, factionDef, points, seed, validateFully: true, out template, out root, out rotation);
        }

        private static bool TryFindLandingSiteInternal(Map map, FactionDef factionDef, float points, int seed, bool validateFully, out GravshipRaidTemplateDef template, out IntVec3 root, out Rot4 rotation)
        {
            template = null;
            root = IntVec3.Invalid;
            rotation = Rot4.North;

            if (map == null)
            {
                return false;
            }

            List<GravshipRaidTemplateDef> pool = GravshipRaidTemplateUtility.GetEligibleTemplates(factionDef, points, map).ToList();
            if (pool.Count == 0)
            {
                return false;
            }

            bool found = false;
            Rand.PushState();
            try
            {
                Rand.Seed = seed;
                int templatesTried = 0;
                while (pool.Count > 0 && templatesTried < MaxTemplatesConsidered)
                {
                    if (!pool.TryRandomElementByWeight((GravshipRaidTemplateDef t) => t.GetSelectionWeight(points), out GravshipRaidTemplateDef candidate))
                    {
                        break;
                    }
                    pool.Remove(candidate);
                    templatesTried++;

                    if (TryFindPlacementForTemplate(candidate, map, validateFully, out IntVec3 foundRoot, out Rot4 foundRot))
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

        private static bool TryFindPlacementForTemplate(GravshipRaidTemplateDef template, Map map, bool validateFully, out IntVec3 root, out Rot4 rotation)
        {
            root = IntVec3.Invalid;
            rotation = Rot4.North;
            if (template?.prefab == null)
            {
                return false;
            }

            List<Rot4> allowedRotations = GetAllowedRotations(template.prefab);
            int clearance = Mathf.CeilToInt(template.landingClearance);
            int maxCandidateAttempts = validateFully ? MaxCandidateAttemptsFull : MaxCandidateAttemptsCheap;
            int expensiveProbesRemaining = validateFully ? MaxExpensiveProbesPerTemplate : 0;

            for (int attempt = 0; attempt < maxCandidateAttempts; attempt++)
            {
                IntVec3 candidate = CellFinder.RandomNotEdgeCell(MinDistanceToMapEdge, map);
                if (!candidate.IsValid)
                {
                    continue;
                }
                Rot4 rot = allowedRotations[Rand.Range(0, allowedRotations.Count)];

                if (!CheapPreScreen(template, map, candidate, rot, clearance))
                {
                    continue;
                }

                // Hostile-accessible: region-based reachability is much cheaper than a CanSpawnPrefab probe,
                // so it is checked on every cheap-prescreen survivor rather than being counted against the
                // expensive-probe budget below.
                if (!map.reachability.CanReachColony(candidate))
                {
                    continue;
                }

                if (!validateFully)
                {
                    root = candidate;
                    rotation = rot;
                    return true;
                }

                if (expensiveProbesRemaining <= 0)
                {
                    break;
                }
                expensiveProbesRemaining--;

                if (!GravshipRaidTemplateUtility.CanSpawnPrefab(template, map, candidate, rot, canWipeEdifices: false))
                {
                    continue;
                }

                root = candidate;
                rotation = rot;
                return true;
            }

            return false;
        }

        private static bool CheapPreScreen(GravshipRaidTemplateDef template, Map map, IntVec3 pos, Rot4 rot, int clearance)
        {
            CellRect footprint = GravshipRaidTemplateUtility.GetRotatedBounds(template, pos, rot);

            CellRect clearRect = clearance > 0 ? footprint.ExpandedBy(clearance) : footprint;
            if (!IsWithinMapEdgeMargin(clearRect, map, MinDistanceToMapEdge))
            {
                return false;
            }

            foreach (IntVec3 cell in footprint.Cells)
            {
                if (!cell.InBounds(map))
                {
                    return false;
                }
                if (cell.Roofed(map))
                {
                    return false;
                }
                if (map.areaManager.Home[cell])
                {
                    return false;
                }
                // Avoid landing on top of an existing gravship pad (player or otherwise) - Substructure is a
                // foundation terrain, kept in TerrainGrid's separate foundationGrid layer. See
                // GravshipRaidTemplateUtility.CanSpawnPrefab's doc-comment for the same FoundationAt pattern.
                if (map.terrainGrid.FoundationAt(cell) != null)
                {
                    return false;
                }
                if (cell.GetFirstBuilding(map) != null)
                {
                    return false;
                }
                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing thing = things[i];
                    if (thing is Blueprint || thing is Frame)
                    {
                        return false;
                    }
                    if (thing is Plant plant && plant.sown)
                    {
                        return false;
                    }
                    // Protected quest locations: Thing.questTags is vanilla's own marker for "a quest cares
                    // about this thing/cell" (see decompiled Verse.Thing and QuestUtility.SendQuestTargetSignals).
                    if (!thing.questTags.NullOrEmpty())
                    {
                        return false;
                    }
                }
            }

            if (clearance > 0)
            {
                // clearRect (computed above) is already guaranteed in-bounds by the IsWithinMapEdgeMargin
                // check, so no ClipInsideMap is needed here.
                foreach (IntVec3 cell in clearRect.Cells)
                {
                    if (footprint.Contains(cell))
                    {
                        continue;
                    }
                    if (map.areaManager.Home[cell])
                    {
                        return false;
                    }
                    if (cell.GetFirstBuilding(map) != null)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsWithinMapEdgeMargin(CellRect rect, Map map, int margin)
        {
            return rect.minX >= margin
                && rect.minZ >= margin
                && rect.maxX <= map.Size.x - 1 - margin
                && rect.maxZ <= map.Size.z - 1 - margin;
        }

        private static List<Rot4> GetAllowedRotations(PrefabDef prefab)
        {
            List<Rot4> rotations = new List<Rot4>(4);
            if ((prefab.rotations & RotEnum.North) != 0)
            {
                rotations.Add(Rot4.North);
            }
            if ((prefab.rotations & RotEnum.East) != 0)
            {
                rotations.Add(Rot4.East);
            }
            if ((prefab.rotations & RotEnum.South) != 0)
            {
                rotations.Add(Rot4.South);
            }
            if ((prefab.rotations & RotEnum.West) != 0)
            {
                rotations.Add(Rot4.West);
            }
            if (rotations.Count == 0)
            {
                rotations.Add(Rot4.North);
            }
            return rotations;
        }
    }
}
