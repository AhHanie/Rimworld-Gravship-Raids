using System.Collections.Generic;
using LudeonTK;
using Verse;

namespace Gravship_Raids
{
    [StaticConstructorOnStartup]
    public static class DebugActionsShuttleRaidTemplates
    {
        [DebugAction("Gravship Raids", "Rotate shuttle template spawn", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RotateShuttleTemplateSpawn()
        {
            GravshipRaidDebugApi.RotateShuttleTemplateSpawn();
        }

        [DebugAction("Gravship Raids", "Spawn shuttle template...", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static List<DebugActionNode> SpawnShuttleTemplate()
        {
            List<DebugActionNode> list = new List<DebugActionNode>();
            foreach (ShuttleRaidTemplateDef template in GravshipRaidDebugApi.GetShuttleTemplates())
            {
                ShuttleRaidTemplateDef localTemplate = template;
                bool valid = ShuttleRaidTemplateUtility.IsValidTemplate(localTemplate);
                string label = localTemplate.defName + (valid ? string.Empty : " [INVALID]") + (localTemplate.disabled ? " [DISABLED]" : string.Empty);
                list.Add(new DebugActionNode(label, DebugActionType.ToolMap)
                {
                    action = delegate
                    {
                        GravshipRaidDebugApi.SpawnShuttleTemplateAt(localTemplate, Find.CurrentMap, UI.MouseCell());
                    }
                });
            }
            return list;
        }

        [DebugAction("Gravship Raids", "Force shuttle raid here", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.ToolMap)]
        private static void ForceShuttleRaid()
        {
            GravshipRaidDebugApi.ForceShuttleRaidAt(Find.CurrentMap, UI.MouseCell());
        }

        [DebugAction("Gravship Raids", "Force shuttle raid with template...", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static List<DebugActionNode> ForceShuttleRaidWithTemplate()
        {
            List<DebugActionNode> list = new List<DebugActionNode>();
            foreach (ShuttleRaidTemplateDef template in GravshipRaidDebugApi.GetShuttleTemplates())
            {
                ShuttleRaidTemplateDef localTemplate = template;
                bool valid = ShuttleRaidTemplateUtility.IsValidTemplate(localTemplate);
                string label = localTemplate.defName + (valid ? string.Empty : " [INVALID]") + (localTemplate.disabled ? " [DISABLED]" : string.Empty);
                list.Add(new DebugActionNode(label, DebugActionType.ToolMap)
                {
                    action = delegate
                    {
                        GravshipRaidDebugApi.ForceShuttleRaidAt(localTemplate, Find.CurrentMap, UI.MouseCell());
                    }
                });
            }
            return list;
        }

        [DebugAction("Gravship Raids", "Force enemy shuttle departure", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForceEnemyShuttleDeparture()
        {
            GravshipRaidDebugApi.ForceEnemyShuttleDeparture(Find.CurrentMap);
        }
    }
}
