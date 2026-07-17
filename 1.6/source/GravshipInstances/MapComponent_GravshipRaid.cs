using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace Gravship_Raids
{
    public class MapComponent_GravshipRaid : MapComponent
    {
        private const int SweepIntervalTicks = 251;

        private List<EnemyGravshipInstance> instances = new List<EnemyGravshipInstance>();

        private class SettleSession
        {
            public EnemyGravshipEffects Effects;
            public List<Thing> Thrusters;
            public Vector3 GroundCenter;
            public float DurationSeconds;
            public float RemainingSeconds;
        }

        private readonly List<SettleSession> settleSessions = new List<SettleSession>();

        public MapComponent_GravshipRaid(Map map)
            : base(map)
        {
        }

        public void BeginSettleEffects(IReadOnlyList<Thing> thrusters, Vector3 groundCenter, Rot4 shipRotation, IntVec3 launchDirection, float durationSeconds)
        {
            if (thrusters == null || thrusters.Count == 0 || durationSeconds <= 0f)
            {
                return;
            }
            settleSessions.Add(new SettleSession
            {
                Effects = new EnemyGravshipEffects(map, shipRotation, launchDirection),
                Thrusters = new List<Thing>(thrusters),
                GroundCenter = groundCenter,
                DurationSeconds = durationSeconds,
                RemainingSeconds = durationSeconds
            });
        }

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();
            if (settleSessions.Count == 0 || Find.CurrentMap != map)
            {
                return;
            }
            for (int i = settleSessions.Count - 1; i >= 0; i--)
            {
                SettleSession session = settleSessions[i];
                session.Effects.Tick(Time.deltaTime);
                session.Effects.DrawThrusters(session.Thrusters);
                float intensity = Mathf.Clamp01(session.RemainingSeconds / session.DurationSeconds);
                session.Effects.DrawDownwash(session.GroundCenter, intensity);
                session.RemainingSeconds -= Time.deltaTime;
                if (session.RemainingSeconds <= 0f)
                {
                    session.Effects.End();
                    settleSessions.RemoveAt(i);
                }
            }
        }

        public IReadOnlyList<EnemyGravshipInstance> Instances => instances;

        public int ActiveInstanceCount => instances.Count((EnemyGravshipInstance i) =>
            i.state != GravshipRaidState.Departed && i.state != GravshipRaidState.Destroyed &&
            (i.state == GravshipRaidState.Landing || i.core != null));

        public static MapComponent_GravshipRaid GetFor(Map map)
        {
            return map?.GetComponent<MapComponent_GravshipRaid>();
        }

        public void RegisterInstance(EnemyGravshipInstance instance)
        {
            if (instance == null || instances.Contains(instance))
            {
                return;
            }
            instances.Add(instance);
            Logger.Message($"MapComponent_GravshipRaid.RegisterInstance: registered {instance} on map {map}. Active instances now {ActiveInstanceCount}.");
        }

        public void DeregisterInstance(EnemyGravshipInstance instance)
        {
            if (instance == null)
            {
                return;
            }
            if (instances.Remove(instance))
            {
                Logger.Message($"MapComponent_GravshipRaid.DeregisterInstance: removed {instance} from map {map}.");
            }
        }

        public EnemyGravshipInstance GetInstanceForCore(Thing core)
        {
            if (core == null)
            {
                return null;
            }
            return instances.FirstOrDefault((EnemyGravshipInstance i) => i.core == core);
        }

        public EnemyGravshipInstance GetInstanceForPawn(Pawn pawn)
        {
            if (pawn == null)
            {
                return null;
            }
            return instances.FirstOrDefault((EnemyGravshipInstance i) => i.crew != null && i.crew.Contains(pawn));
        }

        public EnemyGravshipInstance GetInstanceAt(IntVec3 cell)
        {
            return instances.FirstOrDefault((EnemyGravshipInstance i) => i.template != null && GravshipRaidTemplateUtility.GetRotatedBounds(i.template, i.root, i.rotation).Contains(cell));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref instances, "instances", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars && instances == null)
            {
                instances = new List<EnemyGravshipInstance>();
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                int removedCount = instances.RemoveAll((EnemyGravshipInstance i) => i == null || (i.core == null && i.state != GravshipRaidState.Landing && i.state != GravshipRaidState.Departed && i.state != GravshipRaidState.Destroyed));
                if (removedCount > 0)
                {
                    Logger.Warning($"MapComponent_GravshipRaid.ExposeData: pruned {removedCount} instance(s) with an unexpectedly null core reference on map {map} during PostLoadInit.");
                }
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            foreach (EnemyGravshipInstance instance in instances.ToList())
            {
                if (instance == null || instance.state != GravshipRaidState.Launching)
                {
                    continue;
                }
                if (instance.departingSkyfaller != null && instance.departingSkyfaller.Spawned)
                {
                    // Normal mid-rise - GravshipDepartureSkyfaller.LeaveMap will finish this on its own.
                    continue;
                }
                Logger.Warning($"MapComponent_GravshipRaid.FinalizeInit: {instance} was saved mid-departure (Launching) with no live departing skyfaller; forcing completion.");
                EnemyGravshipRaidUtility.CompleteDeparture(instance, map);
            }
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (instances.Count == 0)
            {
                return;
            }
            if (Find.TickManager.TicksGame % SweepIntervalTicks != 0)
            {
                return;
            }
            SweepFinishedInstances();
        }

        private void SweepFinishedInstances()
        {
            foreach (EnemyGravshipInstance instance in instances.ToList())
            {
                if (instance == null)
                {
                    instances.Remove(instance);
                    continue;
                }

                if ((instance.state == GravshipRaidState.Landing || instance.state == GravshipRaidState.Landed || instance.state == GravshipRaidState.Boarding) && !AnyCrewStillLordOwned(instance))
                {
                    instance.state = GravshipRaidState.Destroyed;
                    Logger.Message($"MapComponent_GravshipRaid.SweepFinishedInstances: {instance} has no crew left owned by any Lord; marking it abandoned/Destroyed so it stops occupying a landing slot.");
                }

                if ((instance.state == GravshipRaidState.Departed || instance.state == GravshipRaidState.Destroyed) && !StillReferencedByLiveLord(instance))
                {
                    DeregisterInstance(instance);
                }
            }
        }

        private static bool AnyCrewStillLordOwned(EnemyGravshipInstance instance)
        {
            if (instance.crew == null)
            {
                return false;
            }
            for (int i = 0; i < instance.crew.Count; i++)
            {
                if (instance.crew[i]?.lord != null)
                {
                    return true;
                }
            }
            return false;
        }

        private bool StillReferencedByLiveLord(EnemyGravshipInstance instance)
        {
            List<Lord> lords = map.lordManager.lords;
            for (int i = 0; i < lords.Count; i++)
            {
                if (lords[i].LordJob is LordJob_GravshipRaid job && job.Instance == instance)
                {
                    return true;
                }
            }
            return false;
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            foreach (EnemyGravshipInstance instance in instances)
            {
                CompTransporter transporter = instance?.core?.TryGetComp<CompTransporter>();
                if (transporter?.innerContainer == null || !transporter.innerContainer.Any)
                {
                    continue;
                }
                Logger.Message($"MapComponent_GravshipRaid.MapRemoved: passing {transporter.innerContainer.Count} boarded pawn(s) from {instance} to world pawns before map removal.");
                transporter.innerContainer.ClearAndDestroyContentsOrPassToWorld(DestroyMode.Vanish);
            }
        }
    }
}
