using Joonaxii.Collections;
using Joonaxii.IO.Image;
using System;
using System.Collections.Generic;

namespace Joonaxii
{
    public class Atlas
    {
        public RefStack<Region> Regions => _regions;

        public int Width => _width;
        public int Height => _height;

        private RefStack<Region> _regions = new RefStack<Region>();
        private static RefStack<SpriteRect> _scans = new RefStack<SpriteRect>();
        private static ImageData _tempBuffer = default;
        private static RefStack<int> _tempSprites = new RefStack<int>();
        private int _width;
        private int _height;

        public static bool Pack<T>(in AtlasSettings settings, IRefStack<T> sprites, List<Atlas> atlases) where T : ISprite
        {
            if(sprites.Count < 1) { return false; }
            SpriteComparer<T> comparer = new SpriteComparer<T>();         
            comparer.Sprites = sprites;

            int maxSize = settings.MaxSize;
            int rawPadding = settings.Padding;
            int padding = settings.Padding;

            _tempSprites.Reserve(sprites.Count, false);
            for (int i = 0; i < sprites.Count; i++)
            {
                ref T sprt = ref sprites[i];
                if (sprt.ShouldBeInAtlas)
                {
                    _tempSprites.Push(i);
                }
            }
            Array.Sort(_tempSprites.Buffer, 0, _tempSprites.Count, comparer);

            int spriteFloor = 0;
            int count = 0;
            int maxW = 0;
            int maxH= 0;

            Atlas atlas = new Atlas();
            atlas.InitScan(maxSize, rawPadding, _tempSprites, sprites, spriteFloor, ref count, ref maxW, ref maxH);

            int edgeW = 0;
            int edgeH = 0;

            _scans.Clear(false);
            _scans.Reserve(count >> 1, false);

            int xPos = 0;
            int yPos = 0;
            int scanPos = 0;

            int tempW = 0;
            while(spriteFloor < _tempSprites.Count)
            {
                ref SpriteRect scan = ref _scans.Push();
                GetMaxSize(_tempSprites, sprites, spriteFloor, ref tempW, ref maxH);
                padding = maxH < 1 ? FastMath.Max(rawPadding, 1) : rawPadding;

                maxH += padding;
                ++scanPos;

                scan.x = (ushort)(xPos);
                scan.y = (ushort)(yPos);
                scan.width = (ushort)(atlas._width);
                scan.height = (ushort)(FastMath.Min(FastMath.Max(maxH, 1 + padding), atlas._height - yPos));

                while (spriteFloor < _tempSprites.Count && scan.width > padding && scan.height > padding)
                {
                    ref T sprite = ref sprites[_tempSprites[spriteFloor]];
                    if(!atlas.TryInstert(atlases.Count, scanPos, padding, _tempSprites[spriteFloor], ref sprite, ref edgeW, ref edgeH))
                    {
                        break;
                    }
                    ++spriteFloor;
                }

                if(scan.height <= padding || yPos + scan.height > atlas._height)
                {
                    atlas = FlushAtlas(atlas, maxSize, edgeW, edgeH, atlases);
                    atlas.InitScan(maxSize, padding, _tempSprites, sprites, spriteFloor, ref count, ref maxW, ref maxH);

                    edgeW = 0;
                    edgeH = 0;

                    _scans.Clear(false);
                    scanPos = 0;
                    xPos = 0;
                    yPos = 0;
                    continue;
                }

                xPos = 0;
                yPos += scan.height;
            }

            FlushAtlas(atlas, maxSize, edgeW, edgeH, atlases);
            return atlases.Count > 0;
        }

        public void Blit<T>(IRefStack<T> sprites, ref ImageData img) where T : ISprite
        {
            img.Allocate(ImageFormat.RGBA32, _width, _height);
            for (int i = 0; i < _regions.Count; i++)
            {
                CopyPixels(in _regions[i], ref img, sprites);
            }
        }

        private static bool CopyPixels<T>(in Region region, ref ImageData img, IRefStack<T> sprites) where T : ISprite
        {
            ref T sprite = ref sprites[region.source];
            if(!sprite.GetFrameData(ref _tempBuffer))
            {
                return false;
            }

            var rect = sprite.Source;
            var copyMode = ImageData.GetCopyMethod(_tempBuffer.Format, img.Format);

            int dstBpp = ImageData.GetBitsPerPixel(img.Format) >> 3;
            int srcBpp = ImageData.GetBitsPerPixel(_tempBuffer.Format) >> 3;

            int dstScan = dstBpp * img.Width;
            int dstSrcScan = dstBpp * rect.width;
            int srcScan = srcBpp * _tempBuffer.Width;

            int dstP = (region.rect.y * img.Width + region.rect.x) * dstBpp;
            int srcP = (rect.y * _tempBuffer.Width + rect.x) * srcBpp;

            var dst = img.Data;
            var src = _tempBuffer.Data;

  
            for (int y = 0; y < rect.height; y++, dstP += dstScan, srcP += srcScan)
            {
        
                var srcRange = src.Slice(srcP, dstSrcScan);
                var dstRange = dst.Slice(dstP, dstSrcScan);

                copyMode.Invoke(srcRange, dstRange);
            }
            return true;
        }
        
        private static void GetMaxSize<T>(RefStack<int> indices, IRefStack<T> sprites, int floor, ref int width, ref int height) where T : ISprite
        {
            ref T sprt = ref sprites[indices[floor]];

            width = sprt.Source.width;
            height = sprt.Source.height;
        }

        private void InitScan<T>(int maxSize, int padding, RefStack<int> indices, IRefStack<T> sprites, int spriteFloor, ref int count, ref int maxW, ref int maxH) where T : ISprite
        {
            count = sprites.Count - spriteFloor;
            _regions.Clear(false);

            GetMaxSize(indices, sprites, spriteFloor, ref maxW, ref maxH);

            double aspect = FastMath.Max(maxH, 1.0) / FastMath.Max(maxW, 1.0);

            double sqr = FastMath.Max(Math.Floor(Math.Sqrt(count) * aspect), 1.0);
            int wCount = (int)FastMath.Max(count / sqr, 1);

            padding = maxH < 1 ? FastMath.Max(padding, 1) : padding;
            maxW = FastMath.Max(maxW, 8) + padding;
            maxH = FastMath.Max(maxH, 8) + padding;

            _width = Math.Min(maxW * wCount, maxSize);
            _height = maxSize;
        }

        private static Atlas FlushAtlas(Atlas atlas, int maxSize, int edgeW, int edgeH, List<Atlas> atlases, bool createNew = true)
        {
            if (atlas._regions.Count > 0)
            {
                atlas._width = Math.Min(edgeW, maxSize);
                atlas._height = Math.Min(edgeH, maxSize);
                atlases.Add(atlas);
                if (!createNew) { return atlas; }
                atlas = new Atlas();
            }
            atlas._regions.Clear();
            atlas._width = 1;
            atlas._height = 1;
            return atlas;
        }

        private bool TryInstert<T>(int atlasIdx, int current, int padding, int index, ref T sprite, ref int edgeW, ref int edgeH) where T : ISprite
        {
            for (int i = current - 2; i >= 0; i--)
            {
                if(InsertToScan(atlasIdx, ref _scans[i], padding, index, ref sprite, ref edgeW, ref edgeH))
                {
                    return true;
                }
            }
            return current > 0 && InsertToScan(atlasIdx, ref _scans[current - 1], padding, index, ref sprite, ref edgeW, ref edgeH);
        }
        private bool InsertToScan<T>(int atlasIdx, ref SpriteRect scan, int padding, int index, ref T sprite, ref int edgeW, ref int edgeH) where T : ISprite
        {
            var rect = sprite.Source;
            int sprW = rect.width + padding;
            int sprH = rect.height + padding;

            if(scan.width < sprW || scan.height < sprH)
            {
                sprite.Index = -1;
                sprite.AtlasIndex = -1;
                return false;
            }
            sprite.Index = _regions.Count;
            sprite.AtlasIndex = atlasIdx;

            ref Region reg = ref _regions.Push();
            reg.source = index;
            reg.rect = new SpriteRect(scan.x, scan.y, rect.width, rect.height);
            scan.ShrinkX(sprW);

            edgeW = FastMath.Max(edgeW, scan.x);
            edgeH = FastMath.Max(edgeH, scan.y + scan.height);
            return true;
        }

        private class SpriteComparer<T> : IComparer<int> where T : ISprite
        {
            public IRefStack<T> Sprites
            {
                get => _sprites;
                set => _sprites = value;
            }

            private IRefStack<T> _sprites;

            public SpriteComparer()
            {
            }

            public int Compare(int x, int y)
            {
                SpriteRect a = _sprites[x].Source;
                SpriteRect b = _sprites[y].Source;

                uint valueA = (uint)(a.width) | ((uint)(a.height) << 16);
                uint valueB = (uint)(b.width) | ((uint)(b.height) << 16);
                return valueB.CompareTo(valueA);
            }
        }

        public struct Region
        {
            public int source;
            public SpriteRect rect;
        }
        public struct SpriteRect
        {
            public ushort x;
            public ushort y;
            public ushort width;
            public ushort height;

            public SpriteRect(ushort x, ushort y, ushort width, ushort height)
            {
                this.x = x;
                this.y = y;
                this.width = width;
                this.height = height;
            }

            public void ShrinkX(int amount)
            {
                x = (ushort)(x + amount);
                width = (ushort)(width - amount);
            }
        }
        public struct Sprite
        {
            public SpriteRect original;
            public SpriteRect trimmed;
        }
    }

    public interface ISprite
    {
        int AtlasIndex { get; set; }
        int Index { get; set; }
        Atlas.SpriteRect Source { get; }
        bool ShouldBeInAtlas { get; }

        bool GetFrameData(ref ImageData img);
    }

    public struct AtlasSettings
    {
        public int MaxSize
        {
            get => FastMath.Clamp(_maxSize, 64, 8192);
            set => _maxSize = FastMath.Clamp(value, 64, 8192);
        }
        public int Padding
        {
            get => FastMath.Clamp(_padding, 0, 8);
            set => _padding = FastMath.Clamp(value, 0, 8);
        }

        private int _maxSize;
        private int _padding;
    }
}
