using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Gravship_Raids
{
    // Soft compatibility with Vanilla Gravship Expanded - Chapter 2 (vanillaexpanded.gravship2): equips this
    // mod's own gravship/shuttle raid crews with a fully charged VGE2 astrorig before they land on a vacuum map.
    //
    // No compile-time reference to VGE2's assembly exists (or is needed) - the astrorig is a plain ThingDef
    // looked up by name, and CompApparelOxygenProvider fills a newly made astrorig to full charge on its own
    // (PostPostMake), so this helper only has to create and wear it. Equipping it here, before VGE2's own global
    // GenSpawn.Spawn patch runs, makes that patch see the conflicting oxygen-provider apparel already worn and
    // skip giving a random astrorig/oxygen pack of its own.
    internal sealed class VGE2AstrorigCompatibility : RaidCompatibilityModule
    {
        private const string VGE2PackageId = "vanillaexpanded.gravship2";

        private const string AstrorigDefName = "VGE_Apparel_Astrorig";

        private static bool enableAstrorigsForSpaceRaids = true;

        public override bool IsActive => ModsConfig.IsActive(VGE2PackageId);

        public override bool HasSettings => true;

        public override void ExposeSettings()
        {
            Scribe_Values.Look(ref enableAstrorigsForSpaceRaids, "enableAstrorigsForSpaceRaids", true);
        }

        public override void DrawSettings(Listing_Standard listing)
        {
            listing.CheckboxLabeled(
                "GravshipRaids.Settings.EnableAstrorigsForSpaceRaids".Translate(),
                ref enableAstrorigsForSpaceRaids,
                "GravshipRaids.Settings.EnableAstrorigsForSpaceRaidsDesc".Translate());
        }

        public override void OnRaidPawnsArriving(IEnumerable<Pawn> pawns, Map map)
        {
            if (map == null || map.Disposed || !map.Biome.inVacuum || pawns == null || !enableAstrorigsForSpaceRaids)
            {
                return;
            }

            ThingDef astrorigDef = DefDatabase<ThingDef>.GetNamedSilentFail(AstrorigDefName);
            if (astrorigDef == null || !astrorigDef.IsApparel)
            {
                Logger.Message($"VGE2AstrorigCompatibility.OnRaidPawnsArriving: ThingDef '{AstrorigDefName}' is missing or not apparel; skipping astrorig equip.");
                return;
            }

            int equipped = 0;
            int skipped = 0;
            foreach (Pawn pawn in pawns)
            {
                if (pawn == null || !pawn.RaceProps.Humanlike || pawn.apparel == null)
                {
                    continue;
                }

                if (pawn.apparel.WornApparel.Exists((Apparel a) => a.def == astrorigDef))
                {
                    skipped++;
                    continue;
                }

                if (!ApparelUtility.HasPartsToWear(pawn, astrorigDef))
                {
                    skipped++;
                    continue;
                }

                foreach (Apparel worn in pawn.apparel.WornApparel.ToArray())
                {
                    if (!ApparelUtility.CanWearTogether(astrorigDef, worn.def, pawn.RaceProps.body))
                    {
                        pawn.apparel.Remove(worn);
                        worn.Destroy();
                    }
                }

                Apparel astrorig = (Apparel)ThingMaker.MakeThing(astrorigDef);
                PawnGenerator.PostProcessGeneratedGear(astrorig, pawn);
                pawn.apparel.Wear(astrorig, dropReplacedApparel: false);
                equipped++;
            }

            Logger.Message($"VGE2AstrorigCompatibility.OnRaidPawnsArriving: equipped {equipped} astrorig(s), skipped {skipped} pawn(s) on map '{map}'.");
        }
    }
}
