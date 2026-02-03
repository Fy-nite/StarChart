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

        public void RegisterApp(IStarChartApp app)
        {
            if (app == null) return;
            
            var ctx = new PluginContext 
            {
                VFS = Adamantite.VFS.VFSGlobal.Manager,
                Arguments = Array.Empty<string>(),
                WindowingContext = null, 
                Graphics = _graphics
            };
            
            try
            {
                app.Initialize(ctx);
                app.Start();
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
        
    }
}
