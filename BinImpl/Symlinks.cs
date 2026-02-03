using System;
using StarChart.PTY;
using Adamantite.VFS;
using StarChart.stdlib.W11;

namespace StarChart.Bin
{
    // Registry of VFS "symlink" builtins that can be invoked to start
    // built-in programs (ShProgram, XTerm, etc.) from system code paths.
    public static class Symlinks
    {
        // Normalizes a path/name to a lower-case key used by the switch.
        static string NormalizeKey(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            s = s.Trim();
            // Accept both /bin/sh and sh forms
            if (s.StartsWith("/")) s = System.IO.Path.GetFileName(s);
            return s.ToLowerInvariant();
        }

        // Try executing a script path on the supplied PTY. Returns true if handled.
        public static bool TryExecuteScript(string path, IPty pty, VfsManager vfs, DisplayServer? server = null, Action<StarChart.Plugins.IStarChartApp>? registerApp = null)
        {
            if (string.IsNullOrEmpty(path) || pty == null) return false;
            var key = NormalizeKey(path);
            try
            {
                switch (key)
                {
                    case "sh":
                        // Start the built-in ShProgram on the provided PTY
                        try 
                        { 
                            var sp = new ShProgram(pty, vfs); 
                            registerApp?.Invoke(sp);
                        } 
                        catch { }
                        return true;
                    case "xterm":
                        // If a DisplayServer is available, create an XTerm window instead
                        if (server != null)
                        {
                            try
                            {
                                var xt = new XTerm(server, "xterm", "XTerm", 80, 24, 10, 10, 1);
                                xt.Render();
                                registerApp?.Invoke(xt);
                                try { var sh = new StarChart.stdlib.W11.Shell(xt); } catch { }
                            }
                            catch { }
                            return true;
                        }
                        break;
                }
            }
            catch { }
            return false;
        }

        // Try spawning a session for a login shell name (e.g. 'sh' or 'xterm'). Returns true if handled.
        // If `currentVt` or `currentXterm` is provided, prefer reusing that terminal
        // to start the shell instead of creating a new fullscreen VT.
        public static bool TrySpawnForLogin(string shellName, Runtime runtime, DisplayServer? server, VfsManager? vfs, string username,
            Adamantite.GPU.VirtualTerminal? currentVt = null, StarChart.stdlib.W11.XTerm? currentXterm = null)
        {
            if (string.IsNullOrEmpty(shellName)) return false;
            var key = NormalizeKey(shellName);
            try
            {
                switch (key)
                {
                    case "sh":
                        // Prefer reusing the current terminal if available
                        try
                        {
                            if (currentVt != null)
                            {
                                try 
                                { 
                                    var sp = new ShProgram(new StarChart.PTY.VirtualPty(currentVt), vfs ?? Adamantite.VFS.VFSGlobal.Manager); 
                                    runtime.RegisterApp(sp);
                                } 
                                catch { }
                                return true;
                            }
                            if (currentXterm != null)
                            {
                                try { var sh = new StarChart.stdlib.W11.Shell(currentXterm); } catch { }
                                return true;
                            }
                            // Fallback: create a fullscreen VirtualTerminal and run ShProgram on it
                            var userVt = new Adamantite.GPU.VirtualTerminal(80, 20);
                            userVt.DefaultForeground = 0xFFFFFFFFu;
                            userVt.DefaultBackground = 0xFF000000u;
                            userVt.FillScreen = true;
                            userVt.WriteLine($"Login: {username}");
                            var userPty = new StarChart.PTY.VirtualPty(userVt);
                            try 
                            { 
                                var sp = new ShProgram(userPty, vfs ?? Adamantite.VFS.VFSGlobal.Manager); 
                                runtime.RegisterApp(sp);
                            } 
                            catch { }
                            try { runtime.AttachVirtualTerminal(userVt); } catch { }
                            return true;
                        }
                        catch { return false; }

                    case "xterm":
                        if (server != null)
                        {
                            try
                            {
                                int x = 10, y = 10;
                                var xt = new XTerm(server, "xterm", username, 80, 24, x, y, 1, fontKind: XTerm.FontKind.Clean8x8);
                                xt.Render();
                                try { var sh = new StarChart.stdlib.W11.Shell(xt); } catch { }
                                runtime.RegisterApp(xt);
                                return true;
                            }
                            catch { }
                        }
                        break;
                }
            }
            catch { }
            return false;
        }

        // Create small "symlink" files in the provided VFS so tools/users can inspect
        // the fact that these paths point to builtins. This writes a tiny text file
        // with a marker (e.g. "builtin:sh") at /bin/sh and /bin/xterm when possible.
        public static void RegisterDefaultSymlinks(VfsManager vfs)
        {
            if (vfs == null) return;
            try
            {
                void TryWrite(string path, string content)
                {
                    try
                    {
                        if (!vfs.Exists(path))
                        {
                            var data = System.Text.Encoding.UTF8.GetBytes(content);
                            vfs.WriteAllBytes(path, data);
                        }
                    }
                    catch { }
                }

                // Ensure /bin exists; if not, try creating it
                try { if (!vfs.Exists("/bin")) vfs.CreateDirectory("/bin"); } catch { }

                TryWrite("/bin/sh", "builtin:sh\n# source: PTY/ShProgram.cs\n");
                TryWrite("/bin/xterm", "builtin:xterm\n# source: stdlib/W11/XTerm.cs\n");
            }
            catch { }
        }
    }
}
