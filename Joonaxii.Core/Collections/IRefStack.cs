using System;
using System.Collections.Generic;

namespace Joonaxii.Collections
{
    public interface IRefStack<T> : ICollection<T>
    {
        ref T this[int i] { get; }

        Span<T> Push(ReadOnlySpan<T> buffer);
        ref T Push(in T value);
        ref T Push();
    }
}
