using System;
using System.Collections.Generic;
using ZagreusValley.Generated;

namespace ZagreusValley.HadesArt;

/// <summary>Pixel helpers: crop, area-average downscale (keeps alpha edges clean) and packing frames into a layouts.json grid.</summary>
public static class Fit
{
    /// <summary>Frame size used for 'vanilla' layouts before AssetReplacer resamples to the real vanilla sheet.</summary>
    const int VanillaStaging = 32;

    public static (int, int, byte[]) Crop((int W, int H, byte[] Rgba) src, int x, int y, int w, int h)
    {
        var o = new byte[w * h * 4];
        for (int row = 0; row < h; row++)
            Buffer.BlockCopy(src.Rgba, ((y + row) * src.W + x) * 4, o, row * w * 4, w * 4);
        return (w, h, o);
    }

    /// <summary>Area-average resample with premultiplied alpha, letterboxed to keep aspect ratio, anchored bottom-centre (feet on the ground).</summary>
    public static byte[] Resample(byte[] src, int sw, int sh, int dw, int dh)
    {
        double scale = Math.Min((double)dw / sw, (double)dh / sh);
        int tw = Math.Max(1, (int)Math.Round(sw * scale)), th = Math.Max(1, (int)Math.Round(sh * scale));
        int ox = (dw - tw) / 2, oy = dh - th;
        var o = new byte[dw * dh * 4];
        for (int ty = 0; ty < th; ty++)
        for (int tx = 0; tx < tw; tx++)
        {
            int x0 = tx * sw / tw, x1 = Math.Max(x0 + 1, (tx + 1) * sw / tw);
            int y0 = ty * sh / th, y1 = Math.Max(y0 + 1, (ty + 1) * sh / th);
            double r = 0, g = 0, b = 0, a = 0; int n = 0;
            for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                int i = (y * sw + x) * 4; double al = src[i + 3] / 255.0;
                r += src[i] * al; g += src[i + 1] * al; b += src[i + 2] * al; a += al; n++;
            }
            int d = ((oy + ty) * dw + ox + tx) * 4;
            if (a <= 0) continue;
            o[d] = (byte)(r / a); o[d + 1] = (byte)(g / a); o[d + 2] = (byte)(b / a);
            o[d + 3] = (byte)(a / n >= 0.5 ? 255 : 0);   // hard edges, like Stardew's pixel art
        }
        return o;
    }

    public static (int, int, byte[]) ToGrid(List<(int W, int H, byte[] Rgba)> frames, LayoutRow layout)
    {
        int fw = int.TryParse(layout.FrameW, out var a) ? a : VanillaStaging;
        int fh = int.TryParse(layout.FrameH, out var b) ? b : VanillaStaging;
        int cols = int.TryParse(layout.Cols, out var c) ? c : 4;
        int rows = int.TryParse(layout.Rows, out var d) ? d : (frames.Count + cols - 1) / cols;
        int W = fw * cols, H = fh * rows;
        var o = new byte[W * H * 4];
        for (int i = 0; i < cols * rows; i++)
        {
            var f = frames[i % frames.Count];              // too few frames: repeat them
            var px = Resample(f.Rgba, f.W, f.H, fw, fh);
            int gx = (i % cols) * fw, gy = (i / cols) * fh;
            for (int y = 0; y < fh; y++) Buffer.BlockCopy(px, y * fw * 4, o, ((gy + y) * W + gx) * 4, fw * 4);
        }
        return (W, H, o);
    }
}
