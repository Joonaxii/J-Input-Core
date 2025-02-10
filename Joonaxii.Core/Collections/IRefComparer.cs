using System;
using System.Collections.Generic;

namespace Joonaxii.Collections
{
    public interface IRefComparer<T> : IComparer<T>
    {
        int CompareTo(in T lhs, in T rhs);
    }

    public interface IRefComparer<T, U> : IRefComparer<T>
    {
        int CompareTo(in T lhs, in U rhs);
    }

    public class RefComparerWrapper<T> : IRefComparer<T> where T : IComparable<T>
    {
        public static RefComparerWrapper<T> Default { get; } = new RefComparerWrapper<T>();

        public int Compare(T x, T y)
        {
            return x.CompareTo(y);
        }

        public int CompareTo(in T lhs, in T rhs)
        {
            return lhs.CompareTo(rhs);
        }
    }

    public class RefComparerWrapper<T, U> : IRefComparer<T, U> where T : IComparable<T>, IComparable<U>
    {
        public static RefComparerWrapper<T, U> Default { get; } = new RefComparerWrapper<T, U>();

        public int Compare(T x, T y)
        {
            return x.CompareTo(y);
        }

        public int CompareTo(in T lhs, in T rhs)
        {
            return lhs.CompareTo(lhs);
        }

        public int CompareTo(in T lhs, in U rhs)
        {
            return lhs.CompareTo(lhs);
        }
    }
}