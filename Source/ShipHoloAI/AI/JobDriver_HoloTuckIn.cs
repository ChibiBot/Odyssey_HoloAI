using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ShipHoloAI
{
    /// <summary>
    /// Drift to the sleeper, linger a moment, then tuck them in: mood memory, a soft
    /// glow, and one of V.E.S.T.A.'s murmured lines. No interaction bubble or play
    /// log — the recipient is asleep and TryGainMemory does not wake pawns.
    /// </summary>
    public class JobDriver_HoloTuckIn : JobDriver
    {
        private const int TuckInDurationTicks = 180;

        private Pawn Sleeper => (Pawn)job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Nothing physical is claimed; a hologram reserves no one.
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Sleeper.Dead || Sleeper.Awake());

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil linger = ToilMaker.MakeToil("HoloTuckIn");
            linger.defaultCompleteMode = ToilCompleteMode.Delay;
            linger.defaultDuration = TuckInDurationTicks;
            linger.tickAction = () => pawn.rotationTracker.FaceTarget(Sleeper);
            yield return linger;

            Toil fire = ToilMaker.MakeToil("HoloTuckInFire");
            fire.defaultCompleteMode = ToilCompleteMode.Instant;
            fire.initAction = () =>
            {
                if (Sleeper.Spawned && !Sleeper.Dead && !Sleeper.Awake())
                {
                    FireTuckIn(pawn, Sleeper);
                }
            };
            yield return fire;
        }

        /// <summary>Static so the self-test can assert the effect directly.</summary>
        internal static void FireTuckIn(Pawn avatar, Pawn sleeper)
        {
            sleeper.needs?.mood?.thoughts?.memories?.TryGainMemory(HoloAI_DefOf.HoloAI_TuckedInByVESTA);
            FleckMaker.ThrowLightningGlow(sleeper.DrawPos, sleeper.Map, 0.4f);
            PrismSpeech.Bark(avatar, "vesta_tuckin");
        }
    }
}
