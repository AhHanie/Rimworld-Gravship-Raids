using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.Sound;

namespace Gravship_Raids
{
    public class MapComponent_CrashlandedGravshipStart : MapComponent
    {
        private List<EjectionSession> sessions = new List<EjectionSession>();

        public MapComponent_CrashlandedGravshipStart(Map map)
            : base(map)
        {
        }

        public static MapComponent_CrashlandedGravshipStart GetFor(Map map)
        {
            return map?.GetComponent<MapComponent_CrashlandedGravshipStart>();
        }

        public void ScheduleEjection(List<(Building_CryptosleepCasket casket, Pawn pawn)> occupants, int dueTick, IntVec3 root)
        {
            if (occupants.NullOrEmpty())
            {
                return;
            }
            EjectionSession session = new EjectionSession
            {
                dueTick = dueTick,
                root = root
            };
            for (int i = 0; i < occupants.Count; i++)
            {
                session.occupants.Add(new CasketOccupant(occupants[i].casket, occupants[i].pawn));
            }
            sessions.Add(session);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref sessions, "crashlandedGravshipEjectionSessions", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars && sessions == null)
            {
                sessions = new List<EjectionSession>();
            }
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (sessions.Count == 0)
            {
                return;
            }
            int currentTick = Find.TickManager.TicksGame;
            for (int i = sessions.Count - 1; i >= 0; i--)
            {
                if (currentTick < sessions[i].dueTick)
                {
                    continue;
                }
                ResolveSession(sessions[i]);
                sessions.RemoveAt(i);
            }
        }

        private void ResolveSession(EjectionSession session)
        {
            int resolvedCount = 0;
            for (int i = 0; i < session.occupants.Count; i++)
            {
                CasketOccupant occupant = session.occupants[i];
                if (occupant?.pawn == null || occupant.pawn.Destroyed)
                {
                    continue;
                }

                if (occupant.casket != null && !occupant.casket.Destroyed && occupant.casket.Spawned && occupant.casket.GetDirectlyHeldThings().Contains(occupant.pawn))
                {
                    occupant.casket.EjectContents();
                    resolvedCount++;
                    continue;
                }

                if (!occupant.pawn.Spawned)
                {
                    // The casket holding this colonist was destroyed or otherwise lost before the scheduled
                    // wake-up (e.g. hostile action during the very short window between impact and ejection).
                    // Recover the colonist directly instead of letting them stay trapped forever.
                    Logger.Warning($"MapComponent_CrashlandedGravshipStart.ResolveSession: casket holding '{occupant.pawn.LabelShort}' was lost before the scheduled wake-up; recovering the colonist directly near the wreck.");
                    IntVec3 cell = CellFinder.RandomClosewalkCellNear(session.root.IsValid ? session.root : map.Center, map, 10);
                    GenSpawn.Spawn(occupant.pawn, cell, map, Rot4.Random);
                    if (occupant.pawn.RaceProps.IsFlesh)
                    {
                        occupant.pawn.health.AddHediff(HediffDefOf.CryptosleepSickness);
                    }
                    resolvedCount++;
                }
            }

            if (resolvedCount > 0)
            {
                TargetInfo wreckTarget = new TargetInfo(session.root.IsValid ? session.root : map.Center, map);
                SoundDefOf.CryptosleepCasket_Eject.PlayOneShot(SoundInfo.InMap(wreckTarget));
                Messages.Message("GravshipRaids.CrashlandedGravship.MessageColonistsWoke".Translate(), wreckTarget, MessageTypeDefOf.PositiveEvent, historical: false);
            }
        }

        private class CasketOccupant : IExposable
        {
            public Building_CryptosleepCasket casket;
            public Pawn pawn;

            public CasketOccupant()
            {
            }

            public CasketOccupant(Building_CryptosleepCasket casket, Pawn pawn)
            {
                this.casket = casket;
                this.pawn = pawn;
            }

            public void ExposeData()
            {
                Scribe_References.Look(ref casket, "casket");
                Scribe_References.Look(ref pawn, "pawn");
            }
        }

        private class EjectionSession : IExposable
        {
            public List<CasketOccupant> occupants = new List<CasketOccupant>();
            public int dueTick;
            public IntVec3 root;

            public void ExposeData()
            {
                Scribe_Collections.Look(ref occupants, "occupants", LookMode.Deep);
                Scribe_Values.Look(ref dueTick, "dueTick");
                Scribe_Values.Look(ref root, "root");
                if (Scribe.mode == LoadSaveMode.PostLoadInit)
                {
                    occupants?.RemoveAll((CasketOccupant o) => o == null);
                }
                if (occupants == null)
                {
                    occupants = new List<CasketOccupant>();
                }
            }
        }
    }
}
