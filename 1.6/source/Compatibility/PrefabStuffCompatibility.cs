using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Gravship_Raids
{
    // Guards against another mod making a prefab piece stuffable after this mod's own prefab XML (which never
    // specifies <stuff> for such pieces) has already loaded - e.g. ReBuild: Doors and Corners' seamless wall
    // lamps option adding stuffCategories to WallLamp. Without this, ThingMaker.MakeThing logs a
    // "madeFromStuff but stuff=null" error before falling back to GenStuff.DefaultStuffFor itself; resolving
    // the material here up front gets the same material with no error.
    internal static class PrefabStuffCompatibility
    {
        public static void SpawnPrefab(PrefabDef prefab, Map map, IntVec3 pos, Rot4 rot, Faction faction = null, List<Thing> spawned = null)
        {
            PrefabUtility.SpawnPrefab(prefab, map, pos, rot, faction, spawned, ResolveMissingStuff);
        }

        private static Tuple<ThingDef, ThingDef> ResolveMissingStuff(PrefabThingData data)
        {
            if (data?.def == null || data.stuff != null || !data.def.MadeFromStuff)
            {
                return null;
            }

            ThingDef fallbackStuff = GenStuff.DefaultStuffFor(data.def);
            return Tuple.Create(data.def, fallbackStuff);
        }
    }
}
