using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ShipHoloAI
{
    /// <summary>
    /// Walk to a crew member, face them for a moment, then fire the manual interaction:
    /// speech bubble, play-log entry, and a small mood memory for the colonist.
    /// </summary>
    public class JobDriver_HoloChat : JobDriver
    {
        private const int ChatDurationTicks = 180;

        private Pawn Recipient => (Pawn)job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Nothing physical is claimed; a hologram reserves no one.
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Recipient.Dead || !Recipient.Awake());

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil chat = ToilMaker.MakeToil("HoloChat");
            chat.defaultCompleteMode = ToilCompleteMode.Delay;
            chat.defaultDuration = ChatDurationTicks;
            chat.tickAction = () => pawn.rotationTracker.FaceTarget(Recipient);
            yield return chat;

            // Reached only if the delay completed without the job failing.
            Toil fire = ToilMaker.MakeToil("HoloChatFire");
            fire.defaultCompleteMode = ToilCompleteMode.Instant;
            fire.initAction = () =>
            {
                if (Recipient.Spawned && !Recipient.Dead)
                {
                    FireInteraction();
                }
            };
            yield return fire;
        }

        private void FireInteraction()
        {
            InteractionDef intDef = HoloAI_DefOf.HoloAI_Chitchat;
            MoteMaker.MakeInteractionBubble(pawn, Recipient, intDef.interactionMote,
                intDef.GetSymbol(pawn.Faction, pawn.Ideo), intDef.GetSymbolColor(pawn.Faction));
            Find.PlayLog.Add(new PlayLogEntry_Interaction(intDef, pawn, Recipient, null));
            Recipient.needs?.mood?.thoughts?.memories?.TryGainMemory(HoloAI_DefOf.HoloAI_TalkedWithPRISM);

            if (pawn is Pawn_HoloAvatar avatar)
            {
                avatar.SetChatCooldown();
            }
        }
    }
}
