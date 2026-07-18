using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ShipHoloAI
{
    /// <summary>
    /// Periodic ship-status watchdog. Currently: warns once when gravship fuel drops
    /// below a quarter, re-arming after it climbs back above a third.
    /// </summary>
    public class HoloAIAnnouncer : GameComponent
    {
        private const int CheckIntervalTicks = 2000;
        private const float WarnFraction = 0.25f;
        private const float RearmFraction = 0.35f;

        private HashSet<int> warnedMaps = new HashSet<int>();

        public HoloAIAnnouncer(Game game)
        {
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % CheckIntervalTicks != 0)
            {
                return;
            }

            foreach (Map map in Find.Maps)
            {
                if (!map.IsPlayerHome || PrismSpeech.FindActiveCore(map) == null)
                {
                    continue;
                }
                Building_GravEngine engine = map.listerBuildings
                    .AllBuildingsColonistOfClass<Building_GravEngine>().FirstOrFallback();
                if (engine == null || engine.MaxFuel <= 0f)
                {
                    continue;
                }

                float fraction = engine.TotalFuel / engine.MaxFuel;
                if (fraction < WarnFraction && !warnedMaps.Contains(map.uniqueID))
                {
                    warnedMaps.Add(map.uniqueID);
                    PrismSpeech.Say(map, "announce_lowfuel");
                }
                else if (fraction > RearmFraction)
                {
                    warnedMaps.Remove(map.uniqueID);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref warnedMaps, "warnedMaps", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && warnedMaps == null)
            {
                warnedMaps = new HashSet<int>();
            }
        }
    }
}
