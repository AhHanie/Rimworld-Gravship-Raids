using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Gravship_Raids
{
    public enum ShuttleRaidState : byte
    {
        Landing,
        Landed,
        Boarding,
        Departing,
        Departed,
        Lost
    }

    public class EnemyShuttleRaidInstance : IExposable, ILoadReferenceable
    {
        public int loadID = -1;

        public ShuttleRaidTemplateDef template;

        public Faction faction;

        public Thing shuttle;

        public List<Pawn> crew = new List<Pawn>();

        public ShuttleRaidState state = ShuttleRaidState.Landing;

        public bool shuttleLost;

        public int departureTick = -1;

        public EnemyShuttleRaidInstance()
        {
        }

        public EnemyShuttleRaidInstance(ShuttleRaidTemplateDef template, Faction faction, Thing shuttle)
        {
            loadID = Find.UniqueIDsManager.GetNextThingID();
            this.template = template;
            this.faction = faction;
            this.shuttle = shuttle;
        }

        public bool ShuttleAvailable => shuttle != null && shuttle.Spawned && !shuttle.Destroyed;

        public string GetUniqueLoadID()
        {
            return "GRShuttleInstance_" + loadID;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref loadID, "loadID", -1);
            Scribe_Defs.Look(ref template, "template");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref shuttle, "shuttle");
            Scribe_Collections.Look(ref crew, "crew", LookMode.Reference);
            Scribe_Values.Look(ref state, "state", ShuttleRaidState.Landing);
            Scribe_Values.Look(ref shuttleLost, "shuttleLost", false);
            Scribe_Values.Look(ref departureTick, "departureTick", -1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                crew?.RemoveAll((Pawn p) => p == null);
            }
            if (Scribe.mode == LoadSaveMode.LoadingVars && crew == null)
            {
                crew = new List<Pawn>();
            }
        }

        public override string ToString()
        {
            return $"EnemyShuttleRaidInstance(template={template?.defName}, state={state}, crew={crew?.Count ?? 0})";
        }
    }
}
