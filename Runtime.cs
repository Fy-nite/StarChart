using Adamantite.GFX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Adamantite.VFS;
using StarChart.stdlib.W11;
using Canvas = Adamantite.GFX.Canvas;
using AsmoV2;
using Microsoft.Xna.Framework.Input;

namespace StarChart
{
    // A simple runtime that hosts a W11 DisplayServer and composites
    // its windows onto the provided Adamantite.GFX.Canvas.
    public class Runtime : IConsoleGame, IEngineHost
    {
        DisplayServer? _server;
        Canvas? _surface;
        AsmoGameEngine? _engine;
        TwmManager? _twm;
        bool _prevLeftDown;
        List<XTerm> _xterms = new();
        KeyboardState _prevKeyboard;

        // Expose the running TWM instance so callers can wrap/unwrap client windows.
        public TwmManager? Twm => _twm;

        // Expose the server so external systems can create/manipulate windows.
        public DisplayServer? Server => _server;

        public void Init(Canvas surface)
        {
            _surface = surface ?? throw new ArgumentNullException(nameof(surface));


            // create the display server
            _server = new DisplayServer();

            // Read startup configuration from VFS /etc/W11/Winit if available.
            // Supported lines:
            //  - twm
            //  - xterm [cols] [rows] [x] [y] [scale] [fontKind]
            bool startedAny = false;
            var vfs = VFSGlobal.Manager;
            if (vfs != null && vfs.Exists("/etc/W11/Winit"))
            {
                try
                {
                    using var s = vfs.OpenRead("/etc/W11/Winit");
                    using var sr = new StreamReader(s);
                    string? line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 0) continue;
                        if (parts[0].Equals("twm", StringComparison.OrdinalIgnoreCase))
                        {
                            _twm = new TwmManager(_server);
                            startedAny = true;
                        }
                        else if (parts[0].Equals("xterm", StringComparison.OrdinalIgnoreCase))
                        {
                            int cols = parts.Length > 1 ? int.Parse(parts[1]) : 40;
                            int rows = parts.Length > 2 ? int.Parse(parts[2]) : 12;
                            int x = parts.Length > 3 ? int.Parse(parts[3]) : 40;
                            int y = parts.Length > 4 ? int.Parse(parts[4]) : 40;
                            int scale = parts.Length > 5 ? int.Parse(parts[5]) : 1;
                            XTerm.FontKind fk = XTerm.FontKind.Classic5x7;
                            if (parts.Length > 6) Enum.TryParse<XTerm.FontKind>(parts[6], true, out fk);
                            var xt = new XTerm(_server, "xterm", "XTerm", cols, rows, x, y, scale, fk);
                            xt.Render();
                            _xterms.Add(xt);
                            startedAny = true;
                        }
                    }
                }
                catch
                {
                    // ignore malformed winit
                }
            }

            // Default: do not start twm by default. If nothing configured, create a single xterm.
           
                var xt1 = new XTerm(_server, "xterm", "XTerm", 80, 20, 10, 10, 2, fontKind: XTerm.FontKind.Classic5x7);
                xt1.Render();
                _xterms.Add(xt1);

                // Attach a shell to the default xterm so it's interactive
                try
                {
                    var sh = new Shell(xt1);
                }
                catch { }
            

            // If an /etc/passwd exists in the VFS, spawn an XTerm and Shell per user entry
            if (vfs != null && vfs.Exists("/etc/passwd"))
            {
                try
                {
                    using var ps = vfs.OpenRead("/etc/passwd");
                    using var prs = new StreamReader(ps);
                    string? pline;
                    int pidx = 0;
                    while ((pline = prs.ReadLine()) != null)
                    {
                        pline = pline.Trim();
                        if (string.IsNullOrEmpty(pline) || pline.StartsWith("#")) continue;
                        // simple format: username:shell
                        var parts = pline.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
                        var uname = parts.Length > 0 ? parts[0] : string.Empty;
                        var shellName = parts.Length > 1 ? parts[1] : "sh";
                        if (string.IsNullOrEmpty(uname)) continue;

                        int x = 10 + (pidx % 3) * 260;
                        int y = 10 + (pidx / 3) * 180;
                        var xterm = new XTerm(_server, "xterm", uname, 80, 24, x, y, 1, fontKind: XTerm.FontKind.Clean8x8);
                        xterm.Render();
                        _xterms.Add(xterm);

                        try
                        {
                            // attach a Shell to the xterm; Shell currently provides builtins and run/exec
                            var sh = new Shell(xterm);
                        }
                        catch { }

                        pidx++;
                    }
                }
                catch { }
            }
        }

        // IEngineHost implementation - engine will call this when the runtime is hosted.
        public void SetEngine(AsmoGameEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        // Allow callers to change the main presentation scale (VBlank window scaling)
        // at runtime. This forwards to the engine if available.
        public void SetPresentationScale(float scale)
        {
            if (_engine == null) return;
            _engine.SetScale(scale);
        }

        public void Update(double deltaTime)
        {
            // Nothing special for now. Clients can manipulate windows via the DisplayServer API.
            // If a TWM was started from a shell it may have registered itself on the server;
            // prefer the explicit runtime _twm if set, otherwise use the server's WindowManager.
            var activeTwm = _twm ?? _server?.WindowManager;
            if (activeTwm is StarChart.Plugins.IStarChartWindowManager pluginWM)
            {
                pluginWM.Update();
            }
            else if (activeTwm is TwmManager twm)
            {
                twm.Update();
            }
            foreach (var xt in _xterms) xt.Update(deltaTime);

            var mouse = Mouse.GetState();
            float scale = _engine?.PresentationScale ?? 1f;
            if (scale <= 0) scale = 1f;
            int mx = (int)(mouse.X / scale);
            int my = (int)(mouse.Y / scale);
            bool leftDown = mouse.LeftButton == ButtonState.Pressed;
            bool leftPressed = leftDown && !_prevLeftDown;
            bool leftReleased = !leftDown && _prevLeftDown;
            _prevLeftDown = leftDown;

            if (activeTwm is StarChart.Plugins.IStarChartWindowManager pluginWM2)
            {
                pluginWM2.HandleMouse(mx, my, leftDown, leftPressed, leftReleased);
            }
            else if (activeTwm is TwmManager twmMgr)
            {
                twmMgr.HandleMouse(mx, my, leftDown, leftPressed, leftReleased);
            }

            var keyboard = Keyboard.GetState();
            bool shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);

            // Route keyboard input to the focused window if it's an XTerm. Do not default to the
            // first XTerm — behave like X11 with no window manager: keyboard goes to the focused window.
            var focused = _server?.FocusedWindow;
            if (focused != null)
            {
                var fx = _xterms.FirstOrDefault(x => x.Window == focused);
                if (fx != null)
                {
                    foreach (var key in keyboard.GetPressedKeys())
                    {
                        if (!_prevKeyboard.IsKeyDown(key))
                        {
                            fx.HandleKey(key, shift);
                        }
                    }
                }
            }

            // If there's no TWM, emulate X11 behaviour: find the top-most mapped window under
            // the pointer, focus/raise it on click, and forward mouse events to it if we have
            // a handler (e.g. XTerm). This lets multiple terminals/windows be interacted with.
            if (activeTwm == null && _server != null)
            {
                // iterate from top-most to bottom
                for (int i = _server.Windows.Count - 1; i >= 0; i--)
                {
                    var w = _server.Windows[i];
                    if (!w.IsMapped || w.IsDestroyed) continue;

                    int wScale = Math.Max(1, w.Scale);
                    int onscreenW = w.Canvas.Width * wScale;
                    int onscreenH = w.Canvas.Height * wScale;
                    if (mx < w.Geometry.X || my < w.Geometry.Y) continue;
                    if (mx >= w.Geometry.X + onscreenW || my >= w.Geometry.Y + onscreenH) continue;

                    // We hit window `w` — focus/raise on press
                    if (leftPressed)
                    {
                        _server.BringToFront(w);
                        _server.FocusWindow(w);
                    }

                    // If this window corresponds to an XTerm, forward mouse pixels
                    var xt = _xterms.FirstOrDefault(x => x.Window == w);
                    if (xt != null)
                    {
                        int localX = (mx - w.Geometry.X) / wScale;
                        int localY = (my - w.Geometry.Y) / wScale;
                        xt.HandleMousePixel(localX, localY, leftDown, leftPressed, leftReleased);
                    }

                    break;
                }
            }

            // If a TWM is active, it wraps clients in frame windows. Forward pointer
            // events to the wrapped client XTerm when the pointer is inside the client's
            // content area so terminals remain interactive even when framed.
            if (activeTwm is TwmManager activeTwmMgr && _server != null)
            {
                for (int i = _server.Windows.Count - 1; i >= 0; i--)
                {
                    var frame = _server.Windows[i];
                    if (!frame.IsMapped || frame.IsDestroyed) continue;

                    if (!activeTwmMgr.TryGetClientFromFrame(frame, out var client)) continue;
                    if (client == null) continue;

                    int fScale = Math.Max(1, frame.Scale);
                    int titleH = activeTwmMgr.TitlebarHeight * fScale;
                    int contentX = frame.Geometry.X;
                    int contentY = frame.Geometry.Y + titleH;
                    int contentW = client.Canvas.Width * Math.Max(1, client.Scale);
                    int contentH = client.Canvas.Height * Math.Max(1, client.Scale);

                    if (mx < contentX || my < contentY) continue;
                    if (mx >= contentX + contentW || my >= contentY + contentH) continue;

                    // Map into client canvas pixels and forward
                    var xt = _xterms.FirstOrDefault(x => x.Window == client);
                    if (xt != null)
                    {
                        int localX = (mx - contentX) / Math.Max(1, client.Scale);
                        int localY = (my - contentY) / Math.Max(1, client.Scale);
                        xt.HandleMousePixel(localX, localY, leftDown, leftPressed, leftReleased);
                    }

                    break;
                }
            }

            _prevKeyboard = keyboard;
        }

        // Convenience helpers to wrap/unwrap by XID.
        public bool WrapWindow(uint xid)
        {
            if (_server == null || _twm == null) return false;
            var w = _server.GetWindowById(xid);
            if (w == null) return false;
            return _twm.WrapWindow(w);
        }

        public bool UnwrapWindow(uint xid)
        {
            if (_server == null || _twm == null) return false;
            var w = _server.GetWindowById(xid);
            if (w == null) return false;
            return _twm.UnwrapWindow(w);
        }

        public void Draw(Canvas surface)
        {
            if (_server == null || _surface == null) return;

            bool fullRedraw = _server.ConsumeFullRedraw();
            if (fullRedraw)
            {
                _surface.Clear(Microsoft.Xna.Framework.Color.Black);
                foreach (var w in _server.Windows)
                {
                    if (!w.IsMapped || w.IsDestroyed) continue;
                    DrawWindowRegion(w, w.Geometry.X, w.Geometry.Y, w.Canvas.Width * Math.Max(1, w.Scale), w.Canvas.Height * Math.Max(1, w.Scale));
                    w.Canvas.ClearDirty();
                }
                return;
            }

            var dirtyRects = new List<(int x, int y, int w, int h)>();
            foreach (var w in _server.Windows)
            {
                if (!w.IsMapped || w.IsDestroyed) continue;
                if (!w.Canvas.IsDirty) continue;

                int scale = Math.Max(1, w.Scale);
                int x = w.Geometry.X + w.Canvas.DirtyX * scale;
                int y = w.Geometry.Y + w.Canvas.DirtyY * scale;
                int wpx = w.Canvas.DirtyWidth * scale;
                int hpx = w.Canvas.DirtyHeight * scale;
                if (wpx > 0 && hpx > 0)
                {
                    dirtyRects.Add((x, y, wpx, hpx));
                }
                w.Canvas.ClearDirty();
            }

            foreach (var rect in dirtyRects)
            {
                ClearRect(rect.x, rect.y, rect.w, rect.h);
                foreach (var w in _server.Windows)
                {
                    if (!w.IsMapped || w.IsDestroyed) continue;
                    DrawWindowRegion(w, rect.x, rect.y, rect.w, rect.h);
                }
            }
        }

        void DrawWindowRegion(Window w, int rectX, int rectY, int rectW, int rectH)
        {
            int scale = Math.Max(1, w.Scale);
            int winX = w.Geometry.X;
            int winY = w.Geometry.Y;
            int winW = w.Canvas.Width * scale;
            int winH = w.Canvas.Height * scale;

            int ix0 = Math.Max(rectX, winX);
            int iy0 = Math.Max(rectY, winY);
            int ix1 = Math.Min(rectX + rectW, winX + winW);
            int iy1 = Math.Min(rectY + rectH, winY + winH);
            if (ix1 <= ix0 || iy1 <= iy0) return;

            int canvasXStart = Math.Max(0, (ix0 - winX) / scale);
            int canvasYStart = Math.Max(0, (iy0 - winY) / scale);
            int canvasXEnd = Math.Min(w.Canvas.Width, (ix1 - winX + scale - 1) / scale);
            int canvasYEnd = Math.Min(w.Canvas.Height, (iy1 - winY + scale - 1) / scale);

            // Fast path when no scaling is applied (scale == 1): operate directly on arrays
            var srcBuf = w.Canvas.PixelBuffer;
            var dstBuf = _surface.PixelData;
            int dstWidth = _surface.width;
            int srcWidth = w.Canvas.Width;

            if (scale == 1)
            {
                for (int cy = canvasYStart; cy < canvasYEnd; cy++)
                {
                    int sy = winY + cy;
                    if (sy < iy0 || sy >= iy1 || sy < 0 || sy >= _surface.height) continue;

                    int srcRow = cy * srcWidth * 4;
                    int dstRowBase = sy * dstWidth;

                    for (int cx = canvasXStart; cx < canvasXEnd; cx++)
                    {
                        int dstX = winX + cx;
                        if (dstX < ix0 || dstX >= ix1 || dstX < 0 || dstX >= _surface.width) continue;
                        int dstIdx = dstRowBase + dstX;

                        int sidx = srcRow + cx * 4;
                        byte a = srcBuf[sidx + 0];
                        byte r = srcBuf[sidx + 1];
                        byte g = srcBuf[sidx + 2];
                        byte b = srcBuf[sidx + 3];
                        if (a == 255)
                        {
                            dstBuf[dstIdx] = new Microsoft.Xna.Framework.Color(r, g, b, a);
                        }
                        else if (a == 0)
                        {
                            // transparent, do nothing
                        }
                        else
                        {
                            var dst = dstBuf[dstIdx];
                            byte outR = (byte)((r * a + dst.R * (255 - a)) / 255);
                            byte outG = (byte)((g * a + dst.G * (255 - a)) / 255);
                            byte outB = (byte)((b * a + dst.B * (255 - a)) / 255);
                            dstBuf[dstIdx] = new Microsoft.Xna.Framework.Color(outR, outG, outB, (byte)255);
                        }
                    }
                }
                return;
            }

            // General path for scaled blits
            for (int cy = canvasYStart; cy < canvasYEnd; cy++)
            {
                int syBase = winY + cy * scale;
                int srcRow = cy * srcWidth * 4;
                for (int cx = canvasXStart; cx < canvasXEnd; cx++)
                {
                    int sxBase = winX + cx * scale;
                    int sidx = srcRow + cx * 4;
                    byte a = srcBuf[sidx + 0];
                    byte r = srcBuf[sidx + 1];
                    byte g = srcBuf[sidx + 2];
                    byte b = srcBuf[sidx + 3];
                    var col = new Microsoft.Xna.Framework.Color(r, g, b, a);

                    for (int syOff = 0; syOff < scale; syOff++)
                    {
                        int sy = syBase + syOff;
                        if (sy < iy0 || sy >= iy1 || sy < 0 || sy >= _surface.height) continue;
                        int dstRow = sy * dstWidth;
                        for (int sxOff = 0; sxOff < scale; sxOff++)
                        {
                            int sx = sxBase + sxOff;
                            if (sx < ix0 || sx >= ix1 || sx < 0 || sx >= _surface.width) continue;
                            int idx = dstRow + sx;
                            if (a == 255)
                            {
                                dstBuf[idx] = col;
                            }
                            else if (a != 0)
                            {
                                var dst = dstBuf[idx];
                                byte outR = (byte)((r * a + dst.R * (255 - a)) / 255);
                                byte outG = (byte)((g * a + dst.G * (255 - a)) / 255);
                                byte outB = (byte)((b * a + dst.B * (255 - a)) / 255);
                                dstBuf[idx] = new Microsoft.Xna.Framework.Color(outR, outG, outB, (byte)255);
                            }
                        }
                    }
                }
            }
        }

        void ClearRect(int x, int y, int w, int h)
        {
            int x0 = Math.Max(0, x);
            int y0 = Math.Max(0, y);
            int x1 = Math.Min(_surface.width, x + w);
            int y1 = Math.Min(_surface.height, y + h);
            if (x1 <= x0 || y1 <= y0) return;
            for (int yy = y0; yy < y1; yy++)
            {
                int row = yy * _surface.width;
                for (int xx = x0; xx < x1; xx++)
                {
                    _surface.PixelData[row + xx] = Microsoft.Xna.Framework.Color.Black;
                }
            }
        }

        // Convenience wrapper so callers can create windows without accessing Server directly.
        public Window? CreateWindow(string title, WindowGeometry geometry, int scale = 1, bool preserveOnScreenSize = false)
        {
            if (_server == null) return null;
            var w = _server.CreateWindow(title, geometry, WindowStyle.Titled, scale);
            if (preserveOnScreenSize) w.SetScale(scale, preserveOnScreenSize: true);
            return w;
        }

        public bool SetWindowScale(uint xid, int scale, bool preserveOnScreenSize = true)
        {
            if (_server == null) return false;
            return _server.SetWindowScale(xid, scale, preserveOnScreenSize);
        }

        public bool SetWindowOnScreenSize(uint xid, int width, int height)
        {
            if (_server == null) return false;
            return _server.SetWindowOnScreenSize(xid, width, height);
        }

        public bool SetWindowContentSize(uint xid, int width, int height)
        {
            if (_server == null) return false;
            return _server.SetWindowContentSize(xid, width, height);
        }

        bool TryMapMouseToXTerm(XTerm xt, int mx, int my, out int localX, out int localY)
        {
            localX = 0;
            localY = 0;
            if (xt == null) return false;

            var win = xt.Window;
            var x = win.Geometry.X;
            var y = win.Geometry.Y;

            if (_twm != null && _twm.TryGetFrame(win, out var frame))
            {
                x = frame.Geometry.X;
                y = frame.Geometry.Y + _twm.TitlebarHeight * Math.Max(1, frame.Scale);
            }

            if (mx < x || my < y) return false;
            int lx = mx - x;
            int ly = my - y;

            localX = lx / Math.Max(1, win.Scale);
            localY = ly / Math.Max(1, win.Scale);
            return true;
        }
    }
}
