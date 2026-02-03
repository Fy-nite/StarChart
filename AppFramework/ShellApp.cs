using System;
using StarChart.Plugins;
using StarChart.Bin;
using StarChart.PTY;
using Adamantite.VFS;

namespace StarChart.AppFramework
{
    // Adapter that exposes the existing ShProgram as an IStarChartApp.
    // This lets you run the shell as a plugin/application inside StarChart.
    public class ShellApp : IStarChartApp
    {
        private readonly IPty _pty;
        private readonly VfsManager? _vfs;
        private ShProgram? _sh;
        private PluginContext? _ctx;

        public ShellApp(IPty pty, VfsManager? vfs = null)
        {
            _pty = pty ?? throw new ArgumentNullException(nameof(pty));
            _vfs = vfs;
        }

        public object? MainWindow => null;

        public void Initialize(PluginContext context)
        {
            _ctx = context;
        }

        public void Start()
        {
            var vfs = _vfs ?? _ctx?.VFS ?? throw new InvalidOperationException("VFS not available");
            _sh = new ShProgram(_pty, vfs);
            // Block Start until the shell exits so AppHost.Run behaves like a foreground app
            _sh.WaitForExit();
        }

        public void Stop()
        {
            // ShProgram currently detaches on exit; if it exposes a Stop, call it here.
            _sh = null;
        }
    }
}
