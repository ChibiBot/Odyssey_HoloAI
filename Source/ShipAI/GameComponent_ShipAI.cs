using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace OdysseyHoloAI
{
    public class GameComponent_ShipAI : GameComponent
    {
        public GameComponent_ShipAI(Game game) : base() { }

        public override void GameComponentTick()
        {
            if (Find.TickManager.Paused || Find.TickManager.TicksGame % 60 != 0) return;

            foreach (var map in Find.Maps)
            {
                var area = map?.areaManager?.AllAreas?.OfType<Area_Allowed>()
                    .FirstOrDefault(a => a != null && a.Label == "Gravship Tiles");
                if (area == null) continue;

                var pawns = map.mapPawns.AllPawnsSpawned.Where(p => p?.kindDef?.defName == "OH_ShipAI");
                foreach (var p in pawns)
                {
                    if (p.playerSettings == null) continue;

                    if (p.playerSettings.AreaRestrictionInPawnCurrentMap != area)
                        p.playerSettings.AreaRestrictionInPawnCurrentMap = area;

                    if (!area[p.Position])
                    {
                        var dest = FindClosestAllowedCell(p.Position, map, area);
                        if (dest.IsValid)
                        {
                            p.jobs?.StopAll();
                            p.pather?.StopDead();
                            p.Position = dest;
                            MoteMaker.ThrowText(dest.ToVector3Shifted(), map, "Ship AI: ship-bound.");
                        }
                    }
                }
            }
        }

        private static IntVec3 FindClosestAllowedCell(IntVec3 from, Map map, Area_Allowed area)
        {
            int maxRadius = 50;
            for (int r = 0; r <= maxRadius; r++)
                foreach (var c in GenRadial.RadialCellsAround(from, r, true))
                    if (c.InBounds(map) && area[c] && c.Standable(map))
                        return c;
            return IntVec3.Invalid;
        }
    }
}
