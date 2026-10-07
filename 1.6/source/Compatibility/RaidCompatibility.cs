using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Gravship_Raids
{
    internal static class RaidCompatibility
    {
        private static readonly List<RaidCompatibilityModule> AllModules = DiscoverModules();

        private static List<RaidCompatibilityModule> activeModules;

        private static List<RaidCompatibilityModule> ActiveModules
        {
            get
            {
                if (activeModules == null)
                {
                    activeModules = AllModules.Where((RaidCompatibilityModule m) => SafeIsActive(m)).ToList();
                }
                return activeModules;
            }
        }

        internal static void Initialize(Harmony harmony)
        {
            foreach (RaidCompatibilityModule module in ActiveModules)
            {
                try
                {
                    module.Install(harmony);
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, $"RaidCompatibility.Initialize ({module.GetType().Name})");
                }
            }
        }

        internal static void ExposeSettings()
        {
            foreach (RaidCompatibilityModule module in AllModules)
            {
                module.ExposeSettings();
            }
        }

        internal static void DrawSettings(Listing_Standard listing)
        {
            foreach (RaidCompatibilityModule module in ActiveModules)
            {
                if (!module.HasSettings)
                {
                    continue;
                }
                listing.GapLine();
                module.DrawSettings(listing);
            }
        }

        internal static void OnRaidPawnsArriving(IEnumerable<Pawn> pawns, Map map)
        {
            foreach (RaidCompatibilityModule module in ActiveModules)
            {
                try
                {
                    module.OnRaidPawnsArriving(pawns, map);
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, $"RaidCompatibility.OnRaidPawnsArriving ({module.GetType().Name})");
                }
            }
        }

        internal static IDisposable BeginRaidPrefabSpawn()
        {
            List<IDisposable> scopes = new List<IDisposable>();
            foreach (RaidCompatibilityModule module in ActiveModules)
            {
                IDisposable scope = module.BeginRaidPrefabSpawn();
                if (scope != null)
                {
                    scopes.Add(scope);
                }
            }
            return new CompositeScope(scopes);
        }

        internal static bool TryRandomizeFuelLevel(Thing thing)
        {
            foreach (RaidCompatibilityModule module in ActiveModules)
            {
                try
                {
                    if (module.TryRandomizeFuelLevel(thing))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, $"RaidCompatibility.TryRandomizeFuelLevel ({module.GetType().Name})");
                }
            }
            return false;
        }

        private static List<RaidCompatibilityModule> DiscoverModules()
        {
            List<RaidCompatibilityModule> result = new List<RaidCompatibilityModule>();
            foreach (Type type in typeof(RaidCompatibilityModule).AllSubclassesNonAbstract().OrderBy((Type t) => t.Name, StringComparer.Ordinal))
            {
                try
                {
                    result.Add((RaidCompatibilityModule)Activator.CreateInstance(type));
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, $"RaidCompatibility.DiscoverModules ({type.Name})");
                }
            }
            return result;
        }

        private static bool SafeIsActive(RaidCompatibilityModule module)
        {
            try
            {
                return module.IsActive;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, $"RaidCompatibility.IsActive ({module.GetType().Name})");
                return false;
            }
        }

        private sealed class CompositeScope : IDisposable
        {
            private readonly List<IDisposable> scopes;

            public CompositeScope(List<IDisposable> scopes)
            {
                this.scopes = scopes;
            }

            public void Dispose()
            {
                for (int i = scopes.Count - 1; i >= 0; i--)
                {
                    scopes[i].Dispose();
                }
                scopes.Clear();
            }
        }
    }
}
