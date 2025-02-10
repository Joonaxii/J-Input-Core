using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Joonaxii.Collections
{
    [Serializable]
    public class OrderedList<T> : IList<T>
    {
        public delegate int CompareT(in T lhs, in T rhs);
        public delegate bool PredicateT(in T value);

        public int Count => _count;
        public int Capacity => _buffer.Length;
        bool ICollection<T>.IsReadOnly => false;

        /// <summary>
        /// NOTE: Should only be used for reading, this returns a ref to avoid needless copying of larger structs etc. Downside being that you can write into it as well which is not adviced unless you know what you're doing!
        /// </summary>
        public ref T this[int i]
        {
            get => ref _buffer[i];
        }
        T IList<T>.this[int index]
        {
            get => this[index];
            set => throw new NotSupportedException();
        }

        protected T[] _buffer;
        protected int _count;
        protected IRefComparer<T> _comparer;

        public OrderedList(IRefComparer<T> comparer) : this(8, comparer) { }
        public OrderedList(int capacity, IRefComparer<T> comparer)
        {
            _comparer = comparer;
            Reserve(capacity);
        }

        public void Clear() => Clear(false);
        public void Clear(bool trim)
        {
            Array.Clear(_buffer, 0, _count);
            _count = 0;

            if (trim)
            {
                Trim();
            }
        }

        public void Trim()
        {
            if (_buffer.Length > _count)
            {
                Array.Resize(ref _buffer, _count);
            }
        }

        public bool Add(in T item)
            => Add(in item, out _);
        public bool Add(in T item, out int index)
        {
            int ind = Nearest(in item);
            if (ind < _count && _comparer.CompareTo(in _buffer[ind], in item) == 0)
            {
                index = ind;
                return false;
            }

            CheckForExpansion(_count + 1);
            if (ind < _count)
            {
                Array.Copy(_buffer, ind, _buffer, ind + 1, _count - ind);
            }
            _buffer[ind] = item;
            index = ind;
            _count++;
            return true;
        }

        public bool Add(in T item, CompareT predicate) => Add(in item, out _, predicate);
        public bool Add(in T item, out int index, CompareT predicate)
        {
            int ind = Nearest(in item, predicate);
            if (ind < _count && predicate.Invoke(in _buffer[ind], in item) == 0)
            {
                index = ind;
                return false;
            }

            CheckForExpansion(_count + 1);
            if (ind < _count)
            {
                Array.Copy(_buffer, ind, _buffer, ind + 1, _count - ind);
            }
            _buffer[ind] = item;
            index = ind;
            _count++;
            return true;
        }

        public int IndexOf(in T value)
        {
            int l = 0;
            int r = _count - 1;

            while (l <= r)
            {
                int m = l + (r - l) / 2;
                int res = _comparer.CompareTo(in _buffer[m], in value);

                if (res == 0)
                {
                    return m;
                }
                else if (res < 0)
                {
                    l = m + 1;
                }
                else
                {
                    r = m - 1;
                }
            }
            return -1;
        }
        public int IndexOf(in T value, CompareT predicate)
        {
            int l = 0;
            int r = _count - 1;

            while (l <= r)
            {
                int m = l + (r - l) / 2;
                int res = predicate.Invoke(in _buffer[m], in value);

                if (res == 0)
                {
                    return m;
                }
                else if (res < 0)
                {
                    l = m + 1;
                }
                else
                {
                    r = m - 1;
                }
            }
            return -1;
        }

        public int Nearest(in T value)
        {
            int l = 0;
            int r = _count - 1;
            int m;
            if (_count <= 0)
            {
                return 0;
            }

            while (l <= r)
            {
                m = l + (r - l) / 2;
                int res = _comparer.CompareTo(in _buffer[m], in value);
                if (res == 0)
                {
                    return m;
                }
                else if (res < 0)
                {
                    l = m + 1;
                }
                else
                {
                    r = m - 1;
                }
            }
            return l;
        }
        public int Nearest(in T value, CompareT predicate)
        {
            int l = 0;
            int r = _count - 1;
            int m;
            if (_count <= 0)
            {
                return 0;
            }

            while (l <= r)
            {
                m = l + (r - l) / 2;
                int res = predicate.Invoke(in _buffer[m], in value);
                if (res == 0)
                {
                    return m;
                }
                else if (res < 0)
                {
                    l = m + 1;
                }
                else
                {
                    r = m - 1;
                }
            }
            return l;
        }

        public void Update(int index)
        {
            if (_count < 2 || index < 0 || index >= _count) { return; }

            int startIndex = -1;
            int idx;
            while (true)
            {
                if (startIndex == index) { break; }
                startIndex = startIndex < 0 ? index : startIndex;

                int ret;
                ref var cur = ref _buffer[index];

                idx = index - 1;
                if (startIndex != idx && idx > -1)
                {
                    ref var rhs = ref _buffer[idx];
                    ret = _comparer.CompareTo(in cur, in rhs);
                    if (ret < 0)
                    {
                        Swap(ref cur, ref rhs);
                        index--;
                        continue;
                    }
                }

                idx = index + 1;
                if (startIndex != idx && idx < _count)
                {
                    ref var rhs = ref _buffer[idx];
                    ret = _comparer.CompareTo(in cur, in rhs);
                    if (ret > 0)
                    {
                        Swap(ref cur, ref rhs);
                        index++;
                        continue;
                    }
                }
                break;
            }
        }
        public void Update(int index, CompareT predicate)
        {
            if (_count < 2 || index < 0 || index >= _count) { return; }

            int startIndex = -1;
            int idx;
            while (true)
            {
                if (startIndex == index) { break; }
                startIndex = startIndex < 0 ? index : startIndex;

                int ret;
                ref var cur = ref _buffer[index];

                idx = index - 1;
                if (startIndex != idx && idx > -1)
                {
                    ref var rhs = ref _buffer[idx];
                    ret = predicate.Invoke(in cur, in rhs);
                    if (ret < 0)
                    {
                        Swap(ref cur, ref rhs);
                        index--;
                        continue;
                    }
                }

                idx = index + 1;
                if (startIndex != idx && idx < _count)
                {
                    ref var rhs = ref _buffer[idx];
                    ret = predicate.Invoke(in cur, in rhs);
                    if (ret > 0)
                    {
                        Swap(ref cur, ref rhs);
                        index++;
                        continue;
                    }
                }
                break;
            }
        }

        public bool Contains(in T item) => IndexOf(in item) > -1;
        public bool Contains(in T item, CompareT predicate) => IndexOf(in item, predicate) > -1;
        public void CopyTo(T[] array, int arrayIndex)
        {
            int len = Math.Min(array.Length - arrayIndex, _count);
            Array.Copy(_buffer, 0, array, arrayIndex, len);
        }

        public void CloneTo(OrderedList<T> other)
        {
            other.Clear();
            other.Reserve(_count);

            Array.Copy(_buffer, 0, other._buffer, 0, _count);
            other._count = _count;
        }

        public bool Remove(in T item)
            => RemoveAt(IndexOf(in item));
        public bool Remove(in T item, CompareT predicate)
            => RemoveAt(IndexOf(in item, predicate));
        public bool RemoveAt(int ind)
        {
            if (ind < 0 || ind >= _count) { return false; }

            _count--;
            if (ind < _count)
            {
                Array.Copy(_buffer, ind + 1, _buffer, ind, _count - ind);
            }
            return true;
        }

        public bool RemoveIf(PredicateT predicate, bool stopAtFirst = false)
        {
            bool removed = false;
            for (int i = Count - 1; i >= 0; i--)
            {
                if (predicate.Invoke(in _buffer[i]))
                {
                    removed |= RemoveAt(i);
                    if (stopAtFirst)
                    {
                        break;
                    }
                }
            }
            return removed;
        }

        public void Reserve(int count)
        {
            if (_buffer == null)
            {
                _buffer = new T[count];
                return;
            }

            if (count > _buffer.Length)
            {
                Array.Resize(ref _buffer, count);
            }
        }

        public bool TryGet(in T key, out T value) => TryGet(in key, out value);
        public bool TryGet(in T key, out T value, CompareT predicate)
        {
            int ind = IndexOf(in key, predicate);
            if (ind < 0)
            {
                value = default;
                return false;
            }

            value = _buffer[ind];
            return true;
        }
        public int IndexOf(T item) => IndexOf(in item);
        public bool Contains(T item) => Contains(in item);
        public bool Remove(T item) => Remove(in item);

        public void Sort()
        {
            Array.Sort(_buffer, 0, _count, _comparer);
        }
        public void Sort(IComparer<T> comparer)
        {
            Array.Sort(_buffer, 0, _count, comparer);
        }

        private void CheckForExpansion(int count)
        {
            int cap = _buffer.Length;
            while (count > cap)
            {
                cap += Math.Max(cap >> 1, 1);
            }
            Reserve(cap);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Swap(ref T lhs, ref T rhs)
        {
            (rhs, lhs) = (lhs, rhs);
        }

        void ICollection<T>.Add(T item) { }
        void IList<T>.Insert(int index, T item) { }
        void IList<T>.RemoveAt(int index) { }
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < _count; i++)
            {
                yield return _buffer[i];
            }
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    [Serializable]
    public class OrderedList<T, U> : OrderedList<T>
    {
        public delegate int CompareU(in T lhs, in U rhs);
        protected IRefComparer<T, U> _comparerU;

        public OrderedList(IRefComparer<T, U> comparer) : base(8, comparer)
        {
            _comparerU = comparer;
        }
        public OrderedList(int capacity, IRefComparer<T, U> comparer) : base(capacity, comparer)
        {
            _comparerU = comparer;
        }

        public int IndexOf(in U value)
        {
            int l = 0;
            int r = _count - 1;

            while (l <= r)
            {
                int m = l + (r - l) / 2;
                int res = _comparerU.CompareTo(in _buffer[m], in value);

                if (res == 0)
                {
                    return m;
                }
                else if (res < 0)
                {
                    l = m + 1;
                }
                else
                {
                    r = m - 1;
                }
            }
            return -1;
        }

        public int IndexOf(in U value, CompareU predicate)
        {
            int l = 0;
            int r = _count - 1;

            while (l <= r)
            {
                int m = l + (r - l) / 2;
                int res = predicate.Invoke(in _buffer[m], in value);

                if (res == 0)
                {
                    return m;
                }
                else if (res < 0)
                {
                    l = m + 1;
                }
                else
                {
                    r = m - 1;
                }
            }
            return -1;
        }

        public bool Contains(in U item) => IndexOf(in item) > -1;
        public bool Contains(in U item, CompareU predicate) => IndexOf(in item, predicate) > -1;

        public T SelectBy(in U item)
        {
            int ind = IndexOf(in item);
            return ind > -1 ? _buffer[ind] : default;
        }

        public T SelectBy(in U item, CompareU predicate)
        {
            int ind = IndexOf(in item, predicate);
            return ind > -1 ? _buffer[ind] : default;
        }
        public bool Remove(in U value)
        {
            return RemoveAt(IndexOf(in value));
        }
        public bool Remove(in U value, CompareU predicate)
        {
            return RemoveAt(IndexOf(in value, predicate));
        }

        public bool TryGet(in U key, out T value)
        {
            int ind = IndexOf(in key);
            if (ind < 0)
            {
                value = default;
                return false;
            }
            value = _buffer[ind];
            return true;
        }

        public bool TryGet(in U key, out T value, CompareU predicate)
        {
            int ind = IndexOf(in key, predicate);
            if (ind < 0)
            {
                value = default;
                return false;
            }
            value = _buffer[ind];
            return true;
        }
    }

    public class RefComparer<T> : IRefComparer<T> where T : IRefComparable<T>
    {
        public static RefComparer<T> Default { get; } = new RefComparer<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(T x, T y)
        {
            return x.CompareTo(in y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(in T lhs, in T rhs)
        {
            return lhs.CompareTo(in rhs);
        }
    }

    public class RefComparer<T, U> : IRefComparer<T, U> where T : IRefComparable<T>, IRefComparable<U>
    {
        public static RefComparer<T, U> Default { get; } = new RefComparer<T, U>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(T x, T y)
        {
            return x.CompareTo(in y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(in T lhs, in T rhs)
        {
            return lhs.CompareTo(in rhs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(in T lhs, in U rhs)
        {
            return lhs.CompareTo(in rhs);
        }
    }

    public class ValueComparer<T> : IRefComparer<T> where T : IComparable<T>
    {
        public static ValueComparer<T> Default { get; } = new ValueComparer<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(T x, T y)
        {
            return x.CompareTo(y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(in T lhs, in T rhs)
        {
            return lhs.CompareTo(rhs);
        }
    }

    public class ValueComparer<T, U> : IRefComparer<T, U> where T : IComparable<T>, IComparable<U>
    {
        public static ValueComparer<T, U> Default { get; } = new ValueComparer<T, U>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(T x, T y)
        {
            return x.CompareTo(y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(in T lhs, in T rhs)
        {
            return lhs.CompareTo(rhs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(in T lhs, in U rhs)
        {
            return lhs.CompareTo(rhs);
        }
    }
}
