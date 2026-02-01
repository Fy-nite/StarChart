using System;
using System.Collections.Generic;
using Adamantite.VFS;
using StarChart.stdlib.W11;
using StarChart.PTY;

namespace StarChart.PTY
{
    // Manages sessions keyed by username. Creates PTY + Shell host for each session.
    public class SessionManager
    {
        readonly Dictionary<string, (VirtualPty pty, object host)> _sessions = new(StringComparer.OrdinalIgnoreCase);
        readonly VfsManager _vfs;
        readonly DisplayServer _server;

        public SessionManager(DisplayServer server, VfsManager vfs)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
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

            // Always attach an interactive Shell to the VT for user input
            var sh = new StarChart.stdlib.W11.Shell(vt);
            _sessions[username] = (pty, sh);

            return (pty, vt);
        }

        private void ExecuteScript(string scriptPath, IPty pty)
        {
            if (scriptPath == "/bin/sh")
            {
                var sh = new StarChart.Bin.ShProgram(pty, _vfs);
            }
        }

        public bool KillSession(string username)
        {
            return _sessions.Remove(username);
        }
    }
}
