using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ShipHoloAI
{
    /// <summary>
    /// While I.X.I.A. is projected, the vanilla "Need warden" alert is noise — she
    /// is the warden. Vanilla's scan only counts free colonists with Warden work
    /// enabled, so a crew that leaves the brig entirely to her sees a permanent
    /// false alert. The postfix re-runs the same scan counting a projected
    /// I.X.I.A. as a warden for prisoners held on the ship's substructure; she is
    /// ship-bound, so a prisoner held dirt-side still genuinely needs a colonist
    /// warden and still alerts.
    /// </summary>
    [HarmonyPatch(typeof(Alert_NeedWarden), nameof(Alert_NeedWarden.GetReport))]
    public static class Patch_AlertNeedWarden
    {
        public static void Postfix(ref AlertReport __result)
        {
            if (!__result.active)
            {
                return;
            }
            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                Map map = maps[i];
                if (!map.IsPlayerHome || !map.mapPawns.PrisonersOfColonySpawned.Any())
                {
                    continue;
                }
                if (HasColonistWarden(map) || IxiaCoversPrisoners(map))
                {
                    continue;
                }
                __result = AlertReport.CulpritIs(map.mapPawns.PrisonersOfColonySpawned[0]);
                return;
            }
            __result = false;
        }

        /// <summary>Vanilla's own warden test, verbatim (Alert_NeedWarden.GetReport).</summary>
        private static bool HasColonistWarden(Map map)
        {
            foreach (Pawn colonist in map.mapPawns.FreeColonistsSpawned)
            {
                if ((colonist.Spawned || colonist.BrieflyDespawned()) && !colonist.Downed
                    && colonist.workSettings != null
                    && colonist.workSettings.GetPriority(WorkTypeDefOf.Warden) > 0)
                {
                    return true;
                }
            }
            return false;
        }

        internal static bool IxiaCoversPrisoners(Map map)
        {
            if (HoloAuraMapComponent.ProjectedOn(map) != HoloAI_DefOf.HoloAI_Persona_IXIA)
            {
                return false;
            }
            foreach (Pawn prisoner in map.mapPawns.PrisonersOfColonySpawned)
            {
                if (map.terrainGrid.FoundationAt(prisoner.Position)?.IsSubstructure != true)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
