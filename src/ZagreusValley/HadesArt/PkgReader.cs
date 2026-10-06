using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using K4os.Compression.LZ4;

namespace ZagreusValley.HadesArt;

/// <summary>Reads Supergiant .pkg packages (Hades: version 7, LZ4 chunks). C# port of the reading half of
/// deppth by quaerus (MIT, github.com/quaerus/deppth): sggpio.py, compression.py, entries.py.</summary>
public sealed class PkgReader
{
    const int ChunkSize = 0x2000000;
    const byte EndOfChunk = 0xBE, EndOfFile = 0xFF;
    const byte Texture = 0xAD, Texture3D = 0xAA, Bink = 0xBB, Atlas = 0xDE, BinkAtlas = 0xEE, Include = 0xCC, Spine = 0xF0;

    public sealed record SubAtlas(string Name, int X, int Y, int W, int H, int TopLeftX, int TopLeftY, int OrigW, int OrigH);
    public sealed record AtlasEntry(string TextureName, List<SubAtlas> Subs);

    /// <summary>XNB texture payloads by name, as stored (decode with <see cref="XnbTexture"/>).</summary>
    public readonly Dictionary<string, byte[]> Textures = new(StringComparer.OrdinalIgnoreCase);
    public readonly List<AtlasEntry> Atlases = new();

    Stream raw = Stream.Null;
    byte[] buf = Array.Empty<byte>();
    int pos;
    bool compressed, isManifest;

    /// <summary>Reads <paramref name="pkgPath"/> (textures) and, when present, its "_manifest" sibling (atlases).</summary>
    public static PkgReader Read(string pkgPath)
    {
        var r = new PkgReader();
        r.ReadFile(pkgPath, manifest: false);
        if (File.Exists(pkgPath + "_manifest")) r.ReadFile(pkgPath + "_manifest", manifest: true);
        return r;
    }

    void ReadFile(string path, bool manifest)
    {
        using var fs = File.OpenRead(path);
        raw = fs; buf = Array.Empty<byte>(); pos = 0; isManifest = manifest;
        ReadAll();
    }

    void ReadAll()
    {
        int compressor = raw.ReadByte();       // 0x20 = lz4 in Hades
        raw.ReadByte(); raw.ReadByte();
        int version = raw.ReadByte();
        if (compressor is not (0x20 or 0x00) || version != 7)
            throw new InvalidDataException($"not a Hades package (compressor {compressor:X2}, version {version})");
        compressed = compressor == 0x20;

        while (true)
        {
            int type = ReadByteOrEof();
            if (type < 0 || type == EndOfFile) break;
            if (type == EndOfChunk) { buf = Array.Empty<byte>(); pos = 0; continue; }
            switch (type)
            {
                case Texture:
                case Texture3D:
                {
                    string name = ReadString(); int size = ReadInt();
                    byte[] data = Take(size);
                    if (type == Texture) Textures[name] = data;
                    break;
                }
                case Atlas: Atlases.Add(ReadAtlas()); break;
                case Bink:
                {
                    int first = Take(1)[0];
                    if (first == 0xFF) { int n = ReadInt(); Take(1); if (n > 0) Take(4); }
                    ReadString(); break;
                }
                case BinkAtlas:
                {
                    ReadInt(); int ver = ReadInt(); ReadString(); ReadInt(); ReadInt();
                    if (ver > 1) { ReadInt(); ReadInt(); if (ver > 2) Take(4); }
                    break;
                }
                case Include: ReadString(); break;
                case Spine: Take(1); ReadString(); Take(ReadIntSigned()); Take(ReadIntSigned()); break;
                default: throw new InvalidDataException($"unknown entry type 0x{type:X2}");
            }
        }
    }

    AtlasEntry ReadAtlas()
    {
        ReadInt();                                   // size, ignored
        int version = 0, count = ReadInt();
        if (count == 2142336875) { version = ReadInt(); count = ReadInt(); }
        var subs = new List<SubAtlas>(count);
        for (int i = 0; i < count; i++)
        {
            string name = ReadString();
            int x = ReadInt(), y = ReadInt(), w = ReadInt(), h = ReadInt();
            int tlx = ReadInt(), tly = ReadInt(), ow = ReadInt(), oh = ReadInt();
            Take(8);                                 // scale ratio (2 floats)
            if (version > 0) Take(1);                // flags
            if (version > 2) { int hull = ReadInt(); Take(hull * 8); }
            subs.Add(new SubAtlas(name, x, y, w, h, tlx, tly, ow, oh));
        }
        string tex;
        if (Take(1)[0] == 221 || isManifest) tex = ReadString();   // reference to a texture stored elsewhere
        else
        {
            tex = ReadString(); int size = ReadInt(); Textures[tex] = Take(size);
        }
        return new AtlasEntry(tex, subs);
    }

    // ---- chunked stream ----
    int ReadByteOrEof()
    {
        if (pos >= buf.Length && !NextChunk()) return -1;
        return buf[pos++];
    }

    byte[] Take(int n)
    {
        var o = new byte[n]; int got = 0;
        while (got < n)
        {
            if (pos >= buf.Length && !NextChunk()) throw new EndOfStreamException();
            int k = Math.Min(n - got, buf.Length - pos);
            Buffer.BlockCopy(buf, pos, o, got, k); pos += k; got += k;
        }
        return o;
    }

    bool NextChunk()
    {
        if (raw.Position >= raw.Length) return false;
        int target = raw.Position <= 4 ? ChunkSize - (int)raw.Position : ChunkSize;
        if (!compressed)
        {
            buf = new byte[Math.Min(target, raw.Length - raw.Position)];
            ReadRaw(buf); pos = 0; return true;
        }
        raw.ReadByte();                              // compressed flag; deppth always treats chunks as compressed
        var lenBytes = new byte[4]; ReadRaw(lenBytes);
        int compLen = (lenBytes[0] << 24) | (lenBytes[1] << 16) | (lenBytes[2] << 8) | lenBytes[3];
        var comp = new byte[compLen]; ReadRaw(comp);
        buf = new byte[target];
        LZ4Codec.Decode(comp, 0, compLen, buf, 0, target);
        pos = 0;
        return true;
    }

    void ReadRaw(byte[] dst)   // Stream.ReadExactly is .NET 7+; Stardew runs on .NET 6
    {
        int got = 0;
        while (got < dst.Length) { int n = raw.Read(dst, got, dst.Length - got); if (n <= 0) throw new EndOfStreamException(); got += n; }
    }

    int ReadInt() { var b = Take(4); return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]; }
    int ReadIntSigned() => ReadInt();
    string ReadString() { int n = Take(1)[0]; return Encoding.ASCII.GetString(Take(n)); }
}
