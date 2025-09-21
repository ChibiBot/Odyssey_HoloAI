using System.Linq;
using RimWorld;
using Verse;

namespace OdysseyHoloAI
{
    public class MapComponent_GravshipArea : MapComponent
    {
        private Area_Allowed gravArea;
        private int nextScanTick;

        public MapComponent_GravshipArea(Map map) : base(map) { }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            gravArea = GravshipAreaUtility.GetOrCreateArea(map);
            GravshipAreaUtility.RebuildArea(map, gravArea);
            AssignToShipAIs();
            nextScanTick = Find.TickManager.TicksGame + 600; // ~10s
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame >= nextScanTick)
            {
                nextScanTick += 600;
                if (gravArea == null) gravArea = GravshipAreaUtility.GetOrCreateArea(map);
                GravshipAreaUtility.RebuildArea(map, gravArea);
                AssignToShipAIs();
            }
        }

        private void AssignToShipAIs()
        {
            if (gravArea == null) return;

            foreach (var p in map.mapPawns.AllPawnsSpawned.Where(p => p?.kindDef?.defName == "OH_ShipAI"))
            {
                if (p.playerSettings == null) continue;

                // 1.6+ API: AreaRestrictionInPawnCurrentMap
                if (p.playerSettings.AreaRestrictionInPawnCurrentMap != gravArea)
                    p.playerSettings.AreaRestrictionInPawnCurrentMap = gravArea;
            }
        }
    }
}
