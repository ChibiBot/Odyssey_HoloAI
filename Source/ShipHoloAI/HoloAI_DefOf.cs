using RimWorld;
using Verse;

namespace ShipHoloAI
{
    [DefOf]
    public static class HoloAI_DefOf
    {
        public static ThingDef HoloAI_HoloCore;

        static HoloAI_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HoloAI_DefOf));
        }
    }
}
