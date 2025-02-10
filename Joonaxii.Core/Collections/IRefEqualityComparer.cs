using System;

namespace Joonaxii.Collections
{
    public interface IRefEqualityComparer<T>
    {
        bool AreEqual(in T x, in T y);
    }

    public interface IRefEqualityComparer<T, U>
    {
        bool AreEqual(in T x, in U y);
    }

    public class RefEqualityComparer<T> : IRefEqualityComparer<T> where T : IRefEquatable<T>
    {
        public static RefEqualityComparer<T> Default { get; } = new RefEqualityComparer<T>();
        public bool AreEqual(in T x, in T y) => x.Equals(in y);
    }

    public class RefEqualityComparer<T, U> : IRefEqualityComparer<T, U> where T : IRefEquatable<T>, IRefEquatable<U>
    {
        public static RefEqualityComparer<T, U> Default { get; } = new RefEqualityComparer<T, U>();
        public bool AreEqual(in T x, in T y) => x.Equals(in y);
        public bool AreEqual(in T x, in U y) => x.Equals(in y);
    }

    public class ValueEqualityComparer<T> : IRefEqualityComparer<T> where T : IEquatable<T>
    {
        public static ValueEqualityComparer<T> Default { get; } = new ValueEqualityComparer<T>();
        public bool AreEqual(in T x, in T y) => x.Equals(y);
    }

    public class ValueEqualityComparer<T, U> : IRefEqualityComparer<T, U> where T : IEquatable<T>, IEquatable<U>
    {
        public static ValueEqualityComparer<T, U> Default { get; } = new ValueEqualityComparer<T, U>();
        public bool AreEqual(in T x, in T y) => x.Equals(y);
        public bool AreEqual(in T x, in U y) => x.Equals(y);
    }
}
