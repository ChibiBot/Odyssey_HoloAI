using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Odyssey_HoloAI;

public static class HoloAIUtility
{
    private static PawnKindDef? cachedKindDef;
    private static readonly PropertyInfo? AreaRestrictionProperty = AccessTools.Property(typeof(Pawn_PlayerSettings), "AreaRestriction");
    private static readonly FieldInfo? AreaRestrictionField = AccessTools.Field(typeof(Pawn_PlayerSettings), "areaRestriction");
    private static readonly MethodInfo? NotifyAreaChangedMethod = AccessTools.Method(typeof(Pawn_PlayerSettings), "Notify_AreaChanged")
        ?? AccessTools.Method(typeof(Pawn_PlayerSettings), "Notify_AreaRestrictionChanged");
    private static readonly FieldInfo? MapComponentsField = AccessTools.Field(typeof(Map), "components");
    private static readonly PropertyInfo? MapComponentsProperty = AccessTools.Property(typeof(Map), "components");
    private static bool areaRestrictionAssignmentFailed;
    private static bool mapComponentInjectionFailed;

    public static PawnKindDef? HoloAIKindDef
    {
        get
        {
            cachedKindDef ??= DefDatabase<PawnKindDef>.GetNamedSilentFail("Odyssey_HoloAI");
            return cachedKindDef;
        }
    }

    public static GravshipMapComponent? EnsureGravshipComponent(Map? map)
    {
        if (map == null)
        {
            return null;
        }

        var component = map.GetComponent<GravshipMapComponent>();
        if (component != null)
        {
            return component;
        }

        if (MapComponentsField?.GetValue(map) is List<MapComponent> components)
        {
            component = new GravshipMapComponent(map);
            components.Add(component);
            component.FinalizeInit();
            return component;
        }

        if (MapComponentsProperty?.GetValue(map) is IList<MapComponent> propertyComponents)
        {
            component = new GravshipMapComponent(map);
            propertyComponents.Add(component);
            component.FinalizeInit();
            return component;
        }

        if (!mapComponentInjectionFailed)
        {
            Log.Warning("[Odyssey_HoloAI] Unable to attach gravship map component; map components field unavailable.");
            mapComponentInjectionFailed = true;
        }

        return null;
    }

    public static bool IsHoloAI(Pawn pawn)
    {
        return pawn?.kindDef == HoloAIKindDef;
    }

    public static Pawn? FindExistingHoloAI(Map map)
    {
        return map.mapPawns?.SpawnedPawnsInFaction(Faction.OfPlayer)
            .FirstOrDefault(p => p is Pawn { Spawned: true } pawn && IsHoloAI(pawn));
    }

    public static Pawn? EnsureHoloAI(Map map, IntVec3 fallbackCell, Area_Gravship gravshipArea)
    {
        EnsureGravshipComponent(map);

        var existing = FindExistingHoloAI(map);
        if (existing != null)
        {
            AssignArea(existing, gravshipArea);
            return existing;
        }

        var kind = HoloAIKindDef;
        if (kind == null)
        {
            return null;
        }
        var pawn = PawnGenerator.GeneratePawn(kind, Faction.OfPlayer);
        if (pawn == null)
        {
            return null;
        }

        if (!TryGetSpawnCell(map, gravshipArea, fallbackCell, out var spawnCell))
        {
            spawnCell = fallbackCell;
        }

        GenSpawn.Spawn(pawn, spawnCell, map, WipeMode.Vanish);
        AssignArea(pawn, gravshipArea);

        if (pawn.drafter == null)
        {
            pawn.drafter = new Pawn_DraftController(pawn);
        }

        return pawn;
    }

    public static void AssignArea(Pawn pawn, Area_Gravship gravshipArea)
    {
        if (pawn.playerSettings == null)
        {
            pawn.playerSettings = new Pawn_PlayerSettings(pawn);
        }

        var settings = pawn.playerSettings;
        var assigned = false;
        if (AreaRestrictionProperty?.CanWrite == true)
        {
            AreaRestrictionProperty.SetValue(settings, gravshipArea);
            assigned = true;
        }
        else if (AreaRestrictionField != null)
        {
            AreaRestrictionField.SetValue(settings, gravshipArea);
            assigned = true;
        }

        if (!assigned && !areaRestrictionAssignmentFailed)
        {
            Log.Warning("[Odyssey_HoloAI] Unable to assign area restriction for HoloAI pawn; property not found.");
            areaRestrictionAssignmentFailed = true;
        }

        if (assigned)
        {
            NotifyAreaChangedMethod?.Invoke(settings, System.Array.Empty<object?>());
        }
    }

    public static bool TryEnsurePositionInsideArea(Pawn pawn, Area_Gravship area, IntVec3 anchor)
    {
        if (area[pawn.Position])
        {
            return true;
        }

        if (!TryGetSpawnCell(pawn.Map, area, anchor, out var target))
        {
            target = anchor;
        }

        if (!target.IsValid)
        {
            return false;
        }

        pawn.pather?.StopDead();
        if (!TryTeleportPawn(pawn, target, pawn.Map))
        {
            return false;
        }
        return area[target];
    }

    private static bool TryTeleportPawn(Pawn pawn, IntVec3 cell, Map? map)
    {
        if (map == null || !cell.IsValid || !cell.InBounds(map))
        {
            return false;
        }

        var rotation = pawn.Rotation;
        var spawned = pawn.Spawned;
        if (spawned)
        {
            if (pawn.Map != map)
            {
                return false;
            }

            pawn.DeSpawn(DestroyMode.Vanish);
        }

        GenSpawn.Spawn(pawn, cell, map, rotation, WipeMode.Vanish);
        return true;
    }

    private static bool TryGetSpawnCell(Map map, Area_Gravship area, IntVec3 fallback, out IntVec3 cell)
    {
        if (area.TryFindClosestCell(fallback, out var areaCell))
        {
            cell = areaCell;
            return true;
        }

        if (fallback.InBounds(map))
        {
            cell = fallback;
            return true;
        }

        var candidates = area.ActiveCells.ToList();
        if (candidates.Any())
        {
            cell = candidates[0];
            return true;
        }

        cell = IntVec3.Invalid;
        return false;
    }
}
