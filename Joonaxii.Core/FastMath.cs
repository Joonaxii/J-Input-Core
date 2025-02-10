using Joonaxii.Colors;
using System;
using System.Net;
using System.Runtime.CompilerServices;

namespace Joonaxii
{
    public static class FastMath
    {
        private readonly static float[] UI8_TO_FLOAT;
        private readonly static float[] I8_TO_FLOAT;
        private readonly static byte[] MULT_TABLE;
        private readonly static byte[] DIV_TABLE;

        private readonly static int[] LOG2_TABLE  =
        {
            63,  0, 58,  1, 59, 47, 53,  2,
            60, 39, 48, 27, 54, 33, 42,  3,
            61, 51, 37, 40, 49, 18, 28, 20,
            55, 30, 34, 11, 43, 14, 22,  4,
            62, 57, 46, 52, 38, 26, 32, 41,
            50, 36, 17, 19, 29, 10, 13, 21,
            56, 45, 25, 31, 35, 16,  9, 12,
            44, 24, 15,  8, 23,  7,  6,  5
        };

        static FastMath()
        {
            MULT_TABLE = new byte[256 * 256];
            DIV_TABLE = new byte[256 * 256];
            UI8_TO_FLOAT = new float[256];
            I8_TO_FLOAT = new float[256];
             
            for (int i = 0; i < 256; i++)
            {
                UI8_TO_FLOAT[i] = i / 255.0f;
                I8_TO_FLOAT[i] = (i < -127 ? i : i) / 127.0f;

                for (int j = 0; j < 256; j++)
                {
                    MULT_TABLE[i | (j << 8)] = (byte)((i * j * 0x10101U + 0x800000U) >> 24);
                    DIV_TABLE[i | (j << 8)] = (byte)(j < 0 ? 0 : ((i / (float)(j)) * 255.0f));
                }
            }
        }

        public static int Log2(ulong value)
        {
            if (value == 0) { return 0; }
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            value |= value >> 32;
            return LOG2_TABLE[(((value - (value >> 1)) * 0x07EDD5E59A4E28C2)) >> 58];
        }

        public static int Log2(long value) => Log2((ulong)value);

        public static bool IsPowerOf2(this sbyte value) => (value & (value - 1)) == 0;
        public static bool IsPowerOf2(this byte value) => (value & (value - 1)) == 0;

        public static bool IsPowerOf2(this short value) => (value & (value - 1)) == 0;
        public static bool IsPowerOf2(this ushort value) => (value & (value - 1)) == 0;

        public static bool IsPowerOf2(this int value) => (value & (value - 1)) == 0;
        public static bool IsPowerOf2(this uint value) => (value & (value - 1)) == 0;

        public static bool IsPowerOf2(this long value) => (value & (value - 1)) == 0;
        public static bool IsPowerOf2(this ulong value) => (value & (value - 1)) == 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Grayscale(this Color32 rgba)
         => Grayscale(rgba.r, rgba.g, rgba.b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Grayscale(byte r, byte g, byte b)
        {
            return (byte)FastMath.Clamp((r * 0.299) + (g * 0.587) + (b * 0.114), 0, 255);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Clamp01(float value)
            => value < 0 ? 0 : value > 1 ? 1 : value;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Clamp(int value, int min, int max)
            => value < min ? min : value > max ? max : value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Clamp(float value, float min, float max)
            => value < min ? min : value > max ? max : value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Clamp(double value, double min, double max)
            => value < min ? min : value > max ? max : value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InverseLerp01(float a, float b, float v)
        {
            return Clamp01((v - a) / (b - a));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToInt(this bool value) => value ? 1 : 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte MultUI8(int lhs, int rhs)
        {
            return MULT_TABLE[(lhs & 0xFF) | ((rhs & 0xFF) << 8)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte DivUI8(int lhs, int rhs)
        {
            return DIV_TABLE[(lhs & 0xFF) | ((rhs & 0xFF) << 8)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Max(int lhs, int rhs)
        {
            return lhs < rhs ? rhs : lhs;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Max(int v0, int v1, int v2)
            => Max(Max(v0, v1), v2);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Min(int lhs, int rhs)
        {
            return lhs > rhs ? rhs : lhs;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort Min(ushort lhs, ushort rhs)
        {
            return lhs > rhs ? rhs : lhs;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Max(byte lhs, byte rhs)
        {
            return lhs < rhs ? rhs : lhs;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Min(byte lhs, byte rhs)
        {
            return lhs > rhs ? rhs : lhs;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Min(long lhs, long rhs)
        {
            return lhs > rhs ? rhs : lhs;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Max(double lhs, double rhs)
        {
            return lhs < rhs ? rhs : lhs;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Min(double lhs, double rhs)
        {
            return lhs > rhs ? rhs : lhs;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref double Max(ref double lhs, ref double rhs)
        {
            return ref lhs > rhs ? ref rhs : ref lhs;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref double Min(ref double lhs, ref double rhs)
        {
            return ref lhs < rhs ? ref rhs : ref lhs;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref double Mid(ref double x, ref double y, ref double z)
        {
            return ref x > y ? ref y > z ? ref y : ref (x > z ? ref z : ref x) : ref (y > z ? ref (z > x ? ref z : ref x) : ref y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Abs(int lhs)
        {
            return lhs < 0 ? -lhs : lhs;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MultColor32(ref Color32 lhs, Color32 rhs)
        {
            lhs.r = MULT_TABLE[lhs.r | (rhs.r << 8)];
            lhs.g = MULT_TABLE[lhs.g | (rhs.g << 8)];
            lhs.b = MULT_TABLE[lhs.b | (rhs.b << 8)];
            lhs.a = MULT_TABLE[lhs.a | (rhs.a << 8)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MultColorAlpha(ref Color32 lhs, byte alpha)
        {
            lhs.a = MULT_TABLE[lhs.a | (alpha << 8)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Overlaps(int min0, int max0, int min1, int max1)
        {
            return !(max0 < min1 || min0 > max1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte LoopUI8(float time)
        {
            return (byte)(Repeat01(time) * 255.0f);
        }   
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte LoopUI8(double time)
        {
            return (byte)(Repeat01(time) * 255.0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Repeat01(float time)
        {
            return (float)(time - Math.Floor(time));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Repeat01(double time)
        {
            return time - Math.Floor(time);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Lerp(float lhs, float rhs, float time)
        {
            return lhs + (rhs - lhs) * time;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Lerp(byte lhs, byte rhs, float time)
        {
            return (byte)(lhs + (rhs - lhs) * time);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Normalize(byte value)
        {
            return UI8_TO_FLOAT[value];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Normalize(sbyte value)
        {
            return I8_TO_FLOAT[value + 128];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 Lerp(Color32 lhs, Color32 rhs, float time)
        {
            return new Color32(  
                (byte)(lhs.r + (rhs.r - lhs.r) * time),
                (byte)(lhs.g + (rhs.g - lhs.g) * time),
                (byte)(lhs.b + (rhs.b - lhs.b) * time),
                (byte)(lhs.a + (rhs.a - lhs.a) * time)
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int NextDivByPowof2(int value, int power)
        {
            return (value & (power - 1)) != 0 ? (value + (power - 1)) & ~(power - 1) : value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint NextDivByPowof2(uint value, uint power)
        {
            return (value & (power - 1)) != 0 ? (value + (power - 1)) & ~(power - 1) : value;
        }
    }
}
