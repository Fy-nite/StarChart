using System;

namespace VBlank.GPU
{
    public static class ColorUtil
    {
        public static uint FromRgba(byte r, byte g, byte b, byte a = 255)
        {
            return ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
        }

        public static byte A(uint argb) => (byte)(argb >> 24);
        public static byte R(uint argb) => (byte)(argb >> 16);
        public static byte G(uint argb) => (byte)(argb >> 8);
        public static byte B(uint argb) => (byte)(argb);

        // Alpha blend src over dst (simple straight alpha)
        public static uint Blend(uint src, uint dst)
        {
            byte sa = A(src);
            if (sa == 0) return dst;
            if (sa == 255) return src;

            float alpha = sa / 255f;

            byte sr = R(src);
            byte sg = G(src);
            byte sb = B(src);

            byte dr = R(dst);
            byte dg = G(dst);
            byte db = B(dst);
            byte da = A(dst);

            byte outR = (byte)Math.Clamp((int)(sr * alpha + dr * (1 - alpha)), 0, 255);
            byte outG = (byte)Math.Clamp((int)(sg * alpha + dg * (1 - alpha)), 0, 255);
            byte outB = (byte)Math.Clamp((int)(sb * alpha + db * (1 - alpha)), 0, 255);
            byte outA = (byte)Math.Clamp((int)(sa + da * (1 - alpha)), 0, 255);

            return FromRgba(outR, outG, outB, outA);
        }

        // Multiply color channels (including alpha) component-wise, values are 0..255
        public static uint Multiply(uint color, uint tint)
        {
            byte sa = A(color);
            byte sr = R(color);
            byte sg = G(color);
            byte sb = B(color);

            byte ta = A(tint);
            byte tr = R(tint);
            byte tg = G(tint);
            byte tb = B(tint);

            byte outA = (byte)((sa * ta) / 255);
            byte outR = (byte)((sr * tr) / 255);
            byte outG = (byte)((sg * tg) / 255);
            byte outB = (byte)((sb * tb) / 255);

            return FromRgba(outR, outG, outB, outA);
        }
    }
}
