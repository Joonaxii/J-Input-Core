using System;

namespace Joonaxii.Utilities.Collections
{
    public struct MemorySegment<T>
    {
        public Span<T> Span => _array.AsSpan(_index, _length);
        public int Length => _length;

        internal int Index => _index;
        internal T[] Array => _array;

        private T[] _array;
        private int _index;
        private int _length;

        public MemorySegment(T[] array) : this(array, 0, array.Length){ }

        public MemorySegment(in ArraySegment<T> other) : this(other.Array, other.Offset, other.Count) { }
        public MemorySegment(T[] array, int start, int length) : this()
        {
            _array = array;
            _index = start;
            _length = length;
        }
        public ref T this[int i] => ref _array[i];
        public static implicit operator MemorySegment<T>(ArraySegment<T> other) => new MemorySegment<T>(in other);
    }

    public struct ReadOnlyMemorySegment<T>
    {
        public ReadOnlySpan<T> Span => _array.AsSpan(_index, _length);
        public int Length => _length;

        internal int Index => _index;
        internal T[] Array => _array;

        private T[] _array;
        private int _index;
        private int _length;

        public ReadOnlyMemorySegment(T[] array) : this(array, 0, array.Length) { }
        public ReadOnlyMemorySegment(in MemorySegment<T> other) : this(other.Array, other.Index, other.Length) { }
        public ReadOnlyMemorySegment(in ArraySegment<T> other) : this(other.Array, other.Offset, other.Count) { }
        public ReadOnlyMemorySegment(T[] array, int start, int length) : this()
        {
            _array = array;
            _index = start;
            _length = length;
        }
        public readonly ref T this[int i] => ref _array[i];
        public static implicit operator ReadOnlyMemorySegment<T>(MemorySegment<T> other) => new ReadOnlyMemorySegment<T>(in other);
        public static implicit operator ReadOnlyMemorySegment<T>(ArraySegment<T> other) => new ReadOnlyMemorySegment<T>(in other);
    }
}
