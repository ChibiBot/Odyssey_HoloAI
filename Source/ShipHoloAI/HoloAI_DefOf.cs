using RimWorld;
using Verse;

namespace ShipHoloAI
{
    [DefOf]
    public static class HoloAI_DefOf
    {
        public static ThingDef HoloAI_HoloCore;
        public static PawnKindDef HoloAI_PRISM;
        public static InteractionDef HoloAI_Chitchat;
        public static ThoughtDef HoloAI_TalkedWithPRISM;
        public static JobDef HoloAI_Chat;

        static HoloAI_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HoloAI_DefOf));
        }
    }
}
