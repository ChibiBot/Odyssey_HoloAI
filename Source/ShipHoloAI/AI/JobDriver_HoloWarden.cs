using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ShipHoloAI
{
    /// <summary>
    /// Walk to a selected prisoner or slave and personally perform the warden
    /// interaction: a recruit attempt (resistance reduction, or the recruitment
    /// itself once resistance is broken) for a secure prisoner, or a suppression
    /// interaction for a secure slave. Mirrors JobDriver_HoloChat's manual-firing
    /// pattern — the avatar has no Pawn_InteractionsTracker, so the vanilla
    /// InteractionWorker and its play-log/mote/letter side effects are fired by hand
    /// instead of going through pawn.interactions.TryInteractWith.
    /// </summary>
    public class JobDriver_HoloWarden : JobDriver
    {
        private const int InteractDurationTicks = 180;

        private Pawn Recipient => (Pawn)job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Nothing physical is claimed; a hologram reserves no one.
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOnMentalState(TargetIndex.A);
            this.FailOnNotAwake(TargetIndex.A);
            this.FailOn(() => !IsValidTarget(Recipient));

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil interact = ToilMaker.MakeToil("HoloWardenInteract");
            interact.defaultCompleteMode = ToilCompleteMode.Delay;
            interact.defaultDuration = InteractDurationTicks;
            interact.tickAction = () => pawn.rotationTracker.FaceTarget(Recipient);
            yield return interact;

            // Reached only if the delay completed without the job failing.
            Toil fire = ToilMaker.MakeToil("HoloWardenFire");
            fire.defaultCompleteMode = ToilCompleteMode.Instant;
            fire.initAction = () =>
            {
                if (Recipient.Spawned && !Recipient.Dead && IsValidTarget(Recipient))
                {
                    FireInteraction();
                }
            };
            yield return fire;
        }

        private static bool IsValidTarget(Pawn recipient)
        {
            if (recipient.guest == null)
            {
                return false;
            }
            if (recipient.IsPrisonerOfColony && recipient.guest.PrisonerIsSecure)
            {
                return true;
            }
            return ModsConfig.IdeologyActive && recipient.IsSlaveOfColony && recipient.guest.SlaveIsSecure;
        }

        private void FireInteraction()
        {
            Pawn recipient = Recipient;
            InteractionDef intDef;
            if (recipient.IsPrisonerOfColony && recipient.guest.PrisonerIsSecure)
            {
                intDef = InteractionDefOf.RecruitAttempt;
            }
            else
            {
                // IsValidTarget already confirmed Ideology + a secure slave.
                intDef = InteractionDefOf.Suppress;
                recipient.mindState.lastSlaveSuppressedTick = Find.TickManager.TicksGame;
            }

            List<RulePackDef> extraSentencePacks = new List<RulePackDef>();
            intDef.Worker.Interacted(pawn, recipient, extraSentencePacks,
                out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets);

            MoteMaker.MakeInteractionBubble(pawn, recipient, intDef.interactionMote,
                intDef.GetSymbol(pawn.Faction, pawn.Ideo), intDef.GetSymbolColor(pawn.Faction));
            PlayLogEntry_Interaction logEntry = new PlayLogEntry_Interaction(intDef, pawn, recipient, extraSentencePacks);
            Find.PlayLog.Add(logEntry);
            if (letterDef != null)
            {
                string text = logEntry.ToGameStringFromPOV(pawn);
                if (!letterText.NullOrEmpty())
                {
                    text = text + "\n\n" + letterText;
                }
                Find.LetterStack.ReceiveLetter(letterLabel, text, letterDef, lookTargets ?? (LookTargets)pawn);
            }
        }
    }
}
