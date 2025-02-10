using System;
using System.Runtime.InteropServices;

namespace Joonaxii.Colors
{
    [StructLayout(LayoutKind.Explicit,Pack =1, Size=4)]
    public struct Color32 : IEquatable<Color32>
    {
        [FieldOffset(0)] public byte r;
        [FieldOffset(1)] public byte g;
        [FieldOffset(2)] public byte b;
        [FieldOffset(3)] public byte a;
        [FieldOffset(0)] private int _hash;

        public Color32(byte r, byte g, byte b, byte a) : this()
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public override bool Equals(object obj)
        {
            return obj is Color32 color && Equals(color);
        }

        public bool Equals(Color32 other)
        {
            return _hash == other._hash;
        }

        public override int GetHashCode()
        {
            return _hash;
        }

        public static bool operator ==(Color32 left, Color32 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Color32 left, Color32 right)
        {
            return !(left == right);
        }
    }
}
