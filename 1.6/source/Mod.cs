using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace Gravship_Raids
{
    public class Mod : Verse.Mod
    {
        public Mod(ModContentPack content) : base(content)
        {
            LongEventHandler.QueueLongEvent(Init, "GravshipRaids.LoadingLabel", doAsynchronously: true, null);
        }

        private void Init()
        {
            Harmony harmony = new Harmony("sk.gravshipraids");
            harmony.PatchAll();

            GetSettings<GravshipRaidsSettings>();
            GravshipRaidTemplateUtility.PopulateCoreCellCache();
            GravshipRaidsSettings.PruneInvalidGlobalFactionEntries();
            GravshipRaidsSettings.PruneInvalidShuttleFactionEntries();

            if (ModsConfig.IsActive("VanillaStorytellersExpanded.WinstonWave"))
            {
                WinstonWavesCompatibility.TryInstall(harmony);
            }

            if (!ModsConfig.OdysseyActive)
            {
                Log.Error("[Gravship Raids] The Odyssey DLC was not detected. Gravship Raids requires Odyssey - gravship raid incidents will remain inactive until Odyssey is enabled.");
            }
            else
            {
                Logger.Message("Initialized. Odyssey detected; gravship raid content is available.");
            }

            if (!ModsConfig.RoyaltyActive)
            {
                Log.Warning("[Gravship Raids] The Royalty DLC was not detected. Shuttle raids are an optional feature that requires Royalty (for CompShuttle/TransportShipDef) and will remain unavailable until Royalty is enabled. Gravship raids are unaffected.");
            }
            else
            {
                Logger.Message("Royalty detected; shuttle raid content is available.");
            }
        }

        public override string SettingsCategory()
        {
            return "GravshipRaids.Settings.Category".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            ModSettingsWindow.Draw(inRect);
            base.DoSettingsWindowContents(inRect);
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            Logger.Message("Settings saved.");
        }
    }
}
