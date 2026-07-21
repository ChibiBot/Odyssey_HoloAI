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
        public static InteractionDef HoloAI_Lecture;
        public static ThoughtDef HoloAI_TalkedWithPRISM;
        public static ThoughtDef HoloAI_ComfortedByPRISM;
        public static ThoughtDef HoloAI_TuckedInByVESTA;
        public static ThoughtDef HoloAI_AttendedSeminar;
        public static JobDef HoloAI_Chat;
        public static JobDef HoloAI_InstallPersona;
        public static JobDef HoloAI_WardenInteract;
        public static JobDef HoloAI_WardenDeliverFood;
        public static JobDef HoloAI_WardenFeed;
        public static JobDef HoloAI_TuckIn;
        public static JobDef HoloAI_Seminar;
        public static JobDef HoloAI_EmergencyTend;
        public static JobDef HoloAI_CleanFilth;
        public static RulePackDef HoloAI_Announcements;
        public static ThingDef HoloAI_Mote_SpeechText;
        public static HoloPersonaDef HoloAI_Persona_PRISM;
        public static HoloPersonaDef HoloAI_Persona_VESTA;
        public static HoloPersonaDef HoloAI_Persona_HERMES;
        public static HoloPersonaDef HoloAI_Persona_ATHENA;
        public static HoloPersonaDef HoloAI_Persona_ACESO;
        public static HoloPersonaDef HoloAI_Persona_IXIA;

        static HoloAI_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HoloAI_DefOf));
        }
    }
}
