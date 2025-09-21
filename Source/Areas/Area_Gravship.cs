using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Odyssey_HoloAI;

public class Area_Gravship : Area_Allowed
{
    public Area_Gravship(AreaManager areaManager) : base(areaManager)
    {
    }

    public override string Label => "Odyssey.HoloAI.AreaLabel".Translate();

    public override Color Color => new(0.36f, 0.77f, 1.0f);

    public override int ListPriority => 9001;

    public void ResetCells(IEnumerable<IntVec3> cells)
    {
        var toClear = ActiveCells.ToList();
        foreach (var cell in toClear)
        {
            this[cell] = false;
        }

        foreach (var cell in cells)
        {
            if (cell.InBounds(Map))
            {
                this[cell] = true;
            }
        }
    }

    public bool TryFindClosestCell(IntVec3 position, out IntVec3 cell)
    {
        cell = IntVec3.Invalid;
        var candidates = ActiveCells.ToList();
        if (!candidates.Any())
        {
            return false;
        }

        var best = IntVec3.Invalid;
        var bestDist = float.MaxValue;
        foreach (var candidate in candidates)
        {
            var dist = candidate.DistanceToSquared(position);
            if (!best.IsValid || dist < bestDist)
            {
                best = candidate;
                bestDist = dist;
            }
        }

        cell = best;
        return cell.IsValid;
    }
}
