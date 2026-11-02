using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Gravship_Raids
{
    public static class ShuttleRaidSpawnUtility
    {
        private static readonly int[] SearchRadii = { 8, 20, 40 };

        public static bool TryFindSafeCell(Pawn pawn, Map map, IntVec3 near, out IntVec3 cell)
        {
            if (!near.IsValid || !near.InBounds(map))
            {
                near = map.Center;
            }

            bool Valid(IntVec3 c)
            {
                return c.InBounds(map)
                    && c.Standable(map)
                    && c.WalkableBy(map, pawn)
                    && c.GetDoor(map) == null
                    && !ShuttleRaidTemplateUtility.IsWaterAt(c, map)
                    && map.reachability.CanReachColony(c);
            }

            for (int i = 0; i < SearchRadii.Length; i++)
            {
                if (CellFinder.TryFindRandomCellNear(near, map, SearchRadii[i], Valid, out cell))
                {
                    return true;
                }
            }
            return CellFinder.TryFindRandomCell(map, Valid, out cell);
        }

        public static bool TrySpawnOnSafeLand(Pawn pawn, Map map, IntVec3 near, Rot4 rotation)
        {
            if (pawn == null || pawn.Destroyed)
            {
                return false;
            }

            pawn.holdingOwner?.Remove(pawn);
            if (pawn.IsWorldPawn())
            {
                Find.WorldPawns.RemovePawn(pawn);
            }

            if (TryFindSafeCell(pawn, map, near, out IntVec3 cell))
            {
                GenSpawn.Spawn(pawn, cell, map, rotation);
                if (pawn.Spawned)
                {
                    return true;
                }
            }

            Logger.Error($"ShuttleRaidSpawnUtility.TrySpawnOnSafeLand: could not place {pawn} (id {pawn.thingIDNumber}) on map {map} near {near}; keeping them as a world pawn instead of destroying them.");
            if (!pawn.IsWorldPawn())
            {
                Find.WorldPawns.PassToWorld(pawn);
            }
            return false;
        }

        public static int TrySpawnAllOnSafeLand(IEnumerable<Pawn> pawns, Map map, IntVec3 near, Rot4 rotation)
        {
            int spawned = 0;
            foreach (Pawn pawn in new List<Pawn>(pawns))
            {
                if (TrySpawnOnSafeLand(pawn, map, near, rotation))
                {
                    spawned++;
                }
            }
            return spawned;
        }
    }
}
