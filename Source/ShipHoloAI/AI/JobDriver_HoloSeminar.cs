using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ShipHoloAI
{
    /// <summary>
    /// Walk to the student, lecture for a few seconds, then land the seminar: capped
    /// XP into their highest-passion skill via the vanilla learning pipeline
    /// (Learn(direct: false) applies passion factors and the 4000-XP/day saturation),
    /// a small mood memory that doubles as the cooldown, and the manually-fired
    /// HoloAI_Lecture interaction for the bubble and play log.
    /// </summary>
    public class JobDriver_HoloSeminar : JobDriver
    {
        private const int LectureDurationTicks = 300;
        private const float SeminarXP = 300f;

        private Pawn Student => (Pawn)job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Nothing physical is claimed; a hologram reserves no one.
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Student.Dead || !Student.Awake() || Student.Drafted || Student.InMentalState);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil lecture = ToilMaker.MakeToil("HoloSeminar");
            lecture.defaultCompleteMode = ToilCompleteMode.Delay;
            lecture.defaultDuration = LectureDurationTicks;
            lecture.tickAction = () => pawn.rotationTracker.FaceTarget(Student);
            yield return lecture;

            Toil fire = ToilMaker.MakeToil("HoloSeminarFire");
            fire.defaultCompleteMode = ToilCompleteMode.Instant;
            fire.initAction = () =>
            {
                if (Student.Spawned && !Student.Dead)
                {
                    FireSeminar(pawn, Student);
                }
            };
            yield return fire;
        }

        /// <summary>The student's highest-passion skill (ties broken by level).</summary>
        internal static SkillRecord PickSkill(Pawn student)
        {
            SkillRecord best = null;
            foreach (SkillRecord skill in student.skills.skills)
            {
                if (skill.TotallyDisabled)
                {
                    continue;
                }
                if (best == null
                    || skill.passion > best.passion
                    || (skill.passion == best.passion && skill.Level > best.Level))
                {
                    best = skill;
                }
            }
            return best;
        }

        /// <summary>Static so the self-test can assert the effect directly.</summary>
        internal static void FireSeminar(Pawn avatar, Pawn student)
        {
            SkillRecord skill = PickSkill(student);
            if (skill == null)
            {
                return;
            }
            // direct: false routes through LearnRateFactor — passion multiplier,
            // global learning factor, and the daily saturation cap all apply.
            skill.Learn(SeminarXP);
            student.needs?.mood?.thoughts?.memories?.TryGainMemory(HoloAI_DefOf.HoloAI_AttendedSeminar);

            InteractionDef intDef = HoloAI_DefOf.HoloAI_Lecture;
            MoteMaker.MakeInteractionBubble(avatar, student, intDef.interactionMote,
                intDef.GetSymbol(avatar.Faction, avatar.Ideo), intDef.GetSymbolColor(avatar.Faction));
            Find.PlayLog.Add(new PlayLogEntry_Interaction(intDef, avatar, student, null));

            string line = PrismSpeech.ResolveLine("athena_seminar");
            if (!line.NullOrEmpty())
            {
                MoteMaker.ThrowText(avatar.DrawPos + new Vector3(0f, 0f, 0.65f),
                    avatar.Map, line, new Color(0f, 0.855f, 1f), 5f);
            }
        }
    }
}
