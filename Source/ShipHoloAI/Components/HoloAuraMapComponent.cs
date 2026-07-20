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

        /// <summary>
        /// The persona currently walking this map's ship: non-null only while a
        /// powered core's avatar is spawned, refreshed each 250-tick pass. Persona
        /// abilities that hook the wider game (stat parts, Harmony patches) read
        /// this instead of scanning buildings, and inherit power-gating and the
        /// one-persona opportunity cost for free. Up to 250 ticks stale after power
        /// loss — same tolerance as the auras themselves.
        /// </summary>
        public HoloPersonaDef ProjectedPersona { get; private set; }

        public static HoloPersonaDef ProjectedOn(Map map)
        {
            return map?.GetComponent<HoloAuraMapComponent>()?.ProjectedPersona;
        }

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
            HoloPersonaDef persona = core?.ActivePersona;
            bool projected = core?.Avatar != null && core.Avatar.Spawned;
            ProjectedPersona = projected ? persona : null;

            HediffDef auraDef = persona?.auraHediff;
            if (auraDef == null || !projected)
            {
                return;
            }

            // Warden-style personas do the work themselves rather than buffing crew —
            // the hediff lives on the avatar's own body instead of spreading outward.
            if (persona.auraTargetsAvatar)
            {
                RefreshAura(core.Avatar, auraDef);
                return;
            }

            foreach (Pawn colonist in map.mapPawns.FreeColonistsSpawned)
            {
                if (map.terrainGrid.FoundationAt(colonist.Position)?.IsSubstructure != true)
                {
                    continue;
                }
                RefreshAura(colonist, auraDef);
            }
        }

        private static void RefreshAura(Pawn pawn, HediffDef auraDef)
        {
            Hediff aura = pawn.health.hediffSet.GetFirstHediffOfDef(auraDef);
            if (aura == null)
            {
                aura = pawn.health.AddHediff(auraDef);
            }
            HediffComp_Disappears expiry = aura.TryGetComp<HediffComp_Disappears>();
            if (expiry != null)
            {
                expiry.ticksToDisappear = AuraRefreshTicks;
            }
        }
    }
}
