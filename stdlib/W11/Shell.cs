using System;
using Adamantite.VFS;
using Adamantite.GPU;
using StarChart.Plugins;

namespace StarChart.stdlib.W11
{
    // Very small interactive shell that hooks into XTerm's OnEnter event.
    public class Shell
    {
        readonly XTerm _term;
        readonly VirtualTerminal? _vterm;
        TwmManager? _twm;
        readonly PluginLoader _pluginLoader;
        IStarChartPlugin? _currentPlugin;

        public Shell(XTerm term)
        {
            _term = term ?? throw new ArgumentNullException(nameof(term));
            _vterm = null;
            _term.OnEnter += OnEnter;
            _pluginLoader = new PluginLoader(VFSGlobal.Manager);
        }

        // New ctor to support VirtualTerminal-based sessions (fullscreen TTY)
        public Shell(VirtualTerminal vterm)
        {
            _vterm = vterm ?? throw new ArgumentNullException(nameof(vterm));
            _term = null!; // unused in VT mode
            _vterm.OnEnter += OnEnter;
            _pluginLoader = new PluginLoader(VFSGlobal.Manager);
        }

        void OnEnter(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            var cmd = line.Trim();

            // allow running relative executables like "./app.dll"
            if (cmd.StartsWith("./"))
            {
                var path = cmd.Substring(2);
                if (!path.StartsWith("/")) path = "/" + path;

                // Check if it's a DLL - use plugin loader
                if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    LoadPluginFromPath(path);
                    return;
                }

                // Otherwise, use assembly host for .asm files
                var host = new StarChart.Assembly.StarChartAssemblyHost();
                host.Call("exec:" + path, new StarChart.Assembly.AssemblyRuntimeContext());
                return;
            }

            // start a minimal TWM window manager that will wrap mapped windows
            if (string.Equals(cmd, "startwm", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cmd, "start_twm", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cmd, "twm", StringComparison.OrdinalIgnoreCase))
            {
                var server = _term?.Window?.Owner;
                if (server == null)
                {
                    WriteLine("startwm: no display server available");
                    return;
                }
                // If the server already has a registered WM, report an error to the user.
                if (server.WindowManager != null)
                {
                    WriteLine("startwm: a window manager is already running");
                    return;
                }

                try
                {
                    _twm = new TwmManager(server);
                    WriteLine("startwm: twm started");
                }
                catch (Exception ex)
                {
                    WriteLine("startwm: failed to start twm: " + ex.Message);
                }
                return;
            }

            if (string.Equals(cmd, "stopwm", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cmd, "stop_twm", StringComparison.OrdinalIgnoreCase))
            {
                if (_twm == null)
                {
                    WriteLine("stopwm: twm not running");
                    return;
                }
                try
                {
                    _twm.Stop();
                }
                catch { }
                _twm = null;
                WriteLine("stopwm: twm stopped");
                return;
            }
            // basic builtins: ls, cat, run, exec
            if (cmd.StartsWith("ls"))
            {
                var parts = cmd.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                var path = parts.Length > 1 ? parts[1] : "/";
                if (VFSGlobal.Manager == null)
                {
                    WriteLine("VFS not initialized");
                    return;
                }
                try
                {
                    foreach (var fi in VFSGlobal.Manager.Enumerate(path))
                    {
                        WriteLine((fi.IsDirectory ? "d" : "-") + " " + fi.Path + " " + fi.Length);
                    }
                }
                catch (Exception ex)
                {
                    WriteLine("ls: " + ex.Message);
                }
                return;
            }

            if (cmd.StartsWith("cat "))
            {
                var path = cmd.Substring(4).Trim();
                if (VFSGlobal.Manager == null)
                {
                    WriteLine("VFS not initialized");
                    return;
                }
                try
                {
                    var bytes = VFSGlobal.Manager.ReadAllBytes(path);
                    var s = System.Text.Encoding.UTF8.GetString(bytes);
                    WriteLine(s);
                }
                catch (Exception ex)
                {
                    WriteLine("cat: " + ex.Message);
                }
                return;
            }

            if (cmd.StartsWith("run "))
            {
                var path = cmd.Substring(4).Trim();
                var host = new StarChart.Assembly.StarChartAssemblyHost();
                // run via host exec:
                host.Call("exec:" + path, new StarChart.Assembly.AssemblyRuntimeContext());
                return;
            }

            WriteLine($"Unknown command: {cmd}");
        }

        void LoadPluginFromPath(string path)
        {
            try
            {
                // First, discover what kind of plugin it is
                var discovery = _pluginLoader.DiscoverPlugin(path);
                if (discovery == null)
                {
                    WriteLine($"Error: {path} is not a valid StarChart plugin");
                    WriteLine("Plugins must have one of: [StarChartApp], [StarChartWindowManager],");
                    WriteLine("  [StarChartDesktopEnvironment], [StarChartGame], [StarChartService]");
                    return;
                }

                WriteLine($"Discovered: {discovery.Name} ({discovery.Kind}) v{discovery.Version}");
                if (!string.IsNullOrEmpty(discovery.Description))
                {
                    WriteLine($"  {discovery.Description}");
                }

                var server = _term?.Window?.Owner;
                
                // Special handling for window managers
                if (discovery.Kind == PluginKind.WindowManager)
                {
                    if (server == null)
                    {
                        WriteLine("Error: No display server available");
                        return;
                    }

                    if (server.WindowManager != null)
                    {
                        WriteLine("Error: A window manager is already running");
                        WriteLine("Stop it first with 'stopwm'");
                        return;
                    }
                }

                // Create plugin context
                var context = new PluginContext
                {
                    DisplayServer = server,
                    VFS = VFSGlobal.Manager,
                    Arguments = Array.Empty<string>(),
                    WorkingDirectory = "/"
                };

                // Load and initialize
                var plugin = _pluginLoader.LoadPlugin(path, context);
                if (plugin == null)
                {
                    WriteLine("Error: Failed to load plugin");
                    return;
                }

                // Start the plugin
                plugin.Start();
                _currentPlugin = plugin;

                WriteLine($"Started: {discovery.Name}");
                WriteLine("Press Ctrl+C or type 'stop' to stop the plugin");
            }
            catch (Exception ex)
            {
                WriteLine($"Error loading plugin: {ex.Message}");
            }
        }

        void WriteLine(string s)
        {
            try
            {
                if (_vterm != null) _vterm.WriteLine(s);
                else _term.WriteLine(s);
            }
            catch { }
        }
    }
}
