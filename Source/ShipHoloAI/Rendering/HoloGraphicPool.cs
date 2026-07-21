using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ShipHoloAI
{
    /// <summary>
    /// Per-part parameters for the hologram filter. RGB of tint drives the gradient
    /// ramp; dissolveFrom (0..1 from the top, or negative for none) makes the lower
    /// portion of the sprite break apart into light particles.
    /// </summary>
    public struct HoloFilter
    {
        public Color tint;
        public float alpha;
        public float dissolveFrom;

        public HoloFilter(Color tint, float alpha, float dissolveFrom = -1f)
        {
            this.tint = tint;
            this.alpha = alpha;
            this.dissolveFrom = dissolveFrom;
        }

        public string Key => tint.ToString() + "|" + alpha + "|" + dissolveFrom;
    }

    /// <summary>
    /// The hologram filter (concept: Concept/HoloCore_Projection.png). Takes vanilla
    /// pawn textures and bakes translucent "made of light" copies:
    ///  - luminance mapped through a blue gradient ramp (deep -> electric -> pale)
    ///  - emissive lift and subtle horizontal scanlines
    ///  - soft rim glow behind the silhouette
    ///  - edge fade (solid core, ethereal edges)
    ///  - optional lower-body dissolution into dithered light particles
    /// Serves cached materials on the Transparent shader, plus additive glow
    /// materials for the shimmer/emitter overlays.
    /// The attribute satisfies vanilla's startup audit for types holding
    /// asset-type statics (the Material caches below); all materials are built
    /// lazily on the main thread in the render path regardless.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HoloGraphicPool
    {
        private const float EdgeFadePx = 7f;
        private const int ScanlinePeriod = 3;
        private const float ScanlineDim = 0.86f;
        private const float RimGlowAlpha = 0.45f;
        private const float RimGlowBlurPx = 2.5f;

        private static readonly Dictionary<string, HoloGraphic> graphicCache = new Dictionary<string, HoloGraphic>();
        private static readonly Dictionary<string, Material> pulseMatCache = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Texture2D> processedTexCache = new Dictionary<string, Texture2D>();
        private static Material emitterRingMat;

        private static readonly string[] RotSuffixes = { "_north", "_east", "_south", "_west" };

        public static HoloGraphic Get(string texPath, HoloFilter filter)
        {
            string key = texPath + "|" + filter.Key;
            if (graphicCache.TryGetValue(key, out HoloGraphic cached))
            {
                return cached;
            }

            Material[] mats = new Material[4];
            for (int rotIndex = 0; rotIndex < 4; rotIndex++)
            {
                Texture2D tex = ProcessedTexture(texPath, RotSuffixes[rotIndex], filter);
                if (tex != null)
                {
                    mats[rotIndex] = MaterialPool.MatFrom(new MaterialRequest(tex, ShaderDatabase.Transparent, Color.white));
                }
            }
            HoloGraphic graphic = new HoloGraphic(mats);
            graphicCache[key] = graphic;
            return graphic;
        }

        /// <summary>Legacy entry: flat color = tint RGB + alpha, no dissolution.</summary>
        public static HoloGraphic Get(string texPath, Color color)
        {
            return Get(texPath, new HoloFilter(color, color.a));
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
            Texture2D tex = ProcessedTexture(texPath, suffix,
                new HoloFilter(new Color(0.4f, 0.8f, 1f), 1f));
            Material mat = tex == null
                ? null
                : MaterialPool.MatFrom(new MaterialRequest(tex, ShaderDatabase.MoteGlow, Color.white));
            pulseMatCache[key] = mat;
            return mat;
        }

        /// <summary>Soft additive emitter ring drawn beneath the projected avatar.</summary>
        public static Material EmitterRingMat
        {
            get
            {
                if (emitterRingMat == null)
                {
                    emitterRingMat = MaterialPool.MatFrom(
                        new MaterialRequest(BakeEmitterRing(), ShaderDatabase.MoteGlow, Color.white));
                }
                return emitterRingMat;
            }
        }

        private static Texture2D ProcessedTexture(string texPath, string suffix, HoloFilter filter)
        {
            string key = texPath + suffix + "|" + filter.Key;
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
            Texture2D result = source == null ? null : BakeHologram(source, filter);
            processedTexCache[key] = result;
            return result;
        }

        private static Texture2D BakeHologram(Texture2D source, HoloFilter filter)
        {
            Texture2D readable = MakeReadable(source);
            int w = readable.width;
            int h = readable.height;
            Color32[] src = readable.GetPixels32();
            float scale = w / 128f;

            float[] edge = EdgeDistance(src, w, h);

            // Gradient ramp stops derived from the part tint.
            Color deep = Color.Lerp(new Color(0.03f, 0.08f, 0.30f), filter.tint, 0.35f);
            Color mid = filter.tint;
            Color high = Color.Lerp(filter.tint, new Color(0.95f, 0.99f, 1f), 0.75f);

            Color32[] outPx = new Color32[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                Color32 p = src[i];
                if (p.a == 0)
                {
                    continue;
                }
                int y = i / w;
                int x = i - y * w;

                // Luminance -> emissive lift -> blue ramp
                float lum = (0.299f * p.r + 0.587f * p.g + 0.114f * p.b) / 255f;
                float t = Mathf.Pow(lum, 0.8f);
                Color c = t < 0.5f
                    ? Color.Lerp(deep, mid, t * 2f)
                    : Color.Lerp(mid, high, (t - 0.5f) * 2f);

                float a = (p.a / 255f) * filter.alpha;

                // Edge fade (solid core, soft silhouette)
                float ef = Mathf.Clamp01(edge[i] / (EdgeFadePx * scale));
                a *= ef * ef * (3f - 2f * ef);

                // Scanlines: slightly darker and more transparent raster rows.
                // Texture rows map to screen rows, so this reads as classic raster.
                if (y % ScanlinePeriod == 0)
                {
                    c = Color.Lerp(Color.black, c, ScanlineDim + 0.06f);
                    a *= ScanlineDim;
                }

                // Lower dissolution into light particles (texture y=0 is BOTTOM row
                // in Unity, so low y = feet).
                if (filter.dissolveFrom >= 0f)
                {
                    float rowFrac = (float)y / h; // 0 at bottom
                    float dissolveBand = filter.dissolveFrom;
                    if (rowFrac < dissolveBand)
                    {
                        float f = 1f - rowFrac / dissolveBand; // 0 at band top -> 1 at bottom
                        // Deterministic speckle: pixels wink out progressively, the
                        // survivors brighten (energy motes).
                        float noise = Hash01(x * 73856093 ^ y * 19349663);
                        if (noise < f)
                        {
                            a = 0f;
                        }
                        else
                        {
                            c = Color.Lerp(c, high, f * 0.8f);
                            a *= 1f - f * 0.35f;
                        }
                    }
                }

                outPx[i] = new Color32(
                    (byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f),
                    (byte)(Mathf.Clamp01(a) * 255f));
            }

            // Rim glow: blurred silhouette in mid-tone, composited beneath the figure.
            Color32[] final = CompositeRimGlow(outPx, src, w, h, mid, scale);

            readable.SetPixels32(final);
            readable.Apply(updateMipmaps: true);
            return readable;
        }

        private static Color32[] CompositeRimGlow(Color32[] figure, Color32[] src, int w, int h, Color glowColor, float scale)
        {
            int radius = Mathf.Max(1, Mathf.RoundToInt(RimGlowBlurPx * scale));
            float[] mask = new float[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                mask[i] = src[i].a / 255f;
            }
            float[] blurred = BoxBlur(mask, w, h, radius);
            blurred = BoxBlur(blurred, w, h, radius);

            Color32[] result = new Color32[figure.Length];
            for (int i = 0; i < figure.Length; i++)
            {
                float glowA = blurred[i] * RimGlowAlpha;
                if (glowA <= 0.004f && figure[i].a == 0)
                {
                    continue;
                }
                // glow under figure: out = fig over glow
                float fa = figure[i].a / 255f;
                float outA = fa + glowA * (1f - fa);
                if (outA <= 0f)
                {
                    continue;
                }
                float r = (figure[i].r / 255f * fa + glowColor.r * glowA * (1f - fa)) / outA;
                float g = (figure[i].g / 255f * fa + glowColor.g * glowA * (1f - fa)) / outA;
                float b = (figure[i].b / 255f * fa + glowColor.b * glowA * (1f - fa)) / outA;
                result[i] = new Color32(
                    (byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f),
                    (byte)(Mathf.Clamp01(b) * 255f), (byte)(Mathf.Clamp01(outA) * 255f));
            }
            return result;
        }

        private static float[] BoxBlur(float[] input, int w, int h, int radius)
        {
            float[] tmp = new float[input.Length];
            float[] output = new float[input.Length];
            float norm = 1f / (2 * radius + 1);
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                float acc = 0f;
                for (int x = -radius; x <= radius; x++)
                {
                    acc += input[row + Mathf.Clamp(x, 0, w - 1)];
                }
                for (int x = 0; x < w; x++)
                {
                    tmp[row + x] = acc * norm;
                    int add = Mathf.Min(x + radius + 1, w - 1);
                    int sub = Mathf.Max(x - radius, 0);
                    acc += input[row + add] - input[row + sub];
                }
            }
            for (int x = 0; x < w; x++)
            {
                float acc = 0f;
                for (int y = -radius; y <= radius; y++)
                {
                    acc += tmp[Mathf.Clamp(y, 0, h - 1) * w + x];
                }
                for (int y = 0; y < h; y++)
                {
                    output[y * w + x] = acc * norm;
                    int add = Mathf.Min(y + radius + 1, h - 1);
                    int sub = Mathf.Max(y - radius, 0);
                    acc += tmp[add * w + x] - tmp[sub * w + x];
                }
            }
            return output;
        }

        private static float[] EdgeDistance(Color32[] px, int w, int h)
        {
            const float Big = 1e9f;
            const float Diag = 1.41421f;
            float[] dist = new float[px.Length];
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
            return dist;
        }

        private static float Hash01(int n)
        {
            unchecked
            {
                n = (n << 13) ^ n;
                return (((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483648f);
            }
        }

        private static Texture2D BakeEmitterRing()
        {
            const int size = 96;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true);
            Color32[] px = new Color32[size * size];
            Vector2 center = new Vector2(size / 2f - 0.5f, size / 2f - 0.5f);
            float maxR = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = Vector2.Distance(new Vector2(x, y), center) / maxR;
                    // soft disc + two rings
                    float a = Mathf.Clamp01(1f - r) * 0.35f;
                    a += Mathf.Exp(-Mathf.Pow((r - 0.72f) / 0.05f, 2f)) * 0.5f;
                    a += Mathf.Exp(-Mathf.Pow((r - 0.45f) / 0.04f, 2f)) * 0.35f;
                    Color c = Color.Lerp(new Color(0.1f, 0.55f, 1f), new Color(0.7f, 0.95f, 1f),
                        Mathf.Clamp01(1.2f - r));
                    px[y * size + x] = new Color32(
                        (byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f),
                        (byte)(Mathf.Clamp01(a) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(updateMipmaps: true);
            tex.name = "HoloAI_EmitterRing";
            return tex;
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

        // Used by SilhouetteUtility to draw a recolored silhouette for roofed/hidden
        // pawns. The baked hologram materials aren't meaningfully recolorable, and the
        // base implementation would otherwise log an error and hand back BadGraphic
        // (a magenta placeholder) — the pre-baked look already reads fine as-is.
        public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
        {
            return this;
        }
    }
}
