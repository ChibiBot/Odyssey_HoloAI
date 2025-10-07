using System.Collections.Generic;
using System.Reflection;
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

[HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
internal static class Pawn_TakeDamage_ShipAIPatch
{
    private static bool Prefix(Thing __instance, ref DamageWorker.DamageResult __result)
    {
        if (__instance is not Pawn pawn)
        {
            return true;
        }

        if (!ShipAIUtility.IsShipAI(pawn))
        {
            return true;
        }

        __result = new DamageWorker.DamageResult();
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

[HarmonyPatch]
internal static class Pawn_DraftController_CanDraft_ShipAIPatch
{
    private static bool Prepare()
    {
        return AccessTools.PropertyGetter(typeof(Pawn_DraftController), "CanDraft") != null;
    }

    private static MethodBase TargetMethod()
    {
        return AccessTools.PropertyGetter(typeof(Pawn_DraftController), "CanDraft");
    }

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
        if (__result && nd == NeedDefOf.Indoors)
        {
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (ShipAIUtility.IsShipAI(pawn))
            {
                __result = false;
            }
        }
    }
}

[HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage))]
internal static class JobGiver_Work_TryGiveJob_ShipAIPatch
{
    private static void Postfix(Pawn pawn, ref Job __result)
    {
        if (__result == null || !ShipAIUtility.IsShipAI(pawn))
        {
            return;
        }

        Map map = pawn.Map;
        if (map == null)
        {
            return;
        }

        if (!JobTargetsWithinAllowedArea(map, __result))
        {
            __result = null;
        }
    }

    private static bool JobTargetsWithinAllowedArea(Map map, Job job)
    {
        if (!TargetsWithinAllowedArea(map, job.targetA, job.targetQueueA))
        {
            return false;
        }

        if (!TargetsWithinAllowedArea(map, job.targetB, job.targetQueueB))
        {
            return false;
        }
        
        return true;
    }

    private static bool TargetsWithinAllowedArea(Map map, LocalTargetInfo target, List<LocalTargetInfo> queue)
    {
        if (!TargetWithinAllowedArea(map, target))
        {
            return false;
        }

        if (queue == null)
        {
            return true;
        }

        for (int i = 0; i < queue.Count; i++)
        {
            if (!TargetWithinAllowedArea(map, queue[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TargetWithinAllowedArea(Map map, LocalTargetInfo target)
    {
        if (!target.IsValid)
        {
            return true;
        }

        if (target.HasThing)
        {
            Thing thing = target.Thing;
            if (thing.Spawned && thing.Map == map)
            {
                return ShipAIGravUtility.IsOnAllowedTile(map, thing.Position);
            }

            return true;
        }

        return ShipAIGravUtility.IsOnAllowedTile(map, target.Cell);
    }
}
