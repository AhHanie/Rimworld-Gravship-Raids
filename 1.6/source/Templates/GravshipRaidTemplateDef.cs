using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Gravship_Raids
{
    public class GravshipRaidTemplateDef : Def
    {
        public PrefabDef prefab;

        public bool disabled = false;

        public string author;

        public float selectionWeight = 1f;

        public SimpleCurve selectionWeightPerPointsCurve;

        public float minPoints = 0f;

        public float maxPoints = -1f;

        public List<FactionDef> allowedFactions;

        public List<FactionDef> disallowedFactions;

        public List<BiomeDef> allowedBiomes;

        public List<BiomeDef> disallowedBiomes;

        public List<PlanetLayerDef> allowedLayers;

        public float landingClearance = 3f;

        public int retreatTimeoutTicks = 3600;

        public ThingDef arrivalSkyfaller;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (prefab == null)
            {
                yield return "prefab is not assigned.";
                yield break;
            }

            List<(PrefabThingData data, IntVec3 cell)> coreThings = prefab.GetThings()
                .Where(t => t.data?.def != null && t.data.def.HasComp<CompEnemyGravshipCore>())
                .ToList();
            if (coreThings.Count == 0)
            {
                yield return $"prefab '{prefab.defName}' contains no CompEnemyGravshipCore-bearing building; every template needs exactly one.";
            }
            else if (coreThings.Count > 1)
            {
                yield return $"prefab '{prefab.defName}' contains {coreThings.Count} CompEnemyGravshipCore-bearing buildings; every template needs exactly one.";
            }

            HashSet<IntVec3> occupiedCells = new HashSet<IntVec3>();
            foreach (var (data, cell) in prefab.GetThings())
            {
                if (data?.def == null)
                {
                    continue;
                }
                foreach (IntVec3 occupied in GenAdj.OccupiedRect(cell, Rot4.North, data.def.Size))
                {
                    occupiedCells.Add(occupied);
                }
            }
            bool hasOpenInteriorCell = false;
            CellRect localRect = new CellRect(0, 0, prefab.size.x, prefab.size.z);
            foreach (IntVec3 localCell in localRect.Cells)
            {
                if (!occupiedCells.Contains(localCell))
                {
                    hasOpenInteriorCell = true;
                    break;
                }
            }
            if (!hasOpenInteriorCell)
            {
                yield return $"prefab '{prefab.defName}' has no open interior floor cell (every local cell within its {prefab.size} footprint is covered by a thing); raid pawns would have nowhere to spawn.";
            }

            if (maxPoints >= 0f && maxPoints < minPoints)
            {
                yield return $"maxPoints ({maxPoints}) is less than minPoints ({minPoints}).";
            }

            if (!allowedFactions.NullOrEmpty() && !disallowedFactions.NullOrEmpty())
            {
                yield return "allowedFactions (whitelist) and disallowedFactions (blacklist) are both defined; this is almost certainly a mistake.";
            }

            if (!allowedBiomes.NullOrEmpty() && !disallowedBiomes.NullOrEmpty())
            {
                yield return "allowedBiomes (whitelist) and disallowedBiomes (blacklist) are both defined; this is almost certainly a mistake.";
            }

            if (selectionWeight <= 0f && selectionWeightPerPointsCurve == null)
            {
                yield return "selectionWeight is <= 0 and no selectionWeightPerPointsCurve is set; this template can never be selected.";
            }

            if (landingClearance < 0f)
            {
                yield return "landingClearance is negative.";
            }

            if (retreatTimeoutTicks < 0)
            {
                yield return "retreatTimeoutTicks is negative.";
            }
        }

        public bool AllowsFaction(FactionDef factionDef)
        {
            if (factionDef == null)
            {
                return true;
            }
            if (!disallowedFactions.NullOrEmpty() && disallowedFactions.Contains(factionDef))
            {
                return false;
            }
            if (!allowedFactions.NullOrEmpty() && !allowedFactions.Contains(factionDef))
            {
                return false;
            }
            return true;
        }

        public bool AllowsBiome(BiomeDef biome)
        {
            if (biome == null)
            {
                return true;
            }
            if (!disallowedBiomes.NullOrEmpty() && disallowedBiomes.Contains(biome))
            {
                return false;
            }
            if (!allowedBiomes.NullOrEmpty() && !allowedBiomes.Contains(biome))
            {
                return false;
            }
            return true;
        }

        public bool AllowsLayer(PlanetLayerDef layer)
        {
            if (layer == null || allowedLayers.NullOrEmpty())
            {
                return true;
            }
            return allowedLayers.Contains(layer);
        }

        public bool PointsInRange(float points)
        {
            if (points < minPoints)
            {
                return false;
            }
            if (maxPoints >= 0f && points > maxPoints)
            {
                return false;
            }
            return true;
        }

        public float GetSelectionWeight(float points)
        {
            float weight = selectionWeight;
            if (selectionWeightPerPointsCurve != null)
            {
                weight *= selectionWeightPerPointsCurve.Evaluate(points);
            }
            return Mathf.Max(0f, weight);
        }
    }
}
