using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace Gravship_Raids
{
    public class MapComponent_ShuttleRaid : MapComponent
    {
        private const int SweepIntervalTicks = 251;

        private List<EnemyShuttleRaidInstance> instances = new List<EnemyShuttleRaidInstance>();

        public MapComponent_ShuttleRaid(Map map)
            : base(map)
        {
        }

        public IReadOnlyList<EnemyShuttleRaidInstance> Instances => instances;

        public int ActiveInstanceCount => instances.Count((EnemyShuttleRaidInstance i) =>
            i.state != ShuttleRaidState.Departed && i.state != ShuttleRaidState.Lost);

        public static MapComponent_ShuttleRaid GetFor(Map map)
        {
            return map?.GetComponent<MapComponent_ShuttleRaid>();
        }

        public void RegisterInstance(EnemyShuttleRaidInstance instance)
        {
            if (instance == null || instances.Contains(instance))
            {
                return;
            }
            instances.Add(instance);
            Logger.Message($"MapComponent_ShuttleRaid.RegisterInstance: registered {instance} on map {map}. Active instances now {ActiveInstanceCount}.");
        }

        public void DeregisterInstance(EnemyShuttleRaidInstance instance)
        {
            if (instance == null)
            {
                return;
            }
            if (instances.Remove(instance))
            {
                Logger.Message($"MapComponent_ShuttleRaid.DeregisterInstance: removed {instance} from map {map}.");
            }
        }

        public EnemyShuttleRaidInstance GetInstanceForPawn(Pawn pawn)
        {
            if (pawn == null)
            {
                return null;
            }
            return instances.FirstOrDefault((EnemyShuttleRaidInstance i) => i.crew != null && i.crew.Contains(pawn));
        }

        public EnemyShuttleRaidInstance GetInstanceForShuttle(Thing shuttle)
        {
            if (shuttle == null)
            {
                return null;
            }
            return instances.FirstOrDefault((EnemyShuttleRaidInstance i) => i.shuttle == shuttle);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref instances, "instances", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars && instances == null)
            {
                instances = new List<EnemyShuttleRaidInstance>();
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                instances.RemoveAll((EnemyShuttleRaidInstance i) => i == null);
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            foreach (EnemyShuttleRaidInstance instance in instances.ToList())
            {
                if (instance == null || instance.state != ShuttleRaidState.Departing)
                {
                    continue;
                }
                Logger.Warning($"MapComponent_ShuttleRaid.FinalizeInit: {instance} was saved mid-departure (Departing); finalizing its departure now.");
                EnemyShuttleRaidUtility.FinalizeDeparture(instance);
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
            SweepInstances();
        }

        private void SweepInstances()
        {
            foreach (EnemyShuttleRaidInstance instance in instances.ToList())
            {
                if (instance == null)
                {
                    instances.Remove(instance);
                    continue;
                }

                if (instance.state == ShuttleRaidState.Landing)
                {
                    if (instance.ShuttleAvailable)
                    {
                        instance.state = ShuttleRaidState.Landed;
                        Logger.Message($"MapComponent_ShuttleRaid.SweepInstances: {instance} finished landing.");
                    }
                }
                else if (!instance.ShuttleAvailable && !instance.shuttleLost)
                {
                    instance.shuttleLost = true;
                    if (instance.state == ShuttleRaidState.Landed || instance.state == ShuttleRaidState.Boarding)
                    {
                        instance.state = ShuttleRaidState.Lost;
                    }
                }

                if ((instance.state == ShuttleRaidState.Landing || instance.state == ShuttleRaidState.Landed || instance.state == ShuttleRaidState.Boarding) && !AnyCrewStillLordOwned(instance))
                {
                    EnemyShuttleRaidUtility.AbandonInstance(instance, "no crew left owned by any Lord (periodic sweep)");
                }

                if ((instance.state == ShuttleRaidState.Departed || instance.state == ShuttleRaidState.Lost) && !StillReferencedByLiveLord(instance))
                {
                    DeregisterInstance(instance);
                }
            }
        }

        private static bool AnyCrewStillLordOwned(EnemyShuttleRaidInstance instance)
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

        private bool StillReferencedByLiveLord(EnemyShuttleRaidInstance instance)
        {
            List<Lord> lords = map.lordManager.lords;
            for (int i = 0; i < lords.Count; i++)
            {
                if (lords[i].LordJob is LordJob_ShuttleRaid job && job.Instance == instance)
                {
                    return true;
                }
            }
            return false;
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            foreach (EnemyShuttleRaidInstance instance in instances)
            {
                CompTransporter transporter = instance?.shuttle?.TryGetComp<CompTransporter>();
                if (transporter?.innerContainer == null || !transporter.innerContainer.Any)
                {
                    continue;
                }
                Logger.Message($"MapComponent_ShuttleRaid.MapRemoved: passing {transporter.innerContainer.Count} boarded pawn(s) from {instance} to world pawns before map removal.");
                transporter.innerContainer.ClearAndDestroyContentsOrPassToWorld(DestroyMode.Vanish);
            }
        }
    }
}
