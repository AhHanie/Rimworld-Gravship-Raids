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
