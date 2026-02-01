using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Adamantite.VFS;

namespace StarChart.Bin
{
    public static class ConsoleShell
    {
        public static void Run(VfsManager vfs)
        {
            if (vfs == null) throw new ArgumentNullException(nameof(vfs));
            Console.WriteLine("StarChart headless shell. Type 'startx' to launch the GUI, 'help' for commands, 'exit' to quit.");

            while (true)
            {
                Console.Write("root@starchart:/$ ");
                var line = Console.ReadLine();
                if (line == null) break;
                var cmdline = line.Trim();
                if (string.IsNullOrEmpty(cmdline)) continue;
                var parts = cmdline.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var cmd = parts[0].ToLowerInvariant();

                try
                {
                    switch (cmd)
                    {
                        case "startx":
                        case "startw11":
                        case "x":
                            return; // exit shell loop and start GUI
                        case "exit":
                        case "quit":
                            Environment.Exit(0);
                            return;
                        case "help":
                            Console.WriteLine("Supported commands: ls, cat <file>, echo <text>, pwd, help, startx, exit");
                            break;
                        case "ls":
                            {
                                var path = parts.Length > 1 ? parts[1] : "/";
                                var items = vfs.Enumerate(path);
                                foreach (var it in items)
                                {
                                    Console.WriteLine($"{(it.IsDirectory ? 'd' : '-')}	{it.Path}	{it.Length}");
                                }
                            }
                            break;
                        case "cat":
                            if (parts.Length < 2)
                            {
                                Console.WriteLine("cat: missing file");
                            }
                            else
                            {
                                var filePath = parts[1];
                                if (vfs.Exists(filePath))
                                {
                                    using var s = vfs.OpenRead(filePath);
                                    using var r = new System.IO.StreamReader(s);
                                    Console.WriteLine(r.ReadToEnd());
                                }
                                else
                                {
                                    Console.WriteLine($"cat: {filePath}: No such file");
                                }
                            }
                            break;
                        case "echo":
                            Console.WriteLine(string.Join(' ', parts.Skip(1)));
                            break;
                        case "pwd":
                            Console.WriteLine("/");
                            break;
                        default:
                            // Try executing a file in the VFS (support ./name, ./name.dll, ./name.asm)
                            var target = cmd;
                            if (target.StartsWith("./")) target = target.Substring(2);
                            bool executed = false;
                            string? lastReason = null;

                            string TryExecute(string p)
                            {
                                try
                                {
                                    var rp = p.StartsWith("/") ? p : "/" + p;
                                    if (!vfs.Exists(rp)) return $"not found: {rp}";

                                    // Read bytes
                                    var data = vfs.ReadAllBytes(rp);
                                    // PE file?
                                    if (data.Length >= 2 && data[0] == 'M' && data[1] == 'Z')
                                    {
                                        var asm = AssemblyLoadContext.Default.LoadFromStream(new System.IO.MemoryStream(data));
                                        var appInterface = typeof(StarChart.Plugins.IStarChartApp);
                                        var pluginInterface = typeof(StarChart.Plugins.IStarChartPlugin);
                                        var type = asm.GetTypes().FirstOrDefault(t => appInterface.IsAssignableFrom(t) && !t.IsAbstract);
                                        var ctx = new StarChart.Plugins.PluginContext { VFS = vfs, Arguments = parts.Skip(1).ToArray(), WorkingDirectory = "/", PrimaryPty = null };
                                        if (type != null)
                                        {
                                            var inst = Activator.CreateInstance(type) as StarChart.Plugins.IStarChartApp;
                                            inst?.Initialize(ctx);
                                            inst?.Start();
                                            executed = true;
                                            return "ok";
                                        }
                                        var ptype = asm.GetTypes().FirstOrDefault(t => pluginInterface.IsAssignableFrom(t) && !t.IsAbstract);
                                        if (ptype != null)
                                        {
                                            var inst = Activator.CreateInstance(ptype) as StarChart.Plugins.IStarChartPlugin;
                                            inst?.Initialize(ctx);
                                            inst?.Start();
                                            executed = true;
                                            return "ok";
                                        }
                                        var entry = asm.EntryPoint;
                                        if (entry != null)
                                        {
                                            var parameters = entry.GetParameters().Length == 0 ? null : new object[] { parts.Skip(1).ToArray() };
                                            entry.Invoke(null, parameters);
                                            executed = true;
                                            return "ok";
                                        }
                                        return "no runnable entry";
                                    }

                                    // asm source
                                    var ext = System.IO.Path.GetExtension(rp)?.ToLowerInvariant();
                                    if (ext == ".asm")
                                    {
                                        var bytes = vfs.ReadAllBytes(rp);
                                        var src = System.Text.Encoding.UTF8.GetString(bytes);
                                        var host = new StarChart.Assembly.StarChartAssemblyHost();
                                        var runtime = new StarChart.Assembly.AssemblyRuntime(host);
                                        runtime.RunSource(src);
                                        executed = true;
                                        return "ok";
                                    }

                                    return "unsupported file type";
                                }
                                catch (Exception e)
                                {
                                    return $"exec exception: {e.GetType().Name}: {e.Message}";
                                }
                            }

                            // try direct
                            lastReason = TryExecute(target);
                            if (!executed && !target.Contains('.'))
                            {
                                lastReason = TryExecute(target + ".dll") ?? lastReason;
                                if (!executed) lastReason = TryExecute(target + ".asm") ?? lastReason;
                            }

                            if (!executed)
                                Console.WriteLine($"{cmd}: command not found ({lastReason})");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }
    }
}
