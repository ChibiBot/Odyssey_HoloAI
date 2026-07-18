using RimWorld;
using UnityEngine;
using Verse;

namespace ShipHoloAI
{
    /// <summary>
    /// Renders a fixed vanilla texture (props.texPath) through the hologram filter.
    /// Used for the avatar's body and head so no story tracker is needed.
    /// </summary>
    public class PawnRenderNode_HoloPart : PawnRenderNode
    {
        public PawnRenderNode_HoloPart(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            if (props.texPath.NullOrEmpty())
            {
                return null;
            }
            return HoloGraphicPool.Get(props.texPath, props.color ?? Color.white);
        }
    }

    /// <summary>
    /// Renders the avatar's currently selected vanilla HairDef through the hologram
    /// filter in electric blue. Reads from Pawn_HoloAvatar, not pawn.story.
    /// </summary>
    public class PawnRenderNode_HoloHair : PawnRenderNode
    {
        public PawnRenderNode_HoloHair(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            HairDef hair = (pawn as Pawn_HoloAvatar)?.CurrentHairDef;
            if (hair == null || hair.noGraphic || hair.texPath.NullOrEmpty())
            {
                return null;
            }
            return HoloGraphicPool.Get(hair.texPath, props.color ?? new Color(0.05f, 0.68f, 1f, 0.62f));
        }
    }
}
