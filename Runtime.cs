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
using Adamantite.GPU;
using Microsoft.Xna.Framework.Graphics;
using StarChart.PTY;
using StarChart.Plugins;

namespace StarChart
{
    // A simple runtime that hosts a W11 DisplayServer and composites
    // its windows onto the provided Adamantite.GFX.Canvas.
    // Simple IScheduledTask interface for scheduled jobs
    public interface IScheduledTask
    {
        // Called by the scheduler when the task should run
        void Execute(double deltaTime);
        // Optionally, return true if the task should be removed after execution
        bool IsComplete { get; }
    }

    // Basic round-robin scheduler
    public class Scheduler
    {
        private readonly List<IScheduledTask> _tasks = new();

        public void AddTask(IScheduledTask task)
        {
            if (task != null) _tasks.Add(task);
        }

        public void Update(double deltaTime)
        {
            for (int i = _tasks.Count - 1; i >= 0; i--)
            {
                var task = _tasks[i];
                task.Execute(deltaTime);
                if (task.IsComplete)
                    _tasks.RemoveAt(i);
            }
        }
    }

    public class Runtime : IConsoleGameWithSpriteBatch, IEngineHost
    {
        private readonly bool _skipDefaultVt;
        DisplayServer? _server;
        private Scheduler _scheduler = new Scheduler();
            // Expose the scheduler for external modules
            public Scheduler Scheduler => _scheduler;
        Canvas? _surface;
        AsmoGameEngine? _engine;
        Compositor _compositor = new Compositor();
        Surface? _mainSurface;
        // If >0, limit how many Draw() logging lines are printed (helpful for noisy runs)
        public static int DrawPrintLimit = 0;
        private int _drawLogCount = 0;
        // Track whether the debug grid test window was created to avoid flooding logs
        bool _gridTestCreated = false;
        TwmManager? _twm;
        bool _prevLeftDown;
        List<IStarChartApp> _apps = new();
        KeyboardState _prevKeyboard;

        // New: single virtual terminal for fullscreen TTY mode
        VirtualTerminal? _vt;
        IPty? _pty;

        // Expose the running TWM instance so callers can wrap/unwrap client windows.
        public TwmManager? Twm => _twm;

        // Expose the server so external systems can create/manipulate windows.
        public DisplayServer? Server => _server;

        public void RegisterApp(IStarChartApp app)
        {
            if (app == null) return;
            lock (_apps)
            {
                if (!_apps.Contains(app))
                {
                    _apps.Add(app);
                    // If the app is also a scheduled task, add it to the scheduler
                    if (app is IScheduledTask task)
                    {
                        _scheduler.AddTask(task);
                    }
                }
            }
        }

        public Runtime(bool skipDefaultVt = false)
        {
            _skipDefaultVt = skipDefaultVt;
        }

        private void DrawLog(string msg)
        {
            if (DrawPrintLimit <= 0)
            {
                Console.Error.WriteLine(msg);
                return;
            }
            if (_drawLogCount < DrawPrintLimit)
            {
                Console.Error.WriteLine(msg);
                _drawLogCount++;
            }
        }

        public void Init(Canvas surface)
        {
            _surface = surface ?? throw new ArgumentNullException(nameof(surface));


            // create the display server
            try
            {
                _server = new DisplayServer();
                Console.Error.WriteLine("StarChart: DisplayServer created");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("StarChart: Failed to create DisplayServer: " + ex.Message);
                throw;
            }

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
                        Console.Error.WriteLine($"StarChart: Winit line: '{line}'");
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 0) continue;
                        if (parts[0].Equals("twm", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                _twm = new TwmManager(_server);
                                Console.Error.WriteLine("StarChart: twm started from Winit");
                                startedAny = true;
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine("StarChart: failed to start twm from Winit: " + ex.Message);
                            }
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
                            RegisterApp(xt);
                            try { var sh = new Shell(xt); } catch { }
                            Console.Error.WriteLine($"StarChart: created xterm at {x},{y} size {cols}x{rows} scale {scale}");
                            startedAny = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("StarChart: failed reading /etc/W11/Winit: " + ex.Message);
                }
            }

            // If the runtime was instructed to skip the default VT session (e.g. the
            // user ran `startx`/`startw`), then start the W11 windowing environment
            // instead of creating a fullscreen virtual terminal.
            if (_skipDefaultVt)
            {
                Console.Error.WriteLine("StarChart: Graphical start requested; skipping default virtual terminal.");
                try
                {
                    StartW11();
                    Console.Error.WriteLine("StarChart: W11 started due to graphical request.");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("StarChart: failed to start W11 on graphical request: " + ex.Message);
                }
            }
            else
            {
                // Create a session manager and start a default session that will run /bin/sh (if present)
                try
                {
                    var sessionManager = new StarChart.PTY.SessionManager(_server, vfs ?? new Adamantite.VFS.VfsManager());
                    var res = sessionManager.CreateSession("root");
                    if (res != null)
                    {
                        _vt = res.Value.vt;
                        _pty = res.Value.pty;
                        // Attach VT to runtime for rendering/input routing
                        AttachVirtualTerminal(_vt);
                        Console.Error.WriteLine("StarChart: PTY session started and attached as virtual tty");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("StarChart: failed to create PTY session: " + ex.Message);
                }
            }


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
                        RegisterApp(xterm);

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
        // Example: Register a demo scheduled task (remove or replace in production)
        // _scheduler.AddTask(new DemoScheduledTask());

        // Start the default W11 environment: default xterm(s), optional twm, and per-user shells.
        public void StartW11()
        {
            if (_server == null) throw new InvalidOperationException("Display server not initialized");

            // Create a primary xterm and attach interactive shell
            var xt1 = new XTerm(_server, "xterm", "XTerm", 80, 20, 10, 10, 2, fontKind: XTerm.FontKind.Classic5x7);
            xt1.Render();
            RegisterApp(xt1);

            Console.Error.WriteLine("StarChart: StartW11: primary xterm created and registered");

            try
            {
                var sh = new Shell(xt1);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("StarChart: failed to attach Shell to xterm: " + ex.Message);
            }

            // If /etc/passwd exists, spawn per-user xterms
            var vfs = VFSGlobal.Manager;
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
                        var parts = pline.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
                        var uname = parts.Length > 0 ? parts[0] : string.Empty;
                        if (string.IsNullOrEmpty(uname)) continue;

                        int x = 10 + (pidx % 3) * 260;
                        int y = 10 + (pidx / 3) * 180;
                        var xterm = new XTerm(_server, "xterm", uname, 80, 24, x, y, 1, fontKind: XTerm.FontKind.Clean8x8);
                        xterm.Render();

                        RegisterApp(xterm);

                        Console.Error.WriteLine($"StarChart: StartW11: spawned user xterm '{uname}' at {x},{y}");

                        try
                        {
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
            // Update scheduler before other kernel logic
            _scheduler.Update(deltaTime);
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
                var fx = _apps.OfType<XTerm>().FirstOrDefault(x => x.Window == focused);
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

            // --- Test helper: spawn a small grid window to verify compositor/display ---
            if (!_gridTestCreated)
            {
                try
                {
                    var gwGeom = new WindowGeometry(220, 10, 200, 160);
                    var gw = _server.CreateWindow("grid-test", gwGeom, WindowStyle.Titled, 1);
                    if (gw != null)
                    {
                        // draw a simple grid pattern into the window canvas
                        var c = gw.Canvas;
                        if (c != null)
                        {
                            // clear to light gray
                            c.Clear(0xFFCCCCCC);
                            // draw dark lines every 16 pixels
                            for (int y = 0; y < c.Height; y++)
                            {
                                for (int x = 0; x < c.Width; x++)
                                {
                                    if (x % 16 == 0 || y % 16 == 0)
                                    {
                                        c.SetPixel(x, y, 0xFF444444);
                                    }
                                }
                            }
                        }
                        gw.Map();
                        Console.Error.WriteLine($"Runtime.Update: created grid-test window (mapped) at {gw.Geometry.X},{gw.Geometry.Y} size {gw.Canvas.Width}x{gw.Canvas.Height}");
                        _gridTestCreated = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("Runtime.Update: failed to create grid-test window: " + ex.Message);
                }
            }

            // If we have a fullscreen virtual terminal, route global keyboard to it when no TWM/window-manager.
            if (_pty != null && activeTwm == null)
            {
                foreach (var key in keyboard.GetPressedKeys())
                {
                    if (!_prevKeyboard.IsKeyDown(key))
                    {
                        _pty.HandleKey(key, shift);
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
                    var xt = _apps.OfType<XTerm>().FirstOrDefault(x => x.Window == w);
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
                    var xt = _apps.OfType<XTerm>().FirstOrDefault(x => x.Window == client);
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
            if (surface == null) return;

            DrawLog($"Runtime.Draw: surface={surface.width}x{surface.height} serverWindows={_server?.Windows.Count ?? 0}");

            // If we have a DisplayServer and windows, use the compositor to draw them.
            if (_server != null && _server.Windows.Count > 0)
            {
                // Ensure main surface matches the canvas size
                if (_mainSurface == null || _mainSurface.Width != surface.width || _mainSurface.Height != surface.height)
                {
                    _mainSurface = new Surface(surface.width, surface.height);
                    DrawLog($"Runtime.Draw: created _mainSurface {surface.width}x{surface.height}");
                }

                // Composite all windows into the surface
                _compositor.Compose(_server, _mainSurface);
                DrawLog("Runtime.Draw: compositor composed into _mainSurface");

                // TEMP DEBUG: mark first, middle, and last pixels so we can verify the
                // compositor -> Canvas -> Texture upload/presentation path.
                try
                {
                    if (_mainSurface.Pixels != null && _mainSurface.Pixels.Length > 0)
                    {
                        uint testColor = 0xFFFF00FFu; // ARGB magenta (A=FF,R=FF,G=00,B=FF)
                        int len = _mainSurface.Pixels.Length;
                        _mainSurface.Pixels[0] = testColor;
                        _mainSurface.Pixels[len / 2] = testColor;
                        _mainSurface.Pixels[len - 1] = testColor;

                        uint first = _mainSurface.Pixels[0];
                        int midIdx = len / 2;
                        uint mid = _mainSurface.Pixels[midIdx];
                        uint last = _mainSurface.Pixels[len - 1];
                        DrawLog($"Runtime.Draw: pixel-sample first=0x{first:X8} mid=0x{mid:X8} last=0x{last:X8}");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("Runtime.Draw: failed sampling/_marking _mainSurface pixels: " + ex.Message);
                }

                // Transfer surface pixels to the MonoGame canvas
                for (int i = 0; i < _mainSurface.Pixels.Length; i++)
                {
                    uint p = _mainSurface.Pixels[i];
                    surface.PixelData[i] = new Microsoft.Xna.Framework.Color(
                        (byte)((p >> 16) & 0xFF),
                        (byte)((p >> 8) & 0xFF),
                        (byte)(p & 0xFF),
                        (byte)((p >> 24) & 0xFF)
                    );
                }
                DrawLog("Runtime.Draw: copied _mainSurface pixels to Canvas.PixelData");
            }
            else
            {
                // Fallback: clear the provided surface
                surface.Clear(Microsoft.Xna.Framework.Color.Black);
                DrawLog("Runtime.Draw: no windows to composite; cleared canvas");

                // If a fullscreen virtual terminal is attached, render it directly.
                if (_vt != null)
                {
                    _vt.RenderToCanvas(surface);
                    DrawLog("Runtime.Draw: rendered fullscreen VirtualTerminal to canvas");
                }
            }
        }

        public void Draw(SpriteBatch sb, SpriteFont font, float presentationScale)
        {
            if (_vt != null && _surface != null)
            {
                float scaleX = (_surface.width * presentationScale) / (float)(_vt.Columns * font.MeasureString(" ").X);
                float scaleY = (_surface.height * presentationScale) / (float)(_vt.Rows * font.LineSpacing);
                float scale = Math.Min(scaleX, scaleY);
                int x = (int)((_surface.width * presentationScale - _vt.Columns * font.MeasureString(" ").X * scale) / 2);
                int y = (int)((_surface.height * presentationScale - _vt.Rows * font.LineSpacing * scale) / 2);
                _vt.Draw(sb, font, x, y, scale);
            }
        }

        void DrawWindowRegion(Window w, int rectX, int rectY, int rectW, int rectH)
        {
            // No-op minimal implementation kept for compatibility during refactor.
            return;
        }
        void ClearRect(int x, int y, int w, int h)
        {
            // No-op minimal implementation kept for compatibility during refactor.
            return;
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

        // Attach a fullscreen VirtualTerminal (TTY) to the runtime. When set,
        // the runtime will route keyboard input to the VT when no window manager
        // is active and will render it directly to the canvas when appropriate.
        public void AttachVirtualTerminal(VirtualTerminal vt)
        {
            _vt = vt;
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
