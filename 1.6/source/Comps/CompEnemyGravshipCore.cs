using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Gravship_Raids
{
    public class CompEnemyGravshipCore : ThingComp
    {
        public CompProperties_EnemyGravshipCore Props => (CompProperties_EnemyGravshipCore)props;

        public CompTransporter Transporter => parent.GetComp<CompTransporter>();

        private bool authorResolved;

        private string cachedAuthor;

        public override string CompInspectStringExtra()
        {
            string baseText = "GravshipRaids.EnemyGravshipCoreInspect".Translate();

            if (!authorResolved)
            {
                EnemyGravshipInstance instance = MapComponent_GravshipRaid.GetFor(parent.Map)?.GetInstanceForCore(parent);
                string author = instance?.template?.author;
                cachedAuthor = (author.NullOrEmpty() || author.Trim().Length == 0) ? null : author;
                authorResolved = true;
            }

            if (cachedAuthor == null)
            {
                return baseText;
            }

            return baseText + "\n" + "GravshipRaids.EnemyGravshipCoreAuthor".Translate(cachedAuthor);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (previousMap == null)
            {
                return;
            }
            EnemyGravshipInstance instance = MapComponent_GravshipRaid.GetFor(previousMap)?.GetInstanceForCore(parent);
            if (instance == null)
            {
                return;
            }
            EnemyGravshipRaidUtility.HandleCoreDestroyed(instance);
        }
    }

    public class CompProperties_EnemyGravshipCore : CompProperties
    {
        public CompProperties_EnemyGravshipCore()
        {
            compClass = typeof(CompEnemyGravshipCore);
        }
    }

    public class CompTransporter_Hidden : CompTransporter
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield break;
        }

        public override string CompInspectStringExtra()
        {
            return null;
        }
    }

    public class CompProperties_Transporter_Hidden : CompProperties_Transporter
    {
        public CompProperties_Transporter_Hidden()
        {
            compClass = typeof(CompTransporter_Hidden);
        }
    }
}
