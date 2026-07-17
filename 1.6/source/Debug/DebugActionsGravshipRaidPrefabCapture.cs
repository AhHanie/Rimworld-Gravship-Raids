using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace Gravship_Raids
{
    [StaticConstructorOnStartup]
    public static class DebugActionsGravshipRaidPrefabCapture
    {
        [DebugAction("Gravship Raids", "Capture prefab with terrain...", false, false, false, false, false, 0, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CapturePrefabWithTerrain()
        {
            DebugToolsGeneral.GenericRectTool("Capture", delegate(CellRect rect)
            {
                try
                {
                    PrefabDef prefab = PrefabUtility.CreatePrefab(rect, copyAllThings: true, copyTerrain: true);
                    HashSet<IntVec3> substructureCells = prefab.GetTerrain().Where((t) => t.data.def.isFoundation).Select((t) => t.cell).ToHashSet();
                    List<(PrefabThingData data, IntVec3 cell)> things = prefab.GetThings().Where((t) => substructureCells.Contains(t.cell)).ToList();
                    List<(PrefabTerrainData data, IntVec3 cell)> terrain = prefab.GetTerrain().Where((t) => t.data.def.isFoundation).ToList();

                    string xml = BuildPrefabXml(rect, things, terrain);
                    GUIUtility.systemCopyBuffer = xml;

                    string message = $"[Gravship Raids] Captured prefab {rect.Size.x}x{rect.Size.z} ({things.Count} thing cell(s), {terrain.Count} terrain cell(s)). Copied to clipboard - rename NewPrefab before pasting into a defs file.";
                    Log.Message(message);
                    Messages.Message(message, MessageTypeDefOf.NeutralEvent, historical: false);
                }
                catch (Exception ex)
                {
                    Log.Error($"[Gravship Raids] Prefab capture failed for rect {rect}: {ex}");
                    Messages.Message("[Gravship Raids] Prefab capture failed - see log for details.", MessageTypeDefOf.RejectInput, historical: false);
                }
            }, closeOnComplete: true);
        }

        private static string BuildPrefabXml(CellRect rect, List<(PrefabThingData data, IntVec3 cell)> things, List<(PrefabTerrainData data, IntVec3 cell)> terrain)
        {
            CellRect localRect = new CellRect(0, 0, rect.Size.x, rect.Size.z);
            StringBuilder sb = new StringBuilder();
            const string indent = "  ";
            sb.AppendLine("<PrefabDef>");
            sb.AppendLine(indent + "<defName>NewPrefab</defName> <!-- rename before use -->");
            sb.AppendLine($"{indent}<size>({rect.Size.x},{rect.Size.z})</size>");
            List<(ThingGroupKey key, List<IntVec3> cells)> thingGroups = GroupThings(things);
            if (thingGroups.Count > 0)
            {
                sb.AppendLine(indent + "<things>");
                // PrefabUtility spawns these blocks in XML order. RimWorld allows an edifice to coexist with
                // a non-edifice beneath it, but GenSpawn.CanSpawnAt rejects a non-edifice after an impassable
                // edifice has spawned. Preserve that general placement order for every captured thing type.
                foreach ((ThingGroupKey key, List<IntVec3> cells) in thingGroups.OrderBy((group) => GetPrefabSpawnOrder(group.key.def)))
                {
                    AppendThingGroup(sb, indent, localRect, key, cells);
                }
                sb.AppendLine(indent + "</things>");
            }
            List<(TerrainGroupKey key, List<IntVec3> cells)> terrainGroups = GroupTerrain(terrain);
            if (terrainGroups.Count > 0)
            {
                sb.AppendLine(indent + "<terrain>");
                foreach ((TerrainGroupKey key, List<IntVec3> cells) in terrainGroups)
                {
                    AppendTerrainGroup(sb, indent, localRect, key, cells);
                }
                sb.AppendLine(indent + "</terrain>");
            }
            sb.AppendLine("</PrefabDef>");
            return sb.ToString();
        }

        private static int GetPrefabSpawnOrder(ThingDef def)
        {
            return def != null && !def.IsEdifice() ? 0 : 1;
        }

        private static List<(ThingGroupKey key, List<IntVec3> cells)> GroupThings(List<(PrefabThingData data, IntVec3 cell)> things)
        {
            List<(ThingGroupKey key, List<IntVec3> cells)> groups = new List<(ThingGroupKey, List<IntVec3>)>();
            Dictionary<ThingGroupKey, int> indexByKey = new Dictionary<ThingGroupKey, int>();
            foreach ((PrefabThingData data, IntVec3 cell) in things)
            {
                ThingGroupKey key = new ThingGroupKey(data);
                if (!indexByKey.TryGetValue(key, out int index))
                {
                    index = groups.Count;
                    indexByKey.Add(key, index);
                    groups.Add((key, new List<IntVec3>()));
                }
                groups[index].cells.Add(cell);
            }
            return groups;
        }

        private static List<(TerrainGroupKey key, List<IntVec3> cells)> GroupTerrain(List<(PrefabTerrainData data, IntVec3 cell)> terrain)
        {
            List<(TerrainGroupKey key, List<IntVec3> cells)> groups = new List<(TerrainGroupKey, List<IntVec3>)>();
            Dictionary<TerrainGroupKey, int> indexByKey = new Dictionary<TerrainGroupKey, int>();
            foreach ((PrefabTerrainData data, IntVec3 cell) in terrain)
            {
                TerrainGroupKey key = new TerrainGroupKey(data);
                if (!indexByKey.TryGetValue(key, out int index))
                {
                    index = groups.Count;
                    indexByKey.Add(key, index);
                    groups.Add((key, new List<IntVec3>()));
                }
                groups[index].cells.Add(cell);
            }
            return groups;
        }

        private static void AppendThingGroup(StringBuilder sb, string indent, CellRect localRect, ThingGroupKey key, List<IntVec3> cells)
        {
            string tag = key.def.defName;
            sb.AppendLine(indent + indent + "<" + tag + ">");
            if (cells.Count == 1)
            {
                sb.AppendLine($"{indent}{indent}{indent}<position>{cells[0]}</position>");
            }
            else if (key.def.size == IntVec2.One)
            {
                // Multiple 1x1 cells of the same thing (e.g. repeated hull/wall segments) compress into
                // a handful of covering rects instead of one <position> per cell, same as PrefabUtility.CreatePrefab.
                HashSet<IntVec3> cellSet = cells.ToHashSet();
                sb.AppendLine(indent + indent + indent + "<rects>");
                foreach (CellRect subRect in localRect.EnumerateRectanglesCovering((IntVec3 c) => cellSet.Contains(c)))
                {
                    sb.AppendLine($"{indent}{indent}{indent}{indent}<li>{subRect}</li>");
                }
                sb.AppendLine(indent + indent + indent + "</rects>");
            }
            else
            {
                // Things larger than 1x1 can't be tiled via rects (each rect cell would place a full copy),
                // so repeats list their origins individually instead.
                sb.AppendLine(indent + indent + indent + "<positions>");
                foreach (IntVec3 cell in cells)
                {
                    sb.AppendLine($"{indent}{indent}{indent}{indent}<li>{cell}</li>");
                }
                sb.AppendLine(indent + indent + indent + "</positions>");
            }
            if (key.relativeRotation != RotationDirection.None)
            {
                sb.AppendLine(indent + indent + indent + "<relativeRotation>" + Enum.GetName(typeof(RotationDirection), key.relativeRotation) + "</relativeRotation>");
            }
            if (key.stuff != null)
            {
                sb.AppendLine(indent + indent + indent + "<stuff>" + key.stuff.defName + "</stuff>");
            }
            if (key.quality.HasValue)
            {
                sb.AppendLine($"{indent}{indent}{indent}<quality>{key.quality}</quality>");
            }
            if (key.hp != 0)
            {
                sb.AppendLine($"{indent}{indent}{indent}<hp>{key.hp}</hp>");
            }
            if (key.stackCountRange != IntRange.One)
            {
                sb.AppendLine($"{indent}{indent}{indent}<stackCountRange>{key.stackCountRange.min}~{key.stackCountRange.max}</stackCountRange>");
            }
            if (key.colorDef != null)
            {
                sb.AppendLine($"{indent}{indent}{indent}<colorDef>{key.colorDef}</colorDef>");
            }
            if (key.color != default(Color))
            {
                sb.AppendLine($"{indent}{indent}{indent}<color>{key.color}</color>");
            }
            sb.AppendLine(indent + indent + "</" + tag + ">");
        }

        private static void AppendTerrainGroup(StringBuilder sb, string indent, CellRect localRect, TerrainGroupKey key, List<IntVec3> cells)
        {
            string tag = key.def.defName;
            sb.AppendLine(indent + indent + "<" + tag + ">");
            if (key.color != null)
            {
                sb.AppendLine($"{indent}{indent}{indent}<color>{key.color}</color>");
            }
            HashSet<IntVec3> cellSet = cells.ToHashSet();
            sb.AppendLine(indent + indent + indent + "<rects>");
            foreach (CellRect subRect in localRect.EnumerateRectanglesCovering((IntVec3 c) => cellSet.Contains(c)))
            {
                sb.AppendLine($"{indent}{indent}{indent}{indent}<li>{subRect}</li>");
            }
            sb.AppendLine(indent + indent + indent + "</rects>");
            sb.AppendLine(indent + indent + "</" + tag + ">");
        }

        private readonly struct ThingGroupKey : IEquatable<ThingGroupKey>
        {
            public readonly ThingDef def;
            public readonly ThingDef stuff;
            public readonly ColorDef colorDef;
            public readonly Color color;
            public readonly QualityCategory? quality;
            public readonly int hp;
            public readonly IntRange stackCountRange;
            public readonly RotationDirection relativeRotation;

            public ThingGroupKey(PrefabThingData data)
            {
                def = data.def;
                stuff = data.stuff;
                colorDef = data.colorDef;
                color = data.color;
                quality = data.quality;
                hp = data.hp;
                stackCountRange = data.stackCountRange;
                relativeRotation = data.relativeRotation;
            }

            public bool Equals(ThingGroupKey other)
            {
                return def == other.def && stuff == other.stuff && colorDef == other.colorDef && color == other.color && quality == other.quality && hp == other.hp && stackCountRange == other.stackCountRange && relativeRotation == other.relativeRotation;
            }

            public override bool Equals(object obj)
            {
                return obj is ThingGroupKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                int hash = 17;
                hash = hash * 31 + (def?.GetHashCode() ?? 0);
                hash = hash * 31 + (stuff?.GetHashCode() ?? 0);
                hash = hash * 31 + (colorDef?.GetHashCode() ?? 0);
                hash = hash * 31 + color.GetHashCode();
                hash = hash * 31 + quality.GetHashCode();
                hash = hash * 31 + hp;
                hash = hash * 31 + stackCountRange.GetHashCode();
                hash = hash * 31 + (int)relativeRotation;
                return hash;
            }
        }

        private readonly struct TerrainGroupKey : IEquatable<TerrainGroupKey>
        {
            public readonly TerrainDef def;
            public readonly ColorDef color;

            public TerrainGroupKey(PrefabTerrainData data)
            {
                def = data.def;
                color = data.color;
            }

            public bool Equals(TerrainGroupKey other)
            {
                return def == other.def && color == other.color;
            }

            public override bool Equals(object obj)
            {
                return obj is TerrainGroupKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                int hash = 17;
                hash = hash * 31 + (def?.GetHashCode() ?? 0);
                hash = hash * 31 + (color?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}
