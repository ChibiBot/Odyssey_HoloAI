using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ShipHoloAI
{
    // She's wired into the ship's own systems, so doors never apply to her — open, closed,
    // or locked. Nulling both door checks below collapses TryEnterNextPathCell into the
    // same "plain floor cell" branch every Building_Door subclass falls through to, so
    // multi-cell doors need no special-casing.
    [HarmonyPatch(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.NextCellDoorToWaitForOrManuallyOpen))]
    public static class Patch_PathFollower_NextCellDoorToWaitForOrManuallyOpen
    {
        public static void Postfix(Pawn ___pawn, ref Building_Door __result)
        {
            if (__result != null && ___pawn is Pawn_HoloAvatar)
            {
                __result = null;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_PathFollower), "BuildingBlockingNextPathCell")]
    public static class Patch_PathFollower_BuildingBlockingNextPathCell
    {
        public static void Postfix(Pawn ___pawn, ref Building __result)
        {
            if (__result is Building_Door && ___pawn is Pawn_HoloAvatar)
            {
                __result = null;
            }
        }
    }

    // Thanks to the two postfixes above, this only fires once per door cell entered
    // (not once per tick), matching the lightning-glow used elsewhere for her core
    // project/store/teleport moments.
    [HarmonyPatch(typeof(Pawn_PathFollower), "TryEnterNextPathCell")]
    public static class Patch_PathFollower_DoorCrossFlicker
    {
        public static void Prefix(Pawn_PathFollower __instance, Pawn ___pawn)
        {
            if (!(___pawn is Pawn_HoloAvatar avatar) || !avatar.Spawned)
            {
                return;
            }
            IntVec3 nextCell = __instance.nextCell;
            if (nextCell.GetDoor(avatar.Map) == null)
            {
                return;
            }
            FleckMaker.ThrowLightningGlow(avatar.DrawPos, avatar.Map, 0.7f);
            FleckMaker.ThrowLightningGlow(nextCell.ToVector3Shifted(), avatar.Map, 0.7f);
        }
    }

    [HarmonyPatch(typeof(Building_Door), nameof(Building_Door.CheckFriendlyTouched))]
    public static class Patch_Door_CheckFriendlyTouched
    {
        public static bool Prefix(Pawn p)
        {
            return !(p is Pawn_HoloAvatar);
        }
    }

    [HarmonyPatch(typeof(Building_Door), nameof(Building_Door.Notify_PawnApproaching))]
    public static class Patch_Door_NotifyPawnApproaching
    {
        public static bool Prefix(Pawn p)
        {
            return !(p is Pawn_HoloAvatar);
        }
    }
}
