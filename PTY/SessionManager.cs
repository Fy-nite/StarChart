using System;
using System.Collections.Generic;
using Adamantite.VFS;
using StarChart.PTY;
using StarChart.Bin;
using StarChart.Plugins;

namespace StarChart.PTY
{
    // Manages sessions keyed by username. Creates PTY + Shell host for each session.
    public class SessionManager
    {
        readonly Dictionary<string, (VirtualPty pty, object host)> _sessions = new(StringComparer.OrdinalIgnoreCase);
        readonly VfsManager _vfs;
        readonly object? _windowContext;
        Action<IStarChartApp>? _onRegisterApp;

        public SessionManager(object? windowContext, VfsManager vfs, Action<IStarChartApp>? onRegisterApp = null)
        {
            _windowContext = windowContext;
            _vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
            _onRegisterApp = onRegisterApp;
        }

        // Create a session and return the PTY and its backing VirtualTerminal.
        public (IPty pty, Adamantite.GPU.VirtualTerminal vt)? CreateSession(string username)
        {
            if (string.IsNullOrEmpty(username)) return null;
            if (_sessions.ContainsKey(username)) return null;

            // Create a VT-backed PTY
            var vt = new Adamantite.GPU.VirtualTerminal(80, 20);
            vt.RenderOptions = Adamantite.GPU.VirtualTerminal.TextRenderOptions.Default;
            vt.DefaultForeground = 0xFFFFFFFFu;
            vt.DefaultBackground = 0xFF000000u;
            vt.FillScreen = true;
            var pty = new VirtualPty(vt);

            // If /bin/sh exists in the VFS, execute it as a simple script on the PTY
            try
            {
                ExecuteScript("/bin/sh", pty);
            }
            catch { }

            _sessions[username] = (pty, "shell");

            return (pty, vt);
        }

        private void ExecuteScript(string scriptPath, IPty pty)
        {
            // Allow built-in symlink handlers to intercept script execution first
            try
            {
                if (StarChart.Bin.Symlinks.TryExecuteScript(scriptPath, pty, _vfs, _windowContext, _onRegisterApp))
                    return;
            }
            catch { }

            if (scriptPath == "/bin/sh")
            {
                var sh = new StarChart.Bin.ShProgram(pty, _vfs);
                _onRegisterApp?.Invoke(sh);
            }
        }

        public bool KillSession(string username)
        {
            return _sessions.Remove(username);
        }
    }
}
