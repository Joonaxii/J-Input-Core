using Joonaxii.Collections;
using Joonaxii.Hashing;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Joonaxii.IO
{
    public unsafe static class IOUtil
    {
        // It's dumb but we kind of have to make our own UTF8 Encoding object
        // instead of using Encoding.UTF8 since Encoding.UTF8 adds the BOM
        // by default when encoding which has caused me many gray hairs already.
        public static Encoding UTF8_NO_BOM { get; } = new UTF8Encoding(false);

        public delegate void OnCopy(ReadOnlySpan<byte> buffer);

        // Since this core library is going to be used in Unity which 
        // does not support .NET Core (yet), the APIs for using Spans
        // in Streams is not present, so to still get benefits of Spans
        // elsewhere, we have to use "traditional" buffers for buffered reads/writes
        // even if we're passing in a span to the write/read methods.
        private static RefStack<byte> IO_BUFFER = new RefStack<byte>(256);
        private static RefStack<char> CHAR_BUFFER = new RefStack<char>(256);

        public static Span<byte> ReadBuffered(Stream stream, int length = -1)
        {
            length = (int)(length < 0 ? stream.Length : Math.Min(length, stream.Length));
            IO_BUFFER.Resize(length, false, false);
            stream.Read(IO_BUFFER.Buffer, 0, length);
            return IO_BUFFER.AsSpan(0, length);
        }

        public static void BufferedCopyTo(this Stream stream, Stream other, int bufferSize, OnCopy onCopy = null)
        {
            bufferSize = FastMath.Max(bufferSize, IO_BUFFER.Capacity);
            IO_BUFFER.Resize(bufferSize, false);

            long pos = stream.Position;
            long len = stream.Length - pos;
            while(len > 0)
            {
                int read = (int)FastMath.Min(bufferSize, len);
                read = stream.Read(IO_BUFFER.Buffer, 0, read);
                if(read < 1) { break; }

                if(onCopy != null)
                {
                    onCopy.Invoke(IO_BUFFER.AsSpan(0, read));
                }

                other.Write(IO_BUFFER.Buffer, 0, read);
                len -= read;
            }
            if (stream.CanSeek)
            {
                stream.Seek(pos, SeekOrigin.Begin);
            }
        }
        public static void BufferedRead(this Stream stream, Span<byte> output, int bufferSize = 2048)
        {
            if (output.Length < 1) { return; }

            bufferSize = FastMath.Max(bufferSize, IO_BUFFER.Capacity);
            IO_BUFFER.Resize(bufferSize, false);

            int len = output.Length;
            int outPos = 0;
            while (len > 0 && outPos < output.Length)
            {
                int read = (int)FastMath.Min(bufferSize, len);
                read = stream.Read(IO_BUFFER.Buffer, 0, read);
                if (read < 1) { break; }

                IO_BUFFER.AsSpan(0, FastMath.Min(read, output.Length - outPos)).CopyTo(output.Slice(outPos));
                len -= read;
                outPos += read;
            }
        }
        public static void BufferedWrite(this Stream stream, ReadOnlySpan<byte> input, int bufferSize = 2048)
        {
            if(input.Length < 1) { return; }

            bufferSize = FastMath.Max(bufferSize, IO_BUFFER.Capacity);
            IO_BUFFER.Resize(bufferSize, false);

            long len = input.Length;
            int inPos = 0;
            while (len > 0 && inPos < input.Length)
            {
                int read = (int)FastMath.Min(bufferSize, len);
                if (read < 1) { break; }
                input.Slice(inPos, read).CopyTo(IO_BUFFER.AsSpan());
                stream.Write(IO_BUFFER.Buffer, 0, read);
                len -= read;
                inPos += read;
            }
        }

        public static void Read(this Stream stream, int bytesToRead, ref long position, IRefStack<byte> buffer, int bufferSize = 2048)
        {
            bufferSize = FastMath.Max(bufferSize, IO_BUFFER.Capacity, 256);

            IO_BUFFER.Resize(bufferSize, false);
            while(bytesToRead > 0)
            {
                int read = stream.Read(IO_BUFFER.Buffer, 0, FastMath.Min(bufferSize, bytesToRead));
                position += read;
                if (read <= 0) { break; }
                buffer.Push(IO_BUFFER.AsSpan(0, read));
                bytesToRead -= read;
            }
        }

        public static ref CRC32.State Update(ref this CRC32.State state, Stream stream, int bytesToRead, int bufferSize = 2048)
        {
            bufferSize = FastMath.Max(bufferSize, IO_BUFFER.Capacity);
            IO_BUFFER.Resize(bufferSize, false);

            while (bytesToRead > 0)
            {
                int read = (int)FastMath.Min(bufferSize, bytesToRead);
                read = stream.Read(IO_BUFFER.Buffer, 0, read);
                if (read < 1) { break; }
                state.Update(IO_BUFFER.AsSpan(0, read));
                bytesToRead -= read;
            }
            return ref state;
        }
        
        public static ref MD5.State Update(ref this MD5.State state, Stream stream, int bytesToRead, int bufferSize = 2048)
        {
            bufferSize = FastMath.Max(bufferSize, IO_BUFFER.Capacity);
            IO_BUFFER.Resize(bufferSize, false);

            while (bytesToRead > 0)
            {
                int read = (int)FastMath.Min(bufferSize, bytesToRead);
                read = stream.Read(IO_BUFFER.Buffer, 0, read);
                if (read < 1) { break; }
                state.Update(IO_BUFFER.AsSpan(0, read));
                bytesToRead -= read;
            }
            return ref state;
        }

        public static void WriteZero(this Stream stream, ref long pos, int count)
        {
            if(count <= 0) { return; }
            IO_BUFFER.Reserve(count, false);
            IO_BUFFER.AsSpan().ZeroMem();
            stream.Write(IO_BUFFER.Buffer, 0, count);
            pos += count;
        }

        public static void WriteValue<T>(this Stream stream, T value, ref long position, bool bigEndian = false) where T : unmanaged
            => stream.WriteValue(ref value, -1, ref position, bigEndian);

        public static void WriteValue<T>(this Stream stream, T value, int bytesToWrite, ref long position, bool bigEndian = false) where T : unmanaged
            => stream.WriteValue(ref value, bytesToWrite, ref position, bigEndian);

        public static void WriteValue<T>(this Stream stream, ref T value, ref long position, bool bigEndian = false) where T : unmanaged
            => stream.WriteValue(ref value, -1, ref position, bigEndian);

        public static void WriteValue<T>(this Stream stream, ref T value, int bytesToWrite, ref long position, bool bigEndian = false) where T : unmanaged
        {
            var bytes = UnsafeUtil.AsBytes(ref value);
            bytesToWrite = bytesToWrite < 0 ? bytes.Length : FastMath.Min(bytesToWrite, bytes.Length);

            IO_BUFFER.Resize(bytesToWrite, false);
            var data = IO_BUFFER.AsSpan();
            bytes.Slice(0, bytesToWrite).CopyTo(data);
   
            if (bigEndian)
            {
                data.Reverse();
            }

            stream.Write(IO_BUFFER.Buffer, 0, bytesToWrite);
            position += bytesToWrite;
        }

        public static void WriteValue<T>(this Stream stream, ref T value, bool bigEndian = false) where T : unmanaged
        => WriteValue<T>(stream, ref value, -1, bigEndian);
        public static void WriteValue<T>(this Stream stream, T value, bool bigEndian = false) where T : unmanaged
        => WriteValue<T>(stream, ref value, -1, bigEndian);
        public static void WriteValue<T>(this Stream stream, ref T value, int bytesToWrite, bool bigEndian = false) where T : unmanaged
        {
            var bytes = UnsafeUtil.AsBytes(ref value);
            bytesToWrite = bytesToWrite < 0 ? bytes.Length : FastMath.Min(bytesToWrite, bytes.Length);

            IO_BUFFER.Resize(bytesToWrite, false);
            var data = IO_BUFFER.AsSpan();
            bytes.Slice(0, bytesToWrite).CopyTo(data);
   
            if (bigEndian)
            {
                data.Reverse();
            }

            stream.Write(IO_BUFFER.Buffer, 0, bytesToWrite);
        }

        public static bool TryRead<T>(this Stream stream, ref T value, ref long position, bool bigEndian = false) where T : unmanaged
        {
            int tSize = Marshal.SizeOf<T>();
            IO_BUFFER.Reserve(tSize, false);

            int read = stream.Read(IO_BUFFER.Buffer, 0, tSize);
            var data = IO_BUFFER.Buffer.AsSpan(0, tSize);

            if (bigEndian)
            {
                data.Reverse();
            }

            data.CopyTo(UnsafeUtil.AsBytes(ref value));
            position += tSize;
            return read == tSize;
        }

        public static bool TryRead<T>(this Stream stream, ref T value, int readAmount, ref long position, bool bigEndian = false) where T : unmanaged
        {
            int tSize = FastMath.Min(readAmount, Marshal.SizeOf<T>());
            IO_BUFFER.Reserve(tSize, false);

            int read = stream.Read(IO_BUFFER.Buffer, 0, tSize);
            var data = IO_BUFFER.Buffer.AsSpan(0, tSize);

            if (bigEndian)
            {
                data.Reverse();
            }

            data.CopyTo(UnsafeUtil.AsBytes(ref value));

            position += tSize;
            return read == tSize;
        }
        

        public static bool TryRead<T>(this Stream stream, ref T value, bool bigEndian = false) where T : unmanaged
        {
            int tSize = Marshal.SizeOf<T>();
            IO_BUFFER.Reserve(tSize, false);

            int read = stream.Read(IO_BUFFER.Buffer, 0, tSize);
            var data = IO_BUFFER.Buffer.AsSpan(0, tSize);

            if (bigEndian)
            {
                data.Reverse();
            }

            data.CopyTo(UnsafeUtil.AsBytes(ref value));
            return read == tSize;
        }

        public static string ReadString<T>(this Stream stream, ref long position, Encoding enc = null) where T : unmanaged
        {
            enc ??= IO.IOUtil.UTF8_NO_BOM;
            int tSize = Marshal.SizeOf<T>();
            tSize = tSize > 4 ? 4 : tSize;

            IO_BUFFER.Reserve(tSize, false);

            int length = 0;
            stream.Read(IO_BUFFER.Buffer, 0, tSize);
            IO_BUFFER.Buffer.AsSpan(0, tSize).CopyTo(UnsafeUtil.AsBytes(ref length));

            IO_BUFFER.Reserve(length, false);
            stream.Read(IO_BUFFER.Buffer, 0, length);

            position += length + tSize;
            int chCount = enc.GetCharCount(IO_BUFFER.Buffer, 0, length);
            CHAR_BUFFER.Reserve(chCount, false);
            chCount = enc.GetChars(IO_BUFFER.Buffer, 0, length, CHAR_BUFFER.Buffer, 0);
            return new string(CHAR_BUFFER.Buffer, 0, chCount);
        }

        public static void SkipString<T>(this Stream stream, ref long position) where T : unmanaged
        {
            int tSize = Marshal.SizeOf<T>();
            tSize = tSize > 4 ? 4 : tSize;
            IO_BUFFER.Reserve(tSize, false);

            int length = 0;
            stream.Read(IO_BUFFER.Buffer, 0, tSize);
            stream.Seek(length, SeekOrigin.Current);
            position += length + tSize;
        }
     }
}
