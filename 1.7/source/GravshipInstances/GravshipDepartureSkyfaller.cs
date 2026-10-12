using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Gravship_Raids
{
    public class GravshipDepartureSkyfaller : Skyfaller, IThingHolderTickable
    {
        public bool ShouldTickContents => false;

        public EnemyGravshipInstance instance;

        private List<Thing> cachedHullPieces;

        private List<Thing> cachedThrusterPieces;

        private Dictionary<IntVec3, LinkFlags> cachedCellLinkFlags;

        private EnemyGravshipEffects effects;

        private Vector3 lastRiseOffset;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref instance, "instance");
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (instance == null || !TryGetHullPieces(out List<Thing> hullPieces))
            {
                base.DrawAt(drawLoc, flip);
                return;
            }

            GetDrawPositionAndRotation(ref drawLoc, out float extraRotation);

            Vector3 restingCenter = GenThing.TrueCenter(base.Position, base.Rotation, def.size, def.Altitude);
            lastRiseOffset = drawLoc - restingCenter;

            if (instance.template?.prefab != null)
            {
                List<(TerrainDef def, IntVec3 cell)> floorCells = GravshipRaidTemplateUtility.GetFloorCellsForDraw(instance.template, instance.root, instance.rotation);
                for (int i = 0; i < floorCells.Count; i++)
                {
                    Material floorMat = floorCells[i].def.DrawMatSingle;
                    if (floorMat == null)
                    {
                        continue;
                    }
                    Vector3 floorPos = floorCells[i].cell.ToVector3Shifted().SetToAltitude(AltitudeLayer.Floor) + lastRiseOffset;
                    Matrix4x4 floorMatrix = Matrix4x4.TRS(floorPos, Quaternion.identity, Vector3.one);
                    Graphics.DrawMesh(MeshPool.plane10, floorMatrix, floorMat, 0);
                }
            }

            Dictionary<IntVec3, LinkFlags> cellLinkFlags = GetCellLinkFlags(hullPieces);
            for (int i = 0; i < hullPieces.Count; i++)
            {
                Thing piece = hullPieces[i];
                Graphic graphic = piece?.Graphic;
                if (graphic == null)
                {
                    continue;
                }
                Vector3 pieceCenter = GenThing.TrueCenter(piece.Position, piece.Rotation, piece.def.Size, AltitudeLayer.Skyfaller.AltitudeFor()) + lastRiseOffset;
                GravshipRaidTemplateUtility.DrawGravshipPiece(graphic, piece.def, pieceCenter, piece.Rotation, extraRotation, piece.Position, cellLinkFlags);
            }

            if (TryGetThrusterPieces(out List<Thing> thrusterPieces))
            {
                if (effects == null)
                {
                    IntVec3 launchDirection = EnemyGravshipEffects.MajorityLaunchDirection(thrusterPieces.Select((t) => t.Rotation));
                    effects = new EnemyGravshipEffects(base.Map, base.Rotation, launchDirection);
                }
                effects.Tick(Time.deltaTime);
                effects.DrawThrusters(thrusterPieces, lastRiseOffset);
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
            Vector3 shadowCenter = footprint.CenterVector3 + lastRiseOffset;
            Vector2 shadowSize = new Vector2(footprint.Width, footprint.Height);
            DrawDropSpotShadow(shadowCenter, base.Rotation, shadowMaterial, shadowSize, ticksToImpact);
        }

        private bool TryGetHullPieces(out List<Thing> hullPieces)
        {
            hullPieces = null;
            if (!innerContainer.Any)
            {
                return false;
            }
            if (cachedHullPieces == null)
            {
                cachedHullPieces = innerContainer.Where((Thing t) => !(t is Pawn)).ToList();
            }
            hullPieces = cachedHullPieces;
            return hullPieces.Count > 0;
        }

        private Dictionary<IntVec3, LinkFlags> GetCellLinkFlags(List<Thing> hullPieces)
        {
            if (cachedCellLinkFlags == null)
            {
                cachedCellLinkFlags = GravshipRaidTemplateUtility.BuildCellLinkFlags(
                    hullPieces.Where((t) => t?.def?.graphicData != null)
                              .Select((t) => (t.Position, t.Rotation, t.def.Size, t.def.graphicData.linkFlags)));
            }
            return cachedCellLinkFlags;
        }

        private bool TryGetThrusterPieces(out List<Thing> thrusterPieces)
        {
            thrusterPieces = null;
            if (!TryGetHullPieces(out List<Thing> hullPieces))
            {
                return false;
            }
            if (cachedThrusterPieces == null)
            {
                cachedThrusterPieces = hullPieces.Where((Thing t) => t.TryGetComp<CompGravshipThruster>() != null).ToList();
            }
            thrusterPieces = cachedThrusterPieces;
            return thrusterPieces.Count > 0;
        }

        protected override void LeaveMap()
        {
            Map departureMap = Map;

            if (innerContainer.Any)
            {
                List<Thing> contents = new List<Thing>(innerContainer);
                foreach (Thing t in contents)
                {
                    if (t is Pawn pawn && !pawn.Destroyed && !pawn.IsWorldPawn())
                    {
                        pawn.ExitMap(allowedToJoinOrCreateCaravan: false, Rot4.Invalid);
                    }
                }
                innerContainer.ClearAndDestroyContentsOrPassToWorld(DestroyMode.Vanish);
            }

            effects?.End();

            base.LeaveMap();

            EnemyGravshipRaidUtility.FinalizeDeparture(instance, departureMap);
        }
    }
}
