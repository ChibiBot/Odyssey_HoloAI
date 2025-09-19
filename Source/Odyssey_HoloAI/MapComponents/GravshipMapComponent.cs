using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Odyssey_HoloAI;

public class GravshipMapComponent : MapComponent
{
    private Area_Gravship? gravshipArea;
    private readonly HashSet<Pawn_HolographicAI> registeredPawns = new();
    private int lastAreaUpdateTick = -99999;

    public GravshipMapComponent(Map map) : base(map)
    {
    }

    public override void FinalizeInit()
    {
        base.FinalizeInit();
        EnsureAreaExists();
        UpdateArea(true);
    }

    public override void MapComponentTick()
    {
        base.MapComponentTick();

        if (Find.TickManager.TicksGame % 250 == 0)
        {
            UpdateArea();
        }
    }

    public void RegisterPawn(Pawn_HolographicAI pawn)
    {
        EnsureAreaExists();
        if (gravshipArea == null)
        {
            return;
        }

        registeredPawns.Add(pawn);
        HoloAIUtility.AssignArea(pawn, gravshipArea);
    }

    public void UnregisterPawn(Pawn_HolographicAI pawn)
    {
        registeredPawns.Remove(pawn);
    }

    public void NotifyPawnSuppressed(Pawn_HolographicAI pawn)
    {
        registeredPawns.Remove(pawn);
    }

    private void EnsureAreaExists()
    {
        if (gravshipArea != null && gravshipArea.Map == map)
        {
            return;
        }

        gravshipArea = map.areaManager.AllAreas.OfType<Area_Gravship>().FirstOrDefault();
        if (gravshipArea == null)
        {
            gravshipArea = new Area_Gravship(map.areaManager);
            map.areaManager.AllAreas.Add(gravshipArea);
        }
    }

    private void UpdateArea(bool force = false)
    {
        EnsureAreaExists();
        if (gravshipArea == null)
        {
            return;
        }

        if (!force && Find.TickManager.TicksGame - lastAreaUpdateTick < 250)
        {
            return;
        }

        lastAreaUpdateTick = Find.TickManager.TicksGame;

        var settings = HoloAIMod.Settings;
        var allowedTerrains = settings.ResolveAllowedTerrains().ToList();
        var anchorThingDefs = settings.ResolveAnchorThings().ToList();

        if (!allowedTerrains.Any() || !anchorThingDefs.Any())
        {
            gravshipArea.ResetCells(Enumerable.Empty<IntVec3>());
            return;
        }

        var anchors = map.listerThings.AllThings
            .Where(t => anchorThingDefs.Contains(t.def) && t.Faction == Faction.OfPlayer)
            .ToList();

        if (!anchors.Any())
        {
            gravshipArea.ResetCells(Enumerable.Empty<IntVec3>());
            RemoveTrackedPawns();
            return;
        }

        var allowedCells = CollectCells(anchors, allowedTerrains).ToList();
        if (!allowedCells.Any())
        {
            gravshipArea.ResetCells(allowedCells);
            RemoveTrackedPawns();
            return;
        }

        gravshipArea.ResetCells(allowedCells);

        var anchorCell = anchors[0].Position;

        foreach (var pawn in registeredPawns.ToList())
        {
            if (pawn.Destroyed || pawn.Map != map)
            {
                registeredPawns.Remove(pawn);
                continue;
            }

            HoloAIUtility.AssignArea(pawn, gravshipArea);
            HoloAIUtility.TryEnsurePositionInsideArea(pawn, gravshipArea, anchorCell);
        }

        var existing = HoloAIUtility.FindExistingHoloAI(map);
        if (existing is Pawn_HolographicAI existingHolo)
        {
            registeredPawns.Add(existingHolo);
            HoloAIUtility.AssignArea(existingHolo, gravshipArea);
            HoloAIUtility.TryEnsurePositionInsideArea(existingHolo, gravshipArea, anchorCell);
        }
        else if (existing == null)
        {
            var spawned = HoloAIUtility.EnsureHoloAI(map, anchorCell, gravshipArea);
            if (spawned is Pawn_HolographicAI pawn)
            {
                registeredPawns.Add(pawn);
            }
        }
    }

    private IEnumerable<IntVec3> CollectCells(IEnumerable<Thing> anchors, List<TerrainDef> allowedTerrains)
    {
        var allowedSet = new HashSet<TerrainDef>(allowedTerrains);
        var visited = new HashSet<IntVec3>();
        var queue = new Queue<IntVec3>();

        foreach (var anchor in anchors)
        {
            if (!anchor.Spawned || anchor.Map != map)
            {
                continue;
            }

            foreach (var cell in anchor.OccupiedRect().Cells)
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                if (IsAllowedTerrain(cell, allowedSet))
                {
                    queue.Enqueue(cell);
                }
            }
        }

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (!visited.Add(cell))
            {
                continue;
            }

            if (!IsAllowedTerrain(cell, allowedSet))
            {
                continue;
            }

            foreach (var neighbor in GenAdjFast.AdjacentCellsCardinal(cell))
            {
                if (!neighbor.InBounds(map))
                {
                    continue;
                }

                if (IsAllowedTerrain(neighbor, allowedSet) && !visited.Contains(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        return visited;
    }

    private bool IsAllowedTerrain(IntVec3 cell, HashSet<TerrainDef> allowedSet)
    {
        var terrain = map.terrainGrid.TerrainAt(cell);
        return allowedSet.Contains(terrain);
    }

    private void RemoveTrackedPawns()
    {
        foreach (var pawn in registeredPawns.ToList())
        {
            if (pawn.Spawned)
            {
                pawn.DeSpawn(DestroyMode.Vanish);
            }
        }

        registeredPawns.Clear();
    }
}
