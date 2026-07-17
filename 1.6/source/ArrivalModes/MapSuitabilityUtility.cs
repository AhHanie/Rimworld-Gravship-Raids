using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
using Verse;

namespace Gravship_Raids
{
    public static class MapSuitabilityUtility
    {
        private const int WaterSampleCount = 80;

        private const float MaxWaterFraction = 0.65f;

        private const int RoofSampleCount = 80;

        private const float MaxRoofedFraction = 0.75f;

        private const int MapEdgeMargin = 8;

        private static readonly ConditionalWeakTable<IncidentParms, CachedResult> ResultCache = new ConditionalWeakTable<IncidentParms, CachedResult>();

        private sealed class CachedResult
        {
            public readonly bool Suitable;
            public readonly string Reason;

            public CachedResult(bool suitable, string reason)
            {
                Suitable = suitable;
                Reason = reason;
            }
        }

        public static bool IsMapSuitable(Map map, out string reason)
        {
            return IsMapSuitable(map, null, out reason);
        }

        public static bool IsMapSuitable(Map map, IncidentParms cacheKey, out string reason)
        {
            if (cacheKey != null && ResultCache.TryGetValue(cacheKey, out CachedResult cached))
            {
                reason = cached.Reason;
                return cached.Suitable;
            }

            bool suitable = ComputeIsMapSuitable(map, out reason);

            if (cacheKey != null)
            {
                ResultCache.Remove(cacheKey);
                ResultCache.Add(cacheKey, new CachedResult(suitable, reason));
            }

            return suitable;
        }

        private static bool ComputeIsMapSuitable(Map map, out string reason)
        {
            reason = null;
            if (map == null)
            {
                reason = "map is null";
                return false;
            }

            // Real vanilla flag for "this is not a normal, spatially-open colony map" (ship interiors, vault
            // interiors, and similar - see decompiled Verse.Map.IsPocketMap / RimWorld.Planet.PocketMapParent).
            // A gravship cannot sensibly land inside one of these.
            if (map.IsPocketMap)
            {
                reason = "map is a pocket map (Map.IsPocketMap) - not a spatially-open map a gravship can land on";
                return false;
            }

            int minDimension = MinimumRequiredMapDimension();
            if (minDimension > 0 && (map.Size.x < minDimension || map.Size.z < minDimension))
            {
                reason = $"map size {map.Size} is smaller than the smallest eligible template needs ({minDimension}x{minDimension}, including landing clearance and edge margin)";
                return false;
            }

            int minColonists = GravshipRaidsSettings.minColonistCount;
            if (map.mapPawns.FreeColonistsSpawnedCount < minColonists)
            {
                reason = $"map has fewer than {minColonists} free spawned colonist(s) - not a viable colony to raid";
                return false;
            }

            if (SampledFractionAtLeast(map, WaterSampleCount, IsWaterCell, MaxWaterFraction))
            {
                reason = $"map is estimated to be at least {MaxWaterFraction:P0} covered by water terrain";
                return false;
            }

            if (SampledFractionAtLeast(map, RoofSampleCount, IsRoofedCell, MaxRoofedFraction))
            {
                reason = $"map is estimated to be at least {MaxRoofedFraction:P0} roofed (enclosed/underground)";
                return false;
            }

            return true;
        }

        private static bool IsWaterCell(IntVec3 cell, Map map)
        {
            return cell.GetTerrain(map)?.IsWater ?? false;
        }

        private static bool IsRoofedCell(IntVec3 cell, Map map)
        {
            return cell.Roofed(map);
        }

        private static int MinimumRequiredMapDimension()
        {
            int best = int.MaxValue;
            foreach (GravshipRaidTemplateDef template in DefDatabase<GravshipRaidTemplateDef>.AllDefsListForReading)
            {
                if (!GravshipRaidTemplateUtility.IsValidTemplate(template))
                {
                    continue;
                }
                int longestSide = Mathf.Max(template.prefab.size.x, template.prefab.size.z);
                int required = longestSide + Mathf.CeilToInt(template.landingClearance) * 2 + MapEdgeMargin * 2;
                if (required < best)
                {
                    best = required;
                }
            }
            return best == int.MaxValue ? 0 : best;
        }

        private static bool SampledFractionAtLeast(Map map, int sampleCount, System.Func<IntVec3, Map, bool> predicate, float threshold)
        {
            if (sampleCount <= 0 || threshold <= 0f)
            {
                return false;
            }
            int hits = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                IntVec3 cell = new IntVec3(Rand.Range(0, map.Size.x), 0, Rand.Range(0, map.Size.z));
                if (predicate(cell, map))
                {
                    hits++;
                }
            }
            return hits >= sampleCount * threshold;
        }
    }
}
