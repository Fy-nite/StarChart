using AsmoV2;
using System;
using System.IO;
using System.Text;
using Adamantite.VFS;
using StarChart.Bin;

namespace StarChart
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string? execCmd = null;
            for (int i = 0; i < args.Length; i++)
            {
                if ((args[i] == "--exec" || args[i] == "-c") && i + 1 < args.Length)
                {
                    execCmd = args[i + 1];
                    i++;
                }
            }

            // Prepare a VFS instance that the headless console shell can use.
            var vfs = new VfsManager();
            try
            {
                var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                var rootPath = Path.Combine(exeDir, "root");
                vfs.Mount("/", new Adamantite.VFS.PhysicalFileSystem(rootPath));
                var memFs = new Adamantite.VFS.InMemoryFileSystem();
                vfs.Mount("/mem", memFs);
                vfs.Mount("/bin", memFs);

                try
                {
                    memFs.CreateDirectory("/mem/bin");
                }
                catch { }

                try
                {
                    var shBytes = Encoding.UTF8.GetBytes("#!/bin/sh\n# minimal sh stub\necho \"StarChart sh stub\"\n");
                    vfs.WriteAllBytes("/mem/bin/sh", shBytes);
                    memFs.CreateSymlink("sh", "/mem/bin/sh");
                }
                catch { }
            }
            catch { }

            // Expose the VFS globally so the runtime/GUI will reuse it.
            VFSGlobal.Manager = vfs;

            // Run a simple headless console shell first. Typing 'startx' will return and start the GUI.
            if (!ConsoleShell.Run(vfs, execCmd))
            {
                return;
            }

            // Now start the MonoGame windowed runtime (W11)
            using var game = new AsmoV2.AsmoGameEngine("StarChart", 640, 480);

            // Configure window policy so the engine will scale the canvas based on window size
            game.Policy = new AsmoV2.AsmoGameEngine.WindowPolicy
            {
                AllowUserResizing = true,
                LockSize = false,
                StartFullscreen = false,
                SizeToDisplay = false,
                AltEnterToggle = true
            };
            game.HostNativeGame(new Runtime());
            game.Run();
        }
    }
}
