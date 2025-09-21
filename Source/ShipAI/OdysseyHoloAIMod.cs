using HarmonyLib;
using Verse;

namespace OdysseyHoloAI
{
    public class OdysseyHoloAIMod : Mod
    {
        public OdysseyHoloAIMod(ModContentPack content) : base(content)
        {
            var h = new Harmony("odyssey.holoai");
            h.PatchAll();
        }
    }
}
