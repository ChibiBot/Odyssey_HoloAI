using RimWorld;
using Verse;

namespace ShipHoloAI
{
    /// <summary>
    /// Ideology's styling-station dialog pointed at the avatar. Passing a null station
    /// puts the base dialog in instant-apply mode (no job, no dye consumption) — right
    /// for a being made of light. The avatar's temp story/style trackers MUST be
    /// attached before construction (the base ctor dereferences them); on close the
    /// picks are copied back and the trackers dropped so they never reach a save.
    /// Only construct while ModsConfig.IdeologyActive.
    /// </summary>
    public class Dialog_HoloStyling : Dialog_StylingStation
    {
        private readonly Pawn_HoloAvatar avatar;

        public Dialog_HoloStyling(Pawn_HoloAvatar avatar)
            : base(avatar, null)
        {
            this.avatar = avatar;
        }

        public override void PostClose()
        {
            base.PostClose();
            avatar.DetachStyleTrackers();
        }
    }
}
