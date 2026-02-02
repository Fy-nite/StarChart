using System;
using System.Text;
using System.Reflection;
using System.Diagnostics;
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

            // Support HOST syscall -> emulate Linux-like syscalls using registers
            if (string.Equals(name, "syscall", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("syscall:", StringComparison.OrdinalIgnoreCase))
            {
                int code = -1;
                if (name.StartsWith("syscall:", StringComparison.OrdinalIgnoreCase))
                {
                    var s = name.Substring(8).Trim();
                    if (!int.TryParse(s, out code)) code = -1;
                }
                HandleSyscall(code, ctx);
                return;
            }

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

                // Try to load as a managed assembly first (DLL/EXE).
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
                                // pass empty string[] for args
                                invokeArgs = new object[] { Array.Empty<string>() };
                            }
                            entry.Invoke(null, invokeArgs);
                            return;
                        }
                        catch (TargetInvocationException tie)
                        {
                            Console.WriteLine($"[StarChartHost] managed entry threw: {tie.InnerException?.Message ?? tie.Message}");
                            return;
                        }
                    }
                }
                catch (BadImageFormatException)
                {
                    // Not a managed assembly -- fall through to try as source
                }
                catch (Exception exAsm)
                {
                    Console.WriteLine($"[StarChartHost] failed to load assembly {path}: {exAsm.Message}");
                    return;
                }

                // Fallback: treat file as assembly source text (existing behavior)
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

        void HandleSyscall(int forcedCode, AssemblyRuntimeContext ctx)
        {
            // Determine syscall number from forcedCode or rax
            int num = forcedCode >= 0 ? forcedCode : (int)ctx.Registers.GetValueOrDefault("rax");

            // Common x86_64 Linux-like syscalls (minimal subset)
            // 0 = read, 1 = write, 59 = execve, 60 = exit
            try
            {
                switch (num)
                {
                    case 0: // read(fd, buf, count)
                        {
                            int fd = (int)ctx.Registers.GetValueOrDefault("rdi");
                            int buf = (int)ctx.Registers.GetValueOrDefault("rsi");
                            int count = (int)ctx.Registers.GetValueOrDefault("rdx");
                            if (buf < 0 || buf >= ctx.Memory.Length) { ctx.Registers["rax"] = -1; break; }
                            if (count <= 0) { ctx.Registers["rax"] = 0; break; }

                            if (fd == 0)
                            {
                                // Read a line from stdin
                                var line = System.Console.ReadLine() ?? string.Empty;
                                var bytes = Encoding.UTF8.GetBytes(line + "\n");
                                var toWrite = Math.Min(count, Math.Min(bytes.Length, ctx.Memory.Length - buf));
                                Array.Copy(bytes, 0, ctx.Memory, buf, toWrite);
                                ctx.Registers["rax"] = toWrite;
                                break;
                            }
                            // other fds not supported
                            ctx.Registers["rax"] = -1;
                            break;
                        }
                    case 1: // write(fd, buf, count)
                        {
                            int fd = (int)ctx.Registers.GetValueOrDefault("rdi");
                            int buf = (int)ctx.Registers.GetValueOrDefault("rsi");
                            int count = (int)ctx.Registers.GetValueOrDefault("rdx");
                            if (buf < 0 || buf >= ctx.Memory.Length) { ctx.Registers["rax"] = -1; break; }
                            var avail = Math.Min(count, ctx.Memory.Length - buf);
                            if (avail <= 0) { ctx.Registers["rax"] = 0; break; }
                            var bytes = new byte[avail];
                            Array.Copy(ctx.Memory, buf, bytes, 0, avail);
                            var s = Encoding.UTF8.GetString(bytes);
                            if (fd == 1 || fd == 2)
                            {
                                System.Console.Write(s);
                                ctx.Registers["rax"] = avail;
                            }
                            else
                            {
                                // Unsupported fd
                                ctx.Registers["rax"] = -1;
                            }
                            break;
                        }
                    case 59: // execve(filename, argv, envp)
                        {
                            int fnamePtr = (int)ctx.Registers.GetValueOrDefault("rdi");
                            var fname = ReadStringFromMemory(ctx, fnamePtr);
                            if (string.IsNullOrEmpty(fname)) { ctx.Registers["rax"] = -1; break; }
                            // Attempt to exec path via existing ExecPath
                            ExecPath(fname, ctx);
                            ctx.Registers["rax"] = 0;
                            break;
                        }
                    case 60: // exit(status)
                        {
                            int status = (int)ctx.Registers.GetValueOrDefault("rdi");
                            Exit(status);
                            break;
                        }
                    default:
                        Console.WriteLine($"[StarChartHost] unknown syscall {num}");
                        ctx.Registers["rax"] = -1;
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StarChartHost] syscall {num} failed: {ex.Message}");
                ctx.Registers["rax"] = -1;
            }
        }
    }
}
