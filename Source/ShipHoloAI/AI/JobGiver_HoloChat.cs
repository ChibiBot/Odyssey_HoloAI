using Verse;
using Verse.AI;

namespace ShipHoloAI
{
    /// <summary>
    /// Sends the avatar over to a crew member for a word, on an internal cooldown.
    /// (Job logic lands with the interaction milestone; until then this yields nothing.)
    /// </summary>
    public class JobGiver_HoloChat : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            return null;
        }
    }
}
