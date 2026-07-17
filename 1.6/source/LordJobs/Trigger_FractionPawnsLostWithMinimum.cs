using UnityEngine;
using Verse.AI.Group;

namespace Gravship_Raids
{
    public class Trigger_FractionPawnsLostWithMinimum : Trigger
    {
        private readonly float fraction;

        private readonly int minimumCasualties;

        public Trigger_FractionPawnsLostWithMinimum(float fraction, int minimumCasualties)
        {
            this.fraction = fraction;
            this.minimumCasualties = Mathf.Max(1, minimumCasualties);
        }

        public override bool ActivateOn(Lord lord, TriggerSignal signal)
        {
            if (signal.type != TriggerSignalType.PawnLost)
            {
                return false;
            }
            if (lord.numPawnsLostViolently < minimumCasualties)
            {
                return false;
            }
            return (float)lord.numPawnsLostViolently >= (float)lord.numPawnsEverGained * fraction;
        }
    }
}
