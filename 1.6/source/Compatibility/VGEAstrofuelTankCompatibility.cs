using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Gravship_Raids
{
    // Vanilla Gravship Expanded's "VanillaChemfuelTanks" patch fully replaces vanilla ChemfuelTank's and
    // LargeChemfuelTank's CompProperties_Refuelable with PipeSystem.CompProperties_ResourceStorage
    // (comps Inherit="False"), so its astrofuel tanks no longer carry a CompRefuelable at all.
    // RandomizeFuelTankLevels's TryGetComp<CompRefuelable>() therefore silently skips every astrofuel
    // tank in a spawned raid gravship, leaving flight-fuel facilities registered (via vanilla
    // CompGravshipFacility.providesFuel) but empty.
    //
    // There's no compile-time reference to VEF's PipeSystem assembly in this project, so the storage
    // comp, its capacity field, and its AddResource(float) method are all resolved by reflection, gated
    // on the type actually being loaded.
    internal static class VGEAstrofuelTankCompatibility
    {
        private static readonly Type ResourceStorageType = AccessTools.TypeByName("PipeSystem.CompResourceStorage");

        private static readonly MethodInfo AddResourceMethod = (ResourceStorageType != null)
            ? AccessTools.Method(ResourceStorageType, "AddResource", new[] { typeof(float) })
            : null;

        private static readonly FieldInfo StorageCapacityField = (ResourceStorageType != null)
            ? AccessTools.Field(AccessTools.TypeByName("PipeSystem.CompProperties_ResourceStorage"), "storageCapacity")
            : null;

        private static bool Available => ResourceStorageType != null && AddResourceMethod != null && StorageCapacityField != null;

        internal static bool TryRandomizeFuelLevel(Thing thing)
        {
            if (!Available || !(thing is ThingWithComps thingWithComps))
            {
                return false;
            }

            // Only fill storages the vanilla gravship-facility system itself marks as flight fuel; this
            // keeps the fallback scoped to fuel tanks and out of unrelated pipe-network storage buildings.
            if (thing.TryGetComp<CompGravshipFacility>()?.Props?.providesFuel != true)
            {
                return false;
            }

            ThingComp storageComp = null;
            foreach (ThingComp comp in thingWithComps.AllComps)
            {
                if (ResourceStorageType.IsInstanceOfType(comp))
                {
                    storageComp = comp;
                    break;
                }
            }
            if (storageComp == null)
            {
                return false;
            }

            if (!(StorageCapacityField.GetValue(storageComp.props) is float capacity) || capacity <= 0f)
            {
                return false;
            }

            try
            {
                AddResourceMethod.Invoke(storageComp, new object[] { Rand.Range(0.2f, 1f) * capacity });
            }
            catch (Exception ex)
            {
                Log.Warning($"[Gravship Raids] VGE astrofuel compatibility: failed to refuel '{thing}' via reflection. {ex}");
                return false;
            }

            Logger.Message($"VGEAstrofuelTankCompatibility.TryRandomizeFuelLevel: refueled '{thing}' via reflection (PipeSystem.CompResourceStorage).");
            return true;
        }
    }
}
