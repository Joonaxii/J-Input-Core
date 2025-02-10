using System;
using System.IO;
using System.Text;

namespace Joonaxii.Collections
{
    public class FastStringBuffer : IWriteable
    {
        public Span<char> Span => _buffer.AsSpan().Slice(0, _length);

        public bool IsEoF => _position >= _length;

        public int Length => _length;
        public int Position => _position;
        public char[] Buffer => _buffer;

        private int _length;
        private int _position;
        private char[] _buffer = new char[0];

        public FastStringBuffer() : this(16) { }
        public FastStringBuffer(int capacity)
        {
            Reserve(capacity, false);
        }

        public void Reset()
        {
            _position = 0;
        }

        public void Clear()
        {
            Reset();
            _length = 0;
        }

        public void ReadFrom(byte[] data, Encoding encoding = null)
        {
            encoding ??= Encoding.UTF8;
            int chars = encoding.GetCharCount(data);
            Resize(chars);
            chars = encoding.GetChars(data, 0, data.Length, _buffer, 0);

            _length = chars;
            _position = 0;
        }

        public void ReadFrom(Stream stream, Encoding encoding = null)
        {
            var bytes = IO.IOUtil.ReadBuffered(stream);
            _length = 0;
            _position = 0;
            if (bytes.Length < 1)
            {
                return;
            }

            encoding ??= Encoding.UTF8;
            unsafe
            {
                fixed(byte* bPtr = bytes)
                {
                    int chars = encoding.GetCharCount(bPtr, bytes.Length);
                    Resize(chars);
                    fixed (char* cPtr = _buffer)
                    {
                        chars = encoding.GetChars(bPtr, bytes.Length, cPtr, chars);
                        _length = chars;
                    }
                }
            }
        }

        public bool ReadLine(out Span<char> line)
        {
            if(ReadLine(out SpanRange32 range))
            {
                line = range.Slice(Span);
                return true;
            }

            line = default;
            return false;
        }
        public bool ReadLine(out SpanRange32 line)
        {
            if (IsEoF) 
            {
                line = default;
                return false; 
            }

            int start = _position;
            for (int i = start, j = start + 1; i < _length; i++, j++)
            {
                char cur = _buffer[i];
                char nxt = j < _length ? _buffer[j] : '\0';
                switch (cur)
                {
                    case '\r':
                        if(nxt == '\n')
                        {
                            i = j;
                            goto case '\n';
                        }
                        continue;
                    case '\n':
                        line = new SpanRange32(start, i - start);
                        _position = i + 1;
                        return true;
                    default:
                        if(i == start)
                        {
                            if (char.IsControl(cur) || cur == '\uFEFF')
                            {
                                ++start;
                            }
                        }
                        break;
                }
            }

            line = new SpanRange32(_position, _length - _position);
            _position = _length;
            return true;
        }

        public int Seek(int offset, SeekOrigin origin)
        {
            int finalPos = _position;
            switch (origin)
            {
                default:
                    finalPos = offset;
                    break;
                case SeekOrigin.Current:
                    finalPos += offset;
                    break;
                case SeekOrigin.End:
                    finalPos = _length - offset;
                    break;
            }
            _position = Math.Max(0, finalPos);
            _length = Math.Max(_position, _length);
            return _position;
        }

        public void Append(string str) => Append(str.AsSpan());
        public void Append(Span<char> str)
        {
            ReadOnlySpan<char> spn = str;
            Append(spn);
        }
        public void Append(ReadOnlySpan<char> str)
        {
            Reserve(_position + str.Length, true);
            str.CopyTo(_buffer.AsSpan(_position));
            _position += str.Length;
            _length = Math.Max(_position, _length);
        }
        public void Append(char ch)
        {
            Reserve(_position + 1, true);
            _buffer[_position++] = ch;
            _length = Math.Max(_position, _length);
        }

        public void AppendLine(string str) => AppendLine(str.AsSpan());
        public void AppendLine(Span<char> str)
        {
            ReadOnlySpan<char> spn = str;
            AppendLine(spn);
        }
        public void AppendLine(ReadOnlySpan<char> str)
        {
            Reserve(_position + str.Length + 1, true);
            str.CopyTo(_buffer.AsSpan(_position));
            _position += str.Length;
            _buffer[_position++] = '\n';
            _length = Math.Max(_position, _length);
        }
        public void AppendLine(char ch)
        {
            Reserve(_position + 2, true);
            _buffer[_position++] = ch;
            _buffer[_position++] = '\n';
            _length = Math.Max(_position, _length);
        }

        public override string ToString() => ToString(false);
        public string ToString(bool fromPosition)
        {
            if (fromPosition)
            {
                int len = _length - _position;
                return len < 1 ? "" : new string(_buffer, _position, len);
            }
            return _length < 1 ? "" : new string(_buffer, 0, _length);
        }

        public void Reserve(int count, bool scale)
        {
            if (count <= _buffer.Length) { return; }

            int cap = _buffer.Length;
            if (scale)
            {
                while (cap < count)
                {
                    cap += Math.Max(cap >> 1, 1);
                }
            }
            Array.Resize(ref _buffer, cap);
        }

        public void Resize(int chCount)
        {
            if (_buffer.Length < chCount)
            {
                Array.Resize(ref _buffer, chCount);
            }
            _length = chCount;
        }

        public void Trim(int maxLength)
        {
            if (_length <= maxLength) { return; }
            _length = maxLength;
        }
    }
}
