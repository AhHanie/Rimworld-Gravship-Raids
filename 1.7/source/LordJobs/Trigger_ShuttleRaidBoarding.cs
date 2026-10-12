using Verse.AI.Group;

namespace Gravship_Raids
{
    public class Trigger_ShuttleRaidBoarding : Trigger
    {
        private readonly EnemyShuttleRaidInstance instance;

        public Trigger_ShuttleRaidBoarding(EnemyShuttleRaidInstance instance)
        {
            this.instance = instance;
        }

        public override bool ActivateOn(Lord lord, TriggerSignal signal)
        {
            if (signal.type != TriggerSignalType.Tick)
            {
                return false;
            }
            return instance != null && instance.state == ShuttleRaidState.Boarding;
        }
    }
}
