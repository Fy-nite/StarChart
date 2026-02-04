using Adamantite.GFX;
using Adamantite.GPU;
using Adamantite.Util;
using StarChart.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Adamantite.VFS;
using System.Threading;
using System.Threading.Tasks;
using Canvas = Adamantite.GFX.Canvas;
using VBlank;
using Microsoft.Xna.Framework.Input;
using StarChart.PTY;


namespace StarChart
{
    // A simple runtime that hosts a graphics subsystem (like W11)
    // and composites its output onto the provided Adamantite.GFX.Canvas.
    // Ideally W11 logic should be loaded as a plugin, but for now we reference it.

    public interface IScheduledTask
    {
        void Execute(double deltaTime);
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

    public class Runtime : IConsoleGame, IEngineHost
    {
        // Basic host-provided window system for plugins to control simple windows.
        class StarChartWindowSystem : StarChart.Plugins.IStarChartWindowSystem
        {
            public class HostWindow
            {
                public Guid Id { get; } = Guid.NewGuid();
                public string Title { get; set; } = "";
                public int Width { get; set; }
                public int Height { get; set; }
                public object? Content { get; set; }
            }

            private readonly List<HostWindow> _windows = new();

            public event Action<object>? WindowClosed;

            public object OpenWindow(string title, int width, int height)
            {
                var w = new HostWindow { Title = title, Width = width, Height = height };
                _windows.Add(w);
                return w as object;
            }

            public void CloseWindow(object windowHandle)
            {
                if (windowHandle is HostWindow w)
                {
                    if (_windows.Remove(w))
                    {
                        try { WindowClosed?.Invoke(w); } catch { }
                    }
                }
            }

            public void SetTitle(object windowHandle, string title)
            {
                if (windowHandle is HostWindow w) w.Title = title;
            }

            public void AttachContent(object windowHandle, object? content)
            {
                if (windowHandle is HostWindow w) w.Content = content;
            }
        }

        private readonly bool _skipDefaultVt;
        private Scheduler _scheduler = new Scheduler();
        public Scheduler Scheduler => _scheduler;

        Canvas? _surface;
        AsmoGameEngine? _engine;
        
        // Use the abstract graphics subsystem interface
        IGraphicsSubsystem? _graphics;
        public IGraphicsSubsystem? Graphics => _graphics;

        public void SetGraphicsSubsystem(IGraphicsSubsystem graphics)
        {
            _graphics = graphics;
        }

        // If >0, limit how many Draw() logging lines are printed (helpful for noisy runs)
        public static int DrawPrintLimit = 0;
        private int _drawLogCount = 0;

        // VT/PTY for non-graphical mode or fallback
        VirtualTerminal? _vt;
        IPty? _pty;
        
        private readonly List<IStarChartApp> _activeApps = new List<IStarChartApp>();
        private StarChart.Plugins.IStarChartWindowSystem? _windowSystem;
        private object? _windowSystemRoot;
        private bool _pendingGraphicalRequest = false;
        private readonly List<AppRunInfo> _appRunInfos = new();

        private class AppRunInfo
        {
            public IStarChartApp App { get; set; } = null!;
            public Task? Task { get; set; }
            public CancellationTokenSource? Cancellation { get; set; }
        }

        // One-shot scheduled task wrapper for executing an Action on the Scheduler
        private class OneShotTask : IScheduledTask
        {
            private readonly Action _action;
            public bool IsComplete { get; private set; }

            public OneShotTask(Action action)
            {
                _action = action ?? throw new ArgumentNullException(nameof(action));
            }

            public void Execute(double deltaTime)
            {
                if (IsComplete) return;
                try { _action(); } catch { }
                IsComplete = true;
            }
        }

        public Runtime(bool skipDefaultVt = false)
        {
            _skipDefaultVt = skipDefaultVt;
        }

        private void DrawLog(string msg)
        {
            try
            {
                if (Environment.GetEnvironmentVariable("ASMO_DEBUG") != "1") return;
                if (DrawPrintLimit <= 0)
                {
                    DebugUtil.Debug(msg);
                    return;
                }
                if (_drawLogCount < DrawPrintLimit)
                {
                    DebugUtil.Debug(msg);
                    _drawLogCount++;
                }
            }
            catch { }
        }

        public void Init(Canvas surface)
        {
            _surface = surface ?? throw new ArgumentNullException(nameof(surface));
            var vfs = VFSGlobal.Manager;

            if (_skipDefaultVt)
            {
                DebugUtil.Debug("StarChart: Graphical start requested; initializing graphics subsystem.");
                try
                {
                    IGraphicsSubsystem? subsystem = null;

                    // 1. Try to load W11 explicitly if present
                    if (File.Exists("W11.dll"))
                    {
                         try { System.Reflection.Assembly.LoadFrom("W11.dll"); } catch {}
                    }

                    // 2. Scan loaded assemblies for an implementation
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var type = asm.GetTypes().FirstOrDefault(t => typeof(IGraphicsSubsystem).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
                        if (type != null)
                        {
                            subsystem = (IGraphicsSubsystem?)Activator.CreateInstance(type);
                            if (subsystem != null) break;
                        }
                    }

                    if (subsystem != null)
                    {
                        DebugUtil.Debug($"StarChart: Loaded graphics subsystem: {subsystem.GetType().Name}");
                        subsystem.Initialize(this);
                        _graphics = subsystem;
                    }
                    else
                    {
                        DebugUtil.Debug("StarChart: No IGraphicsSubsystem found.");
                    }
                }
                catch (Exception ex)
                {
                    DebugUtil.Debug("StarChart: Failed to start graphics subsystem: " + ex.Message);
                }
            }
            else
            {
                // Create a session manager and start a default session that will run /bin/sh (if present)
                try
                {
                    // Note: SessionManager previously depended on DisplayServer. 
                    // This dependency should be removed or made optional in SessionManager.
                    // We pass null for now (SessionManager needs update).
                    
                    // We need to use Reflection or just fix SessionManager to accept null.
                    // Assuming SessionManager constructor signature is: (DisplayServer, VfsManager, Action<IStarChartApp>?)
                    // We'll pass null for DisplayServer.
                    
                    // Since DisplayServer type moved to StarChart.stdlib.W11.Windowing,
                    // we might need to cast null or update SessionManager first.
                    // But Runtime.cs is compiling against current codebase.
                    
                    var sessionManager = new StarChart.PTY.SessionManager(null, vfs ?? new Adamantite.VFS.VfsManager());
                    var res = sessionManager.CreateSession("root");
                    if (res != null)
                    {
                        _vt = res.Value.vt;
                        _pty = res.Value.pty;
                        AttachVirtualTerminal(_vt);
                        DebugUtil.Debug("StarChart: PTY session started and attached as virtual tty");
                    }
                }
                catch (Exception ex)
                {
                        DebugUtil.Debug("StarChart: failed to create PTY session: " + ex.Message);
                }
            }
        }

        public void Update(double deltaTime)
        {
            // Update scheduler before other kernel logic
            _scheduler.Update(deltaTime);

            // Delegate to graphics subsystem if active
            if (_graphics != null)
            {
                var mouse = Mouse.GetState();
                var keyboard = Keyboard.GetState();
                
                _graphics.ProcessInput(mouse, keyboard);
                _graphics.Update(deltaTime);
            }
            else
            {
                // Fallback / VT logic
                var keyboard = Keyboard.GetState();
                bool shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);

                // If we have a fullscreen virtual terminal, route global keyboard to it
                if (_pty != null)
                {
                    foreach (var key in keyboard.GetPressedKeys())
                    {
                        // Needs debouncing/state tracking if PTY needs key down events.
                        // Assuming basic logic for now.
                        // Ideally pass keyboard state to a proper input handler.
                        // _pty.HandleKey(key, shift); // TODO: implement proper input
                    }
                }
            }
        }

        public void Draw(Canvas surface)
        {
            if (surface == null) return;

            if (_graphics != null)
            {
                _graphics.Render(surface);
            }
            else
            {
                // Fallback: clear the provided surface
                surface.Clear(Microsoft.Xna.Framework.Color.Black);

                // If a fullscreen virtual terminal is attached, render it directly.
                if (_vt != null)
                {
                    _vt.RenderToCanvas(surface);
                }
            }
        }

        // IEngineHost implementation
        public void SetEngine(AsmoGameEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public void SetPresentationScale(float scale)
        {
            if (_engine == null) return;
            _engine.SetScale(scale);
        }

        /// <summary>
        /// Replace the current window system with an external implementation.
        /// If `rootHandle` is provided the runtime will pass it to the external system
        /// as the host root window (semantics are host-defined).
        /// </summary>
        public void SetWindowSystem(StarChart.Plugins.IStarChartWindowSystem? newSystem, object? rootHandle = null)
        {
            _windowSystem = newSystem;
            _windowSystemRoot = rootHandle;
        }

        public StarChart.Plugins.IStarChartWindowSystem? CurrentWindowSystem => _windowSystem;

        /// <summary>
        /// Request that the runtime start or switch to graphical mode immediately if possible.
        /// Plugins should call this through `PluginContext.HostServices.RequestGraphicalStart()`.
        /// </summary>
        public void RequestGraphicalStart()
        {
            try
            {
                StarChart.ShellControl.RequestGraphicalStart();
                if (_graphics != null) return; // already running

                if (_surface == null)
                {
                    // Defer until surface is available
                    _pendingGraphicalRequest = true;
                    return;
                }

                // Try to load/initialize graphics subsystem similar to Init()
                IGraphicsSubsystem? subsystem = null;
                if (File.Exists("W11.dll"))
                {
                    try { System.Reflection.Assembly.LoadFrom("W11.dll"); } catch { }
                }

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = asm.GetTypes().FirstOrDefault(t => typeof(IGraphicsSubsystem).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
                    if (type != null)
                    {
                        subsystem = (IGraphicsSubsystem?)Activator.CreateInstance(type);
                        if (subsystem != null) break;
                    }
                }

                if (subsystem != null)
                {
                    try
                    {
                        subsystem.Initialize(this);
                        _graphics = subsystem;
                        DebugUtil.Debug($"StarChart: Graphics subsystem started via RequestGraphicalStart: {_graphics.GetType().Name}");
                    }
                    catch (Exception ex)
                    {
                        DebugUtil.Debug("StarChart: Failed to initialize graphics subsystem: " + ex.Message);
                    }
                }
                else
                {
                    DebugUtil.Debug("StarChart: No IGraphicsSubsystem found when requesting graphical start.");
                }
            }
            catch (Exception ex)
            {
                DebugUtil.Debug("StarChart: RequestGraphicalStart failed: " + ex.Message);
            }
        }

        public void RegisterApp(IStarChartApp app)
        {
            if (app == null) return;
            
            if (_windowSystem == null) _windowSystem = new StarChartWindowSystem();

            var ctx = new PluginContext 
            {
                VFS = Adamantite.VFS.VFSGlobal.Manager,
                Arguments = Array.Empty<string>(),
                WindowingContext = _windowSystem, 
                Graphics = _graphics,
                Scheduler = _scheduler
            };
            
            // Provide host services to plugins so they can request graphical start
            ctx.HostServices = new HostServices(this);
            
            try
            {
                // Initialize on the host/main thread
                app.Initialize(ctx);

                // Start the app on a background task so multiple apps can run concurrently.
                var cts = new CancellationTokenSource();
                var runInfo = new AppRunInfo { App = app, Cancellation = cts };
                runInfo.Task = Task.Run(() =>
                {
                    try
                    {
                        app.Start();
                    }
                    catch (Exception ex)
                    {
                        DebugUtil.Debug($"App threw in Start(): {ex.Message}");
                    }
                }, cts.Token);

                _appRunInfos.Add(runInfo);
                _activeApps.Add(app);
            }
            catch (Exception ex)
            {
                DebugUtil.Debug($"Failed to register app {app}: {ex.Message}");
            }
        }

        // Attach a fullscreen VirtualTerminal (TTY) to the runtime.
        public void AttachVirtualTerminal(VirtualTerminal vt)
        {
            _vt = vt;
        }
        
        // Host services implementation exposed to plugins
        private class HostServices : StarChart.Plugins.IHostServices
        {
            private readonly Runtime _runtime;
            public HostServices(Runtime runtime)
            {
                _runtime = runtime;
            }

            public void RequestGraphicalStart()
            {
                _runtime.RequestGraphicalStart();
            }

            public void SetWindowSystem(StarChart.Plugins.IStarChartWindowSystem? system, object? rootHandle = null)
            {
                _runtime.SetWindowSystem(system, rootHandle);
            }

            public StarChart.Plugins.IStarChartWindowSystem? CurrentWindowSystem => _runtime.CurrentWindowSystem;
            public void InvokeOnMainThread(Action action)
            {
                if (action == null) return;
                _runtime.Scheduler.AddTask(new OneShotTask(action));
            }
        }
        
    }
}
