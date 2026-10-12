using Verse.AI.Group;

namespace Gravship_Raids
{
    public class Trigger_GravshipInstanceEvacuating : Trigger
    {
        private readonly EnemyGravshipInstance instance;

        public Trigger_GravshipInstanceEvacuating(EnemyGravshipInstance instance)
        {
            this.instance = instance;
        }

        public override bool ActivateOn(Lord lord, TriggerSignal signal)
        {
            if (signal.type != TriggerSignalType.Tick)
            {
                return false;
            }
            return instance != null && (instance.state == GravshipRaidState.Boarding || instance.state == GravshipRaidState.Destroyed);
        }
    }
}
