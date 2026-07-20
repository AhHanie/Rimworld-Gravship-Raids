using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Gravship_Raids
{
    public enum GravshipRaidState : byte
    {
        Landing,
        Landed,
        Boarding,
        Launching,
        Departed,
        Destroyed
    }

    public class EnemyGravshipInstance : IExposable, ILoadReferenceable
    {
        public int loadID = -1;

        public GravshipRaidTemplateDef template;

        public IntVec3 root;

        public Rot4 rotation;

        public Faction faction;

        public Thing core;

        public List<Thing> spawnedThings = new List<Thing>();

        public List<Pawn> crew = new List<Pawn>();

        public List<Pawn> guardCrew = new List<Pawn>();

        public GravshipRaidState state = GravshipRaidState.Landing;

        public int departureTick = -1;

        public List<GravshipRaidTemplateUtility.TerrainCellSnapshot> terrainSnapshot = new List<GravshipRaidTemplateUtility.TerrainCellSnapshot>();

        public Thing departingSkyfaller;

        public EnemyGravshipInstance()
        {
        }

        public EnemyGravshipInstance(GravshipRaidTemplateDef template, IntVec3 root, Rot4 rotation, Faction faction, Thing core, List<Thing> spawnedThings)
        {
            loadID = Find.UniqueIDsManager.GetNextThingID();
            this.template = template;
            this.root = root;
            this.rotation = rotation;
            this.faction = faction;
            this.core = core;
            this.spawnedThings = spawnedThings ?? new List<Thing>();
        }

        public string GetUniqueLoadID()
        {
            return "GRShipInstance_" + loadID;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref loadID, "loadID", -1);
            Scribe_Defs.Look(ref template, "template");
            Scribe_Values.Look(ref root, "root");
            Scribe_Values.Look(ref rotation, "rotation");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref core, "core");
            Scribe_Collections.Look(ref spawnedThings, "spawnedThings", LookMode.Reference);
            Scribe_Collections.Look(ref crew, "crew", LookMode.Reference);
            Scribe_Collections.Look(ref guardCrew, "guardCrew", LookMode.Reference);
            Scribe_Values.Look(ref state, "state", GravshipRaidState.Landing);
            Scribe_Values.Look(ref departureTick, "departureTick", -1);
            Scribe_Collections.Look(ref terrainSnapshot, "terrainSnapshot", LookMode.Deep);
            Scribe_References.Look(ref departingSkyfaller, "departingSkyfaller");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                spawnedThings?.RemoveAll((Thing t) => t == null);
                crew?.RemoveAll((Pawn p) => p == null);
                guardCrew?.RemoveAll((Pawn p) => p == null || crew == null || !crew.Contains(p));
                terrainSnapshot?.RemoveAll((GravshipRaidTemplateUtility.TerrainCellSnapshot t) => t == null);
            }
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (spawnedThings == null)
                {
                    spawnedThings = new List<Thing>();
                }
                if (crew == null)
                {
                    crew = new List<Pawn>();
                }
                if (guardCrew == null)
                {
                    guardCrew = new List<Pawn>();
                }
                if (terrainSnapshot == null)
                {
                    terrainSnapshot = new List<GravshipRaidTemplateUtility.TerrainCellSnapshot>();
                }
            }
        }

        public bool IsGuard(Pawn pawn)
        {
            return pawn != null && guardCrew != null && guardCrew.Contains(pawn);
        }

        public override string ToString()
        {
            return $"EnemyGravshipInstance(template={template?.defName}, root={root}, state={state}, crew={crew?.Count ?? 0})";
        }
    }
}
