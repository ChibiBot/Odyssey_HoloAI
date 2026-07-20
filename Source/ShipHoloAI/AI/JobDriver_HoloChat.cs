using System.Collections.Generic;
using RimWorld;
using UnityEngine;
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

            // Companion triage: when P.R.I.S.M. reaches someone near their breaking
            // point, the chat lands as a stronger comfort memory and she says one of
            // her comfort lines out loud.
            Pawn_HoloAvatar avatar = pawn as Pawn_HoloAvatar;
            bool comfort = avatar?.holoCore?.ActivePersona == HoloAI_DefOf.HoloAI_Persona_PRISM
                && JobGiver_HoloChat.NearBreak(Recipient);
            Recipient.needs?.mood?.thoughts?.memories?.TryGainMemory(
                comfort ? HoloAI_DefOf.HoloAI_ComfortedByPRISM : HoloAI_DefOf.HoloAI_TalkedWithPRISM);
            if (comfort)
            {
                string line = PrismSpeech.ResolveLine("prism_comfort");
                if (!line.NullOrEmpty())
                {
                    MoteMaker.ThrowText(pawn.DrawPos + new Vector3(0f, 0f, 0.65f),
                        pawn.Map, line, new Color(0f, 0.855f, 1f), 5f);
                }
            }

            avatar?.SetChatCooldown();
        }
    }
}
