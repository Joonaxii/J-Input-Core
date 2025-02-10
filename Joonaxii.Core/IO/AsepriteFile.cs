using Joonaxii.Collections;
using Joonaxii.Colors;
using Joonaxii.Hashing;
using Joonaxii.IO.Image;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Runtime.InteropServices;
using static Joonaxii.Atlas;

namespace Joonaxii.IO
{
    // TODO: Actually finish writing and testing this.
    // Might actuall convert my C++ implementation of
    // Aseprite file de/encoder since that works and 
    // has been tested.
    public class AsepriteFile : IDisposable
    {
        private const ushort SIGNATURE = 0xA5E0;

        public int Width => _header.width;
        public int Height => _header.height;

        public int TagCount => _tags.Count;
        public int FrameCount => _frames.Length;

        public Profile Default => _defaultProfile;

        private bool _leaveOpen;
        private Stream _stream;

        private AseHeader _header;
        private AseFrame[] _frames = new AseFrame[0];
        private RefStack<AseLayer> _layers = new RefStack<AseLayer>();
        private RefStack<AseTag> _tags = new RefStack<AseTag>();
        private RefStack<AseCel> _cels = new RefStack<AseCel>();
        private RefStack<Color32> _palette = new RefStack<Color32>();
        private RefStack<AseUserdata> _userdata = new RefStack<AseUserdata>();

        private byte[] _compBuffer = new byte[0];
        private byte[] _pixelBuffer = new byte[0];
        private Profile _defaultProfile = new Profile();
        private int _spriteUserdata = -1;
        private int _rawLayers = 0;

        private AseFileFlags _flags;

        private int _compBufSize;
        private int _maxReso;
        private int _totalData;
        private bool _softClosed;

        public bool Open(Stream stream, bool leaveOpen)
        {
            if (stream == null || !stream.CanRead)
            {
                Debugger.LogWarning("Given stream is either null or is not readable!");
                return false;
            }

            if (_softClosed)
            {
                _leaveOpen = stream != _stream && _leaveOpen;
                _stream = stream;
                _leaveOpen = leaveOpen;
                return true;
            }

            long pos = stream.Position;
            long startP = pos;
            if (!stream.TryRead(ref _header, ref pos) || _header.sig != SIGNATURE)
            {
                Debugger.LogWarning($"Given Aseprite file/data isn't Aseprite data! ({_header.sig:X4} =/= {SIGNATURE:X4} | @{startP}/{pos})");
                return false;
            }
            _leaveOpen = stream != _stream && _leaveOpen;

            Close(false);

            _maxReso = _header.width * _header.height;

            _stream = stream;
            _leaveOpen = leaveOpen;

            _palette.Resize(_header.numColors < 1 ? 256 : _header.numColors, false);
            _palette.AsSpan().ZeroMem();

            uint flags = 0;
            _compBufSize = 0;
            AseChunk chunk = default;
            UserObj previous = default;
            Array.Resize(ref _frames, _header.frames);
            for (int i = 0; i < _frames.Length; i++)
            {
                ref var frame = ref _frames[i];
                if (!stream.TryRead(ref frame, ref pos))
                {
                    Debugger.LogWarning($"Failed to read frame #{i}!");
                    Close(false);
                    return false;
                }

                int chunks = (int)(frame.numChunks0 == 0xFFFF ? frame.numChunks1 : frame.numChunks0);
                for (int j = 0; j < chunks; j++)
                {
                    chunk.position = pos;
                    if (!stream.TryRead(ref chunk.data, ref pos))
                    {
                        Debugger.LogWarning($"Failed to read chunk @ frame #{i}!");
                        Close(false);
                        return false;
                    }
                    ProcessChunk(stream, i, in chunk, ref previous, ref flags, ref _compBufSize, ref pos);
                }
            }

            _totalData = 0;
            Optimize();

            _defaultProfile.Reset();

            _defaultProfile.from = 0;
            _defaultProfile.to = (ushort)(_header.frames - 1);
            for (int i = 0; i < _layers.Count; i++)
            {
                ref var layer = ref _layers[i];
                bool isVisible = IsLayerVisible(i);
                if (!layer.IsNormal || !isVisible) { continue; }
                _defaultProfile.layers.Push((ushort)i);
            }
            _defaultProfile.CalculateFrames(this);
            return true;
        }

        public void Close(bool softClose)
        {
            if (_stream != null)
            {
                if (!_leaveOpen)
                {
                    _stream.Close();
                }
                _stream = null;
            }

            if (softClose) { _softClosed = true; return; }

            _softClosed = false;
            Array.Resize(ref _frames, 0);
            Array.Resize(ref _compBuffer, 0);
            Array.Resize(ref _pixelBuffer, 0);

            _flags = AseFileFlags.None;

            _spriteUserdata = -1;
            _rawLayers = 0;
            _maxReso = 0;
            _totalData = 0;

            _layers.Clear(true);
            _tags.Clear(true);
            _cels.Clear(true);
            _userdata.Clear(true);
        }

        public void Dispose()
        {
            Close(false);
        }

        public bool IsLayerVisible(int index)
        {
            if (index < 0 || index >= _layers.Count) { return false; }

            int curDepth = int.MaxValue;
            do
            {
                ref var layer = ref _layers[index];
                if (layer.data.childLevel < curDepth)
                {
                    curDepth = layer.data.childLevel;
                    if (!layer.IsVisible) { return false; }
                }
                --index;
            } while (curDepth > 0 && index > 0);
            return true;
        }

        public int IndexOfLayer(string name)
         => IndexOfLayer(name.AsSpan());
        public int IndexOfLayer(ReadOnlySpan<char> name)
        {
            for (int i = 0; i < _layers.Count; i++)
            {
                if (name.Equals(_layers[i].name.AsSpan(), StringComparison.InvariantCulture))
                {
                    return i;
                }
            }
            return -1;
        }

        public bool Render(int frame, ReadOnlySpan<ushort> layers, ref ImageData image)
        {
            const int MAX_LAYERS = 256;

            if(frame < 0 || frame >= _frames.Length || layers.Length < 1) 
            {
                Debugger.LogWarning($"Failed to render frame #{frame}! ({_frames.Length} total frames | {layers.Length} given layers)");
                return false; 
            }
            if(layers.Length > MAX_LAYERS)
            {
                layers = layers.Slice(0, MAX_LAYERS);
            }

            int reso = _header.width * _header.height;
            image.Allocate(ImageFormat.RGBA32, _header.width, _header.height);

            Span<int> celsToDraw = stackalloc int[layers.Length];

            var pixSpan = MemoryMarshal.Cast<byte, Color32>(image.Data);
            pixSpan.ZeroMem();

            EnsureBuffers();
            for (int i = 0; i < celsToDraw.Length; i++)
            {
                int lrIdx = layers[i];
                int celScan = lrIdx * _frames.Length;

                celsToDraw[i] = celScan + frame;
            }
            celsToDraw.Sort(CompareCels);

            SpriteRect celRect = default;
            SpriteRect celPos = default;
            int bpp = (_header.depth >> 3);
            for (int i = 0; i < celsToDraw.Length; i++)
            {
                int celIdx = celsToDraw[i];
                int celDraw = GetNonLinked(celIdx, frame);

                if(celDraw < 0) { continue; }

                ref AseCel cel = ref _cels[celDraw];
                ref AseLayer lr = ref _layers[cel.data.layerIndex];

                if (cel.IsEmpty) { continue; }

                byte alpha = (_header.flags & 0x1) != 0x0 ? FastMath.MultUI8(cel.data.opacity, lr.data.opacity) : cel.data.opacity;
                _stream.Seek(cel.filePos, SeekOrigin.Begin);

                GetCroppedCelRect(ref celRect, ref celPos, in cel);

                int celReso = cel.width * cel.height;
                if (cel.IsCompressed)
                {
                    int len = _stream.Read(_compBuffer, 0, cel.size);
                    Compression.Inflate(_compBuffer.AsSpan(0, len), _pixelBuffer);
                }
                else
                {
                    _stream.Read(_pixelBuffer, 0, celReso * bpp);
                }
                int transparentPixel = (lr.data.flags & AseLayerFlags.Background) != 0 ? -1 : _header.transparent;

                Blend(lr.data.blendMode, pixSpan, in celRect, ref celPos, alpha, transparentPixel);
            }
            return true;
        }

        public void Optimize()
        {
            int optimized = 0;
            int total = 0;
            for (int i = 0, iP = 0; i < _rawLayers; i++, iP += _header.frames)
            {
                for (int f = 0, cPos = iP; f < _header.frames; f++, cPos++)
                {
                    ref var cel = ref _cels[cPos];
                    if (!cel.IsEmpty && !cel.IsLinked)
                    {
                        ++total;
                        int dup = FindDuplicate(cPos, f);
                        if(dup > -1)
                        {
                            ++optimized;
                            cel.MakeLinked(dup);
                        }
                    }
                }
            }
            Debugger.Log($"[AsepriteFile] Optimized/Auto-Linked '{optimized}' [{total} | {total - optimized}] cels!");
        }

        private int FindDuplicate(int absPos, int frame)
        {
            ref AseCel cel = ref _cels[absPos];

            --absPos;
            --frame;

            for (int i = frame - 1, j = absPos; i >= 0; i--, j--)
            {
                ref var dupCel = ref _cels[j];
                if (!dupCel.IsLinked && !dupCel.IsEmpty && cel.AreEqual(in dupCel))
                {
                    return i;
                }
            }
            return -1;
        }

        private void Blend(AseBlendMode blend, Span<Color32> pixels, in SpriteRect dst, ref SpriteRect src, int alpha, int alphaPix)
        {
            var blendFunc = AseColor.GetBlendFunc(blend);
            var scanParse = AseColor.GetScanParser(_header.depth);

            int bpp = (_header.depth >> 3);
            int copy = dst.width * bpp;
            int scanBpp = src.width * bpp;

            int dstPos = dst.y * _header.width + dst.x;
            int srcPos = src.y * scanBpp + src.x;

            Span<Color32> scan = stackalloc Color32[dst.width];
            PaletteRef<Color32> pal = new PaletteRef<Color32>(_palette.AsSpan(), alphaPix);
            for (int y = 0; y < dst.height; y++, srcPos += scanBpp, dstPos += _header.width)
            {
                scanParse.Invoke(_pixelBuffer.AsSpan(srcPos, copy), scan, pal);
                AseColor.Blend(blendFunc, pixels.Slice(dstPos, dst.width), scan, alpha);
            }
        }

        private int CompareCels(in int lhs, in int rhs)
        {
            ref AseCel celL = ref _cels[lhs];
            ref AseCel celR = ref _cels[rhs];

            int lhsOrder = (int)(celL.data.layerIndex) + celL.data.zIndex;
            int rhsOrder = (int)(celR.data.layerIndex) + celR.data.zIndex;

            return lhsOrder.CompareTo(rhsOrder);
        }

        private void GetCroppedCelRect(ref SpriteRect rect, ref SpriteRect srcPos, in AseCel cel)
        {
            int minX = cel.data.xPos;
            int minY = cel.data.yPos;

            int maxX = cel.data.xPos + cel.width;
            int maxY = cel.data.yPos + cel.height;

            srcPos.x = (ushort)(cel.data.xPos < 0 ? -cel.data.xPos : 0);
            srcPos.y = (ushort)(cel.data.yPos < 0 ? -cel.data.yPos : 0);

            srcPos.width = cel.width;
            srcPos.height = cel.height;

            rect.x = (ushort)FastMath.Clamp(minX, 0, _header.width);
            rect.y = (ushort)FastMath.Clamp(minY, 0, _header.height);

            rect.width = (ushort)(FastMath.Clamp(maxX, 0, _header.width) - rect.x);
            rect.height = (ushort)(FastMath.Clamp(maxY, 0, _header.height) - rect.y);
        }

        private int GetNonLinked(int celIdx, int frame)
        {
            int scan = celIdx - frame;
            int curCel = celIdx;
            while(frame > 0)
            {
                ref var cel = ref _cels[curCel];
                if (!cel.IsLinked)
                {
                    break;
                }
                int target = scan + cel.linkFrame;
                if (target >= curCel) { return -1; }
                curCel = target;
                frame = cel.linkFrame;
            }
            return _cels[curCel].IsEmpty ? -1 : curCel;
        }

        private void EnsureBuffers()
        {
            Array.Resize(ref _compBuffer, _compBufSize);
            Array.Resize(ref _pixelBuffer, _maxReso * 4);
        }

        private void ProcessChunk(Stream stream, int frameIdx, in AseChunk chunk, ref UserObj lastObj, ref uint flags, ref int compBufSize, ref long pos)
        {
            chunk.SkipToData(stream, ref pos);
            switch (chunk.data.type)
            {
                case AseType.UserData:
                    {
                        int idx = _userdata.Count;
                        _userdata.Push().Read(in chunk, stream, ref pos);
                        switch (lastObj.type)
                        {
                            case AseType.None:
                                _spriteUserdata = _spriteUserdata == -1 ? idx : _spriteUserdata;
                                lastObj = default;
                                break;
                            case AseType.Layer:
                                _layers[lastObj.index].userdata = idx;
                                lastObj = default;
                                break;
                            case AseType.Cel:
                                _cels[lastObj.index].userdata = idx;
                                lastObj = default;
                                break;
                            case AseType.Tags:
                                _tags[lastObj.index++].userdata = idx;
                                if (lastObj.index >= _tags.Count)
                                {
                                    lastObj = default;
                                }
                                break;
                        }
                    }
                    break;
                case AseType.OldPalette0:
                    if ((flags & 0x6) == 0)
                    {
                        flags |= 0x1;
                        int offset = 0;
                        ushort numPackets = 0;
                        stream.TryRead(ref numPackets, ref pos);

                        int skip = 0;
                        int len = 0;
                        for (int i = 0; i < numPackets; i++)
                        {
                            skip = stream.ReadByte();
                            len = stream.ReadByte();
                            pos += 2;
                            len = len < 1 ? 256 : len;

                            offset += skip;
                            for (int j = 0, k = offset; j < len; j++, k++)
                            {
                                stream.TryRead(ref _palette[k], 3, ref pos);
                                _palette[k].a = 0xFF;
                            }
                        }
                    }
                    break;
                case AseType.Palette:
                    {
                        flags |= 0x4;

                        AsePalette hdr = default;
                        stream.TryRead(ref hdr, ref pos);

                        int length = (hdr.last - hdr.first + 1);

                        int len = hdr.last + 1;
                        int oldLen = _palette.Count;
                        if (oldLen < len)
                        {
                            _palette.Resize(len, false);
                            _palette.AsSpan(oldLen).ZeroMem();
                        }

                        uint pFlags = 0;
                        for (int i = 0, j = hdr.first; i < length; i++, j++)
                        {
                            stream.TryRead(ref pFlags, ref pos);
                            stream.TryRead(ref _palette[j], ref pos);

                            if ((flags & 0x1) != 0)
                            {
                                stream.SkipString<ushort>(ref pos);
                            }
                        }
                    }
                    break;

                case AseType.Layer:
                    {
                        lastObj = new UserObj(AseType.Layer, _layers.Count);
                        ref var layer = ref _layers.Push();
                        layer.Read(stream, ref _rawLayers, ref pos);
                        if (layer.data.type == AseLayerType.Group)
                        {
                            ++_rawLayers;
                        }
                    }
                    break;
                case AseType.Cel:
                    {
                        if (_cels.Count < 1)
                        {
                            _cels.Resize(_header.frames * _rawLayers, false);
                            _cels.AsSpan().ZeroMem();
                        }

                        ushort lrIdx = 0;
                        stream.TryRead(ref lrIdx, ref pos);
                        stream.Seek(-2, SeekOrigin.Current);
                        pos -= 2;

                        int rawIdx = _layers[lrIdx].rawIdx;
                        int celIdx = rawIdx * _header.frames + frameIdx;
                        ref var cel = ref _cels[celIdx];

                        cel.Read(in chunk, stream, ref pos);
                        switch (cel.data.type)
                        {
                            case AseCelType.RawImage:
                            case AseCelType.Compressed:
                                {
                                    _stream.Seek(cel.filePos, SeekOrigin.Begin);
                                    MD5.State state = default;
                                    cel.hash = state.Init().Update(_stream, cel.size, 8192).Extract();
                                }
                                break;
                        }

                        chunk.Skip(stream, ref pos);
                        lastObj = new UserObj(AseType.Cel, celIdx);

                        switch (cel.data.type)
                        {
                            case AseCelType.Compressed:
                                compBufSize = Math.Max(compBufSize, cel.size);
                                goto case AseCelType.RawImage;
                            case AseCelType.RawImage:
                                _maxReso = FastMath.Max(_maxReso, cel.width * cel.height);
                                break;
                        }
                    }
                    break;

                case AseType.Tags:
                    {
                        lastObj = new UserObj(AseType.Tags, _tags.Count);

                        ushort count = 0;
                        stream.TryRead(ref count, ref pos);
                        stream.Seek(8, SeekOrigin.Current);
                        pos += 8;

                        _tags.Resize(count, false);
                        for (int i = 0; i < count; i++)
                        {
                            _tags[i].Read(stream, ref pos);
                        }
                    }
                    break;
            }
            chunk.Skip(stream, ref pos);
        }

        private void GetTrimmedBounds(int frame, ReadOnlySpan<ushort> layers, out SpriteRect rect)
        {
            rect.x = _header.width;
            rect.y = _header.height;
            rect.width = 0;
            rect.height = 0;

            int maxX = 0;
            int maxY = 0;
            SpriteRect tmpA = default;
            SpriteRect tmpB = default;
            for (int i = 0; i < layers.Length; i++)
            {
                int celP = layers[i] * _frames.Length + frame;
                celP = GetNonLinked(celP, frame);
                if(celP < 0) { continue; }

                ref var cel = ref _cels[celP];
                switch (cel.data.type)
                {
                    case AseCelType.RawImage:
                    case AseCelType.Compressed:
                        GetCroppedCelRect(ref tmpA, ref tmpB, in cel);

                        rect.x = FastMath.Min(tmpA.x, rect.x);
                        rect.y = FastMath.Min(tmpA.y, rect.y);

                        maxX = FastMath.Max(tmpA.x + tmpA.width, maxX);
                        maxY = FastMath.Max(tmpA.y + tmpA.height, maxY);
                        break;
                }
            }

            rect.width = (ushort)FastMath.Max(maxX - rect.x, 0);
            rect.height = (ushort)FastMath.Max(maxY - rect.y, 0);
        }

        public class Profile
        {
            public bool IsValid => layers.Count > 0 && !string.IsNullOrWhiteSpace(name);

            public int Length => to - from + 1;

            public string name;
            public ushort from;
            public ushort to;
            public RefStack<ushort> layers = new RefStack<ushort>();
            public RefStack<SpriteRect> frames = new RefStack<SpriteRect>();

            public void Reset()
            {
                name = "";
                layers.Clear();
                frames.Clear();
            }

            private void PushLayer(int index, bool noDuplicates)
            {
                if(noDuplicates && layers.IndexOf((ushort)index, ValueEqualityComparer<ushort>.Default) > -1) 
                { 
                    return;
                }
                layers.Push((ushort)index);
            }

            private void CopyCommon(string addName, Profile other)
            {
                Reset();
                name = $"{addName}-{other.name}";
                from = other.from;
                to = other.to;
            }

            private void CommonParse(JObject json, AsepriteFile ase)
            {
                Reset();
                if (json.TryGetChild("tag", out string tagName) && ase.TryGetTagByName(tagName.AsSpan(), out int tagIdx))
                {
                    ref var tag = ref ase.GetTagByIndex(tagIdx);
                    from = tag.data.from;
                    to = tag.data.to;
                    name = tag.name;
                }
                else
                {
                    name = json.GetChild("name", "Default");
                    from = (ushort)json.GetChild<int>("from", 0);
                    to = (ushort)json.GetChild<int>("to", ase.FrameCount - 1);
                }
            }

            private static void PushLayer(AsepriteFile ase, Profile prof, bool requireVisible, int idx)
            {
                ref var lr = ref ase._layers[idx];
                if(!lr.IsVisible && requireVisible) { return; }

                if (lr.data.type == AseLayerType.Group)
                {
                    for (int i = idx + 1; i < ase._layers.Count; i++)
                    {
                        ref var tmp = ref ase._layers[i];
                        if (tmp.data.childLevel - lr.data.childLevel != 1)
                        {
                            break;
                        }
                        PushLayer(ase, prof, requireVisible, i);
                    }
                }
                else if (lr.data.type == AseLayerType.Normal)
                {
                    prof.PushLayer(idx, true);
                }
            }

            private static bool BuildProfile(AsepriteFile ase, bool requireVisible, string layer, Profile common, out Profile profile)
            {
                int idx = ase.IndexOfLayer(layer);
                if (idx > -1)
                {
                    profile = new Profile();
                    profile.CopyCommon(layer, common);
                    PushLayer(ase, profile, requireVisible, idx);
                    return profile.layers.Count > 0;
                }
                profile = null;
                return false;
            }

            public static void Load(JObject json, AsepriteFile ase, Action<Profile> onParse)
            {
                if (json == null || ase == null || onParse == null) { return; }
                Profile common = new Profile();
                common.CommonParse(json, ase);

                Profile profile = new Profile();
                if (json.TryGetChild<JArray>("layers", out var layerNames))
                {
                    foreach (var name in layerNames)
                    {
                        if (name.TryGet(out string str))
                        {
                            int idx = ase.IndexOfLayer(str);
                            if (idx > -1)
                            {
                                common.PushLayer(idx, false);
                            }
                        }
                    }
                    common.CalculateFrames(ase);
                    onParse.Invoke(common);
                }
                else if(json.TryGetChild<JArray>("split", out layerNames))
                {
                    foreach (var name in layerNames)
                    {
                        if (name.TryGet(out string str))
                        {
                            if(BuildProfile(ase, true, str, common, out var copy))
                            {
                                copy.CalculateFrames(ase);
                                onParse.Invoke(copy);
                            }
                        }
                    }
                }
                else if(json.TryGetChild<string>("split", out string value))
                {
                    string type = value.ToLower();
                    switch (type)
                    {
                        case "all":
                        case "visible":
                            {
                                bool requireVis = type == "visible";
                                for (int i = 0; i < ase._layers.Count; i++)
                                {
                                    ref var aseLayer = ref ase._layers[i];
                                    if ((requireVis && !aseLayer.IsVisible) || aseLayer.data.type == AseLayerType.Tilemap)
                                    {
                                        continue;
                                    }
                                    profile.CopyCommon(aseLayer.name, common);

                                    PushLayer(ase, profile, true, i);

                                    if(profile.layers.Count > 0)
                                    {
                                        profile.CalculateFrames(ase);
                                        onParse.Invoke(profile);
                                        profile = new Profile();
                                    }
                                }
                            }
                            break;
                    }
                }
            }

            public void CalculateFrames(AsepriteFile ase)
            {
                frames.Clear();
                frames.Resize(Length, false);

                var lrs = layers.AsSpan();
                for (int i = from; i <= to; i++)
                {
                    ase.GetTrimmedBounds(i, lrs, out frames[i]);
                }
            }
        }

        private ref AseTag GetTagByIndex(int index) => ref _tags[index];

        public bool TryGetTagByName(ReadOnlySpan<char> tagName, out int tagIdx)
        {
            for (int i = 0; i < _tags.Count; i++)
            {
                if (tagName.Equals(_tags[i].name.AsSpan(), StringComparison.InvariantCulture))
                {
                    tagIdx = i;
                    return true;
                }
            }
            tagIdx = -1;
            return false;
        }

        private struct UserObj
        {
            public AseType type;
            public int index;

            public UserObj(AseType type, int index)
            {
                this.type = type;
                this.index = index;
            }
        }

        [System.Flags]
        private enum AseFileFlags
        {
            None,
            DataLoaded = 0x1,
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct AseChunk
        {
            public const int SIZE = 6;

            public long End => position + data.size;

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct Data
            {
                public int size;
                public AseType type; 
            }
            public Data data;
            public long position;

            public void SkipToData(Stream stream, ref long cPos)
            {
                cPos = position + Marshal.SizeOf<AseChunk.Data>();
                stream.Seek(cPos, SeekOrigin.Begin);
            }

            public void Skip(Stream stream, ref long cPos)
            {
                cPos = position + data.size;
                stream.Seek(cPos, SeekOrigin.Begin);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct AseHeader
        {
            public uint size;
            public ushort sig;
            public ushort frames;

            public ushort width;
            public ushort height;

            public ushort depth;
            public uint flags;

            public ushort speed;
            public unsafe fixed uint res0[2];

            public byte transparent;
            public unsafe fixed byte res1[3];
            public ushort numColors;

            public byte pixWidth;
            public byte pixHeight;

            public short gridPosX;
            public short gridPosY;
            public ushort gridWidth;
            public ushort gridHeight;

            public unsafe fixed byte res2[84];
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct AseFrame
        {
            public uint size;
            public ushort signature;
            public ushort numChunks0;
            public ushort duration;
            public unsafe fixed byte reserved[2];
            public uint numChunks1;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct AsePalette
        {
            public int size;
            public int first;
            public int last;
            public unsafe fixed byte reserved[8];
        }

        private struct AseLayer
        {
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct Data
            {
                public AseLayerFlags flags;
                public AseLayerType type;
                public ushort childLevel;
                public ushort defWidth;
                public ushort defHeight;
                public AseBlendMode blendMode;
                public byte opacity;
                public unsafe fixed byte reserved[3];
            }

            public Data data;
            public string name;
            public int userdata;
            public int rawIdx;

            public bool IsVisible => data.flags.HasFlag(AseLayerFlags.Visible);
            public bool IsNormal => data.type == AseLayerType.Normal;

            public void Read(Stream stream, ref int rawLayers, ref long position)
            {
                userdata = -1;
                stream.TryRead(ref data, ref position);
                name = stream.ReadString<ushort>(ref position);

                if (data.type == AseLayerType.Tilemap)
                {
                    rawIdx = rawLayers++;
                    stream.Seek(4, SeekOrigin.Current);
                }
                else if (data.type == AseLayerType.Normal)
                {
                    rawIdx = rawLayers++;
                }
            }
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        private struct AseCel
        {
            public bool IsEmpty => filePos == 0 && data.type == AseCelType.RawImage;
            public bool IsLinked => data.type == AseCelType.Linked;

            public bool IsCompressed => data.type == AseCelType.Compressed;

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct Data
            {
                public ushort layerIndex;
                public short xPos;
                public short yPos;
                public byte opacity;
                public AseCelType type;
                public short zIndex;
                public unsafe fixed byte reserved[5];
            }

            [FieldOffset(0)] public Data data;
            [FieldOffset(16)] public int userdata;
            [FieldOffset(20)] public ushort width;
            [FieldOffset(22)] public ushort height;
            [FieldOffset(20)] public ushort linkFrame;
            [FieldOffset(24)] public int filePos;
            [FieldOffset(28)] public int size;
            [FieldOffset(32)] public MD5.Hash hash;

            public bool AreEqual(in AseCel other)
            {
                return data.xPos == other.data.xPos && data.yPos == other.data.yPos && hash.Equals(in other.hash);
            }

            public void MakeLinked(int linked)
            {
                data.type = AseCelType.Linked;
                linkFrame = (ushort)linked;
            }
            
            public void Read(in AseChunk chunk, Stream stream, ref long position)
            {
                userdata = -1;
                stream.TryRead(ref data, ref position);

                switch (data.type)
                {
                    case AseCelType.Linked:
                        stream.TryRead(ref linkFrame, ref position);
                        break;
                    case AseCelType.Compressed:
                    case AseCelType.RawImage:
                        stream.TryRead(ref width, ref position);
                        stream.TryRead(ref height, ref position);
                        break;
                }

                filePos = (int)position;
                size = (int)(chunk.End - filePos);
            }
        }

        private struct AseTag
        {
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct Data
            {
                public ushort from;
                public ushort to;
                public byte loop;
                public ushort repeat;

                public unsafe fixed byte reserved0[6];
                public Color32 color;
            }

            public Data data;
            public string name;
            public int userdata;

            public void Read(Stream stream, ref long pos)
            {
                userdata = -1;
                stream.TryRead(ref data, ref pos);
                name = stream.ReadString<ushort>(ref pos);
            }
        }

        private struct AseUserdata
        {
            public string data;
            public Color32 color;

            public void Read(in AseChunk chunk, Stream stream, ref long position)
            {
                uint flags = 0;
                stream.TryRead(ref flags, ref position);

                if ((flags = 0x1) != 0)
                {
                    data = stream.ReadString<ushort>(ref position);
                }

                if ((flags = 0x2) != 0)
                {
                    stream.TryRead(ref color, ref position);
                }

                if ((flags = 0x4) != 0)
                {
                    chunk.Skip(stream, ref position);
                }
            }
        }

        [System.Flags]
        private enum AseLayerFlags : ushort
        {
            None = 0x0000,

            Visible = 0x1,
            Editable = 0x2,
            Locked = 0x4,
            Background = 0x8,
            PreferLinked = 0x10,
            Collapsed = 0x20,
            IsRef = 0x40,
        }

        private enum AseLayerType : ushort
        {
            Normal,
            Group,
            Tilemap
        }

        private enum AseCelType : ushort
        {
            RawImage,
            Linked,
            Compressed,
            CompressedTilemap,
        }

        private enum AseType : ushort
        {
            None = 0x0000,

            OldPalette0 = 0x0004,
            OldPalette1 = 0x0011,
            Layer = 0x2004,
            Cel = 0x2005,
            CelExt = 0x2006,
            ClrProfile = 0x2007,
            ExtFile = 0x2008,
            Mask = 0x2016,
            Path = 0x2017,
            Tags = 0x2018,
            Palette = 0x2019,
            UserData = 0x2020,
        }
    }
}
