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

        /// <summary>"body" | "outfit" | "head": resolve the texture from the
        /// avatar's chosen appearance (body type / head type defs — modded defs
        /// included, they enumerate the same databases). Empty = the fixed
        /// texPath, which also remains the fallback.</summary>
        public string holoPart;
    }

    /// <summary>
    /// Renders a vanilla texture through the hologram filter. The texture is either
    /// fixed (props.texPath) or resolved live from the avatar's chosen appearance
    /// (body type, outfit variant, head type) so no story tracker is needed.
    /// </summary>
    public class PawnRenderNode_HoloPart : PawnRenderNode
    {
        public PawnRenderNode_HoloPart(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            string texPath = ResolveTexPath(pawn as Pawn_HoloAvatar);
            if (texPath.NullOrEmpty())
            {
                return null;
            }
            Color color = props.color ?? Color.white;
            float dissolve = (props as PawnRenderNodeProperties_Holo)?.dissolveFrom ?? -1f;
            return HoloGraphicPool.Get(texPath, new HoloFilter(color, color.a, dissolve));
        }

        private string ResolveTexPath(Pawn_HoloAvatar avatar)
        {
            string part = (props as PawnRenderNodeProperties_Holo)?.holoPart;
            if (avatar != null && !part.NullOrEmpty())
            {
                switch (part)
                {
                    case "body":
                        return avatar.CurrentBodyType?.bodyNakedGraphicPath ?? props.texPath;
                    case "outfit":
                        return Pawn_HoloAvatar.OutfitPathFor(avatar.CurrentBodyType) ?? props.texPath;
                    case "head":
                        return avatar.CurrentHeadType?.graphicPath ?? props.texPath;
                }
            }
            return props.texPath;
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
