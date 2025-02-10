using System;

namespace Joonaxii.Collections
{
    public interface IWriteable
    {
        int Position { get; }
        int Length { get; }

        void Append(char ch);
        void Append(Span<char> str);
        void Append(ReadOnlySpan<char> str);
        void Append(string str);
    }
}
