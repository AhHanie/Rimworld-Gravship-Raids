using RimWorld;
using Verse;
using Verse.AI.Group;

namespace Gravship_Raids
{
    public class LordToil_ShuttleAssault : LordToil_AssaultColony
    {
        private readonly EnemyShuttleRaidInstance instance;

        public override IntVec3 FlagLoc => (instance?.shuttle != null && instance.shuttle.Spawned) ? instance.shuttle.Position : IntVec3.Invalid;

        public LordToil_ShuttleAssault(EnemyShuttleRaidInstance instance)
            : base(attackDownedIfStarving: false, canPickUpOpportunisticWeapons: false)
        {
            this.instance = instance;
        }
    }
}
