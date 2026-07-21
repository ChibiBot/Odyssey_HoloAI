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
                if (JobGiver_HoloWarden.WantsConversion(recipient))
                {
                    TryConvertPrisoner(pawn, recipient);
                    return;
                }
                intDef = InteractionDefOf.RecruitAttempt;
                MarkPrisonerInteracted(recipient);
                PrismSpeech.Bark(pawn, "ixia_recruit");
            }
            else
            {
                // IsValidTarget already confirmed Ideology + a secure slave.
                intDef = InteractionDefOf.Suppress;
                recipient.mindState.lastSlaveSuppressedTick = Find.TickManager.TicksGame;
                PrismSpeech.Bark(pawn, "ixia_suppress");
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

        /// <summary>
        /// Vanilla's convert path is built around the warden's own Ideo —
        /// InteractionWorker_ConvertIdeoAttempt reads initiator.Ideo for the certainty
        /// math, letters, and bubble symbol, all of which are null for the avatar. So
        /// the attempt is fired by hand toward guest.ideoForConversion instead: same
        /// certainty formula minus the initiator-ideo terms (memes-vs-traits and relic
        /// factors, treated as 1), same success letter, same play-log sentence packs.
        /// The fail branches that target the initiator socially (resentment memory,
        /// social fight) are dropped — social opinion math about a ToolUser breaks,
        /// and she cannot be fought anyway. Returns true if the prisoner converted.
        /// </summary>
        internal static bool TryConvertPrisoner(Pawn initiator, Pawn recipient)
        {
            MarkPrisonerInteracted(recipient);
            Ideo goal = recipient.guest.ideoForConversion;
            Ideo oldIdeo = recipient.Ideo;
            Precept_Role role = oldIdeo?.GetRole(recipient);
            float reduction = 0.06f
                * initiator.GetStatValue(StatDefOf.ConversionPower)
                * recipient.GetStatValue(StatDefOf.CertaintyLossFactor)
                * Find.Storyteller.difficulty.CertaintyReductionFactor(initiator, recipient);
            if (role != null)
            {
                reduction *= role.def.certaintyLossFactor;
            }
            bool converted = recipient.ideo.IdeoConversionAttempt(reduction, goal);
            PrismSpeech.Bark(initiator, "ixia_convert");

            InteractionDef intDef = InteractionDefOf.ConvertIdeoAttempt;
            List<RulePackDef> extraSentencePacks = new List<RulePackDef>
            {
                converted ? RulePackDefOf.Sentence_ConvertIdeoAttemptSuccess
                          : RulePackDefOf.Sentence_ConvertIdeoAttemptFail
            };
            // The goal ideo stands in for the absent initiator ideo, so the speech
            // bubble shows the ideoligion she is pushing the prisoner toward.
            MoteMaker.MakeInteractionBubble(initiator, recipient, intDef.interactionMote,
                intDef.GetSymbol(initiator.Faction, goal), intDef.GetSymbolColor(initiator.Faction));
            PlayLogEntry_Interaction logEntry = new PlayLogEntry_Interaction(intDef, initiator, recipient, extraSentencePacks);
            Find.PlayLog.Add(logEntry);

            if (converted
                && (PawnUtility.ShouldSendNotificationAbout(initiator) || PawnUtility.ShouldSendNotificationAbout(recipient)))
            {
                string letterText = "LetterConvertIdeoAttempt_Success".Translate(initiator.Named("INITIATOR"),
                    recipient.Named("RECIPIENT"), goal.Named("IDEO"), oldIdeo.Named("OLDIDEO")).Resolve();
                if (role != null)
                {
                    letterText += "\n\n" + "LetterRoleLostLetterIdeoChangedPostfix".Translate(recipient.Named("PAWN"),
                        role.Named("ROLE"), oldIdeo.Named("OLDIDEO")).Resolve();
                }
                Find.LetterStack.ReceiveLetter("LetterLabelConvertIdeoAttempt_Success".Translate(),
                    logEntry.ToGameStringFromPOV(initiator) + "\n\n" + letterText,
                    LetterDefOf.PositiveEvent, new LookTargets(initiator, recipient));
            }
            return converted;
        }

        /// <summary>
        /// What vanilla warden drivers stamp via Toils_Interpersonal.SetLastInteractTime.
        /// ScheduledForInteraction reads exactly these two fields (10000-tick gap, at
        /// most twice a day), so skipping the stamp lets her chain attempts back to
        /// back and trivialize conversion/recruitment.
        /// </summary>
        private static void MarkPrisonerInteracted(Pawn recipient)
        {
            recipient.mindState.lastAssignedInteractTime = Find.TickManager.TicksGame;
            recipient.mindState.interactionsToday++;
        }
    }
}
