using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Gravship_Raids
{
    public static class CrashlandedGravshipStartUtility
    {
        // ~3 seconds at normal speed - long enough to read as a deliberate pause after impact rather than
        // an instant pop-out.
        public const int EjectionDelayTicks = 180;

        private const int MaxLandingSearchRadius = 40;

        public static bool TryFindLandingRoot(PrefabDef prefab, Map map, IntVec3 start, out IntVec3 root, out Rot4 rotation)
        {
            root = IntVec3.Invalid;
            rotation = Rot4.North;
            if (prefab == null || map == null || !start.IsValid)
            {
                return false;
            }

            List<Rot4> rotations = AllowedRotations(prefab);
            int cellCount = GenRadial.NumCellsInRadius(MaxLandingSearchRadius);
            for (int i = 0; i < cellCount; i++)
            {
                IntVec3 candidate = start + GenRadial.RadialPattern[i];
                if (!candidate.InBounds(map))
                {
                    continue;
                }
                for (int r = 0; r < rotations.Count; r++)
                {
                    if (GravshipRaidTemplateUtility.CanSpawnPrefab(prefab, map, candidate, rotations[r], canWipeEdifices: false))
                    {
                        root = candidate;
                        rotation = rotations[r];
                        return true;
                    }
                }
            }
            return false;
        }

        private static List<Rot4> AllowedRotations(PrefabDef prefab)
        {
            List<Rot4> rotations = new List<Rot4>(4);
            if ((prefab.rotations & RotEnum.North) != 0)
            {
                rotations.Add(Rot4.North);
            }
            if ((prefab.rotations & RotEnum.East) != 0)
            {
                rotations.Add(Rot4.East);
            }
            if ((prefab.rotations & RotEnum.South) != 0)
            {
                rotations.Add(Rot4.South);
            }
            if ((prefab.rotations & RotEnum.West) != 0)
            {
                rotations.Add(Rot4.West);
            }
            if (rotations.Count == 0)
            {
                rotations.Add(Rot4.North);
            }
            return rotations;
        }

        public static void CompleteImpact(CrashlandedGravshipArrivalSkyfaller skyfaller, Map map)
        {
            List<Pawn> colonists = new List<Pawn>();
            List<Pawn> pets = new List<Pawn>();
            List<Thing> items = new List<Thing>();
            ThingOwner innerContainer = skyfaller.GetDirectlyHeldThings();
            for (int i = innerContainer.Count - 1; i >= 0; i--)
            {
                Thing thing = innerContainer[i];
                if (thing is Pawn pawn)
                {
                    // Every configured colonist is a humanlike Pawn; every ScenPart_StartingAnimal pet is not.
                    // This is the only signal available post-flight to tell the two apart without extra
                    // save/load bookkeeping.
                    if (pawn.RaceProps.Humanlike)
                    {
                        colonists.Add(pawn);
                    }
                    else
                    {
                        pets.Add(pawn);
                    }
                }
                else
                {
                    items.Add(thing);
                }
            }
            PrefabDef prefab = skyfaller.prefab;
            IntVec3 root = skyfaller.plannedRoot;
            Rot4 rotation = skyfaller.plannedRotation;
            IntVec3 fallbackNear = root.IsValid ? root : skyfaller.Position;

            if (map == null || map.Disposed)
            {
                Logger.Error("CrashlandedGravshipStartUtility.CompleteImpact: map is gone before the wreck skyfaller landed; pawns and items are lost.");
                return;
            }

            if (prefab == null || !GravshipRaidTemplateUtility.CanSpawnPrefab(prefab, map, root, rotation, canWipeEdifices: false))
            {
                Logger.Warning($"CrashlandedGravshipStartUtility.CompleteImpact: wreck prefab '{prefab?.defName ?? "null"}' can no longer be placed at {root} (site changed during the flight-in?); falling back to a safe drop near the site so nothing is lost.");
                FallbackArrive(colonists, pets, items, map, fallbackNear);
                return;
            }

            List<Thing> spawned = new List<Thing>();
            PrefabUtility.SpawnPrefab(prefab, map, root, rotation, Faction.OfPlayer, spawned);

            ApplyCrashDamage(spawned);

            List<Building_CryptosleepCasket> caskets = spawned.OfType<Building_CryptosleepCasket>().ToList();
            List<(Building_CryptosleepCasket casket, Pawn pawn)> occupants = new List<(Building_CryptosleepCasket, Pawn)>();
            List<Thing> protectedFromExplosion = new List<Thing>(caskets);
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn colonist = colonists[i];
                if (i < caskets.Count && caskets[i].TryAcceptThing(colonist, allowSpecialEffects: false))
                {
                    occupants.Add((caskets[i], colonist));
                }
                else
                {
                    Logger.Warning($"CrashlandedGravshipStartUtility.CompleteImpact: could not place colonist '{colonist.LabelShort}' into a wreck casket at {root} ({caskets.Count} casket(s) available for {colonists.Count} colonist(s)); placing nearby and applying cryptosleep sickness directly instead.");
                    RecoverPawnNearby(colonist, map, root);
                    protectedFromExplosion.Add(colonist);
                }
            }

            List<IntVec3> interiorCells = GravshipRaidTemplateUtility.GetOpenInteriorCells(prefab, map, root, rotation).InRandomOrder().ToList();
            PlaceNearby(pets.Cast<Thing>().Concat(items), map, root, interiorCells);
            protectedFromExplosion.AddRange(pets);
            protectedFromExplosion.AddRange(items);

            PlayImpactEffects(map, prefab, root, rotation, spawned, protectedFromExplosion);

            if (occupants.Count > 0)
            {
                MapComponent_CrashlandedGravshipStart.GetFor(map)?.ScheduleEjection(occupants, Find.TickManager.TicksGame + EjectionDelayTicks, root);
            }

            Logger.Message($"CrashlandedGravshipStartUtility.CompleteImpact: wreck landed at {root} (rot {rotation}); {occupants.Count}/{colonists.Count} colonist(s) in caskets, {pets.Count} pet(s) and {items.Count} item stack(s) placed nearby.");
        }

        private static void ApplyCrashDamage(List<Thing> spawned)
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                Thing thing = spawned[i];
                if (thing == null || thing.Destroyed || thing is Building_CryptosleepCasket || !thing.def.useHitPoints)
                {
                    continue;
                }
                bool isDamageTarget = thing.def == ThingDefOf.GravshipHull || thing.def == ThingDefOf.SmallThruster
                    || thing.def == ThingDefOf.ChemfuelTank || thing.TryGetComp<CompGravshipThruster>() != null;
                if (!isDamageTarget)
                {
                    continue;
                }
                // Fixed fraction, not Rand-based: every wreck reads as damaged the same way instead of
                // occasionally rolling near-full HP on the pieces that are supposed to sell "crashed".
                thing.HitPoints = Math.Max(1, thing.MaxHitPoints / 4);
            }
        }

        private static void RecoverPawnNearby(Pawn pawn, Map map, IntVec3 near)
        {
            IntVec3 cell = CellFinder.RandomClosewalkCellNear(near.IsValid ? near : map.Center, map, 6);
            GenSpawn.Spawn(pawn, cell, map, Rot4.Random);
            if (pawn.RaceProps.IsFlesh)
            {
                pawn.health.AddHediff(HediffDefOf.CryptosleepSickness);
            }
        }

        private static void PlaceNearby(IEnumerable<Thing> things, Map map, IntVec3 root, List<IntVec3> interiorCells)
        {
            int i = 0;
            foreach (Thing thing in things)
            {
                IntVec3 cell;
                if (interiorCells.Count > 0)
                {
                    // Multiple things can share one map cell, so cycling back through the list once every
                    // entry has a distinct cell is fine.
                    cell = interiorCells[i % interiorCells.Count];
                    i++;
                }
                else
                {
                    cell = CellFinder.RandomClosewalkCellNear(root, map, 6);
                }
                GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near);
            }
        }

        private const float ExplosionRadius = 2.9f;

        private const int MaxExplosions = 3;

        private static void PlayImpactEffects(Map map, PrefabDef prefab, IntVec3 root, Rot4 rotation, List<Thing> spawned, List<Thing> protectedThings)
        {
            CellRect footprint = GravshipRaidTemplateUtility.GetRotatedBounds(prefab, root, rotation);
            foreach (IntVec3 cell in footprint.Cells)
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }
                if (Rand.Chance(0.3f))
                {
                    FleckMaker.ThrowDustPuff(cell, map, Rand.Range(1f, 2f));
                }
            }
            FleckMaker.ThrowHeatGlow(footprint.CenterCell, map, 2.5f);

            // One explosion per surviving chemfuel tank (ruptured on impact), or a single one at the wreck's
            // center if the design has none, so a landing always bangs. ignoredThings keeps it from ever
            // touching the caskets, their occupants, or anything already placed for the player.
            List<IntVec3> explosionCenters = spawned
                .Where((Thing t) => t != null && !t.Destroyed && t.def == ThingDefOf.ChemfuelTank)
                .Select((Thing t) => t.Position)
                .Take(MaxExplosions)
                .ToList();
            if (explosionCenters.Count == 0)
            {
                explosionCenters.Add(footprint.CenterCell);
            }

            foreach (IntVec3 center in explosionCenters)
            {
                if (!center.InBounds(map))
                {
                    continue;
                }
                GenExplosion.DoExplosion(center, map, ExplosionRadius, DamageDefOf.Bomb, instigator: null, chanceToStartFire: 1f, ignoredThings: protectedThings);
            }

            IgniteWreckage(map, prefab, root, rotation, protectedThings);
        }

        private static void IgniteWreckage(Map map, PrefabDef prefab, IntVec3 root, Rot4 rotation, List<Thing> protectedThings)
        {
            HashSet<IntVec3> occupiedByLoose = new HashSet<IntVec3>();
            for (int i = 0; i < protectedThings.Count; i++)
            {
                if (protectedThings[i] is Building)
                {
                    continue;
                }
                occupiedByLoose.Add(protectedThings[i].Position);
            }

            // A couple of guaranteed fires so the wreck still reads as burning even where the hull/floor is
            // bare metal with too little flammability for the explosion's own chanceToStartFire to catch.
            List<IntVec3> candidates = GravshipRaidTemplateUtility.GetOpenInteriorCells(prefab, map, root, rotation)
                .Where((IntVec3 c) => !occupiedByLoose.Contains(c))
                .InRandomOrder()
                .Take(2)
                .ToList();
            for (int i = 0; i < candidates.Count; i++)
            {
                Fire fire = (Fire)ThingMaker.MakeThing(ThingDefOf.Fire);
                fire.fireSize = Rand.Range(0.4f, 0.8f);
                GenSpawn.Spawn(fire, candidates[i], map, Rot4.North);
            }
        }

        public static void FallbackArrive(List<Pawn> colonists, List<Pawn> pets, List<Thing> items, Map map, IntVec3 near)
        {
            if (map == null || map.Disposed)
            {
                Logger.Error("CrashlandedGravshipStartUtility.FallbackArrive: map is gone; the fallback payload cannot be placed and is lost.");
                return;
            }

            List<List<Thing>> groups = new List<List<Thing>>();
            for (int i = 0; i < colonists.Count; i++)
            {
                groups.Add(new List<Thing> { colonists[i] });
            }
            if (groups.Count == 0)
            {
                groups.Add(new List<Thing>());
            }
            int idx = 0;
            for (int i = 0; i < pets.Count; i++)
            {
                groups[idx % groups.Count].Add(pets[i]);
                idx++;
            }
            for (int i = 0; i < items.Count; i++)
            {
                groups[idx % groups.Count].Add(items[i]);
                idx++;
            }

            IntVec3 spot = near.IsValid ? near : map.Center;
            DropPodUtility.DropThingGroupsNear(spot, map, groups, 110, instaDrop: true, leaveSlag: true, canRoofPunch: true, forbid: true, allowFogged: false);
        }
    }
}
