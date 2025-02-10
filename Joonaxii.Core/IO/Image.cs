using Joonaxii.Collections;
using Joonaxii.Hashing;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CompressionLevel = System.IO.Compression.CompressionLevel;

// These Image De/Encoders have been taken from my own
// J-Core C++ code and converted to C#, not exactly 1 to 1 
// with my C++ code primarily due to .NET Framework being used
// and because C#'s own DeflateStream works differently from
// regular ol' zLib in C++.
//
// The primary reason for rolling my own De/Encoders is that
// reading iniformation about a texture and in general
// just reading pixel data is faster and cleaner this way than
// doing it via Unity's Texture2D APIs which I will need for
// custom sprites for the Localization stuff

namespace Joonaxii.IO.Image
{
    public static class Common
    {
        internal static RefStack<byte> DataBuffer => _dataBuffer;
        internal static RefStack<byte> BigScanBuffer => _bigScanBuffer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CalcualtePadding(int width, int bpp)
        {
            int rem = (width * bpp) & 0x3;
            return rem != 0 ? 4 - rem : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FlipRB(Span<byte> data, ImageFormat format)
        {
            switch (format)
            {
                case ImageFormat.RGBA32:
                case ImageFormat.RGB24:
                    {
                        int bpp = ImageData.GetBitsPerPixel(format) >> 3;
                        for (int i = 0, j = 2; i < data.Length; i += bpp, j += bpp) 
                        {
                            (data[j], data[i]) = (data[i], data[j]);
                        }
                    }
                    break;
            }
        }

        private static RefStack<byte> _dataBuffer = new RefStack<byte>();
        private static RefStack<byte> _bigScanBuffer = new RefStack<byte>();
    }

    public static class Png
    {
        private const ulong PNG_SIG = 0xA1A0A0D474E5089U;
        private unsafe delegate void ReverseFilter(byte* current, byte* prior, int width, byte filter);

        public static bool Decode(string path, ref ImageData img)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Decode(fs, ref img);
            }
        }
        public static bool Decode(Stream stream, ref ImageData img)
        {
            long pos = stream.Position;
            long len = stream.Length;
            ulong sig = 0;
            if (!stream.TryRead(ref sig, ref pos) || sig != PNG_SIG)
            {
                return false;
            }
            _idatBuffer.Clear();
            Common.DataBuffer.Clear();

            int compSize = 0;

            Chunk chunk = default;
            Chunk palette = default;
            Chunk alpha = default;
            IHDR ihdr = default;

            ImageFormat format = ImageFormat.Unknown;
            int width = 0, height = 0;
            int bpp = 0;
            while (chunk.Read(stream, ref pos, len))
            {
                switch (chunk.type)
                {
                    case PngChunkType.IHDR:
                        ihdr.Read(stream, ref pos);

                        if (ihdr.interlaced != 0)
                        {
                            Debugger.LogWarning("[PNG Decode] Interlaced PNGs are not supported! (Currently)");
                            return false;
                        }

                        switch (ihdr.bitDepth)
                        {
                            default:
                                Debugger.LogWarning($"[PNG Decode] Unsupported bit depth {ihdr.bitDepth}!");
                                return false;
                            case 8:
                            case 16:
                                break;
                        }

                        switch (ihdr.colorType)
                        {
                            default:
                                Debugger.LogWarning($"[PNG Decode] Unsupported color type {ihdr.colorType}!");
                                return false;
                            case 0:
                                switch (ihdr.bitDepth)
                                {
                                    default:
                                        Debugger.LogWarning($"[PNG Decode] Unsupported bit depth {ihdr.bitDepth} for color type {ihdr.colorType}!");
                                        return false;
                                    case 8:
                                        format = ImageFormat.Gray8;
                                        bpp = 1;
                                        break;
                                    case 16:
                                        format = ImageFormat.Gray16;
                                        bpp = 2;
                                        break;
                                }
                                break;
                            case 2:
                                switch (ihdr.bitDepth)
                                {
                                    default:
                                        Debugger.LogWarning($"[PNG Decode] Unsupported bit depth {ihdr.bitDepth} for color type {ihdr.colorType}!");
                                        return false;
                                    case 8:
                                        format = ImageFormat.RGB24;
                                        bpp = 3;
                                        break;
                                }
                                break;
                            case 6:
                                switch (ihdr.bitDepth)
                                {
                                    default:
                                        Debugger.LogWarning($"[PNG Decode] Unsupported bit depth {ihdr.bitDepth} for color type {ihdr.colorType}!");
                                        return false;
                                    case 8:
                                        format = ImageFormat.RGBA32;
                                        bpp = 4;
                                        break;
                                }
                                break;
                            case 3:
                                format = ImageFormat.Indexed;
                                bpp = 1;
                                break;
                        }

                        width = FastMath.Abs(ihdr.width);
                        height = FastMath.Abs(ihdr.height);
                        break;

                    case PngChunkType.IDAT:
                        _idatBuffer.Push(in chunk);
                        compSize += chunk.length;
                        break;

                    case PngChunkType.PLTE:
                        palette = chunk;
                        break;

                    case PngChunkType.tRNS:
                        alpha = chunk;
                        break;
                }

                pos = chunk.position + len + 4;
                stream.Seek(pos, SeekOrigin.Begin);
            }

            int scanSize = width * bpp;
            int rawScan = scanSize + 1;
            int rawSize = rawScan * height;

            Common.DataBuffer.Resize(compSize + rawSize, false);
            img.Allocate(format, width, height);

            Span<byte> comp = Common.DataBuffer.AsSpan(0, compSize);
            Span<byte> decomp = Common.DataBuffer.AsSpan(compSize);

            int dPos = 0;
            for (int i = 0; i < _idatBuffer.Count; i++)
            {
                ref var ch = ref _idatBuffer[i];
                stream.Seek(ch.position, SeekOrigin.Begin);
                stream.BufferedRead(comp.Slice(dPos, ch.length));
                dPos += ch.length;
            }
            Compression.Inflate(comp, decomp);

            int scanBufSize = (rawScan + bpp) * 2;
            Span<byte> scan = scanBufSize > 1024 ? Common.BigScanBuffer.Resize(scanBufSize, false) : stackalloc byte[scanBufSize];
            scan.ZeroMem();

            if (format == ImageFormat.Indexed)
            {
                var palDat = img.DataRaw;
                if (palette.type == PngChunkType.PLTE)
                {
                    stream.Seek(palette.position, SeekOrigin.Begin);
                    Span<byte> pBuffer = stackalloc byte[256 * 3];
                    int count = palette.length / 3;
                    stream.BufferedRead(pBuffer.Slice(0, count));

                    for (int i = 0, j = 0, k = 0; i < count; i++, j += 3, k += 4)
                    {
                        palDat[k] = pBuffer[j];
                        palDat[k + 1] = pBuffer[j + 1];
                        palDat[k + 2] = pBuffer[j + 2];
                        palDat[k + 3] = 0xFF;
                    }
                }

                if (alpha.type == PngChunkType.tRNS)
                {
                    stream.Seek(alpha.position, SeekOrigin.Begin);
                    Span<byte> pBuffer = stackalloc byte[256];
                    stream.BufferedRead(pBuffer.Slice(0, alpha.length));

                    for (int i = 0, k = 3; i < alpha.length; i++, k += 4)
                    {
                        palDat[k] = pBuffer[i];
                    }
                }
            }

            unsafe
            {
                Span<byte> prior = scan.Slice(bpp, rawScan);
                Span<byte> current = scan.Slice(rawScan + bpp, rawScan);
                Span<byte> currentPix = current.Slice(1);

                fixed (byte* priPtr = prior)
                fixed (byte* curPtr = current)
                {
                    byte* priPix = priPtr + 1;
                    byte* curPix = curPtr + 1;

                    ReverseFilter filter = ReverseFilter_8;
                    switch (bpp)
                    {
                        case 2:
                            filter = ReverseFilter_16;
                            break;
                        case 3:
                            filter = ReverseFilter_24;
                            break;
                        case 4:
                            filter = ReverseFilter_32;
                            break;
                    }

                    Span<byte> pixelsOut = img.Data;
                    for (int y = 0, xP = 0, dst = 0; y < height; y++, xP += rawScan, dst += scanSize)
                    {
                        decomp.Slice(xP, rawScan).CopyTo(current);

                        byte mode = current[0];
                        current[0] = 0;

                        filter.Invoke(curPtr, priPtr, width, mode);
                        currentPix.CopyTo(pixelsOut.Slice(dst, scanSize));

                        current.CopyTo(prior);
                    }
                }
            }
            return true;
        }

        public static bool Encode(string path, in ImageData img, CompressionLevel compression = CompressionLevel.Optimal)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
            {
                return Encode(fs, in img, compression);
            }
        }
        public static bool Encode(Stream stream, in ImageData img, CompressionLevel compression = CompressionLevel.Optimal)
        {
            long pos = stream.Position;

            Span<byte> buffer = stackalloc byte[32];
            Chunk chunk = default;

            int bpp = ImageData.GetBitsPerPixel(img.Format) >> 3;
            stream.WriteValue(PNG_SIG, ref pos, false);

            buffer.Write(0, img.Width, true);
            buffer.Write(4, img.Height, true);

            switch (img.Format)
            {
                default:
                    buffer.Write<byte>(8, 8, false);
                    buffer.Write<byte>(9, 0, false);
                    break;
                case ImageFormat.RGB24:
                    buffer.Write<byte>(8, 8, false);
                    buffer.Write<byte>(9, 2, false);
                    break;
                case ImageFormat.RGBA32:
                    buffer.Write<byte>(8, 8, false);
                    buffer.Write<byte>(9, 6, false);
                    break;
                case ImageFormat.Indexed:
                    buffer.Write<byte>(8, 8, false);
                    buffer.Write<byte>(9, 3, false);
                    break;
                case ImageFormat.Gray16:
                    buffer.Write<byte>(8, 8, false);
                    buffer.Write<byte>(9, 4, false);
                    break;
            }
            buffer.Slice(10, 3).ZeroMem();

            chunk.type = PngChunkType.IHDR;
            chunk.length = 13;
            WriteChunk(stream, ref pos, in chunk, buffer);

            if (img.Format == ImageFormat.Indexed)
            {
                var raw = img.DataRaw;
                Span<byte> palette = stackalloc byte[768];

                for (int i = 0, j = 0; i < 768; i += 3, j += 4)
                {
                    palette[i] = raw[i];
                    palette[i + 1] = raw[i + 1];
                    palette[i + 2] = raw[i + 2];
                }

                chunk.type = PngChunkType.PLTE;
                chunk.length = 768;
                WriteChunk(stream, ref pos, in chunk, palette);

                for (int i = 0, j = 3; i < 256; i++, j += 4)
                {
                    palette[i] = raw[i];
                }

                chunk.type = PngChunkType.tRNS;
                chunk.length = 256;
                WriteChunk(stream, ref pos, in chunk, palette);
            }

            int scanSR = img.Width * bpp;
            int scanSP = scanSR + 1;
            int scanBufSize = scanSP * 6;
            Span<byte> scan = scanBufSize > 1024 ? Common.BigScanBuffer.Resize(scanBufSize, false) : stackalloc byte[scanBufSize];

            long dataPos = stream.Position;
            pos = dataPos + 8;
            WriteIDAT(stream);

            var defStream = Compression.BeginDeflate(stream, compression);
            unsafe
            {
                fixed (byte* sPtr = scan)
                {
                    byte* prior = sPtr;
                    byte* current = sPtr + scanSP;

                    byte* priorPix = prior + 1;
                    byte* currentPix = current + 1;

                    Span<byte> curSpan = scan.Slice(scanSP, scanSP);
                    Span<byte> curPix = scan.Slice(scanSP + 1, scanSR);

                    var filters = stackalloc byte*[4]
                    {
                        sPtr + scanSP * 2,
                        sPtr + scanSP * 2 + scanSR,
                        sPtr + scanSP * 2 + scanSR * 2,
                        sPtr + scanSP * 2 + scanSR * 3,
                    };

                    long score = 0;
                    byte filter = 0;
                    current[0] = 0;

                    bool isInexed = img.Format == ImageFormat.Indexed;
                    ReadOnlySpan<byte> data = img.Data;
                    ReadOnlySpan<byte> curScan;
                    for (int y = 0, yP = 0; y < img.Height; y++, yP += scanSR)
                    {
                        curScan = data.Slice(yP, scanSR);
                        curScan.CopyTo(curPix);
                        if (!isInexed)
                        {
                            score = 0;
                            filter = 0;
                            CalculateDiff(currentPix, img.Width, bpp, ref score);

                            for (int i = 0; i < 4; i++)
                            {
                                ApplyFilter(currentPix, priorPix, filters[i], img.Width, bpp, i + 1);
                                if (CalculateDiff(filters[i], img.Width, bpp, ref score))
                                {
                                    filter = (byte)(i + 1);
                                }
                            }

                            if (filter > 0)
                            {
                                UnsafeUtil.CopyTo(filters[filter - 1], currentPix, scanSR);
                            }
                            curScan.CopyTo(priorPix);
                            current[0] = filter;
                        }
                        defStream.Deflate(curSpan);
                    }
                }
            }

            defStream.Finish();
            long posS = stream.Position;
            long dataSize = (posS - dataPos);

            stream.Seek(dataPos, SeekOrigin.Begin);
            WriteIDAT(stream, dataPos, (int)dataSize - 8, posS);



            chunk.type = PngChunkType.IEND;
            chunk.length = 0;
            WriteChunk(stream, ref pos, in chunk, default);

            return true;
        }

        private enum PngChunkType : uint
        {
            IHDR = 0x52444849U,
            PLTE = 0x45544C50U,
            tRNS = 0x534E5274U,
            IDAT = 0x54414449U,
            IEND = 0x444E4549U,
        };

        private static RefStack<Chunk> _idatBuffer = new RefStack<Chunk>();
        private struct Chunk
        {
            public int length;
            public PngChunkType type;
            public long position;
            public uint crc;

            public bool Read(Stream stream, ref long position, long length)
            {
                if (length - position < 12)
                {
                    return false;
                }

                stream.TryRead(ref length, ref position, true);
                stream.TryRead(ref type, ref position, false);
                this.position = position;

                if (length - position < length)
                {
                    return false;
                }

                stream.Seek(length, SeekOrigin.Current);
                position += length;

                stream.TryRead(ref crc, ref position, false);
                stream.Seek(this.position, SeekOrigin.Begin);
                position = this.position;

                return true;
            }
        }

        private struct IHDR
        {
            public int width;
            public int height;
            public byte bitDepth;
            public byte colorType;
            public byte compression;
            public byte filter;
            public byte interlaced;

            public void Read(Stream stream, ref long position)
            {
                stream.TryRead(ref width, ref position, true);
                stream.TryRead(ref height, ref position, true);
                stream.TryRead(ref bitDepth, ref position, false);
                stream.TryRead(ref colorType, ref position, false);
                stream.TryRead(ref compression, ref position, false);
                stream.TryRead(ref filter, ref position, false);
                stream.TryRead(ref interlaced, ref position, false);
            }
        };

        private static unsafe void ApplyFilter(byte* current, byte* prior, byte* target, int width, int bpp, int filter)
        {
            width *= bpp;
            switch (filter)
            {
                case 1: //Sub
                    UnsafeUtil.CopyTo(current, target, bpp);
                    for (int x = bpp, xS = 0; x < width; x++, xS++)
                    {
                        target[x] = (byte)(current[x] - current[xS]);
                    }
                    break;
                case 2: //Up
                    for (int x = 0; x < width; x++)
                    {
                        target[x] = (byte)(current[x] - prior[x]);
                    }
                    break;
                case 3: //Average
                    for (int x = 0, xS = -bpp; x < width; x++, xS++)
                    {
                        target[x] = (byte)(current[x] - ((prior[x] + (xS < 0 ? 0 : current[xS])) >> 1));
                    }
                    break;
                case 4: //Paeth
                    for (int x = 0, xS = -bpp; x < width; x++, xS++)
                    {
                        int a = (xS < 0 ? 0 : current[xS]);
                        int b = prior[x];
                        int c = (xS < 0 ? 0 : prior[xS]);
                        target[x] = (byte)(current[x] - PaethPredictor(a, b, c));
                    }
                    break;
            }
        }

        private static unsafe bool CalculateDiff(byte* data, int width, int bpp, ref long current)
        {
            long curVal = 0;

            sbyte* sData = (sbyte*)data;
            for (int i = 0, j = 0; i < width; i++, j += bpp)
            {
                switch (bpp)
                {
                    default:
                        curVal += sData[j];
                        break;
                    case 2:
                        curVal += sData[j];
                        curVal += sData[j + 1];
                        break;
                    case 3:
                        curVal += sData[j];
                        curVal += sData[j + 1];
                        curVal += sData[j + 2];
                        break;
                    case 4:
                        curVal += sData[j];
                        curVal += sData[j + 1];
                        curVal += sData[j + 2];
                        curVal += sData[j + 3];
                        break;
                }

            }
            if(curVal >= current) { return false; }
            current = curVal;
            return true;
        }

        private static void WriteIDAT(Stream stream)
        {
            long pos = 0;
            stream.WriteValue<int>(0, ref pos, false);
            stream.WriteValue(PngChunkType.IDAT, ref pos);
        }
        private static void WriteIDAT(Stream stream, long position, int dataLen, long endPos)
        {
            long startPos = position;
            stream.WriteValue<int>(dataLen, ref position, true);
            stream.WriteValue(PngChunkType.IDAT, ref position);

            CRC32.State state = default;
            uint crc = state.Init().Update(PngChunkType.IDAT).Update(stream, dataLen).Extract();
            stream.WriteValue(crc, ref position, true);
        }

        private static void WriteChunk(Stream stream, ref long pos, in Chunk chunk, ReadOnlySpan<byte> data)
        {
            data = data.Slice(0, chunk.length);

            stream.WriteValue(chunk.length, ref pos, true);
            stream.WriteValue(chunk.type, ref pos, false);
            stream.BufferedWrite(data);
            pos += data.Length;

            CRC32.State state = default;
            uint crc = state.Init().Update(chunk.type).Update(data).Extract();
            stream.WriteValue(crc, ref pos, true);
        }

        private static int PaethPredictor(int a, int b, int c)
        {
            int p = a + b - c;
            int pA = FastMath.Abs(p - a);
            int pB = FastMath.Abs(p - b);
            int pC = FastMath.Abs(p - c);

            return ((pA <= pB).ToInt() | ((pA <= pC).ToInt() << 1) | ((pB <= pC).ToInt() << 2)) switch
            {
                1 => c,
                2 => c,
                3 => a,
                4 => b,
                5 => b,
                6 => b,
                7 => a,
                _ => c,
            };

            // ---Old Implementation/Standard---
            // int32_t p = a + b - c;
            // int32_t pA = FastMath.Abs(p - a);
            // int32_t pB = FastMath.Abs(p - b);
            // int32_t pC = FastMath.Abs(p - c);

            // if (pA <= pB && pA <= pC) { return a; }
            // return pB <= pC ? b : c;
        }

        private unsafe static void ReverseFilter_8(byte* current, byte* prior, int width, byte filter)
        {
            byte* tempA = current - 1;
            byte* tempB = prior - 1;
            switch (filter)
            {
                case 1: //Sub
                    for (int x = 0; x < width; x++)
                    {
                        *current = (byte)(*current + *tempA);

                        ++current;
                        ++tempA;
                    }
                    break;
                case 2: //Up
                    for (int x = 0; x < width; x++)
                    {
                        *current = (byte)(*current + *prior);
                        ++current;
                        ++prior;
                    }
                    break;
                case 3: //Average
                    for (int x = 0; x < width; x++)
                    {
                        *current = (byte)(*current + ((*prior + *tempA) >> 1));

                        ++current;
                        ++tempA;
                        ++prior;
                    }
                    break;
                case 4: //Paeth
                    for (int x = 0; x < width; x++)
                    {
                        *current = (byte)(*current + PaethPredictor(*tempA, *prior, *tempB));

                        ++current;
                        ++tempA;
                        ++tempB;
                        ++prior;
                    }
                    break;
            }
        }
        private unsafe static void ReverseFilter_16(byte* current, byte* prior, int width, byte filter)
        {
            byte* tempA = current - 2;
            byte* tempB = prior - 2;
            switch (filter)
            {
                case 1: //Sub
                    for (int x = 0; x < width; x++)
                    {
                        current[0] = (byte)(current[0] + tempA[0]);
                        current[1] = (byte)(current[1] + tempA[1]);

                        current += 2;
                        tempA += 2;
                    }
                    break;
                case 2: //Up
                    for (int x = 0; x < width; x++)
                    {
                        current[0] = (byte)(current[0] + prior[0]);
                        current[1] = (byte)(current[1] + prior[1]);

                        current += 2;
                        prior += 2;
                    }
                    break;
                case 3: //Average
                    for (int x = 0; x < width; x++)
                    {
                        current[0] = (byte)(current[0] + ((prior[0] + tempA[0]) >> 1));
                        current[1] = (byte)(current[1] + ((prior[1] + tempA[1]) >> 1));

                        current += 2;
                        prior += 2;
                        tempA += 2;
                    }
                    break;
                case 4: //Paeth
                    for (int x = 0; x < width; x++)
                    {
                        current[0] = (byte)(current[0] + PaethPredictor(tempA[0], prior[0], tempB[0]));
                        current[1] = (byte)(current[1] + PaethPredictor(tempA[1], prior[1], tempB[1]));

                        current += 2;
                        prior += 2;
                        tempA += 2;
                        tempB += 2;
                    }
                    break;
            }
        }

        private unsafe static void ReverseFilter_24(byte* current, byte* prior, int width, byte filter)
        {
            byte* tempA = current - 3;
            byte* tempB = prior - 3;
            switch (filter)
            {
                case 1: //Sub
                    for (int x = 0; x < width; x++)
                    {
                        current[0] = (byte)(current[0] + tempA[0]);
                        current[1] = (byte)(current[1] + tempA[1]);
                        current[2] = (byte)(current[2] + tempA[2]);

                        current += 3;
                        tempA += 3;
                    }
                    break;
                case 2: //Up
                    for (int x = 0; x < width; x++)
                    {
                        current[0] = (byte)(current[0] + prior[0]);
                        current[1] = (byte)(current[1] + prior[1]);
                        current[2] = (byte)(current[2] + prior[2]);

                        current += 3;
                        prior += 3;
                    }
                    break;
                case 3: //Average
                    for (int x = 0; x < width; x++)
                    {
                        current[0] = (byte)(current[0] + ((prior[0] + tempA[0]) >> 1));
                        current[1] = (byte)(current[1] + ((prior[1] + tempA[1]) >> 1));
                        current[2] = (byte)(current[2] + ((prior[2] + tempA[2]) >> 1));

                        current += 3;
                        prior += 3;
                        tempA += 3;
                    }
                    break;
                case 4: //Paeth
                    for (int x = 0; x < width; x++)
                    {
                        current[0] = (byte)(current[0] + PaethPredictor(tempA[0], prior[0], tempB[0]));
                        current[1] = (byte)(current[1] + PaethPredictor(tempA[1], prior[1], tempB[1]));
                        current[2] = (byte)(current[2] + PaethPredictor(tempA[2], prior[2], tempB[2]));

                        current += 3;
                        prior += 3;
                        tempA += 3;
                        tempB += 3;
                    }
                    break;
            }
        }
        private unsafe static void ReverseFilter_32(byte* current, byte* prior, int width, byte filter)
        {
            byte* tempA = current - 4;
            byte* tempB = prior - 4;
            switch (filter)
            {
                case 1: //Sub
                    for (int x = 0; x < width; x++)
                    {
                        current[0] = (byte)(current[0] + tempA[0]);
                        current[1] = (byte)(current[1] + tempA[1]);
                        current[2] = (byte)(current[2] + tempA[2]);
                        current[3] = (byte)(current[3] + tempA[3]);

                        current += 4;
                        tempA += 4;
                    }
                    break;
                case 2: //Up
                    for (int x = 0; x < width; x++)
                    {
                        current[0] = (byte)(current[0] + prior[0]);
                        current[1] = (byte)(current[1] + prior[1]);
                        current[2] = (byte)(current[2] + prior[2]);
                        current[3] = (byte)(current[3] + prior[3]);

                        current += 4;
                        prior += 4;
                    }
                    break;
                case 3: //Average
                    for (int x = 0; x < width; x++)
                    {
                        current[0] = (byte)(current[0] + ((prior[0] + tempA[0]) >> 1));
                        current[1] = (byte)(current[1] + ((prior[1] + tempA[1]) >> 1));
                        current[2] = (byte)(current[2] + ((prior[2] + tempA[2]) >> 1));
                        current[3] = (byte)(current[3] + ((prior[3] + tempA[3]) >> 1));

                        current += 4;
                        prior += 4;
                        tempA += 4;
                    }
                    break;
                case 4: //Paeth
                    for (int x = 0; x < width; x++)
                    {
                        current[0] = (byte)(current[0] + PaethPredictor(tempA[0], prior[0], tempB[0]));
                        current[1] = (byte)(current[1] + PaethPredictor(tempA[1], prior[1], tempB[1]));
                        current[2] = (byte)(current[2] + PaethPredictor(tempA[2], prior[2], tempB[2]));
                        current[3] = (byte)(current[3] + PaethPredictor(tempA[3], prior[3], tempB[3]));

                        current += 4;
                        prior += 4;
                        tempA += 4;
                        tempB += 4;
                    }
                    break;
            }
        }


    }

    public static class Bmp
    {
        private const ushort SIGNATURE = 0x4D42;

        public static bool Encode(string path, in ImageData img, int dpi = 96)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
            {
                return Encode(fs, in img, dpi);
            }
        }
        public static bool Encode(Stream stream, in ImageData img, int dpi = 96)
        {
            const int HEADER_SIZE = 54;
            switch (img.Format)
            {
                default:
                    return false;
                case ImageFormat.RGB24:
                case ImageFormat.RGBA32:
                case ImageFormat.Indexed:
                    break;
            }

            long pos = stream.Position;
            int bpp = ImageData.GetBitsPerPixel(img.Format) >> 3;
            int scanSR = img.Width * bpp;
            int padding = Common.CalcualtePadding(img.Width, bpp);
            int scanSP = scanSR + padding;

            int extra = 0;
            switch (img.Format)
            {
                case ImageFormat.Indexed:
                    extra = 1024;
                    break;
                case ImageFormat.RGBA32:
                    extra = 16;
                    break;
            }
            int dataOffset = HEADER_SIZE + extra;
            int total = dataOffset + (scanSP * img.Height);

            Span<byte> scan = scanSP > 1024 ? Common.BigScanBuffer.Resize(scanSP, false) : stackalloc byte[scanSP];
            scan.ZeroMem();

            stream.WriteValue(SIGNATURE, ref pos);
            stream.WriteValue(total, ref pos);
            stream.WriteZero(ref pos, 4);
            stream.WriteValue(dataOffset, ref pos);


            stream.WriteValue(40, ref pos);
            stream.WriteValue(img.Width, ref pos);
            stream.WriteValue(img.Height, ref pos);
            stream.WriteValue<ushort>(1, ref pos);
            stream.WriteValue((ushort)(bpp << 3), ref pos);

            switch (img.Format)
            {
                default:
                    stream.WriteZero(ref pos, 8);
                    break;
                case ImageFormat.RGBA32:
                    stream.WriteValue(3, ref pos);
                    stream.WriteValue(scanSP * img.Height, ref pos);
                    break;
            }

            int dpiV = (int)Math.Round(dpi * 39.3701);
            stream.WriteValue(dpiV, ref pos);
            stream.WriteValue(dpiV, ref pos);

            if(img.Format == ImageFormat.Indexed)
            {
                stream.WriteValue(256, ref pos);
                stream.WriteZero(ref pos, 4);
            }
            else
            {
                stream.WriteZero(ref pos, 8);
            }

            switch (img.Format)
            {
                case ImageFormat.RGBA32:
                    stream.WriteValue(0x00FF0000U, ref pos);
                    stream.WriteValue(0x0000FF00U, ref pos);
                    stream.WriteValue(0x000000FFU, ref pos);
                    stream.WriteValue(0xFF000000U, ref pos);
                    break;
                case ImageFormat.Indexed:
                    {
                        Span<byte> palette = stackalloc byte[1024];
                        img.DataRaw.Slice(0, 1024).TryCopyTo(palette);
                        Common.FlipRB(palette, ImageFormat.RGBA32);
                        stream.BufferedWrite(palette, 1024);
                    }
                    break;
            }

            int width = img.Width;
            int height = img.Height;
            ReadOnlySpan<byte> pData = img.Data;

            int yP = height * scanSR - scanSR;
            for (int y = 0; y < img.Height; y++, yP -= scanSR)
            {
                pData.Slice(yP, scanSR).CopyTo(scan);
                Common.FlipRB(scan, img.Format);
                stream.BufferedWrite(scan);
            }
            return true;
        }

        private enum BmpVersion
        {
            Version2,
            Version3,
            VersionX,
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BmpFile
        {
            public ushort signature;
            public int length;
            public uint reserved0;
            public int dataOffset;
            public int headerSize;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BmpInfo
        {
            public int width;
            public int height;
            public ushort planes;
            public ushort bpp;

            public int comrpession;
            public int imageSize;

            public int pPMX;
            public int pPMY;

            public int colorsUsed;
            public int numOfColors;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BmpHeader
        {
            public BmpVersion Version => file.headerSize < 40 ? BmpVersion.Version2 : BmpVersion.Version3;

            public BmpFile file;
            public BmpInfo info;
        }
    }

    public enum ImageFormat
    {
        Unknown,
        RGBA32,
        RGB24,
        Gray8,
        Gray16,
        Indexed,
    }

    public struct ImageData
    {
        public delegate void DoCopyPixels(ReadOnlySpan<byte> src, Span<byte> dst);

        public int Width => _width;
        public int Height => _height;
        public ImageFormat Format => _format;

        private int _width;
        private int _height;
        private ImageFormat _format;
        private byte[] _buffer;

        public Span<byte> DataRaw => _buffer == null ? default : _buffer.AsSpan(0, GetRequiredSize(_format, _width, _height, true));
        public Span<byte> Data => _buffer == null ? default : _buffer.AsSpan(GetOffset(), GetRequiredSize(_format, _width, _height, false));

        public static int GetBitsPerPixel(ImageFormat fmt)
        {
            switch (fmt)
            {
                default: return 8;
                case ImageFormat.RGB24: return 24;
                case ImageFormat.RGBA32: return 32;
                case ImageFormat.Gray16: return 16;
            }
        }

        public static int GetRequiredSize(ImageFormat fmt, int width, int height, bool includePalette)
        {
            int reso = width * height * (GetBitsPerPixel(fmt) >> 3);
            includePalette &= fmt == ImageFormat.Indexed;
            return includePalette ? reso + 1024 : reso;
        }

        public void Clear()
        {
            _width = 0;
            _height = 0;
            _format = ImageFormat.Unknown;
            Array.Resize(ref _buffer, 0);
        }

        public void Allocate(ImageFormat format)
            => Allocate(format, _width, _height);
        public void Allocate(int width, int height)
            => Allocate(_format, width, height);
        public void Allocate(ImageFormat format, int width, int height)
        {
            _format = format;
            _width = FastMath.Max(width, 0);
            _height = FastMath.Max(height, 0);

            int rqSize = GetRequiredSize(_format, _width, _height, true);
            if (rqSize > (_buffer?.Length ?? 0))
            {
                Array.Resize(ref _buffer, rqSize);
            }
            _buffer.AsSpan(0, rqSize).ZeroMem();
        }

        public ref ImageData CopyTo(ref ImageData imgDst, bool allowResize = true)
        {
            if (allowResize)
            {
                imgDst.Allocate(_width, _height);
            }
            else if (imgDst._width != _width || imgDst._height != _height)
            {
                return ref imgDst;
            }

            var copyMethod = GetCopyMethod(_format, imgDst._format);
            var src = DataRaw;
            var dst = imgDst.DataRaw;
            copyMethod.Invoke(src, dst);
            return ref imgDst;
        }

        public static DoCopyPixels GetCopyMethod(ImageFormat src, ImageFormat dst)
        {
            return src switch
            {
                ImageFormat.Indexed => dst switch
                {
                    ImageFormat.RGBA32 => IndexedToRGBA,
                    ImageFormat.RGB24 => IndexedToRGB,
                    ImageFormat.Indexed => ExactCopy,
                    ImageFormat.Gray8 => IndexedToGray8,
                    ImageFormat.Gray16 => IndexedToGray16,
                    _ => NoCopy,
                },
                ImageFormat.RGBA32 => dst switch
                {
                    ImageFormat.RGB24 => RGBAToRGB,
                    ImageFormat.RGBA32 => ExactCopy,
                    ImageFormat.Gray8 => RGBAToGray8,
                    ImageFormat.Gray16 => RGBAToGray16,
                    _ => NoCopy,
                },
                ImageFormat.RGB24 => dst switch
                {
                    ImageFormat.RGBA32 => RGBToRGBA,
                    ImageFormat.RGB24 => ExactCopy,
                    ImageFormat.Gray8 => RGBToGray8,
                    ImageFormat.Gray16 => RGBToGray16,
                    _ => NoCopy,
                },
                ImageFormat.Gray8 => dst switch
                {
                    ImageFormat.RGBA32 => Gray8ToRGBA,
                    ImageFormat.RGB24 => Gray8ToRGB,
                    ImageFormat.Gray8 => ExactCopy,
                    ImageFormat.Gray16 => Gray8ToGray16,
                    _ => NoCopy,
                },
                ImageFormat.Gray16 => dst switch
                {
                    ImageFormat.RGBA32 => Gray16ToRGBA,
                    ImageFormat.RGB24 => Gray16ToRGB,
                    ImageFormat.Gray8 => Gray16ToGray8,
                    ImageFormat.Gray16 => ExactCopy,
                    _ => NoCopy,
                },
                _ => NoCopy,
            };
        }

        private static void NoCopy(ReadOnlySpan<byte> src, Span<byte> dst) { }
        private static void ExactCopy(ReadOnlySpan<byte> src, Span<byte> dst) => src.CopyTo(dst);

        private static void RGBAToRGB(ReadOnlySpan<byte> src, Span<byte> dst)
        {
            for (int i = 0, j = 0; i < src.Length; i += 4, j += 3)
            {
                dst[j] = src[i];
                dst[j + 1] = src[i + 1];
                dst[j + 2] = src[i + 2];
            }
        }

        private static void IndexedToGray8(ReadOnlySpan<byte> src, Span<byte> dst)
        {
            ReadOnlySpan<byte> palette = src.Slice(0, 1024);
            src = src.Slice(1024);

            for (int i = 0, j = 0; i < src.Length; i++, j++)
            {
                var tmp = palette.Slice(src[i], 4);
                dst[j] = FastMath.Grayscale(tmp[0], tmp[1], tmp[2]);
            }
        }

        private static void IndexedToGray16(ReadOnlySpan<byte> src, Span<byte> dst)
        {
            ReadOnlySpan<byte> palette = src.Slice(0, 1024);
            src = src.Slice(1024);

            for (int i = 0, j = 0; i < src.Length; i++, j += 2)
            {
                var tmp = palette.Slice(src[i], 4);
                dst[j] = FastMath.Grayscale(tmp[0], tmp[1], tmp[2]);
                dst[j + 1] = tmp[3];
            }
        }

        private static void RGBToGray8(ReadOnlySpan<byte> src, Span<byte> dst)
        {
            for (int i = 0, j = 0; i < src.Length; i += 3, j++)
            {
                dst[j] = FastMath.Grayscale(src[i], src[i + 1], src[i + 2]);
            }
        }

        private static void Gray16ToGray8(ReadOnlySpan<byte> src, Span<byte> dst)
        {
            for (int i = 0, j = 0; i < src.Length; i += 2, j++)
            {
                dst[j] = src[i];
            }
        }

        private static void Gray8ToRGB(ReadOnlySpan<byte> src, Span<byte> dst)
        {
            for (int i = 0, j = 0; i < src.Length; i++, j += 4)
            {
                dst[j] = src[i];
                dst[j + 1] = src[i];
                dst[j + 2] = src[i];
            }
        }

        private static void Gray8ToRGBA(ReadOnlySpan<byte> src, Span<byte> dst)
        {
            for (int i = 0, j = 0; i < src.Length; i++, j += 4)
            {
                dst[j] = src[i];
                dst[j + 1] = src[i];
                dst[j + 2] = src[i];
                dst[j + 3] = 0xFF;
            }
        }

        private static void Gray16ToRGB(ReadOnlySpan<byte> src, Span<byte> dst)
        {
            for (int i = 0, j = 0; i < src.Length; i += 2, j += 3)
            {
                dst[j] = src[i];
                dst[j + 1] = src[i];
                dst[j + 2] = src[i];
            }
        }

        private static void Gray16ToRGBA(ReadOnlySpan<byte> src, Span<byte> dst)
        {
            for (int i = 0, j = 0; i < src.Length; i += 2, j += 3)
            {
                dst[j] = src[i];
                dst[j + 1] = src[i];
                dst[j + 2] = src[i];
                dst[j + 3] = src[i + 1];
            }
        }

        private static void Gray8ToGray16(ReadOnlySpan<byte> src, Span<byte> dst)
        {
            for (int i = 0, j = 0; i < src.Length; i++, j += 2)
            {
                dst[j] = src[i];
                dst[j + 1] = 0xFF;
            }
        }

        private static void RGBAToGray8(ReadOnlySpan<byte> src, Span<byte> dst)
        {
            for (int i = 0, j = 0; i < src.Length; i += 4, j++)
            {
                dst[j] = FastMath.Grayscale(src[i], src[i + 1], src[i + 2]);
            }
        }

        private static void RGBToGray16(ReadOnlySpan<byte> src, Span<byte> dst)
        {
            for (int i = 0, j = 0; i < src.Length; i += 3, j += 2)
            {
                dst[j] = FastMath.Grayscale(src[i], src[i + 1], src[i + 2]);
                dst[j + 1] = 0xFF;
            }
        }

        private static void RGBAToGray16(ReadOnlySpan<byte> src, Span<byte> dst)
        {
            for (int i = 0, j = 0; i < src.Length; i += 4, j += 2)
            {
                dst[j] = FastMath.Grayscale(src[i], src[i + 1], src[i + 2]);
                dst[j + 1] = src[i + 3];
            }
        }

        private static void RGBToRGBA(ReadOnlySpan<byte> src, Span<byte> dst)
        {
            for (int i = 0, j = 0; i < src.Length; i += 3, j += 4)
            {
                dst[j] = src[i];
                dst[j + 1] = src[i + 1];
                dst[j + 2] = src[i + 2];
                dst[j + 3] = 0xFF;
            }
        }

        private static void IndexedToRGBA(ReadOnlySpan<byte> src, Span<byte> dst)
        {
            ReadOnlySpan<byte> palette = src.Slice(0, 1024);
            src = src.Slice(1024);
            for (int i = 0, j = 0; i < src.Length; i++, j += 4)
            {
                palette.Slice(src[i] << 2, 4).CopyTo(dst.Slice(j, 4));
            }
        }

        private static void IndexedToRGB(ReadOnlySpan<byte> src, Span<byte> dst)
        {
            ReadOnlySpan<byte> palette = src.Slice(0, 1024);
            src = src.Slice(1024);
            for (int i = 0, j = 0; i < src.Length; i++, j += 3)
            {
                palette.Slice(src[i] << 2, 3).CopyTo(dst.Slice(j, 3));
            }
        }

        private int GetOffset() => _format == ImageFormat.Indexed ? 1024 : 0;
    }

}
