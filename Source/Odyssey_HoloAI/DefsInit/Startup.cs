using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Odyssey_HoloAI;

[StaticConstructorOnStartup]
public static class Startup
{
    private static readonly HashSet<string> ReportedRaceMembers = new();

    static Startup()
    {
        ConfigureRaceDef();
    }

    private static void ConfigureRaceDef()
    {
        var raceDef = DefDatabase<ThingDef>.GetNamedSilentFail("Odyssey_HoloAI_Race");
        if (raceDef?.race == null)
        {
            Log.Warning("[Odyssey_HoloAI] Unable to locate Odyssey_HoloAI_Race. The holographic assistant may not load correctly.");
            return;
        }

        var race = raceDef.race;
        TryAssignValue(race, "fleshType", FleshTypeDefOf.Mechanoid);
        TryAssignValue(race, "isFlesh", false);
        TryAssignValue(race, "needsFood", false);
        TryAssignValue(race, "needsRest", false);
        TryAssignValue(race, "needsJoy", false);
        TryAssignValue(race, "needsMood", true);
        TryAssignValue(race, "usesHitPoints", false);
        TryAssignValue(race, "makesFootprints", false);
        TryAssignValue(race, "wildness", 0f);
        TryAssignValue(race, "manhunterOnDamageChance", 0f);
        TryAssignValue(race, "gestationPeriodDays", 0f);
        TryAssignValue(race, "leatherAmount", 0);
        TryAssignValue(race, "immunityGainSpeedFactor", 1f);
        TryAssignValue(race, "lifeExpectancy", 1000f);
        ClearHediffGiverSets(race);
    }

    private static void TryAssignValue(object instance, string memberName, object value)
    {
        if (instance == null)
        {
            return;
        }

        var type = instance.GetType();
        var field = AccessTools.Field(type, memberName);
        var property = AccessTools.Property(type, memberName);

        if (field == null && property == null)
        {
            ReportMissing(memberName);
            return;
        }

        if (field != null && TryConvert(value, field.FieldType, out var fieldValue))
        {
            field.SetValue(instance, fieldValue);
            return;
        }

        if (property?.CanWrite == true && TryConvert(value, property.PropertyType, out var propertyValue))
        {
            property.SetValue(instance, propertyValue);
            return;
        }

        ReportMissing(memberName);
    }

    private static bool TryConvert(object value, Type targetType, out object? converted)
    {
        if (value == null)
        {
            if (!targetType.IsValueType)
            {
                converted = null;
                return true;
            }

            converted = null;
            return false;
        }

        if (targetType.IsInstanceOfType(value))
        {
            converted = value;
            return true;
        }

        try
        {
            converted = Convert.ChangeType(value, targetType);
            return true;
        }
        catch
        {
            if (value is string stringValue && targetType.IsEnum)
            {
                try
                {
                    converted = Enum.Parse(targetType, stringValue, true);
                    return true;
                }
                catch
                {
                    // ignored
                }
            }
        }

        converted = null;
        return false;
    }

    private static void ClearHediffGiverSets(RaceProperties race)
    {
        var type = race.GetType();
        var field = AccessTools.Field(type, "hediffGiverSets");
        if (field != null)
        {
            if (field.GetValue(race) is List<HediffGiverSetDef> list)
            {
                list.Clear();
            }
            else
            {
                field.SetValue(race, new List<HediffGiverSetDef>());
            }

            return;
        }

        var property = AccessTools.Property(type, "hediffGiverSets");
        if (property?.CanWrite == true)
        {
            if (property.GetValue(race) is List<HediffGiverSetDef> list)
            {
                list.Clear();
            }
            else
            {
                property.SetValue(race, new List<HediffGiverSetDef>());
            }
        }
    }

    private static void ReportMissing(string memberName)
    {
        if (ReportedRaceMembers.Add(memberName))
        {
            Log.Warning($"[Odyssey_HoloAI] Unable to configure race property '{memberName}' for Odyssey_HoloAI_Race. The hologram may behave differently in this RimWorld version.");
        }
    }
}
