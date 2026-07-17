using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            GetSettings<GravshipRaidsSettings>();
            GravshipRaidTemplateUtility.PopulateCoreCellCache();
            GravshipRaidsSettings.PruneInvalidGlobalFactionEntries();

            if (!ModsConfig.OdysseyActive)
            {
                Log.Error("[Gravship Raids] The Odyssey DLC was not detected. Gravship Raids requires Odyssey - gravship raid incidents will remain inactive until Odyssey is enabled.");
            }
            else
            {
                Logger.Message("Initialized. Odyssey detected; gravship raid content is available.");
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
