using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Gravship_Raids
{
    public class CrashlandedGravshipArrivalSkyfaller : Skyfaller
    {
        public PrefabDef prefab;

        public IntVec3 plannedRoot;

        public Rot4 plannedRotation;

        private List<(PrefabThingData data, IntVec3 cell, Rot4 rot)> cachedPieces;

        private List<(TerrainDef def, IntVec3 cell)> cachedFloorCells;

        private Dictionary<IntVec3, LinkFlags> cachedCellLinkFlags;

        private Dictionary<(ThingDef def, ThingDef stuff), Graphic> cachedPieceGraphics;

        private Vector3 lastFallOffset;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref prefab, "prefab");
            Scribe_Values.Look(ref plannedRoot, "plannedRoot");
            Scribe_Values.Look(ref plannedRotation, "plannedRotation");
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!TryGetPieces(out List<(PrefabThingData data, IntVec3 cell, Rot4 rot)> pieces))
            {
                base.DrawAt(drawLoc, flip);
                return;
            }

            GetDrawPositionAndRotation(ref drawLoc, out float extraRotation);

            Vector3 restingCenter = GenThing.TrueCenter(base.Position, base.Rotation, def.size, def.Altitude);
            lastFallOffset = drawLoc - restingCenter;

            if (TryGetFloorCells(out List<(TerrainDef def, IntVec3 cell)> floorCells))
            {
                for (int i = 0; i < floorCells.Count; i++)
                {
                    Material floorMat = floorCells[i].def.DrawMatSingle;
                    if (floorMat == null)
                    {
                        continue;
                    }
                    Vector3 floorPos = floorCells[i].cell.ToVector3Shifted().SetToAltitude(AltitudeLayer.Floor) + lastFallOffset;
                    Matrix4x4 floorMatrix = Matrix4x4.TRS(floorPos, Quaternion.identity, Vector3.one);
                    Graphics.DrawMesh(MeshPool.plane10, floorMatrix, floorMat, 0);
                }
            }

            Dictionary<IntVec3, LinkFlags> cellLinkFlags = GetCellLinkFlags(pieces);
            for (int i = 0; i < pieces.Count; i++)
            {
                PrefabThingData data = pieces[i].data;
                if (data?.def == null)
                {
                    continue;
                }
                Graphic graphic = GetPieceGraphic(data.def, data.stuff);
                if (graphic == null)
                {
                    continue;
                }
                Vector3 pieceCenter = GenThing.TrueCenter(pieces[i].cell, pieces[i].rot, data.def.Size, AltitudeLayer.Skyfaller.AltitudeFor()) + lastFallOffset;
                GravshipRaidTemplateUtility.DrawGravshipPiece(graphic, data.def, pieceCenter, pieces[i].rot, extraRotation, pieces[i].cell, cellLinkFlags);
            }

            DrawDropSpotShadow();
        }

        protected override void DrawDropSpotShadow()
        {
            Material shadowMaterial = ShadowMaterial;
            if (shadowMaterial == null || prefab == null)
            {
                base.DrawDropSpotShadow();
                return;
            }
            CellRect footprint = GravshipRaidTemplateUtility.GetRotatedBounds(prefab, plannedRoot, plannedRotation);
            Vector3 shadowCenter = footprint.CenterVector3 + lastFallOffset;
            Vector2 shadowSize = new Vector2(footprint.Width, footprint.Height);
            DrawDropSpotShadow(shadowCenter, base.Rotation, shadowMaterial, shadowSize, ticksToImpact);
        }

        private bool TryGetPieces(out List<(PrefabThingData data, IntVec3 cell, Rot4 rot)> pieces)
        {
            pieces = null;
            if (prefab == null || !plannedRoot.IsValid)
            {
                return false;
            }
            if (cachedPieces == null)
            {
                cachedPieces = PrefabUtility.GetThings(prefab, plannedRoot, plannedRotation).ToList();
            }
            pieces = cachedPieces;
            return true;
        }

        private bool TryGetFloorCells(out List<(TerrainDef def, IntVec3 cell)> floorCells)
        {
            floorCells = null;
            if (prefab == null || !plannedRoot.IsValid)
            {
                return false;
            }
            if (cachedFloorCells == null)
            {
                cachedFloorCells = GravshipRaidTemplateUtility.GetFloorCellsForDraw(prefab, plannedRoot, plannedRotation);
            }
            floorCells = cachedFloorCells;
            return true;
        }

        private Graphic GetPieceGraphic(ThingDef def, ThingDef stuff)
        {
            if (cachedPieceGraphics == null)
            {
                cachedPieceGraphics = new Dictionary<(ThingDef, ThingDef), Graphic>();
            }
            var key = (def, stuff);
            if (!cachedPieceGraphics.TryGetValue(key, out Graphic graphic))
            {
                graphic = GravshipRaidTemplateUtility.GetPieceGraphic(def, stuff);
                cachedPieceGraphics[key] = graphic;
            }
            return graphic;
        }

        private Dictionary<IntVec3, LinkFlags> GetCellLinkFlags(List<(PrefabThingData data, IntVec3 cell, Rot4 rot)> pieces)
        {
            if (cachedCellLinkFlags == null)
            {
                cachedCellLinkFlags = GravshipRaidTemplateUtility.BuildCellLinkFlags(
                    pieces.Where((p) => p.data?.def?.graphicData != null)
                          .Select((p) => (p.cell, p.rot, p.data.def.Size, p.data.def.graphicData.linkFlags)));
            }
            return cachedCellLinkFlags;
        }

        protected override void SpawnThings()
        {
            CrashlandedGravshipStartUtility.CompleteImpact(this, base.Map);
        }
    }
}
