using RimWorld;
using UnityEngine;
using Verse;

namespace ShipHoloAI
{
    /// <summary>Node properties with hologram-filter extras (set from the tree XML).</summary>
    public class PawnRenderNodeProperties_Holo : PawnRenderNodeProperties
    {
        /// <summary>Fraction of the sprite (from the bottom) that dissolves into
        /// light particles; negative = no dissolution.</summary>
        public float dissolveFrom = -1f;
    }

    /// <summary>
    /// Renders a fixed vanilla texture (props.texPath) through the hologram filter.
    /// Used for the avatar's body, head, and outfit so no story tracker is needed.
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
            Color color = props.color ?? Color.white;
            float dissolve = (props as PawnRenderNodeProperties_Holo)?.dissolveFrom ?? -1f;
            return HoloGraphicPool.Get(props.texPath, new HoloFilter(color, color.a, dissolve));
        }
    }

    /// <summary>
    /// Renders the avatar's currently selected vanilla HairDef through the hologram
    /// filter in her chosen color. Reads from Pawn_HoloAvatar, not pawn.story.
    /// </summary>
    public class PawnRenderNode_HoloHair : PawnRenderNode
    {
        public PawnRenderNode_HoloHair(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            Pawn_HoloAvatar avatar = pawn as Pawn_HoloAvatar;
            HairDef hair = avatar?.CurrentHairDef;
            if (hair == null || hair.noGraphic || hair.texPath.NullOrEmpty())
            {
                return null;
            }
            // RGB follows the avatar's chosen color (styling dialog); alpha stays the
            // node's hologram translucency.
            Color rgb = avatar.HoloHairColor;
            float alpha = (props.color ?? new Color(0f, 0f, 0f, 0.7f)).a;
            return HoloGraphicPool.Get(hair.texPath, new HoloFilter(rgb, alpha));
        }
    }
}
