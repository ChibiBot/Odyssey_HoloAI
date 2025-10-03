using RimWorld;
using Verse;
using Verse.AI;

namespace Odyssey_HoloAI;

public class CompProperties_ShipAILeash : CompProperties
{
    public CompProperties_ShipAILeash()
    {
        compClass = typeof(CompShipAILeash);
    }
}

public class CompShipAILeash : ThingComp
{
    private IntVec3 lastValidCell = IntVec3.Invalid;
    private IntVec3 fallbackCell = IntVec3.Invalid;
    private Thing anchor;

    public CompProperties_ShipAILeash Props => (CompProperties_ShipAILeash)props;

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref lastValidCell, "lastValidCell", IntVec3.Invalid);
        Scribe_Values.Look(ref fallbackCell, "fallbackCell", IntVec3.Invalid);
        Scribe_References.Look(ref anchor, "anchor");
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);

        if (parent is not Pawn pawn || !pawn.Spawned)
        {
            return;
        }

        pawn.drafter?.Drafted = false;
        if (pawn.playerSettings != null)
        {
            pawn.playerSettings.hostilityResponse = HostilityResponseMode.Flee;
        }

        pawn.needs?.AddOrRemoveNeedsAsAppropriate();

        if (!lastValidCell.IsValid || !ShipAIGravUtility.IsOnAllowedTile(pawn.Map, lastValidCell))
        {
            if (ShipAIGravUtility.IsOnAllowedTile(pawn.Map, pawn.Position))
            {
                lastValidCell = pawn.Position;
            }
            else
            {
                IntVec3 candidate = ShipAIGravUtility.FindClosestAllowedStandableCell(pawn.Map, pawn.Position);
                if (candidate.IsValid)
                {
                    lastValidCell = candidate;
                }
            }
        }

        if (!fallbackCell.IsValid || !ShipAIGravUtility.IsAllowedStandable(pawn.Map, fallbackCell))
        {
            fallbackCell = ShipAIGravUtility.IsAllowedStandable(pawn.Map, pawn.Position)
                ? pawn.Position
                : ShipAIGravUtility.FindClosestAllowedStandableCell(pawn.Map, pawn.Position);

            if (!fallbackCell.IsValid)
            {
                fallbackCell = ShipAIGravUtility.FindAnyAllowedCell(pawn.Map);
            }
        }
    }

    public void Initialize(Thing anchorThing, IntVec3 spawnCell)
    {
        anchor = anchorThing;
        fallbackCell = spawnCell;

        if (parent is Pawn pawn && pawn.Spawned)
        {
            Map map = pawn.Map;
            if (spawnCell.IsValid && !ShipAIGravUtility.IsAllowedStandable(map, spawnCell))
            {
                IntVec3 corrected = ShipAIGravUtility.FindClosestAllowedStandableCell(map, spawnCell);
                if (corrected.IsValid)
                {
                    fallbackCell = corrected;
                }
            }

            if (!lastValidCell.IsValid)
            {
                if (ShipAIGravUtility.IsOnAllowedTile(map, pawn.Position))
                {
                    lastValidCell = pawn.Position;
                }
                else if (fallbackCell.IsValid)
                {
                    lastValidCell = fallbackCell;
                }
            }
        }
    }

    public override void CompTick()
    {
        base.CompTick();

        if (parent is not Pawn pawn || !pawn.Spawned)
        {
            return;
        }

        Map map = pawn.Map;
        Pawn_PathFollower pather = pawn.pather;
        if (pather != null && pather.Moving)
        {
            LocalTargetInfo destination = pather.Destination;
            if (destination.IsValid && !ShipAIGravUtility.IsOnAllowedTile(map, destination.Cell))
            {
                pawn.jobs?.EndCurrentJob(JobCondition.Incompletable);
                pather.StopDead();
            }
        }

        if (ShipAIGravUtility.IsOnAllowedTile(map, pawn.Position))
        {
            lastValidCell = pawn.Position;
            return;
        }

        IntVec3 returnCell = DetermineReturnCell(map, pawn.Position);
        if (!returnCell.IsValid)
        {
            return;
        }

        pawn.pather?.StopDead();
        ShipAIGravUtility.TeleportPawn(pawn, returnCell, map);
        lastValidCell = returnCell;
    }

    private IntVec3 DetermineReturnCell(Map map, IntVec3 root)
    {
        if (lastValidCell.IsValid && ShipAIGravUtility.IsAllowedStandable(map, lastValidCell))
        {
            return lastValidCell;
        }

        if (fallbackCell.IsValid && ShipAIGravUtility.IsAllowedStandable(map, fallbackCell))
        {
            return fallbackCell;
        }

        if (anchor != null && anchor.Spawned)
        {
            IntVec3 anchorCell = ShipAIGravUtility.FindClosestAllowedStandableCell(anchor.Map, anchor.Position);
            if (anchorCell.IsValid)
            {
                return anchorCell;
            }
        }

        IntVec3 nearby = ShipAIGravUtility.FindClosestAllowedStandableCell(map, root);
        if (nearby.IsValid)
        {
            return nearby;
        }

        return ShipAIGravUtility.FindAnyAllowedCell(map);
    }
}
