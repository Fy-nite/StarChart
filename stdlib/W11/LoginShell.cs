using System;
using StarChart.stdlib.W11;
using Microsoft.Xna.Framework.Graphics;
using Adamantite.GPU;

namespace StarChart.stdlib.W11
{
    // Minimal login shell that requires the user to type `startw` to start the W11 environment.
    public class LoginShell
    {
        readonly XTerm? _xterm;
        readonly VirtualTerminal? _vterm;
        readonly Runtime _runtime;
        // When true the next entered line is interpreted as a pixel-density selection
        // for starting W11 (1=low,2=medium,3=high).
        bool _awaitDensitySelection = false;

        // When true we're waiting for a username at the login prompt
        bool _awaitingLogin = true;

        public LoginShell(XTerm term, Runtime runtime)
        {
            _xterm = term ?? throw new ArgumentNullException(nameof(term));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            // Make the login shell look like a classic virtual terminal: white on black
            // and occupy the full display (borderless) when possible.
            try
            {
                _xterm.Foreground = 0xFFFFFFFFu; // white
                _xterm.Background = 0xFF000000u; // black
                // Use a borderless window style so it resembles a fullscreen terminal.
                _xterm.Window.Style = WindowStyle.Borderless;

                // Try to size the window to the display resolution so it behaves like a
                // virtual terminal. Fall back silently if GraphicsAdapter isn't available.
                try
                {
                    var dm = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
                    _xterm.Window.SetOnScreenSize(dm.Width, dm.Height);
                    _xterm.Window.Geometry.X = 0;
                    _xterm.Window.Geometry.Y = 0;
                }
                catch { }
            }
            catch { }

            _xterm.OnEnter += OnEnter;
            _xterm.WriteLine("Welcome to StarChart.");
            _xterm.WriteLine("login:");
            _awaitingLogin = true;
        }

        // New constructor for VirtualTerminal-based login (full-screen TTY)
        public LoginShell(VirtualTerminal vterm, Runtime runtime)
        {
            _vterm = vterm ?? throw new ArgumentNullException(nameof(vterm));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

            try
            {
                _vterm.DefaultForeground = 0xFFFFFFFFu; // white
                _vterm.DefaultBackground = 0xFF000000u; // black
            }
            catch { }

            _vterm.OnEnter += OnEnter;
            _vterm.WriteLine("Welcome to StarChart.");
            _vterm.WriteLine("login:");
            _awaitingLogin = true;
        }

        void OnEnter(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            var cmd = line.Trim();
            // If we previously asked for a pixel-density selection, interpret this
            // line as the choice and then start W11 with the requested density.
            if (_awaitDensitySelection)
            {
                float scale = 0.5f; // default to medium
                var s = cmd.ToLowerInvariant();
                if (s == "1" || s == "low" || s == "l") scale = 0.25f;
                else if (s == "2" || s == "medium" || s == "med" || s == "m") scale = 0.5f;
                else if (s == "3" || s == "high" || s == "h" || s == "native") scale = 1.0f;

                try
                {
                    _runtime.SetPresentationScale(scale);
                    if (_vterm != null) _vterm.WriteLine($"Presentation scale set to {scale}");
                    else if (_xterm != null) _xterm.WriteLine($"Presentation scale set to {scale}");
                }
                catch { }

                // Clear selection state and start W11
                _awaitDensitySelection = false;
                if (_vterm != null) _vterm.WriteLine("Starting W11...");
                else if (_xterm != null) _xterm.WriteLine("Starting W11...");

                try
                {
                    _runtime.StartW11();
                    if (_vterm != null)
                    {
                        _vterm.WriteLine("W11 started.");
                    }
                    else if (_xterm != null)
                    {
                        _xterm.WriteLine("W11 started.");
                    }
                }
                catch (Exception ex)
                {
                    if (_vterm != null)
                    {
                        _vterm.WriteLine("Failed to start W11: " + ex.Message);
                    }
                    else if (_xterm != null)
                    {
                        _xterm.WriteLine("Failed to start W11: " + ex.Message);
                    }
                }
                return;
            }

            // If the user typed the legacy startw command at the login prompt
            // allow them to select pixel density as before.
            if (string.Equals(cmd, "startw", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cmd, "startW", StringComparison.OrdinalIgnoreCase))
            {
                if (_vterm != null)
                {
                    _vterm.WriteLine("Select pixel density for W11:");
                    _vterm.WriteLine("1) Low (large pixels, e.g. 320x200)");
                    _vterm.WriteLine("2) Medium (e.g. 640x480)");
                    _vterm.WriteLine("3) High (native)");
                    _vterm.WriteLine("Enter 1/2/3 and press Enter:");
                }
                else if (_xterm != null)
                {
                    _xterm.WriteLine("Select pixel density for W11:");
                    _xterm.WriteLine("1) Low (large pixels, e.g. 320x200)");
                    _xterm.WriteLine("2) Medium (e.g. 640x480)");
                    _xterm.WriteLine("3) High (native)");
                    _xterm.WriteLine("Enter 1/2/3 and press Enter:");
                }

                _awaitDensitySelection = true;
                return;
            }

            // Otherwise treat input as a login name (like a simple getty).
            if (_awaitingLogin)
            {
                var username = cmd;
                // try to find user in /etc/passwd
                try
                {
                    var vfs = Adamantite.VFS.VFSGlobal.Manager;
                    if (vfs != null && vfs.Exists("/etc/passwd"))
                    {
                        using var s = vfs.OpenRead("/etc/passwd");
                        using var sr = new System.IO.StreamReader(s);
                        string? pline;
                        bool found = false;
                        while ((pline = sr.ReadLine()) != null)
                        {
                            pline = pline.Trim();
                            if (string.IsNullOrEmpty(pline) || pline.StartsWith("#")) continue;
                            var parts = pline.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length == 0) continue;
                            if (parts[0] == username)
                            {
                                found = true;
                                var shellName = parts.Length > 1 ? parts[1] : "sh";
                                // start the requested shell: if it's 'w11' start W11,
                                // otherwise spawn an XTerm and attach the interactive Shell.
                                if (string.Equals(shellName, "w11", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(shellName, "startw", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (_vterm != null) _vterm.WriteLine($"Starting W11 for {username}...");
                                    else if (_xterm != null) _xterm.WriteLine($"Starting W11 for {username}...");
                                    _runtime.StartW11();
                                }
                                else
                                {
                                    // Spawn a fullscreen VirtualTerminal for the user instead of an XTerm
                                    try
                                    {
                                        var userVt = new VirtualTerminal(80, 20);
                                        userVt.DefaultForeground = 0xFFFFFFFFu;
                                        userVt.DefaultBackground = 0xFF000000u;
                                        userVt.FillScreen = true;
                                        userVt.WriteLine($"Login: {username}");
                                        // Attach shell to the VT
                                        try { var sh = new Shell(userVt); } catch { }
                                        // Let the runtime know about the fullscreen VT so it can render/route input
                                        try { _runtime.AttachVirtualTerminal(userVt); } catch { }

                                        if (_vterm != null) _vterm.WriteLine($"Login succeeded. Started session for {username} in VirtualTerminal.");
                                        else if (_xterm != null) _xterm.WriteLine($"Login succeeded. Started session for {username} in VirtualTerminal.");
                                    }
                                    catch (Exception ex)
                                    {
                                        if (_vterm != null) _vterm.WriteLine("Failed to start user session: " + ex.Message);
                                        else if (_xterm != null) _xterm.WriteLine("Failed to start user session: " + ex.Message);
                                    }
                                }

                                break;
                            }
                        }

                        if (!found)
                        {
                            if (_vterm != null) _vterm.WriteLine("Login incorrect");
                            else if (_xterm != null) _xterm.WriteLine("Login incorrect");
                            if (_vterm != null) _vterm.WriteLine("login:");
                            else if (_xterm != null) _xterm.WriteLine("login:");
                            return;
                        }
                    }
                    else
                    {
                        // no passwd file: allow any login and spawn an XTerm
                        var server = _runtime.Server;
                        if (server != null)
                        {
                            try
                            {
                                var xt = new XTerm(server, "xterm", username, 80, 24, 10, 10, 1, fontKind: XTerm.FontKind.Clean8x8);
                                xt.Render();
                                try { var sh = new Shell(xt); } catch { }
                                if (_vterm != null) _vterm.WriteLine($"Login succeeded. Started session for {username} in XTerm.");
                                else if (_xterm != null) _xterm.WriteLine($"Login succeeded. Started session for {username} in XTerm.");
                            }
                            catch (Exception ex)
                            {
                                if (_vterm != null) _vterm.WriteLine("Failed to start user session: " + ex.Message);
                                else if (_xterm != null) _xterm.WriteLine("Failed to start user session: " + ex.Message);
                            }
                        }
                        else
                        {
                            if (_vterm != null) _vterm.WriteLine("No /etc/passwd and no display server; cannot start session.");
                            else if (_xterm != null) _xterm.WriteLine("No /etc/passwd and no display server; cannot start session.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_vterm != null) _vterm.WriteLine("Login failed: " + ex.Message);
                    else if (_xterm != null) _xterm.WriteLine("Login failed: " + ex.Message);
                }

                // After handling login attempt, present a fresh login prompt
                if (_vterm != null) _vterm.WriteLine("login:");
                else if (_xterm != null) _xterm.WriteLine("login:");
                return;
            }

            if (_vterm != null)
            {
                _vterm.WriteLine("Unknown command. Type 'startw' to start W11.");
            }
            else if (_xterm != null)
            {
                _xterm.WriteLine("Unknown command. Type 'startw' to start W11.");
            }
        }
    }
}
