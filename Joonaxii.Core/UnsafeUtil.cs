using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Joonaxii
{
    public unsafe static class UnsafeUtil
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Swap<T>(ref T lhs, ref T rhs)
        {
            T temp = lhs;
            lhs = rhs;
            rhs = temp;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static U Reinterpret<T, U>(this T input) where T : unmanaged where U : unmanaged
        {
            return *(U*)(&input);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool UnsafeEquals<T, U, C>(T lhs, U rhs) where T : unmanaged where U : unmanaged where C : unmanaged, IEquatable<C>
        {
            return (*(C*)(&lhs)).Equals(*(C*)(&rhs));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<byte> AsBytes<T>(ref T input) where T : unmanaged
        {
            return MemoryMarshal.AsBytes(new Span<T>(Unsafe.AsPointer(ref input), 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyTo<T>(T* src, Span<T> dst) where T : unmanaged
        {
            for (int i = 0; i < dst.Length; i++)
            {
                dst[i] = *src++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyTo<T>(T* src, T* dst, int count) where T : unmanaged
        {
            while(count-- > 0)
            {
                *dst++ = *src++;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyTo<T>(this ReadOnlySpan<T> src, T* dst) where T : unmanaged
        {
            for (int i = 0; i < src.Length; i++)
            {
                *dst++ = src[i];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyTo<T>(this Span<T> src, T* dst) where T : unmanaged
        {
            for (int i = 0; i < src.Length; i++)
            {
                *dst++ = src[i];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ZeroMem<T>(this Span<T> input) where T : unmanaged
        {
            fixed(T* ptr = input)
            {
                int len = input.Length;
                T* iPtr = ptr;
                while (len-- > 0)
                {
                    *iPtr++ = default;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Memset<T>(this Span<T> input, in T value) where T : unmanaged
        {
            fixed(T* ptr = input)
            {
                int len = input.Length;
                T* iPtr = ptr;
                while (len-- > 0)
                {
                    *iPtr++ = value;
                }
            }
        }

        public static unsafe int SizeOf<T>() where T : unmanaged => sizeof(T);

        public static int IndexOf<T>(T* data, int count, T value) where T : unmanaged, IEquatable<T>
        {
            for (int i = 0; i < count; i++, data++)
            {
                if (data->Equals(value)) { return i; }
            }
            return -1;
        }

        public static int IndexOf<T>(T* data, int count, T value, Func<T, T, bool> equals) where T : unmanaged
        {
            for (int i = 0; i < count; i++, data++)
            {
                if (equals(*data, value)) { return i; }
            }
            return -1;
        }

        public static void StrCopy(char* dest, int destLen, string str)
        {
            Debugger.Assert(str.Length <= destLen, $"Length of string ({str.Length}) is larger than length of destination ({destLen})!");
            fixed (char* ptr = str)
            {
                Buffer.MemoryCopy(ptr, dest, destLen << 1, str.Length << 1);
            }
        }

        public static ref TTo CopyBytes<TFrom, TTo>(ref TFrom valueFrom, ref TTo valueTo)
             where TFrom : unmanaged where TTo : unmanaged
        {
            Debugger.Assert(sizeof(TTo) <= sizeof(TFrom), $"Size of '{typeof(TTo)}' ({sizeof(TTo)}) is larger than size of '{typeof(TFrom)}' ({sizeof(TFrom)})!");
            valueTo = default(TTo);

            fixed (TFrom* tF = &valueFrom)
            fixed (TTo* tT = &valueTo)
            {
                Buffer.MemoryCopy(tF, tT, sizeof(TTo), sizeof(TFrom));
            }
            return ref valueTo;
        }

        public static T Read<T>(void* data, int dataLen) where T : unmanaged
        {
            Debugger.Assert(sizeof(T) <= dataLen, $"Size of '{typeof(T)}' ({sizeof(T)}) is larger than size of given data ({dataLen})!");
            T dOut = default;
            dOut = *(T*)data;
            return dOut;
        }

        public static void Write<T>(void* dest, int destLen, T value) where T : unmanaged
        {
            Debugger.Assert(sizeof(T) <= destLen, $"Size of '{typeof(T)}' ({sizeof(T)}) is larger than size of destination data ({destLen})!");
            Buffer.MemoryCopy(&value, dest, destLen, sizeof(T));
        }

        public static void InverseMemoryCopy<TFrom, TTo>(TFrom* source, TTo* destination, int destCount, int sourceCount)
                 where TFrom : unmanaged where TTo : unmanaged
        {
            Debugger.Assert(sizeof(TTo) <= sizeof(TFrom), $"Size of '{typeof(TTo)}' ({sizeof(TTo)}) is larger than size of '{typeof(TFrom)}' ({sizeof(TFrom)})!");
            Debugger.Assert(sourceCount <= destCount, $"Source count is larger than destination count! ({sourceCount} > {destCount})");

            TFrom* srcE = source + sourceCount - 1;
            TTo* destE = destination + sourceCount - 1;
            for (int i = 0; i < sourceCount; i++)
            {
                *destE-- = *(TTo*)srcE--;
            }
        }
    }
}
