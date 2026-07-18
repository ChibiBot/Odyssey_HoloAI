using RimWorld;
using UnityEngine;
using Verse;

namespace ShipHoloAI
{
    public class CompProperties_HoloShimmer : CompProperties
    {
        public CompProperties_HoloShimmer()
        {
            compClass = typeof(CompHoloShimmer);
        }
    }

    /// <summary>
    /// Per-frame pulse: redraws the current hair texture over the pawn as an additive
    /// glow whose alpha breathes on a sine. Drawn via PostDraw, so it bypasses the pawn
    /// render cache and animates even while the base sprite is atlased.
    /// </summary>
    public class CompHoloShimmer : ThingComp
    {
        private static readonly Vector2 OverlaySize = new Vector2(1.5f, 1.5f);
        private static MaterialPropertyBlock mpb;

        public override void PostDraw()
        {
            if (!(parent is Pawn_HoloAvatar avatar) || !avatar.Spawned)
            {
                return;
            }
            DrawEmitterRing(avatar);
            HairDef hair = avatar.CurrentHairDef;
            if (hair == null || hair.noGraphic)
            {
                return;
            }
            Rot4 rot = avatar.Rotation;
            Material mat = HoloGraphicPool.GetPulseMat(hair.texPath, rot);
            if (mat == null)
            {
                return;
            }

            float phase = Time.realtimeSinceStartup * 2.2f + avatar.thingIDNumber * 0.7f;
            float alpha = 0.10f + 0.08f * Mathf.Sin(phase);

            Color pulseColor = avatar.HoloHairColor;
            mpb = mpb ?? new MaterialPropertyBlock();
            mpb.SetColor(ShaderPropertyIDs.Color, new Color(pulseColor.r, pulseColor.g, pulseColor.b, alpha));

            Vector3 pos = avatar.DrawPos + HeadOffsetFor(rot);
            pos.y += 0.06f; // just above the hair layer
            Mesh mesh = rot == Rot4.West
                ? MeshPool.GridPlaneFlip(OverlaySize)
                : MeshPool.GridPlane(OverlaySize);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one), mat, 0, null, 0, mpb);
        }

        /// <summary>Soft projection ring at her feet — she is being projected, after
        /// all. Drawn at floor altitude so pawns and items layer above it.</summary>
        private static void DrawEmitterRing(Pawn_HoloAvatar avatar)
        {
            Material mat = HoloGraphicPool.EmitterRingMat;
            if (mat == null)
            {
                return;
            }
            float breathe = 0.30f + 0.10f * Mathf.Sin(Time.realtimeSinceStartup * 1.6f + avatar.thingIDNumber);
            mpb = mpb ?? new MaterialPropertyBlock();
            mpb.SetColor(ShaderPropertyIDs.Color, new Color(1f, 1f, 1f, breathe));
            Vector3 pos = avatar.DrawPos;
            pos.z -= 0.28f;
            pos.y = AltitudeLayer.FloorEmplacement.AltitudeFor() + 0.005f;
            Mesh mesh = MeshPool.GridPlane(new Vector2(1.25f, 0.9f));
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one), mat, 0, null, 0, mpb);
        }

        private static Vector3 HeadOffsetFor(Rot4 rot)
        {
            if (rot == Rot4.East)
            {
                return new Vector3(0.10f, 0f, 0.34f);
            }
            if (rot == Rot4.West)
            {
                return new Vector3(-0.10f, 0f, 0.34f);
            }
            return new Vector3(0f, 0f, 0.34f);
        }
    }
}
