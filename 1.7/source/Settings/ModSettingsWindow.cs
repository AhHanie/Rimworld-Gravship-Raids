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

        private static Vector2 shuttleFactionScrollPosition = Vector2.zero;

        private static Vector2 gravshipScrollPosition = Vector2.zero;

        private static float gravshipViewHeight = 1000f;

        private static Vector2 shuttleScrollPosition = Vector2.zero;

        private static float shuttleViewHeight = 1000f;

        private static SettingsTab selectedTab = SettingsTab.Gravship;

        private enum SettingsTab
        {
            Gravship,
            Shuttle
        }

        public static void Draw(Rect inRect)
        {
            Rect frame = new Rect(inRect.x, inRect.y + TabDrawer.TabHeight, inRect.width, inRect.height - TabDrawer.TabHeight);
            Widgets.DrawMenuSection(frame);

            List<TabRecord> tabs = new List<TabRecord>
            {
                new TabRecord("GravshipRaids.Settings.GravshipTab".Translate(), () => selectedTab = SettingsTab.Gravship, () => selectedTab == SettingsTab.Gravship),
                new TabRecord("GravshipRaids.Settings.ShuttleSectionHeader".Translate(), () => selectedTab = SettingsTab.Shuttle, () => selectedTab == SettingsTab.Shuttle)
            };
            TabDrawer.DrawTabs(frame, tabs);

            Rect viewport = frame.ContractedBy(8f);
            if (selectedTab == SettingsTab.Gravship)
            {
                DrawPage(viewport, ref gravshipScrollPosition, ref gravshipViewHeight, DrawGravshipRaidSettings);
            }
            else
            {
                DrawPage(viewport, ref shuttleScrollPosition, ref shuttleViewHeight, DrawShuttleRaidSettings);
            }
        }

        private static void DrawPage(Rect viewport, ref Vector2 scrollPosition, ref float viewHeight, Action<Listing_Standard> drawContents)
        {
            Rect viewRect = new Rect(0f, 0f, viewport.width - 16f, viewHeight);
            Widgets.BeginScrollView(viewport, ref scrollPosition, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);
            // Some sections (e.g. the global faction filter list) are only visible conditionally and can push
            // total content past the fixed mod-settings window height. Without this, GetRect silently starts a
            // new column off to the side (effectively off-screen) instead of just growing downward, which made
            // later checkboxes disappear instead of being pushed down. The scroll view above is what actually
            // makes the extra height reachable.
            listing.maxOneColumn = true;

            drawContents(listing);
            DrawSharedSettings(listing);

            if (Event.current.type == EventType.Layout)
            {
                viewHeight = listing.CurHeight + 30f;
                scrollPosition.y = Mathf.Clamp(scrollPosition.y, 0f, Mathf.Max(0f, viewHeight - viewport.height));
            }
            listing.End();
            Widgets.EndScrollView();
        }

        private static void DrawSharedSettings(Listing_Standard listing)
        {
            listing.GapLine();
            listing.Label("GravshipRaids.Settings.SharedSectionHeader".Translate());
            listing.CheckboxLabeled("GravshipRaids.Settings.DebugLogging".Translate(), ref GravshipRaidsSettings.debugLogging, "GravshipRaids.Settings.DebugLoggingDesc".Translate());
            RaidCompatibility.DrawSettings(listing, RaidSettingsScope.Both);
        }

        private static void DrawGravshipRaidSettings(Listing_Standard listing)
        {
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
                "GravshipRaids.Settings.AllowEnemyGravcoreDrops".Translate(),
                ref GravshipRaidsSettings.allowEnemyGravcoreDrops,
                "GravshipRaids.Settings.AllowEnemyGravcoreDropsDesc".Translate());
            listing.CheckboxLabeled(
                "GravshipRaids.Settings.HardcoreEnemyDepartureDestroysUnguardedMaps".Translate(),
                ref GravshipRaidsSettings.hardcoreEnemyDepartureDestroysUnguardedMaps,
                "GravshipRaids.Settings.HardcoreEnemyDepartureDestroysUnguardedMapsDesc".Translate());
            listing.CheckboxLabeled(
                "GravshipRaids.Settings.AllowEmptyEnemyGravshipDeparture".Translate(),
                ref GravshipRaidsSettings.allowEmptyEnemyGravshipDeparture,
                "GravshipRaids.Settings.AllowEmptyEnemyGravshipDepartureDesc".Translate());
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

            RaidCompatibility.DrawSettings(listing, RaidSettingsScope.GravshipOnly);
        }

        private static void DrawShuttleRaidSettings(Listing_Standard listing)
        {
            if (!ModsConfig.RoyaltyActive)
            {
                GUI.color = Color.red;
                listing.Label("GravshipRaids.Settings.RoyaltyMissing".Translate());
                GUI.color = Color.white;
                return;
            }

            listing.CheckboxLabeled("GravshipRaids.Settings.EnableShuttleRaids".Translate(), ref GravshipRaidsSettings.enableShuttleRaids, "GravshipRaids.Settings.EnableShuttleRaidsDesc".Translate());

            if (!GravshipRaidsSettings.enableShuttleRaids)
            {
                return;
            }

            listing.Label("GravshipRaids.Settings.ShuttleIncidentWeight".Translate(GravshipRaidsSettings.shuttleIncidentWeightFactor.ToString("0.00")));
            GravshipRaidsSettings.shuttleIncidentWeightFactor = listing.Slider(GravshipRaidsSettings.shuttleIncidentWeightFactor, 0f, 3f);

            listing.Label("GravshipRaids.Settings.ShuttleCasualtyRetreatThreshold".Translate(GravshipRaidsSettings.shuttleCasualtyRetreatThreshold.ToStringPercent()));
            GravshipRaidsSettings.shuttleCasualtyRetreatThreshold = listing.Slider(GravshipRaidsSettings.shuttleCasualtyRetreatThreshold, 0.05f, 1f);

            TextFieldNumericLabeledLeft(listing, "GravshipRaids.Settings.ShuttleMinThreatPoints".Translate(), ref GravshipRaidsSettings.shuttleMinThreatPoints, ref GravshipRaidsSettings.shuttleMinThreatPointsBuffer, 0f, 100000f);

            float maxConcurrentFloat = GravshipRaidsSettings.shuttleMaxConcurrentPerMap;
            listing.Label("GravshipRaids.Settings.ShuttleMaxConcurrentPerMap".Translate(GravshipRaidsSettings.shuttleMaxConcurrentPerMap.ToString()));
            maxConcurrentFloat = listing.Slider(maxConcurrentFloat, 1f, 5f);
            GravshipRaidsSettings.shuttleMaxConcurrentPerMap = Mathf.RoundToInt(maxConcurrentFloat);

            TextFieldNumericLabeledLeft(listing, "GravshipRaids.Settings.ShuttleMinColonistCount".Translate(), ref GravshipRaidsSettings.shuttleMinColonistCount, ref GravshipRaidsSettings.shuttleMinColonistCountBuffer, 0, 20);

            if (listing.ButtonTextLabeled("GravshipRaids.Settings.ShuttleMinEnemyFactionTechLevel".Translate(), GravshipRaidsSettings.shuttleMinEnemyFactionTechLevel.ToStringHuman().CapitalizeFirst()))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (TechLevel level in Enum.GetValues(typeof(TechLevel)))
                {
                    if (level == TechLevel.Undefined)
                    {
                        continue;
                    }
                    TechLevel capturedLevel = level;
                    options.Add(new FloatMenuOption(level.ToStringHuman().CapitalizeFirst(), () => GravshipRaidsSettings.shuttleMinEnemyFactionTechLevel = capturedLevel));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.CheckboxLabeled("GravshipRaids.Settings.ShuttleEnableMinPlayerTechLevel".Translate(), ref GravshipRaidsSettings.shuttleEnableMinPlayerTechLevel, "GravshipRaids.Settings.ShuttleEnableMinPlayerTechLevelDesc".Translate());
            if (GravshipRaidsSettings.shuttleEnableMinPlayerTechLevel)
            {
                if (listing.ButtonTextLabeled("GravshipRaids.Settings.ShuttleMinPlayerTechLevel".Translate(), GravshipRaidsSettings.shuttleMinPlayerTechLevel.ToStringHuman().CapitalizeFirst()))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    foreach (TechLevel level in Enum.GetValues(typeof(TechLevel)))
                    {
                        if (level == TechLevel.Undefined)
                        {
                            continue;
                        }
                        TechLevel capturedLevel = level;
                        options.Add(new FloatMenuOption(level.ToStringHuman().CapitalizeFirst(), () => GravshipRaidsSettings.shuttleMinPlayerTechLevel = capturedLevel));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            }

            listing.CheckboxLabeled("GravshipRaids.Settings.ShuttleEnableGlobalFactionFilter".Translate(), ref GravshipRaidsSettings.shuttleGlobalFactionFilterEnabled, "GravshipRaids.Settings.ShuttleEnableGlobalFactionFilterDesc".Translate());
            if (GravshipRaidsSettings.shuttleGlobalFactionFilterEnabled)
            {
                DrawShuttleFactionFilter(listing);
            }

            RaidCompatibility.DrawSettings(listing, RaidSettingsScope.ShuttleOnly);
        }

        private static void DrawShuttleFactionFilter(Listing_Standard listing)
        {
            if (listing.ButtonText("GravshipRaids.Settings.AddExcludedFaction".Translate()))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (FactionDef factionDef in DefDatabase<FactionDef>.AllDefsListForReading)
                {
                    if (!factionDef.humanlikeFaction || GravshipRaidsSettings.shuttleDisallowedFactionDefNames.Contains(factionDef.defName))
                    {
                        continue;
                    }
                    FactionDef captured = factionDef;
                    options.Add(new FloatMenuOption(captured.LabelCap, () => GravshipRaidsSettings.shuttleDisallowedFactionDefNames.Add(captured.defName)));
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
            Rect viewRect = new Rect(0f, 0f, outerRect.width - 16f, Mathf.Max(GravshipRaidsSettings.shuttleDisallowedFactionDefNames.Count * rowHeight, outerRect.height));
            Widgets.BeginScrollView(outerRect, ref shuttleFactionScrollPosition, viewRect);
            string toRemove = null;
            float y = 0f;
            foreach (string defName in GravshipRaidsSettings.shuttleDisallowedFactionDefNames)
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
                GravshipRaidsSettings.shuttleDisallowedFactionDefNames.Remove(toRemove);
            }
            listing.Gap(listing.verticalSpacing);
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
