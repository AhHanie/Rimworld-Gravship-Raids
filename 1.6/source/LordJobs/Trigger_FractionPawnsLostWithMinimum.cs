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
            bool meetsMinimum = lord.numPawnsLostViolently >= minimumCasualties;
            bool meetsFraction = (float)lord.numPawnsLostViolently >= (float)lord.numPawnsEverGained * fraction;
            bool result = meetsMinimum && meetsFraction;
            Logger.Message($"Trigger_FractionPawnsLostWithMinimum.ActivateOn: lord={lord} numPawnsLostViolently={lord.numPawnsLostViolently} numPawnsEverGained={lord.numPawnsEverGained} fraction={fraction} minimumCasualties={minimumCasualties} meetsMinimum={meetsMinimum} meetsFraction={meetsFraction} result={result}.");
            return result;
        }
    }
}
