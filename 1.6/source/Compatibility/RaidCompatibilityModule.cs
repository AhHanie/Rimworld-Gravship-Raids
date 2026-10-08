using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Gravship_Raids
{
    internal enum RaidSettingsScope
    {
        GravshipOnly,
        ShuttleOnly,
        Both
    }

    internal abstract class RaidCompatibilityModule
    {
        public abstract bool IsActive { get; }

        public virtual bool HasSettings => false;

        public virtual RaidSettingsScope SettingsScope => RaidSettingsScope.GravshipOnly;

        public virtual void Install(Harmony harmony)
        {
        }

        public virtual void ExposeSettings()
        {
        }

        public virtual void DrawSettings(Listing_Standard listing)
        {
        }

        public virtual void OnRaidPawnsArriving(IEnumerable<Pawn> pawns, Map map)
        {
        }

        public virtual IDisposable BeginRaidPrefabSpawn()
        {
            return null;
        }

        public virtual bool TryRandomizeFuelLevel(Thing thing)
        {
            return false;
        }
    }
}
