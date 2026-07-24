using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Gravship_Raids
{
    public class ScenPart_CrashlandedGravshipStart : ScenPart
    {
        public PrefabDef prefab;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref prefab, "prefab");
        }

        public override void GenerateIntoMap(Map map)
        {
            if (Find.GameInitData == null || !ModsConfig.OdysseyActive)
            {
                return;
            }

            ThingDef skyfallerDef = GravshipRaidsDefOf.GR_CrashlandedGravshipSkyfaller;

            List<Pawn> colonists = new List<Pawn>(Find.GameInitData.startingAndOptionalPawns);

            List<Pawn> pets = new List<Pawn>();
            List<Thing> items = new List<Thing>();
            foreach (ScenPart part in Find.Scenario.AllParts)
            {
                foreach (Thing thing in part.PlayerStartingThings())
                {
                    if (thing is Pawn animal)
                    {
                        pets.Add(animal);
                        continue;
                    }
                    if (thing.def.CanHaveFaction)
                    {
                        thing.SetFactionDirect(Faction.OfPlayer);
                    }
                    items.Add(thing);
                }
            }
            for (int i = 0; i < colonists.Count; i++)
            {
                if (!Find.GameInitData.startingPossessions.TryGetValue(colonists[i], out List<ThingDefCount> possessions))
                {
                    continue;
                }
                for (int j = 0; j < possessions.Count; j++)
                {
                    Thing possession = StartingPawnUtility.GenerateStartingPossession(possessions[j]);
                    if (possession.def.CanHaveFaction)
                    {
                        possession.SetFactionDirect(Faction.OfPlayer);
                    }
                    items.Add(possession);
                }
            }

            IntVec3 searchStart = MapGenerator.PlayerStartSpotValid ? MapGenerator.PlayerStartSpot : map.Center;

            if (prefab == null || skyfallerDef == null)
            {
                Logger.Error($"ScenPart_CrashlandedGravshipStart.GenerateIntoMap: configured wreck prefab '{prefab?.defName ?? "null"}' or GR_CrashlandedGravshipSkyfaller failed to resolve; falling back to a safe drop-pod-style start with no wreck.");
                CrashlandedGravshipStartUtility.FallbackArrive(colonists, pets, items, map, searchStart);
                return;
            }

            if (!CrashlandedGravshipStartUtility.TryFindLandingRoot(prefab, map, searchStart, out IntVec3 root, out Rot4 rotation))
            {
                Logger.Warning($"ScenPart_CrashlandedGravshipStart.GenerateIntoMap: no legal landing site found for wreck prefab '{prefab.defName}' near {searchStart}; falling back to a safe drop-pod-style start with no wreck.");
                CrashlandedGravshipStartUtility.FallbackArrive(colonists, pets, items, map, searchStart);
                return;
            }

            // Subsequent ScenParts in this scenario (the near-player-start scatter parts) read this after we
            // return, so the relocated landing root must be committed before GenerateIntoMap moves on to them.
            MapGenerator.PlayerStartSpot = root;

            CrashlandedGravshipArrivalSkyfaller skyfaller = (CrashlandedGravshipArrivalSkyfaller)ThingMaker.MakeThing(skyfallerDef);
            skyfaller.prefab = prefab;
            skyfaller.plannedRoot = root;
            skyfaller.plannedRotation = rotation;

            ThingOwner innerContainer = skyfaller.GetDirectlyHeldThings();
            for (int i = 0; i < colonists.Count; i++)
            {
                innerContainer.TryAdd(colonists[i], canMergeWithExistingStacks: false);
            }
            for (int i = 0; i < pets.Count; i++)
            {
                innerContainer.TryAdd(pets[i], canMergeWithExistingStacks: false);
            }
            for (int i = 0; i < items.Count; i++)
            {
                innerContainer.TryAdd(items[i], canMergeWithExistingStacks: false);
            }

            // Direct spawn (not the ThingDef-based GenSpawn.Spawn overload, which would run CanSpawnAt against
            // root/rotation) - the skyfaller is airborne, not an edifice occupying the landing cell, exactly
            // like the mod's existing raid arrival skyfaller.
            GenSpawn.Spawn(skyfaller, root, map, rotation);

            Logger.Message($"ScenPart_CrashlandedGravshipStart.GenerateIntoMap: spawned crashlanded gravship wreck skyfaller at {root} (rot {rotation}) with {colonists.Count} colonist(s), {pets.Count} pet(s), {items.Count} item stack(s) aboard.");
        }
    }
}
