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

        private const int MaxCandidateAttemptsFullOrbit = 480;

        private const int MaxCandidateAttemptsCheapOrbit = 400;

        private const int MaxExpensiveProbesPerTemplate = 8;

        internal enum LandingSiteRejectionReason
        {
            MapEdgeMargin,
            Structural,
            HomeArea,
            Reachability,
            NoReachableDoor,
            PrefabPlacement,
            OrbitRoofed,
            OrbitExistingFoundation,
            OrbitBuilding,
            OrbitBlueprintOrFrame,
            OrbitPlant,
            OrbitQuestTag,
            OrbitClearanceBuilding
        }

        internal sealed class LandingSearchDiagnostics
        {
            internal int TemplatesConsidered;

            internal readonly Dictionary<LandingSiteRejectionReason, int> RejectionCounts = new Dictionary<LandingSiteRejectionReason, int>();

            internal bool HasOrbitDeckStats;

            internal IntVec3 LargestInscribedRectanglePosition;

            internal int EligibleDeckCellCount;

            internal int LargestConnectedDeckRegionCellCount;

            internal CellRect LargestConnectedDeckRegionBounds;

            internal IntVec2 LargestInscribedRectangleSize;

            internal string SmallestTemplateDefName;

            internal IntVec2 SmallestTemplateFootprintSize;

            internal int SmallestTemplateClearance;

            internal int GroundAnchorCount;

            internal void Record(LandingSiteRejectionReason reason)
            {
                RejectionCounts.TryGetValue(reason, out int count);
                RejectionCounts[reason] = count + 1;
            }

            internal string Summarize()
            {
                List<string> parts = new List<string>();
                foreach (var pair in RejectionCounts.OrderByDescending((p) => p.Value))
                {
                    parts.Add($"{Label(pair.Key)}: {pair.Value}");
                }
                string suffix = parts.Count > 0 ? ", " + string.Join(", ", parts) : string.Empty;
                string deckSuffix = string.Empty;
                if (HasOrbitDeckStats)
                {
                    string smallestTemplatePart = SmallestTemplateDefName != null
                        ? $"; smallest eligible template '{SmallestTemplateDefName}' needs {SmallestTemplateFootprintSize.x}x{SmallestTemplateFootprintSize.z} ({SmallestTemplateFootprintSize.x * SmallestTemplateFootprintSize.z} cells) contiguous deck, +{SmallestTemplateClearance} clearance around it"
                        : string.Empty;
                    string largestPatchDims = LargestConnectedDeckRegionCellCount > 0
                        ? $"{LargestConnectedDeckRegionBounds.Width}x{LargestConnectedDeckRegionBounds.Height}"
                        : "0x0";
                    string inscribedRectPart = LargestInscribedRectanglePosition.IsValid
                        ? $"{LargestInscribedRectangleSize.x}x{LargestInscribedRectangleSize.z} at {LargestInscribedRectanglePosition}"
                        : $"{LargestInscribedRectangleSize.x}x{LargestInscribedRectangleSize.z}";
                    deckSuffix = $"; map has {EligibleDeckCellCount} unroofed/clear deck cell(s) total, largest contiguous patch is {largestPatchDims} ({LargestConnectedDeckRegionCellCount} cells), largest fully-open rectangle within it is {inscribedRectPart}{smallestTemplatePart}; {GroundAnchorCount} walkable cell(s) on the map";
                }
                return $"templates considered: {TemplatesConsidered}{suffix}{deckSuffix}";
            }

            private static string Label(LandingSiteRejectionReason reason)
            {
                switch (reason)
                {
                    case LandingSiteRejectionReason.MapEdgeMargin:
                        return "too close to map edge";
                    case LandingSiteRejectionReason.Structural:
                        return "blocked by roof/foundation/building/blueprint/plant/quest content";
                    case LandingSiteRejectionReason.HomeArea:
                        return "inside Home area";
                    case LandingSiteRejectionReason.Reachability:
                        return "not reachable from the colony";
                    case LandingSiteRejectionReason.NoReachableDoor:
                        return "no ship door has a walkable path to the colony";
                    case LandingSiteRejectionReason.PrefabPlacement:
                        return "prefab placement rejected";
                    case LandingSiteRejectionReason.OrbitRoofed:
                        return "orbit: footprint cell roofed";
                    case LandingSiteRejectionReason.OrbitExistingFoundation:
                        return "orbit: footprint cell has an existing foundation (likely overlaps a player-built pad)";
                    case LandingSiteRejectionReason.OrbitBuilding:
                        return "orbit: footprint cell has a building on it";
                    case LandingSiteRejectionReason.OrbitBlueprintOrFrame:
                        return "orbit: footprint cell has a blueprint/frame on it";
                    case LandingSiteRejectionReason.OrbitPlant:
                        return "orbit: footprint cell has a sown plant on it";
                    case LandingSiteRejectionReason.OrbitQuestTag:
                        return "orbit: footprint cell has quest-tagged content on it";
                    case LandingSiteRejectionReason.OrbitClearanceBuilding:
                        return "orbit: clearance ring around footprint has a building on it";
                    default:
                        return reason.ToString();
                }
            }
        }

        public static bool HasViableLandingArea(Map map, FactionDef factionDef, float points)
        {
            return TryFindLandingSiteInternal(map, factionDef, points, Rand.Int, validateFully: false, null, out _, out _, out _);
        }

        internal static bool HasViableLandingArea(Map map, FactionDef factionDef, float points, out LandingSearchDiagnostics diagnostics)
        {
            diagnostics = new LandingSearchDiagnostics();
            return TryFindLandingSiteInternal(map, factionDef, points, Rand.Int, validateFully: false, diagnostics, out _, out _, out _);
        }

        public static bool TryFindLandingSite(Map map, FactionDef factionDef, float points, int seed, out GravshipRaidTemplateDef template, out IntVec3 root, out Rot4 rotation)
        {
            return TryFindLandingSiteInternal(map, factionDef, points, seed, validateFully: true, null, out template, out root, out rotation);
        }

        internal static bool TryFindLandingSite(Map map, FactionDef factionDef, float points, int seed, out GravshipRaidTemplateDef template, out IntVec3 root, out Rot4 rotation, out LandingSearchDiagnostics diagnostics)
        {
            diagnostics = new LandingSearchDiagnostics();
            return TryFindLandingSiteInternal(map, factionDef, points, seed, validateFully: true, diagnostics, out template, out root, out rotation);
        }

        private static bool TryFindLandingSiteInternal(Map map, FactionDef factionDef, float points, int seed, bool validateFully, LandingSearchDiagnostics diagnostics, out GravshipRaidTemplateDef template, out IntVec3 root, out Rot4 rotation)
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

            bool isOrbit = IsOrbitMap(map);
            HashSet<IntVec3> orbitDeckEligibleCells = isOrbit ? GetOrbitDeckEligibleCells(map) : null;
            List<IntVec3> orbitGroundAnchors = isOrbit ? GetGroundAnchorCells(map) : null;

            if (isOrbit && diagnostics != null)
            {
                PopulateOrbitDeckDiagnostics(pool, orbitDeckEligibleCells, diagnostics);
                diagnostics.GroundAnchorCount = orbitGroundAnchors.Count;
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
                    if (diagnostics != null)
                    {
                        diagnostics.TemplatesConsidered++;
                    }

                    if (TryFindPlacementForTemplate(candidate, map, validateFully, diagnostics, isOrbit, orbitGroundAnchors, orbitDeckEligibleCells, out IntVec3 foundRoot, out Rot4 foundRot))
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

        private static bool TryFindPlacementForTemplate(GravshipRaidTemplateDef template, Map map, bool validateFully, LandingSearchDiagnostics diagnostics, bool isOrbit, List<IntVec3> orbitGroundAnchors, HashSet<IntVec3> orbitDeckEligibleCells, out IntVec3 root, out Rot4 rotation)
        {
            root = IntVec3.Invalid;
            rotation = Rot4.North;
            if (template?.prefab == null)
            {
                return false;
            }

            if (isOrbit)
            {
                return TryFindOrbitPlacementForTemplate(template, map, validateFully, diagnostics, orbitGroundAnchors, orbitDeckEligibleCells, out root, out rotation);
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

                if (!CheapPreScreen(template, map, candidate, rot, clearance, isOrbit: false, diagnostics, null))
                {
                    continue;
                }

                if (!map.reachability.CanReachColony(candidate))
                {
                    diagnostics?.Record(LandingSiteRejectionReason.Reachability);
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
                    diagnostics?.Record(LandingSiteRejectionReason.PrefabPlacement);
                    continue;
                }

                root = candidate;
                rotation = rot;
                return true;
            }

            return false;
        }

        private static bool TryFindOrbitPlacementForTemplate(GravshipRaidTemplateDef template, Map map, bool validateFully, LandingSearchDiagnostics diagnostics, List<IntVec3> groundAnchors, HashSet<IntVec3> deckEligibleCells, out IntVec3 root, out Rot4 rotation)
        {
            root = IntVec3.Invalid;
            rotation = Rot4.North;
            if (groundAnchors.NullOrEmpty())
            {
                return false;
            }

            List<Rot4> allowedRotations = GetAllowedRotations(template.prefab);
            int clearance = Mathf.CeilToInt(template.landingClearance);
            List<IntVec3> doorLocalCells = GravshipRaidTemplateUtility.GetDoorLocalCells(template);
            int maxCandidateAttempts = validateFully ? MaxCandidateAttemptsFullOrbit : MaxCandidateAttemptsCheapOrbit;
            int expensiveProbesRemaining = validateFully ? MaxExpensiveProbesPerTemplate : 0;

            for (int attempt = 0; attempt < maxCandidateAttempts; attempt++)
            {
                IntVec3 anchor = groundAnchors[Rand.Range(0, groundAnchors.Count)];
                Rot4 rot = allowedRotations[Rand.Range(0, allowedRotations.Count)];

                IntVec3 candidate;
                if (doorLocalCells.Count > 0)
                {
                    IntVec3 localDoor = doorLocalCells[Rand.Range(0, doorLocalCells.Count)];
                    IntVec3 localDir = GenAdj.CardinalDirections[Rand.Range(0, GenAdj.CardinalDirections.Length)];
                    IntVec3 localExteriorCell = GravshipRaidTemplateUtility.GetLocalExteriorCell(template, localDoor, localDir);
                    if (!localExteriorCell.IsValid)
                    {
                        continue;
                    }
                    candidate = anchor - GravshipRaidTemplateUtility.GetLocalCellOffset(template, localExteriorCell, rot);
                }
                else
                {
                    candidate = anchor;
                }

                if (!candidate.IsValid)
                {
                    continue;
                }

                CellRect footprint = GravshipRaidTemplateUtility.GetRotatedBounds(template, candidate, rot);

                if (!CheapPreScreen(template, map, candidate, rot, clearance, isOrbit: true, diagnostics, deckEligibleCells))
                {
                    continue;
                }

                if (doorLocalCells.Count > 0)
                {
                    if (!HasReachableDoor(template, doorLocalCells, candidate, rot, map))
                    {
                        diagnostics?.Record(LandingSiteRejectionReason.NoReachableDoor);
                        continue;
                    }
                }
                else if (!HasFootprintConnectedToColony(footprint, map))
                {
                    diagnostics?.Record(LandingSiteRejectionReason.Reachability);
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
                    diagnostics?.Record(LandingSiteRejectionReason.PrefabPlacement);
                    continue;
                }

                root = candidate;
                rotation = rot;
                return true;
            }

            return false;
        }

        internal static HashSet<IntVec3> GetOrbitDeckEligibleCells(Map map)
        {
            HashSet<IntVec3> result = new HashSet<IntVec3>();
            foreach (IntVec3 cell in map.AllCells)
            {
                if (PassesStructuralCellChecks(cell, map))
                {
                    result.Add(cell);
                }
            }
            return result;
        }

        private static List<IntVec3> GetGroundAnchorCells(Map map)
        {
            List<IntVec3> result = new List<IntVec3>();
            foreach (IntVec3 cell in map.AllCells)
            {
                if (cell.Walkable(map))
                {
                    result.Add(cell);
                }
            }
            return result;
        }

        private static void PopulateOrbitDeckDiagnostics(List<GravshipRaidTemplateDef> pool, HashSet<IntVec3> eligibleCells, LandingSearchDiagnostics diagnostics)
        {
            diagnostics.HasOrbitDeckStats = true;
            diagnostics.EligibleDeckCellCount = eligibleCells.Count;

            var (largestCount, largestBounds) = FindLargestConnectedRegion(eligibleCells);
            diagnostics.LargestConnectedDeckRegionCellCount = largestCount;
            diagnostics.LargestConnectedDeckRegionBounds = largestBounds;
            var (inscribedSize, inscribedPos) = FindLargestInscribedRectangle(eligibleCells, largestBounds);
            diagnostics.LargestInscribedRectangleSize = inscribedSize;
            diagnostics.LargestInscribedRectanglePosition = inscribedPos;

            GravshipRaidTemplateDef smallestTemplate = pool
                .Where((t) => t?.prefab != null)
                .OrderBy((t) => t.prefab.size.x * t.prefab.size.z)
                .FirstOrDefault();
            if (smallestTemplate != null)
            {
                diagnostics.SmallestTemplateDefName = smallestTemplate.defName;
                diagnostics.SmallestTemplateFootprintSize = smallestTemplate.prefab.size;
                diagnostics.SmallestTemplateClearance = Mathf.CeilToInt(smallestTemplate.landingClearance);
            }
        }

        private static (int count, CellRect bounds) FindLargestConnectedRegion(HashSet<IntVec3> eligibleCells)
        {
            HashSet<IntVec3> visited = new HashSet<IntVec3>();
            Queue<IntVec3> queue = new Queue<IntVec3>();
            int bestCount = 0;
            CellRect bestBounds = default;

            foreach (IntVec3 start in eligibleCells)
            {
                if (!visited.Add(start))
                {
                    continue;
                }
                int count = 1;
                int minX = start.x, maxX = start.x, minZ = start.z, maxZ = start.z;
                queue.Clear();
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    IntVec3 current = queue.Dequeue();
                    for (int i = 0; i < GenAdj.CardinalDirections.Length; i++)
                    {
                        IntVec3 next = current + GenAdj.CardinalDirections[i];
                        if (!eligibleCells.Contains(next) || !visited.Add(next))
                        {
                            continue;
                        }
                        count++;
                        if (next.x < minX) minX = next.x;
                        if (next.x > maxX) maxX = next.x;
                        if (next.z < minZ) minZ = next.z;
                        if (next.z > maxZ) maxZ = next.z;
                        queue.Enqueue(next);
                    }
                }
                if (count > bestCount)
                {
                    bestCount = count;
                    bestBounds = new CellRect(minX, minZ, maxX - minX + 1, maxZ - minZ + 1);
                }
            }

            return (bestCount, bestBounds);
        }

        private static (IntVec2 size, IntVec3 pos) FindLargestInscribedRectangle(HashSet<IntVec3> cells, CellRect bounds)
        {
            int width = bounds.Width;
            if (width <= 0 || bounds.Height <= 0)
            {
                return (IntVec2.Zero, IntVec3.Invalid);
            }

            int[] heights = new int[width];
            int[] stack = new int[width + 1];
            IntVec2 bestSize = IntVec2.Zero;
            IntVec3 bestPos = IntVec3.Invalid;
            int bestArea = 0;

            for (int z = bounds.minZ; z <= bounds.maxZ; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    IntVec3 cell = new IntVec3(bounds.minX + x, 0, z);
                    heights[x] = cells.Contains(cell) ? heights[x] + 1 : 0;
                }

                int stackSize = 0;
                for (int x = 0; x <= width; x++)
                {
                    int currentHeight = (x == width) ? 0 : heights[x];
                    while (stackSize > 0 && heights[stack[stackSize - 1]] >= currentHeight)
                    {
                        int h = heights[stack[--stackSize]];
                        int left = stackSize > 0 ? stack[stackSize - 1] + 1 : 0;
                        int w = x - left;
                        int area = h * w;
                        if (area > bestArea)
                        {
                            bestArea = area;
                            bestSize = new IntVec2(w, h);
                            bestPos = new IntVec3(bounds.minX + left, 0, z - h + 1);
                        }
                    }
                    stack[stackSize++] = x;
                }
            }

            return (bestSize, bestPos);
        }

        private static bool CheapPreScreen(GravshipRaidTemplateDef template, Map map, IntVec3 pos, Rot4 rot, int clearance, bool isOrbit, LandingSearchDiagnostics diagnostics, HashSet<IntVec3> deckEligibleCells)
        {
            CellRect footprint = GravshipRaidTemplateUtility.GetRotatedBounds(template, pos, rot);

            if (isOrbit)
            {
                if (!IsWithinMapEdgeMargin(footprint, map, 0))
                {
                    diagnostics?.Record(LandingSiteRejectionReason.MapEdgeMargin);
                    return false;
                }
            }
            else if (!IsWithinMapEdgeMargin(clearance > 0 ? footprint.ExpandedBy(clearance) : footprint, map, MinDistanceToMapEdge))
            {
                diagnostics?.Record(LandingSiteRejectionReason.MapEdgeMargin);
                return false;
            }

            if (isOrbit)
            {
                foreach (IntVec3 localCell in GravshipRaidTemplateUtility.GetPrefabOccupiedLocalCells(template))
                {
                    IntVec3 cell = GravshipRaidTemplateUtility.TransformCell(template, localCell, pos, rot);
                    bool eligible = deckEligibleCells != null ? deckEligibleCells.Contains(cell) : PassesStructuralCellChecks(cell, map);
                    if (!eligible)
                    {
                        LandingSiteRejectionReason structuralReason = GetStructuralFailReason(cell, map) ?? LandingSiteRejectionReason.Structural;
                        diagnostics?.Record(structuralReason);
                        return false;
                    }
                }
            }
            else
            {
                foreach (IntVec3 cell in footprint.Cells)
                {
                    if (!PassesStructuralCellChecks(cell, map))
                    {
                        diagnostics?.Record(LandingSiteRejectionReason.Structural);
                        return false;
                    }
                    if (map.areaManager.Home[cell])
                    {
                        diagnostics?.Record(LandingSiteRejectionReason.HomeArea);
                        return false;
                    }
                }
            }

            if (clearance > 0)
            {
                CellRect clearRect = footprint.ExpandedBy(clearance);
                if (isOrbit)
                {
                    clearRect = clearRect.ClipInsideMap(map);
                }
                foreach (IntVec3 cell in clearRect.Cells)
                {
                    if (footprint.Contains(cell))
                    {
                        continue;
                    }
                    if (!isOrbit && map.areaManager.Home[cell])
                    {
                        diagnostics?.Record(LandingSiteRejectionReason.HomeArea);
                        return false;
                    }
                    if (cell.GetFirstBuilding(map) != null)
                    {
                        diagnostics?.Record(isOrbit ? LandingSiteRejectionReason.OrbitClearanceBuilding : LandingSiteRejectionReason.Structural);
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool PassesStructuralCellChecks(IntVec3 cell, Map map)
        {
            return !GetStructuralFailReason(cell, map).HasValue;
        }

        private static LandingSiteRejectionReason? GetStructuralFailReason(IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map))
            {
                return LandingSiteRejectionReason.MapEdgeMargin;
            }
            if (cell.Roofed(map))
            {
                return LandingSiteRejectionReason.OrbitRoofed;
            }
            if (map.terrainGrid.FoundationAt(cell) != null)
            {
                return LandingSiteRejectionReason.OrbitExistingFoundation;
            }
            if (cell.GetFirstBuilding(map) != null)
            {
                return LandingSiteRejectionReason.OrbitBuilding;
            }
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing is Blueprint || thing is Frame)
                {
                    return LandingSiteRejectionReason.OrbitBlueprintOrFrame;
                }
                if (thing is Plant plant && plant.sown)
                {
                    return LandingSiteRejectionReason.OrbitPlant;
                }
                if (!thing.questTags.NullOrEmpty())
                {
                    return LandingSiteRejectionReason.OrbitQuestTag;
                }
            }
            return null;
        }

        internal static bool IsOrbitMap(Map map)
        {
            return map.Tile.Valid && map.Tile.LayerDef == PlanetLayerDefOf.Orbit;
        }

        private static bool HasReachableDoor(GravshipRaidTemplateDef template, List<IntVec3> doorLocalCells, IntVec3 pos, Rot4 rot, Map map)
        {
            if (doorLocalCells.NullOrEmpty())
            {
                return true;
            }

            foreach (IntVec3 localDoor in doorLocalCells)
            {
                for (int i = 0; i < GenAdj.CardinalDirections.Length; i++)
                {
                    IntVec3 localDir = GenAdj.CardinalDirections[i];
                    IntVec3 localExteriorCell = GravshipRaidTemplateUtility.GetLocalExteriorCell(template, localDoor, localDir);
                    if (!localExteriorCell.IsValid)
                    {
                        continue;
                    }
                    IntVec3 exteriorCell = GravshipRaidTemplateUtility.TransformCell(template, localExteriorCell, pos, rot);
                    if (exteriorCell.InBounds(map) && exteriorCell.Walkable(map))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasFootprintConnectedToColony(CellRect footprint, Map map)
        {
            foreach (IntVec3 cell in footprint.Cells)
            {
                if (cell.Walkable(map))
                {
                    return true;
                }
            }
            return false;
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
