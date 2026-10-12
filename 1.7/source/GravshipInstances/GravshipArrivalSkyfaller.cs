using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Gravship_Raids
{
    public class GravshipArrivalSkyfaller : Skyfaller
    {
        private List<(PrefabThingData data, IntVec3 cell, Rot4 rot)> cachedPieces;

        private List<(TerrainDef def, IntVec3 cell)> cachedFloorCells;

        private List<(PrefabThingData data, IntVec3 cell, Rot4 rot)> cachedThrusterPieces;

        private Dictionary<IntVec3, LinkFlags> cachedCellLinkFlags;

        private Dictionary<(ThingDef def, ThingDef stuff), Graphic> cachedPieceGraphics;

        private EnemyGravshipEffects effects;

        private Vector3 lastFallOffset;

        public EnemyGravshipInstance instance;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref instance, "instance");
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

            // Floor first, drawn well below AltitudeLayer.Skyfaller (the pieces below) so it never visually
            // clips over the walls/hull it sits underneath - Unity's depth sort (RimWorld's y-position-as-depth
            // trick) makes draw order here immaterial, but this keeps read order matching visual order.
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

            if (GravshipRaidsSettings.enableRaidshipEffects && TryGetThrusterPieces(out List<(PrefabThingData data, IntVec3 cell, Rot4 rot)> thrusterPieces))
            {
                if (effects == null)
                {
                    IntVec3 launchDirection = EnemyGravshipEffects.MajorityLaunchDirection(thrusterPieces.Select((p) => p.rot));
                    effects = new EnemyGravshipEffects(base.Map, base.Rotation, launchDirection);
                }
                effects.Tick(Time.deltaTime);
                effects.DrawThrusters(thrusterPieces, lastFallOffset);
            }

            DrawDropSpotShadow();
        }

        protected override void DrawDropSpotShadow()
        {
            Material shadowMaterial = ShadowMaterial;
            if (shadowMaterial == null)
            {
                return;
            }
            GravshipRaidTemplateDef template = instance?.template;
            if (template?.prefab == null)
            {
                base.DrawDropSpotShadow();
                return;
            }
            CellRect footprint = GravshipRaidTemplateUtility.GetRotatedBounds(template, instance.root, instance.rotation);
            Vector3 shadowCenter = footprint.CenterVector3 + lastFallOffset;
            Vector2 shadowSize = new Vector2(footprint.Width, footprint.Height);
            DrawDropSpotShadow(shadowCenter, base.Rotation, shadowMaterial, shadowSize, ticksToImpact);
        }

        private bool TryGetPieces(out List<(PrefabThingData data, IntVec3 cell, Rot4 rot)> pieces)
        {
            pieces = null;
            PrefabDef prefab = instance?.template?.prefab;
            if (prefab == null)
            {
                return false;
            }
            if (cachedPieces == null)
            {
                cachedPieces = PrefabUtility.GetThings(prefab, instance.root, instance.rotation).ToList();
            }
            pieces = cachedPieces;
            return true;
        }

        private bool TryGetFloorCells(out List<(TerrainDef def, IntVec3 cell)> floorCells)
        {
            floorCells = null;
            GravshipRaidTemplateDef template = instance?.template;
            if (template?.prefab == null)
            {
                return false;
            }
            if (cachedFloorCells == null)
            {
                cachedFloorCells = GravshipRaidTemplateUtility.GetFloorCellsForDraw(template, instance.root, instance.rotation);
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

        private bool TryGetThrusterPieces(out List<(PrefabThingData data, IntVec3 cell, Rot4 rot)> thrusterPieces)
        {
            thrusterPieces = null;
            if (!TryGetPieces(out List<(PrefabThingData data, IntVec3 cell, Rot4 rot)> pieces))
            {
                return false;
            }
            if (cachedThrusterPieces == null)
            {
                cachedThrusterPieces = pieces.Where((p) => p.data?.def?.GetCompProperties<CompProperties_GravshipThruster>() != null).ToList();
            }
            thrusterPieces = cachedThrusterPieces;
            return true;
        }

        protected override void SpawnThings()
        {
            Map map = base.Map;

            List<Pawn> pawns = new List<Pawn>();
            for (int i = innerContainer.Count - 1; i >= 0; i--)
            {
                if (innerContainer[i] is Pawn pawn)
                {
                    pawns.Add(pawn);
                }
            }

            if (instance == null)
            {
                // Should never happen - Arrive always sets this before spawning the skyfaller. Kept purely as
                // a "never silently lose pawns" backstop.
                Logger.Error("GravshipArrivalSkyfaller.SpawnThings: no EnemyGravshipInstance was set; falling back to a plain pawn drop with no ship.");
                for (int i = 0; i < pawns.Count; i++)
                {
                    GenSpawn.Spawn(pawns[i], base.Position, map, base.Rotation);
                }
                return;
            }

            PawnsArrivalModeWorker_GravshipLanding.FinishLanding(instance, map, pawns);
        }
    }
}
