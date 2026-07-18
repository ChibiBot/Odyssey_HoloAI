using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ShipHoloAI
{
    /// <summary>
    /// The hologram filter. Takes vanilla pawn textures (body, head, any HairDef),
    /// bakes edge-faded translucent copies at runtime (solid center, alpha falling off
    /// toward the silhouette edge), and serves cached materials/graphics built on the
    /// Transparent shader. Also provides additive glow materials for the pulse overlay.
    /// </summary>
    public static class HoloGraphicPool
    {
        private const float EdgeFadePx = 9f;

        private static readonly Dictionary<string, HoloGraphic> graphicCache = new Dictionary<string, HoloGraphic>();
        private static readonly Dictionary<string, Material> pulseMatCache = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Texture2D> processedTexCache = new Dictionary<string, Texture2D>();

        private static readonly string[] RotSuffixes = { "_north", "_east", "_south", "_west" };

        public static HoloGraphic Get(string texPath, Color color)
        {
            string key = texPath + "|" + color;
            if (graphicCache.TryGetValue(key, out HoloGraphic cached))
            {
                return cached;
            }

            Material[] mats = new Material[4];
            for (int rotIndex = 0; rotIndex < 4; rotIndex++)
            {
                Texture2D tex = ProcessedTexture(texPath, RotSuffixes[rotIndex]);
                if (tex != null)
                {
                    mats[rotIndex] = MaterialPool.MatFrom(new MaterialRequest(tex, ShaderDatabase.Transparent, color));
                }
            }
            HoloGraphic graphic = new HoloGraphic(mats);
            graphicCache[key] = graphic;
            return graphic;
        }

        /// <summary>Additive glow material for the shimmer overlay (west reuses east).</summary>
        public static Material GetPulseMat(string texPath, Rot4 rot)
        {
            string suffix = RotSuffixes[rot == Rot4.West ? Rot4.East.AsInt : rot.AsInt];
            string key = texPath + suffix + "|pulse";
            if (pulseMatCache.TryGetValue(key, out Material cached))
            {
                return cached;
            }
            Texture2D tex = ProcessedTexture(texPath, suffix);
            Material mat = tex == null
                ? null
                : MaterialPool.MatFrom(new MaterialRequest(tex, ShaderDatabase.MoteGlow, Color.white));
            pulseMatCache[key] = mat;
            return mat;
        }

        private static Texture2D ProcessedTexture(string texPath, string suffix)
        {
            string key = texPath + suffix;
            if (processedTexCache.TryGetValue(key, out Texture2D cached))
            {
                return cached;
            }

            Texture2D source = ContentFinder<Texture2D>.Get(texPath + suffix, reportFailure: false);
            if (source == null && suffix == "_west")
            {
                // Most pawn textures have no west variant; the flipped east mesh mirrors it.
                source = ContentFinder<Texture2D>.Get(texPath + "_east", reportFailure: false);
            }
            Texture2D result = source == null ? null : BakeEdgeFade(source);
            processedTexCache[key] = result;
            return result;
        }

        private static Texture2D BakeEdgeFade(Texture2D source)
        {
            Texture2D readable = MakeReadable(source);
            int w = readable.width;
            int h = readable.height;
            Color32[] px = readable.GetPixels32();

            // Two-pass chamfer distance transform: distance (in px) to the nearest
            // transparent pixel or image border, then a smooth alpha ramp over EdgeFadePx.
            float[] dist = new float[px.Length];
            const float Big = 1e9f;
            const float Diag = 1.41421f;
            for (int i = 0; i < px.Length; i++)
            {
                dist[i] = px[i].a < 10 ? 0f : Big;
            }
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (dist[i] == 0f) continue;
                    if (x == 0 || y == 0 || x == w - 1 || y == h - 1) { dist[i] = 1f; continue; }
                    float d = dist[i];
                    d = Mathf.Min(d, dist[i - 1] + 1f);
                    d = Mathf.Min(d, dist[i - w] + 1f);
                    d = Mathf.Min(d, dist[i - w - 1] + Diag);
                    d = Mathf.Min(d, dist[i - w + 1] + Diag);
                    dist[i] = d;
                }
            }
            for (int y = h - 1; y >= 0; y--)
            {
                for (int x = w - 1; x >= 0; x--)
                {
                    int i = y * w + x;
                    if (dist[i] == 0f) continue;
                    float d = dist[i];
                    if (x < w - 1) d = Mathf.Min(d, dist[i + 1] + 1f);
                    if (y < h - 1)
                    {
                        d = Mathf.Min(d, dist[i + w] + 1f);
                        if (x < w - 1) d = Mathf.Min(d, dist[i + w + 1] + Diag);
                        if (x > 0) d = Mathf.Min(d, dist[i + w - 1] + Diag);
                    }
                    dist[i] = d;
                }
            }

            float scale = w / 128f; // fade width tuned for 128px textures, scale for others
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a == 0) continue;
                float t = Mathf.Clamp01(dist[i] / (EdgeFadePx * scale));
                t = t * t * (3f - 2f * t); // smoothstep
                px[i].a = (byte)(px[i].a * t);
            }

            readable.SetPixels32(px);
            readable.Apply(updateMipmaps: true);
            return readable;
        }

        private static Texture2D MakeReadable(Texture2D source)
        {
            RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            Graphics.Blit(source, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, mipChain: true);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            copy.name = source.name + "_holo";
            return copy;
        }
    }

    /// <summary>Minimal four-rotation graphic serving pre-built hologram materials.</summary>
    public class HoloGraphic : Graphic
    {
        private readonly Material[] mats;

        public HoloGraphic(Material[] mats)
        {
            this.mats = mats;
            drawSize = Vector2.one;
        }

        public override Material MatSingle => mats[Rot4.South.AsInt] ?? mats[Rot4.East.AsInt];

        public override Material MatNorth => mats[Rot4.North.AsInt] ?? MatSingle;

        public override Material MatEast => mats[Rot4.East.AsInt] ?? MatSingle;

        public override Material MatSouth => mats[Rot4.South.AsInt] ?? MatSingle;

        public override Material MatWest => mats[Rot4.West.AsInt] ?? MatEast;

        public override Material MatAt(Rot4 rot, Thing thing = null)
        {
            return mats[rot.AsInt] ?? MatSingle;
        }
    }
}
