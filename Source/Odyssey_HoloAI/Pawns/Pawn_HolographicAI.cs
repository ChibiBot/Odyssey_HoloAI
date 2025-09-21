using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Odyssey_HoloAI;

public class Pawn_HolographicAI : Pawn
{
    private static readonly Dictionary<SkillDef, int> TargetSkillLevels = new()
    {
        { SkillDefOf.Medicine, 16 },
        { SkillDefOf.Intellectual, 16 },
        { SkillDefOf.Social, 14 }
    };

    private static readonly WorkTypeDef[] PreferredWorkTypes =
    {
        WorkTypeDefOf.Doctor,
        WorkTypeDefOf.Warden,
        WorkTypeDefOf.Research,
        WorkTypeDefOf.Hauling
    };

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);
        var component = HoloAIUtility.EnsureGravshipComponent(map);
        component?.RegisterPawn(this);
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        var component = HoloAIUtility.EnsureGravshipComponent(Map);
        component?.UnregisterPawn(this);
        base.DeSpawn(DestroyMode.Vanish);
    }

    public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
    {
        absorbed = true;
    }

    public override void Kill(DamageInfo? dinfo, Hediff? exactCulprit = null)
    {
        // The hologram cannot be killed. Simply despawn it instead of going through the death pipeline.
        if (Spawned)
        {
            var component = HoloAIUtility.EnsureGravshipComponent(Map);
            component?.NotifyPawnSuppressed(this);
            DeSpawn(DestroyMode.Vanish);
        }
    }

    public override void PostMake()
    {
        base.PostMake();

        story ??= new Pawn_StoryTracker(this);
        skills ??= new Pawn_SkillTracker(this);
        health ??= new Pawn_HealthTracker(this);
        needs ??= new Pawn_NeedsTracker(this);
        records ??= new Pawn_RecordsTracker(this);
        abilities ??= new Pawn_AbilityTracker(this);
        playerSettings ??= new Pawn_PlayerSettings(this);
        workSettings ??= new Pawn_WorkSettings(this);

        EnsureTraits();
        ApplySkillLevels();
        ConfigureWorkPriorities();
    }

    private void EnsureTraits()
    {
        story.traits ??= new TraitSet(this);

        EnsureTrait(TraitDefOf.Kind);
        EnsureTrait(TraitDefOf.Industriousness, 2);

        story.DisabledWorkTags |= WorkTags.Violent;
    }

    private void EnsureTrait(TraitDef traitDef, int degree = 0)
    {
        if (story.traits.HasTrait(traitDef) && story.traits.DegreeOfTrait(traitDef) == degree)
        {
            return;
        }

        // Remove any existing degrees of the same trait to avoid duplicates.
        for (var i = story.traits.allTraits.Count - 1; i >= 0; i--)
        {
            if (story.traits.allTraits[i].def == traitDef)
            {
                story.traits.allTraits.RemoveAt(i);
            }
        }

        story.traits.GainTrait(new Trait(traitDef, degree, forced: true));
    }

    private void ApplySkillLevels()
    {
        foreach (var (skillDef, level) in TargetSkillLevels)
        {
            var record = skills.GetSkill(skillDef);
            record.Level = level;
            record.xpSinceLastLevel = 0f;
            record.xpSinceMidnight = 0f;
            record.passion = Passion.Major;
        }
    }

    private void ConfigureWorkPriorities()
    {
        workSettings.EnableAndInitialize();

        foreach (var workType in PreferredWorkTypes)
        {
            if (workSettings.WorkIsActive(workType))
            {
                workSettings.SetPriority(workType, 3);
            }
        }

        // Ensure violent work remains effectively disabled even if another mod re-enables it.
        foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
        {
            if ((workType.workTags & WorkTags.Violent) != 0)
            {
                workSettings.SetPriority(workType, 0);
            }
        }
    }
}
