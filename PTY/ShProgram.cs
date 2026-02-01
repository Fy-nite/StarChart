using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using StarChart.PTY;

namespace StarChart.Bin
{
    // Built-in /bin/sh implementation as a C# program that runs on a PTY.
    // Reads input lines, executes simple commands, and writes output.
    public class ShProgram
    {
        readonly System.Threading.Tasks.TaskCompletionSource<int> _exitTcs = new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly IPty _pty;
        private readonly Adamantite.VFS.VfsManager _vfs;
        private string _input = "";
        private string _cwd = "/";
        private readonly List<string> _history = new List<string>();

        public ShProgram(IPty pty, Adamantite.VFS.VfsManager vfs)
        {
            _pty = pty ?? throw new ArgumentNullException(nameof(pty));
            _vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
            _pty.OnInput += OnInput;
            WritePrompt();
        }

        private void OnInput(object? sender, char c)
        {
            if (c == '\n')
            {
                var cmdline = _input.Trim();
                if (!string.IsNullOrEmpty(cmdline)) _history.Add(cmdline);
                ProcessCommand(cmdline);
                _input = "";
            }
            else if (c == '\b')
            {
                if (_input.Length > 0) _input = _input.Substring(0, _input.Length - 1);
            }
            else if (c == '\t')
            {
                // Tab completion for last token
                var tokenStart = _input.LastIndexOf(' ');
                var prefix = tokenStart >= 0 ? _input.Substring(tokenStart + 1) : _input;
                var dir = _cwd;
                var entries = new List<string>();
                try
                {
                    var resolved = ResolvePath(prefix);
                    string listPath;
                    if (prefix.Contains('/'))
                    {
                        var idx = prefix.LastIndexOf('/');
                        listPath = ResolvePath(prefix.Substring(0, idx + 1));
                        prefix = prefix.Substring(idx + 1);
                    }
                    else listPath = _cwd;

                    var files = _vfs.Enumerate(listPath ?? "/");
                    entries = files.Select(f => System.IO.Path.GetFileName(f.Path)).Where(n => n != null).ToList();
                }
                catch { }

                var match = entries.FirstOrDefault(e => e.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    if (tokenStart >= 0)
                        _input = _input.Substring(0, tokenStart + 1) + match;
                    else
                        _input = match;
                }
            }
            else
            {
                _input += c;
            }
        }

        private void ProcessCommand(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                WritePrompt();
                return;
            }

            var parts = SplitArgs(line);
            if (parts.Length == 0)
            {
                WritePrompt();
                return;
            }

            var cmd = parts[0];
            string output;

            try
            {
                switch (cmd.ToLowerInvariant())
                {
                    case "help":
                        output = "Supported: ls, cat, echo, pwd, cd, history, !n, clear, exit, mkdir, touch, rm, help";
                        break;
                    case "ls":
                        {
                            var pathArg = parts.Length > 1 ? parts[1] : _cwd;
                            var path = ResolvePath(pathArg);
                            var files = _vfs.Enumerate(path);
                            output = string.Join("\n", files.Select(f => $"{(f.IsDirectory ? 'd' : '-')} {f.Path} {f.Length}"));
                        }
                        break;
                    case "cat":
                        if (parts.Length < 2)
                        {
                            output = "cat: missing file";
                        }
                        else
                        {
                            var filePath = ResolvePath(parts[1]);
                            if (_vfs.Exists(filePath))
                            {
                                using var stream = _vfs.OpenRead(filePath);
                                using var reader = new System.IO.StreamReader(stream);
                                output = reader.ReadToEnd();
                            }
                            else
                            {
                                output = $"cat: {filePath}: No such file";
                            }
                        }
                        break;
                    case "echo":
                        output = string.Join(" ", parts.Skip(1));
                        break;
                    case "pwd":
                        output = _cwd;
                        break;
                    case "cd":
                        {
                            var target = parts.Length > 1 ? parts[1] : "/";
                            var resolved = ResolvePath(target);
                            if (_vfs.Exists(resolved))
                            {
                                _cwd = resolved;
                                output = "";
                            }
                            else
                            {
                                output = $"cd: {target}: No such file or directory";
                            }
                        }
                        break;
                    case "history":
                        output = string.Join("\n", _history.Select((h, i) => $"{i + 1}  {h}"));
                        break;
                    case "clear":
                        // ANSI clear screen & move cursor home
                        var seq = "\u001b[2J\u001b[H";
                        foreach (var ch in seq) _pty.WriteToPty(ch);
                        output = "";
                        break;
                    case "exit":
                        output = "exit";
                        _pty.OnInput -= OnInput;
                        // signal exit so hosts can wait for shell termination
                        try { _exitTcs.TrySetResult(0); } catch { }
                        break;
                    case "mkdir":
                        {
                            if (parts.Length < 2) output = "mkdir: missing operand";
                            else
                            {
                                var path = ResolvePath(parts[1]);
                                _vfs.CreateDirectory(path);
                                output = "";
                            }
                        }
                        break;
                    case "touch":
                        {
                            if (parts.Length < 2) output = "touch: missing file operand";
                            else
                            {
                                var path = ResolvePath(parts[1]);
                                try
                                {
                                    // VfsManager helper
                                    _vfs.CreateFile(path);
                                    output = "";
                                }
                                catch { output = "touch: failed"; }
                            }
                        }
                        break;
                    case "rm":
                        {
                            if (parts.Length < 2) output = "rm: missing operand";
                            else
                            {
                                var path = ResolvePath(parts[1]);
                                try
                                {
                                    _vfs.Delete(path);
                                    output = "";
                                }
                                catch { output = "rm: failed"; }
                            }
                        }
                        break;
                    default:
                        if (cmd.StartsWith("!"))
                        {
                            // replay history entry
                            if (int.TryParse(cmd.Substring(1), out var idx) && idx > 0 && idx <= _history.Count)
                            {
                                var entry = _history[idx - 1];
                                ProcessCommand(entry);
                                return;
                            }
                            output = $"{cmd}: no such history entry";
                        }
                        else
                        {
                            // Attempt to execute a file (./prog, ./prog.dll, ./prog.asm)
                            var target = cmd;
                            if (target.StartsWith("./")) target = target.Substring(2);
                            var tried = new List<string>();
                            bool executed = false;

                            string TryAndExecute(string p)
                            {
                                try
                                {
                                    var rp = ResolvePath(p);
                                    if (!_vfs.Exists(rp)) return $"not found: {rp}";
                                    ExecuteFile(rp, parts.Skip(1).ToArray());
                                    executed = true;
                                    return "ok";
                                }
                                catch (Exception e)
                                {
                                    // Provide type and message for better diagnostics
                                    return $"exec exception: {e.GetType().Name}: {e.Message}";
                                }
                            }

                            // direct path
                            tried.Add(target);
                            var res = TryAndExecute(target);
                            // debug: report what we tried and why
                            if (res != null) WriteLine($"[exec-debug] tried {target} -> {res}");
                            // try common extensions
                            if (!executed && !target.Contains('.'))
                            {
                                tried.Add(target + ".dll");
                                var r2 = TryAndExecute(target + ".dll");
                                if (r2 != null) WriteLine($"[exec-debug] tried {target}.dll -> {r2}");
                                res = r2 ?? res;
                                if (!executed)
                                {
                                    tried.Add(target + ".asm");
                                    var r3 = TryAndExecute(target + ".asm");
                                    if (r3 != null) WriteLine($"[exec-debug] tried {target}.asm -> {r3}");
                                    res = r3 ?? res;
                                }
                            }

                            if (executed)
                            {
                                output = ""; // already started
                            }
                            else
                            {
                                output = $"{cmd}: command not found" + (res != null ? (" (" + res + ")") : "");
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                output = $"Error: {ex.Message}";
            }

            if (!string.IsNullOrEmpty(output)) WriteLine(output);
            if (cmd != "exit") WritePrompt();
        }

        // Block until the shell has exited.
        public void WaitForExit()
        {
            _exitTcs.Task.Wait();
        }

        void ExecuteFile(string path, string[] args)
        {
            // Read the file bytes from VFS
            var data = _vfs.ReadAllBytes(path);
            // If it's a PE (DLL/EXE) load it as an assembly
            if (data.Length >= 2 && data[0] == 'M' && data[1] == 'Z')
            {
                var asm = AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(data));
                // Try to find an IStarChartApp implementation
                var appInterface = typeof(StarChart.Plugins.IStarChartApp);
                var pluginInterface = typeof(StarChart.Plugins.IStarChartPlugin);
                var type = asm.GetTypes().FirstOrDefault(t => appInterface.IsAssignableFrom(t) && !t.IsAbstract);
                var ctx = new StarChart.Plugins.PluginContext { VFS = _vfs, Arguments = args, WorkingDirectory = _cwd, PrimaryPty = _pty };
                if (_pty is StarChart.AppFramework.TerminalHost thost) ctx.TerminalHost = thost;
                if (type != null)
                {
                    var inst = Activator.CreateInstance(type) as StarChart.Plugins.IStarChartApp;
                    // If possible, acquire exclusive terminal to avoid races
                    IDisposable? exclus = null;
                    if (ctx.TerminalHost is StarChart.AppFramework.TerminalHost th)
                        exclus = th.AcquireExclusive();
                    try
                    {
                        inst?.Initialize(ctx);
                        // Run Start synchronously so the app can take over the terminal (blocks until it returns)
                        inst?.Start();
                    }
                    finally
                    {
                        exclus?.Dispose();
                    }
                    return;
                }

                // Try a plugin type
                var ptype = asm.GetTypes().FirstOrDefault(t => pluginInterface.IsAssignableFrom(t) && !t.IsAbstract);
                if (ptype != null)
                {
                    var inst = Activator.CreateInstance(ptype) as StarChart.Plugins.IStarChartPlugin;
                    IDisposable? exclus = null;
                    if (ctx.TerminalHost is StarChart.AppFramework.TerminalHost th)
                        exclus = th.AcquireExclusive();
                    try
                    {
                        inst?.Initialize(ctx);
                        inst?.Start();
                    }
                    finally
                    {
                        exclus?.Dispose();
                    }
                    return;
                }

                // Fallback: execute entry point
                var entry = asm.EntryPoint;
                if (entry != null)
                {
                    var parameters = entry.GetParameters().Length == 0 ? null : new object[] { args };
                    entry.Invoke(null, parameters);
                    return;
                }

                throw new InvalidOperationException("No runnable entry found in assembly");
            }

            // For assembly source files, use the Assembly runtime host if available
            var ext = System.IO.Path.GetExtension(path)?.ToLowerInvariant();
            if (ext == ".asm")
            {
                // Ensure VFSGlobal is set so StarChartAssemblyHost can read the file
                var prev = Adamantite.VFS.VFSGlobal.Manager;
                try
                {
                    Adamantite.VFS.VFSGlobal.Manager = _vfs;
                    var bytes = _vfs.ReadAllBytes(path);
                    var src = System.Text.Encoding.UTF8.GetString(bytes);
                    var host = new StarChart.Assembly.StarChartAssemblyHost();
                    var runtime = new StarChart.Assembly.AssemblyRuntime(host);
                    runtime.RunSource(src);
                    return;
                }
                finally
                {
                    Adamantite.VFS.VFSGlobal.Manager = prev;
                }
            }

            throw new InvalidOperationException("Unsupported file type for execution");
        }

        private string[] SplitArgs(string line)
        {
            var args = new List<string>();
            var cur = new System.Text.StringBuilder();
            bool inQuote = false;
            foreach (var ch in line)
            {
                if (ch == '"') { inQuote = !inQuote; continue; }
                if (ch == ' ' && !inQuote)
                {
                    if (cur.Length > 0) { args.Add(cur.ToString()); cur.Clear(); }
                }
                else cur.Append(ch);
            }
            if (cur.Length > 0) args.Add(cur.ToString());
            return args.ToArray();
        }

        private string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return _cwd;
            if (path.StartsWith("/")) return Normalize(path);
            if (_cwd == "/") return Normalize("/" + path);
            return Normalize(_cwd.TrimEnd('/') + "/" + path);
        }

        private string Normalize(string p)
        {
            // very simple normalization, collapsing "/./" and "/../" where possible
            var parts = p.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var stack = new Stack<string>();
            foreach (var part in parts)
            {
                if (part == ".") continue;
                if (part == "..") { if (stack.Count > 0) stack.Pop(); continue; }
                stack.Push(part);
            }
            var arr = stack.Reverse().ToArray();
            return "/" + string.Join('/', arr);
        }

        private void WritePrompt()
        {
            var prompt = $"root@starchart:{_cwd}$ ";
            foreach (char c in prompt) _pty.WriteToPty(c);
        }

        private void WriteLine(string s)
        {
            foreach (char c in s) _pty.WriteToPty(c);
            _pty.WriteToPty('\r');
            _pty.WriteToPty('\n');
        }
    }
}
