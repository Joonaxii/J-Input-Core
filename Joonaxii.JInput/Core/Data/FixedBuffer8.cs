using Newtonsoft.Json.Linq;
using System.IO;
using System;

namespace Joonaxii.JInput
{
    public interface IFixedBuffer<T>
    {
        int Length { get; }

        T GetAt(int index);
        void SetAt(T value, int index);

        U GetAt<U>(int index) where U : unmanaged;
        void SetAt<U>(U value, int index) where U : unmanaged;

        void Add(T value);
        void Insert(int index, T value);

        bool Remove(T value);
        int RemoveAll(T value);
        void RemoveAt(int index);
    }

    public unsafe struct ByteBuffer8 : IFixedBuffer<byte>
    {
        public const int SIZE = 8;

        public int Length => _length;

        private byte _length;
        private fixed byte _buffer[SIZE];

        public void SetRaw(byte* ptr, int length)
        {
            _length = (byte)(length < SIZE ? length : SIZE);

            fixed (byte* buf = _buffer)
            {
                Buffer.MemoryCopy(ptr, buf, SIZE * sizeof(byte), _length * sizeof(byte));
            }
        }

        public byte* GetRaw()
        {
            fixed (byte* ptr = _buffer) { return ptr; }
        }

        public void DeserializeBinary(BinaryReader br, Stream stream)
        {
            _length = br.ReadByte();
            _length = (byte)(_length < SIZE ? _length : SIZE);
            for (int i = 0; i < _length; i++)
            {
                _buffer[i] = br.ReadByte();
            }
        }

        public void DeserializeJSON(JToken tok)
        {
            _length = 0;
            for (int i = 0; i < SIZE; i++)
            {
                _buffer[i] = 0;
            }

            if (tok is JObject jObj)
            {
                var arr = jObj.ToObject<JArray>();
                if (arr != null)
                {
                    _length = (byte)(arr.Count < SIZE ? arr.Count : SIZE);
                    for (int i = 0; i < _length; i++)
                    {
                        _buffer[i] = arr[i].Value<byte>();
                    }
                }
            }
        }

        public void SerializeBinary(BinaryWriter bw, Stream stream)
        {
            bw.Write(_length);
            for (int i = 0; i < _length; i++)
            {
                bw.Write(_buffer[i]);
            }
        }

        public void SerializeJSON(JToken tok)
        {
            if (tok is JObject jObj)
            {
                JArray items = new JArray();
                for (int i = 0; i < _length; i++)
                {
                    items.Add(_buffer[i]);
                }
                jObj.Replace(items);
            }
        }

        public void Add(byte value)
        {
            Debugger.Assert(_length < SIZE, "Buffer already full!");
            _buffer[_length++] = value;
        }

        public void Insert(int index, byte value)
        {
            Debugger.Assert(index >= 0 && index <= _length && index < SIZE, $"Index '{_length}' is out of range! (0/{_length})");

            fixed (byte* buf = _buffer)
            {
                UnsafeUtil.InverseMemoryCopy(buf + index, buf + index + 1, SIZE - index - 1, _length - index);
            }

            _buffer[index] = value;
            _length++;
        }

        public bool Remove(byte value)
        {
            for (int i = 0; i < _length; i++)
            {
                if (_buffer[i] == value)
                {
                    RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public int RemoveAll(byte value)
        {
            int removed = 0;
            for (int i = _length - 1; i >= 0; i--)
            {
                if (_buffer[i] == value)
                {
                    RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }

        public void RemoveAt(int index)
        {
            Debugger.Assert(index >= 0 && index < _length, $"Index '{_length}' is out of range! (0/{_length})");
            fixed (byte* buf = _buffer)
            {
                Buffer.MemoryCopy(buf + index + 1, buf + index, sizeof(byte) * SIZE, sizeof(byte) * SIZE - (index + 1) * sizeof(byte));
            }
            _length--;
        }

        public byte GetAt(int index)
        {
            Debugger.Assert(index >= 0 && index < SIZE, $"Index '{index}' is out of range! (0/{SIZE})");
            return _buffer[index];
        }

        public U GetAt<U>(int index) where U : unmanaged
        {
            Debugger.Assert(index >= 0 && index < SIZE, $"Index '{index}' is out of range! (0/{SIZE})");
            U valueOut = default;
            UnsafeUtil.CopyBytes(ref _buffer[index], ref valueOut);
            return valueOut;
        }

        public void SetAt(byte value, int index)
        {
            Debugger.Assert(index >= 0 && index < SIZE, $"Index '{index}' is out of range! (0/{SIZE})");
            _buffer[index] = value;
        }

        public void SetAt<U>(U value, int index) where U : unmanaged
        {
            Debugger.Assert(index >= 0 && index < SIZE, $"Index '{index}' is out of range! (0/{SIZE})");
            UnsafeUtil.CopyBytes(ref value, ref _buffer[index]);
        }
    }

    public unsafe struct UIntBuffer8 : IFixedBuffer<uint>
    {
        public const int SIZE = 8;

        public int Length => _length;

        private byte _length;
        private fixed uint _buffer[SIZE];

        public void SetRaw(uint* ptr, int length)
        {
            _length = (byte)(length < SIZE ? length : SIZE);

            fixed (uint* buf = _buffer)
                Buffer.MemoryCopy(ptr, buf, SIZE * sizeof(uint), _length * sizeof(uint));
        }

        public uint* GetRaw()
        {
            fixed (uint* ptr = _buffer) { return ptr; }
        }

        public void DeserializeBinary(BinaryReader br, Stream stream)
        {
            _length = br.ReadByte();
            _length = (byte)(_length < SIZE ? _length : SIZE);
            for (int i = 0; i < _length; i++)
            {
                _buffer[i] = br.ReadUInt32();
            }
        }

        public void DeserializeJSON(JToken tok)
        {
            _length = 0;
            for (int i = 0; i < SIZE; i++)
            {
                _buffer[i] = 0;
            }

            if (tok is JObject jObj)
            {
                var arr = jObj.ToObject<JArray>();
                if (arr != null)
                {
                    _length = (byte)(arr.Count < SIZE ? arr.Count : SIZE);
                    for (int i = 0; i < _length; i++)
                    {
                        _buffer[i] = arr[i].Value<uint>();
                    }
                }
            }
        }

        public void SerializeBinary(BinaryWriter bw, Stream stream)
        {
            bw.Write(_length);
            for (int i = 0; i < _length; i++)
            {
                bw.Write(_buffer[i]);
            }
        }

        public void SerializeJSON(JToken tok)
        {
            if (tok is JObject jObj)
            {
                JArray items = new JArray();
                for (int i = 0; i < _length; i++)
                {
                    items.Add(_buffer[i]);
                }
                jObj.Replace(items);
            }
        }

        public void Add(uint value)
        {
            Debugger.Assert(_length < SIZE, "Buffer already full!");
            _buffer[_length++] = value;
        }

        public void Insert(int index, uint value)
        {
            Debugger.Assert(index >= 0 && index <= _length && index < SIZE, $"Index '{_length}' is out of range! (0/{_length})");

            fixed (uint* buf = _buffer)
            {
                UnsafeUtil.InverseMemoryCopy(buf + index, buf + index + 1, SIZE - index - 1, _length - index);
            }

            _buffer[index] = value;
            _length++;
        }

        public bool Remove(uint value)
        {
            for (int i = 0; i < _length; i++)
            {
                if (_buffer[i] == value)
                {
                    RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public int RemoveAll(uint value)
        {
            int removed = 0;
            for (int i = _length - 1; i >= 0; i--)
            {
                if (_buffer[i] == value)
                {
                    RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }

        public void RemoveAt(int index)
        {
            Debugger.Assert(index >= 0 && index < _length, $"Index '{_length}' is out of range! (0/{_length})");
            fixed(uint* buf = _buffer)
            {
                Buffer.MemoryCopy(buf + index + 1, buf + index, sizeof(uint) * SIZE, sizeof(uint) * SIZE - (index + 1) * sizeof(uint));
            }
            _length--;
        }

        public uint GetAt(int index)
        {
            Debugger.Assert(index >= 0 && index < SIZE, $"Index '{index}' is out of range! (0/{SIZE})");
            return _buffer[index];
        }

        public U GetAt<U>(int index) where U : unmanaged
        {
            Debugger.Assert(index >= 0 && index < SIZE, $"Index '{index}' is out of range! (0/{SIZE})");
            U valueOut = default;
            UnsafeUtil.CopyBytes(ref _buffer[index], ref valueOut);
            return valueOut;
        }

        public void SetAt(uint value, int index)
        {
            Debugger.Assert(index >= 0 && index < SIZE, $"Index '{index}' is out of range! (0/{SIZE})");
            _buffer[index] = value;
        }

        public void SetAt<U>(U value, int index) where U : unmanaged
        {
            Debugger.Assert(index >= 0 && index < SIZE, $"Index '{index}' is out of range! (0/{SIZE})");
            UnsafeUtil.CopyBytes(ref value, ref _buffer[index]);
        }
    }
}