using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Gravship_Raids
{
    public class LordToilData_BoardEnemyShuttle : LordToilData
    {
        public bool sentBoardingMessage;

        public bool sentStrandedMessage;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref sentBoardingMessage, "sentBoardingMessage", false);
            Scribe_Values.Look(ref sentStrandedMessage, "sentStrandedMessage", false);
        }
    }

    public class LordToil_BoardEnemyShuttle : LordToil
    {
        private const float MassCapacityPerCrew = 250f;

        private const int FallbackRetreatTimeoutTicks = 2500;

        private readonly EnemyShuttleRaidInstance instance;

        public override bool AllowSatisfyLongNeeds => false;

        public override bool AllowSelfTend => false;

        private LordToilData_BoardEnemyShuttle Data => (LordToilData_BoardEnemyShuttle)data;

        public LordToil_BoardEnemyShuttle(EnemyShuttleRaidInstance instance)
        {
            this.instance = instance;
            data = new LordToilData_BoardEnemyShuttle();
        }

        private Thing LiveShuttle => (instance != null && instance.ShuttleAvailable) ? instance.shuttle : null;

        private bool BoardingWindowOpen => instance == null || instance.departureTick < 0 || Find.TickManager.TicksGame < instance.departureTick;

        public override void Init()
        {
            base.Init();
            Logger.Message($"LordToil_BoardEnemyShuttle.Init: entered boarding toil for {instance?.ToString() ?? "null instance"}.");
            if (instance == null)
            {
                Logger.Error("LordToil_BoardEnemyShuttle.Init: no EnemyShuttleRaidInstance was supplied; every pawn will fall back to map-edge escape.");
                return;
            }

            if (instance.state == ShuttleRaidState.Landing || instance.state == ShuttleRaidState.Landed)
            {
                instance.state = ShuttleRaidState.Boarding;
            }
            if (instance.departureTick < 0)
            {
                instance.departureTick = Find.TickManager.TicksGame + FallbackRetreatTimeoutTicks;
            }

            Thing shuttle = LiveShuttle;
            CompTransporter transporter = shuttle?.TryGetComp<CompTransporter>();
            if (transporter != null)
            {
                transporter.massCapacityOverride = Mathf.Max(transporter.Props.massCapacity, instance.crew.Count * MassCapacityPerCrew);
                if (transporter.groupID < 0)
                {
                    TransporterUtility.InitiateLoading(Gen.YieldSingle(transporter));
                    Logger.Message($"LordToil_BoardEnemyShuttle.Init: started controlled loading (group {transporter.groupID}) for {instance}.");
                }
            }
        }

        public override void UpdateAllDuties()
        {
            EnsureCorrectDuties();
        }

        public override void LordToilTick()
        {
            EnsureCorrectDuties();
        }

        private DutyDef GetExpectedDutyDef(Pawn pawn, Thing shuttle)
        {
            if (shuttle == null || !BoardingWindowOpen)
            {
                return DutyDefOf.ExitMapBestAndDefendSelf;
            }
            if (!pawn.CanReach(shuttle, PathEndMode.Touch, Danger.Deadly))
            {
                return DutyDefOf.ExitMapBestAndDefendSelf;
            }
            return DutyDefOf.EnterTransporterAndDefendSelf;
        }

        private void EnsureCorrectDuties()
        {
            Thing shuttle = LiveShuttle;

            if (shuttle == null && instance != null && instance.shuttle != null && instance.state == ShuttleRaidState.Boarding)
            {
                EnemyShuttleRaidUtility.AbandonInstance(instance, "shuttle destroyed while boarding");
            }

            SendPhaseMessage(shuttle);

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];
                if (pawn?.mindState == null || !pawn.Spawned)
                {
                    continue;
                }

                DutyDef expected = GetExpectedDutyDef(pawn, shuttle);
                if (pawn.mindState.duty == null || pawn.mindState.duty.def != expected)
                {
                    PawnDuty duty = (expected == DutyDefOf.EnterTransporterAndDefendSelf)
                        ? new PawnDuty(expected, shuttle)
                        : new PawnDuty(expected);
                    duty.locomotion = LocomotionUrgency.Jog;
                    pawn.mindState.duty = duty;
                    if (pawn.jobs != null && pawn.jobs.curJob != null)
                    {
                        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    }
                }
            }

            if (shuttle != null && instance != null && instance.state == ShuttleRaidState.Boarding)
            {
                bool stillTrying = AnyCrewStillTryingToBoard(shuttle);
                if (GravshipRaidsSettings.debugLogging && Find.TickManager.TicksGame % 60 == 0)
                {
                    Logger.Message($"LordToil_BoardEnemyShuttle.EnsureCorrectDuties: {instance} boarding status - stillTrying={stillTrying}, boardingWindowOpen={BoardingWindowOpen}, departureTick={instance.departureTick}, currentTick={Find.TickManager.TicksGame}, crew=[{DescribeCrew(shuttle)}].");
                }
                if (!stillTrying)
                {
                    if (EnemyShuttleRaidUtility.HasLivingCrewBoarded(instance, shuttle))
                    {
                        Logger.Message($"LordToil_BoardEnemyShuttle.EnsureCorrectDuties: {instance} finished boarding with living crew aboard; departing.");
                        EnemyShuttleRaidUtility.TryDepart(instance);
                    }
                    else
                    {
                        Logger.Message($"LordToil_BoardEnemyShuttle.EnsureCorrectDuties: {instance} finished boarding with no living crew aboard; abandoning instead of departing.");
                        EnemyShuttleRaidUtility.AbandonInstance(instance, "no living crew boarded before the boarding window closed");
                    }
                }
            }
        }

        private string DescribeCrew(Thing shuttle)
        {
            if (instance?.crew == null)
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < instance.crew.Count; i++)
            {
                Pawn pawn = instance.crew[i];
                if (i > 0)
                {
                    sb.Append("; ");
                }
                if (pawn == null)
                {
                    sb.Append("null");
                    continue;
                }
                bool boarded = pawn.ParentHolder is CompTransporter transporterHolder && transporterHolder.parent == shuttle;
                sb.Append($"{pawn.LabelShort}[dead={pawn.Dead},downed={pawn.Downed},spawned={pawn.Spawned},boarded={boarded},canReach={(pawn.Spawned && !pawn.Dead && !pawn.Downed && pawn.CanReach(shuttle, PathEndMode.Touch, Danger.Deadly))},duty={pawn.mindState?.duty?.def?.defName ?? "none"}]");
            }
            return sb.ToString();
        }

        private bool AnyCrewStillTryingToBoard(Thing shuttle)
        {
            if (!BoardingWindowOpen || instance?.crew == null)
            {
                return false;
            }
            for (int i = 0; i < instance.crew.Count; i++)
            {
                Pawn pawn = instance.crew[i];
                if (pawn == null || pawn.Dead || pawn.Downed || !pawn.Spawned)
                {
                    continue;
                }
                if (pawn.CanReach(shuttle, PathEndMode.Touch, Danger.Deadly))
                {
                    return true;
                }
            }
            return false;
        }

        private void SendPhaseMessage(Thing shuttle)
        {
            if (instance?.faction?.def == null)
            {
                return;
            }
            LordToilData_BoardEnemyShuttle data = Data;
            if (shuttle != null)
            {
                if (!data.sentBoardingMessage)
                {
                    data.sentBoardingMessage = true;
                    Messages.Message("GravshipRaids.MessageShuttleRaidersBoarding".Translate(instance.faction.def.pawnsPlural.CapitalizeFirst(), instance.faction.Name), shuttle, MessageTypeDefOf.NeutralEvent);
                }
                return;
            }

            if (instance.state == ShuttleRaidState.Departed)
            {
                return;
            }
            if (!data.sentStrandedMessage)
            {
                data.sentStrandedMessage = true;
                TargetInfo target = (lord.ownedPawns.Count > 0) ? (TargetInfo)lord.ownedPawns[0] : TargetInfo.Invalid;
                Messages.Message("GravshipRaids.MessageShuttleRaidersStranded".Translate(instance.faction.def.pawnsPlural.CapitalizeFirst(), instance.faction.Name), target, MessageTypeDefOf.NeutralEvent);
            }
        }
    }
}
