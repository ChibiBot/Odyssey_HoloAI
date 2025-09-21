using HarmonyLib;
using Verse;

namespace OdysseyHoloAI
{
    [HarmonyPatch(typeof(Map), nameof(Map.FinalizeInit))]
    public static class Patch_Map_FinalizeInit
    {
        static void Postfix(Map __instance)
        {
            // If our component isn't present, add it.
            if (__instance.GetComponent<MapComponent_GravshipArea>() == null)
            {
                __instance.components.Add(new MapComponent_GravshipArea(__instance));
            }
        }
    }
}
