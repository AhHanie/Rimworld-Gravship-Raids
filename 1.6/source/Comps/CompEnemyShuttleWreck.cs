using RimWorld;
using Verse;

namespace Gravship_Raids
{
    public class CompEnemyShuttleWreck : ThingComp
    {
        public CompProperties_EnemyShuttleWreck Props => (CompProperties_EnemyShuttleWreck)props;

        private bool abandoned;

        public void MarkAbandoned()
        {
            if (abandoned)
            {
                return;
            }
            abandoned = true;
            Logger.Message($"CompEnemyShuttleWreck.MarkAbandoned: {parent} marked as a recoverable wreck.");
        }

        public override bool CompPreventClaimingBy(Faction faction)
        {
            return !abandoned;
        }

        public override bool CompForceDeconstructable()
        {
            return abandoned;
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
