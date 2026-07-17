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

        public static void Draw(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

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

            listing.End();
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
