using Joonaxii.Collections;
using System;
using System.IO;
using System.Text;

namespace Joonaxii.IO
{
    public class CsvFile
    {
        public int Lines => _lines.Count;
        private RefStack<Line> _lines = new RefStack<Line>();
        private FastStringBuffer _data = new FastStringBuffer();
        private int _current;

        public void Clear()
        {
            _current = -1;
            _lines.Clear();
            _data.Clear();
        }

        public static bool CSVEscape(char input, out char output)
        { 
            switch (input) 
            {
                case ',':
                    output = ',';
                    return true;
            }
            return SpanExt.Escape(input, out output);
        }

        public bool ReadFrom(string path, Encoding encoding = null)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open))
                {
                    return ReadFrom(fs, encoding);
                }
            }
            catch (Exception e)
            {
                Debugger.LogWarning($"Failed to read CSV '{path}'! ({e})");
                return false;
            }
        }
        public bool ReadFrom(Stream stream, Encoding encoding = null)
        {
            encoding = encoding ?? IO.IOUtil.UTF8_NO_BOM;
            Clear();
            try
            {
                int len = (int)(stream.Length - stream.Position);
                byte[] buffer = new byte[len];
                stream.Read(buffer, 0, len);

                int chCount = encoding.GetCharCount(buffer);

                _data.Resize(chCount);
                chCount = encoding.GetChars(buffer, 0, len, _data.Buffer, 0);
                _data.Trim(chCount);

                while (_data.ReadLine(out SpanRange32 range))
                {
                    ReadOnlySpan<char> line = range.Slice(_data.Span);
                    if (line.IsWhiteSpace()) { continue; }

                    int pos = 0;
                    int ind;
                    SpanRange32 part;
                    do
                    {
                        ind = line.IndexOfUnescaped(pos, ',');
                        if (ind >= 0)
                        {
                            part = new SpanRange32(range.index + pos, ind - pos);
                        }
                        else
                        {
                            part = new SpanRange32(range.index + pos, range.length - pos);
                        }
                        Add(part);
                        pos = ind + 1;
                    } while (ind > -1);
                    EndLine();
                }
                _data.Seek(0, SeekOrigin.End);

                return true;
            }
            catch (Exception e)
            {
                Debugger.LogWarning($"Failed to parse CSV! ({e})");
                return false;
            }
        }

        public bool WriteTo(string path, int numToWrite)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Create))
                using (var sw = new StreamWriter(fs, IO.IOUtil.UTF8_NO_BOM, 1024, true))
                {
                    return WriteTo(sw, numToWrite);
                }
            }
            catch (Exception e)
            {
                Debugger.LogWarning($"Failed to write CSV '{path}'! ({e})");
                return false;
            }
        }
        public bool WriteTo(Stream stream, int numToWrite)
        {
            using (var sw = new StreamWriter(stream, IO.IOUtil.UTF8_NO_BOM, 1024, true))
            {
                return WriteTo(sw, numToWrite);
            }
        }
        public bool WriteTo(StreamWriter writer, int numToWrite)
        {
            numToWrite = numToWrite <= 0 ? 8 : numToWrite > 8 ? 8 : numToWrite;
            try
            {
                for (int i = 0; i < _lines.Count; i++)
                {
                    _lines[i].Write(_data.Buffer, writer, numToWrite);
                }
                return true;
            }
            catch (Exception e)
            {
                Debugger.LogWarning($"Failed to write CSV ({e})");
                return false;
            }
        }

        public bool GetValue(int line, int index, out string value)
        {
            value = "";

            if (line >= _lines.Count) { return false; }
            if(_lines[line].TryGet(index, out SpanRange32 range))
            {
                ReadOnlySpan<char> spRange = range.Slice(_data.Span);
                value = spRange.Trim().Unescape();
                return true;
            }
            return false;
        }

        public CsvFile Add(string value)
        => Add(value.AsSpan());

        public CsvFile Add(Span<char> value)
        {
            ReadOnlySpan<char> spn = value;
            return Add(spn);
        }
        public CsvFile Add(ReadOnlySpan<char> value)
        {
            if (_lines.IsEmpty || _current < 0)
            {
                _current = 0;
                _lines.Push(default(Line));
            }
            else if(_current >= 8)
            {
                return this;
            }
            _data.Seek(0, SeekOrigin.End);
            int pos = _data.Position;
            value.Escape(_data, CSVEscape);
            int len = _data.Position - pos;

            _lines.Top().Push(_current++, new SpanRange32(pos, len));
            return this;
        }

        private CsvFile Add(SpanRange32 value)
        {
            if (_lines.IsEmpty || _current < 0)
            {
                _current = 0;
                _lines.Push();
            }
            else if (_current >= 8)
            {
                return this;
            }

            _lines.Top().Push(_current++, value);
            return this;
        }

        public void EndLine()
        {
            _current = -1;
        }

        private unsafe struct Line
        {
            private fixed int _ranges[16];

            public bool TryGet(int index, out SpanRange32 value)
            {
                if(index > 7 || index < 0)
                {
                    value = default;
                    return false;
                }

                index <<= 1;
                value = new SpanRange32()
                {
                    index = _ranges[index],
                    length = _ranges[index + 1],
                };
                return true;
            }

            public void Clear()
            {
                for (int i = 0; i < 16; i++)
                {
                    _ranges[i] = 0;
                }
            }

            public void Write(char[] buffer, TextWriter writer, int numToWrite)
            {
                Line cur = this;
                for (int i = 0, j = 0; i < numToWrite; i++, j += 2)
                {
                    writer.Write(buffer, _ranges[j], _ranges[j + 1]);
                    if (i < numToWrite - 1)
                    {
                        writer.Write(',');
                    }
                }
                writer.Write('\n');
            }

            public void Push(int index, SpanRange32 value)
            {
                index <<= 1;
                _ranges[index] = value.index;
                _ranges[index + 1] = value.length;
            }
        }

    }
}
