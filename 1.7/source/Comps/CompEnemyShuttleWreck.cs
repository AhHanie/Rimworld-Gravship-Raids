using RimWorld;
using Verse;

namespace Gravship_Raids
{
    public class CompEnemyShuttleWreck : ThingComp
    {
        private const int OccupantRetryIntervalTicks = 250;

        public CompProperties_EnemyShuttleWreck Props => (CompProperties_EnemyShuttleWreck)props;

        private bool abandoned;

        private bool HasOccupants
        {
            get
            {
                CompTransporter transporter = parent.TryGetComp<CompTransporter>();
                return transporter?.innerContainer != null && transporter.innerContainer.Any;
            }
        }

        private bool ReadyForRecovery => abandoned && !HasOccupants;

        public void MarkAbandoned()
        {
            if (abandoned)
            {
                return;
            }
            abandoned = true;
            Logger.Message($"CompEnemyShuttleWreck.MarkAbandoned: {parent} marked as a wreck; recoverable once empty (occupied: {HasOccupants}).");
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!abandoned || !parent.Spawned || !parent.IsHashIntervalTick(OccupantRetryIntervalTicks))
            {
                return;
            }
            CompTransporter transporter = parent.TryGetComp<CompTransporter>();
            if (transporter?.innerContainer == null || !transporter.innerContainer.Any)
            {
                return;
            }
            // The vanilla Unload ship job keeps retrying too; this covers the cases where it was never queued or was dropped.
            transporter.innerContainer.TryDropAll(parent.InteractionCell, parent.Map, ThingPlaceMode.Near);
            Logger.Message($"CompEnemyShuttleWreck.CompTick: retried unloading {parent}; {transporter.innerContainer.Count} thing(s) still aboard.");
        }

        public override bool CompPreventClaimingBy(Faction faction)
        {
            return !ReadyForRecovery;
        }

        public override bool CompForceDeconstructable()
        {
            return ReadyForRecovery;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref abandoned, "abandoned", false);
        }
    }

    public class CompProperties_EnemyShuttleWreck : CompProperties
    {
        public CompProperties_EnemyShuttleWreck()
        {
            compClass = typeof(CompEnemyShuttleWreck);
        }
    }
}
