using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Gravship_Raids
{
    public class SparseLandingLayerDef : Def
    {
        public List<PlanetLayerDef> layers;

        private static HashSet<PlanetLayerDef> cachedLayers;

        internal static bool Contains(PlanetLayerDef layer)
        {
            if (layer == null)
            {
                return false;
            }
            if (cachedLayers == null)
            {
                HashSet<PlanetLayerDef> built = new HashSet<PlanetLayerDef>();
                foreach (SparseLandingLayerDef def in DefDatabase<SparseLandingLayerDef>.AllDefsListForReading)
                {
                    if (def.layers != null)
                    {
                        built.UnionWith(def.layers);
                    }
                }
                cachedLayers = built;
            }
            return cachedLayers.Contains(layer);
        }
    }
}
