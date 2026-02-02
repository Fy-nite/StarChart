using System;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using Adamantite.VFS;
using StarChart.Assembly;

namespace StarChart.stdlib
{
    /// <summary>
    /// Kernel-level exec helper exposed to stdlib. Allows executing files from the VFS:
    /// - Managed assemblies (.dll/.exe) are loaded in-process and their entry point invoked.
    /// - Native .exe files are written to a temp file and launched as a separate process.
    /// - Assembly source (.asm) is handed to the Assembly runtime host.
    /// </summary>
    public static class KernelExec
    {
        public static void Exec(string path, string[]? args = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                StarChart.stdlib.Sys.Console.WriteLine("exec: empty path");
                return;
            }

            // Normalize VFS path
            if (path.StartsWith("./")) path = path.Substring(1);
            if (!path.StartsWith("/")) path = "/" + path;

            var vfs = VFSGlobal.Manager;
            if (vfs == null)
            {
                StarChart.stdlib.Sys.Console.WriteLine("exec: VFS not initialized");
                return;
            }

            if (!vfs.Exists(path))
            {
                StarChart.stdlib.Sys.Console.WriteLine($"exec: file not found: {path}");
                return;
            }

            byte[] bytes;
            try
            {
                bytes = vfs.ReadAllBytes(path);
            }
            catch (Exception ex)
            {
                StarChart.stdlib.Sys.Console.WriteLine($"exec: failed to read {path}: {ex.Message}");
                return;
            }

            var ext = System.IO.Path.GetExtension(path)?.ToLowerInvariant();

            // Try loading as a managed assembly first
            try
            {
                var asm = System.Reflection.Assembly.Load(bytes);
                var entry = asm.EntryPoint;
                if (entry != null)
                {
                    try
                    {
                        var parms = entry.GetParameters();
                        object?[]? invokeArgs = null;
                        if (parms.Length == 1)
                        {
                            invokeArgs = new object[] { args ?? Array.Empty<string>() };
                        }
                        entry.Invoke(null, invokeArgs);
                        return;
                    }
                    catch (TargetInvocationException tie)
                    {
                        StarChart.stdlib.Sys.Console.WriteLine($"exec: managed entry threw: {tie.InnerException?.Message ?? tie.Message}");
                        return;
                    }
                }
            }
            catch (BadImageFormatException)
            {
                // not a managed assembly - continue
            }
            catch (Exception ex)
            {
                StarChart.stdlib.Sys.Console.WriteLine($"exec: failed to load assembly {path}: {ex.Message}");
                return;
            }

            // Native .exe: write to temp file and start
            if (string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString() + ".exe");
                    System.IO.File.WriteAllBytes(tmp, bytes);
                    var psi = new ProcessStartInfo(tmp)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    if (args != null && args.Length > 0)
                        psi.Arguments = string.Join(' ', args);
                    Process.Start(psi);
                    return;
                }
                catch (Exception ex)
                {
                    StarChart.stdlib.Sys.Console.WriteLine($"exec: failed to start native exe: {ex.Message}");
                    return;
                }
            }

            // Assembly source (.asm) or text: hand to the Assembly runtime
            if (string.Equals(ext, ".asm", StringComparison.OrdinalIgnoreCase) || IsProbablyText(bytes))
            {
                try
                {
                    var host = new StarChart.Assembly.StarChartAssemblyHost();
                    host.Call("exec:" + path, new AssemblyRuntimeContext());
                    return;
                }
                catch (Exception ex)
                {
                    StarChart.stdlib.Sys.Console.WriteLine($"exec: assembly runtime failed: {ex.Message}");
                    return;
                }
            }

            StarChart.stdlib.Sys.Console.WriteLine("exec: unsupported file type");
        }

        static bool IsProbablyText(byte[] bytes)
        {
            var len = Math.Min(bytes.Length, 512);
            for (int i = 0; i < len; i++)
            {
                if (bytes[i] == 0) return false;
            }
            return true;
        }
    }
}
