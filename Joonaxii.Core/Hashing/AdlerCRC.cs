using Joonaxii.Collections;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Joonaxii.Hashing
{
    public unsafe static class AdlerCRC
    {
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

        public static Hash ComputeAdler(this byte[] bytes)
        {
            ReadOnlySpan<byte> span = bytes.AsSpan();
            return span.ComputeAdler();
        }

        public static Hash ComputeAdler(this ReadOnlySpan<byte> bytes)
        {
            State state = default;
            return state.Init().Update(bytes).Extract();
        }
        public static Hash ComputeAdler(this Span<byte> bytes)
        {
            ReadOnlySpan<byte> bytesR = bytes;
            return bytesR.ComputeAdler();
        }

        public static long AddOfObject(this object obj)
        {
            long refId = 0;
            if (obj != null)
            {
                GCHandle handle = GCHandle.Alloc(obj, GCHandleType.Weak);
                try
                {
                    IntPtr ptr = GCHandle.ToIntPtr(handle);
                    refId = ptr.ToInt64();
                }
                finally
                {
                    handle.Free();
                }
            }
            return refId;
        }

        public static Hash ComputeAdler(this object value)
        {
            return AddOfObject(value).ComputeAdler();
        }

        public static Hash ComputeAdler<T>(this T value) where T : unmanaged
        {
            if (value is IHashable<State> crc)
            {
                State state = default;
                state.Init();
                return crc.UpdateHash(ref state).Extract();
            }
            return MemoryMarshal.AsBytes(UnsafeUtil.AsBytes(ref value)).ComputeAdler();
        }

        public static ref State Update<T>(ref this State state, T value) where T : unmanaged
        {
            if (value is IHashable<State> crc)
            {
                return ref crc.UpdateHash(ref state);
            }
            return ref state.Update(UnsafeUtil.AsBytes(ref value));
        }

        public static Hash ComputeAdler(this string str) => str.AsSpan().ComputeAdler(Encoding.UTF8);
        public static Hash ComputeAdler(this string str, Encoding enc) => str.AsSpan().ComputeAdler(enc);

        public static Hash ComputeAdler(this ReadOnlySpan<char> str) => str.ComputeAdler(Encoding.UTF8);
        public static Hash ComputeAdler(this ReadOnlySpan<char> str, Encoding enc)
        {
            fixed (char* cPtr = str)
            {
                int len = cPtr != null ? enc.GetByteCount(cPtr, str.Length) : 0;
                Span<byte> temp = len > 1024 ? new byte[len] : stackalloc byte[len];
                fixed (byte* bPtr = temp)
                {
                    int count = cPtr != null ? enc.GetBytes(cPtr, str.Length, bPtr, len) : 0;
                    return temp.Slice(0, count).ComputeAdler();
                }
            }
        }

        public static ref State Init(this ref State state)
        {
            state.Reset();
            return ref state;
        }

        public static ref State Update(ref this State state, object obj) => ref state.Update(AddOfObject(obj));

        public static ref State Update(ref this State state, string str) => ref state.Update(str.AsSpan(), Encoding.UTF8);
        public static ref State Update(ref this State state, string str, Encoding enc) => ref state.Update(str.AsSpan(), enc);
        public static ref State Update(ref this State state, ReadOnlySpan<char> str) => ref state.Update(str, Encoding.UTF8);
        public static ref State Update(ref this State state, ReadOnlySpan<char> str, Encoding enc)
        {
            fixed (char* cPtr = str)
            {
                int len = cPtr != null ? enc.GetByteCount(cPtr, str.Length) : 0;
                Span<byte> temp = len > 1024 ? new byte[len] : stackalloc byte[len];
                fixed (byte* bPtr = temp)
                {
                    int count = cPtr != null ? enc.GetBytes(cPtr, str.Length, bPtr, len) : 0;
                    ReadOnlySpan<byte> sliced = temp.Slice(0, count);
                    state.Update(sliced);
                    return ref state;
                }
            }
        }

        public static ref State Update(ref this State state, ReadOnlySpan<byte> bytes)
        {
            state.Push(bytes);
            return ref state;
        }

        public struct State
        {
            public const uint MODULO = 65521;

            public uint sum1;
            public uint sum2;

            public void Reset()
            {
                sum1 = 1;
                sum2 = 0;
            }

            public void Push(ReadOnlySpan<byte> data)
            {
                unsafe
                {
                    fixed (byte* bPtr = data)
                    {
                        for (int i = 0; i < data.Length; i++)
                        {
                            sum1 = (sum1 + data[i]) % MODULO;
                            sum2 = (sum1 + sum2) % MODULO;
                        }
                    }
                }
            }

            public Hash Extract()
            {
                return (sum2 << 16) | sum1;
            }
        }

        public struct Hash : IEquatable<Hash>, IRefComparable<Hash>, IRefComparable<uint>, IRefEquatable<Hash>, IRefEquatable<uint>
        {
            public uint value;

            public Hash(uint value)
            {
                this.value = value;
            }

            public override bool Equals(object obj)
            {
                return obj is Hash hash && Equals(hash);
            }

            public bool Equals(Hash other)
            {
                return value == other.value;
            }

            public override int GetHashCode()
            {
                return value.GetHashCode();
            }

            public static bool operator ==(Hash left, Hash right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(Hash left, Hash right)
            {
                return !(left == right);
            }

            public static implicit operator uint(Hash hash)
            {
                return hash.value;
            }

            public static implicit operator Hash(uint hash)
            {
                return new Hash(hash);
            }

            public override string ToString()
            {
                return $"{value:X}";
            }

            public int CompareTo(in Hash other)
            {
                return value.CompareTo(other.value);
            }

            public int CompareTo(in uint other)
            {
                return value.CompareTo(other);
            }

            public bool Equals(in Hash other)
            {
                return value == other.value;
            }

            public bool Equals(in uint other)
            {
                return value == other;
            }
        }
    }
}
