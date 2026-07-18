using HarmonyLib;
using RimWorld;
using Verse;

namespace ShipHoloAI
{
    /// <summary>
    /// The avatar absorbs all damage, so leaving her attackable would make her a
    /// melee-XP training dummy and a decoy that raiders waste attacks on. Two hooks
    /// (verified against the 1.6 decompile) remove her from both sides:
    /// AI target scans consult Pawn.ThreatDisabled via AttackTargetFinder, and the
    /// player's force-attack float menu funnels through DraftedAttack.CanTarget —
    /// which would otherwise APPROVE her via the NonHumanlikeOrWildMan branch.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.ThreatDisabled))]
    public static class Patch_Pawn_ThreatDisabled
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (__instance is Pawn_HoloAvatar)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(FloatMenuOptionProvider_DraftedAttack), "CanTarget")]
    public static class Patch_DraftedAttack_CanTarget
    {
        public static void Postfix(Thing clickedThing, ref bool __result)
        {
            if (clickedThing is Pawn_HoloAvatar)
            {
                __result = false;
            }
        }
    }
}
