using HarmonyLib;
using RimWorld;
using Verse;

namespace Gravship_Raids
{
    [HarmonyPatch(typeof(Skyfaller), "SpawnThings")]
    internal static class ShuttleImpactGuardPatch
    {
        private static bool Prefix(Skyfaller __instance)
        {
            EnemyShuttleRaidInstance instance = FindInstance(__instance, requireUnspawned: true);
            if (instance == null)
            {
                return true;
            }

            Map map = __instance.Map;
            Thing shuttle = instance.shuttle;
            IntVec3 position = __instance.Position;
            Rot4 rotation = shuttle.Rotation;

            if (ShuttleRaidTemplateUtility.IsSiteStillValidAtImpact(instance.template, position, map, rotation, out string reason))
            {
                instance.landingCell = position;
                return true;
            }

            Logger.Warning($"ShuttleImpactGuardPatch: {instance} planned site {position} (rot {rotation}) became invalid before impact: {reason}");

            if (ShuttleRaidTemplateUtility.TryFindRelocatedSite(instance.template, map, position, rotation, ShuttleRaidTemplateUtility.RelocationRadius, out IntVec3 newRoot, out Rot4 newRotation))
            {
                __instance.innerContainer.Remove(shuttle);
                shuttle.Rotation = newRotation;
                GenSpawn.Spawn(shuttle, newRoot, map, newRotation);
                if (shuttle.Spawned)
                {
                    instance.landingCell = newRoot;
                    Logger.Message($"ShuttleImpactGuardPatch: {instance} relocated its landing from {position} to {newRoot} (rot {newRotation}).");
                    return false;
                }
                Logger.Error($"ShuttleImpactGuardPatch: {instance} failed to spawn its shuttle at relocated site {newRoot}.");
                shuttle.holdingOwner?.Remove(shuttle);
                __instance.innerContainer.TryAdd(shuttle);
            }

            EnemyShuttleRaidUtility.RecoverUnlandedShuttle(instance, map, position, "no valid landing site at impact: " + reason);
            return false;
        }

        private static void Postfix(Skyfaller __instance)
        {
            EnemyShuttleRaidInstance instance = FindInstance(__instance, requireUnspawned: true);
            if (instance == null)
            {
                return;
            }
            EnemyShuttleRaidUtility.RecoverUnlandedShuttle(instance, __instance.Map, __instance.Position, "vanilla skyfaller placement did not spawn the shuttle");
        }

        private static EnemyShuttleRaidInstance FindInstance(Skyfaller skyfaller, bool requireUnspawned)
        {
            Map map = skyfaller.Map;
            MapComponent_ShuttleRaid component = MapComponent_ShuttleRaid.GetFor(map);
            if (component == null || component.Instances.Count == 0)
            {
                return null;
            }
            ThingOwner container = skyfaller.innerContainer;
            for (int i = 0; i < component.Instances.Count; i++)
            {
                EnemyShuttleRaidInstance instance = component.Instances[i];
                Thing shuttle = instance?.shuttle;
                if (shuttle == null || shuttle.holdingOwner != container)
                {
                    continue;
                }
                if (requireUnspawned && shuttle.Spawned)
                {
                    continue;
                }
                if (instance.state == ShuttleRaidState.Lost || instance.state == ShuttleRaidState.Departed)
                {
                    continue;
                }
                return instance;
            }
            return null;
        }
    }
}
