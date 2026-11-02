using System.Linq;
using RimWorld;
using Verse;

namespace Gravship_Raids
{
    public static class EnemyShuttleRaidUtility
    {
        public static bool HasLivingCrewBoarded(EnemyShuttleRaidInstance instance, Thing shuttle)
        {
            if (instance?.crew == null || shuttle == null)
            {
                return false;
            }
            CompTransporter transporter = shuttle.TryGetComp<CompTransporter>();
            if (transporter?.innerContainer == null)
            {
                return false;
            }
            foreach (Thing t in transporter.innerContainer)
            {
                if (t is Pawn pawn && !pawn.Dead && instance.crew.Contains(pawn))
                {
                    return true;
                }
            }
            return false;
        }

        public static void AbandonInstance(EnemyShuttleRaidInstance instance, string reason)
        {
            if (instance == null || instance.state == ShuttleRaidState.Departed || instance.state == ShuttleRaidState.Lost)
            {
                return;
            }

            string cleanupOutcome = "no spawned shuttle to clean up";
            string wreckOutcome = "no wreck component";
            if (instance.ShuttleAvailable)
            {
                CompTransporter transporter = instance.shuttle.TryGetComp<CompTransporter>();
                cleanupOutcome = (transporter != null && transporter.CancelLoad()) ? "cancelled active loading" : "no active loading to cancel";

                CompEnemyShuttleWreck wreck = instance.shuttle.TryGetComp<CompEnemyShuttleWreck>();
                if (wreck != null)
                {
                    wreck.MarkAbandoned();
                    wreckOutcome = "marked abandoned";
                }
            }

            instance.state = ShuttleRaidState.Lost;
            instance.departureTick = -1;
            Logger.Message($"EnemyShuttleRaidUtility.AbandonInstance: {instance} abandoned ({reason}); shuttle cleanup: {cleanupOutcome}; wreck: {wreckOutcome}.");
        }

        public static bool IsShuttleIncoming(Thing shuttle)
        {
            if (shuttle == null || shuttle.Destroyed)
            {
                return false;
            }
            return shuttle.Spawned || (shuttle.ParentHolder is Skyfaller skyfaller && skyfaller.Spawned);
        }

        public static void RecoverUnlandedShuttle(EnemyShuttleRaidInstance instance, Map map, IntVec3 near, string reason)
        {
            if (instance == null || instance.state == ShuttleRaidState.Departed || instance.state == ShuttleRaidState.Lost)
            {
                return;
            }

            Thing shuttle = instance.shuttle;
            int pawnsSpawned = 0;
            int pawnsKept = 0;
            int itemsPlaced = 0;
            CompTransporter transporter = shuttle?.TryGetComp<CompTransporter>();
            if (transporter?.innerContainer != null && map != null)
            {
                foreach (Thing thing in transporter.innerContainer.ToList())
                {
                    if (thing is Pawn pawn)
                    {
                        if (ShuttleRaidSpawnUtility.TrySpawnOnSafeLand(pawn, map, near, Rot4.Random))
                        {
                            pawnsSpawned++;
                        }
                        else
                        {
                            pawnsKept++;
                        }
                    }
                    else
                    {
                        transporter.innerContainer.Remove(thing);
                        if (GenPlace.TryPlaceThing(thing, near, map, ThingPlaceMode.Near))
                        {
                            itemsPlaced++;
                        }
                        else
                        {
                            thing.Destroy();
                        }
                    }
                }
            }

            TransportShip ship = shuttle?.TryGetComp<CompShuttle>()?.shipParent;
            if (ship != null)
            {
                Find.TransportShipManager.DeregisterShipObject(ship);
            }
            if (shuttle != null && !shuttle.Spawned && !shuttle.Destroyed && !(shuttle.ParentHolder is Skyfaller))
            {
                shuttle.Destroy(DestroyMode.QuestLogic);
            }

            Logger.Warning($"EnemyShuttleRaidUtility.RecoverUnlandedShuttle: {instance} failed to land ({reason}). Planned site {instance.landingCell} (terrain {(instance.landingCell.IsValid && map != null ? ShuttleRaidTemplateUtility.DescribeTerrain(instance.landingCell, map) : "unknown")}); {pawnsSpawned} crew spawned on safe land, {pawnsKept} kept as world pawns, {itemsPlaced} other item(s) placed.");

            instance.shuttleLost = true;
            instance.shuttle = null;
            instance.state = ShuttleRaidState.Lost;
            instance.departureTick = -1;
        }

        public static void AbortIncomingShip(TransportShip ship, Thing shuttle)
        {
            if (ship != null)
            {
                Find.TransportShipManager.DeregisterShipObject(ship);
            }
            if (shuttle == null || shuttle.Destroyed || shuttle.Spawned)
            {
                return;
            }
            if (shuttle.ParentHolder is Skyfaller skyfaller)
            {
                skyfaller.Destroy();
            }
            else
            {
                shuttle.Destroy(DestroyMode.QuestLogic);
            }
        }

        public static bool TryDepart(EnemyShuttleRaidInstance instance)
        {
            if (instance == null || instance.state != ShuttleRaidState.Boarding)
            {
                return false;
            }
            if (!instance.ShuttleAvailable)
            {
                AbandonInstance(instance, "shuttle unavailable at departure");
                return false;
            }
            if (!HasLivingCrewBoarded(instance, instance.shuttle))
            {
                Logger.Message($"EnemyShuttleRaidUtility.TryDepart: {instance} has no living crew aboard its shuttle; abandoning instead of departing.");
                AbandonInstance(instance, "no living crew boarded");
                return false;
            }

            CompShuttle shuttleComp = instance.shuttle.TryGetComp<CompShuttle>();
            TransportShip ship = shuttleComp?.shipParent;
            if (ship == null)
            {
                Logger.Error($"EnemyShuttleRaidUtility.TryDepart: {instance}'s shuttle has no CompShuttle.shipParent set; cannot fly away.");
                AbandonInstance(instance, "shuttle had no TransportShip to depart with");
                return false;
            }

            instance.state = ShuttleRaidState.Departing;
            Logger.Message($"EnemyShuttleRaidUtility.TryDepart: {instance} beginning departure.");
            ship.ForceJob(ShipJobDefOf.FlyAway);
            FinalizeDeparture(instance);
            return true;
        }

        public static void FinalizeDeparture(EnemyShuttleRaidInstance instance)
        {
            if (instance == null || instance.state == ShuttleRaidState.Departed)
            {
                return;
            }
            instance.state = ShuttleRaidState.Departed;
            instance.shuttle = null;
            Logger.Message($"EnemyShuttleRaidUtility.FinalizeDeparture: {instance} finished its departure.");
        }
    }
}
