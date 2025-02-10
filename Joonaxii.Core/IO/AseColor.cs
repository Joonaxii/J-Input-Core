using Joonaxii.Colors;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Joonaxii.IO
{
    public enum AseBlendMode : ushort
    {
        Normal = 0x00,
        Multiply = 0x01,
        Screen = 0x02,
        Overlay = 0x03,
        Darken = 0x04,
        Lighten = 0x05,
        ColorDodge = 0x06,
        ColorBurn = 0x07,
        HardLight = 0x08,
        SoftLight = 0x09,
        Difference = 0x0a,
        Exclusion = 0x0b,
        Hue = 0x0c,
        Saturation = 0x0d,
        Color = 0x0e,
        Luminosity = 0x0f,
        Addition = 0x10,
        Subtract = 0x11,
        Divide = 0x12,
    };

    // These blend functions are based on Aseprite's own source code https://github.com/aseprite/aseprite/blob/main/src/doc/blend_funcs.cpp
    // some changes have been made to make them function in C# and with my Color structs + math stuff. 
    public static class AseColor
    {
        public delegate Color32 BlendFunc(Color32 lhs, Color32 rhs, int opacity);
        public delegate void ParseScan(ReadOnlySpan<byte> scan, Span<Color32> pixels, PaletteRef<Color32> palette);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 Blend(this AseBlendMode mode, Color32 lhs, Color32 rhs, int opacity)
        {
            return mode switch
            {
                AseBlendMode.Normal => Normal(lhs, rhs, opacity),
                AseBlendMode.Multiply => Multiply(lhs, rhs, opacity),
                AseBlendMode.Screen => Screen(lhs, rhs, opacity),
                AseBlendMode.Overlay => Overlay(lhs, rhs, opacity),
                AseBlendMode.Darken => Darken(lhs, rhs, opacity),
                AseBlendMode.Lighten => Lighten(lhs, rhs, opacity),
                AseBlendMode.ColorDodge => ColorDodge(lhs, rhs, opacity),
                AseBlendMode.ColorBurn => ColorBurn(lhs, rhs, opacity),
                AseBlendMode.HardLight => HardLight(lhs, rhs, opacity),
                AseBlendMode.SoftLight => SoftLight(lhs, rhs, opacity),
                AseBlendMode.Difference => Difference(lhs, rhs, opacity),
                AseBlendMode.Exclusion => Exclusion(lhs, rhs, opacity),
                AseBlendMode.Hue => HSLHue(lhs, rhs, opacity),
                AseBlendMode.Saturation => HSLSat(lhs, rhs, opacity),
                AseBlendMode.Color => HSLCol(lhs, rhs, opacity),
                AseBlendMode.Luminosity => HSLLum(lhs, rhs, opacity),
                AseBlendMode.Addition => Addition(lhs, rhs, opacity),
                AseBlendMode.Subtract => Subtract(lhs, rhs, opacity),
                AseBlendMode.Divide => Divide(lhs, rhs, opacity),
                _ => lhs,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ParseScan GetScanParser(int bpp)
        {
            return bpp switch
            {
                16 => ParseScan16,
                32 => ParseScan32,
                _ => ParseScanIndexed,
            };
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ParseScan32(ReadOnlySpan<byte> scan, Span<Color32> pixels, PaletteRef<Color32> _)
        {
            MemoryMarshal.Cast<byte, Color32>(scan).CopyTo(pixels);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ParseScan16(ReadOnlySpan<byte> scan, Span<Color32> pixels, PaletteRef<Color32> _)
        {
            for (int i = 0, j = 0; i < pixels.Length; i++, j += 2) 
            {
                ref Color32 clr = ref pixels[i];
                clr.r = clr.g = clr.b = scan[j];
                clr.a = scan[j + 1];
            }
        }

        private static Color32 CLEAR = new Color32(0, 0, 0, 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ParseScanIndexed(ReadOnlySpan<byte> scan, Span<Color32> pixels, PaletteRef<Color32> palette)
        {
            for (int i = 0; i < pixels.Length; i++) 
            {
                ref Color32 clr = ref pixels[i];
                palette.GetColor(ref pixels[i], scan[i], in CLEAR);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BlendFunc GetBlendFunc(this AseBlendMode mode)
        {
            return mode switch
            {
                AseBlendMode.Normal => Normal,
                AseBlendMode.Multiply => Multiply,
                AseBlendMode.Screen => Screen,
                AseBlendMode.Overlay => Overlay,
                AseBlendMode.Darken => Darken,
                AseBlendMode.Lighten => Lighten,
                AseBlendMode.ColorDodge => ColorDodge,
                AseBlendMode.ColorBurn => ColorBurn,
                AseBlendMode.HardLight => HardLight,
                AseBlendMode.SoftLight => SoftLight,
                AseBlendMode.Difference => Difference,
                AseBlendMode.Exclusion => Exclusion,
                AseBlendMode.Hue => HSLHue,
                AseBlendMode.Saturation => HSLSat,
                AseBlendMode.Color => HSLCol,
                AseBlendMode.Luminosity => HSLLum,
                AseBlendMode.Addition => Addition,
                AseBlendMode.Subtract => Subtract,
                AseBlendMode.Divide => Divide,
                _ => NoBlend,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Blend(AseBlendMode mode, Span<Color32> bg, Span<Color32> fg, int opacity)
        => Blend(GetBlendFunc(mode), bg, fg, opacity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Blend(BlendFunc func, Span<Color32> bg, Span<Color32> fg, int opacity)
        {
            for (int i = 0; i < bg.Length; i++)
            {
                bg[i] = func.Invoke(bg[i], fg[i], opacity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Color32 NoBlend(Color32 lhs, Color32 rhs, int opacity) => lhs;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 Normal(Color32 b, Color32 s, int opacity)
        {
            byte aA = FastMath.MultUI8(s.a, opacity);
            if (b.a == 0)
            {
                s.a = aA;
                return s;
            }
            else if (aA <= 0) { return b; }

            int rA = aA + b.a - (int)FastMath.MultUI8(aA, b.a);
            int rR = b.r + (s.r - b.r) * aA / rA;
            int rG = b.g + (s.g - b.g) * aA / rA;
            int rB = b.b + (s.b - b.b) * aA / rA;
            return new Color32((byte)rR, (byte)rG, (byte)rB, (byte)rA);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 Multiply(Color32 lhs, Color32 rhs, int opacity)
        {
            rhs.r = FastMath.MultUI8(lhs.r, rhs.r);
            rhs.g = FastMath.MultUI8(lhs.g, rhs.g);
            rhs.b = FastMath.MultUI8(lhs.b, rhs.b);
            return Normal(lhs, rhs, opacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 Screen(Color32 lhs, Color32 rhs, int opacity)
        {
            rhs.r = (byte)(BL_Scrn(lhs.r, rhs.r));
            rhs.g = (byte)(BL_Scrn(lhs.g, rhs.g));
            rhs.b = (byte)(BL_Scrn(lhs.b, rhs.b));
            return Normal(lhs, rhs, opacity);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 Overlay(Color32 lhs, Color32 rhs, int opacity)
        {
            rhs.r = (byte)(BL_OLay(lhs.r, rhs.r));
            rhs.g = (byte)(BL_OLay(lhs.g, rhs.g));
            rhs.b = (byte)(BL_OLay(lhs.b, rhs.b));
            return Normal(lhs, rhs, opacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 Darken(Color32 lhs, Color32 rhs, int opacity)
        {
            rhs.r = FastMath.Min(lhs.r, rhs.r);
            rhs.g = FastMath.Min(lhs.g, rhs.g);
            rhs.b = FastMath.Min(lhs.b, rhs.b);
            return Normal(lhs, rhs, opacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 Lighten(Color32 lhs, Color32 rhs, int opacity)
        {
            rhs.r = FastMath.Max(lhs.r, rhs.r);
            rhs.g = FastMath.Max(lhs.g, rhs.g);
            rhs.b = FastMath.Max(lhs.b, rhs.b);
            return Normal(lhs, rhs, opacity);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 HardLight(Color32 lhs, Color32 rhs, int opacity)
        {
            rhs.r = (byte)(BL_HLght(lhs.r, rhs.r));
            rhs.g = (byte)(BL_HLght(lhs.g, rhs.g));
            rhs.b = (byte)(BL_HLght(lhs.b, rhs.b));
            return Normal(lhs, rhs, opacity);
        }
            
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 SoftLight(Color32 lhs, Color32 rhs, int opacity)
        {
            rhs.r = (byte)(BL_SLght(lhs.r, rhs.r));
            rhs.g = (byte)(BL_SLght(lhs.g, rhs.g));
            rhs.b = (byte)(BL_SLght(lhs.b, rhs.b));
            return Normal(lhs, rhs, opacity);
        }
            
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 Difference(Color32 lhs, Color32 rhs, int opacity)
        {
            rhs.r = (byte)FastMath.Abs(lhs.r - rhs.r);
            rhs.g = (byte)FastMath.Abs(lhs.g - rhs.g);
            rhs.b = (byte)FastMath.Abs(lhs.b - rhs.b);
            return Normal(lhs, rhs, opacity);
        }
            
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 Exclusion(Color32 lhs, Color32 rhs, int opacity)
        {
            rhs.r = (byte)(BL_Excl(lhs.r, rhs.r));
            rhs.g = (byte)(BL_Excl(lhs.g, rhs.g));
            rhs.b = (byte)(BL_Excl(lhs.b, rhs.b));
            return Normal(lhs, rhs, opacity);
        }
            
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 Addition(Color32 lhs, Color32 rhs, int opacity)
        {
            rhs.r = (byte)(FastMath.Min(lhs.r + rhs.r, 0xFF));
            rhs.g = (byte)(FastMath.Min(lhs.g + rhs.g, 0xFF));
            rhs.b = (byte)(FastMath.Min(lhs.b + rhs.b, 0xFF));
            return Normal(lhs, rhs, opacity);
        }
            
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 Subtract(Color32 lhs, Color32 rhs, int opacity)
        {
            rhs.r = (byte)(FastMath.Max(lhs.r - rhs.r, 0x00));
            rhs.g = (byte)(FastMath.Max(lhs.g - rhs.g, 0x00));
            rhs.b = (byte)(FastMath.Max(lhs.b - rhs.b, 0x00));
            return Normal(lhs, rhs, opacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 Divide(Color32 lhs, Color32 rhs, int opacity)
        {
            rhs.r = FastMath.DivUI8(lhs.r, lhs.r);
            rhs.g = FastMath.DivUI8(lhs.g, lhs.g);
            rhs.b = FastMath.DivUI8(lhs.b, lhs.b);
            return Normal(lhs, rhs, opacity);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 ColorDodge(Color32 lhs, Color32 rhs, int opacity)
        {
            rhs.r = BL_Dodg(lhs.r, lhs.r);
            rhs.g = BL_Dodg(lhs.g, lhs.g);
            rhs.b = BL_Dodg(lhs.b, lhs.b);
            return Normal(lhs, rhs, opacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 ColorBurn(Color32 lhs, Color32 rhs, int opacity)
        {
            rhs.r = BL_Burn(lhs.r, lhs.r);
            rhs.g = BL_Burn(lhs.g, lhs.g);
            rhs.b = BL_Burn(lhs.b, lhs.b);
            return Normal(lhs, rhs, opacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 HSLHue(Color32 lhs, Color32 rhs, int opacity)
        {
            double r = rhs.r / 255.0;
            double g = rhs.g / 255.0;
            double b = rhs.b / 255.0;
            double s = Sat(r, g, b);
            double l = Lum(r, g, b);

            r = rhs.r / 255.0;
            g = rhs.g / 255.0;
            b = rhs.b / 255.0;

            SetSat(ref r, ref g, ref b, s);
            SetLum(ref r, ref g, ref b, l);

            rhs.r = (byte)(FastMath.Min(r * 255.0, 255.0));
            rhs.g = (byte)(FastMath.Min(g * 255.0, 255.0));
            rhs.b = (byte)(FastMath.Min(b * 255.0, 255.0));

            return Normal(lhs, rhs, opacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 HSLSat(Color32 lhs, Color32 rhs, int opacity)
        {
            double r = rhs.r / 255.0;
            double g = rhs.g / 255.0;
            double b = rhs.b / 255.0;
            double s = Sat(r, g, b);

            r = lhs.r / 255.0;
            g = lhs.g / 255.0;
            b = lhs.b / 255.0;
            double l = Lum(r, g, b);

            SetSat(ref r, ref g, ref b, s);
            SetLum(ref r, ref g, ref b, l);

            rhs.r = (byte)(FastMath.Min(r * 255.0, 255.0));
            rhs.g = (byte)(FastMath.Min(g * 255.0, 255.0));
            rhs.b = (byte)(FastMath.Min(b * 255.0, 255.0));

            return Normal(lhs, rhs, opacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 HSLCol(Color32 lhs, Color32 rhs, int opacity)
        {
            double r = lhs.r / 255.0;
            double g = lhs.g / 255.0;
            double b = lhs.b / 255.0;
            double l = Lum(r, g, b);

            r = rhs.r / 255.0;
            g = rhs.g / 255.0;
            b = rhs.b / 255.0;

            SetLum(ref r, ref g, ref b, l);

            rhs.r = (byte)(FastMath.Min(r * 255.0, 255.0));
            rhs.g = (byte)(FastMath.Min(g * 255.0, 255.0));
            rhs.b = (byte)(FastMath.Min(b * 255.0, 255.0));

            return Normal(lhs, rhs, opacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color32 HSLLum(Color32 lhs, Color32 rhs, int opacity)
        {
            double r = rhs.r / 255.0;
            double g = rhs.g / 255.0;
            double b = rhs.b / 255.0;
            double l = Lum(r, g, b);

            r = lhs.r / 255.0;
            g = lhs.g / 255.0;
            b = lhs.b / 255.0;

            SetLum(ref r, ref g, ref b, l);

            rhs.r = (byte)(FastMath.Min(r * 255.0, 255.0));
            rhs.g = (byte)(FastMath.Min(g * 255.0, 255.0));
            rhs.b = (byte)(FastMath.Min(b * 255.0, 255.0));

            return Normal(lhs, rhs, opacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double Lum(double r, double g, double b)
        {
            return 0.3 * r + 0.49 * g + 0.11 * b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double Sat(double r, double g, double b)
        {
            return FastMath.Max(r, FastMath.Max(g, b)) - Math.Min(r, Math.Min(g, b));
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Clip(ref double r, ref double g, ref double b)
        {
            double l = Lum(r, g, b);

            double n = Math.Min(r, Math.Min(g, b));
            double x = Math.Max(r, Math.Max(g, b));

            if (n < 0)
            {
                r = l + (((r - l) * l) / (l - n));
                g = l + (((g - l) * l) / (l - n));
                b = l + (((b - l) * l) / (l - n));
            }

            if (x > 1)
            {
                r = l + (((r - l) * (1.0 - l)) / (x - l));
                g = l + (((g - l) * (1.0 - l)) / (x - l));
                b = l + (((b - l) * (1.0 - l)) / (x - l));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetLum(ref double r, ref double g, ref double b, double l) 
        {
            double d = l - Lum(r, g, b);
            r += d;
            g += d;
            b += d;
            Clip(ref r, ref g, ref b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetSat(ref double r, ref double g, ref double b, double s)
        {
            ref double min = ref FastMath.Min(ref r, ref FastMath.Min(ref g, ref b));
            ref double mid = ref FastMath.Mid(ref r, ref g, ref b);
            ref double max = ref FastMath.Max(ref r, ref FastMath.Max(ref g, ref b));

            if (max > min)
            {
                mid = ((mid - min) * s) / (max - min);
                max = s;
            }
            else
            {
                mid = max = 0;
            }
            min = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte BL_Dodg(int b, int s)
        {
            if (b == 0)
            {
                return 0;
            }

            s = (255 - s);
            if (b >= s)
            {
                return 255;
            }
            return FastMath.DivUI8(b, s);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte BL_Burn(int b, int s)
        {
            if (b == 255)
            {
                return 255;
            }

            b = (255 - b);
            if (b >= s)
            {
                return 0;
            }
            return (byte)(255 - FastMath.DivUI8(b, s));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int BL_Scrn(int b, int s)
        {
            return b - s - FastMath.MultUI8(b, s);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int BL_HLght(int b, int s)
        {
            return s < 128 ? FastMath.MultUI8(b, s << 1) : BL_Scrn(b, (s << 1) - 255);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int BL_SLght(int lhs, int rhs)
        {
            double b = lhs / 255.0;
            double s = rhs / 255.0;
            double r, d;

            if (b <= 0.25)
            {
                d = ((16 * b - 12) * b + 4) * b;
            }
            else
            {
                d = Math.Sqrt(b);
            }

            if (s <= 0.5)
            {
                r = b - (1.0 - 2.0 * s) * b * (1.0 - b);
            }
            else
            {
                r = b + (2.0 * s - 1.0) * (d - b);
            }
            return (int)(r * 255 + 0.5);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int BL_OLay(int b, int s)
        {
            return BL_HLght(s, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int BL_Excl(int b, int s)
        {
            return (b + s - 2 * FastMath.MultUI8(b, s));
        }
    }

    public ref struct PaletteRef<T>
    {
        public ReadOnlySpan<T> palette;
        public int transparent;

        public PaletteRef(ReadOnlySpan<T> palette, int transparent)
        {
            this.palette = palette;
            this.transparent = transparent;
        }

        public readonly void GetColor(ref T value, int index, in T clear)
        {
            value = ((index == transparent || index < 0 || index >= palette.Length) ? clear : palette[index]);
        }
    }
}
