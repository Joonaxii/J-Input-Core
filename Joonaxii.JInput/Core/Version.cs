using Joonaxii.IO;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Joonaxii.JInput
{
    [StructLayout(LayoutKind.Explicit, Size = 4)]
    public struct Version : System.IEquatable<Version>
    {
        [FieldOffset(0)] private int _value;

        [FieldOffset(3)] public byte major;
        [FieldOffset(2)] public byte minor;
        [FieldOffset(1)] public byte patch;
        [FieldOffset(0)] public byte revision;

        static Version()
        {
            JSONUtil.RegisterType<Version>(JTokenType.Array, (in Version version) =>
            {
                return new JArray() { version.major, version.minor, version.patch, version.revision };
            }, 
            (JToken token, out Version vec) =>
            {
                vec = new Version();
                if (token is JArray arr)
                {
                    unsafe
                    {
                        byte* tmp = stackalloc byte[4];
                        int min = Math.Min(arr.Count, 4);
                        for (int i = 0; i < min; i++)
                        {
                            tmp[i] = (byte)arr[0].Get<int>(0);
                        }
                    }
                }
            }
            );
        }

        public Version(byte major, byte minor, byte patch, byte revision) : this()
        {
            this.major = major;
            this.minor = minor;
            this.patch = patch;
            this.revision = revision;
        }

        public override int GetHashCode() => _value;

        public override bool Equals(object obj) => obj is Version version && Equals(version);
        public bool Equals(Version other) => _value == other._value;

        public static bool operator ==(Version a, Version b) => a._value == b._value;
        public static bool operator !=(Version a, Version b) => a._value != b._value;

        public static bool operator >(Version a, Version b) => a._value > b._value;
        public static bool operator <(Version a, Version b) => a._value < b._value;

        public static bool operator >=(Version a, Version b) => a._value >= b._value;
        public static bool operator <=(Version a, Version b) => a._value <= b._value;

        public override string ToString() => $"{major}.{minor}.{patch}.{revision}";

        public void SerializeBinary(BinaryWriter bw, Stream stream)
        {
            bw.Write(_value);
        }

        public void DeserializeBinary(BinaryReader br, Stream stream)
        {
            _value = br.ReadInt32();
        }

        public void SerializeJSON(JToken tok)
        {
            if (tok is JObject jObj)
            {
                jObj["major"] = major;
                jObj["minor"] = minor;
                jObj["patch"] = patch;
                jObj["revision"] = revision;
            }
        }

        public void DeserializeJSON(JToken tok)
        {
            if (tok is JObject jObj)
            {
                major = jObj["major"].Value<byte>();
                minor = jObj["minor"].Value<byte>();
                patch = jObj["patch"].Value<byte>();
                revision = jObj["revision"].Value<byte>();
            }
        }
    }
}