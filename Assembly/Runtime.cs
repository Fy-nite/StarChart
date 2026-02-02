using System;
using System.Collections.Generic;
using System.Text;
using Jakarada.Core;
using Jakarada.Core.AST;

namespace StarChart.Assembly
{
    /// <summary>
    /// Represents the mutable execution context exposed to hosts so they can inspect or modify registers/memory.
    /// </summary>
    public class AssemblyRuntimeContext
    {
        public Dictionary<string, long> Registers { get; } = new(StringComparer.OrdinalIgnoreCase);
        public byte[] Memory { get; }

        public AssemblyRuntimeContext(int memorySize = 64 * 1024)
        {
            Memory = new byte[memorySize];

            // Initialize common registers
            var regs = new[] { "rax", "rbx", "rcx", "rdx", "rsi", "rdi", "rbp", "rsp", "rip" };
            foreach (var r in regs)
                Registers[r] = 0;

            // Stack pointer starts at end of memory
            Registers["rsp"] = Memory.Length - 1;
        }
    }

    /// <summary>
    /// Host interface for exposing platform / StarChart functions to the assembly runtime.
    /// </summary>
    public interface IAssemblyHost
    {
        void Call(string name, AssemblyRuntimeContext ctx);
        void Exit(int code);
        void Write(string text);
        void WriteLine(string text);
    }

    /// <summary>
    /// Simple console-only host implementation. Users can replace this with integration to StarChart functions.
    /// </summary>
    public class ConsoleAssemblyHost : IAssemblyHost
    {
        public void Call(string name, AssemblyRuntimeContext ctx)
        {
            // Example host calls. Extend to hook into StarChart.
            if (string.Equals(name, "print_regs", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var kv in ctx.Registers)
                    Console.WriteLine($"{kv.Key} = {kv.Value}");
            }
            else if (string.Equals(name, "print_rax", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(ctx.Registers.GetValueOrDefault("rax"));
            }
            else
            {
                Console.WriteLine($"[host] unknown host call: {name}");
            }
        }

        public void Exit(int code)
        {
            Environment.Exit(code);
        }

        public void Write(string text)
        {
            Console.Write(text);
        }

        public void WriteLine(string text)
        {
            Console.WriteLine(text);
        }
    }

    /// <summary>
    /// Very small x86-like interpreter that executes the AST produced by Jakarada.Core.
    /// It intentionally implements a tiny subset of instructions and provides a host-call
    /// mechanism so StarChart functions can be invoked from assembly using the "HOST" mnemonic.
    /// </summary>
    public class AssemblyRuntime
    {
        private readonly AssemblyRuntimeContext _ctx;
        private readonly IAssemblyHost _host;

        public AssemblyRuntime(IAssemblyHost? host = null, int memorySize = 64 * 1024)
        {
            _ctx = new AssemblyRuntimeContext(memorySize);
            _host = host ?? new ConsoleAssemblyHost();
        }

        public AssemblyRuntimeContext Context => _ctx;

        public void RunSource(string assemblyCode)
        {
            var program = AssemblyReader.Parse(assemblyCode);
            RunProgram(program);
        }

        public void RunProgram(ProgramNode program)
        {
            // Build a map of labels -> instruction index
            var labelToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var instructions = program.Instructions;

            for (int i = 0; i < instructions.Count; i++)
            {
                var ins = instructions[i];
                if (!string.IsNullOrEmpty(ins.Label))
                    labelToIndex[ins.Label] = i;
            }

            // Basic execution state
            var callStack = new Stack<int>();
            bool zf = false;

            int ip = 0;
            while (ip >= 0 && ip < instructions.Count)
            {
                var ins = instructions[ip];
                ip++; // default increment

                var m = ins.Mnemonic.ToUpperInvariant();
                try
                {
                    switch (m)
                    {
                        case "MOV":
                            ExecuteMov(ins);
                            break;
                        case "ADD":
                            ExecuteAdd(ins);
                            break;
                        case "SUB":
                            ExecuteSub(ins);
                            break;
                        case "CMP":
                            zf = ExecuteCmp(ins);
                            break;
                        case "JMP":
                            {
                                var target = GetLabelOperand(ins, 0);
                                if (labelToIndex.TryGetValue(target, out var idx))
                                {
                                    ip = idx;
                                }
                                else
                                    throw new InvalidOperationException($"Unknown label '{target}'");
                            }
                            break;
                        case "JE":
                            {
                                var target = GetLabelOperand(ins, 0);
                                if (zf)
                                {
                                    if (labelToIndex.TryGetValue(target, out var idx))
                                        ip = idx;
                                    else
                                        throw new InvalidOperationException($"Unknown label '{target}'");
                                }
                            }
                            break;
                        case "JNE":
                            {
                                var target = GetLabelOperand(ins, 0);
                                if (!zf)
                                {
                                    if (labelToIndex.TryGetValue(target, out var idx))
                                        ip = idx;
                                    else
                                        throw new InvalidOperationException($"Unknown label '{target}'");
                                }
                            }
                            break;
                        case "CALL":
                            {
                                // Call a label
                                var target = GetLabelOperand(ins, 0);
                                if (labelToIndex.TryGetValue(target, out var idx))
                                {
                                    callStack.Push(ip);
                                    ip = idx;
                                }
                                else
                                    throw new InvalidOperationException($"Unknown label '{target}'");
                            }
                            break;
                        case "RET":
                            if (callStack.Count == 0)
                                return;
                            ip = callStack.Pop();
                            break;
                        case "HOST":
                            {
                                // HOST <identifier>
                                if (ins.Operands.Count == 0 || ins.Operands[0] is not LabelReferenceOperand lr)
                                    throw new InvalidOperationException("HOST requires an identifier operand");

                                _host.Call(lr.LabelName, _ctx);
                            }
                            break;
                        case "SYS":
                            {
                                // SYS [imm] -> if immediate provided, call specific syscall number, otherwise use rax
                                if (ins.Operands.Count == 0)
                                {
                                    _host.Call("syscall", _ctx);
                                }
                                else if (ins.Operands[0] is ImmediateOperand imm)
                                {
                                    _host.Call("syscall:" + ((int)imm.Value), _ctx);
                                }
                                else
                                {
                                    throw new InvalidOperationException("SYS requires an immediate operand or no operands");
                                }
                            }
                            break;
                        case "SYSCALL":
                            {
                                // SYSCALL -> use registers (rax etc.) to determine syscall
                                _host.Call("syscall", _ctx);
                            }
                            break;
                        default:
                            // Unknown instruction: ignore or expand as needed
                            Console.WriteLine($"[runtime] unhandled instruction: {m}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error executing instruction {ins.Mnemonic} at line {ins.LineNumber}: {ex.Message}", ex);
                }
            }
        }

        private string ReadString(int addr)
        {
            if (addr < 0 || addr >= _ctx.Memory.Length)
                return string.Empty;

            var bytes = new List<byte>();
            for (int i = addr; i < _ctx.Memory.Length; i++)
            {
                var b = _ctx.Memory[i];
                if (b == 0) break;
                bytes.Add(b);
            }

            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        private static string GetLabelOperand(InstructionNode ins, int index)
        {
            if (ins.Operands.Count <= index) throw new InvalidOperationException("Missing operand");
            if (ins.Operands[index] is LabelReferenceOperand lr) return lr.LabelName;
            if (ins.Operands[index] is ImmediateOperand imm) return imm.Value.ToString();
            throw new InvalidOperationException("Expected label operand");
        }

        private void ExecuteMov(InstructionNode ins)
        {
            if (ins.Operands.Count < 2) throw new InvalidOperationException("MOV requires two operands");
            var dest = ins.Operands[0];
            var src = ins.Operands[1];

            if (dest is RegisterOperand rd)
            {
                var val = ReadOperandValue(src);
                _ctx.Registers[rd.Name] = val;
            }
            else
            {
                throw new InvalidOperationException("Only register destinations are supported for MOV");
            }
        }

        private void ExecuteAdd(InstructionNode ins)
        {
            if (ins.Operands.Count < 2) throw new InvalidOperationException("ADD requires two operands");
            if (ins.Operands[0] is not RegisterOperand rd) throw new InvalidOperationException("ADD destination must be a register");
            var a = _ctx.Registers.GetValueOrDefault(rd.Name);
            var b = ReadOperandValue(ins.Operands[1]);
            _ctx.Registers[rd.Name] = a + b;
        }

        private void ExecuteSub(InstructionNode ins)
        {
            if (ins.Operands.Count < 2) throw new InvalidOperationException("SUB requires two operands");
            if (ins.Operands[0] is not RegisterOperand rd) throw new InvalidOperationException("SUB destination must be a register");
            var a = _ctx.Registers.GetValueOrDefault(rd.Name);
            var b = ReadOperandValue(ins.Operands[1]);
            _ctx.Registers[rd.Name] = a - b;
        }

        private bool ExecuteCmp(InstructionNode ins)
        {
            if (ins.Operands.Count < 2) throw new InvalidOperationException("CMP requires two operands");
            var a = ReadOperandValue(ins.Operands[0]);
            var b = ReadOperandValue(ins.Operands[1]);
            return a == b;
        }

        private long ReadOperandValue(OperandNode op)
        {
            switch (op)
            {
                case RegisterOperand r:
                    return _ctx.Registers.GetValueOrDefault(r.Name);
                case ImmediateOperand imm:
                    return imm.Value;
                case LabelReferenceOperand lr:
                    // Labels can be used as immediates (address 0 by default)
                    // Caller may extend this to return actual addresses mapped to labels.
                    return 0;
                default:
                    throw new InvalidOperationException($"Unsupported operand type {op.GetType().Name}");
            }

        }
    }

}