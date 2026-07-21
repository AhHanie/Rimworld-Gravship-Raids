using System.Collections.Generic;
using LudeonTK;
using Verse;

namespace Gravship_Raids
{
    [StaticConstructorOnStartup]
    public static class DebugActionsGravshipRaidTemplates
    {
        [DebugAction("Gravship Raids", "Rotate gravship template spawn", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RotateTemplateSpawn()
        {
            GravshipRaidDebugApi.RotateTemplateSpawn();
        }

        [DebugAction("Gravship Raids", "Spawn gravship template...", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static List<DebugActionNode> SpawnTemplate()
        {
            List<DebugActionNode> list = new List<DebugActionNode>();
            foreach (GravshipRaidTemplateDef template in GravshipRaidDebugApi.GetTemplates())
            {
                GravshipRaidTemplateDef localTemplate = template;
                bool valid = GravshipRaidTemplateUtility.IsValidTemplate(localTemplate);
                string label = localTemplate.defName + (valid ? string.Empty : " [INVALID]") + (localTemplate.disabled ? " [DISABLED]" : string.Empty);
                list.Add(new DebugActionNode(label, DebugActionType.ToolMap)
                {
                    action = delegate
                    {
                        GravshipRaidDebugApi.SpawnTemplateAt(localTemplate, Find.CurrentMap, UI.MouseCell());
                    }
                });
            }
            return list;
        }

        [DebugAction("Gravship Raids", "Test gravship landing search", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TestLandingSearch()
        {
            GravshipRaidDebugApi.TestLandingSearch(Find.CurrentMap);
        }

        [DebugAction("Gravship Raids", "Force gravship raid here", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.PlayingOnMap, actionType = DebugActionType.ToolMap)]
        private static void ForceGravshipRaid()
        {
            GravshipRaidDebugApi.ForceGravshipRaidAt(Find.CurrentMap, UI.MouseCell());
        }

        [DebugAction("Gravship Raids", "Force enemy gravship departure", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForceEnemyGravshipDeparture()
        {
            GravshipRaidDebugApi.ForceEnemyGravshipDeparture(Find.CurrentMap);
        }
    }
}
