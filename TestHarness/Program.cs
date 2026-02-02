using System;
using System.IO;
using StarChart.Assembly;

namespace StarChart.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            var code = File.ReadAllText("/home/charlie/git/VBlank/StarChart/bin/Debug/net10.0/root/data_test.asm");
            var runtime = new AssemblyRuntime();
            runtime.RunSource(code);

            // Verify memory at known offsets based on data_test.asm content
            // Assuming data_test.asm:
            // val1 dq 0x123456789ABCDEF0
            // val2 dd 0xDEADBEEF
            // val3 dw 0xCAFE
            // val4 db 0xAA
            // ptr1 dq val3 (forward ref)
            
            // Expected offsets:
            // val1: 0 (8 bytes)
            // val2: 8 (4 bytes)
            // val3: 12 (2 bytes)
            // val4: 14 (1 byte)
            // ptr1: 15 (8 bytes)
            
            var mem = runtime.Context.Memory;
            
            // val1 check (Little Endian)
            ulong val1 = BitConverter.ToUInt64(mem, 0);
            if (val1 != 0x123456789ABCDEF0) throw new Exception($"val1 failed: {val1:X}");

            // val2 check
            uint val2 = BitConverter.ToUInt32(mem, 8);
            if (val2 != 0xDEADBEEF) throw new Exception($"val2 failed: {val2:X}");

            // val3 check
            ushort val3 = BitConverter.ToUInt16(mem, 12);
            if (val3 != 0xCAFE) throw new Exception($"val3 failed: {val3:X}");

            // val4 check
            byte val4 = mem[14];
            if (val4 != 0xAA) throw new Exception($"val4 failed: {val4:X}");

            // ptr1 check (should point to val3 which is at offset 12 = 0xC)
            ulong ptr1 = BitConverter.ToUInt64(mem, 15);
            if (ptr1 != 12) throw new Exception($"ptr1 failed link: {ptr1} != 12");

            Console.WriteLine("Data test passed!");
        }
    }
}
