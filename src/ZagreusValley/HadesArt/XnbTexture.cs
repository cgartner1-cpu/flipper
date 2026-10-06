using System;
using System.IO;
using BCnEncoder.Decoder;
using BCnEncoder.Shared;

namespace ZagreusValley.HadesArt;

/// <summary>Decodes a Hades texture entry (an uncompressed XNB holding BGRA, BC3 or BC7 data) to RGBA bytes.
/// Layout follows deppth's TextureEntry._get_image.</summary>
public static class XnbTexture
{
    public static (int W, int H, byte[] Rgba) Decode(byte[] xnb)
    {
        using var r = new BinaryReader(new MemoryStream(xnb));
        if (new string(r.ReadChars(4)) != "XNBw") throw new InvalidDataException("not an XNB");
        int ver = r.ReadByte();
        if (ver is not (5 or 6)) throw new InvalidDataException($"XNB version {ver}");
        if (r.ReadByte() != 0) throw new InvalidDataException("compressed XNB");
        r.ReadInt32();                                   // file length
        if (ver == 5)
        {
            int readers = Read7Bit(r);
            for (int i = 0; i < readers; i++) { r.ReadBytes(r.ReadByte()); r.ReadInt32(); }
            Read7Bit(r); Read7Bit(r);
        }
        int fmt = r.ReadInt32(), w = r.ReadInt32(), h = r.ReadInt32();
        r.ReadInt32();                                   // mip count
        byte[] data = r.ReadBytes(r.ReadInt32());
        byte[] rgba;
        switch (fmt)
        {
            case 0:                                      // BGRA
                rgba = data;
                for (int i = 0; i < rgba.Length; i += 4) (rgba[i], rgba[i + 2]) = (rgba[i + 2], rgba[i]);
                break;
            case 6: rgba = Bc(data, w, h, CompressionFormat.Bc3); break;
            case 28: rgba = Bc(data, w, h, CompressionFormat.Bc7); break;
            default: throw new InvalidDataException($"unsupported texture format {fmt}");
        }
        return (w, h, rgba);
    }

    static byte[] Bc(byte[] data, int w, int h, CompressionFormat f)
    {
        var px = new BcDecoder().DecodeRaw(data, w, h, f);
        var o = new byte[w * h * 4];
        for (int i = 0; i < px.Length; i++) { o[i * 4] = px[i].r; o[i * 4 + 1] = px[i].g; o[i * 4 + 2] = px[i].b; o[i * 4 + 3] = px[i].a; }
        return o;
    }

    static int Read7Bit(BinaryReader r)
    {
        int v = 0, shift = 0; byte b;
        do { b = r.ReadByte(); v |= (b & 0x7F) << shift; shift += 7; } while ((b & 0x80) != 0);
        return v;
    }
}
