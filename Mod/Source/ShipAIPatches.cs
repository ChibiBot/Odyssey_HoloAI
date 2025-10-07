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
    private static void Postfix(Pawn pawn, ref ThinkResult __result)
    {
        Job job = __result.Job;
        if (job == null || !ShipAIUtility.IsShipAI(pawn))
        {
            return;
        }

        Map map = pawn.Map;
        if (map == null)
        {
            return;
        }

        if (!JobTargetsWithinAllowedArea(map, job))
        {
            __result = ThinkResult.NoJob;
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

[HarmonyPatch(typeof(WorkGiver_Researcher))]
internal static class WorkGiver_Researcher_TryGiveJob_ShipAIPatch
{
    private static readonly MethodBase targetMethod = AccessTools.Method(typeof(WorkGiver_Researcher), nameof(WorkGiver_Researcher.NonScanJob), new[] { typeof(Pawn) })
        ?? AccessTools.Method(typeof(WorkGiver_Researcher), nameof(WorkGiver_Researcher.NonScanJob), new[] { typeof(Pawn), typeof(bool) });

    private static bool Prepare()
    {
        return targetMethod != null;
    }

    private static MethodBase TargetMethod()
    {
        return targetMethod;
    }

    private static bool Prefix(WorkGiver_Researcher __instance, Pawn pawn, ref Job __result)
    {
        if (!ShipAIUtility.IsShipAI(pawn))
        {
            return true;
        }

        if (!ShipAIResearchUtility.CanShipAIResearchNow())
        {
            __result = null;
            return false;
        }

        if (!MeditationUtility.CanMeditateNow(pawn))
        {
            __result = null;
            return false;
        }

        Job job = MeditationUtility.GetMeditationJob(pawn) ?? JobMaker.MakeJob(JobDefOf.Meditate);
        job.workGiverDef = __instance.def;
        job.ignoreJoyTimeAssignment = true;
        __result = job;
        return false;
    }
}

[HarmonyPatch]
internal static class JobDriver_Meditate_DriverTick_ShipAIPatch
{
    private static readonly MethodInfo targetMethod =
        AccessTools.Method(typeof(JobDriver_Meditate), nameof(JobDriver.DriverTick))
        ?? AccessTools.Method(typeof(JobDriver), nameof(JobDriver.DriverTick));

    private static bool Prepare()
    {
        return targetMethod != null;
    }

    private static MethodBase TargetMethod()
    {
        return targetMethod;
    }

    private static void Postfix(JobDriver __instance)
    {
        if (__instance is not JobDriver_Meditate meditateDriver)
        {
            return;
        }

        Pawn pawn = meditateDriver.pawn;
        if (!ShipAIUtility.IsShipAI(pawn))
        {
            return;
        }

        ShipAIResearchUtility.PerformResearchTick(pawn);
    }
}

internal static class ShipAIResearchUtility
{
    private const float ResearchPerTickFactor = 0.00825f;
    private const float IntellectualXpPerTick = 0.11f;
    private static float? cachedHighTechBenchFactor;

    public static bool CanShipAIResearchNow()
    {
        if (Find.ResearchManager == null)
        {
            return false;
        }

        return Find.ResearchManager.GetProject != null;
    }

    public static void PerformResearchTick(Pawn pawn)
    {
        if (!CanShipAIResearchNow())
        {
            return;
        }

        float researchSpeed = pawn.GetStatValue(StatDefOf.ResearchSpeed);
        float progress = researchSpeed * GetHighTechBenchFactor() * ResearchPerTickFactor;
        Find.ResearchManager.ResearchPerformed(progress, pawn);

        pawn.skills?.Learn(SkillDefOf.Intellectual, IntellectualXpPerTick);
    }

    private static float GetHighTechBenchFactor()
    {
        if (cachedHighTechBenchFactor.HasValue)
        {
            return cachedHighTechBenchFactor.Value;
        }

        float factor = 1f;
        
        ThingDef hiTechResearchBench = DefDatabase<ThingDef>.GetNamedSilentFail("HiTechResearchBench");
        if (hiTechResearchBench != null)
        {
            factor = hiTechResearchBench.GetStatValueAbstract(StatDefOf.ResearchSpeedFactor);
        }

        ThingDef multiAnalyzer = DefDatabase<ThingDef>.GetNamedSilentFail("MultiAnalyzer");
        if (multiAnalyzer != null)
        {
            factor += multiAnalyzer.GetStatValueAbstract(StatDefOf.ResearchSpeedFactor);
        }

        cachedHighTechBenchFactor = factor;
        return factor;
    }
}
