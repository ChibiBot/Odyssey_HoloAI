using RimWorld;
using Verse;

namespace ShipHoloAI
{
    [DefOf]
    public static class HoloAI_DefOf
    {
        public static ThingDef HoloAI_HoloCore;
        public static PawnKindDef HoloAI_PRISM;

        static HoloAI_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HoloAI_DefOf));
        }
    }
}
