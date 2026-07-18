using HarmonyLib;
using RimWorld;
using Verse;

namespace ShipHoloAI
{
    /// <summary>
    /// After any letter arrives, let P.R.I.S.M. react in her own voice. All vanilla
    /// ReceiveLetter overloads funnel into this one (verified against 1.6 decompile).
    /// She emits Messages, never Letters, so no recursion is possible.
    /// </summary>
    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter),
        typeof(Letter), typeof(string), typeof(int), typeof(bool))]
    public static class Patch_LetterStack
    {
        public static void Postfix(Letter let)
        {
            if (let?.def == null || Current.ProgramState != ProgramState.Playing)
            {
                return;
            }

            string root = RootFor(let.def);
            if (root == null)
            {
                return;
            }

            Map map = let.lookTargets?.PrimaryTarget.Map ?? Find.CurrentMap;
            PrismSpeech.Say(map, root);
        }

        private static string RootFor(LetterDef def)
        {
            if (def == LetterDefOf.ThreatBig)
            {
                return "announce_threatbig";
            }
            if (def == LetterDefOf.ThreatSmall)
            {
                return "announce_threatsmall";
            }
            if (def == LetterDefOf.NegativeEvent)
            {
                return "announce_negative";
            }
            return null;
        }
    }
}
