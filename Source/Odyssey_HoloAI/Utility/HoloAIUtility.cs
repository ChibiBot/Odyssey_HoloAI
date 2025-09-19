using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Odyssey_HoloAI;

public static class HoloAIUtility
{
    private static PawnKindDef? cachedKindDef;

    public static PawnKindDef? HoloAIKindDef
    {
        get
        {
            cachedKindDef ??= DefDatabase<PawnKindDef>.GetNamedSilentFail("Odyssey_HoloAI");
            return cachedKindDef;
        }
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

        pawn.playerSettings?.Notify_AreaChanged();
        return pawn;
    }

    public static void AssignArea(Pawn pawn, Area_Gravship gravshipArea)
    {
        if (pawn.playerSettings == null)
        {
            pawn.playerSettings = new Pawn_PlayerSettings(pawn);
        }

        pawn.playerSettings.AreaRestriction = gravshipArea;
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
        PawnUtility.TryTeleportThing(pawn, target, pawn.Map);
        return area[target];
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
