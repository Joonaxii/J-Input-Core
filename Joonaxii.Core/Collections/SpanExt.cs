using Joonaxii.Colors;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Joonaxii.Collections
{
    public static class SpanExt
    {
        public delegate int CompareFunc<T>(in T lhs, in T rhs);

        public delegate bool DoEscape(char input, out char output);
        public delegate bool DoUnescape(char input, out char output);
        public delegate void DoAppendCh(char ch);

        public static void Write<T>(this Span<byte> span, int position, T value, bool bigEndian) where T : unmanaged
            => span.Slice(position).Write(value, bigEndian);

        public static void Write<T>(this Span<byte> span, T value, bool bigEndian) where T : unmanaged
        {
            unsafe
            {
                var data = MemoryMarshal.AsBytes(new Span<T>(Unsafe.AsPointer(ref value), 1));
                data.CopyTo(span);
                if (bigEndian)
                {
                    span.Slice(0, data.Length).Reverse();
                }
            }
        }

        public static ReadOnlySpan<char> TrimLineEnd(this ReadOnlySpan<char> line)
        {
            int end = line.Length;
            for (int i = line.Length - 1; i >= 0; i--)
            {
                switch (line[i])
                    {
                        case '\n':
                        case '\r':
                            --end;
                            continue;
                    }
                break;
            }
            return line.Slice(0, end);
        }

        public static Span<char> TrimLineEnd(this Span<char> line)
        {
            int end = line.Length;
            for (int i = line.Length - 1; i >= 0; i--)
            {
                switch (line[i])
                {
                    case '\n':
                    case '\r':
                        --end;
                        continue;
                }
                break;
            }
            return line.Slice(0, end);
        }


        public static Span<char> Unescape(this Span<char> input, DoUnescape unescapeCheck = null)
        {
            if (input.Length < 1) { return input; }
            unescapeCheck = unescapeCheck ?? Unescape;

            int cPos = 0;
            for (int i = 0; i < input.Length; i++)
            {
                char ch = input[i];
                if (ch == '\\')
                {
                    i++;
                    if (i >= input.Length)
                    {
                        break;
                    }
                    unescapeCheck.Invoke(input[i], out ch);
                }
                input[cPos++] = ch;
            }
            return input.Slice(0, cPos);
        }

        public static string Unescape(this ReadOnlySpan<char> input, DoUnescape unescapeCheck = null)
        {
            if (input.Length < 1) { return ""; }
            Span<char> tmp = input.Length > 2048 ? new char[input.Length] : stackalloc char[input.Length];
            input.CopyTo(tmp);
            return tmp.Unescape(unescapeCheck).ToString();
        }

        public static Span<char> Unescape(this ReadOnlySpan<char> input, Span<char> buffer, DoUnescape unescapeCheck = null)
        {
            if (input.Length < 1 || buffer.Length < input.Length) { return default; }
            input.CopyTo(buffer);
            return buffer.Unescape(unescapeCheck);
        }

        public static void Escape(this ReadOnlySpan<char> input, IWriteable writeable, DoEscape escapeCheck = null)
        {
            if(input.Length < 1) { return; }
            input.Escape(writeable.Append, escapeCheck);
        }

        public static void Escape(this ReadOnlySpan<char> input, TextWriter writer, DoEscape escapeCheck = null)
        {
            if (writer == null) { return; }
            input.Escape(writer.Write, escapeCheck);
        }
        public static void Escape(this ReadOnlySpan<char> input, DoAppendCh append, DoEscape escapeCheck = null)
        {
            if (input.Length < 1 || append == null) { return; }

            escapeCheck = escapeCheck ?? Escape;
            for (int i = 0; i < input.Length; i++)
            {
                char ch = input[i];
                if(escapeCheck.Invoke(ch, out char nCh))
                {
                    append.Invoke('\\');
                    ch = nCh;
                }
                append.Invoke(ch);
            }
        }

        public static bool Unescape(char input, out char output)
        {
            switch (input)
            {
                default:
                    output = input;
                    return false;
                case 'n':
                    output = '\n';
                    return true;
                case '\"':
                    output = '\"';
                    return true;
                case '\'':
                    output = '\'';
                    return true;
                case '0':
                    output = '\0';
                    return true;
                case 'r':
                    output = '\r';
                    return true;
                case 't':
                    output = '\t';
                    return true;
                case '\\':
                    output = '\\';
                    return true;
            }
        }

        public static bool Escape(char input, out char output)
        {
            switch (input)
            {
                default:
                    output = input;
                    return false;
                case '\n':
                    output = 'n';
                    return true;
                case '\"':
                    output = '\"';
                    return true;
                case '\'':
                    output = '\'';
                    return true;
                case '\0':
                    output = '0';
                    return true;
                case '\r':
                    output = 'r';
                    return true;
                case '\t':
                    output = 't';
                    return true;
                case '\\':
                    output = '\\';
                    return true;
            }
        }

        public static int IndexOfUnescaped(this ReadOnlySpan<char> input, int start, char find)
        {
            for (int i = start; i < input.Length; i++)
            {
                char ch = input[i];
                if (ch == '\\')
                {
                    ++i;
                    continue;
                }
                if (ch == find)
                {
                    return i;
                }
            }
            return -1;
        }

        public static bool FindClosing(this ReadOnlySpan<char> data, char open, char close, out int closeIdx)
        {
            if(data.Length < 1 || data[0] != open)
            {
                closeIdx = -1;
                return false;
            }

            int openC = 0;
            for (int i = 1; i < data.Length; i++)
            {
                char ch = data[i];
                if(ch == close)
                { 
                    if(openC-- == 0)
                    {
                        closeIdx = i;
                        return true;
                    }
                }
                else if(ch == open)
                {
                    ++openC;
                }
            }
            closeIdx = -1;
            return true;
        }

        public static ReadOnlySpan<char> TrimData(this ReadOnlySpan<char> data)
        {
            data = data.Trim();
            if (FindClosing(data, '[', ']', out int idx))
            {
                data = data.Slice(1, (idx > -1 ? idx : data.Length) - 1).Trim();
            }
            return data;
        }

        public static int IndexOfNotInside(this ReadOnlySpan<char> str, char find, char opening, char closing)
        {
            int open = 0;
            bool isSame = opening == closing;
            for (int i = 0; i < str.Length; i++)
            {
                char ch = str[i];
                if(ch == find && open == 0)
                {
                    return i;
                } 
                else if(ch == opening)
                {
                    if (isSame)
                    {
                        open = open > 0 ? 0 : 1;
                    }
                    else
                    {
                        ++open;
                    }
                }
                else if(ch == closing && open > 0)
                {
                    --open;
                }
            }
            return -1;
        }

        public static void Sort<T>(this Span<T> span, CompareFunc<T> comparer)
        {
            if(comparer == null || span.Length < 2) { return; }
            QuickSort(span, 0, span.Length - 1, comparer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ReverseEndianess<T>(ref T input) where T : unmanaged
        {
            unsafe
            {
                T temp = input;
                MemoryMarshal.AsBytes(new Span<T>(&temp, 1)).Reverse();
                return temp;
            }
        }

        public static void ReverseEndianess(this Span<byte> buffer, int elementSize)
        {
            if (elementSize < 2) { return; }

            int half = elementSize >> 1;
            for (int i = 0; i < buffer.Length;i += elementSize)
            {
                for (int lo = 0, hi = elementSize - 1; lo < half; lo++, hi--)
                {
                    byte c = buffer[lo + i];
                    buffer[lo + i] = buffer[hi + i];
                    buffer[hi + i] = c;
                }
            }
        }

        public static bool TryParseBool(this ReadOnlySpan<char> str, out bool result, bool strict = false)
        {
            if (str.Length < 1)
            {
                result = false;
                return false;
            }

            if(!strict && str.TryParseInt(out int v))
            {
                result = v != 0;
                return true;
            }

            if(str.Equals("true".AsSpan(), StringComparison.InvariantCultureIgnoreCase))
            {
                result = true;
                return true;
            }
            result = false;
            return str.Equals("false".AsSpan(), StringComparison.InvariantCultureIgnoreCase);
        }

        public static bool TryParseInt(this ReadOnlySpan<char> str, out int result, bool strict = false)
        {
            result = 0;
            if (str.Length < 1)
            {
                return false;
            }

            long total = 0;
            bool sign = false;
            if (str[0] == '-')
            {
                sign = true;
                str = str.Slice(1);
            }

            int len = Math.Min(str.Length, 10);
            for (int i = 0; i < len; i++)
            {
                char ch = str[i];
                switch (ch)
                {
                    case '_':
                    case ' ':
                    case '\'':
                        if (strict)
                        {
                            return false;
                        }
                        continue;
                }
                if (!char.IsDigit(ch)) { return false; }
                total = (ch - '0') + total * 10;
            }

            result = (int)(sign ? -total : total);
            return true;
        }

        public static bool TryParseFloat(this ReadOnlySpan<char> str, out float result, bool strict = false)
        {
            if(str.Length < 1) 
            {
                result = 0;
                return false; 
            }
            float rez = 0.0f, fract = 1.0f;

            str = str.TrimEnd("fFdD".AsSpan());
            if (str[0] == '-')
            {
                fract = -1;
                str = str.Slice(1);
            }

            bool pointSeen = false;
            for (int i = 0; i < str.Length; i++)
            {
                char ch = str[i];
                if (ch == '.')
                {
                    if (strict && pointSeen)
                    {
                        result = 0;
                        return false;
                    }

                    pointSeen = true;
                    continue;
                }

                if (!char.IsDigit(ch))
                {
                    result = 0;
                    return false;
                }

                float d = ch - '0';
                if (pointSeen)
                {
                    fract /= 10.0f;
                }
                rez = rez * 10.0f + d;
            }

            result = rez * fract;
            return !strict | pointSeen;
        }

        public static bool TryParseDouble(this ReadOnlySpan<char> str, out double result, bool strict = false)
        {
            if (str.Length < 1)
            {
                result = 0;
                return false;
            }

            double rez = 0.0, fract = 1.0;
            str = str.TrimEnd("fFdD".AsSpan());
            if (str[0] == '-')
            {
                fract = -1;
                str = str.Slice(1);
            }

            bool pointSeen = false;
            for (int i = 0; i < str.Length; i++)
            {
                char ch = str[i];
                if (ch == '.')
                {
                    if(strict && pointSeen) 
                    {
                        result = 0;
                        return false;
                    }

                    pointSeen = true;
                    continue;
                }

                if (!char.IsDigit(ch))
                {
                    result = 0;
                    return false;
                }

                float d = ch - '0';
                if (pointSeen)
                {
                    fract /= 10.0;
                }
                rez = rez * 10.0 + d;
            }

            result = rez * fract;
            return !strict | pointSeen;
        }

        public static bool TryParseColor(this ReadOnlySpan<char> str, out Color32 result)
        {
            if(str.Length < 1)
            {
                result = default;
                return false;
            }

            if (str[0] == '#')
            {
                str = str.Slice(1);
            }

            int len = Math.Min(8, str.Length);
            char alp = str.Length <= 6 ? 'F' : '0';
            Span<char> hex = stackalloc char[8]
            {
                '0', '0', '0', '0',
                '0', '0', alp, alp,
            };
            for (int i = 0; i < len; i++)
            {
                hex[i] = str[i];
                if (!IsValidHex(hex[i]))
                {
                    result = default;
                    return false;
                }
            }
            result = new Color32(
                ParseHex(hex[0], hex[1]), 
                ParseHex(hex[2], hex[3]),
                ParseHex(hex[4], hex[5]),
                ParseHex(hex[6], hex[7]));
            return true;
        }

        private static void QuickSort<T>(Span<T> span, int left, int right, CompareFunc<T> comparer)
        {
            if (left < right)
            {
                int pivot = Partition(span, left, right, comparer);
                if (pivot > 1)
                {
                    QuickSort(span, left, pivot - 1, comparer);
                }
                if (pivot + 1 < right)
                {
                    QuickSort(span, pivot + 1, right, comparer);
                }
            }
        }

        private static int Partition<T>(Span<T> span, int left, int right, CompareFunc<T> comparer)
        {
            ref T pivot = ref span[left];
            while (true)
            {
                while (comparer.Invoke(in span[left], in pivot) < 0)
                {
                    left++;
                }

                while (comparer.Invoke(in span[right], in pivot) > 0)
                {
                    right--;
                }

                if (left < right)
                {
                    if (comparer.Invoke(in span[left], in span[right]) == 0)
                    {
                        return right;
                    }
                    UnsafeUtil.Swap(ref span[left], ref span[right]);
                }
                else
                {
                    return right;
                }
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidHex(char ch)
        {
            return (ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'F') || (ch >= 'a' && ch <= 'f');
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int HexVal(char ch)
        {
            if (ch >= '0' && ch <= '9') return ch - '0';
            else if (ch >= 'A' && ch <= 'F') return ch - 'A' + 10;
            else if (ch >= 'a' && ch <= 'f') return ch - 'a' + 10;
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ParseHex(char hi, char lo)
        {
            return (byte)(HexVal(lo) | (HexVal(hi) << 4));
        }

        public static bool TryParseEnum<T>(this ReadOnlySpan<char> str, out T result, bool ignoreCase = true, bool strict = true, bool nameOnly = false) where T : Enum
        {
            return EnumInfo<T>.TryParse(str, out result, ignoreCase, strict, nameOnly);
        }

        private static class EnumInfo<T> where T : Enum
        {
            private static string[] _names;
            private static T[] _values;
            private static bool _isFlags;

            static EnumInfo()
            {
                _names = Enum.GetNames(typeof(T));
                _values = Enum.GetValues(typeof(T)) as T[];

                _isFlags = typeof(T).GetCustomAttribute<System.FlagsAttribute>() != null;
            }

            public static int ToInt(T eValue)
            {
                return Convert.ToInt32(eValue);
            }

            public static T FromInt(int lValue)
            {
                return (T)Enum.ToObject(typeof(T), lValue);
            }

            public static bool TryParse(ReadOnlySpan<char> str, out T value, bool ignoreCase = true, bool strict = true, bool nameOnly = false)
            {
                if(str.Length < 1)
                {
                    value = default;
                    return false;
                }
                strict &= !_isFlags;
                StringComparison comp = ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture;

                int ind;
                var data = str;

                int lval = 0;
                bool found = false;
                do
                {
                    ind = data.IndexOf('|');
                    var temp = (ind < 0 ? data : data.Slice(0, ind)).Trim();
                    for (int i = 0; i < _names.Length; i++)
                    {
                        if (_names[i].AsSpan().Equals(temp, comp))
                        {
                            lval |= ToInt(_values[i]);
                            found = true;
                        }
                        else if(!nameOnly && temp.TryParseInt(out int v))
                        {
                            lval |= v;
                            found = true;
                        }
                    }
                    data = data.Slice(ind + 1);
                } while (ind > -1);

                value = FromInt(lval);
                return found;
            }
        }
    }

    [System.Serializable]
    public struct SpanRange32
    {
        public int index;
        public int length;

        public SpanRange32(int index, int length)
        {
            this.index = index;
            this.length = length;
        }

        public override string ToString() => $"{index} : {length}";
        public Span<char> Slice(Span<char> spn) => spn.Slice(index, length);
        public ReadOnlySpan<char> Slice(ReadOnlySpan<char> spn) => spn.Slice(index, length);
    }

    [System.Serializable]
    public struct SpanRange16
    {
        public ushort index;
        public ushort length;

        public SpanRange16(int index, int length)
        {
            this.index = (ushort)index;
            this.length = (ushort)length;
        }

        public override string ToString() => $"{index} : {length}";

        public Span<char> Slice(Span<char> spn) => spn.Slice(index, length);
        public ReadOnlySpan<char> Slice(ReadOnlySpan<char> spn) => spn.Slice(index, length);
    }
}
