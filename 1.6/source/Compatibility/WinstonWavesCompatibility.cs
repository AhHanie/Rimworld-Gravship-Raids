using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Gravship_Raids
{
    // Lets Winston Waves (VanillaStorytellersExpanded.WinstonWave) select and execute the existing gravship
    // raid flow as one of its scheduled waves, without a compile-time reference to Winston's assembly.
    //
    // Winston never runs GR_GravshipRaid's IncidentWorker: VSEWW.NextRaidInfo builds its own IncidentParms,
    // privately picks a RaidStrategyDef, generates pawns itself, and later resolves arrival and lords on its
    // own. GR_GravshipAssault has a deliberately zero vanilla selection weight and is never in Winston's own
    // strategy pools, so the only way to get a gravship raid out of Winston is to force parms.raidStrategy
    // before Winston generates pawns, and then take over the one arrival branch that matters at dispatch time.
    //
    // Both private VSEWW.NextRaidInfo members below are resolved by reflection and only patched if found, so
    // a missing/incompatible Winston Waves version simply leaves this compatibility inactive instead of
    // throwing during mod load.
    internal static class WinstonWavesCompatibility
    {
        private const string WinstonStorytellerDefName = "VSE_WinstonWave";

        private static FieldInfo parmsField;
        private static FieldInfo raidPawnsField;

        internal static void TryInstall(Harmony harmony)
        {
            Type nextRaidInfoType = AccessTools.TypeByName("VSEWW.NextRaidInfo");
            if (nextRaidInfoType == null)
            {
                Log.Warning("[Gravship Raids] Winston Waves compatibility: could not find type 'VSEWW.NextRaidInfo'; compatibility will remain inactive. Winston Waves may be missing, disabled, or on an incompatible version.");
                return;
            }

            MethodInfo chooseRandomStrategyDefMethod = AccessTools.Method(nextRaidInfoType, "ChooseRandomStrategyDef");
            MethodInfo resolveRaidArrivalMethod = AccessTools.Method(nextRaidInfoType, "ResolveRaidArrival");
            parmsField = AccessTools.Field(nextRaidInfoType, "parms");
            raidPawnsField = AccessTools.Field(nextRaidInfoType, "raidPawns");

            if (chooseRandomStrategyDefMethod == null || resolveRaidArrivalMethod == null || parmsField == null || raidPawnsField == null)
            {
                Log.Warning("[Gravship Raids] Winston Waves compatibility: could not resolve required members (ChooseRandomStrategyDef/ResolveRaidArrival/parms/raidPawns) on 'VSEWW.NextRaidInfo'; compatibility will remain inactive. This likely means an incompatible Winston Waves version is installed.");
                return;
            }

            try
            {
                harmony.Patch(chooseRandomStrategyDefMethod, prefix: new HarmonyMethod(typeof(WinstonWavesCompatibility), nameof(ChooseRandomStrategyDefPrefix)));
                harmony.Patch(resolveRaidArrivalMethod, prefix: new HarmonyMethod(typeof(WinstonWavesCompatibility), nameof(ResolveRaidArrivalPrefix)));
            }
            catch (Exception ex)
            {
                Log.Warning($"[Gravship Raids] Winston Waves compatibility: failed to apply Harmony patches to 'VSEWW.NextRaidInfo'. {ex}");
                return;
            }

            Logger.Message("Winston Waves compatibility installed.");
        }

        /// <summary>
        /// Prefix for VSEWW.NextRaidInfo.ChooseRandomStrategyDef. Forces the gravship raid strategy onto an
        /// otherwise-eligible Winston wave before Winston generates pawns/loot for it, so every downstream
        /// Winston system (UI, rewards, kill counter, reinforcements, SendRaid) keeps working off one
        /// consistent wave object. Returns true to let Winston's own selector run unless every condition holds.
        /// </summary>
        private static bool ChooseRandomStrategyDefPrefix(object __instance)
        {
            try
            {
                if (!ShouldForceGravshipRaid(__instance, out IncidentParms parms))
                {
                    return true;
                }

                parms.raidStrategy = GravshipRaidsDefOf.GR_GravshipAssault;
                Logger.Message("WinstonWavesCompatibility.ChooseRandomStrategyDefPrefix: selected GR_GravshipAssault for this Winston wave.");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "WinstonWavesCompatibility.ChooseRandomStrategyDefPrefix");
                return true;
            }
        }

        private static bool ShouldForceGravshipRaid(object instance, out IncidentParms parms)
        {
            parms = null;

            if (Find.Storyteller?.def?.defName != WinstonStorytellerDefName)
            {
                return false;
            }

            if (!GravshipRaidsSettings.enableWinstonWavesCompatibility)
            {
                return false;
            }

            if (!(parmsField.GetValue(instance) is IncidentParms candidateParms))
            {
                return false;
            }

            // Cheap eligibility gate first, so an ineligible wave never consumes the chance roll below - this
            // keeps compatibility's randomness independent of how many waves Winston happens to generate.
            if (!GravshipRaidEligibility.CanUseGravshipRaid(candidateParms, out string reason))
            {
                Logger.Message($"WinstonWavesCompatibility.ShouldForceGravshipRaid: declining for this Winston wave - {reason}.");
                return false;
            }

            if (!Rand.Chance(GravshipRaidsSettings.ClampedWinstonWavesGravshipChance()))
            {
                return false;
            }

            parms = candidateParms;
            return true;
        }

        /// <summary>
        /// Prefix for VSEWW.NextRaidInfo.ResolveRaidArrival. Only acts on waves this compatibility already
        /// selected as a gravship raid. Rechecks eligibility at dispatch time (the map can change in the days
        /// between a Winston wave being queued and it actually arriving) and atomically falls back to a normal
        /// Winston arrival - for the same already-generated pawns - if anything about the landing is no longer
        /// viable.
        /// </summary>
        private static bool ResolveRaidArrivalPrefix(object __instance)
        {
            IncidentParms parms = parmsField.GetValue(__instance) as IncidentParms;
            if (parms == null || parms.raidStrategy != GravshipRaidsDefOf.GR_GravshipAssault)
            {
                return true;
            }

            try
            {
                if (GravshipRaidEligibility.CanUseGravshipRaid(parms, out string reason)
                    && GravshipRaidsDefOf.GR_GravshipLanding.Worker.TryResolveRaidSpawnCenter(parms))
                {
                    List<Pawn> raidPawns = raidPawnsField.GetValue(__instance) as List<Pawn>;
                    if (raidPawns == null)
                    {
                        Logger.Warning("WinstonWavesCompatibility.ResolveRaidArrivalPrefix: 'raidPawns' field was null/unexpected type; falling back to a normal Winston arrival.");
                        FallBackToOrdinaryArrival(parms);
                        return true;
                    }

                    // Winston's SendRaid reads parms.raidArrivalMode right after this prefix runs (its
                    // GetLetterText formats parms.raidArrivalMode.textEnemy) - TryResolveRaidSpawnCenter above
                    // never sets it, so without this line it stays null and SendRaid throws before ever
                    // reaching MakeLords, silently leaving the just-landed crew without a Lord and the wave
                    // stuck with sent=false (Winston then keeps retrying to send it).
                    parms.raidArrivalMode = GravshipRaidsDefOf.GR_GravshipLanding;
                    GravshipRaidsDefOf.GR_GravshipLanding.Worker.Arrive(raidPawns, parms);
                    Logger.Message("WinstonWavesCompatibility.ResolveRaidArrivalPrefix: dispatched the gravship landing for this Winston wave.");
                    return false;
                }

                Logger.Message($"WinstonWavesCompatibility.ResolveRaidArrivalPrefix: gravship landing no longer viable at dispatch time ({reason ?? "no landing site available"}); falling back to a normal Winston arrival.");
                FallBackToOrdinaryArrival(parms);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "WinstonWavesCompatibility.ResolveRaidArrivalPrefix");
                FallBackToOrdinaryArrival(parms);
                return true;
            }
        }

        private static void FallBackToOrdinaryArrival(IncidentParms parms)
        {
            parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
            parms.raidArrivalMode = null;
        }
    }
}
