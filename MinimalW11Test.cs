using Adamantite.GFX;
using Microsoft.Xna.Framework.Graphics;
using System;
using Adamantite.Util;
namespace StarChart
{
    /// <summary>
    /// Minimal test that directly writes colored rectangles to the canvas
    /// to verify the rendering pipeline works end-to-end.
    /// </summary>
    public class MinimalW11Test : IConsoleGame
    {
        private Canvas? _canvas;
        private bool _initialized = false;

        public void Init(Canvas surface)
        {
            _canvas = surface ?? throw new ArgumentNullException(nameof(surface));
            DebugUtil.Debug($"MinimalW11Test: Init canvas {_canvas.width}x{_canvas.height}");
        }

        public void Update(double deltaTime)
        {
            if (_canvas == null) return;

            // Only draw once to avoid spamming logs
            if (_initialized)
            {
                // Keep marking dirty so uploads continue
                _canvas.MarkDirtyRect(0, 0, _canvas.width, _canvas.height);
                return;
            }
            _initialized = true;

            DebugUtil.Debug("MinimalW11Test: Drawing test rectangles...");

            // Draw a red rectangle (top-left)
            DrawRect(_canvas, 50, 50, 200, 150, 0xFFFF0000); // Red

            // Draw a green rectangle (center)
            DrawRect(_canvas, 400, 300, 200, 150, 0xFF00FF00); // Green

            // Draw a blue rectangle (bottom-right)
            DrawRect(_canvas, 800, 500, 200, 150, 0xFF0000FF); // Blue

            // Draw a yellow rectangle (overlap test)
            DrawRect(_canvas, 300, 200, 150, 100, 0xFFFFFF00); // Yellow

            DebugUtil.Debug("MinimalW11Test: Rectangles drawn. Marking canvas dirty.");
            
            // Verify pixels were written
            try
            {
                var c0 = _canvas.PixelData[0];
                var c1 = _canvas.PixelData[_canvas.PixelData.Length / 2];
                var c2 = _canvas.PixelData[_canvas.PixelData.Length - 1];
                uint v0 = ((uint)c0.A << 24) | ((uint)c0.R << 16) | ((uint)c0.G << 8) | c0.B;
                uint v1 = ((uint)c1.A << 24) | ((uint)c1.R << 16) | ((uint)c1.G << 8) | c1.B;
                uint v2 = ((uint)c2.A << 24) | ((uint)c2.R << 16) | ((uint)c2.G << 8) | c2.B;
                DebugUtil.Debug($"MinimalW11Test: Canvas pixels after draw: first=0x{v0:X8} mid=0x{v1:X8} last=0x{v2:X8}");
                
                // Check a pixel we know we drew (red rect at 50,50)
                int redIdx = 50 * _canvas.width + 50;
                var cRed = _canvas.PixelData[redIdx];
                uint vRed = ((uint)cRed.A << 24) | ((uint)cRed.R << 16) | ((uint)cRed.G << 8) | cRed.B;
                DebugUtil.Debug($"MinimalW11Test: Red pixel at (50,50) idx={redIdx} value=0x{vRed:X8} expected=0xFFFF0000");
            }
            catch (Exception ex)
            {
                DebugUtil.Debug($"MinimalW11Test: Pixel check failed: {ex.Message}");
            }
            
            _canvas.MarkDirtyRect(0, 0, _canvas.width, _canvas.height);
        }

        public void Draw(Canvas surface)
        {
            // Nothing - we draw in Update
        }

        private void DrawRect(Canvas canvas, int x, int y, int w, int h, uint argb)
        {
            byte a = (byte)((argb >> 24) & 0xFF);
            byte r = (byte)((argb >> 16) & 0xFF);
            byte g = (byte)((argb >> 8) & 0xFF);
            byte b = (byte)(argb & 0xFF);

            var color = new Microsoft.Xna.Framework.Color(r, g, b, a);

            for (int py = 0; py < h; py++)
            {
                int ty = y + py;
                if (ty < 0 || ty >= canvas.height) continue;

                for (int px = 0; px < w; px++)
                {
                    int tx = x + px;
                    if (tx < 0 || tx >= canvas.width) continue;

                    int idx = ty * canvas.width + tx;
                    if (idx >= 0 && idx < canvas.PixelData.Length)
                    {
                        canvas.PixelData[idx] = color;
                    }
                }
            }
        }
    }
}
