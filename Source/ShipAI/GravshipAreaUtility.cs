using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace OdysseyHoloAI
{
    public static class GravshipAreaUtility
    {
        private static HashSet<TerrainDef> _gravTerrains;

        public static bool IsGravshipCell(Map map, IntVec3 c)
        {
            var td = c.GetTerrain(map);
            if (td == null) return false;

            EnsureCache();
            if (_gravTerrains.Contains(td)) return true;

            // Fallback heuristic: defName/label contains grav + substructure / gravship
            var name = td.defName?.ToLowerInvariant() ?? "";
            var label = td.label?.ToLowerInvariant() ?? "";
            if (name.Contains("gravship") || (name.Contains("grav") && name.Contains("substructure"))) return true;
            if (label.Contains("gravship") || (label.Contains("grav") && label.Contains("substructure"))) return true;

            return false;
        }

        private static void EnsureCache()
        {
            if (_gravTerrains != null) return;

            _gravTerrains = new HashSet<TerrainDef>();
            foreach (var td in DefDatabase<TerrainDef>.AllDefsListForReading)
            {
                var name = td.defName?.ToLowerInvariant() ?? "";
                var label = td.label?.ToLowerInvariant() ?? "";
                if (name.Contains("gravship") || (name.Contains("grav") && name.Contains("substructure")) ||
                    label.Contains("gravship") || (label.Contains("grav") && label.Contains("substructure")))
                {
                    _gravTerrains.Add(td);
                }
            }
        }

        public static Area_Allowed GetOrCreateArea(Map map, string label = "Gravship Tiles")
        {
            var existing = map.areaManager.AllAreas
                .OfType<Area_Allowed>()
                .FirstOrDefault(a => a != null && string.Equals(a.Label, label, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing;

            // RimWorld 1.6+: bool TryMakeNewAllowed(out Area_Allowed area)
            if (map.areaManager.TryMakeNewAllowed(out Area_Allowed created) && created != null)
            {
                created.SetLabel(label);
                return created;
            }

            // Fallback for older APIs (just in case)
            var area = map.areaManager.AllAreas.OfType<Area_Allowed>().FirstOrDefault(a => a.Label == label);
            return area;
        }

        public static void RebuildArea(Map map, Area_Allowed area)
        {
            if (area == null) return;
            foreach (var c in map.AllCells)
                area[c] = IsGravshipCell(map, c);
        }
    }
}
