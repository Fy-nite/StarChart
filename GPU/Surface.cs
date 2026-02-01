using System;

namespace VBlank.GPU
{
    public class Surface
    {
        public int Width { get; }
        public int Height { get; }

        // Pixel format: 0xAARRGGBB
        public uint[] Pixels { get; }

        public Surface(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
            Pixels = new uint[width * height];
        }

        public void Clear(uint color)
        {
            for (int i = 0; i < Pixels.Length; i++) Pixels[i] = color;
        }

        public void SetPixel(int x, int y, uint color)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return;
            int idx = y * Width + x;
            uint dst = Pixels[idx];
            Pixels[idx] = ColorUtil.Blend(color, dst);
        }

        public uint GetPixel(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return 0;
            return Pixels[y * Width + x];
        }

        public void FillRect(int x, int y, int w, int h, uint color)
        {
            int x0 = Math.Max(0, x);
            int y0 = Math.Max(0, y);
            int x1 = Math.Min(Width, x + w);
            int y1 = Math.Min(Height, y + h);

            for (int yy = y0; yy < y1; yy++)
            {
                int baseIdx = yy * Width;
                for (int xx = x0; xx < x1; xx++)
                {
                    int idx = baseIdx + xx;
                    Pixels[idx] = ColorUtil.Blend(color, Pixels[idx]);
                }
            }
        }

        public void Blit(Surface src, int dstX, int dstY)
        {
            if (src == null) return;

            for (int y = 0; y < src.Height; y++)
            {
                int ty = dstY + y;
                if (ty < 0 || ty >= Height) continue;
                int srcRow = y * src.Width;
                int dstRow = ty * Width;
                for (int x = 0; x < src.Width; x++)
                {
                    int tx = dstX + x;
                    if (tx < 0 || tx >= Width) continue;
                    uint s = src.Pixels[srcRow + x];
                    uint d = Pixels[dstRow + tx];
                    Pixels[dstRow + tx] = ColorUtil.Blend(s, d);
                }
            }
        }

        // Draw a textured quad using nearest sampling and tinting. The texture is drawn
        // into dst coordinates [dstX,dstY]..[dstX+dstW-1,dstY+dstH-1]. Texture coords are 0..1.
        public void DrawTexturedQuad(Surface texture, int dstX, int dstY, int dstW, int dstH, uint tint)
        {
            if (texture == null) return;

            for (int yy = 0; yy < dstH; yy++)
            {
                int ty = dstY + yy;
                if (ty < 0 || ty >= Height) continue;
                int dstRow = ty * Width;
                // v coordinate in texture (0..1)
                float v = (dstH == 0) ? 0f : (yy + 0.5f) / dstH;
                int srcY = Math.Clamp((int)(v * texture.Height), 0, texture.Height - 1);
                for (int xx = 0; xx < dstW; xx++)
                {
                    int tx = dstX + xx;
                    if (tx < 0 || tx >= Width) continue;
                    float u = (dstW == 0) ? 0f : (xx + 0.5f) / dstW;
                    int srcX = Math.Clamp((int)(u * texture.Width), 0, texture.Width - 1);

                    uint s = texture.Pixels[srcY * texture.Width + srcX];
                    // apply tint by multiplying channels
                    uint tinted = ColorUtil.Multiply(s, tint);
                    uint d = Pixels[dstRow + tx];
                    Pixels[dstRow + tx] = ColorUtil.Blend(tinted, d);
                }
            }
        }
    }
}
