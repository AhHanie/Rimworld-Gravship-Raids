using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Gravship_Raids
{
    public class LordToilData_BoardEnemyGravship : LordToilData
    {
        public bool sentBoardingMessage;

        public bool sentStrandedMessage;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref sentBoardingMessage, "sentBoardingMessage", false);
            Scribe_Values.Look(ref sentStrandedMessage, "sentStrandedMessage", false);
        }
    }

    public class LordToil_BoardEnemyGravship : LordToil
    {
        private const float MassCapacityPerCrew = 250f;

        private const int FallbackRetreatTimeoutTicks = 2500;

        private readonly EnemyGravshipInstance instance;

        public override bool AllowSatisfyLongNeeds => false;

        public override bool AllowSelfTend => false;

        private LordToilData_BoardEnemyGravship Data => (LordToilData_BoardEnemyGravship)data;

        public LordToil_BoardEnemyGravship(EnemyGravshipInstance instance)
        {
            this.instance = instance;
            data = new LordToilData_BoardEnemyGravship();
        }

        private Thing Core => (instance?.core != null && instance.core.Spawned) ? instance.core : null;

        private bool BoardingWindowOpen => instance == null || instance.departureTick < 0 || Find.TickManager.TicksGame < instance.departureTick;

        public override void Init()
        {
            base.Init();
            if (instance == null)
            {
                Logger.Error("LordToil_BoardEnemyGravship.Init: no EnemyGravshipInstance was supplied; every pawn will fall back to map-edge escape.");
            }
            else
            {
                if (instance.state == GravshipRaidState.Landed)
                {
                    instance.state = GravshipRaidState.Boarding;
                }
                if (instance.departureTick < 0)
                {
                    int timeout = (instance.template != null && instance.template.retreatTimeoutTicks > 0) ? instance.template.retreatTimeoutTicks : FallbackRetreatTimeoutTicks;
                    instance.departureTick = Find.TickManager.TicksGame + timeout;
                }

                Thing core = Core;
                CompTransporter transporter = core?.TryGetComp<CompTransporter>();
                if (transporter != null)
                {
                    transporter.massCapacityOverride = Mathf.Max(transporter.Props.massCapacity, instance.crew.Count * MassCapacityPerCrew);
                    if (transporter.groupID < 0)
                    {
                        TransporterUtility.InitiateLoading(Gen.YieldSingle(transporter));
                        Logger.Message($"LordToil_BoardEnemyGravship.Init: started controlled loading (group {transporter.groupID}) for {instance}.");
                    }
                }
            }

            // Deliberately does not call EnsureCorrectDuties() here: Lord.GotoToil (decompiled source) always
            // calls Init() immediately followed by UpdateAllDuties() on the same toil-entry, so doing it here
            // too would just be a redundant duplicate pass - matches the convention every vanilla LordToil
            // subclass already follows (e.g. LordToil_AssaultColony.Init() only does its lesson-teaching side
            // effect, never its own duty assignment).
        }

        public override void UpdateAllDuties()
        {
            EnsureCorrectDuties();
        }

        public override void LordToilTick()
        {
            EnsureCorrectDuties();
        }

        private DutyDef GetExpectedDutyDef(Pawn pawn, Thing core)
        {
            if (core == null || !BoardingWindowOpen)
            {
                return DutyDefOf.ExitMapBestAndDefendSelf;
            }
            if (!pawn.CanReach(core, PathEndMode.Touch, Danger.Deadly))
            {
                return DutyDefOf.ExitMapBestAndDefendSelf;
            }
            return DutyDefOf.EnterTransporterAndDefendSelf;
        }

        private void EnsureCorrectDuties()
        {
            Thing core = Core;
            SendPhaseMessage(core);

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];
                if (pawn?.mindState == null || !pawn.Spawned)
                {
                    continue;
                }

                DutyDef expected = GetExpectedDutyDef(pawn, core);
                if (pawn.mindState.duty == null || pawn.mindState.duty.def != expected)
                {
                    PawnDuty duty = (expected == DutyDefOf.EnterTransporterAndDefendSelf)
                        ? new PawnDuty(expected, core)
                        : new PawnDuty(expected);
                    duty.locomotion = LocomotionUrgency.Jog;
                    pawn.mindState.duty = duty;
                    if (pawn.jobs != null && pawn.jobs.curJob != null)
                    {
                        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    }
                }
            }

            // Hand off to the real Boarding -> Launching -> Departed sequence once boarding is
            // "complete" across the WHOLE raid instance, not just this lord's own owned pawns - a raid can be
            // split into several lords (IncidentParmsUtility.SplitIntoGroups), and one lord's group finishing
            // early must not launch the ship out from under another lord's still-boarding pawns.
            // EnemyGravshipRaidUtility.BeginDeparture itself no-ops for any state other than Boarding and for
            // a missing/despawned core, so this is safe to evaluate every tick from every lord without an
            // extra latch here.
            if (core != null && instance != null && instance.state == GravshipRaidState.Boarding && !AnyCrewStillTryingToBoard(core))
            {
                EnemyGravshipRaidUtility.BeginDeparture(instance, lord.Map);
            }
        }

        private bool AnyCrewStillTryingToBoard(Thing core)
        {
            if (!BoardingWindowOpen || instance?.crew == null)
            {
                return false;
            }
            for (int i = 0; i < instance.crew.Count; i++)
            {
                Pawn pawn = instance.crew[i];
                if (pawn == null || pawn.Dead || !pawn.Spawned)
                {
                    continue;
                }
                if (pawn.CanReach(core, PathEndMode.Touch, Danger.Deadly))
                {
                    return true;
                }
            }
            return false;
        }

        private void SendPhaseMessage(Thing core)
        {
            if (instance?.faction?.def == null)
            {
                return;
            }
            LordToilData_BoardEnemyGravship data = Data;
            if (core != null)
            {
                if (!data.sentBoardingMessage)
                {
                    data.sentBoardingMessage = true;
                    Messages.Message("GravshipRaids.MessageRaidersBoarding".Translate(instance.faction.def.pawnsPlural.CapitalizeFirst(), instance.faction.Name), core, MessageTypeDefOf.NeutralEvent);
                }
                return;
            }

            if (instance.state == GravshipRaidState.Departed || instance.state == GravshipRaidState.Destroyed)
            {
                return;
            }
            if (!data.sentStrandedMessage)
            {
                data.sentStrandedMessage = true;
                TargetInfo target = (lord.ownedPawns.Count > 0) ? (TargetInfo)lord.ownedPawns[0] : TargetInfo.Invalid;
                Messages.Message("GravshipRaids.MessageRaidersStranded".Translate(instance.faction.def.pawnsPlural.CapitalizeFirst(), instance.faction.Name), target, MessageTypeDefOf.NeutralEvent);
            }
        }
    }
}
