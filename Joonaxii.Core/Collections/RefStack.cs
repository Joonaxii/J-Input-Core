using Joonaxii.Utilities.Collections;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Joonaxii.Collections
{
    public class RefStack<T> : IRefStack<T>
    {
        public int Count => _count;
        public bool IsEmpty => _count <= 0;

        public T[] Buffer => _buffer;

        bool ICollection<T>.IsReadOnly => false;

        public int Capacity => _buffer.Length;

        public ref T this[int i]
        {
            get => ref _buffer[i];
        }

        protected T[] _buffer;
        protected int _count;

        public RefStack() : this(8) { }
        public RefStack(int capacity)
        {
            Reserve(capacity, false);
        }

        public Span<T> AsSpan()
        {
            return this.AsSpan(0, _count);
        }

        public Span<T> AsSpan(int start)
        {
            return this.AsSpan(start, _count - start);
        }

        public Span<T> AsSpan(int start, int length)
        {
            return _buffer.AsSpan(start, length);
        }
 
        public MemorySegment<T> AsSegment()
        {
            return this.AsSegment(0, _count);
        }

        public MemorySegment<T> AsSegment(int start)
        {
            return this.AsSegment(start, _count - start);
        }

        public MemorySegment<T> AsSegment(int start, int length)
        {
            return new MemorySegment<T>(_buffer, start, length);
        }
        
        public Memory<T> AsMemory()
        {
            return this.AsMemory(0, _count);
        }

        public Memory<T> AsMemory(int start)
        {
            return this.AsMemory(start, _count - start);
        }

        public Memory<T> AsMemory(int start, int length)
        {
            return _buffer.AsMemory(start, length);
        }

        public Span<T> Push(ReadOnlySpan<T> values)
        {
            Reserve(_count + values.Length, true);
            var region = _buffer.AsSpan(_count, values.Length);
            values.CopyTo(region);
            _count += values.Length;
            return region;
        }

        public ref T Push(in T value)
        {
            Reserve(_count + 1, true);
            _buffer[_count] = value;
            return ref _buffer[_count++];
        }

        public ref T Push()
        {
            Reserve(_count + 1, true);
            _buffer[_count] = default;
            return ref _buffer[_count++];
        }

        public ref T Top()
        {
            return ref _buffer[_count - 1];
        }

        public void Pop()
        {
            if(_count < 1) { return; }
            --_count;
        }

        public bool RemoveAt(int index)
        {
            if(index < 0 || index >= _count) { return false; }
            Array.Copy(_buffer, index + 1, _buffer, index, _count - index - 1);
            --_count;
            return true;
        }

        public void Clear() => Clear(false);
        public void Clear(bool trim)
        {
            _count = 0;
            if (trim)
            {
                Trim();
            }
        }
        
        public void Trim()
        {
            if(_buffer.Length > _count)
            {
                Array.Resize(ref _buffer, _count);
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            Array.Copy(_buffer, 0, array, arrayIndex, Math.Min(_count, array.Length - arrayIndex));
        }

        public Span<T> Resize(int count, bool scale, bool trim = false)
        {
            count = count < 0 ? 0 : count;

            Reserve(count, scale);
            _count = count;
            if (trim)
            {
                Trim();
            }
            return _count < 1 ? default : _buffer.AsSpan(0, _count);
        }

        public int IndexOf(in T value, IRefEqualityComparer<T> comparer)
        {
            for (int i = 0; i < _count; i++)
            {
                if(comparer.AreEqual(in value, in _buffer[i]))
                {
                    return i;
                }
            }
            return -1;
        }

        public int IndexOf<U>(in U value, IRefEqualityComparer<T, U> comparer)
        {
            for (int i = 0; i < _count; i++)
            {
                if (comparer.AreEqual(in _buffer[i], in value))
                {
                    return i;
                }
            }
            return -1;
        }

        public void Reserve(int count, bool scale)
        {
            if (_buffer != null && count <= _buffer.Length) { return; }

            int cap = _buffer?.Length ?? 0;
            if (scale)
            {
                while (cap < count)
                {
                    cap += Math.Max(cap >> 1, 1);
                }
            }
            else
            {
                cap = count;
            }
            Array.Resize(ref _buffer, cap);
        }

        void ICollection<T>.Add(T item)
        {
            Push(in item);
        }

        bool ICollection<T>.Contains(T item) => false;
        bool ICollection<T>.Remove(T item) => false;

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
}
