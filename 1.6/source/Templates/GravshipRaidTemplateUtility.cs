using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Gravship_Raids
{
    public static class GravshipRaidTemplateUtility
    {
        private static readonly Dictionary<GravshipRaidTemplateDef, IntVec3> CoreCellCache = new Dictionary<GravshipRaidTemplateDef, IntVec3>();

        internal static void PopulateCoreCellCache()
        {
            foreach (GravshipRaidTemplateDef template in DefDatabase<GravshipRaidTemplateDef>.AllDefsListForReading)
            {
                if (template?.prefab == null)
                {
                    continue;
                }
                List<(PrefabThingData data, IntVec3 cell)> coreThings = template.prefab.GetThings()
                    .Where(t => t.data?.def != null && t.data.def.HasComp<CompEnemyGravshipCore>())
                    .ToList();
                if (coreThings.Count != 1)
                {
                    Logger.Warning($"GravshipRaidTemplateUtility.PopulateCoreCellCache: template '{template.defName}' has {coreThings.Count} CompEnemyGravshipCore-bearing thing(s) (expected exactly 1); skipping its core-cell cache entry.");
                    continue;
                }
                CoreCellCache[template] = coreThings[0].cell;
            }
        }

        public static bool IsValidTemplate(GravshipRaidTemplateDef template)
        {
            return template?.prefab != null && !template.ConfigErrors().Any();
        }

        public static bool IsEligibleTemplate(GravshipRaidTemplateDef template, FactionDef factionDef, float points, Map map)
        {
            if (template.disabled)
            {
                return false;
            }
            if (!IsValidTemplate(template))
            {
                return false;
            }
            if (!template.PointsInRange(points))
            {
                return false;
            }
            if (!template.AllowsFaction(factionDef))
            {
                return false;
            }
            if (!GravshipRaidsSettings.AllowsFactionGlobally(factionDef))
            {
                return false;
            }
            if (map != null)
            {
                if (!template.AllowsBiome(map.Biome))
                {
                    return false;
                }
                if (map.Tile.Valid && !template.AllowsLayer(map.Tile.LayerDef))
                {
                    return false;
                }
            }
            return true;
        }

        public static IEnumerable<GravshipRaidTemplateDef> GetEligibleTemplates(FactionDef factionDef, float points, Map map)
        {
            foreach (GravshipRaidTemplateDef template in DefDatabase<GravshipRaidTemplateDef>.AllDefsListForReading)
            {
                if (IsEligibleTemplate(template, factionDef, points, map))
                {
                    yield return template;
                }
            }
        }
        public static bool HasEligibleTemplate(FactionDef factionDef, float points, Map map)
        {
            return GetEligibleTemplates(factionDef, points, map).Any();
        }
        public static int MakeSelectionSeed(FactionDef factionDef, float points, Map map)
        {
            int factionHash = factionDef?.shortHash ?? 0;
            int mapHash = map?.uniqueID ?? 0;
            int pointsHash = Mathf.RoundToInt(points);
            return Gen.HashCombineInt(factionHash, mapHash, pointsHash, 0);
        }

        public static GravshipRaidTemplateDef SelectTemplate(FactionDef factionDef, float points, Map map, int seed)
        {
            List<GravshipRaidTemplateDef> candidates = GetEligibleTemplates(factionDef, points, map).ToList();
            if (candidates.Count == 0)
            {
                return null;
            }
            GravshipRaidTemplateDef result = null;
            Rand.PushState();
            try
            {
                Rand.Seed = seed;
                candidates.TryRandomElementByWeight((GravshipRaidTemplateDef t) => t.GetSelectionWeight(points), out result);
            }
            finally
            {
                Rand.PopState();
            }
            return result;
        }

        public static GravshipRaidTemplateDef SelectTemplate(FactionDef factionDef, float points, Map map)
        {
            return SelectTemplate(factionDef, points, map, MakeSelectionSeed(factionDef, points, map));
        }

        public static CellRect GetRotatedBounds(GravshipRaidTemplateDef template, IntVec3 pos, Rot4 rot)
        {
            Rot4 validatedRot = PrefabUtility.ValidateRotation(template.prefab, rot);
            return GenAdj.OccupiedRect(pos, validatedRot, template.prefab.size);
        }

        public static IntVec3 TransformCell(GravshipRaidTemplateDef template, IntVec3 relativeCell, IntVec3 pos, Rot4 rot)
        {
            Rot4 validatedRot = PrefabUtility.ValidateRotation(template.prefab, rot);
            IntVec3 root = PrefabUtility.GetRoot(template.prefab, pos, validatedRot);
            return root + PrefabUtility.GetAdjustedLocalPosition(relativeCell, validatedRot);
        }
        public static IntVec3 GetCoreCell(GravshipRaidTemplateDef template, IntVec3 pos, Rot4 rot)
        {
            if (template?.prefab == null)
            {
                return IntVec3.Invalid;
            }
            if (!CoreCellCache.TryGetValue(template, out IntVec3 localCoreCell))
            {
                Logger.Error($"GravshipRaidTemplateUtility.GetCoreCell: no cached core cell for template '{template.defName}' (invalid template, or not yet cached); falling back to the prefab's center cell.");
                localCoreCell = new IntVec3(template.prefab.size.x / 2, 0, template.prefab.size.z / 2);
            }
            return TransformCell(template, localCoreCell, pos, rot);
        }

        public static IEnumerable<IntVec3> GetOpenInteriorCells(GravshipRaidTemplateDef template, Map map, IntVec3 pos, Rot4 rot)
        {
            if (template?.prefab == null || map == null)
            {
                yield break;
            }
            CellRect footprint = GetRotatedBounds(template, pos, rot);
            foreach (IntVec3 cell in footprint.Cells)
            {
                if (!cell.InBounds(map) || !cell.Standable(map) || cell.GetFirstBuilding(map) != null)
                {
                    continue;
                }
                yield return cell;
            }
        }
        public static List<(TerrainDef def, IntVec3 cell)> GetFloorCellsForDraw(GravshipRaidTemplateDef template, IntVec3 root, Rot4 rotation)
        {
            List<(TerrainDef def, IntVec3 cell)> result = new List<(TerrainDef, IntVec3)>();
            if (template?.prefab == null)
            {
                return result;
            }
            foreach (var (data, cell) in template.prefab.GetTerrain())
            {
                if (data?.def == null)
                {
                    continue;
                }
                IntVec3 absolute = TransformCell(template, cell, root, rotation);
                result.Add((data.def, absolute));
            }
            return result;
        }

        public static bool CanSpawnPrefab(GravshipRaidTemplateDef template, Map map, IntVec3 pos, Rot4 rot, bool canWipeEdifices = false)
        {
            if (template?.prefab == null)
            {
                return false;
            }
            return CanSpawnPrefab(template.prefab, map, pos, rot, canWipeEdifices);
        }

        public static bool CanSpawnPrefab(PrefabDef prefab, Map map, IntVec3 pos, Rot4 rot, bool canWipeEdifices = false)
        {
            if (prefab == null || map == null)
            {
                return false;
            }
            Rot4 validatedRot = PrefabUtility.ValidateRotation(prefab, rot);
            IntVec3 root = PrefabUtility.GetRoot(prefab, pos, validatedRot);
            List<PaintedCell> painted = new List<PaintedCell>();
            try
            {
                foreach (var (data, cell) in prefab.GetTerrain())
                {
                    if (data?.def == null)
                    {
                        continue;
                    }
                    IntVec3 absolute = root + PrefabUtility.GetAdjustedLocalPosition(cell, validatedRot);
                    if (!absolute.InBounds(map))
                    {
                        return false;
                    }
                    painted.Add(new PaintedCell(absolute, map.terrainGrid.FoundationAt(absolute), map.terrainGrid.TopTerrainAt(absolute)));
                    map.terrainGrid.SetTerrain(absolute, data.def);
                }
                return PrefabUtility.CanSpawnPrefab(prefab, map, pos, validatedRot, canWipeEdifices);
            }
            finally
            {
                for (int i = painted.Count - 1; i >= 0; i--)
                {
                    PaintedCell entry = painted[i];
                    TerrainDef currentFoundation = map.terrainGrid.FoundationAt(entry.cell);
                    if (currentFoundation != entry.originalFoundation)
                    {
                        if (entry.originalFoundation != null)
                        {
                            map.terrainGrid.SetFoundation(entry.cell, entry.originalFoundation);
                        }
                        else if (currentFoundation != null)
                        {
                            map.terrainGrid.RemoveFoundation(entry.cell, doLeavings: false);
                        }
                    }
                    TerrainDef currentTop = map.terrainGrid.TopTerrainAt(entry.cell);
                    if (currentTop != entry.originalTop)
                    {
                        map.terrainGrid.SetTerrain(entry.cell, entry.originalTop);
                    }
                }
            }
        }

        private readonly struct PaintedCell
        {
            public readonly IntVec3 cell;
            public readonly TerrainDef originalFoundation;
            public readonly TerrainDef originalTop;

            public PaintedCell(IntVec3 cell, TerrainDef originalFoundation, TerrainDef originalTop)
            {
                this.cell = cell;
                this.originalFoundation = originalFoundation;
                this.originalTop = originalTop;
            }
        }

        public class TerrainCellSnapshot : IExposable
        {
            public IntVec3 cell;
            public TerrainDef originalFoundation;
            public TerrainDef originalTop;
            public TerrainDef paintedFoundation;
            public TerrainDef paintedTop;
            public bool hasPaintedSnapshot;

            public TerrainCellSnapshot()
            {
            }

            public TerrainCellSnapshot(IntVec3 cell, TerrainDef originalFoundation, TerrainDef originalTop, TerrainDef paintedFoundation, TerrainDef paintedTop)
            {
                this.cell = cell;
                this.originalFoundation = originalFoundation;
                this.originalTop = originalTop;
                this.paintedFoundation = paintedFoundation;
                this.paintedTop = paintedTop;
                hasPaintedSnapshot = true;
            }

            public void ExposeData()
            {
                Scribe_Values.Look(ref cell, "cell");
                Scribe_Defs.Look(ref originalFoundation, "originalFoundation");
                Scribe_Defs.Look(ref originalTop, "originalTop");
                Scribe_Defs.Look(ref paintedFoundation, "paintedFoundation");
                Scribe_Defs.Look(ref paintedTop, "paintedTop");
                Scribe_Values.Look(ref hasPaintedSnapshot, "hasPaintedSnapshot", false);
            }
        }

        public static List<TerrainCellSnapshot> SnapshotTerrain(GravshipRaidTemplateDef template, Map map, IntVec3 pos, Rot4 rot)
        {
            List<TerrainCellSnapshot> result = new List<TerrainCellSnapshot>();
            if (template?.prefab == null || map == null)
            {
                return result;
            }
            Rot4 validatedRot = PrefabUtility.ValidateRotation(template.prefab, rot);
            IntVec3 root = PrefabUtility.GetRoot(template.prefab, pos, validatedRot);
            foreach (var (data, cell) in template.prefab.GetTerrain())
            {
                if (data?.def == null)
                {
                    continue;
                }
                IntVec3 absolute = root + PrefabUtility.GetAdjustedLocalPosition(cell, validatedRot);
                if (!absolute.InBounds(map))
                {
                    continue;
                }
                TerrainDef originalFoundation = map.terrainGrid.FoundationAt(absolute);
                TerrainDef originalTop = map.terrainGrid.TopTerrainAt(absolute);
                TerrainDef paintedFoundation = data.def.isFoundation ? data.def : originalFoundation;
                TerrainDef paintedTop = data.def.isFoundation ? originalTop : data.def;
                result.Add(new TerrainCellSnapshot(absolute, originalFoundation, originalTop, paintedFoundation, paintedTop));
            }
            return result;
        }

        public static void RestoreTerrain(List<TerrainCellSnapshot> snapshot, Map map)
        {
            if (snapshot == null || map == null)
            {
                return;
            }
            for (int i = snapshot.Count - 1; i >= 0; i--)
            {
                TerrainCellSnapshot entry = snapshot[i];
                if (entry == null || !entry.cell.InBounds(map))
                {
                    continue;
                }
                if (CellShowsPlayerInteraction(entry.cell, map))
                {
                    continue;
                }
                if (entry.hasPaintedSnapshot)
                {
                    TerrainDef expectedFoundation = map.terrainGrid.FoundationAt(entry.cell);
                    TerrainDef expectedTop = map.terrainGrid.TopTerrainAt(entry.cell);
                    if (expectedFoundation != entry.paintedFoundation || expectedTop != entry.paintedTop)
                    {
                        continue;
                    }
                }
                TerrainDef currentFoundation = map.terrainGrid.FoundationAt(entry.cell);
                if (currentFoundation != entry.originalFoundation)
                {
                    if (entry.originalFoundation != null)
                    {
                        map.terrainGrid.SetFoundation(entry.cell, entry.originalFoundation);
                    }
                    else if (currentFoundation != null)
                    {
                        map.terrainGrid.RemoveFoundation(entry.cell, doLeavings: false);
                    }
                }
                TerrainDef currentTop = map.terrainGrid.TopTerrainAt(entry.cell);
                if (currentTop != entry.originalTop)
                {
                    map.terrainGrid.SetTerrain(entry.cell, entry.originalTop);
                }
            }
        }

        private static bool CellShowsPlayerInteraction(IntVec3 cell, Map map)
        {
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing t = things[i];
                if (t is Blueprint)
                {
                    return true;
                }
                if (t is Building building && building.Faction == Faction.OfPlayer)
                {
                    return true;
                }
            }
            return false;
        }

        public static Dictionary<IntVec3, LinkFlags> BuildCellLinkFlags(IEnumerable<(IntVec3 cell, Rot4 rot, IntVec2 size, LinkFlags flags)> pieces)
        {
            Dictionary<IntVec3, LinkFlags> result = new Dictionary<IntVec3, LinkFlags>();
            foreach (var (cell, rot, size, flags) in pieces)
            {
                if (flags == LinkFlags.None)
                {
                    continue;
                }
                foreach (IntVec3 occupied in GenAdj.OccupiedRect(cell, rot, size))
                {
                    result.TryGetValue(occupied, out LinkFlags existing);
                    result[occupied] = existing | flags;
                }
            }
            return result;
        }

        public static void DrawGravshipPiece(Graphic graphic, ThingDef def, Vector3 loc, Rot4 rot, float extraRotation, IntVec3 cell, Dictionary<IntVec3, LinkFlags> cellLinkFlags)
        {
            Mesh mesh = graphic.MeshAt(rot);
            Quaternion quat = graphic.QuatFromRot(rot);
            if (extraRotation != 0f)
            {
                quat *= Quaternion.Euler(Vector3.up * extraRotation);
            }
            if (graphic.data != null && graphic.data.addTopAltitudeBias)
            {
                quat *= Quaternion.Euler(Vector3.left * 2f);
            }
            Vector3 drawLoc = loc + graphic.DrawOffset(rot);
            Material mat = LinkedMatFor(graphic, def, cell, cellLinkFlags) ?? graphic.MatAt(rot);
            Graphics.DrawMesh(mesh, drawLoc, quat, mat, 0);
        }

        public static Graphic GetPieceGraphic(ThingDef def, ThingDef stuff)
        {
            Graphic graphic = def?.graphic;
            if (graphic == null || def.graphicData == null)
            {
                return graphic;
            }
            Color drawColor = (stuff != null) ? def.GetColorForStuff(stuff) : def.graphicData.color;
            Color drawColorTwo = def.graphicData.colorTwo;
            if (def.graphicData.ignoreThingDrawColor || (drawColor.IndistinguishableFrom(graphic.Color) && drawColorTwo.IndistinguishableFrom(graphic.ColorTwo)))
            {
                return graphic;
            }
            return graphic.GetColoredVersion(graphic.Shader, drawColor, drawColorTwo);
        }

        private static Material LinkedMatFor(Graphic graphic, ThingDef def, IntVec3 cell, Dictionary<IntVec3, LinkFlags> cellLinkFlags)
        {
            if (!(graphic is Graphic_Linked linkedGraphic) || def?.graphicData == null)
            {
                return null;
            }
            int num = 0;
            int bit = 1;
            for (int i = 0; i < 4; i++)
            {
                IntVec3 c = cell + GenAdj.CardinalDirections[i];
                if (cellLinkFlags != null && cellLinkFlags.TryGetValue(c, out LinkFlags neighborFlags) && (neighborFlags & def.graphicData.linkFlags) != 0)
                {
                    num += bit;
                }
                bit *= 2;
            }
            return MaterialAtlasPool.SubMaterialFromAtlas(linkedGraphic.SubGraphic.MatSingle, (LinkDirections)num);
        }
    }
}
