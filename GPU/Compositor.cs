using System;
using System.Collections.Generic;
using StarChart.stdlib.W11;

namespace VBlank.GPU
{
    // Simple compositor that converts W11 Window canvases into Surfaces and
    // draws them into a target Surface in server window order (already sorted by layer).
    public class Compositor
    {
        readonly Dictionary<uint, Surface> _cache = new();

        // Compose the current DisplayServer windows into the provided target Surface.
        // Only updates offscreen Surfaces when the source W11 Canvas is dirty
        // to avoid re-copying large unchanged windows (e.g. the background) every frame.
        public void Compose(DisplayServer server, Surface target)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (target == null) throw new ArgumentNullException(nameof(target));
            // Clear to transparent black so lower layers (like a background window) will draw first.
            target.Clear(0x00000000);

            foreach (var w in server.Windows)
            {
                if (w == null) continue;
                if (!w.IsMapped || w.IsDestroyed) continue;

                var c = w.Canvas;
                if (c == null) continue;

                // ensure we have a cached surface for this window's render id
                if (!_cache.TryGetValue(w.RenderId, out var surf) || surf.Width != c.Width || surf.Height != c.Height)
                {
                    surf = new Surface(c.Width, c.Height);
                    _cache[w.RenderId] = surf;
                    // newly created surf -- force full copy
                    c.MarkDirtyRect(0, 0, c.Width, c.Height);
                }

                // Only copy when the W11 canvas has dirtied regions to avoid touching large
                // canvases (like a static background) every frame which can cause visible flicker.
                if (c.IsDirty)
                {
                    int dx = c.DirtyX;
                    int dy = c.DirtyY;
                    int dw = c.DirtyWidth;
                    int dh = c.DirtyHeight;
                    var buf = c.PixelBuffer;
                    int cw = c.Width;

                    for (int yy = 0; yy < dh; yy++)
                    {
                        int srcRow = (dy + yy) * cw * 4;
                        int dstRow = (dy + yy) * cw;
                        for (int xx = 0; xx < dw; xx++)
                        {
                            int sx = srcRow + (dx + xx) * 4;
                            int di = dstRow + (dx + xx);
                            byte a = buf[sx + 0];
                            byte r = buf[sx + 1];
                            byte g = buf[sx + 2];
                            byte b = buf[sx + 3];
                            surf.Pixels[di] = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
                        }
                    }

                    c.ClearDirty();
                }

                // Draw into target. Respect window scale (onscreen size = canvas size * scale)
                int scale = Math.Max(1, w.Scale);
                int dstW = c.Width * scale;
                int dstH = c.Height * scale;
                target.DrawTexturedQuad(surf, w.Geometry.X, w.Geometry.Y, dstW, dstH, 0xFFFFFFFFu);
            }

            // Optionally purge cache entries for windows that no longer exist
            var valid = new HashSet<uint>();
            foreach (var w in server.Windows) if (w != null) valid.Add(w.RenderId);
            var toRemove = new List<uint>();
            foreach (var k in _cache.Keys) if (!valid.Contains(k)) toRemove.Add(k);
            foreach (var k in toRemove) _cache.Remove(k);
        }
    }
}
