using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Gravship_Raids
{
    public class GravshipRaidsSettings : ModSettings
    {
        public const float MaxGravshipGuardFraction = 0.3f;

        public static bool enabled = true;

        public static float incidentWeightFactor = 1f;

        public static float minThreatPoints = 300f;

        public static int maxConcurrentShipsPerMap = 1;

        public static float casualtyRetreatThreshold = 0.5f;

        public static int minColonistCount = 1;

        public static bool enableRaidshipEffects = true;

        public static bool enableMinPlayerTechLevel = false;

        public static TechLevel minPlayerTechLevel = TechLevel.Industrial;

        public static TechLevel minEnemyFactionTechLevel = TechLevel.Spacer;

        public static bool hardcoreEnemyDepartureDestroysUnguardedMaps = false;

        public static bool enableGravshipGuards = false;

        public static float gravshipGuardFraction = 0.1f;

        public static bool debugLogging = false;

        public static bool globalFactionFilterEnabled = false;

        public static List<string> globalDisallowedFactionDefNames = new List<string>();

        public static string minThreatPointsBuffer;

        public static string minColonistCountBuffer;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref incidentWeightFactor, "incidentWeightFactor", 1f);
            Scribe_Values.Look(ref minThreatPoints, "minThreatPoints", 300f);
            Scribe_Values.Look(ref maxConcurrentShipsPerMap, "maxConcurrentShipsPerMap", 1);
            Scribe_Values.Look(ref casualtyRetreatThreshold, "casualtyRetreatThreshold", 0.5f);
            Scribe_Values.Look(ref minColonistCount, "minColonistCount", 1);
            Scribe_Values.Look(ref enableRaidshipEffects, "enableRaidshipEffects", true);
            Scribe_Values.Look(ref enableMinPlayerTechLevel, "enableMinPlayerTechLevel", false);
            Scribe_Values.Look(ref minPlayerTechLevel, "minPlayerTechLevel", TechLevel.Industrial);
            Scribe_Values.Look(ref minEnemyFactionTechLevel, "minEnemyFactionTechLevel", TechLevel.Spacer);
            Scribe_Values.Look(
                ref hardcoreEnemyDepartureDestroysUnguardedMaps,
                "hardcoreEnemyDepartureDestroysUnguardedMaps",
                false);
            Scribe_Values.Look(ref enableGravshipGuards, "enableGravshipGuards", false);
            Scribe_Values.Look(ref gravshipGuardFraction, "gravshipGuardFraction", 0.1f);
            Scribe_Values.Look(ref debugLogging, "debugLogging", false);
            Scribe_Values.Look(ref globalFactionFilterEnabled, "globalFactionFilterEnabled", false);
            Scribe_Collections.Look(ref globalDisallowedFactionDefNames, "globalDisallowedFactionDefNames", LookMode.Value);
            if (globalDisallowedFactionDefNames == null)
            {
                globalDisallowedFactionDefNames = new List<string>();
            }
        }

        public static float ClampedGravshipGuardFraction()
        {
            return Mathf.Clamp(gravshipGuardFraction, 0f, MaxGravshipGuardFraction);
        }

        public static void PruneInvalidGlobalFactionEntries()
        {
            int removed = globalDisallowedFactionDefNames.RemoveAll(defName => DefDatabase<FactionDef>.GetNamedSilentFail(defName) == null);
            if (removed > 0)
            {
                Logger.Message($"GravshipRaidsSettings.PruneInvalidGlobalFactionEntries: removed {removed} stale entry(ies) from globalDisallowedFactionDefNames (no matching FactionDef found).");
            }
        }

        public static bool AllowsFactionGlobally(FactionDef factionDef)
        {
            if (!globalFactionFilterEnabled || factionDef == null)
            {
                return true;
            }
            return !globalDisallowedFactionDefNames.Contains(factionDef.defName);
        }
    }
}
