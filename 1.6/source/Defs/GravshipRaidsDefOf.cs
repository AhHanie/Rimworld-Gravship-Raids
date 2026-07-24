using RimWorld;
using Verse;

namespace Gravship_Raids
{
    [DefOf]
    public static class GravshipRaidsDefOf
    {
        public static IncidentDef GR_GravshipRaid;

        public static RaidStrategyDef GR_GravshipAssault;

        public static PawnsArrivalModeDef GR_GravshipLanding;

        public static ThingDef GR_EnemyGravshipCore;

        public static ThingDef GR_GravshipDepartureSkyfaller;

        public static ThingDef GR_GravshipArrivalSkyfaller_Small;

        public static ThingDef GR_GravshipArrivalSkyfaller_Medium;

        public static ThingDef GR_GravshipArrivalSkyfaller_Large;

        public static ThingDef GR_CrashlandedGravshipSkyfaller;

        public static PrefabDef GR_Prefab_CrashlandedGravship;

        public static PrefabDef GR_Prefab_LostTribeGravship;

        static GravshipRaidsDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(GravshipRaidsDefOf));
        }
    }
}
