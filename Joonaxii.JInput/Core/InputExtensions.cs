using System;
using System.Collections.Generic;

namespace Joonaxii.JInput
{
    public static unsafe class InputExtensions
    {
        public static int DeviceToIndex(DeviceIndex device)
        {
            int index = *(byte*)&device;
            return index.IsPowerOf2() ? FastMath.Log2(index) : -1;
        }

        public static int BinarySearch<T>(this IList<T> list, T value) where T : IComparable => BinarySearch(list, value, 0, list.Count);
        public static int BinarySearch<T>(this IList<T> list, T value, int start, int count) where T : IComparable
        {
            int min = start;
            int max = start + count - 1;

            while(min <= max)
            {
                int mid = (min + max) >> 1;
                var v = list[mid];

                switch (value.CompareTo(v))
                {
                    case 0: return mid;
                    case -1:
                        max = mid - 1;
                        continue;
                    default:
                        min = mid + 1;
                        continue;
                }
            }
            return -1;
        }
    }
}
