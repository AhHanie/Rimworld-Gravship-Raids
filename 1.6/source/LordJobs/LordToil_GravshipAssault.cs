using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Gravship_Raids
{
    public class LordToil_GravshipAssault : LordToil_AssaultColony
    {
        private const float MinGuardDefendRadius = 10f;

        private const float MaxGuardDefendRadius = 34f;

        private readonly EnemyGravshipInstance instance;

        public override IntVec3 FlagLoc => TryGetDefendPoint(out IntVec3 point) ? point : IntVec3.Invalid;

        public LordToil_GravshipAssault(EnemyGravshipInstance instance)
            : base(attackDownedIfStarving: false, canPickUpOpportunisticWeapons: false)
        {
            this.instance = instance;
        }

        public override void UpdateAllDuties()
        {
            base.UpdateAllDuties();
            AssignGuardDuties();
        }

        private void AssignGuardDuties()
        {
            if (instance?.guardCrew == null || instance.guardCrew.Count == 0)
            {
                return;
            }
            if (!TryGetDefendPoint(out IntVec3 defendPoint))
            {
                return;
            }
            float radius = GuardDefendRadius();
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];
                if (pawn?.mindState == null || !instance.IsGuard(pawn))
                {
                    continue;
                }
                PawnDuty duty = new PawnDuty(DutyDefOf.Defend, defendPoint, defendPoint, radius);
                pawn.mindState.duty = duty;
            }
        }

        private bool TryGetDefendPoint(out IntVec3 point)
        {
            if (instance == null)
            {
                point = IntVec3.Invalid;
                return false;
            }
            if (instance.core != null && instance.core.Spawned)
            {
                point = instance.core.Position;
                return true;
            }
            if (instance.state == GravshipRaidState.Landing || instance.state == GravshipRaidState.Landed || instance.state == GravshipRaidState.Boarding)
            {
                point = instance.root;
                return true;
            }
            point = IntVec3.Invalid;
            return false;
        }

        private float GuardDefendRadius()
        {
            if (instance?.template == null)
            {
                return MinGuardDefendRadius;
            }
            CellRect bounds = GravshipRaidTemplateUtility.GetRotatedBounds(instance.template, instance.root, instance.rotation);
            float halfDiagonal = new Vector2(bounds.Width, bounds.Height).magnitude / 2f;
            return Mathf.Clamp(halfDiagonal + 4f, MinGuardDefendRadius, MaxGuardDefendRadius);
        }
    }
}
