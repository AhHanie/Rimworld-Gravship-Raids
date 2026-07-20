using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Gravship_Raids
{
    public static class ModSettingsWindow
    {
        private static Vector2 globalFactionScrollPosition = Vector2.zero;

        private static Vector2 settingsScrollPosition = Vector2.zero;

        private static float settingsViewHeight = 1000f;

        public static void Draw(Rect inRect)
        {
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, settingsViewHeight);
            Widgets.BeginScrollView(inRect, ref settingsScrollPosition, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);
            // Some sections (e.g. the global faction filter list) are only visible conditionally and can push
            // total content past the fixed mod-settings window height. Without this, GetRect silently starts a
            // new column off to the side (effectively off-screen) instead of just growing downward, which made
            // later checkboxes disappear instead of being pushed down. The scroll view above is what actually
            // makes the extra height reachable.
            listing.maxOneColumn = true;

            listing.CheckboxLabeled("GravshipRaids.Settings.Enable".Translate(), ref GravshipRaidsSettings.enabled, "GravshipRaids.Settings.EnableDesc".Translate());

            if (!ModsConfig.OdysseyActive)
            {
                GUI.color = Color.red;
                listing.Label("GravshipRaids.Settings.OdysseyMissing".Translate());
                GUI.color = Color.white;
            }

            listing.GapLine();

            listing.Label("GravshipRaids.Settings.IncidentWeight".Translate(GravshipRaidsSettings.incidentWeightFactor.ToString("0.00")));
            GravshipRaidsSettings.incidentWeightFactor = listing.Slider(GravshipRaidsSettings.incidentWeightFactor, 0f, 3f);

            listing.Label("GravshipRaids.Settings.CasualtyRetreatThreshold".Translate(GravshipRaidsSettings.casualtyRetreatThreshold.ToStringPercent()));
            GravshipRaidsSettings.casualtyRetreatThreshold = listing.Slider(GravshipRaidsSettings.casualtyRetreatThreshold, 0.05f, 1f);

            TextFieldNumericLabeledLeft(listing, "GravshipRaids.Settings.MinThreatPoints".Translate(), ref GravshipRaidsSettings.minThreatPoints, ref GravshipRaidsSettings.minThreatPointsBuffer, 0f, 100000f);

            float maxShipsFloat = GravshipRaidsSettings.maxConcurrentShipsPerMap;
            listing.Label("GravshipRaids.Settings.MaxConcurrentShips".Translate(GravshipRaidsSettings.maxConcurrentShipsPerMap.ToString()));
            maxShipsFloat = listing.Slider(maxShipsFloat, 1f, 5f);
            GravshipRaidsSettings.maxConcurrentShipsPerMap = Mathf.RoundToInt(maxShipsFloat);

            TextFieldNumericLabeledLeft(listing, "GravshipRaids.Settings.MinColonistCount".Translate(), ref GravshipRaidsSettings.minColonistCount, ref GravshipRaidsSettings.minColonistCountBuffer, 0, 20);

            if (listing.ButtonTextLabeled("GravshipRaids.Settings.MinEnemyFactionTechLevel".Translate(), GravshipRaidsSettings.minEnemyFactionTechLevel.ToStringHuman().CapitalizeFirst()))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (TechLevel level in Enum.GetValues(typeof(TechLevel)))
                {
                    if (level == TechLevel.Undefined)
                    {
                        continue;
                    }
                    TechLevel capturedLevel = level;
                    options.Add(new FloatMenuOption(level.ToStringHuman().CapitalizeFirst(), () => GravshipRaidsSettings.minEnemyFactionTechLevel = capturedLevel));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.GapLine();
            listing.CheckboxLabeled("GravshipRaids.Settings.EnableRaidshipEffects".Translate(), ref GravshipRaidsSettings.enableRaidshipEffects, "GravshipRaids.Settings.EnableRaidshipEffectsDesc".Translate());
            listing.CheckboxLabeled(
                "GravshipRaids.Settings.HardcoreEnemyDepartureDestroysUnguardedMaps".Translate(),
                ref GravshipRaidsSettings.hardcoreEnemyDepartureDestroysUnguardedMaps,
                "GravshipRaids.Settings.HardcoreEnemyDepartureDestroysUnguardedMapsDesc".Translate());

            listing.CheckboxLabeled(
                "GravshipRaids.Settings.EnableGravshipGuards".Translate(),
                ref GravshipRaidsSettings.enableGravshipGuards,
                "GravshipRaids.Settings.EnableGravshipGuardsDesc".Translate());
            if (GravshipRaidsSettings.enableGravshipGuards)
            {
                listing.Label("GravshipRaids.Settings.GravshipGuardFraction".Translate(GravshipRaidsSettings.gravshipGuardFraction.ToStringPercent()));
                GravshipRaidsSettings.gravshipGuardFraction = Mathf.Clamp(
                    listing.Slider(GravshipRaidsSettings.gravshipGuardFraction, 0f, GravshipRaidsSettings.MaxGravshipGuardFraction),
                    0f,
                    GravshipRaidsSettings.MaxGravshipGuardFraction);
            }

            listing.GapLine();
            listing.CheckboxLabeled("GravshipRaids.Settings.EnableMinPlayerTechLevel".Translate(), ref GravshipRaidsSettings.enableMinPlayerTechLevel, "GravshipRaids.Settings.EnableMinPlayerTechLevelDesc".Translate());
            if (GravshipRaidsSettings.enableMinPlayerTechLevel)
            {
                if (listing.ButtonTextLabeled("GravshipRaids.Settings.MinPlayerTechLevel".Translate(), GravshipRaidsSettings.minPlayerTechLevel.ToStringHuman().CapitalizeFirst()))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    foreach (TechLevel level in Enum.GetValues(typeof(TechLevel)))
                    {
                        if (level == TechLevel.Undefined)
                        {
                            continue;
                        }
                        TechLevel capturedLevel = level;
                        options.Add(new FloatMenuOption(level.ToStringHuman().CapitalizeFirst(), () => GravshipRaidsSettings.minPlayerTechLevel = capturedLevel));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            }

            listing.GapLine();
            listing.CheckboxLabeled("GravshipRaids.Settings.EnableGlobalFactionFilter".Translate(), ref GravshipRaidsSettings.globalFactionFilterEnabled, "GravshipRaids.Settings.EnableGlobalFactionFilterDesc".Translate());
            if (GravshipRaidsSettings.globalFactionFilterEnabled)
            {
                DrawGlobalFactionFilter(listing);
            }

            listing.GapLine();
            listing.CheckboxLabeled("GravshipRaids.Settings.DebugLogging".Translate(), ref GravshipRaidsSettings.debugLogging, "GravshipRaids.Settings.DebugLoggingDesc".Translate());

            if (Event.current.type == EventType.Layout)
            {
                settingsViewHeight = listing.CurHeight + 30f;
            }
            listing.End();
            Widgets.EndScrollView();
        }

        private static void DrawGlobalFactionFilter(Listing_Standard listing)
        {
            if (listing.ButtonText("GravshipRaids.Settings.AddExcludedFaction".Translate()))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (FactionDef factionDef in DefDatabase<FactionDef>.AllDefsListForReading)
                {
                    if (!factionDef.humanlikeFaction || GravshipRaidsSettings.globalDisallowedFactionDefNames.Contains(factionDef.defName))
                    {
                        continue;
                    }
                    FactionDef captured = factionDef;
                    options.Add(new FloatMenuOption(captured.LabelCap, () => GravshipRaidsSettings.globalDisallowedFactionDefNames.Add(captured.defName)));
                }
                if (options.Count == 0)
                {
                    options.Add(new FloatMenuOption("GravshipRaids.Settings.NoFactionsAvailable".Translate(), null));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            const float rowHeight = 26f;
            Rect outerRect = listing.GetRect(150f);
            Widgets.DrawBoxSolid(outerRect, new Color(0f, 0f, 0f, 0.1f));
            Rect viewRect = new Rect(0f, 0f, outerRect.width - 16f, Mathf.Max(GravshipRaidsSettings.globalDisallowedFactionDefNames.Count * rowHeight, outerRect.height));
            Widgets.BeginScrollView(outerRect, ref globalFactionScrollPosition, viewRect);
            string toRemove = null;
            float y = 0f;
            foreach (string defName in GravshipRaidsSettings.globalDisallowedFactionDefNames)
            {
                FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(defName);
                string label = factionDef != null ? factionDef.LabelCap.Resolve() : defName;
                Rect rowRect = new Rect(0f, y, viewRect.width, rowHeight);
                Rect removeRect = new Rect(rowRect.xMax - rowHeight, rowRect.y + 1f, rowHeight - 2f, rowHeight - 2f);
                Widgets.Label(new Rect(rowRect.x + 4f, rowRect.y, rowRect.width - rowHeight - 4f, rowRect.height), label);
                if (Widgets.ButtonText(removeRect, "X"))
                {
                    toRemove = defName;
                }
                y += rowHeight;
            }
            Widgets.EndScrollView();
            if (toRemove != null)
            {
                GravshipRaidsSettings.globalDisallowedFactionDefNames.Remove(toRemove);
            }
            listing.Gap(listing.verticalSpacing);
        }
        private static void TextFieldNumericLabeledLeft<T>(Listing_Standard listing, string label, ref T val, ref string buffer, float min, float max) where T : struct
        {
            Rect rect = listing.GetRect(Text.LineHeight);
            Rect fieldRect = rect.RightHalf().Rounded();
            Widgets.Label(rect, label);
            Widgets.TextFieldNumeric(fieldRect, ref val, ref buffer, min, max);
            listing.Gap(listing.verticalSpacing);
        }
    }
}
