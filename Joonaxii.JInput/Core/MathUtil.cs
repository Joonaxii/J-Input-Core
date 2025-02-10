using System.Runtime.CompilerServices;

namespace Joonaxii.JInput
{
    public static class MathUtil
    {
        public static float Normalize(this short value) => value < 0 ? (value / (-(float)short.MinValue)) : (value / (float)short.MaxValue);
        public static float Normalize(this ushort value) => value / (float)ushort.MaxValue;
        public static float Normalize(this byte value) => value / (float)byte.MaxValue;
    }
}
