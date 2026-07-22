using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Gravship_Raids
{
    // The installed AlienRace.Comp_OutfitStandHAR.PostSpawnSetup unconditionally re-derives its race from
    // faction.def.basicMemberKind.race on every non-respawn spawn, with no null check on basicMemberKind:
    //
    //     public override void PostSpawnSetup(bool respawningAfterLoad)
    //     {
    //         base.PostSpawnSetup(respawningAfterLoad); // ThingComp.PostSpawnSetup is an empty no-op.
    //         if (!respawningAfterLoad)
    //         {
    //             Race = (parent.Faction != null) ? (parent.Faction.def.basicMemberKind.race ?? ThingDefOf.Human) : ThingDefOf.Human;
    //         }
    //     }
    //
    // A raid faction whose FactionDef has no basicMemberKind makes the ".race" access throw, aborting
    // PrefabUtility.SpawnPrefab mid-spawn for any prefab containing a Building_OutfitStand. There is no
    // race==null guard to pre-empt by priming the field first, so the only way to stop the throw without a
    // transpiler is to skip HAR's own method body for the affected stand and perform the equivalent
    // ThingDefOf.Human fallback ourselves through its own public Race setter (which still runs HAR's normal
    // body/head/gender/graphics initialization). Since the base call is a confirmed no-op, skipping the whole
    // method in that narrow case loses nothing. This patch only touches HAR's initialization path while this
    // mod is actively spawning one of its own prefabs, and only for stands whose faction actually has no
    // basicMemberKind - every other case (factionless stands, factions with a normal basicMemberKind) falls
    // through to HAR's original method unmodified.
    //
    // No compile-time reference to AlienRace.dll: the target method is resolved dynamically via
    // TargetMethod, and Prepare gates the whole patch on HAR actually being active.
    [HarmonyPatch]
    internal static class HAROutfitStandCompatibility
    {
        private const string HarPackageId = "erdelf.HumanoidAlienRaces";

        private static PropertyInfo raceProperty;

        private static int raidPrefabSpawnDepth;

        private static readonly HashSet<FactionDef> LoggedFallbackFactions = new HashSet<FactionDef>();

        private static bool IsSpawningRaidPrefab => raidPrefabSpawnDepth > 0;

        [HarmonyPrepare]
        private static bool Prepare()
        {
            if (!ModsConfig.IsActive(HarPackageId))
            {
                return false;
            }

            Type compType = AccessTools.TypeByName("AlienRace.Comp_OutfitStandHAR");
            PropertyInfo raceProp = (compType != null) ? AccessTools.Property(compType, "Race") : null;

            if (raceProp == null || raceProp.GetSetMethod(nonPublic: true) == null)
            {
                Log.Warning("[Gravship Raids] HAR compatibility: HAR is active but AlienRace.Comp_OutfitStandHAR.Race could not be resolved by reflection; leaving the outfit-stand fix unpatched for this HAR version.");
                return false;
            }

            raceProperty = raceProp;
            Logger.Message("HAR compatibility: patching AlienRace.Comp_OutfitStandHAR.PostSpawnSetup to fall back to ThingDefOf.Human when a raid faction has no basicMemberKind.");
            return true;
        }

        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            Type compType = AccessTools.TypeByName("AlienRace.Comp_OutfitStandHAR");
            return AccessTools.Method(compType, "PostSpawnSetup", new[] { typeof(bool) });
        }

        // Returning true lets HAR's original method run unmodified. Returning false skips it entirely -
        // only reached once we have already performed the equivalent Race assignment ourselves.
        [HarmonyPrefix]
        private static bool Prefix(object __instance, bool respawningAfterLoad)
        {
            if (!IsSpawningRaidPrefab || respawningAfterLoad)
            {
                return true;
            }

            if (!(__instance is ThingComp comp) || !(comp.parent is Building_OutfitStand stand))
            {
                return true;
            }

            Faction faction = stand.Faction;
            if (faction?.def == null || faction.def.basicMemberKind != null)
            {
                // Factionless stands and factions with a normal basicMemberKind are already handled correctly by HAR.
                return true;
            }

            try
            {
                raceProperty.SetValue(__instance, ThingDefOf.Human, null);
                LogFallbackOnce(faction.def, stand);
            }
            catch (Exception ex)
            {
                Log.Warning($"[Gravship Raids] HAR compatibility: failed to set the human fallback race on outfit stand '{stand}' for faction '{faction.def.defName}'; letting HAR's own PostSpawnSetup run, which may throw. {ex}");
                return true;
            }

            return false;
        }

        public static IDisposable BeginRaidPrefabSpawn()
        {
            return new RaidPrefabSpawnScope();
        }

        private static void LogFallbackOnce(FactionDef factionDef, Building_OutfitStand stand)
        {
            if (!LoggedFallbackFactions.Add(factionDef))
            {
                return;
            }
            Log.Warning($"[Gravship Raids] HAR compatibility: faction '{factionDef.defName}' has no basicMemberKind, so outfit stand '{stand}' fell back to a human mannequin instead of the faction's race (HAR would otherwise throw here).");
        }

        private sealed class RaidPrefabSpawnScope : IDisposable
        {
            private bool disposed;

            public RaidPrefabSpawnScope()
            {
                raidPrefabSpawnDepth++;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }
                disposed = true;
                raidPrefabSpawnDepth--;
            }
        }
    }
}
