using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Odyssey_HoloAI;

internal static class ShipAIGravUtility
{
    private static readonly string[] GravshipTerrainPrefixes =
    {
        "Odyssey_Gravship",
        "OH_Gravship",
        "Gravship"
    };

    private const string SubstructureToken = "Substructure";

    public static bool IsOnAllowedTile(Map map, IntVec3 cell)
    {
        if (map == null || !cell.InBounds(map))
        {
            return false;
        }

        TerrainDef terrain = map.terrainGrid?.TerrainAt(cell);
        if (terrain == null)
        {
            return IsOnAllowedStructure(map, cell);
        }

        string defName = terrain.defName;
        if (MatchesAllowedSurface(defName))
        {
            return true;
        }

        return IsOnAllowedStructure(map, cell);
    }

    public static bool IsAllowedStandable(Map map, IntVec3 cell)
    {
        return IsOnAllowedTile(map, cell) && cell.Standable(map);
    }

    public static IntVec3 FindClosestAllowedStandableCell(Map map, IntVec3 root, float maxRadius = 30f)
    {
        if (map == null || !root.IsValid)
        {
            return IntVec3.Invalid;
        }

        foreach (IntVec3 cell in GenRadial.RadialCellsAround(root, maxRadius, useCenter: true))
        {
            if (IsAllowedStandable(map, cell))
            {
                return cell;
            }
        }

        return IntVec3.Invalid;
    }

    public static IntVec3 FindAnyAllowedCell(Map map)
    {
        if (map == null)
        {
            return IntVec3.Invalid;
        }

        foreach (IntVec3 cell in map.AllCells)
        {
            if (IsAllowedStandable(map, cell))
            {
                return cell;
            }
        }

        return IntVec3.Invalid;
    }

    public static void TeleportPawn(Pawn pawn, IntVec3 cell, Map map)
    {
        if (pawn == null || map == null || !cell.IsValid)
        {
            return;
        }

        bool spawned = pawn.Spawned;
        if (spawned)
        {
            pawn.DeSpawn(DestroyMode.Vanish);
        }

        GenSpawn.Spawn(pawn, cell, map);
    }

    private static bool MatchesAllowedSurface(string defName)
    {
        if (defName.NullOrEmpty())
        {
            return false;
        }

        for (int i = 0; i < GravshipTerrainPrefixes.Length; i++)
        {
            if (defName.StartsWith(GravshipTerrainPrefixes[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return defName.IndexOf(SubstructureToken, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsOnAllowedStructure(Map map, IntVec3 cell)
    {
        if (map?.thingGrid == null)
        {
            return false;
        }

        List<Thing> thingList = map.thingGrid.ThingsListAtFast(cell);
        for (int i = 0; i < thingList.Count; i++)
        {
            Thing thing = thingList[i];
            if (thing?.def == null)
            {
                continue;
            }

            if (MatchesAllowedSurface(thing.def.defName))
            {
                return true;
            }
        }

        return false;
    }
}
