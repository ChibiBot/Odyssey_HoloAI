using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Odyssey_HoloAI;

[StaticConstructorOnStartup]
internal static class ShipAIPatchBootstrap
{
    static ShipAIPatchBootstrap()
    {
        Harmony harmony = new Harmony("Odyssey.HoloAI");
        harmony.PatchAll();
    }
}

internal static class ShipAIUtility
{
    public static bool IsShipAI(Pawn pawn)
    {
        return pawn?.def?.defName == "OH_ShipAI";
    }
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.TakeDamage))]
internal static class Pawn_TakeDamage_ShipAIPatch
{
    private static bool Prefix(Pawn __instance, ref DamageResult __result)
    {
        if (!ShipAIUtility.IsShipAI(__instance))
        {
            return true;
        }

        __result = new DamageResult();
        return false;
    }
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.ThreatDisabled))]
internal static class Pawn_ThreatDisabled_ShipAIPatch
{
    private static void Postfix(Pawn __instance, ref bool __result)
    {
        if (ShipAIUtility.IsShipAI(__instance))
        {
            __result = true;
        }
    }
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.TryGetAttackVerb))]
internal static class Pawn_TryGetAttackVerb_ShipAIPatch
{
    private static void Postfix(Pawn __instance, ref Verb __result)
    {
        if (ShipAIUtility.IsShipAI(__instance))
        {
            __result = null;
        }
    }
}

[HarmonyPatch(typeof(Pawn_DraftController), "get_CanDraft")]
internal static class Pawn_DraftController_CanDraft_ShipAIPatch
{
    private static void Postfix(Pawn_DraftController __instance, ref bool __result)
    {
        Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
        if (ShipAIUtility.IsShipAI(pawn))
        {
            __result = false;
        }
    }
}

[HarmonyPatch(typeof(Pawn_DraftController), "set_Drafted")]
internal static class Pawn_DraftController_SetDrafted_ShipAIPatch
{
    private static bool Prefix(Pawn_DraftController __instance, bool value)
    {
        if (!value)
        {
            return true;
        }

        Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
        if (!ShipAIUtility.IsShipAI(pawn))
        {
            return true;
        }

        return false;
    }
}

[HarmonyPatch(typeof(Pawn_NeedsTracker), "ShouldHaveNeed")]
internal static class Pawn_NeedsTracker_ShouldHaveNeed_ShipAIPatch
{
    private static void Postfix(Pawn_NeedsTracker __instance, NeedDef nd, ref bool __result)
    {
        if (__result && nd == NeedDefOf.Outdoors)
        {
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (ShipAIUtility.IsShipAI(pawn))
            {
                __result = false;
            }
        }
    }
}
