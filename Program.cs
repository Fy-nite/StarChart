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
            int? printFrames = null;
            for (int i = 0; i < args.Length; i++)
            {
                if ((args[i] == "--exec" || args[i] == "-c") && i + 1 < args.Length)
                {
                    execCmd = args[i + 1];
                    i++;
                }
                else if ((args[i] == "--print-frames" || args[i] == "-p") && i + 1 < args.Length)
                {
                    if (int.TryParse(args[i + 1], out var v)) printFrames = v;
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
                // Register small symlink marker files for builtin programs
                try
                {
                    StarChart.Bin.Symlinks.RegisterDefaultSymlinks(vfs);
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
            using var game = new AsmoV2.AsmoGameEngine("StarChart", 1280, 800);

            // Configure window policy so the engine will scale the canvas based on window size
            game.Policy = new AsmoV2.AsmoGameEngine.WindowPolicy
            {
                AllowUserResizing = true,
                LockSize = false,
                StartFullscreen = false,
                SizeToDisplay = false,
                AltEnterToggle = true
            };
            // Pass whether the shell requested graphical startup so the Runtime can
            // avoid creating a fullscreen VT session and instead start W11 properly.
            
            var runtime = new Runtime(ShellControl.StartGraphicalRequested);
            if (printFrames.HasValue)
            {
                Runtime.DrawPrintLimit = printFrames.Value;
            }
            game.HostNativeGame(runtime);
            game.Run();
        }
    }
}
