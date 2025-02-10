using Joonaxii.Collections;
using System;

namespace Joonaxii
{
    public class OrderedLUT<TKey, TValue> : OrderedList<Pair<TKey, TValue>, TKey>
    {
        public OrderedLUT(IRefComparer<TKey> comparer) : base(8, new PairComparer<TKey, TValue>(comparer)) { }
        public OrderedLUT(int capacity, IRefComparer<TKey> comparer) : base(capacity, new PairComparer<TKey, TValue>(comparer)) { }

        public ref TValue this[TKey key]
        {
            get
            {
                int idx = this.IndexOf(key);
                if(idx < 0)
                {
                    this.Add(new Pair<TKey, TValue>() { key = key, value = default }, out idx);
                }
                return ref _buffer[idx].value;
            }
        }

        public ref TValue GetByIndex(int index)
        {
            return ref _buffer[index].value;
        }

        public ref TValue GetByKey(in TKey key)
        {
            int idx = IndexOf(in key);
            return ref _buffer[idx].value;
        }

        public bool TryAdd(in TKey key, in TValue value) => TryAdd(in key, in value, out int _);
        public bool TryAdd(in TKey key, in TValue value, out int index)
        {
            Pair<TKey, TValue> pair = new Pair<TKey, TValue>()
            {
                key = key,
                value = value,
            };
            return this.Add(in pair, out index);
        }

        public void AddOrUpdate(in TKey key, in TValue value) => TryAdd(in key, in value, out int _);
        public void AddOrUpdate(in TKey key, in TValue value, out int index)
        {
            if(!this.Add(new Pair<TKey, TValue>() { key = key, value = value }, out index))
            {
                _buffer[index].value = value;
            }
        }

        public bool TryGetValue(in TKey key, out TValue value)
        {
            int idx = this.IndexOf(in key);
            if(idx < 0)
            {
                value = default;
                return false;
            }
            value = _buffer[idx].value;
            return true;
        }

        public bool TryGetIndex(in TKey key, out int index)
        {
            index = this.IndexOf(in key);
            return index > -1;
        }
    }

    [Serializable]
    public struct Pair<TKey, TValue>
    {
        public TKey key;
        public TValue value;
    }

    public class PairComparer<TKey, TValue> : IRefComparer<Pair<TKey, TValue>, TKey>
    {
        private IRefComparer<TKey> _keyComparer;

        public PairComparer(IRefComparer<TKey> keyComparer)
        {
            _keyComparer = keyComparer;
        }

        public int Compare(Pair<TKey, TValue> lhs, Pair<TKey, TValue> rhs)
        {
            return _keyComparer.CompareTo(in lhs.key, in rhs.key);
        }

        public int CompareTo(in Pair<TKey, TValue> lhs, in TKey rhs)
        {
            return _keyComparer.CompareTo(in lhs.key, in rhs);
        }

        public int CompareTo(in Pair<TKey, TValue> lhs, in Pair<TKey, TValue> rhs)
        {
            return _keyComparer.CompareTo(in lhs.key, in rhs.key);
        }
    }
}
