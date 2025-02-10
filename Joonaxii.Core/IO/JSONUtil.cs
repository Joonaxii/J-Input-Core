using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Joonaxii.IO
{
    public static class JSONUtil
    {
        public delegate JToken DoSerialize<T>(in T value);
        public delegate void DoDeserialize<T>(JToken obj, out T value);

        public delegate JToken DoSerializeObj(in object value);
        public delegate void DoDeserializeObj(JToken obj, out object value);

        private class SerializeInfo
        {
            public JTokenType tokenType;
            public SerializeInfo(JTokenType tokenType)
            {
                this.tokenType = tokenType;
            }
        }
        private class SerializeInfo<T> : SerializeInfo
        {
            public DoSerialize<T> serialize;
            public DoDeserialize<T> deserialize;
            public SerializeInfo(DoSerialize<T> serialize, DoDeserialize<T> deserialize, JTokenType tokenType) : base(tokenType)
            {
                this.serialize = serialize;
                this.deserialize = deserialize;
            }
        }

        private static Dictionary<Type, SerializeInfo> _serializeMethods;

        static JSONUtil()
        {
            _serializeMethods = new Dictionary<Type, SerializeInfo>();

            RegisterType<string>(JTokenType.String, null, null);

            RegisterType<byte>(JTokenType.Integer, null, null);
            RegisterType<sbyte>(JTokenType.Integer, null, null);
            RegisterType<short>(JTokenType.Integer, null, null);
            RegisterType<ushort>(JTokenType.Integer, null, null);
            RegisterType<int>(JTokenType.Integer, null, null);
            RegisterType<uint>(JTokenType.Integer, null, null);
            RegisterType<long>(JTokenType.Integer, null, null);
            RegisterType<ulong>(JTokenType.Integer, null, null);

            RegisterType<float>(JTokenType.Float, null, null);
            RegisterType<decimal>(JTokenType.Float, null, null);

            RegisterType<bool>(JTokenType.Boolean, null, null);

            RegisterType<JObject>(JTokenType.Object, null, null);
            RegisterType<JArray>(JTokenType.Array, null, null);
        }

        public static void RegisterType<T>(JTokenType type, DoSerialize<T> serialize, DoDeserialize<T> deserialize)
        {
            if (_serializeMethods.ContainsKey(typeof(T))) { return; }

            serialize ??= (in T value) => new JValue(value);
            deserialize ??= (JToken token, out T value) => value = token.Value<T>();

            _serializeMethods[typeof(T)] = new SerializeInfo<T>(serialize, deserialize, type);
        }

        private static bool TryGetType(Type type, out SerializeInfo info)
        {
            if(type != null)
            {
                if (type.IsEnum)
                {
                    return _serializeMethods.TryGetValue(typeof(long), out info);
                }
                return _serializeMethods.TryGetValue(type, out info);
            }

            info = null;
            return false;
        }

        public static T CastEnum<T, U>(U i)
        {
            return (T)Convert.ChangeType(i, Enum.GetUnderlyingType(typeof(T)));
        }

        public static U CastEnumToType<T, U>(T e)
        {
            return (U)Convert.ChangeType(e, typeof(U));
        }

        private static string ParsePath(ReadOnlySpan<char> spn, out int index)
        {
            index = -1;

            int ind = spn.IndexOf('[');
            if(ind < 0)
            {
                return spn.ToString();
            }

            var name = spn.Slice(0, ind);
            spn = spn.Slice(ind + 1);
            ind = spn.IndexOf(']');
            spn = spn.Slice(0, ind < 0 ? spn.Length : ind);

            index = int.TryParse(spn.ToString(), out int idx) ? idx : -1;
            return name.ToString();
        }

        public static bool TryGetToken(this JToken token, string name, bool isPath, out JToken child)
        {
            child = null;  
            if (string.IsNullOrEmpty(name))
            {
                child = token;
            }
            else
            {
                if (isPath)
                {
                    int idx = 0;
                    int pos = 0;
                    ReadOnlySpan<char> sp = name.AsSpan();
                    JToken cur = token;
                    do
                    {
                        if(!(cur is JObject obj)) 
                        {
                            cur = null;
                            break;
                        }

                        idx = name.IndexOf('/', pos);
                        string tkName;
                        int iOut;
                        if (idx > -1)
                        {
                           tkName = ParsePath(sp.Slice(pos, idx - pos), out iOut);
                        }
                        else
                        {
                            tkName = ParsePath(sp.Slice(pos), out iOut);
                        }

                        if (!obj.ContainsKey(tkName)) { break; }
                        cur = obj[tkName];

                        JArray arr = cur as JArray;

                        if ((iOut > -1 && cur.Type != JTokenType.Array) || 
                            (iOut < 0 && cur.Type == JTokenType.Array) || (arr != null && arr.Count <= iOut))
                        {
                            cur = null;
                            break;
                        }

                        if(idx > -1 && cur.Type != JTokenType.Object)
                        {
                            cur = null;
                            break;
                        }

                        if(arr != null)
                        {
                            cur = arr[idx];
                        }

                        pos = idx + 1;
                    } while (idx > -1);
                    child = cur;
                }
                else if(token is JObject obj)
                {
                    child = obj.ContainsKey(name) ? obj[name] : null;
                }
            }
            return child != null;
        }

        public static bool TryGet<T>(this JToken token, out T val)
        {
            val = default;
            Type type = typeof(T);
            if (token == null || !TryGetType(type, out var info) || token.Type != info.tokenType) { return false; }

            if (type.IsEnum)
            {
                (info as SerializeInfo<long>).deserialize(token, out long v);
                val = CastEnum<T, long>(v);
                return true;
            }
            (info as SerializeInfo<T>).deserialize(token, out val);
            return true;
        }
        public static bool TryGetChild<T>(this JToken token, string key, out T val)
            => TryGetChild<T>(token, key, false, out val);

        public static bool TryGetChildByPath<T>(this JToken token, string key, out T val)
            => TryGetChild<T>(token, key, true, out val);

        public static bool TryGetChild<T>(this JToken token, string key, bool isPath, out T val)
        {
            val = default;
            if (!TryGetToken(token, key, isPath, out var child)) { return false; }
            return TryGet(child, out val);
        }

        public static bool TryCreate<T>(in T val, out JToken token)
        {
            token = null;
            Type type = typeof(T);
            if (!TryGetType(type, out var info)) { return false; }
            if (type.IsEnum)
            {
                token = (info as SerializeInfo<long>).serialize(CastEnumToType<T, long>(val));
                return true;
            }

            token = (info as SerializeInfo<T>).serialize(in val);

            return true;
        }

        public static bool TryAdd<T>(this JObject obj, string key, in T val)
        {
            if(TryCreate<T>(in val, out var v))
            {
                obj[key] = v;
                return true;
            }
            return false;
        }
        public static bool TryAdd<T>(this JArray obj, in T val)
        {
            if (TryCreate<T>(in val, out var v))
            {
                obj.Add(v);
                return true;
            }
            return false;
        }

        public static T Get<T>(this JToken token, T defVal = default)
        {
            if (TryGet<T>(token, out T v)) { return v; }
            return defVal;
        }

        public static T GetChild<T>(this JToken token, string key, T defVal = default)
             => GetChild<T>(token, key, false, defVal);
        public static T GetChildByPath<T>(this JToken token, string key, T defVal = default)
            => GetChild<T>(token, key, true, defVal);
        public static T GetChild<T>(this JToken token, string key, bool isPath, T defVal = default)
        {
            if (!TryGetToken(token, key, isPath, out var child)) { return defVal; }
            return Get(child, defVal);
        }

        public static bool TryCreateOptional<T>(in OptionalValue<T> val, out JToken token, Func<T, bool> check = null)
        {
            token = null;
            if (!val.enabled || (check != null && !check.Invoke(val.value)))
            {
                return false;
            }
            return TryCreate<T>(in val.value, out token);
        }

        public static bool TryAddOptional<T>(this JObject obj, string key, in OptionalValue<T> val, Func<T, bool> check = null)
        {
            if (TryCreateOptional<T>(in val, out var v, check))
            {
                obj[key] = v;
                return true;
            }
            return false;
        }
        public static bool TryAddOptional<T>(this JArray obj, in OptionalValue<T> val, Func<T, bool> check = null)
        {
            if (TryCreateOptional<T>(in val, out var v, check))
            {
                obj.Add(v);
                return true;
            }
            return false;
        }

        public static bool TryGetOptional<T>(this JToken token, ref OptionalValue<T> val)
        {
            Type type = typeof(T);
            if (token == null || !TryGetType(type, out var info) || token.Type != info.tokenType)
            {
                val.Clear();
                return false;
            }
            val.enabled = true;

            if (type.IsEnum)
            {
                (info as SerializeInfo<long>).deserialize(token, out long v);
                val.value = CastEnum<T, long>(v);
                return true;
            }

            (info as SerializeInfo<T>).deserialize(token, out val.value);
            return true;
        }
        public static bool TryGetOptionalChild<T>(this JToken token, string key, ref OptionalValue<T> val)
            => TryGetOptionalChild<T>(token, key, false, ref val);
        public static bool TryGetOptionalChildByPath<T>(this JToken token, string key, ref OptionalValue<T> val)
            => TryGetOptionalChild<T>(token, key, true, ref val);
        public static bool TryGetOptionalChild<T>(this JToken token, string key, bool isPath, ref OptionalValue<T> val)
        {
            if (!TryGetToken(token, key, isPath, out var child)) 
            {
                val.Clear();
                return false; 
            }
            return TryGetOptional(child, ref val);
        }
    }
}
