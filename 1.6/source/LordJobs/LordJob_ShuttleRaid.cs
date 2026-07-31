using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace Gravship_Raids
{
    public class LordJob_ShuttleRaid : LordJob
    {
        public override bool AddFleeToil => false;

        private static readonly IntRange AssaultTimeBeforeGiveUp = new IntRange(26000, 38000);

        private const int MinimumCasualtiesForFractionRetreat = 2;

        private EnemyShuttleRaidInstance instance;

        public EnemyShuttleRaidInstance Instance => instance;

        private Faction assaulterFaction;

        private bool canTimeoutOrFlee = true;

        public override bool GuiltyOnDowned => true;

        public LordJob_ShuttleRaid()
        {
        }

        public LordJob_ShuttleRaid(EnemyShuttleRaidInstance instance, Faction assaulterFaction, bool canTimeoutOrFlee = true)
        {
            this.instance = instance;
            this.assaulterFaction = assaulterFaction;
            this.canTimeoutOrFlee = canTimeoutOrFlee;
        }

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();

            LordToil_ShuttleAssault assaultToil = new LordToil_ShuttleAssault(instance);
            stateGraph.AddToil(assaultToil);
            stateGraph.StartingToil = assaultToil;

            LordToil_BoardEnemyShuttle boardToil = new LordToil_BoardEnemyShuttle(instance);
            stateGraph.AddToil(boardToil);

            Transition boardingTransition = new Transition(assaultToil, boardToil);
            boardingTransition.AddTrigger(new Trigger_ShuttleRaidBoarding(instance));
            stateGraph.AddTransition(boardingTransition);

            if (assaulterFaction != null && assaulterFaction.def.humanlikeFaction)
            {
                if (canTimeoutOrFlee)
                {
                    Transition timeoutTransition = new Transition(assaultToil, boardToil);
                    Trigger_TicksPassed ticksTrigger = new Trigger_TicksPassed(AssaultTimeBeforeGiveUp.RandomInRange);
                    ticksTrigger.WithFilter(new TriggerFilter_MapExitable());
                    timeoutTransition.AddTrigger(ticksTrigger);
                    timeoutTransition.AddPreAction(new TransitionAction_Message("GravshipRaids.MessageShuttleRaidersGivenUpRetreating".Translate(assaulterFaction.def.pawnsPlural.CapitalizeFirst(), assaulterFaction.Name)));
                    stateGraph.AddTransition(timeoutTransition);

                    Transition damageTransition = new Transition(assaultToil, boardToil);
                    Trigger_FractionColonyDamageTaken damageTrigger = new Trigger_FractionColonyDamageTaken(new FloatRange(0.25f, 0.35f).RandomInRange, 900f);
                    damageTrigger.WithFilter(new TriggerFilter_MapExitable());
                    damageTransition.AddTrigger(damageTrigger);
                    damageTransition.AddPreAction(new TransitionAction_Message("GravshipRaids.MessageShuttleRaidersSatisfiedRetreating".Translate(assaulterFaction.def.pawnsPlural.CapitalizeFirst(), assaulterFaction.Name)));
                    stateGraph.AddTransition(damageTransition);
                }

                Transition casualtyTransition = new Transition(assaultToil, boardToil);
                float casualtyThreshold = Mathf.Clamp(GravshipRaidsSettings.shuttleCasualtyRetreatThreshold, 0.05f, 1f);
                casualtyTransition.AddTrigger(new Trigger_FractionPawnsLostWithMinimum(casualtyThreshold, MinimumCasualtiesForFractionRetreat));
                casualtyTransition.AddPreAction(new TransitionAction_Message("GravshipRaids.MessageShuttleRaidersRetreatingCasualties".Translate(assaulterFaction.def.pawnsPlural.CapitalizeFirst(), assaulterFaction.Name)));
                stateGraph.AddTransition(casualtyTransition);

                Transition nonHostileTransition = new Transition(assaultToil, boardToil);
                nonHostileTransition.AddTrigger(new Trigger_BecameNonHostileToPlayer());
                nonHostileTransition.AddPreAction(new TransitionAction_Message("GravshipRaids.MessageShuttleRaidersRetreatingNonHostile".Translate(assaulterFaction.def.pawnsPlural.CapitalizeFirst(), assaulterFaction.Name)));
                stateGraph.AddTransition(nonHostileTransition);

                Logger.Message($"LordJob_ShuttleRaid.CreateGraph: {instance} - wired retreat transitions for faction '{assaulterFaction.def.defName}' (canTimeoutOrFlee={canTimeoutOrFlee}, casualtyThreshold={Mathf.Clamp(GravshipRaidsSettings.shuttleCasualtyRetreatThreshold, 0.05f, 1f)}, minimumCasualties={MinimumCasualtiesForFractionRetreat}).");
            }
            else
            {
                Logger.Warning($"LordJob_ShuttleRaid.CreateGraph: {instance} - assaulterFaction is '{assaulterFaction?.def?.defName ?? "null"}' (humanlike={assaulterFaction?.def?.humanlikeFaction}); no retreat transitions were added, only the shared boarding-follow transition exists.");
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
