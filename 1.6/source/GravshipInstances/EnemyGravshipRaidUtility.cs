using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.Sound;

namespace Gravship_Raids
{
    public static class EnemyGravshipRaidUtility
    {
        public static bool BeginDeparture(EnemyGravshipInstance instance, Map map)
        {
            if (instance == null || map == null)
            {
                return false;
            }
            if (instance.state != GravshipRaidState.Boarding)
            {
                return false;
            }
            if (instance.core == null || !instance.core.Spawned)
            {
                return false;
            }

            instance.state = GravshipRaidState.Launching;
            Logger.Message($"EnemyGravshipRaidUtility.BeginDeparture: {instance} beginning departure sequence.");
            CompleteDeparture(instance, map);
            return true;
        }

        public static void CompleteDeparture(EnemyGravshipInstance instance, Map map)
        {
            try
            {
                Thing core = (instance.core != null && !instance.core.Destroyed) ? instance.core : null;
                bool coreLive = core != null && core.Spawned;
                IntVec3 launchCell = coreLive ? core.Position : instance.root;
                Rot4 launchRot = coreLive ? core.Rotation : instance.rotation;

                List<Thing> boarded = new List<Thing>();
                if (coreLive)
                {
                    CompTransporter transporter = core.TryGetComp<CompTransporter>();
                    if (transporter?.innerContainer != null && transporter.innerContainer.Any)
                    {
                        boarded.AddRange(transporter.innerContainer);
                        transporter.groupID = -1;
                    }
                }

                PlayLaunchEffect(map, launchCell);

                GravshipRaidTemplateUtility.RestoreTerrain(instance.terrainSnapshot, map);
                instance.terrainSnapshot?.Clear();

                List<Thing> hullPieces;
                if (GravshipRaidsSettings.enableRaidshipEffects)
                {
                    hullPieces = CollectAndDespawnHullPieces(instance);
                }
                else
                {
                    RemoveSpawnedThings(instance);
                    hullPieces = new List<Thing>();
                }

                bool risingAway = TransferDepartingShipToLeavingEffect(boarded, hullPieces, instance, map, launchCell, launchRot);
                if (!risingAway)
                {
                    instance.core = null;
                    instance.state = GravshipRaidState.Departed;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"EnemyGravshipRaidUtility.CompleteDeparture: exception while completing departure for {instance}: {ex}");
                instance.core = null;
                instance.state = GravshipRaidState.Departed;
            }
        }

        private static List<Thing> CollectAndDespawnHullPieces(EnemyGravshipInstance instance)
        {
            List<Thing> hullPieces = new List<Thing>();
            if (instance.spawnedThings == null)
            {
                return hullPieces;
            }
            foreach (Thing t in instance.spawnedThings)
            {
                if (t == null || t.Destroyed)
                {
                    continue;
                }
                if (t.Spawned)
                {
                    t.DeSpawn(DestroyMode.Vanish);
                }
                hullPieces.Add(t);
            }
            instance.spawnedThings.Clear();
            return hullPieces;
        }

        private static bool TransferDepartingShipToLeavingEffect(List<Thing> boarded, List<Thing> hullPieces, EnemyGravshipInstance instance, Map map, IntVec3 cell, Rot4 rot)
        {
            if (boarded.Count == 0 && hullPieces.Count == 0)
            {
                return false;
            }
            ThingDef skyfallerDef = GravshipRaidsDefOf.GR_GravshipDepartureSkyfaller;
            if (skyfallerDef == null)
            {
                // Should never happen (DefOf-checked at startup) - never spill boarded pawns or leave hull
                // pieces dangling even if the leaving-effect def is somehow missing.
                Logger.Error("EnemyGravshipRaidUtility.TransferDepartingShipToLeavingEffect: GR_GravshipDepartureSkyfaller is null; falling back to direct pawn removal/hull destruction with no leaving effect.");
                foreach (Thing t in boarded)
                {
                    if (t is Pawn pawn && !pawn.Destroyed && !pawn.IsWorldPawn())
                    {
                        pawn.ExitMap(allowedToJoinOrCreateCaravan: false, Rot4.Invalid);
                    }
                    else if (!(t is Pawn))
                    {
                        t.DestroyOrPassToWorld(DestroyMode.Vanish);
                    }
                }
                foreach (Thing t in hullPieces)
                {
                    if (t != null && !t.Destroyed)
                    {
                        t.Destroy(DestroyMode.Vanish);
                    }
                }
                return false;
            }

            List<Thing> allContents = new List<Thing>(boarded.Count + hullPieces.Count);
            allContents.AddRange(boarded);
            allContents.AddRange(hullPieces);

            GravshipDepartureSkyfaller leaving = (GravshipDepartureSkyfaller)SkyfallerMaker.MakeSkyfaller(skyfallerDef, allContents);
            leaving.instance = instance;
            GenSpawn.Spawn(leaving, cell, map, rot);
            instance.departingSkyfaller = leaving;
            return true;
        }

        private static void RemoveSpawnedThings(EnemyGravshipInstance instance)
        {
            if (instance.spawnedThings == null)
            {
                return;
            }
            List<Thing> things = new List<Thing>(instance.spawnedThings);
            foreach (Thing t in things)
            {
                if (t == null || t.Destroyed)
                {
                    continue;
                }
                t.Destroy(DestroyMode.Vanish);
            }
            instance.spawnedThings.Clear();
        }

        private static void PlayLaunchEffect(Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map))
            {
                return;
            }
            TargetInfo target = new TargetInfo(cell, map);
            SoundDefOf.Gravship_Engine_Start?.PlayOneShot(SoundInfo.InMap(target));
            SoundDefOf.Gravship_Launch?.PlayOneShot(SoundInfo.InMap(target));
            FleckMaker.ThrowDustPuff(cell, map, Rand.Range(2f, 3f));
            FleckMaker.ThrowHeatGlow(cell, map, 4f);
        }

        public static void HandleCoreDestroyed(EnemyGravshipInstance instance)
        {
            if (instance == null)
            {
                return;
            }
            if (instance.state != GravshipRaidState.Landed && instance.state != GravshipRaidState.Boarding)
            {
                // Already mid/post departure (Launching/Departed) or already handled (Destroyed) - this is our
                // own departure cleanup destroying the core, or a duplicate notification, not real combat loss.
                return;
            }

            instance.state = GravshipRaidState.Destroyed;
            instance.core = null;
            instance.departureTick = -1;

            if (instance.faction?.def != null)
            {
                Messages.Message("GravshipRaids.MessageGravshipCoreDestroyed".Translate(instance.faction.def.pawnsPlural.CapitalizeFirst(), instance.faction.Name), MessageTypeDefOf.PositiveEvent);
            }
            Logger.Message($"EnemyGravshipRaidUtility.HandleCoreDestroyed: {instance}'s core was destroyed; retreat canceled, hull left as ruin, surviving crew fall back to map-edge escape.");
        }
    }
}
