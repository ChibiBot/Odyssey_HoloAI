using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ShipHoloAI
{
    /// <summary>
    /// I.X.I.A.'s signature: the Thousand-Locked Gate. While she is projected,
    /// prisoners held on the ship break out four times less often — the MTB factor
    /// sits between vanilla's security doors (x1.1) and Bliss Lobotomy (x10), so
    /// she is a deterrent, never immunity. The factor is appended to the prisoner
    /// tab's break-interval breakdown, same format as vanilla's own lines.
    /// </summary>
    [HarmonyPatch(typeof(PrisonBreakUtility), nameof(PrisonBreakUtility.InitiatePrisonBreakMtbDays))]
    public static class Patch_PrisonBreakMtb
    {
        internal const float MtbFactor = 4f;

        public static void Postfix(Pawn pawn, StringBuilder sb, ref float __result)
        {
            if (__result <= 0f)
            {
                return;
            }
            float factor = BreakMtbFactorFor(pawn);
            if (factor == 1f)
            {
                return;
            }
            __result *= factor;
            // sb != null only for the prisoner-tab breakdown tooltip.
            sb?.AppendLine("  - " + "HoloAI_IxiaBreakFactor".Translate() + ": x" + factor.ToStringPercent());
        }

        /// <summary>Exposed for the self-test: x4 while I.X.I.A. walks this ship.</summary>
        internal static float BreakMtbFactorFor(Pawn prisoner)
        {
            if (prisoner == null || !prisoner.Spawned
                || prisoner.Map.terrainGrid.FoundationAt(prisoner.Position)?.IsSubstructure != true
                || HoloAuraMapComponent.ProjectedOn(prisoner.Map) != HoloAI_DefOf.HoloAI_Persona_IXIA)
            {
                return 1f;
            }
            return MtbFactor;
        }
    }

    /// <summary>When someone dares anyway, the Crimson Warden has opinions.</summary>
    [HarmonyPatch(typeof(PrisonBreakUtility), nameof(PrisonBreakUtility.StartPrisonBreak),
        new[] { typeof(Pawn) })]
    public static class Patch_PrisonBreakAnnounce
    {
        public static void Postfix(Pawn initiator)
        {
            Map map = initiator?.MapHeld;
            if (map != null && HoloAuraMapComponent.ProjectedOn(map) == HoloAI_DefOf.HoloAI_Persona_IXIA)
            {
                PrismSpeech.Say(map, "ixia_prisonbreak");
            }
        }
    }
}
