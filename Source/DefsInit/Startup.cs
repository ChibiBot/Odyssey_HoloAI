using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Odyssey_HoloAI;

[StaticConstructorOnStartup]
public static class Startup
{
    static Startup()
    {
        EnsureRaceIsInitialised();
    }

    private static void EnsureRaceIsInitialised()
    {
        var raceDef = DefDatabase<ThingDef>.GetNamedSilentFail("Odyssey_HoloAI_Race");
        if (raceDef?.race == null)
        {
            Log.Warning("[Odyssey_HoloAI] Unable to locate Odyssey_HoloAI_Race. The holographic assistant may not load correctly.");
            return;
        }

        raceDef.race.hediffGiverSets ??= new List<HediffGiverSetDef>();
        raceDef.race.hediffGiverSets.Clear();
    }
}
