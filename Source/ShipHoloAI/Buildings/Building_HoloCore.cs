using RimWorld;
using Verse;

namespace ShipHoloAI
{
    public class Building_HoloCore : Building
    {
        private CompPowerTrader powerComp;

        public bool Powered => powerComp == null || powerComp.PowerOn;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerTrader>();
        }
    }
}
