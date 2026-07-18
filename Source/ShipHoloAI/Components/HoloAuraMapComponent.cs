using RimWorld;
using Verse;

namespace ShipHoloAI
{
    /// <summary>
    /// Applies the active persona's aura hediff to free colonists standing on gravship
    /// substructure while the avatar is projected. The hediff self-expires (600 ticks)
    /// and is refreshed each pass, so leaving the ship, losing power, or swapping
    /// personas lets it lapse on its own.
    /// </summary>
    public class HoloAuraMapComponent : MapComponent
    {
        private const int PassIntervalTicks = 250;
        private const int AuraRefreshTicks = 600;

        public HoloAuraMapComponent(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % PassIntervalTicks != 0)
            {
                return;
            }
            Building_HoloCore core = PrismSpeech.FindActiveCore(map);
            HediffDef auraDef = core?.ActivePersona.auraHediff;
            if (auraDef == null || core.Avatar == null || !core.Avatar.Spawned)
            {
                return;
            }
            foreach (Pawn colonist in map.mapPawns.FreeColonistsSpawned)
            {
                if (map.terrainGrid.FoundationAt(colonist.Position)?.IsSubstructure != true)
                {
                    continue;
                }
                Hediff aura = colonist.health.hediffSet.GetFirstHediffOfDef(auraDef);
                if (aura == null)
                {
                    aura = colonist.health.AddHediff(auraDef);
                }
                HediffComp_Disappears expiry = aura.TryGetComp<HediffComp_Disappears>();
                if (expiry != null)
                {
                    expiry.ticksToDisappear = AuraRefreshTicks;
                }
            }
        }
    }
}
