using System.Collections.Generic;
using System.Xml;
using RimWorld;
using Verse;

namespace Gravship_Raids
{
    // Extends the vanilla PrefabDef with captured roof data. Instances still register in
    // DefDatabase<PrefabDef> (RimWorld indexes subclasses under their nearest non-abstract base), so any
    // existing <prefab> reference typed as PrefabDef resolves to one of these without further changes -
    // only the XML root tag needs to change from <PrefabDef> to <Gravship_Raids.GravshipRaidPrefabDef>.
    public class GravshipRaidPrefabDef : PrefabDef
    {
        public List<PrefabRoofData> roofs = new List<PrefabRoofData>();

        public IEnumerable<(PrefabRoofData data, IntVec3 cell)> GetRoofs()
        {
            foreach (PrefabRoofData data in roofs)
            {
                if (data?.def == null || data.rects.NullOrEmpty())
                {
                    continue;
                }
                foreach (CellRect rect in data.rects)
                {
                    foreach (IntVec3 cell in rect.Cells)
                    {
                        yield return (data, cell);
                    }
                }
            }
        }
    }

    public class PrefabRoofData
    {
        public RoofDef def;

        public List<CellRect> rects;

        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            XmlHelper.ParseElements(this, xmlRoot, "def");
        }
    }
}
