using System;
using System.Runtime.InteropServices;
using System.Text;
using Joonaxii.Collections;

namespace Joonaxii.Hashing
{
    public unsafe static class MD5
    {
        private static readonly int[] TABLE_S = new int[64]
        {
           7, 12, 17, 22,  7, 12, 17, 22,  7, 12, 17, 22,  7, 12, 17, 22,
           5,  9, 14, 20,  5,  9, 14, 20,  5,  9, 14, 20,  5,  9, 14, 20,
           4, 11, 16, 23,  4, 11, 16, 23,  4, 11, 16, 23,  4, 11, 16, 23,
           6, 10, 15, 21,  6, 10, 15, 21,  6, 10, 15, 21,  6, 10, 15, 21,
        };
        private static readonly uint[] TABLE_K = new uint[64]
        {
            0xd76aa478U, 0xe8c7b756U, 0x242070dbU, 0xc1bdceeeU,
            0xf57c0fafU, 0x4787c62aU, 0xa8304613U, 0xfd469501U,
            0x698098d8U, 0x8b44f7afU, 0xffff5bb1U, 0x895cd7beU,
            0x6b901122U, 0xfd987193U, 0xa679438eU, 0x49b40821U,
            0xf61e2562U, 0xc040b340U, 0x265e5a51U, 0xe9b6c7aaU,
            0xd62f105dU, 0x02441453U, 0xd8a1e681U, 0xe7d3fbc8U,
            0x21e1cde6U, 0xc33707d6U, 0xf4d50d87U, 0x455a14edU,
            0xa9e3e905U, 0xfcefa3f8U, 0x676f02d9U, 0x8d2a4c8aU,
            0xfffa3942U, 0x8771f681U, 0x6d9d6122U, 0xfde5380cU,
            0xa4beea44U, 0x4bdecfa9U, 0xf6bb4b60U, 0xbebfbc70U,
            0x289b7ec6U, 0xeaa127faU, 0xd4ef3085U, 0x04881d05U,
            0xd9d4d039U, 0xe6db99e5U, 0x1fa27cf8U, 0xc4ac5665U,
            0xf4292244U, 0x432aff97U, 0xab9423a7U, 0xfc93a039U,
            0x655b59c3U, 0x8f0ccc92U, 0xffeff47dU, 0x85845dd1U,
            0x6fa87e4fU, 0xfe2ce6e0U, 0xa3014314U, 0x4e0811a1U,
            0xf7537e82U, 0xbd3af235U, 0x2ad7d2bbU, 0xeb86d391U,
        };

        public static ref State Update(ref this State state, byte[] bytes)
        {
            ReadOnlySpan<byte> span = bytes.AsSpan();
            return ref state.Update(span);
        }
        public static ref State Update(ref this State state, Span<byte> bytes)
        {
            ReadOnlySpan<byte> bytesR = bytes;
            return ref state.Update(bytesR);
        }

        public static Hash ComputeMD5(this byte[] bytes)
        {
            ReadOnlySpan<byte> span = bytes.AsSpan();
            return span.ComputeMD5();
        }
        public static Hash ComputeMD5(this ReadOnlySpan<byte> data)
        {
            State state = new State();
            return state.Init().Update(data).Extract();
        }
        public static Hash ComputeMD5(this Span<byte> bytes)
        {
            ReadOnlySpan<byte> bytesR = bytes;
            return bytesR.ComputeMD5();
        }
        public static Hash ComputeMD5<T>(this T value) where T : unmanaged
        {
            if (value is IHashable<State> crc)
            {
                State state = default;
                state.Init();
                return crc.UpdateHash(ref state).Extract();
            }
            return MemoryMarshal.AsBytes(UnsafeUtil.AsBytes(ref value)).ComputeMD5();
        }

        public static ref State Update<T>(ref this State state, T value) where T : unmanaged
        {
            if (value is IHashable<State> crc)
            {
                return ref crc.UpdateHash(ref state);
            }
            return ref state.Update(UnsafeUtil.AsBytes(ref value));
        }

        public static Hash ComputeMD5(this string str) => str.AsSpan().ComputeMD5(Encoding.UTF8);
        public static Hash ComputeMD5(this string str, Encoding enc) => str.AsSpan().ComputeMD5(enc);

        public static Hash ComputeMD5(this Span<char> str) => ((ReadOnlySpan<char>)str).ComputeMD5(Encoding.UTF8);
        public static Hash ComputeMD5(this Span<char> str, Encoding enc) => ((ReadOnlySpan<char>)str).ComputeMD5(enc);

        public static Hash ComputeMD5(this ReadOnlySpan<char> str) => str.ComputeMD5(Encoding.UTF8);
        public static Hash ComputeMD5(this ReadOnlySpan<char> str, Encoding enc)
        {
            fixed (char* cPtr = str)
            {
                int len = cPtr == null ? 0 : enc.GetByteCount(cPtr, str.Length);
                Span<byte> temp = len > 1024 ? new byte[len] : stackalloc byte[len];
                fixed (byte* bPtr = temp)
                {
                    int count = bPtr == null || cPtr == null ? 0 : enc.GetBytes(cPtr, str.Length, bPtr, temp.Length);
                    ReadOnlySpan<byte> sliced = temp.Slice(0, count);
                    return sliced.ComputeMD5();
                }
            }
        }

        public static ref State Init(ref this State state)
        {
            state.Reset();
            return ref state;
        }

        public static ref State Update(ref this State state, string str) => ref state.Update(str.AsSpan(), Encoding.UTF8);
        public static ref State Update(ref this State state, string str, Encoding enc) => ref state.Update(str.AsSpan(), enc);
        public static ref State Update(ref this State state, ReadOnlySpan<char> str) => ref state.Update(str, Encoding.UTF8);
        public static ref State Update(ref this State state, ReadOnlySpan<char> str, Encoding enc)
        {
            fixed (char* cPtr = str)
            {
                int len = cPtr == null ? 0 : enc.GetByteCount(cPtr, str.Length);
                Span<byte> temp = len > 1024 ? new byte[len] : stackalloc byte[len];
                fixed (byte* bPtr = temp)
                {
                    int count = bPtr == null || cPtr == null ? 0 : enc.GetBytes(cPtr, len, bPtr, temp.Length);
                    Update(ref state, temp.Slice(0, count));
                }
                return ref state;
            }
        }
        public static ref State Update(ref this State state, ReadOnlySpan<byte> data)
        {
            state.Push(data);
            return ref state;
        }

        public static MD5.Hash Extract(ref this State state)
        {
            return state.Extract();
        }

        public unsafe struct State
        {
            public Hash current;
            public fixed uint chunk[16];
            public int total;
            public int length;

            public void Reset()
            {
                current = new Hash(Hash.DEF_V0, Hash.DEF_V1, Hash.DEF_V2, Hash.DEF_V3);
                total = 0;
                length = 0;

                for (int i = 0; i < 16; i++)
                {
                    chunk[i] = 0;
                }
            }

            public void Push(ReadOnlySpan<byte> data)
            {
                fixed (uint* ptr = chunk)
                {
                    Span<byte> buffer = new Span<byte>(ptr, 64);
                    total += data.Length;
                    int pos = 0;

                    while (pos < data.Length)
                    {
                        int read = Math.Min(data.Length - pos, 64 - length);
                        data.Slice(pos, read).CopyTo(buffer.Slice(length));

                        length += read;
                        pos += read;

                        if (length >= 64)
                        {
                            Flush();
                        }
                    }
                }
            }

            public void Flush()
            {
                MD5.Hash hash = current;

                uint temp, f;
                int g;
                for (int i = 0; i < 64; i++)
                {
                    if (i < 16)
                    {
                        f = (hash.b & hash.c) | ((~hash.b) & hash.d);
                        g = i;
                    }
                    else if (i < 32)
                    {
                        f = (hash.d & hash.b) | ((~hash.d) & hash.c);
                        g = (5 * i + 1) & 0xF;
                    }
                    else if (i < 48)
                    {
                        f = hash.b ^ hash.c ^ hash.d;
                        g = (3 * i + 5) & 0xF;
                    }
                    else
                    {
                        f = hash.c ^ (hash.b | (~hash.d));
                        g = (7 * i) & 0xF;
                    }

                    temp = hash.d;
                    hash.d = hash.c;
                    hash.c = hash.b;
                    hash.b += RotateL((hash.a + f + TABLE_K[i] + chunk[g]), TABLE_S[i]);
                    hash.a = temp;
                }

                current.a += hash.a;
                current.b += hash.b;
                current.c += hash.c;
                current.d += hash.d;
                length = 0;

                for (int i = 0; i < 16; i++)
                {
                    chunk[i] = 0;
                }
            }

            public Hash Extract()
            {
                State temp = this;
                return temp.Extract_Internal();
            }

            private Hash Extract_Internal()
            {
                fixed (uint* ptr = chunk)
                {
                    Span<byte> data = new Span<byte>(ptr, 64);
                    ulong bits = ((ulong)total) << 3;

                    int left = 64 - length;
                    if (left <= 0)
                    {
                        Flush();
                        data[0] = 0x80;
                        MemoryMarshal.Write(data.Slice(64 - 8), ref bits);
                        Flush();
                    }
                    else if (left < 9)
                    {
                        data[length] = 0x80;
                        Flush();

                        MemoryMarshal.Write(data.Slice(64 - 8), ref bits);
                        Flush();
                    }
                    else
                    {
                        data[length] = 0x80;
                        MemoryMarshal.Write(data.Slice(64 - 8), ref bits);
                        Flush();
                    }
                }
                return current;
            }

            private static uint RotateL(uint x, int n)
            {
                return ((x) << n) | ((x) >> (32 - n));
            }
        }

        [System.Serializable, StructLayout(LayoutKind.Explicit)]
        public struct Hash : IEquatable<Hash>, IRefComparable<Hash>, IRefEquatable<Hash> 
        {
            public const uint DEF_V0 = 0x67452301U;
            public const uint DEF_V1 = 0xefcdab89U;
            public const uint DEF_V2 = 0x98badcfeU;
            public const uint DEF_V3 = 0x10325476U;

            [FieldOffset(0)] public uint a;
            [FieldOffset(4)] public uint b;
            [FieldOffset(8)] public uint c;
            [FieldOffset(12)] public uint d;

            [FieldOffset(0)] private ulong _lhs;
            [FieldOffset(8)] private ulong _rhs;

            public bool IsZero => (_lhs | _rhs) == 0;

            public Hash(uint v0, uint v1, uint v2, uint v3) : this()
            {
                this.a = v0;
                this.b = v1;
                this.c = v2;
                this.d = v3;
            }

            public override bool Equals(object obj)
            {
                return obj is Hash hash && Equals(hash);
            }

            public bool Equals(Hash other)
            {
                return a == other.a &&
                       b == other.b &&
                       c == other.c &&
                       d == other.d;
            }

            public static bool operator ==(Hash left, Hash right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(Hash left, Hash right)
            {
                return !(left == right);
            }

            public override string ToString()
            {
                return $"{Flip(a):x}{Flip(b):x}{Flip(c):x}{Flip(d):x}";
            }

            public static uint Flip(uint input)
            {
                return ((input >> 24) & 0xFF) | (((input >> 16) & 0xFF) << 8) | (((input >> 8) & 0xFF) << 16) | (((input) & 0xFF) << 24);
            }

            public override int GetHashCode()
            {
                int hashCode = 435695894;
                hashCode = hashCode * -1521134295 + a.GetHashCode();
                hashCode = hashCode * -1521134295 + b.GetHashCode();
                hashCode = hashCode * -1521134295 + c.GetHashCode();
                hashCode = hashCode * -1521134295 + d.GetHashCode();
                return hashCode;
            }

            public int CompareTo(in Hash other)
            {
                int comp = _lhs.CompareTo(other._lhs);
                return comp == 0 ? _rhs.CompareTo(other._rhs) : comp;
            }

            public bool Equals(in Hash rhs)
            {
                return a == rhs.a &&
                       b == rhs.b &&
                       c == rhs.c &&
                       d == rhs.d;
            }
        }
    }
}
