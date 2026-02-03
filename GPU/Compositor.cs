using System;
using System.Collections.Generic;
using StarChart.stdlib.W11;

namespace Adamantite.GPU
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
                try
                {
                    Console.Error.WriteLine($"Compositor: window '{w.Title}' id={w.XID} mapped={w.IsMapped} dirty={c.IsDirty} size={c.Width}x{c.Height}");
                }
                catch { }

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

                    // For debugging: if this is the grid-test window, force a full copy
                    if (string.Equals(w.Title, "grid-test", StringComparison.OrdinalIgnoreCase))
                    {
                        dx = 0; dy = 0; dw = c.Width; dh = c.Height;
                        try { Console.Error.WriteLine($"Compositor: forcing full copy for window id={w.XID} title={w.Title}"); } catch { }
                    }

                    // Diagnostic: sample raw canvas bytes at first/mid/last positions so we can
                    // compare the source bytes with what we store into surf.Pixels.
                    try
                    {
                        if (buf != null && buf.Length >= 4)
                        {
                            int fx = 0, fy = 0;
                            int mx = Math.Max(0, c.Width / 2), my = Math.Max(0, c.Height / 2);
                            int lx = Math.Max(0, c.Width - 1), ly = Math.Max(0, c.Height - 1);
                            int fs = (fy * cw + fx) * 4;
                            int ms = (my * cw + mx) * 4;
                            int ls = (ly * cw + lx) * 4;
                            if (fs + 3 < buf.Length && ms + 3 < buf.Length && ls + 3 < buf.Length)
                            {
                                byte f0 = buf[fs + 0]; byte f1 = buf[fs + 1]; byte f2 = buf[fs + 2]; byte f3 = buf[fs + 3];
                                byte m0 = buf[ms + 0]; byte m1 = buf[ms + 1]; byte m2 = buf[ms + 2]; byte m3 = buf[ms + 3];
                                byte l0 = buf[ls + 0]; byte l1 = buf[ls + 1]; byte l2 = buf[ls + 2]; byte l3 = buf[ls + 3];
                                Console.Error.WriteLine($"Compositor: canvas-bytes id={w.XID} first=[{f0:X2},{f1:X2},{f2:X2},{f3:X2}] mid=[{m0:X2},{m1:X2},{m2:X2},{m3:X2}] last=[{l0:X2},{l1:X2},{l2:X2},{l3:X2}]");
                            }
                        }
                    }
                    catch { }

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

                // Sample the cached surface so we can see what was copied from the client
                try
                {
                    if (surf != null && surf.Pixels != null && surf.Pixels.Length > 0)
                    {
                        uint sf = surf.Pixels[0];
                        uint sm = surf.Pixels[surf.Pixels.Length / 2];
                        uint sl = surf.Pixels[surf.Pixels.Length - 1];
                        Console.Error.WriteLine($"Compositor: surf-sample id={w.XID} first=0x{sf:X8} mid=0x{sm:X8} last=0x{sl:X8}");
                    }
                }
                catch { }

                // Draw into target. Respect window scale (onscreen size = canvas size * scale)
                int scale = Math.Max(1, w.Scale);
                int dstW = c.Width * scale;
                int dstH = c.Height * scale;
                // Simple direct copy for scale=1 opaque windows (faster and avoids blend bugs)
                if (scale == 1)
                {
                    int x0 = Math.Max(0, w.Geometry.X);
                    int y0 = Math.Max(0, w.Geometry.Y);
                    int x1 = Math.Min(target.Width, w.Geometry.X + dstW);
                    int y1 = Math.Min(target.Height, w.Geometry.Y + dstH);
                    for (int y = y0; y < y1; y++)
                    {
                        int srcY = y - w.Geometry.Y;
                        if (srcY < 0 || srcY >= surf.Height) continue;
                        int srcRow = srcY * surf.Width;
                        int dstRow = y * target.Width;
                        for (int x = x0; x < x1; x++)
                        {
                            int srcX = x - w.Geometry.X;
                            if (srcX < 0 || srcX >= surf.Width) continue;
                            target.Pixels[dstRow + x] = surf.Pixels[srcRow + srcX];
                        }
                    }
                }
                else
                {
                    target.DrawTexturedQuad(surf, w.Geometry.X, w.Geometry.Y, dstW, dstH, 0xFFFFFFFFu);
                }
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
