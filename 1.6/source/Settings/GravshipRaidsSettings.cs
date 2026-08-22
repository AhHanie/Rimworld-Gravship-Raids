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

        public static bool allowEnemyGravcoreDrops = false;

        public static bool debugLogging = false;

        public static bool globalFactionFilterEnabled = false;

        public static List<string> globalDisallowedFactionDefNames = new List<string>();

        public static string minThreatPointsBuffer;

        public static string minColonistCountBuffer;

        public static bool enableShuttleRaids = true;

        public static float shuttleIncidentWeightFactor = 1f;

        public static float shuttleMinThreatPoints = 300f;

        public static int shuttleMaxConcurrentPerMap = 1;

        public static float shuttleCasualtyRetreatThreshold = 0.5f;

        public static int shuttleMinColonistCount = 1;

        public static bool shuttleEnableMinPlayerTechLevel = false;

        public static TechLevel shuttleMinPlayerTechLevel = TechLevel.Industrial;

        public static TechLevel shuttleMinEnemyFactionTechLevel = TechLevel.Industrial;

        public static bool shuttleGlobalFactionFilterEnabled = false;

        public static List<string> shuttleDisallowedFactionDefNames = new List<string>();

        public static string shuttleMinThreatPointsBuffer;

        public static string shuttleMinColonistCountBuffer;

        public static bool enableWinstonWavesCompatibility = true;

        public static float winstonWavesGravshipChance = 0.20f;

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
            Scribe_Values.Look(ref allowEnemyGravcoreDrops, "allowEnemyGravcoreDrops", false);
            Scribe_Values.Look(ref debugLogging, "debugLogging", false);
            Scribe_Values.Look(ref globalFactionFilterEnabled, "globalFactionFilterEnabled", false);
            Scribe_Collections.Look(ref globalDisallowedFactionDefNames, "globalDisallowedFactionDefNames", LookMode.Value);
            if (globalDisallowedFactionDefNames == null)
            {
                globalDisallowedFactionDefNames = new List<string>();
            }

            Scribe_Values.Look(ref enableShuttleRaids, "enableShuttleRaids", true);
            Scribe_Values.Look(ref shuttleIncidentWeightFactor, "shuttleIncidentWeightFactor", 1f);
            Scribe_Values.Look(ref shuttleMinThreatPoints, "shuttleMinThreatPoints", 300f);
            Scribe_Values.Look(ref shuttleMaxConcurrentPerMap, "shuttleMaxConcurrentPerMap", 1);
            Scribe_Values.Look(ref shuttleCasualtyRetreatThreshold, "shuttleCasualtyRetreatThreshold", 0.5f);
            Scribe_Values.Look(ref shuttleMinColonistCount, "shuttleMinColonistCount", 1);
            Scribe_Values.Look(ref shuttleEnableMinPlayerTechLevel, "shuttleEnableMinPlayerTechLevel", false);
            Scribe_Values.Look(ref shuttleMinPlayerTechLevel, "shuttleMinPlayerTechLevel", TechLevel.Industrial);
            Scribe_Values.Look(ref shuttleMinEnemyFactionTechLevel, "shuttleMinEnemyFactionTechLevel", TechLevel.Industrial);
            Scribe_Values.Look(ref shuttleGlobalFactionFilterEnabled, "shuttleGlobalFactionFilterEnabled", false);
            Scribe_Collections.Look(ref shuttleDisallowedFactionDefNames, "shuttleDisallowedFactionDefNames", LookMode.Value);
            if (shuttleDisallowedFactionDefNames == null)
            {
                shuttleDisallowedFactionDefNames = new List<string>();
            }

            Scribe_Values.Look(ref enableWinstonWavesCompatibility, "enableWinstonWavesCompatibility", true);
            Scribe_Values.Look(ref winstonWavesGravshipChance, "winstonWavesGravshipChance", 0.20f);
        }

        public static float ClampedGravshipGuardFraction()
        {
            return Mathf.Clamp(gravshipGuardFraction, 0f, MaxGravshipGuardFraction);
        }

        public static float ClampedWinstonWavesGravshipChance()
        {
            return Mathf.Clamp01(winstonWavesGravshipChance);
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

        public static void PruneInvalidShuttleFactionEntries()
        {
            int removed = shuttleDisallowedFactionDefNames.RemoveAll(defName => DefDatabase<FactionDef>.GetNamedSilentFail(defName) == null);
            if (removed > 0)
            {
                Logger.Message($"GravshipRaidsSettings.PruneInvalidShuttleFactionEntries: removed {removed} stale entry(ies) from shuttleDisallowedFactionDefNames (no matching FactionDef found).");
            }
        }

        public static bool AllowsShuttleFactionGlobally(FactionDef factionDef)
        {
            if (!shuttleGlobalFactionFilterEnabled || factionDef == null)
            {
                return true;
            }
            return !shuttleDisallowedFactionDefNames.Contains(factionDef.defName);
        }
    }
}
