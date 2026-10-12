using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Gravship_Raids
{
    public class ShuttleRaidTemplateDef : Def
    {
        public ThingDef shuttle;

        public TransportShipDef shipDef;

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

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (shuttle == null)
            {
                yield return "shuttle is not assigned.";
                yield break;
            }

            if (!shuttle.HasComp<CompShuttle>())
            {
                yield return $"shuttle '{shuttle.defName}' has no CompShuttle; it is not a placeable shuttle.";
            }

            if (!shuttle.HasComp<CompTransporter>())
            {
                yield return $"shuttle '{shuttle.defName}' has no CompTransporter; it is not a placeable shuttle.";
            }

            if (shipDef == null)
            {
                yield return "shipDef is not assigned.";
            }
            else
            {
                if (shipDef.shipThing != shuttle)
                {
                    yield return $"shipDef '{shipDef.defName}' has shipThing '{shipDef.shipThing?.defName ?? "null"}', which does not match this template's shuttle '{shuttle.defName}'.";
                }
                if (shipDef.arrivingSkyfaller == null)
                {
                    yield return $"shipDef '{shipDef.defName}' has no arrivingSkyfaller.";
                }
                if (shipDef.leavingSkyfaller == null)
                {
                    yield return $"shipDef '{shipDef.defName}' has no leavingSkyfaller.";
                }
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
