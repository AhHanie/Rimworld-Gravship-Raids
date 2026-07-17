using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace Gravship_Raids
{
    public class LordJob_GravshipRaid : LordJob
    {
        public override bool AddFleeToil => false;

        private static readonly IntRange AssaultTimeBeforeGiveUp = new IntRange(26000, 38000);

        private const int MinimumCasualtiesForFractionRetreat = 2;

        private EnemyGravshipInstance instance;

        public EnemyGravshipInstance Instance => instance;

        private Faction assaulterFaction;

        private bool canTimeoutOrFlee = true;

        public override bool GuiltyOnDowned => true;

        public LordJob_GravshipRaid()
        {
        }

        public LordJob_GravshipRaid(EnemyGravshipInstance instance, Faction assaulterFaction, bool canTimeoutOrFlee = true)
        {
            this.instance = instance;
            this.assaulterFaction = assaulterFaction;
            this.canTimeoutOrFlee = canTimeoutOrFlee;
        }

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();

            LordToil_AssaultColony assaultToil = new LordToil_AssaultColony(attackDownedIfStarving: false, canPickUpOpportunisticWeapons: false);
            stateGraph.AddToil(assaultToil);
            stateGraph.StartingToil = assaultToil;

            LordToil_BoardEnemyGravship boardToil = new LordToil_BoardEnemyGravship(instance);
            stateGraph.AddToil(boardToil);

            // Matches vanilla LordJob_AssaultColony: every retreat trigger is gated on a real, humanlike
            // assaulter faction. A null/non-humanlike faction (never expected for this mod's raids, which
            // restrict eligibility to humanlike factions) would leave the assault
            // toil with no outgoing transitions at all, matching vanilla's own behavior for the same case.
            if (assaulterFaction != null && assaulterFaction.def.humanlikeFaction)
            {
                if (canTimeoutOrFlee)
                {
                    Transition timeoutTransition = new Transition(assaultToil, boardToil);
                    Trigger_TicksPassed ticksTrigger = new Trigger_TicksPassed(AssaultTimeBeforeGiveUp.RandomInRange);
                    ticksTrigger.WithFilter(new TriggerFilter_MapExitable());
                    timeoutTransition.AddTrigger(ticksTrigger);
                    timeoutTransition.AddPreAction(new TransitionAction_Message("GravshipRaids.MessageRaidersGivenUpRetreating".Translate(assaulterFaction.def.pawnsPlural.CapitalizeFirst(), assaulterFaction.Name)));
                    stateGraph.AddTransition(timeoutTransition);

                    Transition damageTransition = new Transition(assaultToil, boardToil);
                    Trigger_FractionColonyDamageTaken damageTrigger = new Trigger_FractionColonyDamageTaken(new FloatRange(0.25f, 0.35f).RandomInRange, 900f);
                    damageTrigger.WithFilter(new TriggerFilter_MapExitable());
                    damageTransition.AddTrigger(damageTrigger);
                    damageTransition.AddPreAction(new TransitionAction_Message("GravshipRaids.MessageRaidersSatisfiedRetreating".Translate(assaulterFaction.def.pawnsPlural.CapitalizeFirst(), assaulterFaction.Name)));
                    stateGraph.AddTransition(damageTransition);
                }

                Transition casualtyTransition = new Transition(assaultToil, boardToil);
                float casualtyThreshold = Mathf.Clamp(GravshipRaidsSettings.casualtyRetreatThreshold, 0.05f, 1f);
                casualtyTransition.AddTrigger(new Trigger_FractionPawnsLostWithMinimum(casualtyThreshold, MinimumCasualtiesForFractionRetreat));
                casualtyTransition.AddPreAction(new TransitionAction_Message("GravshipRaids.MessageRaidersRetreatingCasualties".Translate(assaulterFaction.def.pawnsPlural.CapitalizeFirst(), assaulterFaction.Name)));
                stateGraph.AddTransition(casualtyTransition);

                Transition nonHostileTransition = new Transition(assaultToil, boardToil);
                nonHostileTransition.AddTrigger(new Trigger_BecameNonHostileToPlayer());
                nonHostileTransition.AddPreAction(new TransitionAction_Message("GravshipRaids.MessageRaidersRetreatingNonHostile".Translate(assaulterFaction.def.pawnsPlural.CapitalizeFirst(), assaulterFaction.Name)));
                stateGraph.AddTransition(nonHostileTransition);
            }

            return stateGraph;
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref assaulterFaction, "assaulterFaction");
            Scribe_References.Look(ref instance, "instance");
            Scribe_Values.Look(ref canTimeoutOrFlee, "canTimeoutOrFlee", true);
        }
    }
}
