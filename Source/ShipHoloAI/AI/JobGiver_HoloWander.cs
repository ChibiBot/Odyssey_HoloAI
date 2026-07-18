using RimWorld;
using Verse;
using Verse.AI;

namespace ShipHoloAI
{
    /// <summary>
    /// Wanders around the holocore, restricted to the gravship's connected substructure
    /// so the avatar never strolls off her own hull.
    /// </summary>
    public class JobGiver_HoloWander : JobGiver_Wander
    {
        public JobGiver_HoloWander()
        {
            wanderRadius = 12f;
            ticksBetweenWandersRange = new IntRange(120, 320);
            locomotionUrgency = LocomotionUrgency.Amble;
            wanderDestValidator = OnShip;
        }

        protected override IntVec3 GetWanderRoot(Pawn pawn)
        {
            if (pawn is Pawn_HoloAvatar avatar && avatar.holoCore != null && avatar.holoCore.Spawned)
            {
                return avatar.holoCore.Position;
            }
            return pawn.Position;
        }

        private static bool OnShip(Pawn pawn, IntVec3 dest, IntVec3 root)
        {
            TerrainDef foundation = pawn.Map.terrainGrid.FoundationAt(dest);
            return foundation != null && foundation.IsSubstructure;
        }
    }
}
