using System;

namespace VBlank.GPU
{
    // Wrapper for a source Surface used as a texture resource
    public class Texture
    {
        public Surface Surface { get; }

        public int Width => Surface.Width;
        public int Height => Surface.Height;

        public Texture(Surface surface)
        {
            Surface = surface ?? throw new ArgumentNullException(nameof(surface));
        }
    }
}
