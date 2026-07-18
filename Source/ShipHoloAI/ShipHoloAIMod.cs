using HarmonyLib;
using Verse;

namespace ShipHoloAI
{
    public class ShipHoloAIMod : Mod
    {
        public ShipHoloAIMod(ModContentPack content) : base(content)
        {
        }
    }

    [StaticConstructorOnStartup]
    public static class HarmonyBootstrap
    {
        static HarmonyBootstrap()
        {
            new Harmony("chibi.shipholoai").PatchAll();
        }
    }
}
