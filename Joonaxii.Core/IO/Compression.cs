using Joonaxii.Hashing;
using System;
using System.IO;
using System.IO.Compression;

namespace Joonaxii.IO
{
    public static class Compression
    {
        public static void Inflate(this ReadOnlySpan<byte> input, Span<byte> output)
        {
            unsafe
            {
                fixed(byte* iPtr = input)
                {
                    using(UnmanagedMemoryStream memIn = new UnmanagedMemoryStream(iPtr + 2, input.Length - 2))
                    using(DeflateStream inf = new DeflateStream(memIn, CompressionMode.Decompress))
                    {
                        inf.BufferedRead(output, 8192);
                    }
                }
            }
        }

        public static DeflateState BeginDeflate(Stream stream, CompressionLevel level = CompressionLevel.Optimal)
        => new DeflateState(stream, level);

        public static void Deflate(Stream stream, ref long position, ReadOnlySpan<byte> input, CompressionLevel level = CompressionLevel.Optimal)
        {
            unsafe
            {
                uint adler = input.ComputeAdler();
                fixed(byte* iPtr = input)
                {
                    AdlerCRC.State state = default;
                    state.Init();

                    ushort header = 0x78;
                    switch (level)
                    {
                        case CompressionLevel.NoCompression:
                        case CompressionLevel.Fastest:
                            header |= 0x0100;
                            break;
                        case CompressionLevel.Optimal:
                            header |= 0xDA00;
                            break;
                        default:
                            header |= 0x9C00;
                            break;
                    }

                    stream.WriteValue(header, ref position);
                    using (UnmanagedMemoryStream memIn = new UnmanagedMemoryStream(iPtr, input.Length))
                    using(DeflateStream inf = new DeflateStream(stream, level, true))
                    {
                        memIn.BufferedCopyTo(inf, 8192, (ReadOnlySpan<byte> data) => state = state.Update(data));
                    }
                    position = stream.Position;
                    stream.WriteValue(ref adler, 2, ref position);
                }
            }
        }


        public class DeflateState : IDisposable
        {
            public AdlerCRC.State adler;
            public long startPos;
            public Stream stream;
            public DeflateStream defStream;

            public DeflateState(Stream stream, CompressionLevel level)
            {
                this.stream = stream;
                startPos = stream.Position;

                ushort header = 0x78;
                switch (level)
                {
                    case CompressionLevel.NoCompression:
                    case CompressionLevel.Fastest:
                        header |= 0x0100;
                        break;
                    case CompressionLevel.Optimal:
                        header |= 0xDA00;
                        break;
                    default:
                        header |= 0x9C00;
                        break;
                }

                long position = 0;
                stream.WriteValue(header, ref position);

                defStream = new DeflateStream(stream, level, true);
                adler.Init();
            }

            public void Deflate(ReadOnlySpan<byte> data)
            {
                if (defStream == null) { return; }
                adler.Update(data);
                defStream.BufferedWrite(data, 8192);
            }

            public void Finish()
            {
                if(defStream == null) { return; }

                defStream.Close();
                defStream.Dispose();
                long pos = stream.Position;
                stream.WriteValue(adler.Extract(), ref pos, true);
            }

            public void Dispose()
            {
                Finish();
            }
        }
    }
}
