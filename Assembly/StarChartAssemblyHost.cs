using System;
using System.Text;
using Adamantite.VFS;

namespace StarChart.Assembly
{
    /// <summary>
    /// Host that integrates the assembly runtime with StarChart's VFS. It allows executing
    /// assembly source files stored in the virtual file system via host calls like
    /// "HOST exec:/path/to/file.asm" or "HOST exec" where the filename pointer is placed in RAX.
    /// </summary>
    public class StarChartAssemblyHost : IAssemblyHost
    {
        readonly VfsManager? _vfs;

        public StarChartAssemblyHost()
        {
            _vfs = VFSGlobal.Manager;
        }

        public void Call(string name, AssemblyRuntimeContext ctx)
        {
            if (string.IsNullOrEmpty(name)) return;

            // Support HOST exec:/path/to/file.asm
            if (name.StartsWith("exec:", StringComparison.OrdinalIgnoreCase))
            {
                var path = name.Substring(5).Trim();
                ExecPath(path, ctx);
                return;
            }

            // Support HOST exec -> filename pointer in rax (zero-terminated string in runtime memory)
            if (string.Equals(name, "exec", StringComparison.OrdinalIgnoreCase))
            {
                var addr = (int)ctx.Registers.GetValueOrDefault("rax");
                var path = ReadStringFromMemory(ctx, addr);
                ExecPath(path, ctx);
                return;
            }

            // Fallback: basic console commands
            if (string.Equals(name, "print_regs", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var kv in ctx.Registers)
                    Console.WriteLine($"{kv.Key} = {kv.Value}");
                return;
            }

            if (string.Equals(name, "print_rax", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(ctx.Registers.GetValueOrDefault("rax"));
                return;
            }

            Console.WriteLine($"[StarChartHost] unknown host call: {name}");
        }

        public void Exit(int code)
        {
            Environment.Exit(code);
        }

        public void Write(string text)
        {
            throw new NotImplementedException();
        }

        public void WriteLine(string text)
        {
            throw new NotImplementedException();
        }

        void ExecPath(string path, AssemblyRuntimeContext ctx)
        {
            if (string.IsNullOrEmpty(path))
            {
                Console.WriteLine("[StarChartHost] empty path");
                return;
            }

            if (_vfs == null)
            {
                Console.WriteLine("[StarChartHost] VFS not initialized");
                return;
            }

            if (!_vfs.Exists(path))
            {
                Console.WriteLine($"[StarChartHost] file not found: {path}");
                return;
            }

            try
            {
                var bytes = _vfs.ReadAllBytes(path);
                var src = Encoding.UTF8.GetString(bytes);

                var runtime = new AssemblyRuntime(this);
                runtime.RunSource(src);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StarChartHost] failed to exec {path}: {ex.Message}");
            }
        }

        string ReadStringFromMemory(AssemblyRuntimeContext ctx, int addr)
        {
            if (addr < 0 || addr >= ctx.Memory.Length) return string.Empty;
            var i = addr;
            var sb = new StringBuilder();
            while (i < ctx.Memory.Length)
            {
                var b = ctx.Memory[i++];
                if (b == 0) break;
                sb.Append((char)b);
            }
            return sb.ToString();
        }
    }
}
