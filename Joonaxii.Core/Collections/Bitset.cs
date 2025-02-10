using Joonaxii.IO;
using System;
using System.IO;

namespace Joonaxii.Collections
{
    public class Bitset
    {
        public int Count => _count;
        private int _count;
        private byte[] _bits = new byte[0];

        public bool this[int i]
        {
            get
            {
                Extract(i, out int bI, out int lI);
                return bI >= 0 && bI < _bits.Length && (_bits[bI] & (1 << lI)) != 0;
            }

            set
            {
                if(i < 0) { return; }

                Extract(i, out int bI, out int lI);
                if(bI >= _bits.Length)
                {
                    Array.Resize(ref _bits, bI);
                }

                _count = _count <= i ? i + 1 : _count;
                ref byte bits = ref _bits[bI];
                bits = value ? (byte)(bits | (1 << lI)) : (byte)(bits & ~(1 << lI));
            }
        }

        public Bitset() : this(32) { }
        public Bitset(int capacity) 
        {
            _count = 0;
            int byteCount = (int)(FastMath.NextDivByPowof2(capacity, 8) >> 3);
            Array.Resize(ref _bits, byteCount);
        }

        public void Clear()
        {
            _count = 0;
            for (int i = 0; i < _bits.Length; i++)
            {
                _bits[i] = 0;
            }
        }

        public void SetAll(bool state)
        {
            byte val = (byte)(state ? 0xFF : 0x00);
            for (int i = 0; i < _bits.Length; i++)
            {
                _bits[i] = val;
            }
        }

        public void Read(Stream stream)
        {
            stream.TryRead(ref _count);
            int byteCount = (int)(FastMath.NextDivByPowof2(_count, 8) >> 3);
            if(_bits.Length < byteCount)
            {
                Array.Resize(ref _bits, byteCount);
            }
            stream.Read(_bits, 0, byteCount);
        }

        public void Write(Stream stream)
        {
            stream.WriteValue(ref _count);
            int byteCount = (int)(FastMath.NextDivByPowof2(_count, 8) >> 3);
            stream.Write(_bits, 0, byteCount);
        }

        private static void Extract(int raw, out int index, out int local)
        {
            index = raw >> 3;
            local = raw - (index << 3);
        }
    }
}
